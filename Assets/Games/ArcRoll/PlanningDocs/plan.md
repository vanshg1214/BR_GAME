# ArcRoll - Comprehensive Game Design & Implementation Plan

## 1. Game Overview
ArcRoll is a therapeutic VR game combining Basketball and Bowling mechanics. The primary physical therapy goals are promoting sideways flexion/extension (catching the ball at the outer edges of the patient's Range of Motion) and controlling elbow extension angle and speed (shooting hoops at varying heights).

## 2. Environment & Assets Required
To create a premium, cohesive aesthetic that matches the other games (PopStrike, Whack a Mole) and provides an immersive experience, we need the following assets:

### Environment & Skybox
- **Skybox:** A vibrant, futuristic or sporty skybox (e.g., a stylized sunset or neon cyber-arena) to give a polished, premium feel.
- **Environment Base:** A sleek "Alley" or "Court" floor. It needs enough physical depth to comfortably accommodate the 3x5 Grid in front of the player.
- **Lighting:** Dynamic lighting to highlight the player area, the cannons, and the targets.

### 3D Models & Prefabs
- **Cannons:** 2x futuristic or sporty cannons (Left and Right) that will shoot the balls.
- **Balls:** 
  - 1x Basketball model (requires high bounce physics material).
  - 1x Bowling Ball model (requires heavy rolling physics material).
- **Targets:** 
  - 1x Basketball Hoop (with a dynamic net or particle net).
  - 1x Set of Bowling Pins (Standard 10-pin setup).
- **Obstacles (Nintendo Switch Sports Style):**
  - Sliding Wall/Barrier (Moves horizontally).
  - Swinging Pendulum (Hangs from above).
  - Floor Pop-up Barrier (Rises from the ground).

### Audio (SFX)
- **Cannon Fire:** A satisfying *thump* or *mechanical pop*.
- **Ball Interactions:** Catching the ball (leather glove smack), Basketball bouncing on the court, Bowling ball rolling on wood.
- **Success/Score:** Basketball *swish* (net sound), Bowling pins crashing (strike sound).
- **Atmosphere:** Dynamic Crowd Noise (cheers, applause) that scales with successful hits and combos.

### Visual Effects (VFX)
- **Catch Indicator:** Holographic visual cues showing exactly where the patient's hand should be placed (at the edge of their ROM).
- **Success Particles:** Confetti, spark bursts, or glowing trails when a hoop is scored or pins are knocked down.
- **Trail Effects:** A subtle particle trail behind the balls to emphasize speed and trajectory.

## 3. Scripts Needed
We will organize the codebase into modular scripts. Here is the exact list of scripts required and their responsibilities:

### Core Systems
- `ArcRollGameManager.cs`: Handles game state (Start, Game Over), Score tracking, and Events. *(Already Created)*
- `ArcRollLevelDirector.cs`: Parses the CSV file to spawn waves of targets line-by-line. Manages the global session timer.
- `WorkspaceMapper.cs` (Reuse from PopStrike): Ensures targets and cannons fire within the patient's calibrated safe Range of Motion (ROM).

### Interaction & Physics
- `CannonController.cs`: Handles aiming at the patient's ROM edge, timing the shots, and firing the specific ball type.
- `Ball.cs`: Base class for ball physics and collision detection.
- `BasketballPhysics.cs`: Extends `Ball.cs`. Strictly maps the player's elbow extension velocity to the throw distance.
- `BowlingPhysics.cs`: Extends `Ball.cs`. Handles floor rolling friction and pin impact velocity.
- `PlayerHands.cs`: XR Interaction script tuned to catch the ball magnetically when the hand is in the correct ROM position.

### Grid & Targets
- `GridSpawner.cs`: Maps real-world spatial coordinates to the logical 3x5 grid for spawning hoops and pins.
- `TargetMover.cs`: Handles the Movement Behaviors (Horizontal, Vertical, Wavy) for the hoops.
- `BasketballHoop.cs`: Trigger detection for scoring a swish and triggering VFX.
- `BowlingPin.cs`: Physics collision and scoring for knocking down pins.

### Obstacles
- `ObstacleBase.cs`: Base class for obstacle logic.
- `SlidingBarrier.cs`: Moves left and right on a loop.
- `SwingingPendulum.cs`: Swings on a hinge joint based on gravity.
- `PopupWall.cs`: Rises from the floor on a set interval timer.

## 4. How to Handle the Entire Process Properly

### Phase 1: Physics & Interaction Calibration (The Core)
The most critical part of a VR rehab game is the physical feel. If it feels wrong, it defeats the therapy.
- **Action:** Start by building an isolated test scene with just the Cannon, the Player Hands, and a single Hoop. 
- **Goal:** Perfect the throwing physics so that the *elbow extension speed* accurately translates to throwing power. Ensure catching feels effortless but requires the correct physical reach.

### Phase 2: CSV Data & Grid Integration (The Brain)
Once throwing feels good, we implement the level progression logic.
- **Action:** Build the 3x5 Grid mapping and the `ArcRollLevelDirector.cs`.
- **Goal:** The system should read a CSV line like `Basketball, (1,3), Horizontal, SlidingBarrier` and instantly spawn those objects perfectly aligned in the world. This ensures we can easily generate hundreds of levels later.

### Phase 3: Obstacles & Difficulty (The Challenge)
With the level spawning correctly, we introduce dynamic challenges.
- **Action:** Build the 3 Nintendo Switch Sports obstacles and hook them into the Grid Spawner.
- **Goal:** Ensure they block the player fairly and require timing/cognitive focus, without feeling impossible to beat.

### Phase 4: Polish & "Juice" (The Premium Feel)
The game needs to feel like a high-budget VR title.
- **Action:** Add the Skybox, Crowd SFX, Particle trails, and Confetti. 
- **Goal:** The patient should feel highly rewarded (visually and aurally) every time they make a shot or hit a strike. The environment should feel alive and reactive.
