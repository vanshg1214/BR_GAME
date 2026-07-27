# CSV Architecture Change Plan: Whack-a-Mole VR

This document outlines the architectural changes required to introduce a CSV-based, data-driven level generation system to the Whack-a-Mole VR physiotherapy game. 

The primary goal of this feature is to allow therapists (and eventually AI) to generate specific, tailored routines for individual patients using a "physio grid input" system, which can be fetched from the cloud without updating the application.

> [!IMPORTANT]
> **Safety & Visuals Guarantee:** The proposed system ensures that the physical table, holes, and visual spawning animations will remain exactly the same. Targets will never spawn outside the patient's safe Range of Motion (ROM).

---

## 1. The Current Architecture (How it works now)

The current system relies on two main components to adapt to a patient's physical limitations and spawn targets:

### A. Dynamic Hole Generation (`HoleLayoutGenerator.cs`)
The game does not use fixed, pre-placed holes. It dynamically generates a custom grid at runtime based on the patient's `RehabProfile` (specifically `armLength` and `maxHorizontalROM`).
- It calculates an arc of playable space.
- For every calculated position, it spawns a visual hole mesh and an invisible `ProxyFlatSpawn` transform.
- All these proxy transforms are saved into a master list called `spawnPoints`.

### B. Infinite Random Spawning (`MoleSpawner.cs`)
The game runs a continuous infinite loop (`SpawnLoop`) constrained by intervals from the `DifficultyProfile`.
- When the timer triggers, it runs a mathematical function (`GetFurthestAvailableHole()`) to pick an empty physical hole that is far from the player's face and far from other active moles.
- It rolls a random probability to decide which type of animal to spawn (Squirrel, Dog, Fake, Bird, Cage Hamster).
- It instantiates the chosen animal at the selected hole.

---

## 2. The Proposed Architecture (How it will work with CSV)

To support specific routines (e.g., PopStrike's `Azimuth, Elevation, Distance`), we will introduce a **"Nearest Neighbor Mapping"** system. 

Because the flat table renders the Y-axis (Elevation) largely irrelevant for ground targets, we will rely on **Polar Coordinates `(Angle, Distance)`** from the CSV to dictate spawns. 

### Step-by-Step Implementation Flow:
1. **CSV Parsing:** When the game starts, a new `CSVLevelParser` script reads the assigned CSV file and creates a Queue of `(Angle, Distance)` targets.
2. **Spawning Trigger:** `MoleSpawner.cs` continues to use its normal difficulty-based spawn timer. 
3. **Nearest Neighbor Hole Selection:** When the timer triggers, it reads the next `(Angle, Distance)` target from the CSV Queue.
   - It mathematically converts this theoretical Polar coordinate into an X, Z world position relative to the player.
   - It loops through all the physically generated `spawnPoints` on the table.
   - It finds the physical hole that is geometrically **closest** to that theoretical CSV point.
4. **Character Selection:** The character type (Dog, Squirrel, Fake) remains randomized using the current probability logic, ensuring the gameplay feel isn't lost. The chosen character is spawned in the designated hole.

---

## 3. Edge Cases & Failsafe Logic

Because this is medical software, the game must never break, crash, or spawn unreachable targets. The following edge cases are strictly accounted for:

> [!CAUTION]
> **Edge Case 1: The CSV asks for a target far outside the table.**
> *(e.g., The CSV asks for a distance of 1.2m, but the patient's ROM generated a table only 0.6m deep).*
> - **Failsafe:** The logic searches for the "nearest physical hole". It will automatically snap to the furthest available hole on the edge of the generated table. It naturally clamps to the patient's safe boundary.

> [!WARNING]
> **Edge Case 2: The nearest hole to the CSV target is already occupied.**
> - **Failsafe:** The system sorts all available empty holes by distance. If the #1 closest hole is occupied, it smoothly selects the #2 closest available hole.

> [!NOTE]
> **Edge Case 3: The CSV runs out of targets, is empty, or fails to load.**
> *(e.g., The routine ends in 2 minutes, but the patient plays for 5 minutes).*
> - **Failsafe:** `MoleSpawner.cs` detects that the CSV Queue is empty. It automatically switches a boolean flag and falls back to the original `GetFurthestAvailableHole()` logic. The game transitions from the scripted routine to infinite random mode seamlessly.

> [!WARNING]
> **Edge Case 4: The CSV data is corrupted (text instead of numbers).**
> - **Failsafe:** The parser uses `float.TryParse`. If a row is malformed, it logs a silent warning, drops that specific row, and immediately parses the next valid row.

---

## 4. Cloud Integration Roadmap

Fetching the CSV from the cloud ensures patients receive updated routines without requiring app updates. This will be implemented in phases:

- **Phase 1 (Local Foundation):** Build the `CSVLevelParser` to read a local CSV file packaged with the game. This tests the core "Nearest Neighbor" logic safely.
- **Phase 2 (Global Cloud Fetch):** The game uses `UnityWebRequest` to download a single global `level.csv` from a remote server (e.g., AWS or Firebase Storage) before the game starts.
- **Phase 3 (Individual Patient Profiles):** Tied into the `PlayerProfile.json` login system. When Patient #1042 logs in, the headset requests their specific routine from the database, allowing Vishvek/Therapists to assign unique grids to unique users.

---

## Conclusion
This architectural change requires **NO visual modifications** to the table generation or character animations. It safely bridges the gap between prescriptive data-driven routines and adaptive, safe physical boundaries.
