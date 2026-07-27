# POPSTRIKE VR — Game Design Document 

## Rehabilitation VR Game | Quest 3 / Quest 3S 

### 1. CONCEPT & CLINICAL INTENT 
PopStrike is a hand-tracked VR rehabilitation game that disguises therapeutic movement as a viscerally satisfying arcade experience. Balloons pop up in the range of user's movement and the user needs to pop them. There are 4 balloon types for each clinical aspect. More on each of them below. 
**Core loop**: Scan range of motion → generate balloon layout → play through task sequence → score and review. 

### 2. THE FOUR BALLOON TYPES 

#### 2.1 BLAZE BALLOON (Orange) — Punch 
**Gesture**: Closed fist, full knuckle extension strike. The wrist must cross a velocity threshold (configurable per patient profile) and the hand must be in a closed fist pose at time of contact. 
**Appearance**: Matte orange with a faint ember glow pulsing at 1Hz. A small flame particle halo floats around the top. 
**Interaction**: Player punches through the balloon. It does not require a careful approach — it rewards aggression and full-arm commitment. The balloon must be struck with a minimum velocity (configurable parameter) to pop; slow nudges do nothing. This trains patients to commit to full extension rather than tentative half-movements. 
**Pop VFX**: The balloon erupts in an orange-gold burst of confetti shards and a ring of flame particles that expand outward and fade in 0.4 seconds. A small shockwave ripple distorts the space briefly. Screen edges flash warm orange. 
**Pop SFX**: A deep, satisfying thud followed immediately by a crackling pop, like a firecracker going off inside a pillow. Punchy low-end.  

#### 2.2 BLADE BALLOON (Blue) — Slash Line 
**Gesture**: Four fingers extended and together, thumb either adducted or abducted, thumb position doesn't really matter. The hand must be in an open, flat blade configuration. A slash gesture is detected by rapid lateral or diagonal hand velocity while in this pose. 
**Appearance**: Blue balloons appear in a connected line of five, glowing with a cool electric-blue tint. A faint dashed line or lightning arc connects them visually, making the slash direction obvious. The line can be horizontal, vertical, or diagonal (up to 45°). A faint directional arrow pulses along the chain. 
**Interaction**: The player must slash through all five balloons in a single continuous motion. Partial chains (e.g., hitting 3 of 5) do not count as success — only a full-chain slash registers. If the player slashes in the wrong direction and only clips one or two, those balloons glow red briefly and respawn — giving visual feedback without harsh punishment. 
**Pop VFX**: All five balloons explode in a cascading chain reaction, one after another in 80ms intervals, producing a blue-white lightning streak that traces the slash path. Electric sparks scatter. The hand trail lights up brilliant white during this moment. 
**Pop SFX**: A rising electric hum that peaks at the last balloon with a sharp crack, like a tesla coil discharge. Very satisfying sequential pop-pop-pop-pop-POP rhythm.  

#### 2.3 TRACE BALLOON (Green) — Index Finger Path Trace 
**Gesture**: Index finger extended, all other fingers (including middle, ring, pinky) flexed into palm. Thumb position is irrelevant. 
**Appearance**: Green balloons appear in clusters of 2–5 connected by a glowing curvilinear or straight path rendered as a vine-like tube. The balloons pulse gently and the path glows with a trail of animated green particles flowing from first to last balloon, indicating direction of travel. 
**Interaction**: The player touches the first green balloon with their index fingertip, which triggers a "locked on" state. They must then keep their fingertip within the path corridor (tolerance configurable, e.g., ±4 cm) and trace through each subsequent balloon in order. Lifting off the path before completing the chain resets that cluster. The path corridor is visualized with a green glow that turns gold as the finger passes through it, giving clear spatial feedback. 
This task demands slow, controlled, intentional movement — a deliberate contrast to the punch and slash tasks, adding therapeutic variety and preventing fatigue from explosive movements. 
**Pop VFX**: As each green balloon is touched in sequence, it bursts into a shower of green sparkles and leaf-like particles. On chain completion, a burst of golden light radiates from the final balloon and the entire path lights up gold briefly. Very nurturing, organic feel. 
**Pop SFX**: A soft, resonant chime per balloon, building in pitch up the chain. On completion, a warm chord resolves — like a musical puzzle being solved. Contrast to the aggressive audio of orange and blue.  

#### 2.4 TRAIL BALLOON (Transparent) — Trail Making Test A & B 
**Gesture**: Index finger extended (same as green balloon gesture), used to touch and connect balloons in sequence. 
**Appearance**: Transparent/frosted glass spheres with either a number (TMT-A) or alternating number and letter (TMT-B) displayed inside, rendered with a soft inner glow. They appear simultaneously. 
- **TMT-A**: Balloons labeled 1 through N. Patient connects them in ascending numerical order by touching each with the index finger, which draws a line between them. 
- **TMT-B**: Balloons labeled alternating numbers and letters (1, A, 2, B, 3, C...). Patient must alternate between number and letter in ascending order. This requires active cognitive switching and working memory load. 
**Interaction**: Each correct touch animates a glowing line between the two balloons. Incorrect touches cause the balloon to flash red and emit a soft error tone. The timer runs from first touch. Time-to-complete and error count are logged. The number/letter size, count, and distribution across 3D space is driven by the CSV file. 
**Pop VFX**: Each correct connection draws a glowing traced line that persists until the full set is complete. On final connection, all balloons simultaneously burst into glittering silver confetti and the connecting lines flash gold and dissolve beautifully. 
**Pop SFX**: Each correct connection plays a brief ascending piano note, building a melody across the sequence. Final connection triggers a short musical flourish. TMT-B has a subtly different tone palette.  

