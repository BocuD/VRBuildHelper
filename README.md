# Note: Due to recent changes in the VRChat SDK this project is currently no longer considered "supported". It will most likely continue to function, but until a rewrite is done I would recommend against using it. A new version is in the works which will support the current version of the SDK in a better way.

# VR Build Helper
An integrated editor toolset that adds a number of quality of life features to assist VRChat world creators in managing their projects.
## Features
 - Manage multiple branches of a world that upload to seperate pipeline ids
 - Keep track of any builds and uploads you do for each branch and manage build numbers across PC and mobile platforms
 - Streamlined build interface
 - Convenient blueprint ID selection and switching
 - Automatically backup uploaded worlds and retest or reupload them later
 - Automatically upload your world with a single button press - and even upload for two platforms in one go autonomously
 - Upload custom images for your world
 - Edit your world name, description, tags, image etc from the editor
 - Export .vrcw files for worlds
 - Editor mode persistent camera
 - Save camera position before uploading
 - much more, read the documentation!

![Main window](https://i.imgur.com/lt40krp.png)

# Installation
## Installation via VRChat Creator Companion (recommended):
Add [this](https://bocud.github.io/BocuDPackages/) repository to your CreatorCompanion sources. From there VRChatApiTools should show up within the list of available packages.

## Installation through a UnityPackage
1. Download latest unitypackage release from [here](https://github.com/BocuD/VRBuildHelper/releases/latest)
2. Install the downloaded unitypackage

Special Thanks: 
@Haï~ for initial deployment manager implementation
@Nestorboy for letting me steal some of his inspector code :)

## Requirements
- Latest version of Package Manager version of VRChat SDK (installed by [VRChat Creator Companion](https://vcc.docs.vrchat.com/))
- UdonSharp v1.0.0 or later
- VRChatApiTools v0.4.0 or later https://github.com/BocuD/VRChatApiTools/

### For complete documentation, please visit [the wiki](https://github.com/BocuD/VRBuildHelper/wiki).
