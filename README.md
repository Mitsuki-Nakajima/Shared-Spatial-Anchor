# Shared Spatial Anchor / Multiplayer Mixed Reality Prototype

## Overview

This project explores how to build a multiplayer mixed reality environment using Unity and Meta Quest 3.

The larger goal of the project is to create a virtual version of the NYU Makerspace where users can interact together in the same metaverse environment. Ideally, this includes both users who are physically inside the Makerspace and users who are joining remotely from different real-world locations.

The main technical question of this prototype is:

> How can multiple users in different physical spaces share position and movement information inside one common virtual Makerspace?

This report is written as a tutorial and progress report so that other team members can understand the current implementation and continue development.



## Project Goal

The goal of this prototype is to move away from relying entirely on Meta’s Multiplayer Mixed Reality template and instead rebuild the minimum necessary systems from scratch.

The prototype focuses on:

- setting up a clean Unity mixed reality project
- understanding the core ideas behind multiplayer MR
- creating a shared virtual Makerspace coordinate system
- spawning networked players
- synchronizing basic player avatar movement
- preparing a calibration system for aligning local user space with the shared virtual space


## Problem

In a normal same-room mixed reality application, multiple users can share the same physical environment. In that case, shared spatial anchors or colocation systems can help everyone agree on the same physical reference point.

Our project has a more difficult requirement:

- one user may be inside the actual NYU Makerspace
- another user may join from home
- another user may join from a different room or building

These users do not share the same real-world coordinate system.

Therefore, the challenge is not only networking. The challenge is also spatial alignment.

Each headset has its own local tracking space. To make everyone appear in the same virtual Makerspace, each user’s local position must be converted into a shared virtual coordinate system.



## Design Approach

The current design separates the system into two spaces:

1. **Local XR Space**
   - This is the coordinate system created by each user’s headset.
   - It tracks the user’s head and hands relative to their own physical room.

2. **Shared Makerspace Space**
   - This is the common virtual coordinate system for the NYU Makerspace model.
   - All networked avatars should be displayed relative to this space.

The intended workflow is:

1. The user’s headset and hands are tracked locally by `XR Origin`.
2. The local pose is converted into the shared Makerspace coordinate system.
3. The converted pose is sent through the network.
4. Other users render that avatar in the shared virtual Makerspace.

This makes it possible for users in different physical rooms to still appear together in one virtual environment.



## Current Implementation

The current Unity scene contains the following main objects:

```text
XR Origin (VR)
NetworkManager
SharedSpaceManager
CalibrationManager
MakerspaceRoot
SpawnPoints
PlayerAvatar
UI
EventSystem
```

### XR Origin

`XR Origin (VR)` is used for local headset and controller tracking. It represents the local user’s real-world tracking space.

The XR Origin is not treated as the networked player object. Instead, it is used only to read local tracking data.

### NetworkManager

`NetworkManager` handles multiplayer networking using Unity Netcode for GameObjects.

It is responsible for:

- starting a host session
- starting a client session
- spawning the player prefab
- managing networked objects

### MakerspaceRoot

`MakerspaceRoot` represents the shared virtual NYU Makerspace coordinate system.

In the current prototype, this can be a simple placeholder scene. Later, it can be replaced with a more accurate model of the Makerspace.

### PlayerAvatar

`PlayerAvatar` is the networked player representation.

The current avatar is simple and uses basic shapes:

- a sphere for the head
- a cube for the left hand
- a cube for the right hand

This keeps the prototype simple while still allowing us to test player position and movement synchronization.

### SharedSpaceManager

`SharedSpaceManager` is responsible for converting local XR poses into the shared Makerspace coordinate system.

It stores a reference to the Makerspace root and provides a method for applying the alignment matrix.

### CalibrationManager

`CalibrationManager` stores the alignment transformation between a user’s local XR space and the shared virtual Makerspace space.

The calibration workflow is not fully finished yet, but the structure is prepared.

### SessionBootstrap

`SessionBootstrap` provides simple functions for starting the project as a host or client.

The UI buttons call these functions.

### PlayerRigTracker

