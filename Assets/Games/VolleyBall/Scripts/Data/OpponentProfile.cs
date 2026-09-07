using UnityEngine;

namespace Rehab.Volleyball.Data
{
    /// <summary>
    /// Represents a single wave (row) of targeting data from the CSV file.
    /// Used as a playlist of spatial targets for the AI opponent to aim at.
    /// </summary>
    [System.Serializable]
    public class OpponentProfile
    {
        [Tooltip("The Azimuth (horizontal) angle for the target coordinate.")]
        public float TargetAzimuth;

        [Tooltip("The Elevation (vertical) angle for the target coordinate.")]
        public float TargetElevation;

        [Tooltip("The distance (in meters) for the target coordinate.")]
        public float TargetDistance;

        [Tooltip("True if this profile has valid target coordinate data from the CSV.")]
        public bool HasTarget;
    }
}
