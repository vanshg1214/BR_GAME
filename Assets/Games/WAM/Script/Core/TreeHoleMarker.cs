using UnityEngine;

namespace WhackAMole
{
    /// <summary>
    /// Attach this to your Tree Prefab root.
    /// Drag the actual Hole object inside the tree into the 'holeReference' slot.
    /// The layout generator will read this to know exactly where to spawn the mole.
    /// </summary>
    public class TreeHoleMarker : MonoBehaviour
    {
        [Tooltip("Drag the hole object from inside this prefab here.")]
        public Transform holeReference;
    }
}
