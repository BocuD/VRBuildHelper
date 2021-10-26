using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UdonSharpEditor;
#endif

namespace BocuD.BuildHelper
{
    [ExecuteInEditMode]
    public class BuildHelperData : MonoBehaviour
    {
        public int currentBranchIndex;
        public int lastBuiltBranch;
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
            get => branches[currentBranchIndex];
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
                branches = branches, currentBranch = currentBranchIndex, lastBuiltBranch = lastBuiltBranch, autonomousBuild = autonomousBuild
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
                sw.Close();
            }
        }
    }

    [Serializable]
    public class BranchStorageObject
    {
        public int currentBranch;
        public int lastBuiltBranch;
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
        public bool VRCDataInitialised = false;
        public string VRCName = "Unpublished VRChat world";
        public string VRCDesc = "";
        public int VRCCap = 16;
        public bool vrcReleaseState = false;
        public string vrcTags = "";

        public string VRCNameLocal = "Unpublished VRChat world";
        public string VRCDescLocal = "";
        public int VRCCapLocal = 16;
        public bool vrcReleaseStateLocal = false;
        public string vrcTagsLocal = "";
        
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
    public class DeploymentData
    {
        public string deploymentPath;
        public string initialBranchName;
        public DeploymentUnit[] units;
    }
    
    [Serializable]
    public struct DeploymentUnit
    {
        public bool autoUploader;
        public string fileName;
        public int buildNumber;
        public long fileSize;
        public Platform platform;
        public string gitHash;
        public string filePath;
        public DateTime buildDate;
        public DateTime modifiedDate;
        public long modifiedFileTime;
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