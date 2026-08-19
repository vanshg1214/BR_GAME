# POPSTRIKE VR - GAME DESIGN DOCUMENT

**Platform:** Meta Quest 3 / Quest 3S  
**Genre:** Rehabilitation VR Game

---

## 1. CONCEPT & CLINICAL INTENT
PopStrike is a hand-tracked VR rehabilitation game that disguises therapeutic movement as a viscerally satisfying arcade experience. Balloons pop up in the range of the user's movement, and the user must pop them using specific therapeutic gestures.

**Core Loop:**
Scan range of motion → Generate balloon layout → Play through task sequence → Score and review.

---

## 2. THE FOUR BALLOON TYPES

### 2.1 BLAZE BALLOON (Orange) — Punch
*   **Gesture:** Closed fist, full knuckle extension strike. The hand must be in a closed fist pose at the time of contact.
*   **Appearance:** Matte orange with a faint ember glow pulsing at 1Hz. A small flame particle halo floats around the top.
*   **Interaction:** Player punches through the balloon. It requires full-arm commitment and aggression. Must cross a velocity threshold (configurable per patient) to pop.
*   **Pop VFX:** Erupts in an orange-gold burst of confetti shards and a ring of flame particles that expand outward and fade in 0.4s. Small shockwave ripple distorts space. Screen edges flash warm orange.
*   **Pop SFX:** A deep, satisfying thud followed by a crackling pop (punchy low-end).

### 2.2 BLADE BALLOON (Blue) — Slash Line
*   **Gesture:** Open, flat blade configuration (four fingers extended and together). Detected by rapid lateral or diagonal hand velocity.
*   **Appearance:** Connected line of five balloons glowing with a cool electric-blue tint. A faint dashed line or lightning arc connects them, indicating slash direction.
*   **Interaction:** The player must slash through *all five* balloons in a single continuous motion. Partial chains do not count. Incorrect directional slashes cause balloons to glow red and respawn.
*   **Pop VFX:** All five explode in a cascading chain reaction (80ms intervals) producing a blue-white lightning streak. Hand trail lights up brilliant white.
*   **Pop SFX:** Rising electric hum peaking at the last balloon with a sharp crack (tesla coil discharge).

### 2.3 TRACE BALLOON (Green) — Index Finger Path Trace
*   **Gesture:** Index finger extended, all other fingers flexed into the palm (thumb irrelevant).
*   **Appearance:** Clusters of 2–5 connected by a glowing curvilinear or straight vine-like tube. The path glows with flowing green particles indicating the direction.
*   **Interaction:** Player touches the first balloon to "lock-on", then keeps their fingertip within the path corridor (e.g., ±4 cm) tracing through each subsequent balloon. Lifting off resets the cluster.
*   **Pop VFX:** Each balloon bursts into green sparkles and leaves. Chain completion triggers a golden light burst from the final balloon, illuminating the whole path.
*   **Pop SFX:** Soft, resonant chime building in pitch. Completion resolves into a warm musical puzzle-solved chord.

### 2.4 TRAIL BALLOON (Transparent) — Trail Making Test A & B
*   **Gesture:** Index finger extended (same as Green).
*   **Appearance:** Transparent/frosted glass spheres containing either a number (TMT-A) or alternating number/letter (TMT-B) with a soft inner glow. Spawn simultaneously.
*   **Interaction:**
    *   **TMT-A:** Ascending numerical order (1 → 2 → 3...).
    *   **TMT-B:** Alternating alphanumeric sequence (1 → A → 2 → B...). Demands active cognitive switching.
    *   Correct touches draw glowing lines between balloons. Incorrect touches flash red with a soft error tone.
*   **Pop VFX:** Final connection dissolves lines beautifully, and balloons burst into glittering silver confetti.
*   **Pop SFX:** Ascending piano notes forming a melody. Final connection triggers a musical flourish.

---

## 3. HAND GESTURE DETECTION SYSTEM
**Gesture States:**
*   `CLOSED_FIST` (Orange Transparent VFX over hand)
*   `OPEN_BLADE` (Blue Blade Transparent VFX over hand)
*   `INDEX_POINT` (Green Transparent VFX over hand)

*Note: Gesture confidence thresholds are exposed as float values in a ScriptableObject per patient profile.*

**Hand Trail:**
A persistent motion trail ribbon mesh sampling hand position every 2 frames. It uses an additive blending gradient material. Color reflects state:
*   Rest = White
*   Fist = Gold
*   Blade = Electric Blue
*   Trace = Green
*   TMT = Silver

---

## 4. SPACE SETUP & RANGE OF MOTION MAPPING
*   **Calibration:** System samples max reach points across a 180° front hemisphere, mapping to a 100x50 grid (Azimuth x Elevation).
*   **Safety Margin:** Spawns are placed near boundaries with a default 0.85x margin to prevent pain.
*   All coordinates are world-space vectors computed once at load time.

---

## 5. CSV FORMAT & LEVEL PIPELINE
Coordinates are Spherical: `(Azimuth, Elevation, Radius)`
*   `Azimuth (-90 to +90)`: Horizontal
*   `Elevation (-45 to +90)`: Vertical
*   `Radius`: Fixed per session based on reach calibration.

**Format Rules:**
*   **Orange:** `O, (Az, El, R)` (Exactly 1 coord).
*   **Blue:** `B, (Az1,El1,R), (Az2,El2,R)...` (Exactly 5 coords).
*   **Green:** `G, (Az1,El1,R)...` (2 to 5 coords).
*   **TMT-A:** `TMTA, (Az1,El1,R)...` (2 to 9 coords).
*   **TMT-B:** `TMTB, (Az1,El1,R)...` (Even number of coords).

**Parser Validation:** CSVLevelParser enforces coordinate counts, collinearity for Blue paths, and out-of-range warnings before the session starts.

---

## 6. VFX & AUDIO DESIGN
*   **Atmosphere:** Abstract void / soft nebula gradients shifting hue based on the active balloon type.
*   **Screen Effects:**
    *   Hit confirmation (1-frame full screen flash).
    *   Combo milestone (edge pulse gold).
    *   Miss/Timeout (desaturation pulse).
    *   TMT Error (red vignette).
*   **Audio:** 3D positional. Adaptive background music scales intensity with combo multiplier.

---

## 7. COMBO SYSTEM
Rewards correct form and full ROM commitment over pure speed.
*   **Meter:** Bottom-center circular arc unlocking multipliers (1x → 1.5x → 2x → 3x).
*   **Break Conditions:** Missed balloon, wrong gesture, >2 TMT sequence errors. Single errors forgive combo breaks.
*   **Streak Bonuses:**
    *   5 Correct = "HOT STREAK"
    *   10 Correct = "UNSTOPPABLE"
    *   0 Errors = "PERFECT ROUND"

---

## 8. SCORING & SESSION REVIEW
End of session Results Screen shows:
*   Total score + Multiplier Breakdown.
*   Per-Type Accuracy %, Reaction Time, ROM % used.
*   TMT completion time and error logs.
*   1–3 Star Rating.
*   *Data exported to JSON log for Therapist Dashboard Sync.*
