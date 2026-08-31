POPSTRIKE VR — Game Design Document 

Rehabilitation VR Game | Quest 3 / Quest 3S 

 

1. CONCEPT & CLINICAL INTENT 

PopStrike is a hand-tracked VR rehabilitation game that disguises therapeutic movement as a viscerally satisfying arcade experience. Balloons pop up in the range of user's movement and the user needs to pop them. There are 4 balloon types for each clinical aspect. More on each of them below. 

Core loop: Scan range of motion → generate balloon layout → play through task sequence → score and review. 

 

2. THE FOUR BALLOON TYPES 

2.1 BLAZE BALLOON (Orange) — Punch 

Gesture: Closed fist, full knuckle extension strike. The wrist must cross a velocity threshold (configurable per patient profile) and the hand must be in a closed fist pose at time of contact. 

Appearance: Matte orange with a faint ember glow pulsing at 1Hz. A small flame particle halo floats around the top. 

Interaction: Player punches through the balloon. It does not require a careful approach — it rewards aggression and full-arm commitment. The balloon must be struck with a minimum velocity (configurable parameter) to pop; slow nudges do nothing. This trains patients to commit to full extension rather than tentative half-movements. 

Pop VFX: The balloon erupts in an orange-gold burst of confetti shards and a ring of flame particles that expand outward and fade in 0.4 seconds. A small shockwave ripple distorts the space briefly. Screen edges flash warm orange. 

Pop SFX: A deep, satisfying thud followed immediately by a crackling pop, like a firecracker going off inside a pillow. Punchy low-end.  

 

2.2 BLADE BALLOON (Blue) — Slash Line 

Gesture: Four fingers extended and together, thumb either adducted or abducted, thumb position doesn't really matter. The hand must be in an open, flat blade configuration. A slash gesture is detected by rapid lateral or diagonal hand velocity while in this pose. 

Appearance: Blue balloons appear in a connected line of five, glowing with a cool electric-blue tint. A faint dashed line or lightning arc connects them visually, making the slash direction obvious. The line can be horizontal, vertical, or diagonal (up to 45°). A faint directional arrow pulses along the chain. 

Interaction: The player must slash through all five balloons in a single continuous motion. Partial chains (e.g., hitting 3 of 5) do not count as success — only a full-chain slash registers. If the player slashes in the wrong direction and only clips one or two, those balloons glow red briefly and respawn — giving visual feedback without harsh punishment. 

Pop VFX: All five balloons explode in a cascading chain reaction, one after another in 80ms intervals, producing a blue-white lightning streak that traces the slash path. Electric sparks scatter. The hand trail (see section 6) lights up brilliant white during this moment. 

Pop SFX: A rising electric hum that peaks at the last balloon with a sharp crack, like a tesla coil discharge. Very satisfying sequential pop-pop-pop-pop-POP rhythm.  

 

2.3 TRACE BALLOON (Green) — Index Finger Path Trace 

Gesture: Index finger extended, all other fingers (including middle, ring, pinky) flexed into palm. Thumb position is irrelevant. 

Appearance: Green balloons appear in clusters of 2–5 connected by a glowing curvilinear or straight path rendered as a vine-like tube. The balloons pulse gently and the path glows with a trail of animated green particles flowing from first to last balloon, indicating direction of travel. 

Interaction: The player touches the first green balloon with their index fingertip, which triggers a "locked on" state. They must then keep their fingertip within the path corridor (tolerance configurable, e.g., ±4 cm) and trace through each subsequent balloon in order. Lifting off the path before completing the chain resets that cluster. The path corridor is visualized with a green glow that turns gold as the finger passes through it, giving clear spatial feedback. 

This task demands slow, controlled, intentional movement — a deliberate contrast to the punch and slash tasks, adding therapeutic variety and preventing fatigue from explosive movements. 

Pop VFX: As each green balloon is touched in sequence, it bursts into a shower of green sparkles and leaf-like particles. On chain completion, a burst of golden light radiates from the final balloon and the entire path lights up gold briefly. Very nurturing, organic feel. 

Pop SFX: A soft, resonant chime per balloon, building in pitch up the chain. On completion, a warm chord resolves — like a musical puzzle being solved. Contrast to the aggressive audio of orange and blue.  

 

2.4 TRAIL BALLOON (Transparent) — Trail Making Test A & B 

Gesture: Index finger extended (same as green balloon gesture), used to touch and connect balloons in sequence. 

Appearance: Transparent/frosted glass spheres with either a number (TMT-A) or alternating number and letter (TMT-B) displayed inside, rendered with a soft inner glow. They appear simultaneously — all balloons in a TMT set spawn at once across the 3D space. 

TMT-A: Balloons labeled 1 through N. Patient connects them in ascending numerical order by touching each with the index finger, which draws a line between them. 

