# V4 Update: Implementation Plan

Below is the detailed breakdown of each required change, how to implement it, and the estimated time to complete. 

---

## 1. Grounding Environment (Safety & Immersion)
**Requirement:** Add a chair, table, and plants beneath/near the patient so they feel safe and grounded in the VR space.
**How to Implement:**
1. Create a new Unity Prefab (e.g., `GroundingProps_Prefab`) containing 3D models of a chair, table, and a few potted plants. 
2. Modify `PopstrikeLevelDirector.cs`. When the level starts, find the player's starting position (`CenterEyeAnchor.position`). 
3. Instantiate the `GroundingProps_Prefab` directly below the player (locking the Y-axis to floor level). Position the chair immediately behind the player's spawn point and the table/plants slightly to the side.
**Estimated Time:** 30 minutes (finding suitable low-poly assets and scripting the dynamic spawning).

---

## 2. Morning Skybox
**Requirement:** Replace or toggle the current night skybox with a bright morning skybox.
**How to Implement:**
1. Import a bright, morning Equirectangular HDRI / Skybox material.
2. In `PopstrikeLevelDirector.cs`, expose a `Material morningSkybox` variable.
3. Depending on the level's configuration (or a random toggle), swap it at runtime: `RenderSettings.skybox = morningSkybox;`.
4. Our existing `SkyboxAligner.cs` script will automatically handle rotating the morning sun to face the player, so no additional offset math is needed.
**Estimated Time:** 15 minutes.

---

## 3. Persistent Hand Gesture Lock
**Requirement:** Lock the required hand gesture for the entire level to assist patients with spasticity/pain who cannot maintain a gesture.
**How to Implement:**
1. In `GestureDetector.cs`, we already have a `LockGesture(bool isLeft, GestureState gesture)` function.
2. Modify `PopstrikeLevelDirector.cs` so that when a task begins (e.g., `Blue_Slash`), it immediately figures out the required gesture (`OPEN_BLADE`).
3. Call `GestureDetector.Instance.LockGesture(true, OPEN_BLADE)` and `LockGesture(false, OPEN_BLADE)` instantly. 
4. This will force the game to constantly read the patient's hand as the correct gesture for the duration of that task, regardless of how their fingers physically curl.
**Estimated Time:** 30 minutes.

---

## 4. Disable Gesture Requirement Toggle
**Requirement:** A setting to completely turn off gesture requirements for extremely weak patients (0-4 months post-stroke) who have no finger movement.
**How to Implement:**
1. Add a new toggle setting in the Main Menu UI: `Disable Gesture Requirement`.
2. Save this state in `TemporarySessionData.DisableGestures`.
3. Modify the collision scripts (`BladeBalloon.cs`, `BlazeBalloon.cs`, `TraceBalloon.cs`).
4. Wrap the gesture check in an `if`: 
   ```csharp
   if (!TemporarySessionData.DisableGestures) 
   { 
       // Perform normal gesture validation 
   } 
   ```
5. If disabled, any physical contact with the balloon (even with a resting hand or controller) will count as a valid interaction as long as the speed/position is correct.
**Estimated Time:** 45 minutes (Wiring the UI toggle and updating the validation logic in all 4 balloon types).

---

## 5. [x] FIXED: Level Complete Menu Repositioning & Resizing
**Requirement:** Make the exit menu appear directly in front of the player's face, and significantly enlarge the "Next Level" and "Menu" icons for easy head-aiming.
**How to Implement:**
1. Open `LevelResultsUI.cs` and locate the `ShowResults()` method.
2. When the menu activates, dynamically grab the `CenterEyeAnchor` transform.
3. Teleport the UI Canvas directly in front of the player's gaze: 
   `transform.position = centerEye.position + centerEye.forward * 1.2f;`
   `transform.rotation = Quaternion.LookRotation(centerEye.forward);`
4. In the Unity Editor, open the `LevelCompleteMenu` prefab. Modify the `RectTransform` scales of the "Next Level" and "Main Menu" buttons, multiplying their size by `1.5x` or `2.0x`.
5. Update their BoxColliders/Interactables so the head-gaze reticle easily snaps to them.
**Estimated Time:** 30 minutes.

---

**Total Estimated Implementation Time:** ~2.5 hours.
