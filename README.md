Download: https://github.com/BocuD/VRBuildHelper/releases
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
 - Save camera position before uploading
 - much more, read the documentation!

![Main window](https://i.imgur.com/OYRDLpI.png)

Special Thanks: 
@Haï~ for initial deployment manager implementation
@Nestorboy for letting me steal some of his inspector code :)

## Requirements
- VRCSDK3 WORLD 2021.10.22.18.33 or later (earlier versions are untested but may work)
- UdonSharp 0.20.0 or later (earlier versions are untested but may work)
## Documentation
### Getting started
To set up VR Build Helper in a scene, open the editor window [Window/VR Build Helper] and click Set up Build Helper in this scene. You can now create a new branch by clicking the + button in the branch list.
### Known issues
- The tags editor is currently broken (semi on purpose, this will be fixed later)
### Branches
A branch contains information about a specific version of your world. You can set up a number of build overrides for a branch, for example the world pipeline id or GameObjects that should be excluded from the build. An example setup someone might use:
 - Main branch, containing the live uploaded version of their world
 - Dev branch, using a different pipeline id from the main branch so it gets uploaded as a different world, which includes a number of GameObjects to assist in testing that are not present on the main branch
### VRChat World Editor
You can change you world name, decription, tags, playercount and image right from the editor window, on a per branch basis. Any changes you make will be applied to the upload window automatically on the next build - or be applied autonomously if using the Autonomous builder.

![World Editor](https://i.imgur.com/fQhhoEx.png)
### Blueprint ID Editor
When selecting the blueprint ID for a branch, you won't have to manually (ew, what are we, monkeys?) copy paste it any more.
![Blueprint ID editor](https://i.imgur.com/Wwf92Cp.png)
### GameObject Overrides
You can specify a list of GameObjects that should be excluded from a given branch by enabling GameObject overrides for that branch. Exclusive GameObjects will only be included in builds for a branch that has them in the Exclusive GameObject list. Excluded GameObjects will not be included in builds for that branch.

![GameObject Overrides](https://i.imgur.com/4mcGfzy.png)
### Deployment Manager
Every build uploaded with the Deployment Manager active will be saved to the specified folder. You can later reupload or retest these builds, to see what your progress is or if a newer upload contains issues you can't easily fix.

![Deployment Manager](https://i.imgur.com/lGJnqee.png)
### Build tracking
VR Build Helper tracks every build and upload you do for a branch, for both PC and mobile platforms. It automatically keeps a build number which is updated every time you create a new build. If you switch platforms and do a new build, VR Build Helper will attempt to detect if any changes were made after switching. If not, the build numbers for the new platform will automatically be updated to match the other one.

![Build tracker](https://i.imgur.com/bOh7ECb.png)
### Udon Link
You can access information about the branch and build at runtime. Since the build number is accessible, this can be used to detect version errors between two different builds - for example when a Quest user joins a PC world that has not been updated for their platform yet, or right after an update was published to notify users that are still on the old build that they should rejoin. It can also be used to automatically display information about a current development build by using the TMP options. Branch information is also available.

![Udon Link](https://i.imgur.com/wZTtaXR.png)
### Streamlined build tools
Build options are cleaned up

![Streamlined build tools](https://i.imgur.com/gzQZCY1.png)
### Autonomous builder
You can upload your world for both platforms with the press of a single button. The autonomous builder will then build your world for the first platform, upload it, switch platform, build for the second platform, upload it, and then switch back to the platform you were originally on. It can of course also be used to upload to a single platform autonomously. (no more pressing upload buttons!)
![Autonomous Builder](https://i.imgur.com/uFU8Grq.gif)
### Upload tools
When uploading your world, VR Build Helper lets you save the camera position so you don't have to move it every time. You can specify if the camera position should be unique to this branch or applied to all branches.
VR Build Helper can also upload custom images instead of using the camera.
![Build Tools](https://i.imgur.com/To6ohuf.gif)
