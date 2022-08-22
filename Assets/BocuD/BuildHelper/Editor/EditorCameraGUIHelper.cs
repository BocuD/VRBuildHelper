using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace BocuD.BuildHelper.Editor
{
    public static class EditorCameraGUIHelper
    {
        private static EditorCameraGUI instance;
        
        public static void SetupCapture(Action<Texture2D> onComplete)
        {
            instance?.Close();

            instance = new EditorCameraGUI(onComplete);
        }
        
        [InitializeOnLoadMethod]
        public static void CleanupCamera()
        {
            CameraData cam = UnityEngine.Object.FindObjectOfType<CameraData>();

            if (cam.transform.parent == null)
            {
                BuildHelperData buildHelperData = BuildHelperData.GetDataBehaviour();
                if (buildHelperData != null)
                {
                    cam.transform.SetParent(buildHelperData.transform);
                    cam.gameObject.hideFlags = HideFlags.HideInHierarchy;
                }

                if (Selection.activeGameObject == cam.gameObject)
                {
                    Selection.activeObject = null;
                }
            }
        }
    }

    public class EditorCameraGUI
    {
        private static Action<Texture2D> onCapture;
        private static Camera cam;
        private static RenderTexture preview;
        private static CameraData _camData;

        private static SerializedObject camHelperSO;
        private static SerializedProperty postProcessing;
        private static SerializedProperty postProcessingLayer;
        private static SerializedProperty antiAliasingMode;

        private static GUIContent captureButton;

        public EditorCameraGUI(Action<Texture2D> onComplete)
        {
            onCapture = onComplete;
            preview = new RenderTexture(1200, 900, 24);

            SetupCamera();

            captureButton = new GUIContent
            {
                image = Resources.Load<Texture2D>("Icons/d_FrameCapture@2x"),
                text = " Capture Image"
            };
            
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += Update;
        }
        
        private void SetupCamera()
        {
            _camData = UnityEngine.Object.FindObjectOfType<CameraData>();

            //create a new camera gameobject
            if (_camData == null)
            {
                GameObject newCam = new GameObject("BuildHelperCamera");

                newCam.AddComponent<Camera>();
                newCam.AddComponent<PostProcessLayer>();

                _camData = newCam.AddComponent<CameraData>();
            }

            _camData.gameObject.hideFlags = HideFlags.None;
            _camData.transform.SetParent(null);

            cam = _camData.GetComponent<Camera>();

            camHelperSO = new SerializedObject(_camData);
            postProcessing = camHelperSO.FindProperty(nameof(CameraData.postProcessing));
            postProcessingLayer = camHelperSO.FindProperty(nameof(CameraData.postProcessingLayer));
            antiAliasingMode = camHelperSO.FindProperty(nameof(CameraData.antiAliasingMode));

            Selection.activeObject = cam;

            EditorApplication.RepaintHierarchyWindow();

            ApplyCameraSettings();
        }

        private void ApplyCameraSettings()
        {
            cam.GetComponent<PostProcessLayer>().enabled = _camData.postProcessing;
            cam.GetComponent<PostProcessLayer>().volumeLayer = _camData.postProcessingLayer;
        }

        public void Update()
        {
            if (cam != null)
            {
                cam.targetTexture = preview;
                cam.Render();
            }
        }
        
        private Texture2D Capture()
        {
            RenderTexture tempRT = new RenderTexture(1200, 900, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = (int)_camData.antiAliasingMode
            };

            cam.targetTexture = tempRT;

            RenderTexture.active = tempRT;
            cam.Render();

            Texture2D image = new Texture2D(1200, 900, TextureFormat.ARGB32, false, true);
            image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0);
            image.Apply();

            return image;
        }


        public void OnSceneGUI(SceneView sceneview)
        {
            Handles.BeginGUI();

            GUIStyle background = new GUIStyle(GUI.skin.box);
            background.normal.background = ImageTools.BackgroundTexture(32, 32, new Color(0.22f, 0.22f, 0.22f, 0.75f));
            
            EditorGUILayout.BeginVertical(background, GUILayout.Width(600));

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical(GUILayout.Width(350));

            using (EditorGUI.ChangeCheckScope changeCheckScope = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.LabelField("Post Processing", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(postProcessing);

                if (postProcessing.boolValue)
                {
                    EditorGUILayout.PropertyField(postProcessingLayer);
                }
                
                GUILayout.Space(5);

                EditorGUILayout.LabelField("Camera Options", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(antiAliasingMode);

                if (changeCheckScope.changed)
                {
                    camHelperSO.ApplyModifiedProperties();
                    ApplyCameraSettings();
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            
            EditorGUILayout.LabelField("Camera Preview", EditorStyles.boldLabel);
            GUILayout.Box(preview, GUILayout.Width(600), GUILayout.Height(450));

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fixedHeight = 25
            };

            if (GUILayout.Button(captureButton, buttonStyle))
            {
                onCapture(Capture());
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Align camera with scene view"))
            {
                SceneView view = SceneView.lastActiveSceneView;
                cam.transform.position = view.camera.transform.position;
                cam.transform.rotation = view.rotation;
            }

            if (GUILayout.Button("Align scene view with camera"))
            {
                SceneView view = SceneView.lastActiveSceneView;
                view.AlignViewToObject(cam.transform);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Close"))
            {
                Close();
            }
            
            EditorGUILayout.Space(20);
            EditorGUILayout.EndVertical();

            Handles.EndGUI();
        }

        public void Close()
        {
            EditorCameraGUIHelper.CleanupCamera();
            
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= Update;
        }
    }
}