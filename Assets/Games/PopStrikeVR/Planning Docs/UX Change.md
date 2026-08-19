# UX Feedback Implementation: Gesture Guidance & Floating Indicators

The goal of this plan is to implement two key UX improvements based on stakeholder feedback to make the game more intuitive for elderly patients and less visually aggressive.

## Proposed Changes

### Component 1: Floating Hit Indicators (Replacing Screen Flash)
We will replace the harsh full-screen colored flashes with localized floating "Tick" (Correct) and "Cross" (Wrong) icons that spawn exactly where the interaction occurred.

#### [NEW] `Scripts/UI/HitIndicatorManager.cs`
- **Design:** A Singleton class that manages a pool of floating indicators.
- **Methods:** 
  - `public void ShowTick(Vector3 position)`
  - `public void ShowWrong(Vector3 position)`
- **Animation:** When called, it spawns a world-space UI Sprite (or Canvas) at the given position. The sprite will quickly pop-scale up, float slightly upwards, and fade out smoothly over 1-1.5 seconds.
- **Assets Needed:** Clean, commercial-free Google Icons for a Checkmark (Green) and an X (Red).

#### [MODIFY] `Scripts/Gameplay/PopstrikeFeedbackManager.cs` & `ComboManager.cs`
- **Change:** Remove or disable the full-screen flash logic that was previously tied to hit confirmation or misses.
- **Change:** Update the failure logic (like missing a trace or timing out) to call `HitIndicatorManager.Instance.ShowWrong(position)` instead of a full-screen red flash or desaturation.

#### [MODIFY] `Scripts/Gameplay/Balloons/BaseBalloon.cs` (and subclasses)
- **Change:** When a balloon is successfully popped, pass its position to `HitIndicatorManager.Instance.ShowTick(transform.position)`.
- **Change:** When a trace is failed, pass the hand position to `ShowWrong`.

---

### Component 2: Gesture Guidance Overlays (2D Hand Icons)
To help patients understand what gesture is required without guessing, we will display a 2D floating icon directly above each balloon.

#### [NEW] `Scripts/UI/BalloonGestureIndicator.cs`
- **Design:** A script attached to a World Space Canvas that sits slightly above each balloon (or as a child of the balloon prefab).
- **Functionality:** 
  - On spawn, it assigns the correct 2D hand icon based on the balloon type:
    - **Orange (Punch):** Closed Fist icon.
    - **Blue (Slash):** Flat Hand (Blade) icon.
    - **Green/Transparent (Trace):** Index Finger icon with a "Path/Arrow" visual.
  - It will constantly use `transform.LookAt` (locked on Y-axis) to face the player's camera so it's always readable.
  - **UX Polish (Proximity Fade):** As the player's physical hand gets within ~30cm of the balloon, the icon will smoothly fade out so it doesn't clutter their vision right at the moment of impact.

#### [MODIFY] `Scripts/Gameplay/Balloons/BaseBalloon.cs`
- **Change:** Instantiate or enable the `BalloonGestureIndicator` on spawn and link it to the balloon's specific gesture requirement.

## Open Questions
> [!IMPORTANT]
> - Do you want the Gesture Icons to be purely 2D UI Sprites, or would you prefer 3D hand models? (2D UI Sprites like Google Icons are usually cleaner and easier for elderly patients to read, as requested).
> - For the Trace path (Vine/TMT), should the "Arrow" next to the index finger point generally towards the next balloon dynamically, or should it just be a static generic "draw a line" symbol?

## Verification Plan
### Manual Verification
- Play the game and spawn each of the 4 balloon types. Verify the correct 2D hand gesture icon floats above them.
- Move the headset left and right to ensure the icons perfectly rotate to face the camera.
- Reach for a balloon and ensure the icon smoothly fades out before impact.
- Intentionally hit a balloon with the wrong gesture (or miss a trace) to verify the floating Red Cross spawns at the impact point, animates, and fades out.
- Hit a balloon correctly to verify the Green Tick spawns.
