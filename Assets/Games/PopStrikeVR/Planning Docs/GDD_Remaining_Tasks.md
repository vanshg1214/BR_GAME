# PopStrike VR — Remaining GDD Implementation Tasks

Based on the Game Design Document and the current state of the codebase, here are the outstanding features that still need to be built to complete the gamification, UI, and VFX experience. 

*(Note: "Session Review" has been excluded from this list per your request).*

## 1. Gamification & Score UI
- [ ] **Combo Meter UI:** Build a circular arc UI element anchored at the bottom center of the player's visual field that fills as correct balloons are popped.
- [ ] **World-Space Multiplier UI:** Display the current combo multiplier (×1, ×1.5, ×2, ×3) in large, bold, world-space text near the patient's eye line. It needs a "pop-scale" animation each time the multiplier increments.
- [ ] **Scoring Logic:** Implement a total score tracker that calculates points per hit multiplied by the current `ComboManager` multiplier.
- [ ] **Voice Lines (Optional):** Add a system to play recorded voice lines ("Perfect!", "On fire!", "Incredible chain!") triggered during combo milestones.

## 2. Streak Bonuses (Visual Feedback)
While the `ComboManager` currently broadcasts "Hot Streak" and "Unstoppable", the visual effects described in the GDD are missing:
- [ ] **Hot Streak (5 Combo):** Make all currently active balloons in the scene glow gold.
- [ ] **Unstoppable (10 Combo):** Trigger a background pulse effect, expand and brighten the hand trails, and fire a particle burst from both hands.
- [ ] **Perfect Round:** Add logic to track if a task was completed flawlessly. If true, the balloons in the *next* task must spawn with a "star particle halo" visual.

## 3. Persistent Hand Trails & Gesture Visuals
- [ ] **Dynamic Hand Trails:** Implement a `TrailRenderer` (ribbon mesh) on both palms (8cm wide tapering to 0) and secondary trails on the index and middle fingertips.
- [ ] **Trail Color Logic:** Bind the trail color to the current gesture state: 
  - White at rest
  - Gold during Orange (Fist)
  - Electric Blue during Slash (Blade)
  - Green during Trace (Index Point)
  - Silver during TMT
- [ ] **Velocity-based Glow:** Scale the emission intensity of the hand trail based on the speed of the player's hand movement.
- [ ] **Gesture State Indicators:** Show colored, transparent VFX overlays directly over the player's hand models to indicate their current gesture state (Orange Fist, Blue Blade, Green Index).

## 4. Screen Effects (ScreenEffectsController)
Some screen effects are partially implemented, but the following are missing:
- [ ] **Hit Confirmation:** A brief 1-frame full-screen flash, tinted to match the color of the balloon just popped.
- [ ] **Combo Milestone Pulse:** A screen-edge glow/pulse in gold when a combo multiplier increases.
- [ ] **Miss/Timeout Effect:** A subtle desaturation pulse that lasts 0.5 seconds when the combo breaks due to a timeout.

## 5. Global Visual Atmosphere
- [ ] **Dynamic Environment Hue:** Build a system that subtly shifts the background space/nebula color based on the currently active balloon type (Amber for Orange, Cool Blue for Blade, Forest Green for Trace, Silver-White for TMT).

## 6. Adaptive Audio System
- [ ] **Adaptive Music Layers:** Implement a background music manager with a base 90 BPM rhythm track.
- [ ] **Multiplier Intensity:** Crossfade or add musical stems/layers to the music as the combo multiplier increases.
- [ ] **TMT Cognitive Drop:** Automatically drop the background music down to a minimal atmospheric layer when a TMT-A or TMT-B sequence spawns to reduce cognitive load.
