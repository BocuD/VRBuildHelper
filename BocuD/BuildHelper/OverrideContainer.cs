using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
