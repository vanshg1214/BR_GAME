# V2 Changes & Feedback Analysis

**From: Senior VR Rehab Developer**
**To: Development Team**

This document breaks down the recent feedback from Prashanth. From a VR rehabilitation perspective, our primary goal is to ensure the game is accessible, motivating, and therapeutically beneficial. Frustration (like impossible obstacles or difficult grabbing) is detrimental to rehab, so we will focus heavily on "quality of life" and accessibility improvements.

Here is the breakdown of each point, how we will fix it technically, time estimates, and priority levels.

---

### 0) Canon Positioning, Shoot Feedback, & Streak Bug
* **Feedback:** Place canons in forward FOV. Add air whoosh/sound effect when shooting. Streak doesn’t go above 2.
* **Developer View:** In rehab, patients often have limited neck/torso mobility. Crucial gameplay elements MUST be within the primary 60-degree forward FOV. Missing audio feedback on shooting makes the game feel unresponsive. The streak issue is a critical progression blocker.
* **Technical Fix:**
    * **Canons:** Adjust the spawn positions/rotations of the left and right canons in the scene so they sit comfortably in the player's direct sightline.
    * **Effects:** Add an `AudioSource` to the canons with a "whoosh" sound and trigger a simple wind particle effect upon the `Fire()` method.
    * **Streak Bug:** Debug `ArcRollScoreManager.cs`. It's likely that a hit is triggering `ResetStreak()` inadvertently or the combo math is resetting due to a missing combo timer.
* **Time Estimate:** 1.5 - 2 Hours
* **Priority:** **HIGH** (Accessibility & Core Game Loop)

### 1) Bowling Triangle Obstacle & Variety
* **Feedback:** Triangle rotating obstacle is impossible to go over. Need more variety in bowling.
* **Developer View:** Rehab games need "tunable success rates" (usually around 70-80% success to maintain motivation). An impossible obstacle will demoralize a patient.
* **Technical Fix:**
    * **Obstacle:** Modify the Triangle obstacle. We can either shrink its scale, slow down its rotation, or change the pivot point so there is always a clear, predictable gap for the patient to roll through. 
    * **Variety:** Create 2-3 new obstacle variants (e.g., a simple moving wall, speed-boost pads, or curved ramps). We can use an array of prefabs in the Level Director to spawn them randomly.
* **Time Estimate:** 3 Hours
* **Priority:** **HIGH** (Patient Motivation)

### 2) Basketball Difficulty, Fake Goals, & Hoop Timer
* **Feedback:** Basketball is too hard. Fake goals register. Needs a stationary countdown timer above the hoop.
* **Developer View:** Throwing in VR is notoriously difficult due to lack of weight and release-point feedback. We need heavy aim-assist for rehab patients. Fake goals break trust in the system.
* **Technical Fix:**
    * **Snapping/Assist:** Increase the `Magnetic Pull` radius/strength on the hoop, or implement a trajectory prediction that softly nudges the ball towards the center if the throw is *close enough*.
    * **Fake Goals:** Update the `BasketballHoop` trigger logic. We need to check the ball's Y-velocity (making sure it is falling *downwards* through the hoop) and ensure it enters the top collider, not just grazing the bottom or sides.
    * **Timer:** Add a World Space Canvas as a child to the Hoop prefab. Link a TextMeshPro element to the hoop's lifetime logic to display the remaining seconds (`Mathf.CeilToInt(timeLeft)`).
* **Time Estimate:** 4 - 5 Hours
* **Priority:** **HIGH** (Core Game Loop & Trust)

### 3) Grabbing Mechanics (Snap to Palm)
* **Feedback:** Snap basketball/bowling ball to the front of the palm, not where grabbed.
* **Developer View:** This is a classic VR UX issue. Grabbing a ball off-center makes the throwing arc unpredictable. For therapeutic exercises, the center of mass must align with the hand to ensure the patient's elbow/wrist movements translate accurately.
* **Technical Fix:** 
    * In the XR Grab Interactable component on the ball prefabs, enable **Attach Transform**. 
    * Create an empty GameObject child on the ball, position it exactly at the edge/center where the palm should rest, and assign it to the Attach Transform slot.
* **Time Estimate:** 1 Hour
* **Priority:** **MEDIUM-HIGH** (Ergonomics & Physics)

### 4) New Frisbee Game (Cans Tower)
* **Feedback:** Add a frisbee game to knock down a tower of cans. Patient uses elbow forward/backward movement.
* **Developer View:** Excellent idea. Bowling emphasizes shoulder flexion/extension, Basketball emphasizes upward launch. Frisbee will specifically target horizontal elbow extension and wrist flicking. This perfectly rounds out the physical therapy motions.
* **Technical Fix:**
    * **Frisbee Physics:** Create a Frisbee prefab with an aerodynamic script (applying upward lift based on forward velocity to make it glide).
    * **Targets:** Create a stackable "Can" prefab with rigidbodies and metal collision sounds.
    * **Game Manager:** Create a new `FrisbeeLevelDirector` that spawns the can towers at varying distances.
* **Time Estimate:** 8 - 10 Hours
* **Priority:** **MEDIUM** (New Feature - Scope Expansion)

### 5) Environmental Polish (Audience/Scenery)
* **Feedback:** Add an audience or scenic elements to the environment.
* **Developer View:** A sterile environment feels clinical. Rehab patients perform better when they feel like they are in a fun, lively space (gamification).
* **Technical Fix:**
    * Add a stadium or cheering crowd audio track that swells when streaks are achieved.
    * Drop in some low-poly animated audience assets or change the skybox/lighting to a sunset/arcade vibe to make the space feel warmer.
* **Time Estimate:** 2 Hours
* **Priority:** **LOW** (Polish - Do this last)

---
*Please review these assessments. Once approved, we can begin implementing these fixes starting with the High Priority items.*
