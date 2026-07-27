using UnityEngine;
using WhackAMole;          // BaseMole, ScoreManager, FeedbackManager etc.
using WhackAMole.Targets; // ExplosiveProp

namespace WhackAMole.Characters
{
    /// <summary>
    /// Attached to the HAMSTER. 
    /// The Hamster itself cannot be hit. It simply pops up holding the Bomb and waits.
    /// </summary>
    public class FakeCharacter : BaseMole
    {
        [Header("Target Link")]
        [Tooltip("Drag the Bomb (ExplosiveProp script) child object here.")]
        public ExplosiveProp bombTarget;

        protected override void Awake()
        {
            base.Awake();

            // 1. Disable hit detection on the character body itself so it's immune to the hammer
            Collider[] cols = GetComponentsInChildren<Collider>();
            foreach(Collider c in cols) 
            {
                // Disable colliders that belong to the character, but keep the bomb's colliders active!
                if (bombTarget == null || !c.transform.IsChildOf(bombTarget.transform))
                {
                    c.enabled = false;
                }
            }

            // 2. Listen for the Bomb exploding
            if (bombTarget != null)
            {
                bombTarget.OnTargetHit += HandleBombExploded;
            }
        }

        private void HandleBombExploded()
        {
            // The Bomb was hit and exploded!
            // The character is scared and immediately ducks back down to safety.
            
            // Retract the character into the hole smoothly
            RetractIntoHole();
            
            // If you have a specific "Scared/Duck" animation, trigger it here:
            // Animator anim = GetComponentInChildren<Animator>();
            // if (anim != null) anim.SetTrigger("Scared");
        }

        // We override and ignore the old hitting logic entirely
        public override void OnHit(Vector3 velocity, Vector3 hitPosition) { }
        protected override void TriggerFeedback(Vector3 hitPosition, Vector3 velocity, int holeIndex) { }
        
        public override bool IsFakeOrDecoy => true;
    }
}
