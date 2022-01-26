#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using librsync.net;
using UnityEngine;
using VRC;
using VRC.Core;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

using static VRC.Core.ApiFileHelper;

namespace BocuD.VRChatApiTools
{ 
    public class ApiFileHelperAsync
    {
        //constants from the sdk
        private const int kMultipartUploadChunkSize = 50 * 1024 * 1024; // 50 MB <- is 100MB in the SDK, modified here because 25MB makes more sense
        private const int SERVER_PROCESSING_WAIT_TIMEOUT_CHUNK_SIZE = 50 * 1024 * 1024;
        private const float SERVER_PROCESSING_WAIT_TIMEOUT_PER_CHUNK_SIZE = 120.0f;
        private const float SERVER_PROCESSING_MAX_WAIT_TIMEOUT = 600.0f;
        private const float SERVER_PROCESSING_INITIAL_RETRY_TIME = 2.0f;
        private const float SERVER_PROCESSING_MAX_RETRY_TIME = 10.0f;
        
        //status strings
        private const string prepareFileMessage = "Preparing file for upload...";
        private const string prepareRemoteMessage = "Preparing server for upload...";
        private const string postUploadMessage = "Processing upload...";

        //delay in milliseconds after each write
        private const int postWriteDelay = 750;
        
        //global flow control
        private ApiFile apiFile;
        private static bool wait;
        private static string errorStr;
        private static bool worthRetry;

        private void FileSuccess(ApiContainer c)
        {
            apiFile = c.Model as ApiFile;
            wait = false;
        }

        private void FileFailure(ApiContainer c)
        {
            errorStr = c.Error;
            wait = false;
            
            Logger.LogError(errorStr);
        }

        public delegate void UploadSuccess(ApiFile apiFile, string succes);
        public delegate void UploadError(string error, string moreinfo = "");
        public delegate void UploadProgress(string status, string subStatus, float progress = 0);

