using System;
using TMPro;
using UdonSharp;

namespace BocuD.BuildHelper
{
    public class BuildHelperUdon : UdonSharpBehaviour
    {
        public string branchName;
        public DateTime buildDate;
        public int buildNumber;

        public TextMeshPro tmp;

        private void Start()
        {
            tmp.text = $"{branchName}\nBuild {buildNumber}\n{buildDate}";
        }
    }
}
