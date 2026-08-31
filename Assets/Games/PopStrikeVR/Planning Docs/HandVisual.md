# Full-Hand Illumination Plan

Currently, the `GestureTrailManager` attaches a simple `TrailRenderer` (a line) to a single bone on the hand (like the index tip or knuckle). Because you want the **entire hand** to illuminate to match the gesture, we need to change our approach from drawing lines to manipulating the hand's actual 3D mesh.

## Proposed Changes

### Modify `GestureTrailManager.cs`
We will rewrite the `GestureTrailManager` to manipulate the hand's `SkinnedMeshRenderer` instead of moving `TrailRenderers`.

1. **Find the Hand Mesh:** In `Awake()`, the script will automatically search for the `SkinnedMeshRenderer` (or `OVRMeshRenderer`) attached to your VR hand.
2. **Color Mapping:** We will add 3 customizable colors to the Inspector:
   - `FistGlowColor` (Default: Orange)
   - `BladeGlowColor` (Default: Blue)
   - `PointGlowColor` (Default: Green)
3. **Dynamic Material Property Blocks:** In `Update()`, when a gesture is recognized, we will use a highly optimized `MaterialPropertyBlock` to dynamically inject the correct glowing `_EmissionColor` (and HDR intensity) directly into the hand's material. 
4. **Smooth Transitions:** When the player stops the gesture (or stops moving), the glow will smoothly fade out over a few milliseconds rather than turning off instantly, making it look like a high-end VFX.

> [!TIP]
> Using a `MaterialPropertyBlock` ensures we don't accidentally create duplicate material instances in memory. It is extremely fast and perfect for VR performance!

## User Review Required / Open Questions

Before I implement this, I need your answers on two design decisions:

> [!IMPORTANT]
> 1. **Glow Timing:** Previously, the trails only appeared when you moved your hand fast (Velocity > 0.15 m/s). Do you want the full-hand glow to *also* require fast movement, or should the hand glow brightly *anytime* you hold the correct gesture (even if standing still)?
> 2. **Remove Trails?** Should I completely delete the old `TrailRenderer` logic and variables from the script, or should I leave them in as an optional extra effect so you can have *both* a glowing hand *and* a trail?

Please review this plan and let me know your answers to the open questions. Once you give me permission, I will execute the changes!
