using UnityEngine;
using UnityEngine.UI;

namespace BocuD.BuildHelper
{
    public class BuildHelperToolsMenu : MonoBehaviour
    {
        [Header("Cam menu")]
        public Toggle saveCamPosition;
        public Toggle uniqueCamPosition;

        [Header("Image menu")]
        public Dropdown imageSourceDropdown;
        public Button selectImageButton;
        public RawImage imagePreview;
        public Text aspectRatioWarning;

        public GameObject camOptions;
        public GameObject imageOptions;

        public Material CoverVRCCamMat;
        public Transform CoverVRCCam;

        private Texture2D overrideImage;

        //i hate this. thanks vrc.
        private bool init;
        private void Update()
        {
            if (!init)
            {
                imageSourceDropdown.onValueChanged.AddListener(DropdownUpdate);
                selectImageButton.onClick.AddListener(ChangeImage);
                init = true;
            }
        }

        public void DropdownUpdate(int value)
        {
            switch (value)
            {
                case 0:
                    ShowCamOptions();
                    break;
                case 1:
                    ShowImageOptions();
                    break;
            }
        }

        public void ChangeImage()
        {
            string[] allowedFileTypes = new[] {"png"};
            NativeFilePicker.PickFile(OnFileSelected, allowedFileTypes);
        }

        public void OnFileSelected(string filePath)
        {
            overrideImage = null;
            byte[] fileData;

            if (System.IO.File.Exists(filePath))
            {
                fileData = System.IO.File.ReadAllBytes(filePath);
                overrideImage = new Texture2D(2, 2);
                overrideImage.LoadImage(fileData); //..this will auto-resize the texture dimensions.

                imagePreview.texture = overrideImage;
                CoverVRCCamMat.mainTexture = overrideImage;

                //check aspectRatio and resolution
                if (overrideImage.width * 3 != overrideImage.height * 4)
                {
                    if (overrideImage.width < 1200)
                        aspectRatioWarning.text = "For best results, use a 4:3 image that is at least 1200x900.";
                    else aspectRatioWarning.text = "For best results, use a 4:3 image";
                
                    aspectRatioWarning.color = Color.red;
                }
                else
                {
                    if (overrideImage.width < 1200)
                    {
                        aspectRatioWarning.text = "For best results, use an image that is at least 1200x900.";
                        aspectRatioWarning.color = Color.red;
                    }
                    else
                    {
                        aspectRatioWarning.text = "Your image has the correct aspect ratio and is high resolution. Nice!";
                        aspectRatioWarning.color = Color.green;
                    }
                }
            }
        }
    
        private void ShowCamOptions()
        {
            camOptions.SetActive(true);
            imageOptions.SetActive(false);
        }

        private void ShowImageOptions()
        {
            camOptions.SetActive(false);
            imageOptions.SetActive(true);
        
            Transform VRCCam = GameObject.Find("VRCCam").transform;
            CoverVRCCam.position = VRCCam.position;
            CoverVRCCam.rotation = VRCCam.rotation;
            CoverVRCCam.position += VRCCam.forward;
        }
    }
}