### 3. HAND GESTURE DETECTION SYSTEM 
**Gesture States**: 
- `CLOSED_FIST` — all MCPs flexed, used for Orange balloon (show orange transparent vfx over patient's hand) 
- `OPEN_BLADE` — MCPs extended on index through pinky, used for Blue balloon (show blue blade transparent vfx) 
- `INDEX_POINT` — index extended, MCPs 2–4 flexed, used for Green and Transparent (show green transparent vfx) 

Gesture confidence thresholds are exposed as float values in a ScriptableObject per patient profile. 

**Hand Trail**: Both hands render a persistent motion trail at all times during active play. The trail is a ribbon mesh that samples hand position every 2 frames, fades over 0.3 seconds, and uses a gradient material from fully opaque at the hand to fully transparent at the tail. Color changes based on current state: white at rest, gold during orange punches, electric blue during slash, green during trace, silver during TMT interaction. 

### 4. SPACE SETUP & RANGE OF MOTION MAPPING 
At session start, a brief calibration phase runs. The patient performs a guided "reach as far as comfortable" motion across the front hemisphere (180°). The system samples maximum reach points and maps them to a normalized 100×50 grid (x = horizontal azimuth, y = vertical elevation). This grid corresponds directly to the CSV coordinate space. 
Balloon spawn positions are always placed at or near the boundary of the patient's range of motion. A safety margin scalar (default 0.85×) prevents spawning at the hard limit. 
All spawn positions are computed in world space from the grid → 3D mapping at session load. No runtime computation needed per task.  

### 5. CSV FORMAT & LEVEL PIPELINE  
Every balloon position is defined in spherical coordinates relative to the player's head origin: `(Azimuth, Elevation, Radius)` 
- **Azimuth (°)** — horizontal angle from the player's forward-facing direction. 0° = straight ahead. Positive = right. Negative = left. Range: −90° to +90°. 
- **Elevation (°)** — vertical angle from the horizontal plane. 0° = eye level. Positive = upward. Negative = downward. Range: −45° to +90°. 
- **Radius (units)** — distance from the player's head origin. Fixed per session. 

**ROW FORMAT** 
`TYPE, (Az,El,R), (Az,El,R), ...` 
Rows are executed in order. The LevelDirector advances only after the current task is completed or times out. 

**PER-TYPE FORMAT RULES** 
- **ORANGE**: `O, (Az, El, R)`. Exactly one coordinate. 
- **BLUE**: `B, (Az1,El1,R), ..., (Az5,El5,R)`. Exactly 5 coordinates. Line drawn between them defines slash direction. 
- **GREEN**: `G, (Az1,El1,R), ..., (AzN,ElN,R)`. 2 to 5 coordinates. Path drawn in order. 
- **TMTA**: `TMTA, (Az1,El1,R), ..., (AzN,ElN,R)`. 2 to 9 coordinates. Labelled 1, 2, 3... in listed order. 
- **TMTB**: `TMTB, (Az1,El1,R), ..., (AzN,ElN,R)`. Even number of coordinates. Labelled 1, A, 2, B... in listed order. 

**PARSER RULES & VALIDATION** 
The CSVLevelParser enforces rules on file load. Any violation throws a descriptive error to the therapist dashboard. 

### 6. VFX & AUDIO DESIGN 
**Global Visual Atmosphere**: Abstract void with soft nebula gradients. Background shifts hue based on active balloon type. 
**Hand Trail (Detail)**: TrailRenderer on palm anchor, plus secondary trails on index and middle fingertips. Uses additive blending. Glow intensity scales with hand velocity. 
**Screen Effects**: Hit confirmation flash, combo milestone pulse, miss/timeout desaturation pulse, TMT error vignette. 
**Sound Design**: Positional 3D audio. Adaptive background music layers based on combo multiplier. Voice line system for encouragement.  

### 7. COMBO SYSTEM 
**Combo meter**: Circular arc UI element. Unlocks multipliers: ×1 → ×1.5 → ×2 → ×3. 
**Combo break conditions**: Missing a balloon (timeout), incorrect gesture, incorrect TMT sequence (more than 2 errors). Single errors do not break combo. 
**Streak bonuses**: 5 correct ("HOT STREAK"), 10 correct ("UNSTOPPABLE"), full task perfect ("PERFECT ROUND"). 

### 8. SCORING & SESSION REVIEW 
At session end, a results screen shows: Total score, per-type accuracy/reaction time/ROM used, TMT completion/errors, 1-3 star rating. Data written to local JSON log and syncs to therapist dashboard.