`PlayerRigTracker` connects the local XR tracking data to the networked avatar.

It reads:

- headset transform
- left hand transform
- right hand transform

Then it applies the shared-space conversion and updates the avatar’s head and hand objects.



## Tutorial: How to Set Up the Project

### 1. Create a Unity Project

Create a new Unity 3D project.

Recommended setup:

- Unity 6 or compatible Unity version
- Android build support
- Meta Quest 3 as the target device



### 2. Install Required Packages

Install the following packages:

- XR Plug-in Management
- OpenXR
- XR Interaction Toolkit
- Unity Netcode for GameObjects
- Unity Transport

These packages are needed for Quest XR tracking and multiplayer networking.



### 3. Enable XR for Quest

In Unity:

1. Open `Edit > Project Settings`
2. Go to `XR Plug-in Management`
3. Enable XR for Android
4. Select OpenXR
5. Enable Meta Quest support if available



### 4. Add XR Origin

In the Unity menu:

```text
GameObject > XR > XR Origin (VR)
```

This creates the local player tracking rig.

The important tracked objects are usually:

```text
XR Origin (VR)
└── Camera Offset
    ├── Main Camera
    ├── LeftHand
    └── RightHand
```

These transforms are used later by `PlayerRigTracker`.



### 5. Add Networking

Create an empty GameObject named:

```text
NetworkManager
```

Add the following components:

- `NetworkManager`
- `Unity Transport`
- `SessionBootstrap`

Create UI buttons for:

- Host
- Client

The Host button should call:

```text
SessionBootstrap.StartHostSession()
```

The Client button should call:

```text
SessionBootstrap.StartClientSession()
```



### 6. Create the Shared Makerspace Root

Create an empty GameObject named:

```text
MakerspaceRoot
```

This object represents the shared virtual coordinate system.

For now, it can contain placeholder objects such as:

- floor plane
- walls
- tables
- equipment blocks

Later, this can be replaced with a more accurate NYU Makerspace model.



### 7. Create SharedSpaceManager

Create an empty GameObject named:

```text
SharedSpaceManager
```

Attach the `SharedSpaceManager.cs` script.

In the Inspector, drag `MakerspaceRoot` into the `Makerspace Root` field.



### 8. Create CalibrationManager

Create an empty GameObject named:

```text
CalibrationManager
```

Attach the `CalibrationManager.cs` script.

This script stores the local-to-shared alignment matrix.



### 9. Create PlayerAvatar Prefab

Create an empty GameObject named:

```text
PlayerAvatar
```

Add three child objects:

```text
PlayerAvatar
├── Head
├── LeftHand
└── RightHand
```

Recommended temporary shapes:

- `Head`: sphere
- `LeftHand`: cube
- `RightHand`: cube

Add the following components to `PlayerAvatar`:

- `NetworkObject`
- `NetworkTransform`
- `PlayerRigTracker`

Then drag `PlayerAvatar` into the Project window to make it a prefab.

Assign this prefab to the `Player Prefab` field in `NetworkManager`.



### 10. Run the Project

To test in the Unity Editor:

1. Press Play
2. Click the Host button
3. Confirm that `PlayerAvatar(Clone)` appears in the Hierarchy

This confirms that the network session can start and the player prefab can spawn.

For full multiplayer testing, the project should be tested with multiple instances or multiple Quest devices.



## Code Structure

The main scripts are:

```text
Assets/Scripts/SessionBootstrap.cs
Assets/Scripts/SharedSpaceManager.cs
Assets/Scripts/CalibrationManager.cs
Assets/Scripts/PlayerRigTracker.cs
```

### SessionBootstrap.cs

Purpose:

- starts host session
- starts client session

### SharedSpaceManager.cs

Purpose:

- stores reference to `MakerspaceRoot`
- converts local poses into shared virtual coordinates

### CalibrationManager.cs

Purpose:

- stores alignment matrix
- calculates the offset between local XR space and target virtual spawn point

### PlayerRigTracker.cs

Purpose:

- reads local XR head and hand transforms
- applies shared-space conversion
- updates the networked avatar transforms



## Current Progress

