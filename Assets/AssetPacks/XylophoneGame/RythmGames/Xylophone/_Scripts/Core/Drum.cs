using UnityEngine;

namespace XM {
    public class Drum : MonoBehaviour {
        [Header("Audio")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip hitSound;

        [Header("Animation/VFX")]
        [SerializeField] private Animator animator;
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private ParticleSystem hitVFX;

        private void Start() {
            if (sfxSource == null) {
                sfxSource = GetComponent<AudioSource>();
            }
            if (animator == null) {
                animator = GetComponent<Animator>();
            }
        }

        public void DrumHit() {
            // Play Audio
            if (sfxSource != null && hitSound != null) {
                sfxSource.PlayOneShot(hitSound);
            }

            // Trigger Animation
            if (animator != null && !string.IsNullOrEmpty(hitTrigger)) {
                animator.SetTrigger(hitTrigger);
            }

            // Play VFX
            if (hitVFX != null) {
                hitVFX.Play();
            }

            Debug.Log($"[Drum] {gameObject.name} was hit!");
        }
    }
}
