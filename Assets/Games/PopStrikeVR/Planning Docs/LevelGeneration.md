# Level Generation & Difficulty Scaling Plan

This plan outlines how we will automatically generate 20 professional levels and implement a scalable difficulty system (Easy, Normal, Hard) that dynamically tweaks balloon timing and error tolerance while strictly respecting the patient's calibrated Range of Motion (ROM).

## 1. Difficulty Implementation (Unity Logic)

Currently, the CSV files only dictate the *spatial layout* of the balloons, not how long they stay on screen. To implement Easy, Normal, and Hard modes, we will handle the logic inside `PopstrikeLevelDirector.cs`.

We will add a new `DifficultyLevel` setting with the following tunable limits:
- **Easy Mode:** 
  - `timeBetweenTasks`: 12 seconds
  - `attemptsAllowed`: 3 chances (if they fail 3 times, the balloon deflates and wave ends).
- **Normal Mode:** 
  - `timeBetweenTasks`: 8 seconds
  - `attemptsAllowed`: 2 chances.
- **Hard Mode:** 
  - `timeBetweenTasks`: 5 seconds
  - `attemptsAllowed`: 1 chance (instant fail on mistake).

## 2. Patient Calibration & ROM Safety

Your `WorkspaceMapper.cs` already contains a robust `Mathf.Clamp()` function that forces all spawned balloons into the patient's `MaxAzimuth` and `MaxElevation`. 
Because of this, we can generate "Canonical" CSV levels assuming a maximum healthy wingspan (e.g., -60° to +60° Azimuth). When the patient plays, `WorkspaceMapper` will automatically reel those balloons in so they never spawn outside the patient's safe reach. 

> [!TIP]
> This means we only need to generate **one set** of 20 levels. The game dynamically adapts those exact same levels to fit a small child, an adult, or a patient with restricted shoulder mobility!

## 3. Level Generation Strategy (20 Levels)

I will write a custom **Procedural Generation Script** to mathematically generate the 20 CSV files. This ensures professional, perfectly spaced layouts that avoid overlapping balloons. 

### 3. Level Generation Strategy (Time-Based Loop)

You bring up a brilliant point. Because players have different reaction times (and we pause the timer when they are actively tracing), **we cannot guarantee how many waves a player will complete in 3 minutes.** A fast player might do 40 waves, while a slow player might only do 15.

**The Solution: An Endless Task Pool + Global Level Timer**
Instead of the CSV dictating the length of the level, the CSV will act as a "Pool" of tasks. 
1. We will generate CSV files containing 50 to 100 waves (a massive variety of tasks).
2. The `PopstrikeLevelDirector` will run a **Global Level Timer** (e.g., 3:00 countdown).
3. The Director will pull waves from the CSV one by one. If a player is so fast that they finish all 100 waves, the Director simply loops back to the beginning of the CSV and keeps spawning them.
4. The moment the Global Level Timer hits 0:00, the game instantly stops spawning new balloons and the level ends!

**Why this is perfect for your UI and Difficulty:**
- You can put a strict 3:00 countdown timer on the UI.
- The game will always end at exactly 3:00, no matter what.
- **Difficulty Scaling:** A fast player on "Easy" mode might finish 30 waves in 3 minutes (High Score). A slow player might only finish 12 waves (Lower Score). If we change it to "Hard" mode, the balloons time out faster (e.g., 5 seconds instead of 12 seconds), forcing the player to move faster and potentially fail more often, but the level *still lasts exactly 3 minutes*.

**The 20 Levels:**
We will still generate 20 CSV files, but they will represent **Complexity and Mechanics**, not time duration.
- **Levels 1-5 (Intro):** CSV contains 50 waves of only Orange and Blue balloons. Narrow FOV.
- **Levels 6-10 (Tracing):** CSV contains 50 waves mixing Orange, Blue, and Green balloons.
- **Levels 11-20 (Cognitive):** CSV contains 50 waves mixing all 4 mechanics, heavily featuring TMT logic.

### Formatting Rules (Per the GDD)
The generator will strictly follow your format:
- `O` (Orange Punch): 1 or 2 coordinates.
- `B` (Blue Slash): 3 to 5 coordinates (Generated in linear/curved paths).
- `G` (Green Trace): 2 to 5 coordinates (Continuous paths).
- `A` (TMT-A): 2 to 9 scattered coordinates.
- `T` (TMT-B): Even numbers (2 to 8) scattered coordinates.
- Example Output: `B, (-30,10,0.8);(0,15,0.8);(30,10,0.8)`

## User Review Required

> [!IMPORTANT]
> 1. **Attempts Logic:** Right now, Trace and Trail balloons allow you to fail multiple times (buzzing red) before giving up. Does this sound like a good place to tie the "Number of Attempts" variable for Easy/Normal/Hard?
> 2. **Level Progression:** Are you okay with me writing a Python script right now to instantly generate all 20 of these `.csv` files and place them into your Unity Project folder so you don't have to manually type thousands of coordinates?

Once you approve this plan, I will write the Unity script for the Difficulty Modes, and generate the 20 levels!
