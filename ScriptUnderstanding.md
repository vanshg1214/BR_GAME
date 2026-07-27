# PopStrike VR — Technical Script Reference

Documentation for software engineers and Unity developers maintaining or extending **PopStrike VR**.

---

## 1. System Architecture & Data Flow

PopStrike VR is a gesture-driven physical and cognitive rehabilitation application built using Meta XR / OpenXR in Unity.

```
+--------------------------+
|   PopstrikeMenuManager   |
| (Selects Hand/Diff/Time) |
+--------------------------+
             │
             ▼
+--------------------------+
|   TemporarySessionData   |  <--- (Static Runtime Data Carrier)
+--------------------------+
             │
             ▼
+--------------------------+
|  PopstrikeLevelDirector  |
| (Loads Stage & Targets)  |
+--------------------------+
             │
   ┌─────────┴─────────┬──────────────────┐
   ▼                   ▼                  ▼
[ TrailBalloon ]  [ TraceBalloon ]  [ Blaze / Blade ]
 (TMT Solver)     (Path Manager)    (Punch / Slash)
   │                   │                  │
   └─────────┬─────────┴──────────────────┘
             ▼
+--------------------------+
| PopstrikeFeedbackManager |
| (SFX, Voiceovers, VFX)   |
+--------------------------+
             │
             ▼
+--------------------------+
|     LevelResultsUI       | ---> [ SessionLogger ] (JSON Telemetry Export)
+--------------------------+
```

---

## 2. Core Game Directors & Data Management

### `PopstrikeLevelDirector.cs`

- **Location:** `Assets/Games/PopStrikeVR/Scripts/Core/PopstrikeLevelDirector.cs`
- **Attached To:** `PopStrike_Manager` GameObject in gameplay scenes.
- **What It Does:** Central game loop director. Controls stage progression across Easy, Medium, and Hard difficulties, instantiates target sequences, monitors target completions, and handles round end transitions.
- **Error Cooldown Feature:** Implements `TryReportError()`. When a patient strikes an incorrect target, it triggers an error sound and red screen vignette, followed by an **0.8-second cooldown**. This prevents multiple accidental penalty triggers when a patient's hand lingers near an incorrect target.
- **How to Modify:**
  - **Tuning Error Cooldown:** Adjust `errorCooldownDuration` in the Inspector (default: `0.8s`).
  - **Custom Level Sequences:** Modify `TemporarySessionData.GenerateLevelSequence()` or assign custom `SessionConfigSO` assets.

---

### `PopstrikeMenuManager.cs`

- **Location:** `Assets/Games/PopStrikeVR/Scripts/UI/PopstrikeMenuManager.cs`
- **Attached To:** `Menu_Canvas` GameObject in `PoPStrikeVRMenu.unity`.
- **What It Does:** Manages the main menu UI. Handles pill-button selections for Hand Mode (Left, Both, Right), Difficulty (Easy, Medium, Hard), Session Duration (3 Min, 5 Min), Gesture Mode (ON/OFF), and Environment (Morning/Night).
- **Default Environment Feature:** Contains `defaultToNightScene` (boolean, default: `true`).
- **How to Modify:**
  - **Button Highlighting:** Modify `selectedColor` (glowing yellow) and `normalColor` (white) in the Inspector.

---

### `TemporarySessionData.cs`

- **Location:** `Assets/Games/PopStrikeVR/Scripts/Core/TemporarySessionData.cs`
- **What It Does:** Static in-memory state carrier that holds active session parameters across scene loads.
- **Key Properties:**
  - `HandMode`: `LeftHandOnly`, `BothHands`, `RightHandOnly`
  - `Difficulty`: `"Easy"`, `"Medium"`, `"Hard"`
  - `Duration`: `ThreeMinutes`, `FiveMinutes`
  - `DisableGestures`: `bool` (when `true`, posture and speed requirements are bypassed for accessibility)
  - `IsMorningScene`: `bool` (determines whether `PopStrikeVRMorn_Scene` or `PopStrikeVRGameScene` is loaded)

---

### `SessionLogger.cs`

- **Location:** `Assets/Games/PopStrikeVR/Scripts/Data/SessionLogger.cs`
- **What It Does:** Exports clinical telemetry into JSON format stored at `Application.persistentDataPath/PopstrikeLogs/Session_TIMESTAMP.json`.

---

## 3. Hand Tracking & Gesture Subsystem

### `GestureDetector.cs`

- **Location:** `Assets/Games/PopStrikeVR/Scripts/Interaction/GestureDetector.cs`
- **What It Does:** Evaluates 3D joint distance ratios from Meta XR `OVRHand` / `OVRSkeleton` data to classify player hand postures:
  - `INDEX_POINT`: Extended index finger for pointing and tracing.
  - `CLOSED_FIST`: Closed fist posture for red Blaze punch targets.
  - `OPEN_BLADE`: Open flat hand for blue Blade slash targets.
- **Gesture Locking Feature:** Exposes `LockGesture()`, which locks hand state to `INDEX_POINT` during active TMT sequences. This prevents tracking drops from interrupting active links.

---

### `MetaHandIntegrator.cs` & `HandColliderForwarder.cs`

- **Location:** `Assets/Games/PopStrikeVR/Scripts/Interaction/MetaHandIntegrator.cs`
- **Attached To:** `LeftOVRHandPrefab` and `RightOVRHandPrefab`.
- **What It Does:** Dynamically attaches 3 kinematic sphere trigger hitboxes to specific hand bones once `OVRSkeleton` initializes:
  - `Hitbox_Hand_Index3` (Index tip, 0.04m radius) — Pointing & TMT.
  - `Hitbox_Hand_Middle1` (Knuckles, 0.06m radius) — Fist punches.
  - `Hitbox_Hand_Pinky1` (Hand edge, 0.05m radius) — Blade slashes.
