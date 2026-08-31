# PopStrike VR: Unimplemented Core Mechanics

Based on a comparison between `PopstrikeVR_GDD_Structured.md` and the current C# codebase, the following core game mechanics are required by the GDD but have not yet been fully implemented.

## 1. Blue Blade Balloon (Slash Line Logic)
**GDD Requirements:**
*   **Continuous Motion:** The player must slash through *all five* balloons in a single continuous motion. Partial chains do not count.
*   **Directional Check:** Incorrect directional slashes should cause balloons to glow red and respawn.
*   **Cascading VFX:** All five should explode in a cascading chain reaction (80ms intervals) producing a lightning streak.

**Current Implementation (`BladeBalloon.cs`):** 
It currently just checks if the hand velocity is > 1.0f and pops the *individual* balloon instantly. It does not enforce a continuous line across 5 balloons, checks no direction, has no respawn logic, and pops instantly instead of using the 80ms cascade.

---

## 2. Green Trace Balloon (Path Validation & Vines)
**GDD Requirements:**
*   **Vine Path Visuals:** Clusters must be connected by a glowing curvilinear or straight vine-like tube indicating direction.
*   **Corridor Validation:** The player must keep their fingertip within the path corridor (e.g., ±4 cm) tracing through each subsequent balloon.
*   **Reset Logic:** Lifting the finger off the path before completing it must reset the entire cluster.

**Current Implementation (`TraceBalloon.cs`):** 
It simply checks if the player touches the balloon with an `INDEX_POINT` gesture and pops it. There are no visual vine connections spawned, no ±4cm corridor tracking, and no lift-off reset logic.

---

## 3. Transparent TMT Balloon (Visuals & Sequencing)
**GDD Requirements:**
*   **Text/Labels:** TMT-A must show ascending numbers (1, 2, 3...). TMT-B must show alternating alphanumeric characters (1, A, 2, B...).
*   **Glowing Lines:** Correct touches must draw glowing lines between the balloons to show the completed sequence.

**Current Implementation (`TMTSolverScript.cs` & `TrailBalloon.cs`):** 
The solver successfully tracks an array sequence of hits, but the game does not dynamically assign the 1,A,2,B text to the balloons upon spawning, nor does it draw any glowing connection lines between them during gameplay.

---

## 4. Range of Motion (ROM) Calibration
**GDD Requirements:**
*   **Calibration Phase:** The system must run a calibration routine that samples max reach points across a 180° front hemisphere and maps it to a 100x50 grid (Azimuth x Elevation).

**Current Implementation:** 
The `WorkspaceMapper.cs` and `PatientProfileSO` exist and successfully use a static radius/scale to position objects safely, but there is no active runtime calibration mini-game/sequence to actually build this map based on the specific player's real-time reach.

---

## 5. Screen Post-Processing Effects
**GDD Requirements:**
*   Hit confirmation (1-frame full-screen flash).
*   Combo milestone (edge pulse gold).
*   Miss/Timeout (desaturation pulse).
*   TMT Error (red vignette).

**Current Implementation (`PopstrikeFeedbackManager.cs`):** 
Audio and particle VFX are working, but the screen-space effects (flashes, desaturation, vignettes) are missing.

---

## 6. End Session Data Export & Trigger
**GDD Requirements:**
*   **Results Trigger:** The game loop must transition to the Session Review UI.
*   **JSON Export:** Data exported to a JSON log for Therapist Dashboard Sync.

**Current Implementation:** 
The `SessionReviewUI.cs` exists, but the game loop in `PopstrikeLevelDirector.cs` simply ends with a `// TODO: Trigger Session Review UI` comment. The JSON exporter for therapist data is also absent.