TMT-B: Balloons labeled alternating numbers and letters (1, A, 2, B, 3, C...). Patient must alternate between number and letter in ascending order. This requires active cognitive switching and working memory load — the core clinical purpose of TMT-B. 

Interaction: Each correct touch animates a glowing line between the two balloons. Incorrect touches cause the balloon to flash red and emit a soft error tone — no harsh buzzer, just a subtle cue. The timer runs from first touch. Time-to-complete and error count are logged. 

The number/letter size, count, and distribution across 3D space is driven by the CSV file, meaning difficulty can be tuned precisely per session without code changes. 

Pop VFX: Each correct connection draws a glowing traced line that persists until the full set is complete. On final connection, all balloons simultaneously burst into glittering silver confetti and the connecting lines flash gold and dissolve beautifully. 

Pop SFX: Each correct connection plays a brief ascending piano note, building a melody across the sequence. Final connection triggers a short musical flourish. TMT-B has a subtly different tone palette (brighter, more complex) to distinguish it.  

 

3. HAND GESTURE DETECTION SYSTEM 

Gesture States: 

CLOSED_FIST — all MCPs flexed, used for Orange balloon (show orange transparent vfx over patient's hand to indicate gesture state visually) 

OPEN_BLADE — MCPs extended on index through pinky, used for Blue balloon (show blue blade transparent vfx over patient's hand) 

INDEX_POINT — index extended, MCPs 2–4 flexed, used for Green and Transparent (show green transparent vfx over patient's hand) 

Gesture confidence thresholds are exposed as float values in a ScriptableObject per patient profile so therapists can lower thresholds for patients with limited mobility. 

Hand Trail: Both hands render a persistent motion trail at all times during active play. The trail is a ribbon mesh that samples hand position every 2 frames, fades over 0.3 seconds, and uses a gradient material from fully opaque at the hand to fully transparent at the tail. Color changes based on current state: white at rest, gold during orange punches, electric blue during slash, green during trace, silver during TMT interaction. The trail is the single most important feedback element for making movement feel powerful and satisfying.  

 

4. SPACE SETUP & RANGE OF MOTION MAPPING 

At session start, a brief calibration phase runs. The patient performs a guided "reach as far as comfortable" motion across the front hemisphere (180°). The system samples maximum reach points and maps them to a normalized 100×50 grid (x = horizontal azimuth, y = vertical elevation). This grid corresponds directly to the CSV coordinate space. 

Balloon spawn positions are always placed at or near the boundary of the patient's range of motion — the clinical intent being to gently challenge the edge of capacity without causing pain. A safety margin scalar (default 0.85×) prevents spawning at the hard limit. 

All spawn positions are computed in world space from the grid → 3D mapping at session load. No runtime computation needed per task.  

 

5. CSV FORMAT & LEVEL PIPELINE  

Every balloon position is defined in spherical coordinates relative to the player's head origin: 

(Azimuth, Elevation, Radius) 
 

Azimuth (°) — horizontal angle from the player's forward-facing direction. 

0° = straight ahead. Positive = right. Negative = left. Practical range: −90° to +90°. 

Elevation (°) — vertical angle from the horizontal plane. 

0° = eye level. Positive = upward. Negative = downward. Practical range: −45° to +90°. 

Radius (units) — distance from the player's head origin along the direction vector. This value is fixed per session — it is calibrated once at the start based on the patient's arm reach and stamped into every row of the CSV. All balloons in a session live on the same sphere shell. Designers should treat radius as a read-only session constant, not a per-balloon creative tool. 

The Unity parser converts each (Az, El, R) triplet to a world-space Vector3 using standard spherical-to-Cartesian conversion, then offsets by the player's head position at calibration time.  

 

ROW FORMAT 

Each row in the CSV represents one discrete task that plays out fully before the next row begins. The first cell is always the balloon type identifier. Every subsequent cell is one balloon's coordinate triplet. 

TYPE, (Az,El,R), (Az,El,R), ... 
 

Rows are executed in order from top to bottom. There is no timing data in the CSV — the LevelDirector advances to the next row only after the current task is completed or times out (timeout is set in a separate session config, not in this file). 

 

PER-TYPE FORMAT RULES 

ORANGE — Single Punch Balloon 

O, (Az, El, R) 
 

Exactly one coordinate. One balloon spawns at that position. 

Example: 

O, (30,60,5) 
 

One orange balloon appears 30° right of centre, 60° above eye level, at radius 5. 

 

BLUE — Slash Chain (5 Balloons) 

B, (Az1,El1,R), (Az2,El2,R), (Az3,El3,R), (Az4,El4,R), (Az5,El5,R) 
 

Always exactly 5 coordinates. The balloons are connected visually in the order they are listed. The line drawn between them defines the slash direction the player must follow. The parser derives the dominant slash axis from the vector between the first and last coordinate — this is used for gesture validation. 

Designers should lay out the 5 coordinates as a smooth progression along one angular axis (e.g., stepping elevation by 10° each while holding azimuth constant, as in the example below) to produce a clear, readable slash line. Diagonal lines are valid — mix azimuth and elevation steps together. 

Example: 

B, (30,70,5), (30,60,5), (30,50,5), (30,40,5), (30,30,5) 
 

Five blue balloons descending vertically on the right side. Player slashes downward to clear the chain. 

 

GREEN — Trace Path (2 to 5 Balloons) 

G, (Az1,El1,R), (Az2,El2,R), ..., (AzN,ElN,R) 
 

Minimum 2 coordinates, maximum 5. The trace path is constructed by the parser by drawing a curve through each point in listed order. The player must touch balloon 1 first, trace to balloon 2, and so on. Order matters — it defines the movement direction being trained. 

Example: 

G, (−20,30,5), (0,45,5), (20,30,5) 
 

Three green balloons forming an arc from lower-left to centre-high to lower-right. Patient traces an upward arc and back down. 

 

TMTA — Trail Making Test A (Numbers Only) 

TMTA, (Az1,El1,R), (Az2,El2,R), ..., (AzN,ElN,R) 
 

Each coordinate position is automatically assigned a sequential number label by the parser starting at 1. The first coordinate listed = balloon labelled 1, the second = 2, and so on. The listed order is the correct answer sequence — it is also used by the parser to validate player input and log errors. 

Example: 

TMTA, (−40,20,5), (10,60,5), (−15,45,5), (30,10,5), (−5,30,5) 
 

Five transparent balloons appear simultaneously, labelled 1 through 5. The player connects them in ascending order. The positional arrangement in the CSV defines only where they appear, not any visual ordering — the patient must read the numbers to find the sequence. 

Important: Designers should distribute TMTA coordinates deliberately across the full hemisphere (mix of left/right and high/low positions) to maximize the scanning and reaching demand. 

 

TMTB — Trail Making Test B (Alternating Numbers and Letters) 

TMTB, (Az1,El1,R), (Az2,El2,R), (Az3,El3,R), (Az4,El4,R), ..., (AzN,ElN,R) 
 

The parser assigns labels to coordinates using the following fixed interleave rule, regardless of how many pairs are included: 

Position 1 → label "1" 
Position 2 → label "A" 
Position 3 → label "2" 
Position 4 → label "B" 
Position 5 → label "3" 
Position 6 → label "C" 
...and so on, alternating integer then letter. 
 

The correct answer sequence the patient must follow is: 1 → A → 2 → B → 3 → C → ... 

The coordinate listed at position 1 will wear the label "1", the coordinate at position 2 will wear "A", and so on. The spatial positions and the label assignments are therefore entirely controlled by the order in which the designer lists the coordinates. 

Example: 

TMTB, (−30,50,5), (20,20,5), (−10,65,5), (40,40,5), (0,15,5), (−25,35,5) 
 

Six balloons spawn simultaneously. The parser labels them: −30,50 = "1", 20,20 = "A", −10,65 = "2", 40,40 = "B", 0,15 = "3", −25,35 = "C". Patient must connect them in the sequence 1 → A → 2 → B → 3 → C. 

 

FULL EXAMPLE SESSION CSV 

O, (30,60,5), (50,10,5), (70,10,5) 
O, (−20,45,5) 
B, (30,70,5), (30,60,5), (30,50,5), (30,40,5), (30,30,5) 
G, (−20,30,5), (0,45,5), (20,30,5) 
G, (−35,20,5), (−10,50,5), (15,60,5), (35,40,5) 
O, (0,70,5) 
TMTA, (−40,20,5), (10,60,5), (−15,45,5), (30,10,5), (−5,30,5) 
B, (−30,30,5), (−15,40,5), (0,50,5), (15,40,5), (30,30,5) 
TMTB, (−30,50,5), (20,20,5), (−10,65,5), (40,40,5), (0,15,5), (−25,35,5) 
O, (15,55,5) 
 
 

This is a 10-task session. The level runs top to bottom. Two warm-up orange punches → one vertical slash chain → two green traces of increasing length → one high orange → a 5-target TMT-A → a diagonal slash → a 6-target TMT-B → one final orange. 

 

PARSER RULES & VALIDATION 

The CSVLevelParser must enforce the following on file load, before the session starts. Any violation should throw a descriptive error to the therapist dashboard, not silently skip the row. 

O rows: Must have atleast 1 coordinate. More than 1 means all the orange balls appear simultaneously to the user. 

B rows: Must have atleast 3 coordinates. Fewer is an error. Parser also checks that the 3 or more points are roughly collinear (angular deviation check) and warns if they are not — this protects against slash lines that would be ambiguous to the player. 

G rows: Must have 2 to 5 coordinates. Single coordinate is an error. More than 5 is an error. 

TMTA rows: Must have 2 to 9 coordinates (clinical TMT-A standard uses up to 25, but in VR the practical visibility limit is ~9 per spatial frame). 

TMTB rows: Must have an even number of coordinates. Odd count means a number-letter pair is incomplete — this is always an error. 

All rows: Every (Az, El, R) value must fall within the calibrated ROM bounds for this patient session. The parser checks each coordinate against the stored ROM map and flags any out-of-range spawn as a warning. Out-of-range does not abort the session but is logged for the therapist. 

 

NOTES FOR AI LEVEL GENERATION 

When this CSV is generated by an AI system rather than a human designer, the following rules must be in the generation prompt: 

The session radius R is always passed in as a fixed scalar from the patient profile — the AI must never vary it within a single session file. All coordinates must stay within the patient's calibrated ROM hemisphere. TMT-B coordinate count must always be even. Blue balloon coordinates must form a visually clear line by varying primarily one angle axis at a time. Green trace paths should avoid coordinates that backtrack sharply — smooth arcs are the target. Task ordering should follow a warm-up to challenge progression: orange tasks early, TMT tasks in the second half. 

 

6. VFX & AUDIO DESIGN 

Global Visual Atmosphere 

The play space is a stylized abstract environment — deep space or abstract void with soft nebula gradients in the background. No distracting objects. Balloons are the only things competing for attention. The background subtly shifts hue based on which balloon type is currently active: warm amber for orange tasks, cool blue for blade tasks, soft forest green for trace tasks, clean silver-white for TMT. 

Hand Trail (Detail) 

Implemented as a TrailRenderer on each hand's palm anchor point, plus secondary trails on index and middle fingertips. The main palm trail is 8cm wide at origin, tapering to 0. Material uses additive blending so trails glow over any background. Intensity of the glow scales with hand velocity — fast punches produce bright wide trails, slow traces produce thin delicate lines. This single system alone makes movement feel cinematic. 

Screen Effects 

Hit confirmation: brief 1-frame full-screen flash, tinted to balloon color 

Combo milestone: screen-edge pulse glow in gold 

Miss/timeout: subtle desaturation pulse, 0.5 seconds, then recovery 

TMT error: soft red vignette flash 

Sound Design 

All SFX are positional 3D audio except UI sounds. Background music is adaptive — it has a base layer (ambient rhythmic pulse, ~90 BPM, energetic but not distracting) and layers that add intensity as combo multiplier increases. On TMT tasks, the music drops to minimal atmospheric to reduce cognitive load interference. 

A voice line system (optional, toggled by therapist) provides encouragement at combo milestones: "Perfect!", "On fire!", "Incredible chain!" — recorded, not synthesized.  

 

7. COMBO SYSTEM 

The combo system is the primary engagement driver. It rewards clinical compliance (correct gesture + full ROM commitment) rather than just speed. 

Combo meter: A circular arc UI element anchored at the bottom center of the patient's visual field. It fills as correct balloons are completed. At intervals it unlocks multipliers: ×1 → ×1.5 → ×2 → ×3. 

Combo break conditions: Missing a balloon (timeout), incorrect gesture, incorrect TMT sequence (more than 2 errors in a row). Single errors do not break the combo — this is clinically important, as patients need forgiveness. 

Streak bonuses: 

5 consecutive correct: "HOT STREAK" — all current balloons glow gold, brief music swell 

10 consecutive correct: "UNSTOPPABLE" — background pulses, hand trails expand and brighten, particle burst from both hands 

Full task without any error: "PERFECT ROUND" — balloons on next task spawn with a star particle halo 

Combo visual: The combo multiplier number is displayed in large, bold, world-space text near the patient's eye line, animating with a pop-scale on each increment. 

Combo and rehab alignment: The combo system implicitly rewards completing the full range of motion (because short, lazy movements don't trigger pop) and using the correct gesture (because wrong gesture = miss). Patients are intrinsically motivated to move correctly and completely. 

 

8. SCORING & SESSION REVIEW 

At session end, a results screen shows: 

Total score with combo multiplier breakdown 

Per balloon type: accuracy %, average reaction time, average ROM used (as % of maximum calibrated range) 

TMT specific: completion time, error count, sequence for both A and B (logged separately) 

A simple star rating (1–3 stars) per task 

This data is written to a local JSON log file and optionally synced to a therapist dashboard. The ROM data and TMT metrics directly feed back into the AI level generator for the next session. 

 