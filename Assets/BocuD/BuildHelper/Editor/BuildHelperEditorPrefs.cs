using UnityEditor;

namespace BocuD.BuildHelper
{
    [InitializeOnLoad]
    public static class BuildHelperEditorPrefs
    {
        static BuildHelperEditorPrefs()
        {
            _autoSave = EditorPrefs.GetBool("BuildHelperAutoSave");
            _useAsyncPublish = EditorPrefs.GetBool("BuildHelperAsyncPublish");
            _buildNumberMode = EditorPrefs.GetInt("BuildNumberMode");
        }

        private static bool _autoSave;
        public static bool AutoSave
        {
            set
            {
                if (_autoSave == value) return;
                
                _autoSave = value;
                EditorPrefs.SetBool("BuildHelperAutoSave", value);
            }
            get => _autoSave;
        }

        private static bool _useAsyncPublish;
        public static bool UseAsyncPublish
        {
            set
            {
                if (_useAsyncPublish == value) return;
                
                _useAsyncPublish = value;
                EditorPrefs.SetBool("BuildHelperAsyncPublish", value);
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
                EditorPrefs.SetInt("BuildNumberMode", value);
            }

            get => _buildNumberMode;
        }
    }
}