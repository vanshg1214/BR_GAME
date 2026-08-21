using UnityEngine;

namespace ArcRoll.Gameplay
{
    /// <summary>
    /// Restricts hand grabbing based on the hand mode chosen in the Main Menu.
    /// Attach this to both the Left Hand and Right Hand GameObjects in your scene/prefab.
    /// </summary>
    public class ArcRollHandConstraint : MonoBehaviour
    {
        [Tooltip("Check this if this script is attached to the Left Hand GameObject.")]
        [SerializeField] private bool isLeftHand;

        private void Start()
        {
            ApplyHandConstraints();
        }

        private void ApplyHandConstraints()
        {
            string mode = ArcRoll.UI.ArcRollMenuManager.HandMode;

            // Determine if this hand needs to be disabled
            bool shouldDisable = false;
            if (mode == "Left" && !isLeftHand)
            {
                shouldDisable = true;
            }
            else if (mode == "Right" && isLeftHand)
            {
                shouldDisable = true;
            }

            if (shouldDisable)
            {
                DisableHandInteractions();
            }
        }

        private void DisableHandInteractions()
        {
            string targetHand = isLeftHand ? "left" : "right";
            string oppositeHand = isLeftHand ? "right" : "left";
            int disabledCount = 0;

            // 1. Search the ENTIRE scene for ISDK Interactors or OVRGrabbers.
            // This is necessary because ISDK interactors live in a completely separate 
            // GameObject hierarchy (like OVRInteractionComprehensive) from the visual hand prefab!
            MonoBehaviour[] allComponents = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            
            foreach (var comp in allComponents)
            {
                if (comp == null) continue;
                
                string typeName = comp.GetType().Name;
                if (typeName.Contains("Grabber") || typeName.Contains("Interactor"))
                {
                    // Check if this component belongs to the target hand by inspecting its GameObject name and parents
                    if (IsComponentForHand(comp.transform, targetHand, oppositeHand))
                    {
                        comp.enabled = false;
                        disabledCount++;
                        Debug.Log($"[ArcRollHandConstraint] Disabled ISDK/OVR component: {typeName} on {comp.gameObject.name}");
                    }
                }
            }

            // 2. Disable local collider (Legacy OVRGrabber physical push prevention)
            Collider col = GetComponent<Collider>();
            if (col == null) col = GetComponentInChildren<Collider>();
            if (col != null)
            {
                col.enabled = false;
                Debug.Log($"[ArcRollHandConstraint] Disabled local physical collider on {gameObject.name}");
            }
            
            Debug.Log($"[ArcRollHandConstraint] Finished disabling {disabledCount} interactors for the {targetHand} hand.");
        }

        private bool IsComponentForHand(Transform t, string targetHand, string oppositeHand)
        {
            // Walk up the hierarchy to see if this object belongs to the Left or Right hand
            Transform current = t;
            while (current != null)
            {
                string objName = current.name.ToLower();
                
                // If it explicitly contains the opposite hand's name, it's not ours!
                if (objName.Contains(oppositeHand)) return false;

                // If it explicitly contains our target hand's name, it's ours!
                if (objName.Contains(targetHand)) return true;

                current = current.parent;
            }
            
            // Fallback: If we attached this script directly to a local object that didn't have Left/Right in the name,
            // we assume anything physically under this specific script's transform is ours.
            return t.IsChildOf(this.transform);
        }
    }
}
