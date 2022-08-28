using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace BocuD.BuildHelper
{
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(PostProcessLayer))]
    public class CameraData : MonoBehaviour
    {
        public bool postProcessing;
        public LayerMask postProcessingLayer = 1 << 4;
        
        public enum AntiAliasingMode
        {
            [InspectorName("MSAA 1x")] x1 = 1,
            [InspectorName("MSAA 2x")] x2 = 2,
            [InspectorName("MSAA 4X")] x4 = 4,
            [InspectorName("MSAA 8X")] x8 = 8
        }

        public AntiAliasingMode antiAliasingMode = AntiAliasingMode.x4;
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(CameraData))]
    public class CameraDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("This component stores camera information for BuildHelper. Any changes you make to this GameObject will be saved, including any children other added components.", MessageType.Info);
        }
    }
    #endif
}
