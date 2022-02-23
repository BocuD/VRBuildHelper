#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VRC;
using VRC.Core;

namespace BocuD.VRChatApiTools
{
    public static class ApiFileAsyncExtensions
    {
        //todo: merge StartSimpleUploadAsync and StartMultiPartUploadAsync into one function, then use proxy functions to do the actual calls
        public static async Task<(ApiContainer result, string url)> StartSimpleUploadAsync(this ApiFile apiFile,
            ApiFile.Version.FileDescriptor.Type fileDescriptorType, Func<bool> cancelQuery)
        {
            string uploadUrl = "";
            ApiContainer result = new ApiContainer();
            
            if (!apiFile.IsInitialized)
            {
                result.Error = "Unable to upload file: file not initialized.";
                return (result, uploadUrl);
            }

            int latestVersionNumber = apiFile.GetLatestVersionNumber();

            if (apiFile.GetFileDescriptor(latestVersionNumber, fileDescriptorType) == null)
            {
                result.Error = "Version record doesn't exist";
                return (result, uploadUrl);
            }

            ApiFile.UploadStatus uploadStatus = new ApiFile.UploadStatus(apiFile.id, latestVersionNumber, fileDescriptorType, "start");

            bool wait = true;

            ApiDictContainer apiDictContainer = new ApiDictContainer("url")
            {
                OnSuccess = c =>
                {
                    result = c;
                    wait = false;
                    uploadUrl = (result as ApiDictContainer)?.ResponseDictionary["url"] as string;
                },
                OnError = c => { result = c; wait = false; }
            };

            API.SendPutRequest(uploadStatus.Endpoint, apiDictContainer);

            while (wait)
            {
                if (cancelQuery())
                {
                    result.Error = "The operation was cancelled.";
                    return (result, uploadUrl);
                }
                await Task.Delay(33);
            }

            return (result, uploadUrl);
        }
        
        public static async Task<(ApiContainer result, string url)> StartMultiPartUploadAsync(this ApiFile apiFile, int partNumber,
            ApiFile.Version.FileDescriptor.Type fileDescriptorType, Func<bool> cancelQuery)
        {
            string uploadUrl = "";
            ApiContainer result = new ApiContainer();
            
            if (!apiFile.IsInitialized)
            {
                result.Error = "Unable to upload file: file not initialized.";
                return (result, uploadUrl);
            }

            int latestVersionNumber = apiFile.GetLatestVersionNumber();

            if (apiFile.GetFileDescriptor(latestVersionNumber, fileDescriptorType) == null)
            {
                result.Error = "Version record doesn't exist";
                return (result, uploadUrl);
            }
            
            ApiFile.UploadStatus uploadStatus = new ApiFile.UploadStatus(apiFile.id, latestVersionNumber, fileDescriptorType, "start");

            bool wait = true;

            ApiDictContainer apiDictContainer = new ApiDictContainer("url")
            {
                OnSuccess = c =>
                {
                    result = c;
                    wait = false;
                    uploadUrl = (result as ApiDictContainer)?.ResponseDictionary["url"] as string;
                },
                OnError = c => { result = c; wait = false; }
            };
            
            API.SendPutRequest($"{uploadStatus.Endpoint}?partNumber={partNumber}", apiDictContainer);

            while (wait)
            {
                if (cancelQuery())
                {
                    result.Error = "The operation was cancelled.";
                    return (result, uploadUrl);
                }
                await Task.Delay(33);
            }

            return (result, uploadUrl);
        }

        //literally just a wrapper function around the existing PutSimpleFileToURL to clean up the main ApiFileHelperAsync
        public static async Task<ApiContainer> PutSimpleFileToURLAsync(this ApiFile apiFile, string filename, string md5Base64, 
            string uploadUrl, Action<long, long> onProgress, Func<bool> cancelQuery)
        {
            bool wait = true;
            string errorStr = "";

            HttpRequest req = ApiFile.PutSimpleFileToURL(uploadUrl, filename,
                ApiFileHelper.GetMimeTypeFromExtension(Path.GetExtension(filename)), md5Base64, true,
                () => wait = false,
                error =>
                {
                    errorStr = $"Failed to upload file: {error}";
                    wait = false;
                },
                (uploaded, length) => onProgress?.Invoke(uploaded, length)
            );

            while (wait)
            {
                if (cancelQuery())
                {
                    req?.Abort();
                    return new ApiContainer {Error = "The operation was cancelled."};
                }

                await Task.Delay(33);
            }

            return !string.IsNullOrEmpty(errorStr) ? new ApiContainer { Error = errorStr } : new ApiContainer();
        }

