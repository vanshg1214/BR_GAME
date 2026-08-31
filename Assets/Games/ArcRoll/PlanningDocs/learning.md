# ArcRoll - Developer Learnings from PopStrike & Whack a Mole

This document outlines key engineering patterns, calibration systems, folder structures, and physical mechanics analyzed from the existing games (**PopStrikeVR** and **Whack a Mole**) to ensure **ArcRoll** is built cohesively, professionally, and optimized for VR rehabilitation.

## 1. Project Organization & Clean Architecture

In our existing codebase:
- **Whack a Mole** uses structured folders under `Assets/Script/` (e.g., `Core`, `Moles`, `UI`, `Interactions`, `Calibration`).
- **PopStrikeVR** uses modular folders inside its own subdirectory `Assets/Games/PopStrikeVR/Scripts/` (e.g., `Core`, `Gameplay`, `Data`, `UI`).

### Directory Layout for ArcRoll
To keep scripts structured and isolated, we will organize `Assets/Games/ArcRoll/Scripts/` into:
- `Core/` (`ArcRollGameManager.cs`, `ArcRollLevelDirector.cs`, `CSVLevelParser.cs`)
- `Gameplay/` (`Ball.cs`, `CannonController.cs`, `WorkspaceMapper.cs`)
- `Grid/` (`GridSpawner.cs`, `BasketballHoop.cs`, `BowlingPinSet.cs`)
- `Obstacles/` (`ObstacleBase.cs`, `SlidingBarrier.cs`, etc.)

---

## 2. ROM & Calibration Mapping (Therapeutic Engine)

Rehabilitation games must strictly respect the patient's Range of Motion (ROM) to prevent injury and customize difficulty.

### How it works in existing games:
1. **Whack a Mole (`WorkspaceMapper.cs`):** 
   - Maps normalized grid ratios (-0.5 to 0.5) to a flat board layout.
   - Scale is calculated dynamically from the patient's profile: `armLength`, `maxFlexion` (depth), and `maxAbduction` (width).
2. **PopStrikeVR (`WorkspaceMapper.cs`):**
   - Maps CSV spherical coordinates (Azimuth, Elevation) to Cartesian coordinates relative to a shoulder pivot (`shoulderOffset = new Vector3(0f, -0.20f, -0.15f)` below head).
   - Locks the depth (Z-axis) to a constant `safeRadius` based on patient wingspan to avoid demanding depth changes.

### Extracting this for ArcRoll:
- **Cannon Target (The Catch):** The cannons need to shoot the balls exactly to the outer edges of the patient's comfortable wingspan (sideways flexion/extension). We can query `PatientProfileSO` / `RehabProfileSO` to calculate the left-extreme (Min Azimuth/Abduction) and right-extreme (Max Azimuth/Abduction) coordinates.
- **The 3x5 Grid:** The grid will be generated in front of the player. We will scale the width and height of this 3x5 grid using the patient's calibrated `armLength` and `maxElevation/maxFlexion` to ensure all hoops and pins are fully reachable but force a therapeutic range of motion.

---

## 3. Physical Mechanics & Throwing Trajectory

- **Catching:** The balls are shot **towards** the player from the side cannons. The player intercepts them at their ROM boundaries.
- **Throwing (The Target):** Once caught, the player throws or rolls the ball **forward** towards the 3x5 grid.
- **Elbow Extension Speed & Angle:** 
  - For the **Basketball**, we need to map the release velocity directly to the player's elbow extension. Higher hoops are spawned higher up in the grid, requiring more upward angle and release velocity. We will use Unity XR's velocity scale parameter dynamically based on hoop height.
  - For the **Bowling Ball**, the player must swing their arm low and release the ball to roll along the floor towards the pins. The bowling ball physics material will have minimal friction (`0.1`) and `0` bounciness to guarantee a smooth roll.
