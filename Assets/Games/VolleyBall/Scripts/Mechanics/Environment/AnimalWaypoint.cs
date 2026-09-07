using UnityEngine;

namespace Rehab.Volleyball.Mechanics
{
    public enum WaypointAction
    {
        None,
        Jump,
        StunAtPlayer
    }

    /// <summary>
    /// Attach this to empty GameObjects in your scene to define what an animal should do when it arrives here.
    /// </summary>
    public class AnimalWaypoint : MonoBehaviour
    {
        [Tooltip("What should the animal do when it reaches this point?")]
        public WaypointAction action = WaypointAction.None;
        
        [Tooltip("How long should the animal wait here (in seconds) before moving to the next point?")]
        public float waitTime = 2.0f;
    }
}
