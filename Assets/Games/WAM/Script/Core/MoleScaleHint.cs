using UnityEngine;

namespace WhackAMole
{
    /// <summary>
    /// Placed on ProxyFlatSpawn by HoleLayoutGenerator.
    /// Stores the intended display scale for moles that spawn into this hole.
    /// 
    /// WHY THIS EXISTS:
    /// ProxyFlatSpawn must have localScale = Vector3.one so that mole
    /// localPosition-based animations (pop up/down) work correctly in world space.
    /// Previously, ProxyFlatSpawn had localScale = 0.5 which caused:
    ///   1. All localPosition offsets (hideDepth, visibleDepth) to be halved,
    ///      making moles appear to pop up half as high as intended.
    ///   2. The "spawnOrigin" captured in OnEnable to be in a warped coordinate
    ///      space, causing moles to appear offset from the hole center.
    /// 
    /// Now the mole reads this component in OnEnable and applies the scale itself.
    /// </summary>
    public class MoleScaleHint : MonoBehaviour
    {
        [Tooltip("The desired uniform display scale for a mole spawned at this hole. " +
                 "Set by HoleLayoutGenerator based on ROM calibration.")]
        public float desiredWorldScale = 0.5f;

        [HideInInspector] public int rowIndex;
        [HideInInspector] public int columnIndex;
        [HideInInspector] public int holesInThisRow;

        public bool IsEdgeColumn => columnIndex == 0 || columnIndex == holesInThisRow - 1;
    }
}
