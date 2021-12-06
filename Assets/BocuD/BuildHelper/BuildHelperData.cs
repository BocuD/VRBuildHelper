/* MIT License
 Copyright (c) 2021 BocuD (github.com/BocuD)

 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

 The above copyright notice and this permission notice shall be included in all
 copies or substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 SOFTWARE.
*/

#if UNITY_EDITOR

using System;
using System.IO;
using UdonSharpEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BocuD.BuildHelper
{
    [ExecuteInEditMode]
    public class BuildHelperData : MonoBehaviour
    {
        public int currentBranchIndex;
        public int lastBuiltBranch;
        public bool autoSave = true;
        public Branch[] branches;
        public AutonomousBuildInformation autonomousBuild;
        public OverrideContainer[] overrideContainers;
        public BuildHelperUdon linkedBehaviour;
        public GameObject linkedBehaviourGameObject;

        private void Awake()
        {
            LoadFromJSON();
            #if UNITY_EDITOR
            if (linkedBehaviourGameObject != null)
                linkedBehaviour = linkedBehaviourGameObject.GetUdonSharpComponent<BuildHelperUdon>();
            #endif
        }

        public Branch currentBranch
        {
            get
            {
                if (currentBranchIndex >= 0 && currentBranchIndex < branches.Length)
                    return branches[currentBranchIndex];
                
                return null;
            }
        }

        public void PrepareExcludedGameObjects()
        {
            for (int i = 0; i < branches.Length; i++)
            {
                if (!branches[i].hasOverrides) continue;
            
                OverrideContainer container = overrideContainers[i];
                foreach (GameObject obj in container.ExclusiveGameObjects)
                {
                    obj.tag = "EditorOnly";
                    obj.SetActive(false);
                }
            }
        }

        public void SaveToJSON()
        {
            BranchStorageObject storageObject = new BranchStorageObject
            {
                branches = branches, currentBranch = currentBranchIndex, autoSave = autoSave, lastBuiltBranch = lastBuiltBranch, autonomousBuild = autonomousBuild
            };

            if (storageObject.branches == null) storageObject.branches = new Branch[0];
        
            string savePath = Application.dataPath + $"/Resources/BuildHelper/{SceneManager.GetActiveScene().name}.json";
            CheckIfFileExists();
            
            string json = JsonUtility.ToJson(storageObject, true);
            File.WriteAllText(savePath, json);
        }

        public void LoadFromJSON()
        {
            string savePath = Application.dataPath + $"/Resources/BuildHelper/{SceneManager.GetActiveScene().name}.json";
            CheckIfFileExists();
            
            string json = File.ReadAllText(savePath);
            BranchStorageObject storageObject = JsonUtility.FromJson<BranchStorageObject>(json);

            if (storageObject.branches == null)
                storageObject.branches = new Branch[0];

            if (storageObject.autonomousBuild == null)
                storageObject.autonomousBuild = new AutonomousBuildInformation();
        
            branches = storageObject.branches;
            currentBranchIndex = storageObject.currentBranch;
            autoSave = storageObject.autoSave;
            lastBuiltBranch = storageObject.lastBuiltBranch;
            autonomousBuild = storageObject.autonomousBuild;

            if (overrideContainers == null)
            {
                overrideContainers = new OverrideContainer[branches.Length];
                foreach (OverrideContainer container in overrideContainers)
                {
                    container.ExclusiveGameObjects = new GameObject[0];
                    container.ExcludedGameObjects = new GameObject[0];
                }
            }
            else
            {
                foreach (OverrideContainer container in overrideContainers)
                {
                    if (container.ExclusiveGameObjects == null)
                        container.ExclusiveGameObjects = new GameObject[0];
                    if (container.ExcludedGameObjects == null)
                        container.ExcludedGameObjects = new GameObject[0];
                }
            }
        }
    
        private static void CheckIfFileExists()
        {
            string savePath = Application.dataPath + $"/Resources/BuildHelper/{SceneManager.GetActiveScene().name}.json";
        
            // Create file
            if (!File.Exists(savePath))
            {
                if (!Directory.Exists(Application.dataPath + "/Resources/BuildHelper/"))
                {
                    Directory.CreateDirectory(Application.dataPath + "/Resources/BuildHelper/");
                }

                StreamWriter sw = File.CreateText(savePath);
                
                //write something so it won't be empty on next load
                BranchStorageObject storageObject = new BranchStorageObject();
                
                sw.Write(JsonUtility.ToJson(storageObject));

                if (storageObject.branches == null) storageObject.branches = new Branch[0];

                sw.Close();
            }
        }
        
        public void DeleteJSON()
        {
            string savePath = Application.dataPath + $"/Resources/BuildHelper/{SceneManager.GetActiveScene().name}.json";
        
            // Create file
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    [Serializable]
    public class BranchStorageObject
    {
        public int currentBranch;
        public int lastBuiltBranch;
        public bool autoSave = true;
        public Branch[] branches;
        public AutonomousBuildInformation autonomousBuild;
    }

    [Serializable]
    public class Branch
    {
        //basic branch information
        public string name = "";
        public bool hasOverrides = false;
        public string blueprintID = "";

        //VRC World Data
        public string cachedName = "Unpublished VRChat world";
        public string cachedDescription = "";
        public int cachedCap = 16;
        public string cachedRelease = "private";
        public string cachedTags = "";

        public string editedName = "notInitialised";
        public string editedDescription = "notInitialised";
        public int editedCap = -1;
        public string editedTags = "notInitialised";
        
        public bool vrcDataHasChanges = false;
        public bool vrcImageHasChanges = false;
        public string vrcImageWarning = "";
    
        //VRCCam state
        public bool saveCamPos = true;
        public bool uniqueCamPos = false;
        public Vector3 camPos = Vector3.zero;
        public Quaternion camRot = Quaternion.identity;
    
        public BuildData buildData;

        public bool hasDeploymentData = false;
        public DeploymentData deploymentData;

        public bool hasUdonLink = false;
    }

    [Serializable]
    public class BuildData
    {
        //yes, these should be DateTime for sure.. they are strings because DateTime was not being serialised correctly somehow.. i hate it.
        public string pcBuildTime;
        public string androidBuildTime;

        public string pcUploadTime;
        public string androidUploadTime;

        public int pcBuildVersion = -1;
        public int androidBuildVersion = -1;

        public int pcUploadedBuildVersion = -1;
        public int androidUploadedBuildVersion = -1;

        public BuildData()
        {
            Reset();
        }
    
        public void Reset()
        {
            pcBuildTime = "";
            androidBuildTime = "";
            pcUploadTime = "";
            androidUploadTime = "";

            pcBuildVersion = -1;
            androidBuildVersion = -1;
            pcUploadedBuildVersion = -1;
            androidUploadedBuildVersion = -1;
        }
    }
    
    public enum Platform
    {
        PC,
        mobile
    }

    [Serializable]
    public class OverrideContainer
    {
        public GameObject[] ExclusiveGameObjects;
        public GameObject[] ExcludedGameObjects;

        public void ApplyStateChanges()
        {
            foreach (GameObject obj in ExclusiveGameObjects)
            {
                EnableGameObject(obj);
            }

            foreach (GameObject obj in ExcludedGameObjects)
            {
                DisableGameObject(obj);
            }
        }

        public void ResetStateChanges()
        {
            foreach (GameObject obj in ExcludedGameObjects)
            {
                EnableGameObject(obj);
            }
        }

        public static void EnableGameObject(GameObject obj)
        {
            if (obj != null)
            {
                obj.SetActive(true);
                if (obj.CompareTag("EditorOnly"))
                {
                    obj.tag = "Untagged";
                }
            }
        }
    
        public static void DisableGameObject(GameObject obj)
        {
            if (obj != null)
            {
                obj.SetActive(false);
                if (!obj.CompareTag("EditorOnly"))
                {
                    obj.tag = "EditorOnly";
                }
            }
        }
    }
    
    [Serializable]
    public class AutonomousBuildInformation
    {
        public bool activeBuild;
        public bool singleTarget;
        public Platform initialTarget;
        public Platform secondaryTarget;
        public Progress progress;

        public AutonomousBuildInformation()
        {
            activeBuild = false;
            singleTarget = false;
        }

        public enum Progress
        {
            PreInitialBuild,
            PostInitialBuild,
            PostPlatformSwitch,
            PreSecondaryBuild,
            PostSecondaryBuild,
            Finished
        }
    }
}

#endif