        public async Task UploadFile(string filename, string existingFileId, string fileType, string friendlyName,
            UploadSuccess onSuccess, UploadError onError, UploadProgress onProgress, Func<bool> cancelQuery)
        {
            Logger.Log($"ApiFileHelper Async Starting for: name: {friendlyName}, filename: {filename}, file id: {(!string.IsNullOrEmpty(existingFileId) ? existingFileId : "<new>")}");

            //Init remote config
            if (!await InitRemoteConfig(onError)) return;
            
            bool deltaCompression = ConfigManager.RemoteConfig.GetBool("sdkEnableDeltaCompression");
            Logger.Log(deltaCompression ? "Using delta compression" : "Delta compression disabled");

            //Check filename
            if (!CheckFile(filename, onProgress, onError)) return;
            
            bool wasError = false;

            //Fetch Api File Record
            apiFile = await FetchRecord(filename, existingFileId, friendlyName, onError, onProgress, cancelQuery);
            if (apiFile == null)
                return;

            Logger.Log("Fetched record succesfully");

            if (apiFile.HasQueuedOperation(deltaCompression))
            {
                //delete last version
                onProgress?.Invoke(prepareRemoteMessage, "Cleaning up previous version");

                ApiContainer delete = await apiFile.DeleteLatestVersionAsync();

                if (!delete.Error.IsNullOrWhitespace())
                {
                    onError?.Invoke($"Couldn't delete last version", delete.Error);
                    return;
                }
                
                // delay to let write get through servers
                await Task.Delay(postWriteDelay);
            }

            // check for server side errors from last upload
            if (await HandleFileErrorState(onError, onProgress)) return;

            // verify previous file op is complete
            if (apiFile.HasQueuedOperation(deltaCompression))
            {
                onError?.Invoke("Can't initiate upload", "A previous upload is still being processed. Please try again later.");
                return;
            }
            
            //gemerate file md5
            onProgress?.Invoke(prepareFileMessage, "Generating file hash");
            string fileMD5Base64 = await GenerateMD5Base64(filename, onError, cancelQuery);
            
            if (fileMD5Base64.IsNullOrWhitespace()) return;

            // check if file has been changed
            if (await CheckForExistingVersion(onSuccess, onError, onProgress, fileMD5Base64)) return;
            
            //generate signature file for new upload
            onProgress?.Invoke(prepareFileMessage, "Generating signature");

            string signatureFilename = Tools.GetTempFileName(".sig", out errorStr, apiFile.id);
            if (string.IsNullOrEmpty(signatureFilename))
            {
                onError?.Invoke("Failed to generate file signature", $"Failed to create temp file: {errorStr}");
                CleanupTempFiles(apiFile.id);
                return;
            }

            if(!await CreateFileSignatureInternal(filename, signatureFilename, onError))
            {
                CleanupTempFiles(apiFile.id);
                return;
            }

            // generate signature md5 and file size
            onProgress?.Invoke(prepareFileMessage, "Generating signature hash");

            string sigMD5Base64 = await GenerateMD5Base64(signatureFilename, onError, cancelQuery);

            if (sigMD5Base64.IsNullOrWhitespace()) return;

            if (!Tools.GetFileSize(signatureFilename, out long sigFileSize, out errorStr))
            {
                onError?.Invoke("Failed to generate file signature", $"Couldn't get file size: {errorStr}");
                CleanupTempFiles(apiFile.id);
                return;
            }
            
            // download previous version signature (if exists)
            string existingFileSignaturePath = null;
            if (deltaCompression && apiFile.HasExistingVersion())
            {
                existingFileSignaturePath = await GetExistingFileSignature(onError, onProgress, cancelQuery);
                if (existingFileSignaturePath.IsNullOrWhitespace()) return;
            }
            
            // create delta if needed
            string deltaFilename = null;

            if (deltaCompression && !string.IsNullOrEmpty(existingFileSignaturePath))
            {
                onProgress?.Invoke(prepareFileMessage, "Creating file delta");
                deltaFilename = await CreateFileDelta(filename, onError, existingFileSignaturePath);
                
                if (deltaFilename == null) return;
            }

            // upload smaller of delta and new file
            long deltaFileSize = 0;
            
            //get filesize
            if (!Tools.GetFileSize(filename, out long fullFileSize, out errorStr) ||
                !string.IsNullOrEmpty(deltaFilename) && !Tools.GetFileSize(deltaFilename, out deltaFileSize, out errorStr))
            {
                onError("Failed to create file delta for upload", $"Couldn't get file size: {errorStr}");
                CleanupTempFiles(apiFile.id);
                return;
            }

            bool uploadDeltaFile = deltaCompression && deltaFileSize > 0 && deltaFileSize < fullFileSize;
            Logger.Log(deltaCompression
                    ? $"Delta size {deltaFileSize} ({(deltaFileSize / (float)fullFileSize)} %), full file size {fullFileSize}, uploading {(uploadDeltaFile ? " DELTA" : " FULL FILE")}"
                    : $"Delta compression disabled, uploading FULL FILE, size {fullFileSize}");
            
            //generate MD5 for delta file
            string deltaMD5Base64 = "";
            if (uploadDeltaFile)
            {
                onProgress?.Invoke(prepareFileMessage, "Generating file delta hash");
                deltaMD5Base64 = await GenerateMD5Base64(deltaFilename, onError, cancelQuery);
                if (deltaMD5Base64.IsNullOrWhitespace()) return;
            }

            // validate existing pending version info, if this is a retry
            bool versionAlreadyExists = false;
            
            if (isPreviousUploadRetry)
            {
                bool isValid;

                ApiFile.Version v = apiFile.GetVersion(apiFile.GetLatestVersionNumber());
                if (v != null)
                {
                    //make sure fileSize for file and signature need to match their respective remote versions
                    //make sure MD5 for file and signature match their respective remote versions
                    if (uploadDeltaFile)
                    {
                        isValid = deltaFileSize == v.delta.sizeInBytes &&
                                  string.Compare(deltaMD5Base64, v.delta.md5, StringComparison.Ordinal) == 0 &&
                                  sigFileSize == v.signature.sizeInBytes &&
                                  string.Compare(sigMD5Base64, v.signature.md5, StringComparison.Ordinal) == 0;
                    }
                    else
                    {
                        isValid = fullFileSize == v.file.sizeInBytes &&
                                  string.Compare(fileMD5Base64, v.file.md5, StringComparison.Ordinal) == 0 &&
                                  sigFileSize == v.signature.sizeInBytes &&
                                  string.Compare(sigMD5Base64, v.signature.md5, StringComparison.Ordinal) == 0;
                    }
                }
                else
                {
                    isValid = false;
                }

                if (isValid)
                {
                    versionAlreadyExists = true;

                    Logger.Log("Using existing version record");
                }
                else
                {
                    // delete previous invalid version
                    onProgress?.Invoke(prepareRemoteMessage, "Cleaning up previous version");

                    ApiContainer delete = await apiFile.DeleteLatestVersionAsync();

                    if (!delete.Error.IsNullOrWhitespace())
                    {
                        onError?.Invoke("Couldn't delete last version", delete.Error);
                        return;
                    }
                }
            }
            
            //create new file record
            if (!versionAlreadyExists)
            {
                if (uploadDeltaFile)
                {
                    if (await CreateFileRecord(deltaMD5Base64, deltaFileSize, sigMD5Base64, sigFileSize, onError, onProgress, cancelQuery)) return;
                }
                else
                {
                    if (await CreateFileRecord(fileMD5Base64, fullFileSize, sigMD5Base64, sigFileSize, onError, onProgress, cancelQuery)) return;
                }
            }

            // upload components
            string uploadFileName = uploadDeltaFile ? deltaFilename : filename;
            string uploadMD5 = uploadDeltaFile ? deltaMD5Base64 : fileMD5Base64;
            long uploadFileSize = uploadDeltaFile ? deltaFileSize : fullFileSize;

            switch (uploadDeltaFile)
            {
                case true when apiFile.GetLatestVersion().delta.status == ApiFile.Status.Waiting:
                case false when apiFile.GetLatestVersion().status == ApiFile.Status.Waiting:
                    onProgress?.Invoke($"Uploading {fileType}...", $"Uploading file{(uploadDeltaFile ? " delta..." : "...")}");

                    await UploadFileComponentInternal(apiFile, uploadDeltaFile ? ApiFile.Version.FileDescriptor.Type.delta : ApiFile.Version.FileDescriptor.Type.file, 
                        uploadFileName, uploadMD5, uploadFileSize,
                        file =>
                        {
                            Logger.Log($"Successfully uploaded file{(uploadDeltaFile ? " delta" : "")}");
                            apiFile = file;
                        },
                        error =>
                        {
                            onError?.Invoke($"Failed to upload file{(uploadDeltaFile ? " delta" : "")}", error);
                            CleanupTempFiles(apiFile.id);
                            wasError = true;
                        },
                        (downloaded, length) => onProgress?.Invoke($"Uploading {fileType}...",
                            $"Uploading file{(uploadDeltaFile ? " delta..." : "...")}", Tools.DivideSafe(downloaded, length)), cancelQuery);

                    if (wasError)
                        return;
                    break;
            }
            
            // upload signature
            if (apiFile.GetLatestVersion().signature.status == ApiFile.Status.Waiting)
            {
                onProgress?.Invoke($"Uploading {fileType}...", "Uploading file signature...");

                await UploadFileComponentInternal(apiFile,
                    ApiFile.Version.FileDescriptor.Type.signature, signatureFilename, sigMD5Base64, sigFileSize,
                    file =>
                    {
                        Logger.Log("Successfully uploaded file signature.");
                        apiFile = file;
                    },
                    error =>
                    {
                        onError?.Invoke("Failed to upload file signature", error);
                        CleanupTempFiles(apiFile.id);
                        wasError = true;
                    },
                    (downloaded, length) => onProgress?.Invoke($"Uploading {fileType}...", 
                        "Uploading file signature...", Tools.DivideSafe(downloaded, length)),
                    cancelQuery);

                if (wasError)
                    return;
            }
            
            // Validate file records queued or complete
            onProgress?.Invoke($"Uploading {fileType}...", "Validating upload...");

            bool isUploadComplete = uploadDeltaFile
                ? apiFile.GetFileDescriptor(apiFile.GetLatestVersionNumber(), ApiFile.Version.FileDescriptor.Type.delta)
                    .status == ApiFile.Status.Complete
                : apiFile.GetFileDescriptor(apiFile.GetLatestVersionNumber(), ApiFile.Version.FileDescriptor.Type.file)
                    .status == ApiFile.Status.Complete;
            isUploadComplete = isUploadComplete && apiFile.GetFileDescriptor(apiFile.GetLatestVersionNumber(),
                                   ApiFile.Version.FileDescriptor.Type.signature).status == ApiFile.Status.Complete;

            if (!isUploadComplete)
            {
                onError?.Invoke("Failed to upload file", "Record status is not 'complete'");
                CleanupTempFiles(apiFile.id);
                return;
            }

            bool isServerOpQueuedOrComplete = uploadDeltaFile
                ? apiFile.GetFileDescriptor(apiFile.GetLatestVersionNumber(), ApiFile.Version.FileDescriptor.Type.file)
                    .status != ApiFile.Status.Waiting
                : apiFile.GetFileDescriptor(apiFile.GetLatestVersionNumber(), ApiFile.Version.FileDescriptor.Type.delta)
                    .status != ApiFile.Status.Waiting;

            if (!isServerOpQueuedOrComplete)
            {
                onError?.Invoke("Failed to upload file", "Previous version is still in waiting status");
                CleanupTempFiles(apiFile.id);
                return;
            }
            
            // wait for server processing to complete
            onProgress?.Invoke(postUploadMessage, "Checking file status");
            
            float checkDelay = SERVER_PROCESSING_INITIAL_RETRY_TIME;
            float timeout = GetServerProcessingWaitTimeoutForDataSize(apiFile.GetLatestVersion().file.sizeInBytes);
            double initialStartTime = Time.realtimeSinceStartup;
            double startTime = initialStartTime;
            
            while (apiFile.HasQueuedOperation(uploadDeltaFile))
            {
                // wait before polling again
                onProgress?.Invoke(postUploadMessage, $"Checking status in {Mathf.CeilToInt(checkDelay)} seconds");

                while (Time.realtimeSinceStartup - startTime < checkDelay)
                {
                    if (Time.realtimeSinceStartup - initialStartTime > timeout)
                    {
                        onError("Couldn't verify upload", "Timed out waiting for upload processing to complete.");
                        CleanupTempFiles(apiFile.id);
                        return;
                    }

                    await Task.Delay(33);
                }

                while (true)
                {
                    // check status
                    onProgress?.Invoke(postUploadMessage, "Checking status...");

                    wait = true;
                    worthRetry = false;
                    errorStr = "";
                    API.Fetch<ApiFile>(apiFile.id, FileSuccess, FileFailure);

                    while (wait)
                    {
                        if (cancelQuery())
                        {
                            CleanupTempFiles(apiFile.id);
                            return;
                        }

                        await Task.Delay(33);
                    }

                    if (!string.IsNullOrEmpty(errorStr))
                    {
                        onError?.Invoke("Checking upload status failed.", errorStr);
                        if (!worthRetry)
                        {
                            CleanupTempFiles(apiFile.id);
                            return;
                        }
                    }

                    if (!worthRetry)
                        break;
                }

                checkDelay = Mathf.Min(checkDelay * 2, SERVER_PROCESSING_MAX_RETRY_TIME);
                startTime = Time.realtimeSinceStartup;
            }

            // cleanup and wait for it to finish
            await CleanupTempFilesInternal(apiFile.id);

            onSuccess(apiFile, "Upload succesful!");
        }

