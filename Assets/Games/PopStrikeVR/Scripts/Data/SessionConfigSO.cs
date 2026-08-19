using UnityEngine;

namespace PopstrikeVR.Data
{
    /// <summary>
    /// Stores global gameplay rules, timeouts, and combo system intervals that dictate session flow.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSessionConfig", menuName = "PopstrikeVR/Data/Session Config", order = 2)]
    public class SessionConfigSO : ScriptableObject
    {
        [Header("Task Timing")]
        [Tooltip("Maximum time (in seconds) allowed to complete a task before it times out and breaks the combo.")]
        public float TaskTimeoutSeconds = 15f;
        
        [Tooltip("Time delay (in seconds) between successfully completing a task and spawning the next row.")]
        public float InterTaskDelay = 1.5f;

        [Header("Combo System")]
        [Tooltip("Number of consecutive successful hits required to achieve the 'Hot Streak' bonus.")]
        public int ComboHotStreakThreshold = 5;
        
        [Tooltip("Number of consecutive successful hits required to achieve the 'Unstoppable' bonus.")]
        public int ComboUnstoppableThreshold = 10;
        
        [Tooltip("Error threshold. Number of sequential TMT errors allowed before the combo forcibly breaks.")]
        public int TMTMaxConsecutiveErrors = 2;
    }
}
