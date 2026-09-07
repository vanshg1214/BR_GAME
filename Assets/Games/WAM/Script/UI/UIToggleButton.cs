using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace WhackAMole.UI
{
    [RequireComponent(typeof(Button))]
    public class UIToggleButton : MonoBehaviour
    {
        public enum SettingType { None, EnableTrees, EnableFakeMoles }

        [Header("Save Settings")]
        [Tooltip("If set, this button will automatically save its state to PlayerPrefs without needing events!")]
        public SettingType settingType = SettingType.None;

        [Header("Current State")]
        [Tooltip("Is the setting currently enabled?")]
        [SerializeField] private bool isOn = true;

        [Header("Visual Components")]
        [Tooltip("The Image component that changes color/sprite. If left empty, auto-detects the Button's graphic.")]
        [SerializeField] private Image targetImage;
        [Tooltip("The Text component that changes text. Auto-detects child TextMeshPro component if empty.")]
        [SerializeField] private TMP_Text targetText;

        [Header("Selected (ON) Settings")]
        [SerializeField] private string textWhenOn = "Setting: ON";
        [SerializeField] private Color colorWhenOn = new Color(0.2f, 0.8f, 0.2f, 1f); // Green
        [SerializeField] private Color textColorWhenOn = Color.white;

        [Header("Deselected (OFF) Settings")]
        [SerializeField] private string textWhenOff = "Setting: OFF";
        [SerializeField] private Color colorWhenOff = new Color(0.5f, 0.5f, 0.5f, 1f); // Gray
        [SerializeField] private Color textColorWhenOff = Color.white;

        [Header("Events")]
        [Tooltip("Fires when the button is clicked. Passes true if ON, false if OFF.")]
        public UnityEvent<bool> OnToggled;

        private Button button;

        public bool IsOn => isOn;

        private void Awake()
        {
            button = GetComponent<Button>();
            
            // CRITICAL: Disable Unity's built-in Color Tint transition so it doesn't fight our custom colors!
            button.transition = Selectable.Transition.None;

            button.onClick.AddListener(OnButtonClicked);

            // Auto-load state from SessionSettings if a type is set
            if (settingType == SettingType.EnableTrees)
            {
                isOn = SessionSettings.EnableTrees;
            }
            else if (settingType == SettingType.EnableFakeMoles)
            {
                isOn = SessionSettings.EnableFakeMoles;
            }

            // Auto-find components if the user forgot to assign them in the inspector
            if (targetImage == null)
            {
                targetImage = button.targetGraphic as Image;
            }

            if (targetText == null)
            {
                targetText = GetComponentInChildren<TMP_Text>(true);
            }

            // Apply the initial visual state immediately
            RefreshVisuals();
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClicked);
            }
        }

        /// <summary>
        /// Call this via code to set the toggle state explicitly without simulating a click.
        /// </summary>
        public void SetState(bool state, bool triggerEvent = false)
        {
            if (isOn == state) return;
            
            isOn = state;
            RefreshVisuals();
            
            if (triggerEvent)
            {
                OnToggled?.Invoke(isOn);
            }
        }

        private void OnButtonClicked()
        {
            isOn = !isOn; // Flip the boolean
            RefreshVisuals(); // Update colors and text
            
            // Automatically save to SessionSettings!
            if (settingType == SettingType.EnableTrees) SessionSettings.EnableTrees = isOn;
            if (settingType == SettingType.EnableFakeMoles) SessionSettings.EnableFakeMoles = isOn;

            OnToggled?.Invoke(isOn); // Tell other scripts about the change
        }

        private void RefreshVisuals()
        {
            if (targetImage != null)
            {
                // Set the image color exactly like MainMenuManager does it!
                targetImage.color = isOn ? colorWhenOn : colorWhenOff;
            }

            if (targetText != null)
            {
                targetText.text = isOn ? textWhenOn : textWhenOff;
                targetText.color = isOn ? textColorWhenOn : textColorWhenOff;
            }
        }
    }
}
