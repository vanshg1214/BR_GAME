# Popstrike VR - Context & Architecture Transfer Document

## Project Directory Context
The current Unity project contains highly optimized VR systems that can be reused for Popstrike.

**Main Project Root:** `c:\Users\Sujal\UNITY APPS\VR\Whack a Mole Hard\`
**Main Scripts Folder:** `c:\Users\Sujal\UNITY APPS\VR\Whack a Mole Hard\Assets\Script\`

### Key Script Subdirectories Available for Reuse:
- `\Core\` (GameManager, ObjectPooler, State Management)
- `\Data\` (ScriptableObjects for Difficulty Profiles, Game Settings)
- `\GazeInteractor\` (Optimized VR Gaze clicking and UI interaction)
- `\Interactions\` (VR Hand collision, Physics isolation, Haptics)
- `\UI\` (ScoreManager, Timers, 3D Canvas handlers)
- `\VoiceManager\` (Audio and voice feedback systems)

## Highly Optimized Systems Ready for Popstrike
When building Popstrike VR, we must utilize these existing patterns to guarantee high performance and fast development:

### 1. The Coroutine Animation Pattern (Zero-Garbage Movement)
Instead of using expensive physics or Update() loops for UI/Object movements, use the existing AnimatePosition and AnimateScale coroutine patterns found in `BaseMole.cs`.
- **Why it matters:** It provides perfectly smooth, framerate-independent easing (EaseIn/EaseOut) with zero garbage collection overhead.
- **Usage in Popstrike:** Use this for popup targets, menus, and sliding elements.

### 2. The VR Collision Isolator
We have an established `CollisionIsolator.cs` script.
- **Why it matters:** In VR, when objects shatter or explode, their physics colliders can hit the VR Camera Rig, throwing the player across the map and causing extreme motion sickness.
- **Usage in Popstrike:** Any explosive or shatterable target in Popstrike MUST pass its broken pieces through `CollisionIsolator.IsolateRigidbodies()` immediately upon breaking.

### 3. Centralized Object Pooling
We do NOT use `Instantiate()` or `Destroy()` during active gameplay.
- **Why it matters:** Instantiating objects in VR causes micro-stutters and drops frames.
- **Usage in Popstrike:** Popstrike must use the existing ObjectPooler found in the `\Core\` directory. Targets should be spawned using `GetPooledObject()` and deactivated (`gameObject.SetActive(false)`) when hit.

### 4. Hardware-Agnostic Gaze & UI
The `\GazeInteractor\` folder contains a robust `InstantGazeClick.cs` system.
- **Why it matters:** It allows players to navigate menus using pure head-tracking/gaze, ensuring the game is instantly playable even if controllers lose tracking or the user is in a restricted setup.
- **Usage in Popstrike:** All main menu and lobby screens in the Popstrike suite should inherit this gaze-interaction system.

### 5. Centralized Feedback & Haptics
We use a centralized `FeedbackManager` and `ScoreManager`.
- **Why it matters:** Prevents audio overlap clipping and handles controller haptics centrally.
- **Usage in Popstrike:** When a target is hit in Popstrike, do not put audio sources directly on the target. Call `FeedbackManager.Instance.PlayStandardHit()` to handle spatial audio and controller rumble.

---

## AI INSTRUCTION FOR POPSTRIKE DEVELOPMENT
We have exactly 4 days to build this. Prioritize reusing the scripts in the directories listed above. Do not build new pooling, easing, or scoring systems from scratch if they already exist in the `Assets\Script\` folder. Keep all code heavily optimized for Meta Quest standalone hardware.
