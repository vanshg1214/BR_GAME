# PopStrike VR — Scene Structure & Configuration Reference

Scene organization, hierarchy, and component relationships across **PopStrike VR**.

---

## 1. Scene Map Overview

PopStrike VR consists of 3 primary scenes:

| Scene File | Path | Role | Key Systems |
|---|---|---|---|
| **`PoPStrikeVRMenu.unity`** | `Assets/Games/PopStrikeVR/Scenes/` | Main Menu & Settings Hub | `PopstrikeMenuManager`, `Menu_Canvas`, `OVRCameraRig` |
| **`PopStrikeVRGameScene.unity`** | `Assets/Games/PopStrikeVR/Scenes/` | Night Environment Gameplay | `PopstrikeLevelDirector`, Night Skybox, Level HUD, Results UI |
| **`PopStrikeVRMorn_Scene.unity`** | `Assets/Games/PopStrikeVR/Scenes/` | Morning Environment Gameplay | `PopstrikeLevelDirector`, Daylight Skybox, Level HUD, Results UI |

---

## 2. Main Menu Scene (`PoPStrikeVRMenu.unity`)

### Purpose
Serves as the entry point for PopStrike VR. The player or therapist selects session settings before starting gameplay.

### Hierarchy & Key Objects
```
PoPStrikeVRMenu
├── Directional Light             (Sunlight)
├── OVRCameraRig                  (VR Headset & Tracking Rig)
│   └── TrackingSpace
│       ├── LeftEyeAnchor / RightEyeAnchor
│       ├── LeftHandAnchor  ---> OVRHandPrefab (Left Hand)
│       └── RightHandAnchor ---> OVRHandPrefab (Right Hand)
├── OVRInteractionComprehensive   (Meta XR Touch Interaction SDK)
├── Menu_Canvas                   (3D World-Space Menu UI)
│   └── MenuPanel
│       ├── HandModePills        (Left, Both, Right)
│       ├── DifficultyPills      (Easy, Medium, Hard)
│       ├── DurationPills        (3 Min, 5 Min)
│       ├── GestureButton        (Gestures: ON / OFF)
│       ├── EnvButton            (ENV: MORN / ENV: NIGHT)
│       ├── PlayButton           (Launches Gameplay)
│       └── ExitButton           (Quits or returns to launcher)
├── SkyBoxManager                 (Applies active skybox texture)
└── Global Volume                 (URP Post-Processing: Bloom, Color Adjustments)
```

### Menu Canvas Configuration
The `Menu_Canvas` GameObject contains `VRCanvasAutoPositioner.cs`, but the component is **disabled** in the Inspector. This ensures the menu stays at fixed, hand-crafted scene coordinates (`Pos X: 0, Pos Y: 2, Pos Z: 3`) rather than auto-snapping to the player's view on startup.

---

## 3. Night Gameplay Scene (`PopStrikeVRGameScene.unity`)

### Purpose
The primary gameplay environment for PopStrike VR, set in a dark night environment with glowing targets and bloom post-processing.

### Hierarchy & Key Objects
```
PopStrikeVRGameScene
├── Directional Light             (Moonlight intensity)
├── OVRCameraRig                  (VR Headset & Hand Tracking)
├── OVRInteractionComprehensive   (Meta Interaction SDK)
├── PopStrike_Manager             (Contains PopstrikeLevelDirector & PopstrikeFeedbackManager)
├── PopstrikePooler               (Object Pooler for balloons, hit indicators, and VFX)
├── GameplayHUDCanvas             (VR HUD: Timer, Score, Combo, Stage Progress)
├── TMTSolver                    (TMT 3D line drawing & sequence solver)
├── TracePathManager             (Green trace corridor manager)
├── LevelResultsUI                (Post-stage summary dashboard)
└── NightEnvironmentAssets        (Platform, Trees, Night Skybox)
```

### Manager Wiring
- `PopStrike_Manager` holds both `PopstrikeLevelDirector.cs` and `PopstrikeFeedbackManager.cs`.
- `PopstrikeLevelDirector` connects to `PopstrikePooler`, `TMTSolver`, `TracePathManager`, `GameplayHUDCanvas`, and `LevelResultsUI`.

---

## 4. Morning Gameplay Scene (`PopStrikeVRMorn_Scene.unity`)

### Purpose
Identical in logic and manager wiring to `PopStrikeVRGameScene.unity`, but configured with a bright daylight environment for players who prefer high visibility.

### Scene Differences:
- Directional Light intensity increased for daylight.
- Skybox set to morning atmosphere (`CloudyMorning` / `CasualDay`).
- Adjusted URP Post-Processing profile.

---

## 5. Developer Setup & OpenXR Troubleshooting

### Hand Tracking Not Visible in Unity Editor?
If hand models do not render when testing in the Editor via Quest Link:
1. Open **Edit -> Project Settings -> XR Plug-in Management -> OpenXR** (PC/Standalone tab with monitor icon).
2. Under **Meta XR**, verify that the following three options are checked:
   - `Hand Tracking Subsystem`
   - `Hand Interaction Poses`
   - `Meta Hand Tracking Aim`
3. Restart the Unity Editor (Unity requires a restart after modifying OpenXR subsystems).
4. Put down controllers completely so the headset switches to hand-tracking mode.

### Scene Transition Mechanics
When Play is pressed in `PopstrikeMenuManager`:
1. Player selections are saved to `TemporarySessionData`.
2. `PopstrikeMenuManager` checks `TemporarySessionData.IsMorningScene`.
3. If `IsMorningScene == true`, `PopStrikeVRMorn_Scene` is loaded.
4. If `IsMorningScene == false`, `PopStrikeVRGameScene` is loaded.
5. `PopstrikeLevelDirector` initializes targets upon scene start.
