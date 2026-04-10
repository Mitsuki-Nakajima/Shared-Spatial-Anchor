# Shared Spatial Anchor / Metaverse Prototype (Unity + Meta Quest)

## Overview

This project is a minimal prototype for a multiplayer mixed reality (MR) metaverse space based on the NYU Makerspace.

The goal is to allow users in different physical locations to join the same **virtual Makerspace** and interact with each other.


## Current Progress

* Basic Unity MR scene setup (XR Origin)
* Multiplayer setup using Netcode for GameObjects
* Player spawning via NetworkManager
* Basic player avatar (head + hands)
* Initial structure for shared coordinate system (`SharedSpaceManager`)
* Calibration system (in progress)


## Project Structure

```
Assets/
  Scripts/
    SessionBootstrap.cs
    SharedSpaceManager.cs
    CalibrationManager.cs
    PlayerRigTracker.cs

Scene:
  XR Origin (local tracking)
  NetworkManager
  MakerspaceRoot (shared virtual space)
  PlayerAvatar (networked prefab)
```


## How It Works

Each player:

1. Uses XR Origin for local headset/controller tracking
2. Maps their local position into a shared virtual Makerspace coordinate system
3. Sends their transformed pose over the network
4. Other players render that pose in the same virtual space


## How to Run

### 1. Clone the repo

```
git clone https://github.com/Mitsuki-Nakajima/Shared-Spatial-Anchor.git
```

### 2. Open in Unity

* Open Unity Hub
* Add project
* Open with Unity 6 (or compatible version)

### 3. Run

* Press Play
* Click **Host** (one instance)
* Click **Client** (another instance or device)


## Notes

* `Library/` and other build files are excluded via `.gitignore`
* This is a prototype — not all features are fully implemented yet
* Calibration and spatial alignment are still being refined


## TO-DO

* Implement proper calibration system
* Improve spatial alignment accuracy
* Test with multiple Quest devices
* Add interaction between users
