using UnityEngine;

namespace WhackAMole
{
    /// <summary>
    /// Holds settings that only last for the current play session.
    /// These reset to true every time you hit Play!
    /// </summary>
    public static class SessionSettings
    {
        public static bool EnableTrees = true;
        public static bool EnableFakeMoles = true;
    }
}