        private async Task<string> CreateFileDelta(string filename, UploadError onError, string existingFileSignaturePath)
        {
            string deltaFilenameTemp = Tools.GetTempFileName(".delta", out errorStr, apiFile.id);
            
            if (string.IsNullOrEmpty(deltaFilenameTemp))
            {
                onError?.Invoke("Failed to create file data for upload", $"Failed to create temp file: {errorStr}");
                CleanupTempFiles(apiFile.id);
                return null;
            }

            string deltaFilename = null;
            
            await CreateFileDeltaInternal(filename, existingFileSignaturePath, deltaFilenameTemp,
                () => deltaFilename = deltaFilenameTemp,
                error =>
                {
                    onError?.Invoke("Failed to create file delta for upload", error);
                    CleanupTempFiles(apiFile.id);
                });

            return deltaFilename;
        }

        private async Task<bool> CreateFileRecord(string fileMD5Base64, long fileSize, string sigMD5Base64,
            long sigFileSize, UploadError onError, UploadProgress onProgress, Func<bool> cancelQuery)
        {
            while (true)
            {
                onProgress?.Invoke(prepareRemoteMessage, "Creating file version record...");

                wait = true;
                errorStr = "";

                apiFile.CreateNewVersion(ApiFile.Version.FileType.Full, fileMD5Base64, fileSize,
                    sigMD5Base64, sigFileSize, FileSuccess, FileFailure);

                while (wait)
                {
                    if (cancelQuery())
                    {
                        CleanupTempFiles(apiFile.id);
                        return true;
                    }

                    await Task.Delay(33);
                }

                if (!string.IsNullOrEmpty(errorStr))
                {
                    onError?.Invoke("Failed to create file version record.", errorStr);
                    CleanupTempFiles(apiFile.id);
                    return true;
                }

                // delay to let write get through servers
                await Task.Delay(postWriteDelay);

                break;
            }

            return false;
        }