- **Velocity Tracking:** Calculates 3D hand speed in `FixedUpdate()` for impact validation.

---

### `GestureTrailManager.cs`

- **Location:** `Assets/Games/PopStrikeVR/Scripts/Gameplay/GestureTrailManager.cs`
- **What It Does:** Manages visual hand trails (`IndexTrail`, `BladeTrail`, `FistTrail`) and rim-glow materials. Activates trails only when hand velocity exceeds `minTrailVelocity` (0.15 m/s) and matches the required posture.

---

## 4. Balloon Target Entities & Accessibility

All targets derive from **`BaseBalloon.cs`**, which provides distance-based touch checks (`IsPhysicallyTouching`) and shrink-dissolve pop routines (`DissolvePopRoutine`).

| Script Name | File Location | Required Action | Touch Forgiveness Margin |
|---|---|---|---|
| **`TrailBalloon.cs`** | `Gameplay/Balloons/TrailBalloon.cs` | Point / Fist (if Gestures OFF) | `0.10m` (Default) / `0.22m` (Gestures OFF) |
| **`TraceBalloon.cs`** | `Gameplay/Balloons/TraceBalloon.cs` | Point / Trace | `0.15m` (Default) / `0.22m` (Gestures OFF) |
| **`BlazeBalloon.cs`** | `Gameplay/Balloons/BlazeBalloon.cs` | Fist Punch ($\ge 1.2	ext{ m/s}$) | Standard (Velocity bypassed if Gestures OFF) |
| **`BladeBalloon.cs`** | `Gameplay/Balloons/BladeBalloon.cs` | Blade Slash ($\ge 1.0	ext{ m/s}$) | Standard (Velocity bypassed if Gestures OFF) |

### Accessibility Rules ("Gestures: OFF" Mode)

When **Gestures: OFF** is selected in the menu (`TemporarySessionData.DisableGestures == true`):
1. **Fist Touch Allowed:** `TrailBalloon` and `TraceBalloon` accept hits from any hand hitbox (`Hitbox_`) rather than restricting to the index tip.
2. **Gesture Validation Bypassed:** Automatically forces internal state to `INDEX_POINT` so fist punches trigger number balloons instantly.
3. **Relaxed Forgiveness Distance:** Expands physical touch tolerance to **22cm** (`0.22m`), allowing patients with limited range of motion to pop targets easily.
4. **Velocity Checks Removed:** Punch and slash speed thresholds on `BlazeBalloon` and `BladeBalloon` are set to zero.

---

## 5. Puzzle Solvers & Path Tracking

### `TMTSolverScript.cs`

- **Location:** `Assets/Games/PopStrikeVR/Scripts/Gameplay/TMTSolverScript.cs`
- **What It Does:** Manages Trail Making Test logic:
  - **TMT-A:** Numeric sequence (`1 -> 2 -> 3 -> 4...`).
  - **TMT-B:** Alternating sequence (`1 -> A -> 2 -> B -> 3 -> C...`).
- **Features:** Draws dynamic 3D connecting lines via `LineRenderer`, plays ascending pitch audio chords on consecutive hits, and manages link timeout timers.

---

### `TracePathManager.cs`

- **Location:** `Assets/Games/PopStrikeVR/Scripts/Gameplay/TracePathManager.cs`
- **What It Does:** Controls green trace path corridors. Verifies that the player's index finger remains within the 4cm corridor tolerance, updating path progress until complete.

---

## 6. Feedback, Audio & User Interface

### `PopstrikeFeedbackManager.cs`

- **Location:** `Assets/Games/PopStrikeVR/Scripts/Core/PopstrikeFeedbackManager.cs`
- **What It Does:** Central audio and visual feedback system.
- **Combo Milestones:** Plays supportive announcer voiceover clips as combos build:
  - **5 Combo:** `"Keep Going!"` (`KeepGoingClip`)
  - **10 Combo:** `"Doing Great!"` (`DoingGreatClip`)
  - **15 Combo:** `"Unstoppable!"` (`UnstoppableClip`)
  - **20 Combo:** `"Flawless!"` (`FlawlessClip`)

---

### `PopstrikeHUDController.cs` & `LevelResultsUI.cs`

- **`PopstrikeHUDController`:** Displays live timer, score, combo counter, and stage progress bar.
- **`LevelResultsUI`:** Displays post-round summary (Accuracy %, Total Score, Star Rating, Next Stage button).

---

## 7. Developer Customization Cheat Sheet

| Feature to Modify | Target File | Recommended Action |
|---|---|---|
| **Combo Voiceovers / Text** | `PopstrikeFeedbackManager.cs` / `ComboManager.cs` | Swap audio clips in Inspector or edit string identifiers. |
| **Balloon Touch Forgiveness** | `TraceBalloon.cs` / `TrailBalloon.cs` | Adjust `forgiveness` float values (default `0.15m` / `0.22m`). |
| **Error Cooldown Delay** | `PopstrikeLevelDirector.cs` | Change `errorCooldownDuration` (default `0.8s`). |
| **Default Environment (Night vs Morning)** | `PopstrikeMenuManager.cs` | Toggle `defaultToNightScene` checkbox on `PopstrikeMenuManager`. |
| **Hand Trail Minimum Speed** | `GestureTrailManager.cs` | Adjust `minTrailVelocity` (default `0.15m/s`). |
