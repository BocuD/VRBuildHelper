using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BocuD.BuildHelper
{
    public class BuildHelperData : MonoBehaviour
    {
        public int currentBranch;
        public int lastBuiltBranch;
        public Branch[] branches;
        public OverrideContainer[] overrideContainers;

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
                branches = branches, currentBranch = currentBranch, lastBuiltBranch = lastBuiltBranch
            };

            if (storageObject.branches == null) storageObject.branches = new Branch[0];
        
            string savePath = Application.dataPath + $"/Resources/BuildHelper/{SceneManager.GetActiveScene().name}.json";

            string json = JsonUtility.ToJson(storageObject);

            CheckIfFileExists();

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
        
            branches = storageObject.branches;
            currentBranch = storageObject.currentBranch;
            lastBuiltBranch = storageObject.lastBuiltBranch;

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
    }

    [Serializable]
    public class Branch
    {
        //basic branch data
        public string name;
        public bool hasOverrides;
        public string blueprintID;
    
        //VRC World Data
        public bool VRCDataInitialised;
        public string VRCName;
        public string VRCDesc;
        public int VRCPlayerCount;
        public bool vrcReleaseState;
        public string vrcTags;
    
        //VRCCam state
        public bool saveCamPos = true;
        public bool uniqueCamPos;
        public Vector3 camPos;
        public Quaternion camRot;
    
        public BuildData buildData;
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
}