        private async Task<string> GetExistingFileSignature(UploadError onError, UploadProgress onProgress,
            Func<bool> cancelQuery)
        {
            string existingFileSignaturePath = "";
            onProgress?.Invoke(prepareRemoteMessage, "Downloading previous version signature");

            wait = true;
            errorStr = "";
            apiFile.DownloadSignature(
                data =>
                {
                    // save to temp file
                    existingFileSignaturePath = Tools.GetTempFileName(".sig", out errorStr, apiFile.id);
                    if (string.IsNullOrEmpty(existingFileSignaturePath))
                    {
                        errorStr = $"Failed to create temp file: \n{errorStr}";
                        wait = false;
                    }
                    else
                    {
                        try
                        {
                            File.WriteAllBytes(existingFileSignaturePath, data);
                        }
                        catch (Exception e)
                        {
                            existingFileSignaturePath = null;
                            errorStr = $"Failed to write signature temp file:\n{e.Message}";
                        }

                        wait = false;
                    }
                },
                error =>
                {
                    errorStr = error;
                    wait = false;
                },
                (downloaded, length) => onProgress?.Invoke(prepareRemoteMessage, 
                    "Downloading previous version signature", Tools.DivideSafe(downloaded, length))
            );

            while (wait)
            {
                if (cancelQuery())
                {
                    CleanupTempFiles(apiFile.id);
                    return null;
                }

                await Task.Delay(33);
            }

            if (string.IsNullOrEmpty(errorStr)) return existingFileSignaturePath;
            
            onError?.Invoke("Failed to download previous file version signature.", errorStr);
            CleanupTempFiles(apiFile.id);
            return null;
        }

        private async Task<string> GenerateMD5Base64(string filename, UploadError onError, Func<bool> cancelQuery)
        {
            return await Task.Run(() =>
            {
                try
                {
                    return Convert.ToBase64String(MD5.Create().ComputeHash(File.OpenRead(filename)));
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Failed to generate MD5 hash for file: {filename}", e.Message);
                    CleanupTempFiles(apiFile.id);
                    return null;
                }
            });
        }

        private static bool CheckFile(string filename, UploadProgress onProgress, UploadError onError)
        {
            onProgress?.Invoke(prepareFileMessage, "Checking file...", 1);
            
            if (string.IsNullOrEmpty(filename))
            {
                onError?.Invoke("Empty or null filename passed");
                return false;
            }

            if (!Path.HasExtension(filename))
            {
                onError?.Invoke($"Upload filename must have an extension: {filename}");
                return false;
            }

            FileStream fileStream = null;
            try
            {
                fileStream = File.OpenRead(filename);
                fileStream.Close();
                return true;
            }
            catch (Exception e)
            {
                fileStream?.Close();
                onError?.Invoke("Could not read input file", $"{filename}: {e.Message}");
                return false;
            }
        }

        private bool isPreviousUploadRetry;
        
