using UnityEngine;

namespace WhackAMole.UI
{
    /// <summary>
    /// Attach this to any empty GameObject in your Main Menu scene.
    /// It provides functions that the UI Buttons can call to save settings across scenes!
    /// </summary>
    public class MainMenuSettings : MonoBehaviour
    {
        /// <summary>
        /// Saves whether trees should spawn in the Game Scene.
        /// </summary>
        public void ToggleTrees(bool isOn)
        {
            PlayerPrefs.SetInt("EnableTrees", isOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Saves whether fake (distractor) moles should spawn in the Game Scene.
        /// </summary>
        public void ToggleFakeMoles(bool isOn)
        {
            PlayerPrefs.SetInt("EnableFakeMoles", isOn ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