        public static async Task<ApiContainer> FinishUploadAsync(this ApiFile apiFile,
            ApiFile.Version.FileDescriptor.Type fileDescriptorType,
            List<string> multipartEtags, Func<bool> cancelQuery)
        {
            if (!apiFile.IsInitialized)
            {
                return new ApiContainer { Error = "Unable to finish upload of file: file not initialized." };
            }

            int latestVersionNumber = apiFile.GetLatestVersionNumber();

            if (apiFile.GetFileDescriptor(latestVersionNumber, fileDescriptorType) == null)
            {
                return new ApiContainer { Error = "Version record doesn't exist" };
            }

            ApiContainer result = new ApiContainer();
            bool wait = true;
            
            new ApiFile.UploadStatus(apiFile.id, latestVersionNumber, fileDescriptorType, "finish")
            {
                etags = multipartEtags
            }.Put(c =>
            {
                result = c;
                wait = false;
            }, c =>
            {
                result = c;
                wait = false;
            });

            while (wait)
            {
                if (cancelQuery())
                {
                    return new ApiContainer{Error = "The operation was cancelled."};
                }
                await Task.Delay(33);
            }
            
            return result;
        }

        public static async Task<ApiContainer> GetUploadStatus(this ApiFile apiFile,
            ApiFile.Version.FileDescriptor.Type fileDescriptorType, Func<bool> cancelQuery)
        {
            bool wait = true;

            ApiContainer result = new ApiContainer();

            apiFile.GetUploadStatus(apiFile.GetLatestVersionNumber(), fileDescriptorType,
                c =>
                {
                    result = c;
                    wait = false;
                    Logger.Log($"Found existing multipart upload status (next part = {(c.Model as ApiFile.UploadStatus).nextPartNumber})");
                },
                c =>
                {
                    result = c;
                    wait = false;
                    c.Error = $"Failed to query multipart upload status: {c.Error}";
                });

            while (wait)
            {
                if (cancelQuery())
                {
                    return new ApiContainer { Error = "The operation was cancelled." };
                }
                await Task.Delay(33);
            }
            
            return result;
        }
        
        public static async Task<(ApiContainer, string)> PutMultipartDataToURLAsync(this ApiFile apiFile,
            string uploadUrl,
            byte[] buffer, string mimeType,
            int bytesRead, Action<long, long> onProgress,
            Func<bool> cancelQuery)
        {
            bool wait = true;
            string resultTag = "";

            ApiContainer c = new ApiContainer();

            HttpRequest req = ApiFile.PutMultipartDataToURL(uploadUrl, buffer, bytesRead, mimeType, true,
                etag =>
                {
                    if (!string.IsNullOrEmpty(etag))
                        resultTag = etag;
                    wait = false;
                },
                error =>
                {
                    c.Error = $"Failed to upload data: {error}";
                    wait = false;
                },
                (uploaded, length) => onProgress?.Invoke(uploaded, length)
            );

            while (wait)
            {
                if (cancelQuery())
                {
                    req?.Abort();
                    c.Error = "The operation was cancelled.";
                }

                await Task.Delay(33);
            }

            return (c, resultTag);
        }

        public static async Task<ApiContainer> DeleteLatestVersionAsync(this ApiFile apiFile)
        {
            ApiContainer result = new ApiContainer();
            bool wait = true;

            if (!apiFile.IsInitialized)
            {
                return new ApiContainer { Error = "Unable to delete file: file not initialized." };
            }

            int latestVersionNumber = apiFile.GetLatestVersionNumber();
            if (latestVersionNumber <= 0 || latestVersionNumber >= apiFile.versions.Count)
                return new ApiContainer { Error = $"ApiFile ({apiFile.id}): version to delete is invalid: {latestVersionNumber}" };
            
            if (latestVersionNumber == 1)
                return new ApiContainer { Error = "There is only one version. Deleting version that would delete the file. Please use another method." };

            apiFile.DeleteVersion(latestVersionNumber,
                c =>
                {
                    result = c;
                    wait = false;
                }, c =>
                {
                    result = c;
                    wait = false;
                });

            while (wait)
            {
                await Task.Delay(33);
            }

            return result;
        }
    }
}
#endif