        private async Task<bool> CheckForExistingVersion(UploadSuccess onSuccess, UploadError onError,
            UploadProgress onProgress, string fileMD5Base64)
        {
            onProgress?.Invoke(prepareFileMessage, "Checking for changes");

            if (!apiFile.HasExistingOrPendingVersion()) return false;
            
            Logger.Log("Target ApiFile has pending version");
            
            //compare new file MD5 with existing MD5
            if (string.CompareOrdinal(fileMD5Base64, apiFile.GetFileMD5(apiFile.GetLatestVersionNumber())) == 0)
            {
                Logger.Log("MD5 of new file matches remote MD5");
                //the MD5 of the new file matches MD5 of existing file
                
                //was the last upload succesful?
                if (!apiFile.IsWaitingForUpload())
                {
                    onSuccess(apiFile, "The file to upload matches the remote file already.");
                    CleanupTempFiles(apiFile.id);
                    return true;
                }

                Logger.Log("MD5 of new file matches remote MD5");
                
                //the previous upload wasn't succesful
                isPreviousUploadRetry = true;

                Logger.Log("Retrying previous upload");
            }
            else
            {
                //the MD5 of the new file doesn't match the existing file
                Logger.Log("MD5 of new file doesn't match remote MD5");
                
                //the newest version has pending changes
                if (apiFile.IsWaitingForUpload())
                {
                    Logger.Log("Latest version of remote file has pending changes, cleaning up...");
                    
                    //clean it up; on failure, return
                    ApiContainer delete = await apiFile.DeleteLatestVersionAsync();

                    if (!delete.Error.IsNullOrWhitespace())
                    {
                        onError?.Invoke($"Couldn't delete last version", delete.Error);
                        return true;
                    }
                }

                ApiFile.Version version = apiFile.GetLatestVersion();
                Logger.Log("Version: " + version.version);
                Logger.Log("Version status: " + version.status);
                if (version.file != null)
                {
                    Logger.Log("Version file status: " + version.file.status);
                }

                if (version.delta != null)
                {
                    Logger.Log("Version delta status: " + version.delta.status);
                }
                
                if (version.signature != null)
                {
                    Logger.Log("Version delta status: " + version.signature.status);
                }
                
                //we are clear for upload
                return false;
            }

            return false;
        }

        private static async Task<bool> InitRemoteConfig(UploadError onError)
        {
            //If remoteconfig is already initialised, return true
            if (ConfigManager.RemoteConfig.IsInitialized()) return true;
            
            bool done = false;
            ConfigManager.RemoteConfig.Init(() => done = true, () => done = true);

            //god i hate these.. why can't vrc use proper async programming
            while (!done)
                await Task.Delay(33);

            if (ConfigManager.RemoteConfig.IsInitialized()) return true;

            onError?.Invoke("Failed to fetch remote configuration");
            return false;
        }

        private async Task<bool> HandleFileErrorState(UploadError onError, UploadProgress onProgress)
        {
            if (!apiFile.IsInErrorState()) return false;

            Logger.LogWarning($"ApiFile: {apiFile.id}: server failed to process last uploaded, deleting failed version");

            // delete previous failed version
            onProgress?.Invoke(prepareRemoteMessage, "Cleaning up previous version");

            ApiContainer delete = await apiFile.DeleteLatestVersionAsync();

            if (!delete.Error.IsNullOrWhitespace())
            {
                onError?.Invoke("Couldn't delete last version", delete.Error);
                return true;
            }

            // delay to let write get through servers
            await Task.Delay(postWriteDelay);

            return false;
        }

        private async Task<ApiFile> FetchRecord(string filename, string existingFileId, string friendlyName,
            UploadError onError, UploadProgress onProgress, Func<bool> cancelQuery)
        {
            string extension = Path.GetExtension(filename);
            string mimeType = GetMimeTypeFromExtension(extension);

            onProgress?.Invoke(prepareRemoteMessage,
                string.IsNullOrEmpty(existingFileId) ? "Creating file record..." : "Getting file record...");
            
            if (string.IsNullOrEmpty(friendlyName))
                friendlyName = filename;

            //Get file record
            while (true)
            {
                apiFile = null;
                wait = true;
                worthRetry = false;
                errorStr = "";

                if (string.IsNullOrEmpty(existingFileId))
                    ApiFile.Create(friendlyName, mimeType, extension, FileSuccess, FileFailure);
                else
                    API.Fetch<ApiFile>(existingFileId, FileSuccess, FileFailure);

                while (wait)
                {
                    if (apiFile != null && cancelQuery())
                        return apiFile;

                    await Task.Delay(33);
                }

                if (!string.IsNullOrEmpty(errorStr))
                {
                    if (errorStr.Contains("File not found"))
                    {
                        Logger.LogWarning($"Couldn't find file record: {existingFileId}, creating new file record");

                        existingFileId = "";
                        continue;
                    }

                    onError?.Invoke(
                        string.IsNullOrEmpty(existingFileId)
                            ? "Failed to create file record."
                            : "Failed to get file record.", errorStr);

                    if (!worthRetry)
                        return apiFile;
                }

                if (!worthRetry)
                    break;

                if (string.IsNullOrEmpty(existingFileId))
                    await Task.Delay(postWriteDelay);
            }

            return apiFile;
        }