Completed so far:

- Explored Meta Multiplayer Mixed Reality template
- Created a clean Unity project structure
- Added XR Origin for Quest-style tracking
- Added NetworkManager and Unity Transport
- Created Host and Client buttons
- Created basic networked PlayerAvatar prefab
- Confirmed that the player avatar spawns when starting a host session
- Added scripts for session startup, shared-space conversion, calibration structure, and player rig tracking
- Uploaded the Unity project to GitHub with a Unity `.gitignore`

In progress:

- full calibration button / UI
- testing with actual Quest devices
- verifying head and hand movement synchronization on device
- accurate alignment between real space and virtual Makerspace
- replacing placeholder geometry with a more accurate Makerspace model



## Known Problems and Concerns

### 1. Shared Spatial Anchors vs Remote Users

Shared Spatial Anchors are useful when multiple users are in the same physical location.

However, our project also needs users to join from different locations. This means Shared Spatial Anchors alone may not solve the full problem.

For remote users, we likely need a shared virtual coordinate system and a calibration method instead of relying only on same-room colocation.

### 2. Calibration Accuracy

The biggest technical concern is calibration.

If each user maps their local space slightly differently, avatars may appear offset or rotated incorrectly.

Possible problems include:

- incorrect spawn alignment
- different forward directions
- different floor heights
- headset tracking drift
- mismatch between physical room size and virtual Makerspace size

### 3. Networking Limitations

The current networked avatar is very simple.

It is enough for early testing, but future versions will need:

- smoother movement
- better interpolation
- hand/controller state syncing
- object interaction syncing
- possibly voice or communication features

### 4. Template Complexity

The Meta template was helpful for learning, but it contains many extra systems.

Pros of the template:

- useful reference
- demonstrates multiplayer MR structure
- includes many built-in features

Cons of the template:

- difficult to isolate only the parts we need
- includes miscellaneous systems
- harder to explain and maintain
- not directly designed for our remote Makerspace use case



## Advantages of the Current Approach

### Lightweight Structure

By rebuilding from scratch, the project is easier to understand and debug.

The current version only includes the systems that are directly related to our goal.

### Better for Learning

This approach makes it clearer how each part works:

- XR tracking
- networking
- player spawning
- pose conversion
- calibration

### More Flexible for Remote Participation

Using a shared virtual Makerspace coordinate system makes the design more flexible than relying only on same-room shared anchors.

This approach can support users who are not physically in the same place.



## Disadvantages of the Current Approach

### More Work Required

Because we are not relying entirely on the template, we need to manually implement more systems.

### Calibration Is Still Difficult

The biggest unresolved issue is still how to make calibration accurate and easy for users.

### Current Visuals Are Temporary

The current avatar and Makerspace environment are placeholders.

They are useful for testing, but not final presentation-quality assets.



## Future Work

The next development steps are:

1. Add a working calibration button
2. Test with two Quest 3 devices
3. Verify that head and hand movement are synced correctly
4. Improve avatar movement smoothing
5. Build or import an accurate Makerspace model
6. Add object interaction
7. Synchronize interactable objects across the network
8. Explore persistent anchors for users physically inside the Makerspace
9. Add UI for joining, calibration, and debugging
10. Document the final workflow for future team members



## Conclusion

This project is an early prototype for a multiplayer mixed reality NYU Makerspace environment.

The main contribution so far is not a finished application, but a simplified architecture for understanding and rebuilding the important parts of a multiplayer MR system.

The prototype demonstrates:

- how to set up a clean Unity MR project
- how to start host/client networking
- how to spawn networked players
- how to represent users with simple head and hand avatars
- how a shared virtual coordinate system can be used as the foundation for remote mixed reality collaboration

The next major step is to complete calibration and test the system on multiple Meta Quest 3 devices.



## Repository Notes

This repository should include:

```text
Assets/
Packages/
ProjectSettings/
README.md
.gitignore
```

The following folders should not be committed:

```text
Library/
Temp/
Obj/
Build/
Builds/
Logs/
UserSettings/
```

These files are ignored because Unity can regenerate them locally.
