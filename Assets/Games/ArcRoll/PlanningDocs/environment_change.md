# Environmental Polish & Gamification Plan

To make the VR rehabilitation space feel warmer, more lively, and less "sterile/clinical," we can implement visual and auditory environmental polish. Here is the detailed proposal.

---

## 1. Visual & Atmospheric Polish

### A. Twilight/Sunset Gradient Skybox
* **What:** Replace default Unity skyboxes with a custom two-tone HSL gradient skybox (Peach/Coral shifting to Deep Indigo/Violet).
* **Impact:** Warmer, calming aesthetics reduce patient anxiety, while high-contrast colors make the bright balloons, basketballs, and bowling pins pop visually.

### B. Emissive Neon Lane/Hoop Accents
* **What:** Apply high-intensity emissive materials to key borders:
  * Neon lighting strips along the bowling lanes.
  * Glowing LED rings around the basketball hoops that pulse slightly.
  * Glowing neon brackets on the balloon spawn frames.
* **Impact:** Draws the patient's focus naturally to target zones (visual queuing) and creates a modern arcade aesthetic.

### C. Large LED Scoreboard & Leaderboard
* **What:** Place a large retro-arcade style pixel board or virtual CRT screen in the background.
  * Displays: **Current Score**, **Active Streak (with fire/glow animations)**, and **Patient Name**.
* **Impact:** Immediate visual reward loop for successful movements.

---

## 2. Auditory & Cheering Crowd System

### A. Ambient Stadium Hum
* **What:** Add a low-volume looping background audio clip of a soft outdoor park or distant stadium hum.
* **Impact:** Eliminates sterile silence, establishing a comfortable outdoor playground atmosphere.

### B. Audio-Reactive Streak Swells (`ArcRollEnvironmentManager.cs`)
* **What:** Create a script connected to `ArcRollScoreManager` that monitors the player's streak:
  * **Streak 1-2:** Distant applause or murmurs.
  * **Streak 3-4:** Medium cheers and clapping.
  * **Streak 5+ (Max Combo):** Exploding stadium cheer/roar that fades out after 3 seconds.
* **Impact:** Strong auditory reward system that motivates patients to maintain correct form and continue throwing.

---

## 3. Dynamic Particle Celebrations

### C. Confetti Blast on High Streak
* **What:** Add a confetti particle system behind the target.
  * Triggered whenever the patient scores at a streak of **3 or higher**.
  * Confetti floats down slowly using gravity, catching the twilight skybox lighting.
* **Impact:** Creates a visually rich celebration effect upon score completion.

---

## Implementation Steps (No Code Executed)

1. **Audio Setup:**
   * Create an `ArcRollEnvironmentManager` component.
   * Attach `AudioSource` nodes for background ambiance and dynamic crowd cheers.
2. **Material Configuration:**
   * Create an emissive material using standard Universal Render Pipeline (URP) shader settings.
   * Set global bloom properties in the scene profile to make the neon lines glow softly.
3. **Prefab Placement:**
   * Drop low-poly spectator/bench assets into the scene background.
   * Set up a large UI Canvas in World Space for the retro Scoreboard.