        //untouched
        private static async Task<bool> CreateFileSignatureInternal(string filename, string outputSignatureFilename, UploadError onError)
        {
            Logger.Log($"CreateFileSignature: {filename} => {outputSignatureFilename}");

            await Task.Delay(33);

            Stream inStream;
            FileStream outStream;
            byte[] buf = new byte[512 * 1024];

            try
            {
                inStream = Librsync.ComputeSignature(File.OpenRead(filename));
            }
            catch (Exception e)
            {
                onError?.Invoke($"Couldn't open input file", e.Message);
                return false;
            }

            try
            {
                outStream = File.Open(outputSignatureFilename, FileMode.Create, FileAccess.Write);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Couldn't create output file", e.Message);
                return false;
            }

            while (true)
            {
                IAsyncResult asyncRead;
                try
                {
                    asyncRead = inStream.BeginRead(buf, 0, buf.Length, null, null);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Couldn't read file", e.Message);
                    return false;
                }

                while (!asyncRead.IsCompleted)
                {
                    
                }

                int read;
                try
                {
                    read = inStream.EndRead(asyncRead);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Couldn't read file", e.Message);
                    return false;
                }

                if (read <= 0)
                {
                    break;
                }

                IAsyncResult asyncWrite;
                try
                {
                    asyncWrite = outStream.BeginWrite(buf, 0, read, null, null);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Couldn't write file", e.Message);
                    return false;
                }

                while (!asyncWrite.IsCompleted)
                {
                    
                }

                try
                {
                    outStream.EndWrite(asyncWrite);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Couldn't write file", e.Message);
                    return false;
                }
            }

            inStream.Close();
            outStream.Close();

            return true;
        }

        private static async Task CreateFileDeltaInternal(string newFilename, string existingFileSignaturePath,
            string outputDeltaFilename, Action onSuccess, Action<string> onError)
        {
            Logger.Log($"CreateFileDelta: {newFilename} (delta) {existingFileSignaturePath} => {outputDeltaFilename}");

            await Task.Delay(33);
            
            Stream inStream;
            FileStream outStream;
            byte[] buf = new byte[64 * 1024];

            try
            {
                inStream = Librsync.ComputeDelta(File.OpenRead(existingFileSignaturePath),
                    File.OpenRead(newFilename));
            }
            catch (Exception e)
            {
                onError?.Invoke($"Couldn't open input file: {e.Message}");
                return;
            }

            try
            {
                outStream = File.Open(outputDeltaFilename, FileMode.Create, FileAccess.Write);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Couldn't create output file: {e.Message}");
                return;
            }

            while (true)
            {
                IAsyncResult asyncRead;
                try
                {
                    asyncRead = inStream.BeginRead(buf, 0, buf.Length, null, null);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Couldn't read file: {e.Message}");
                    return;
                }

                while (!asyncRead.IsCompleted)
                    await Task.Delay(33);

                int read;
                try
                {
                    read = inStream.EndRead(asyncRead);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Couldn't read file: {e.Message}");
                    return;
                }

                if (read <= 0)
                    break;

                IAsyncResult asyncWrite;
                try
                {
                    asyncWrite = outStream.BeginWrite(buf, 0, read, null, null);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Couldn't write file: {e.Message}");
                    return;
                }

                while (!asyncWrite.IsCompleted)
                    await Task.Delay(33);

                try
                {
                    outStream.EndWrite(asyncWrite);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Couldn't write file: {e.Message}");
                    return;
                }
            }

            inStream.Close();
            outStream.Close();

            await Task.Delay(33);

            onSuccess?.Invoke();
        }

        /*private static void Error(UploadError onError, ApiFile apiFile, string error, string moreInfo = "")
        {
            if (apiFile == null)
                apiFile = new ApiFile();
            
            Logger.LogError($"Error: {error}\n{moreInfo}");
            onError?.Invoke(apiFile, $"{error}: {moreInfo}");
        }*/

        private static void CleanupTempFiles(string subFolderName)
        {
            Task cleanupTask = CleanupTempFilesInternal(subFolderName);
        }

        private static async Task CleanupTempFilesInternal(string subFolderName)
        {
            if (string.IsNullOrEmpty(subFolderName)) return;
            
            string folder = Tools.GetTempFolderPath(subFolderName);

            while (Directory.Exists(folder))
            {
                try
                {
                    if (Directory.Exists(folder))
                        Directory.Delete(folder, true);
                }
                catch (Exception)
                {
                    //ignored as removing temp files can be supressed
                }

                await Task.Delay(33);
            }
        }

        private static float GetServerProcessingWaitTimeoutForDataSize(int size)
        {
            float timeoutMultiplier = Mathf.Ceil(size / (float)SERVER_PROCESSING_WAIT_TIMEOUT_CHUNK_SIZE);
            return Mathf.Clamp(timeoutMultiplier * SERVER_PROCESSING_WAIT_TIMEOUT_PER_CHUNK_SIZE,
                SERVER_PROCESSING_WAIT_TIMEOUT_PER_CHUNK_SIZE, SERVER_PROCESSING_MAX_WAIT_TIMEOUT);
        }

