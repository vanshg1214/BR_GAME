using UnityEngine;

namespace Rehab.Volleyball.Core
{
    /// <summary>
    /// Singleton manager that handles all Audio and VFX for the game.
    /// Acts as a central hub so other scripts don't need direct references to clips/prefabs.
    /// </summary>
    public class VolleyballEffectsManager : MonoBehaviour
    {
        public static VolleyballEffectsManager Instance { get; private set; }

        [Header("Audio: Ball Impacts")]
        [SerializeField] private AudioClip playerHitSound;
        [SerializeField] private AudioClip aiHitSound;
        [SerializeField] private AudioClip floorHitSound;
        [SerializeField] private AudioClip netHitSound;

        [Header("Audio: Game State")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] private AudioClip whistleSound;
        [SerializeField] private AudioClip playerScoreSound;
        [SerializeField] private AudioClip aiScoreSound;
        [SerializeField] private AudioClip playerCheerSound;
        [SerializeField] private AudioClip aiCheerSound;
        [SerializeField] private AudioClip matchWinSound;
        [SerializeField] private AudioClip matchLoseSound;
        [SerializeField] private AudioClip levelUpSound;
        [SerializeField] private AudioClip levelDownSound;

        [Header("VFX: Particles")]
        [SerializeField] private ParticleSystem playerHitVFXPrefab;
        [SerializeField] private ParticleSystem aiHitVFXPrefab;
        [SerializeField] private ParticleSystem floorDustVFXPrefab;
        [SerializeField] private ParticleSystem pointScoredVFXPrefab;
        
        private AudioSource uiAudioSource;
        private AudioSource bgmAudioSource;

        private void Awake()
        {
            if (Instance == null) { Instance = this; }
            else { Destroy(gameObject); return; }

            uiAudioSource = gameObject.AddComponent<AudioSource>();
            uiAudioSource.spatialBlend = 0f; // 2D sound so UI events are heard clearly everywhere
            uiAudioSource.playOnAwake = false;

            if (backgroundMusic != null)
            {
                bgmAudioSource = gameObject.AddComponent<AudioSource>();
                bgmAudioSource.spatialBlend = 0f; // 2D background music
                bgmAudioSource.loop = true;
                bgmAudioSource.clip = backgroundMusic;
                bgmAudioSource.Play();
            }
        }
        
        // ─── BALL IMPACTS (3D AUDIO & VFX) ───────────────────────────────────

        public void PlayPlayerHit(Vector3 position)
        {
            if (playerHitSound != null) PlayClipAtPointVR(playerHitSound, position);
            else PlayProceduralBeep(position, 440f, 0.1f); // Fallback beep
            
            if (playerHitVFXPrefab != null) Instantiate(playerHitVFXPrefab, position, Quaternion.identity);
        }

        public void PlayAIHit(Vector3 position)
        {
            if (aiHitSound != null) PlayClipAtPointVR(aiHitSound, position);
            else PlayProceduralBeep(position, 330f, 0.15f); // Fallback beep
            
            if (aiHitVFXPrefab != null) Instantiate(aiHitVFXPrefab, position, Quaternion.identity);
        }
        
        private void PlayProceduralBeep(Vector3 position, float frequency, float duration)
        {
            int sampleRate = 44100;
            AudioClip clip = AudioClip.Create("Beep", (int)(sampleRate * duration), 1, sampleRate, false);
            float[] samples = new float[clip.samples];
            for (int i = 0; i < samples.Length; i++)
            {
                // Sine wave
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / sampleRate) * 0.5f;
            }
            clip.SetData(samples, 0);
            PlayClipAtPointVR(clip, position);
        }

        private void PlayClipAtPointVR(AudioClip clip, Vector3 pos, float volume = 1.0f)
        {
            // Custom implementation of PlayClipAtPoint with VR-friendly rolloff
            GameObject tempGO = new GameObject("TempAudio");
            tempGO.transform.position = pos;
            AudioSource aSource = tempGO.AddComponent<AudioSource>();
            aSource.clip = clip;
            aSource.spatialBlend = 1.0f; // 3D sound
            
            // Make the sound audible across the entire court!
            aSource.minDistance = 8.0f; 
            aSource.maxDistance = 50.0f;
            aSource.rolloffMode = AudioRolloffMode.Linear; 
            aSource.volume = volume;
            
            aSource.Play();
            Destroy(tempGO, clip.length);
        }

        public void PlayFloorHit(Vector3 position)
        {
            if (floorHitSound != null) PlayClipAtPointVR(floorHitSound, position);
            if (floorDustVFXPrefab != null) Instantiate(floorDustVFXPrefab, position, Quaternion.identity);
        }

        public void PlayNetHit(Vector3 position)
        {
            if (netHitSound != null) PlayClipAtPointVR(netHitSound, position);
        }

        // ─── GAME STATE (2D AUDIO & VFX) ─────────────────────────────────────

        public void PlayWhistle() => PlayUI(whistleSound);
        
        public void PlayPlayerScored(Vector3 netPosition)
        {
            PlayUI(playerScoreSound);
            PlayUI(playerCheerSound);
            if (pointScoredVFXPrefab != null) 
            {
                // Spawn confetti slightly above the net
                Instantiate(pointScoredVFXPrefab, netPosition + Vector3.up * 2f, Quaternion.identity);
            }
        }
        
        public void PlayAIScored()
        {
            PlayUI(aiScoreSound);
            PlayUI(aiCheerSound);
        }
        
        public void PlayMatchWin() => PlayUI(matchWinSound);
        public void PlayMatchLose() => PlayUI(matchLoseSound);
        public void PlayLevelUp() => PlayUI(levelUpSound);
        public void PlayLevelDown() => PlayUI(levelDownSound);

        private void PlayUI(AudioClip clip)
        {
            if (clip != null && uiAudioSource != null)
            {
                uiAudioSource.PlayOneShot(clip);
            }
        }
    }
}
