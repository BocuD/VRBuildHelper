using System;
using UnityEditor;
using UnityEditor.PackageManager.UI;
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

            if (cam == null) return;

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
        private Action<Texture2D> onCapture;
        private Camera cam;
        private RenderTexture preview;
        private CameraData _camData;

        private SerializedObject camHelperSO;
        private SerializedProperty postProcessing;
        private SerializedProperty postProcessingLayer;
        private SerializedProperty antiAliasingMode;

        private SerializedObject camTransformSO;
        private SerializedProperty position;
        private SerializedProperty rotation;

        private GUIContent captureButton;
        private GUIContent saveButton;

        private GUIStyle background;

        public EditorCameraGUI(Action<Texture2D> onComplete = null)
        {
            onCapture = onComplete;
            preview = new RenderTexture(1200, 900, 24);

            SetupCamera();

            if (onComplete != null)
            {
                captureButton = new GUIContent
                {
                    image = Resources.Load<Texture2D>("Icons/d_FrameCapture@2x"),
                    text = " Capture Image"
                };
            }	

            saveButton = new GUIContent
            {
                image = EditorGUIUtility.IconContent("d_SaveAs@2x").image,
                text = " Capture and Export Image"
            };
            
            background = new GUIStyle(GUI.skin.box)
            {
                normal =
                {
                    background = ImageTools.BackgroundTexture(32, 32, new Color(0.22f, 0.22f, 0.22f, 0.85f))
                }
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

            camTransformSO = new SerializedObject(cam.transform);
            position = camTransformSO.FindProperty("m_LocalPosition");
            rotation = camTransformSO.FindProperty("m_LocalRotation");
            
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


        private bool followMouse = false;
        private Vector3 moveCamera = Vector3.zero;
        private Vector3 moveCameraLerp = Vector3.zero;
        
        public bool windowMode = false;
        public EditorCameraWindow cameraWindow;
        
        public void OnSceneGUI(SceneView sceneview)
        {
            Handles.BeginGUI();

            if (windowMode)
            {
                EditorGUILayout.BeginVertical(background, GUILayout.Width(600));
                EditorGUILayout.LabelField("BuildHelper Camera", EditorStyles.boldLabel);

                if (GUILayout.Button("Dock Camera window"))
                {
                    windowMode = false;
                    cameraWindow.Close();
                }
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.BeginVertical(background, GUILayout.Width(600));
                DrawCameraGUI();
                EditorGUILayout.EndVertical();
            }

            Handles.EndGUI();
        }

        public void DrawCameraGUI()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(450));
            
            EditorGUI.indentLevel++;
            
            using (EditorGUI.ChangeCheckScope changeCheckScope = new EditorGUI.ChangeCheckScope())
            {
                camHelperSO.Update();

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

            GUILayout.Space(5);

            EditorGUILayout.LabelField("Camera Transform", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Camera Position");
            cam.transform.position = EditorGUILayout.Vector3Field("", cam.transform.position);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Camera Rotation");
            cam.transform.localEulerAngles = EditorGUILayout.Vector3Field("", cam.transform.localEulerAngles);
            EditorGUILayout.EndHorizontal();

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
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            
            EditorGUILayout.LabelField("Camera Preview", EditorStyles.boldLabel);

            EditorGUI.indentLevel--;
            
            Rect previewRect = GUILayoutUtility.GetRect(600, 450, GUILayout.ExpandWidth(true));
            GUI.Box(previewRect, preview);
            {
                switch (Event.current.type)
                {
                    case EventType.MouseDown:
                        Vector2 mousePosition = Event.current.mousePosition;
                        if (previewRect.Contains(mousePosition))
                        {
                            followMouse = true;
                            Event.current.Use();
                        }

                        break;

                    case EventType.MouseUp:
                        if (followMouse)
                        {
                            followMouse = false;
                            moveCamera = Vector3.zero;
                            Event.current.Use();
                        }

                        break;

                    case EventType.MouseDrag:
                        if (followMouse)
                        {
                            cam.transform.RotateAround(cam.transform.position, Vector3.up,
                                (Event.current.delta.x / 10));
                            cam.transform.RotateAround(cam.transform.position, cam.transform.right,
                                (Event.current.delta.y / 10));
                            Event.current.Use();
                        }

                        break;

                    case EventType.KeyDown:
                        if (followMouse)
                        {
                            switch (Event.current.keyCode)
                            {
                                case KeyCode.W:
                                    moveCamera.z += 1;
                                    break;

                                case KeyCode.S:
                                    moveCamera.z -= 1;
                                    break;

                                case KeyCode.A:
                                    moveCamera.x -= 1;
                                    break;

                                case KeyCode.D:
                                    moveCamera.x += 1;
                                    break;
                            }
                        }

                        break;

                    case EventType.KeyUp:
                        if (followMouse)
                        {
                            switch (Event.current.keyCode)
                            {
                                case KeyCode.W:
                                    moveCamera.z -= 1;
                                    break;

                                case KeyCode.S:
                                    moveCamera.z += 1;
                                    break;

                                case KeyCode.A:
                                    moveCamera.x += 1;
                                    break;

                                case KeyCode.D:
                                    moveCamera.x -= 1;
                                    break;
                            }
                        }

                        break;
                }
            }
            
            EditorGUI.indentLevel++;

            //force higher frequency updates if we are still moving the camera
            moveCameraLerp = Vector3.Lerp(moveCameraLerp, moveCamera * 0.1f, 0.05f);
            cam.transform.position += cam.transform.rotation * moveCameraLerp;

            if (followMouse || moveCameraLerp.magnitude > 0.01f)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fixedHeight = 25
            };

            EditorGUILayout.BeginHorizontal();
            if (captureButton != null)
            {
                if (GUILayout.Button(captureButton, buttonStyle))
                {
                    onCapture(Capture());
                }
            }

            if (GUILayout.Button(saveButton, buttonStyle))
            {
                Texture2D image = Capture();
                byte[] output = image.EncodeToPNG();

                string location = EditorUtility.SaveFilePanel("Save Image", Application.dataPath, "image", "png");
                System.IO.File.WriteAllBytes(location, output);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (!windowMode)
            {
                if (GUILayout.Button("Undock window"))
                {
                    windowMode = true;
                    cameraWindow = EditorCameraWindow.OpenWindow(this);
                }
            }
            else
            {
                if (GUILayout.Button("Dock to scene view"))
                {
                    windowMode = false;
                    cameraWindow.Close();
                }
            }

            if (GUILayout.Button("Close"))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(20);
        }

        public void Close()
        {
            EditorCameraGUIHelper.CleanupCamera();

            if (windowMode)
            {
                cameraWindow.Close();
            }
            
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= Update;
        }
    }

    public class EditorCameraWindow : EditorWindow
    {
        private EditorCameraGUI helperInstance;
        
        public static EditorCameraWindow OpenWindow(EditorCameraGUI helper)
        {
            EditorCameraWindow window = GetWindow<EditorCameraWindow>();
            
            window.titleContent = new GUIContent("BuildHelper Camera");
            window.helperInstance = helper;
            
            EditorApplication.update += window.Repaint;

            return window;
        }
        
        public void OnGUI()
        {
            if (helperInstance == null) Close();
            
            helperInstance.DrawCameraGUI();
        }

        private void OnDestroy()
        {
            if (helperInstance != null) helperInstance.windowMode = false;
            
            EditorApplication.update -= Repaint;
        }
    }
}