        private static bool UploadFileComponentValidateFileDesc(ApiFile apiFile, ApiFile.Version.FileDescriptor fileDesc, 
            string filename, string md5Base64, long fileSize, 
            Action<ApiFile> onSuccess, Action<string> onError)
        {
            if (fileDesc.status != ApiFile.Status.Waiting)
            {
                // nothing to do (might be a retry)
                Logger.Log("UploadFileComponent: (file record not in waiting status, done)");
                onSuccess?.Invoke(apiFile);
                return false;
            }

            if (fileSize != fileDesc.sizeInBytes)
            {
                onError?.Invoke("UploadFileComponent: File size does not match version descriptor");
                return false;
            }

            if (string.CompareOrdinal(md5Base64, fileDesc.md5) != 0)
            {
                onError?.Invoke("UploadFileComponent: File MD5 does not match version descriptor");
                return false;
            }

            // make sure file is right size
            if (!Tools.GetFileSize(filename, out long tempSize, out string errorStr))
            {
                onError?.Invoke($"UploadFileComponent: Couldn't get file size : {errorStr}");
                return false;
            }

            if (tempSize == fileSize) return true;
            
            onError?.Invoke("UploadFileComponent: File size does not match input size");
            return false;
        }

        private static async Task UploadFileComponentDoSimpleUpload(ApiFile apiFile, ApiFile.Version.FileDescriptor.Type fileDescriptorType,
            string filename, string md5Base64,
            Action<string> onError, Action<long, long> onProgress, Func<bool> cancelQuery)
        {
            Logger.Log($"Starting simple upload for {apiFile.name}...");
            
            (ApiContainer result, string uploadUrl) = await apiFile.StartSimpleUploadAsync(fileDescriptorType, cancelQuery);

            if (!result.Error.IsNullOrWhitespace())
            {
                onError?.Invoke(result.Error);
                return;
            }
            
            if (uploadUrl.IsNullOrWhitespace())
            {
                onError?.Invoke("Invalid URL provided by API");
                return;
            }
            
            // delay to let write get through servers
            await Task.Delay(postWriteDelay);

            //PUT file to url
            result = await apiFile.PutSimpleFileToURLAsync(filename, md5Base64, uploadUrl, onProgress, cancelQuery);
            
            if (!result.Error.IsNullOrWhitespace())
            {
                onError?.Invoke(result.Error);
                return;
            }

            //finish upload
            result = await apiFile.FinishUploadAsync(fileDescriptorType, null, cancelQuery);

            if (result.Error.IsNullOrWhitespace())
            {
                // delay to let write get through servers
                await Task.Delay(postWriteDelay);
            }
            else
            {
                onError?.Invoke(result.Error);
            }
        }

        private static async Task UploadFileComponentDoMultipartUpload(ApiFile apiFile, ApiFile.Version.FileDescriptor.Type fileDescriptorType,
            string filename, long fileSize,
            Action<string> onError, Action<long, long> onProgress, Func<bool> cancelQuery)
        {
            Logger.Log($"Starting multipart upload for {apiFile.name}...");

            //get existing multipart upload status in case there is one
            ApiContainer uploadStatusContainer = await apiFile.GetUploadStatus(fileDescriptorType, cancelQuery);

            if (!uploadStatusContainer.Error.IsNullOrWhitespace())
            {
                onError?.Invoke(uploadStatusContainer.Error);
                return;
            }

            ApiFile.UploadStatus uploadStatus = uploadStatusContainer.Model as ApiFile.UploadStatus;

            FileStream fs;

            // split file into chunks
            try
            {
                fs = File.OpenRead(filename);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Couldn't open file: {e.Message}");
                return;
            }

            byte[] buffer = new byte[kMultipartUploadChunkSize * 2];

            long totalBytesUploaded = 0;
            List<string> etags = new List<string>();
            if (uploadStatus != null)
                etags = uploadStatus.etags.ToList();

            Logger.Log($"File stream length: {fs.Length}");

            //why is this a FloorToInt? what the fuck? so 100MB parts on a 250MB world gives you... a 100MB and a 150MB part? 
            int numParts = Mathf.Max(1, Mathf.FloorToInt(fs.Length / (float)kMultipartUploadChunkSize));

            Logger.Log($"Multipart upload part count: {numParts}");

            for (int partNumber = 1; partNumber <= numParts; partNumber++)
            {
                Logger.Log($"Uploading part {partNumber}...");

                // read chunk
                int bytesToRead = partNumber < numParts ? kMultipartUploadChunkSize : (int)(fs.Length - fs.Position);
                int bytesRead;

                try
                {
                    bytesRead = fs.Read(buffer, 0, bytesToRead);
                }
                catch (Exception e)
                {
                    fs.Close();
                    onError?.Invoke($"Couldn't read file: {e.Message}");
                    return;
                }

                if (bytesRead != bytesToRead)
                {
                    fs.Close();
                    onError?.Invoke("Couldn't read file: read incorrect number of bytes from stream");
                    return;
                }

                // check if this part has been upload already
                // NOTE: uploadStatus.nextPartNumber == number of parts already uploaded
                if (uploadStatus != null && partNumber <= uploadStatus.nextPartNumber)
                {
                    totalBytesUploaded += bytesRead;
                    continue;
                }

                //request target URL for upload
                (ApiContainer result, string uploadUrl) = await apiFile.StartMultiPartUploadAsync(partNumber, fileDescriptorType, cancelQuery);

                if (!result.Error.IsNullOrWhitespace())
                {
                    onError?.Invoke(result.Error);
                    fs.Close();
                    return;
                }

                if (uploadUrl.IsNullOrWhitespace())
                {
                    onError?.Invoke("Invalid URL provided by API while uploading multipart file");
                    fs.Close();
                    return;
                }

                await Task.Delay(postWriteDelay);

                void OnMultiPartUploadProgress(long uploadedBytes, long totalBytes)
                {
                    onProgress(totalBytesUploaded + uploadedBytes, fileSize);
                }

                //PUT file
                (ApiContainer putResult, string etag) = await apiFile.PutMultipartDataToURLAsync(uploadUrl, buffer,
                    GetMimeTypeFromExtension(Path.GetExtension(filename)), bytesRead, OnMultiPartUploadProgress,
                    cancelQuery);

                if (!putResult.Error.IsNullOrWhitespace())
                {
                    fs.Close();
                    onError?.Invoke(errorStr);
                    return;
                }

                etags.Add(etag);
                totalBytesUploaded += bytesRead;
            }

            // delay to let write get through servers
            await Task.Delay(postWriteDelay);

            //finish upload
            ApiContainer container = await apiFile.FinishUploadAsync(fileDescriptorType, etags, cancelQuery);

            if (container.Error.IsNullOrWhitespace())
            {
                // delay to let write get through servers
                await Task.Delay(postWriteDelay);
                fs.Close();
            }
            else
            {
                onError?.Invoke(container.Error);
                fs.Close();
            }
        }

