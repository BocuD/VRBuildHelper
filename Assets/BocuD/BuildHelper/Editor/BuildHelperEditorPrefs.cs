using UnityEditor;

namespace BocuD.BuildHelper
{
    [InitializeOnLoad]
    public static class BuildHelperEditorPrefs
    {
        private const string asyncPublishPath = "BuildHelperAsyncPublish";
        private const string buildNumberPath = "BuildHelperNumberMode";
        private const string platformSwitchPath = "BuildHelperNumberMode";
        private const string buildOnlyPath = "BuildHelperBuildOnly";
        
        static BuildHelperEditorPrefs()
        {
            _useAsyncPublish = EditorPrefs.GetBool(asyncPublishPath);
            _buildNumberMode = EditorPrefs.GetInt(buildNumberPath);
            _platformSwitchMode = EditorPrefs.GetInt(platformSwitchPath);
            _showBuildOnly = EditorPrefs.GetBool(buildOnlyPath);
        }
        
        private static bool _useAsyncPublish;
        public static bool UseAsyncPublish
        {
            set
            {
                if (_useAsyncPublish == value) return;
                
                _useAsyncPublish = value;
                EditorPrefs.SetBool(asyncPublishPath, value);
            }
            get => _useAsyncPublish;
        }
        
        private static int _buildNumberMode;
        public static int BuildNumberMode
        {
            set
            {
                if (_buildNumberMode == value) return;
                
                _buildNumberMode = value;
                EditorPrefs.SetInt(buildNumberPath, value);
            }
            get => _buildNumberMode;
        }

        private static int _platformSwitchMode;

        public static int PlatformSwitchMode
        {
            set
            {
                if (_platformSwitchMode == value) return;

                _platformSwitchMode = value;
                EditorPrefs.SetInt(platformSwitchPath, value);
            }

            get => _platformSwitchMode;
        }
        
        private static bool _showBuildOnly;
        public static bool ShowBuildOnly
        {
            set
            {
                if (_showBuildOnly == value) return;
                
                _showBuildOnly = value;
                EditorPrefs.SetBool(asyncPublishPath, value);
            }
            get => _showBuildOnly;
        }
    }
}