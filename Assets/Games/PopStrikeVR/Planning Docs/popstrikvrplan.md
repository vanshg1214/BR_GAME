# Popstrike VR - Detailed 4-Day Execution Plan

## OVERVIEW
This document outlines the professional, optimized 4-day execution plan for developing the Popstrike VR Rehabilitation game for Meta Quest 3/3S. The architecture will heavily reuse existing, high-performance systems from the previous VR project (e.g., zero-garbage Coroutine Animations, CollisionIsolator, ObjectPooler, and FeedbackManager). All code will be written cleanly with standard namespaces (`PopstrikeVR.*`).

---

## DAY 1: CORE SYSTEMS, NAMESPACES & INFRASTRUCTURE
**Focus:** Establishing the foundational data models, spatial mappings, and CSV parsing infrastructure.

1. **Project & Namespace Initialization**
   - Create folder structure: `Assets/Script/Popstrike/Core`, `.../Data`, `.../Gameplay`, `.../Visuals`.
   - Setup standard namespaces: `PopstrikeVR.Core`, `PopstrikeVR.Data`, `PopstrikeVR.Gameplay`.

2. **Data Models & Scriptable Objects**
   - Create `PatientProfileSO` to store gesture confidence thresholds and normalized ROM (Range of Motion) data.
   - Create `SessionConfigSO` to define global rules (timeouts, multiplier intervals).

3. **CSV Level Parser & Validation**
   - Build `CSVLevelParser.cs` to read session files row by row.
   - Implement robust coordinate parsing: `(Azimuth, Elevation, Radius)` to Vector3.
   - Implement validation logic for all task types:
     - **Orange (O):** Check for at least 1 coordinate.
     - **Blue (B):** Check for exactly 5 collinear coordinates.
     - **Green (G):** Check for 2 to 5 coordinates forming a trace path.
     - **TMTA/TMTB:** Check coordinate counts and enforce even/odd rules.

4. **Architecture Integration (Reuse)**
   - Refactor `ObjectPooler.cs` to handle Balloon spawning safely without Instantiate/Destroy overhead.
   - Integrate `FeedbackManager.cs` to provide a central hub for all spatial audio and haptics.

---

## DAY 2: GAMEPLAY LOGIC, BALLOONS & INTERACTION
**Focus:** Implementing the core VR mechanics, gesture detection, and object behaviors.

1. **Balloon OOP Hierarchy**
   - Create abstract `BaseBalloon.cs` incorporating zero-garbage `AnimatePosition/Scale` coroutines.
   - Implement subclasses: `BlazeBalloon.cs` (Orange), `BladeBalloon.cs` (Blue), `TraceBalloon.cs` (Green), and `TrailBalloon.cs` (Transparent).

2. **Hand Gesture Detection System**
   - Implement `GestureDetector.cs` reading from the hand-tracking API.
   - Define and broadcast the three core states: `CLOSED_FIST`, `OPEN_BLADE`, `INDEX_POINT`.
   - Incorporate velocity thresholds (crucial for Blaze/Blue balloon popping mechanics).

3. **Spatial Mapping & Safety Zones**
   - Develop `WorkspaceMapper.cs` to map the normalized 100x50 ROM grid to world space around the player's head.
   - Apply the 0.85x safety margin scalar to prevent hard-limit strain on the patient.

4. **Hit Detection & Isolation**
   - Implement rigid collision checks utilizing the `CollisionIsolator` to prevent VR Rig physics blowouts when balloons shatter.

---

## DAY 3: GAME FEEL, VFX, AUDIO & COMBO SYSTEM
**Focus:** Adding juice, visual feedback, and patient motivation elements.

1. **Hand Trail System**
   - Build `HandTrailManager.cs` utilizing a persistent `TrailRenderer`.
   - Implement dynamic color shifting based on gesture state:
     - Rest: White
     - Fist: Gold
     - Blade: Electric Blue
     - Point: Green / Silver (TMT)

2. **VFX Implementation**
   - Create specialized pop effects:
     - **Blaze:** Orange-gold confetti and flame shockwave.
     - **Blade:** Cascading chain lightning sparks.
     - **Trace:** Green leaf particles and golden path resolving.

3. **Audio Setup**
   - Route specific SFX logic (bass thud vs. electric hum vs. piano chimes) through `FeedbackManager`.
   - Integrate adaptive background music layered on the combo state.

4. **Combo & Streak System**
   - Build `ComboManager.cs` to track multipliers (x1.5, x2, x3).
   - Implement specific streak events (Hot Streak at 5, Unstoppable at 10, Perfect Round).

---

## DAY 4: TMT SYSTEMS, UI, LOGGING & POLISH
**Focus:** Finalizing clinical testing systems, data logging, and performance reviews.

1. **TMT (Trail Making Test) Logic**
   - Implement `TMTSolver.cs` to govern the sequential logic of TMT-A (1-N) and TMT-B (1, A, 2, B...).
   - Add the visual glowing line connections between successfully touched transparent balloons.

2. **UI & Session Review**
   - Build a 3D Canvas Results Screen displaying: Accuracy, Reaction Time, ROM used (%), and Star Ratings.
   - Utilize `InstantGazeClick` for gaze-based UI interaction.

3. **Data Logging**
   - Implement `SessionLogger.cs` using `JsonUtility` to serialize end-of-session data.
   - Ensure secure local storage formatting compatible with the therapist dashboard.

4. **Optimization & Performance Audit**
   - Run a deep profiler pass to guarantee zero Garbage Collection spikes during gameplay.
   - Validate 90/120 FPS stability on standalone Quest 3 hardware.