        private static async Task UploadFileComponentVerifyRecord(ApiFile apiFile,
            ApiFile.Version.FileDescriptor.Type fileDescriptorType, ApiFile.Version.FileDescriptor fileDesc,
            Action<ApiFile> onSuccess, Action<string> onError)
        {
            float initialStartTime = Time.realtimeSinceStartup;
            float startTime = initialStartTime;
            float timeout = GetServerProcessingWaitTimeoutForDataSize(fileDesc.sizeInBytes);
            float waitDelay = SERVER_PROCESSING_INITIAL_RETRY_TIME;

            while(true)
            {
                if (apiFile == null)
                {
                    onError?.Invoke("ApiFile is null");
                    return;
                }

                ApiFile.Version.FileDescriptor desc = apiFile.GetFileDescriptor(apiFile.GetLatestVersionNumber(), fileDescriptorType);
                if (desc == null)
                {
                    onError?.Invoke($"File descriptor is null ('{fileDescriptorType}')");
                    return;
                }

                if (desc.status != ApiFile.Status.Waiting)
                {
                    // upload completed or is processing
                    break;
                }
                
                // wait for next poll
                while (Time.realtimeSinceStartup - startTime < waitDelay)
                {
                    if (Time.realtimeSinceStartup - initialStartTime > timeout)
                    {
                        onError?.Invoke("Couldn't verify upload status: Timed out wait for server processing");
                        return;
                    }

                    await Task.Delay(33);
                }
                
                while (true)
                {
                    wait = true;
                    worthRetry = false;

                    apiFile.Refresh(
                        (c) =>
                        {
                            wait = false;
                        },
                        (c) =>
                        {
                            errorStr = "Couldn't verify upload status: " + c.Error;
                            wait = false;
                            if (c.Code == 400)
                                worthRetry = true;
                        });

                    while (wait)
                    {
                        await Task.Delay(33);
                    }

                    if (!string.IsNullOrEmpty(errorStr))
                    {
                        onError?.Invoke($"Couldn't verify upload status: {errorStr}");
                        if (!worthRetry)
                            return;
                    }

                    if (!worthRetry)
                        break;
                }

                waitDelay = Mathf.Min(waitDelay * 2, SERVER_PROCESSING_MAX_RETRY_TIME);
                startTime = Time.realtimeSinceStartup;
            }

            onSuccess?.Invoke(apiFile);
        }

        private static async Task UploadFileComponentInternal(ApiFile apiFile, ApiFile.Version.FileDescriptor.Type fileDescriptorType,
            string filename, string md5Base64, long fileSize,
            Action<ApiFile> onSuccess, Action<string> onError, Action<long, long> onProgress, Func<bool> cancelQuery)
        {
            Logger.Log($"UploadFileComponent: {fileDescriptorType} ({apiFile.id}): {filename}");
            
            ApiFile.Version.FileDescriptor fileDesc = apiFile.GetFileDescriptor(apiFile.GetLatestVersionNumber(), fileDescriptorType);

            if (!UploadFileComponentValidateFileDesc(apiFile, fileDesc, filename, md5Base64, fileSize, onSuccess, onError))
                return;

            switch (fileDesc.category)
            {
                case ApiFile.Category.Simple:
                    await UploadFileComponentDoSimpleUpload(apiFile, fileDescriptorType, filename, md5Base64, onError, onProgress, cancelQuery);
                    break;
                case ApiFile.Category.Multipart:
                    await UploadFileComponentDoMultipartUpload(apiFile, fileDescriptorType, filename,
                        fileSize, onError, onProgress, cancelQuery);
                    break;
                default:
                    onError?.Invoke($"Unsupported file category type: {fileDesc.category}");
                    return;
            }

            await UploadFileComponentVerifyRecord(apiFile, fileDescriptorType, fileDesc, onSuccess, onError);
        }
    }
}
#endif