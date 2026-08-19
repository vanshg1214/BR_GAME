using UnityEngine;
using PopstrikeVR.Gameplay;

namespace PopstrikeVR.Core
{
    /// <summary>
    /// Central hub for all audiovisual feedback in PopstrikeVR.
    /// Manages zero-garbage pooling for VFX particles and plays categorized SFX.
    /// Also listens to ComboManager to layer adaptive music or play streak sounds.
    /// </summary>
    public class PopstrikeFeedbackManager : MonoBehaviour
    {
        public static PopstrikeFeedbackManager Instance { get; private set; }

        [Header("Audio Sources")]
        [Tooltip("The main 2D AudioSource for UI and global SFX.")]
        public AudioSource GlobalSFXSource;
        [Tooltip("The AudioSource for adaptive background music.")]
        public AudioSource MusicSource;

        [Header("Background Music")]
        [Tooltip("The audio clip to play as background music.")]
        public AudioClip BackgroundMusicClip;
        [Tooltip("The volume of the background music (0 to 1).")]
        [Range(0f, 1f)] public float BackgroundMusicVolume = 0.5f;

        [Header("Balloon SFX")]
        public AudioClip BlazeThudClip;
        public AudioClip BladeHumClip;
        public AudioClip ErrorToneClip;

        [Header("Trace (Green Tube) SFX")]
        [Tooltip("Single sound played on every correct trace connection.")]
        public AudioClip TraceChimeClip;
        [Tooltip("Played when the entire Trace path is successfully completed.")]
        public AudioClip TraceFlourishClip;

        [Header("Trail TMT-A Scale (White Tube)")]
        [Tooltip("Ascending notes for TMT-A (e.g. Piano).")]
        public AudioClip[] TMTA_AscendingNotes;
        [Tooltip("Plays when the TMT-A path is fully completed.")]
        public AudioClip TMTA_SuccessFlourish;

        [Header("Trail TMT-B Scale (Cognitive Shift)")]
        [Tooltip("Ascending notes for TMT-B (e.g. Electric Piano or Synth).")]
        public AudioClip[] TMTB_AscendingNotes;
        [Tooltip("Plays when the TMT-B path is fully completed.")]
        public AudioClip TMTB_SuccessFlourish;

        [Header("Motivation SFX (Combo Streaks)")]
        public AudioClip GreatMovementClip;
        public AudioClip KeepGoingClip;
        public AudioClip DoingGreatClip;
        public AudioClip UnstoppableClip;
        public AudioClip FlawlessClip;

        [Header("Screen Effect Settings")]
        [Tooltip("The color of the screen border when an error occurs.")]
        public Color ErrorVignetteColor = Color.red;
        [Tooltip("How thick/intense the error border is (0 to 1). Increased to 0.85 for VR visibility.")]
        [Range(0f, 1f)] public float ErrorVignetteIntensity = 0.85f;
        
        [Tooltip("The intensity of the double-pulse border flash when balloons spawn.")]
        [Range(0f, 1f)] public float SpawnVignetteIntensity = 0.6f;

        [Header("Spawn Warning Colors")]
        public Color BlazeSpawnColor = new Color(1f, 0.5f, 0f, 1f);
        public Color BladeSpawnColor = new Color(0f, 0.5f, 1f, 1f);
        public Color TraceSpawnColor = new Color(0f, 1f, 0f, 1f);
        public Color TrailSpawnColor = new Color(1f, 1f, 1f, 1f);

        [Header("Pop Flash Colors")]
        public Color BlazePopColor = new Color(1f, 0.4f, 0f, 1f);
        public Color BladePopColor = new Color(0.5f, 0.8f, 1f, 0.3f);
        public Color TracePopColor = new Color(0.5f, 1f, 0.5f, 0.3f);
        public Color TrailPopColor = new Color(1f, 1f, 1f, 0.5f);

        [Header("VFX Prefabs (Requires ObjectPooler)")]
        public GameObject BlazePopVFX;
        public GameObject BladeLightningVFX;
        public GameObject TraceLeafVFX;
        public GameObject TrailConfettiVFX;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            if (GlobalSFXSource == null)
            {
                GlobalSFXSource = gameObject.AddComponent<AudioSource>();
                GlobalSFXSource.playOnAwake = false;
            }

            if (MusicSource == null)
            {
                MusicSource = gameObject.AddComponent<AudioSource>();
                MusicSource.playOnAwake = false;
                MusicSource.loop = true;
            }
        }

        private void Start()
        {
            if (BackgroundMusicClip != null && MusicSource != null)
            {
                MusicSource.clip = BackgroundMusicClip;
                MusicSource.volume = BackgroundMusicVolume;
                MusicSource.loop = true;
                MusicSource.Play();
            }
            if (ComboManager.Instance != null)
            {
                ComboManager.Instance.OnComboChanged += HandleComboChanged;
                ComboManager.Instance.OnStreakEvent += PlayStreakFeedback;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Allows tweaking the music volume slider in real-time during Play Mode in the Editor
            if (MusicSource != null && Application.isPlaying)
            {
                MusicSource.volume = BackgroundMusicVolume;
            }
        }
#endif

        private void OnDestroy()
        {
            if (ComboManager.Instance != null)
            {
                ComboManager.Instance.OnComboChanged -= HandleComboChanged;
                ComboManager.Instance.OnStreakEvent -= PlayStreakFeedback;
            }
        }

        #region Play SFX Methods

        public void PlayBlazeThud() => PlayGlobalClip(BlazeThudClip);
        public void PlayBladeHum() => PlayGlobalClip(BladeHumClip);
        public void PlayTraceChime() => PlayGlobalClip(TraceChimeClip);
        public void PlayTraceFlourish() => PlayGlobalClip(TraceFlourishClip);
        
        // Legacy fallback for ROM Calibration
        public void PlayTrailFlourish() => PlayGlobalClip(TMTA_SuccessFlourish);
        
        public void PlayErrorTone() 
        {
            PlayGlobalClip(ErrorToneClip);
        }

        public void PlayTMTAScaleNote(int sequenceIndex, bool isFinalNote)
        {
            if (isFinalNote && TMTA_SuccessFlourish != null)
            {
                PlayGlobalClip(TMTA_SuccessFlourish);
                return;
            }

            if (TMTA_AscendingNotes != null && TMTA_AscendingNotes.Length > 0)
            {
                AudioClip clipToPlay = TMTA_AscendingNotes[sequenceIndex % TMTA_AscendingNotes.Length];
                PlayGlobalClip(clipToPlay);
            }
        }

        public void PlayTMTBScaleNote(int sequenceIndex, bool isFinalNote)
        {
            if (isFinalNote && TMTB_SuccessFlourish != null)
            {
                PlayGlobalClip(TMTB_SuccessFlourish);
                return;
            }

            if (TMTB_AscendingNotes != null && TMTB_AscendingNotes.Length > 0)
            {
                AudioClip clipToPlay = TMTB_AscendingNotes[sequenceIndex % TMTB_AscendingNotes.Length];
                PlayGlobalClip(clipToPlay);
            }
        }

        private void PlayGlobalClip(AudioClip clip)
        {
            if (clip != null && GlobalSFXSource != null)
            {
                GlobalSFXSource.PlayOneShot(clip);
            }
        }

        #endregion

        #region Play VFX Methods

        public void PlayBlazeVFX(Vector3 position) 
        {
            SpawnVFX("VFX_BlazePop", BlazePopVFX, position);
            // Disabled screen edges warm orange flash per UX feedback
            // PopstrikeVR.Visuals.ScreenEffectsController.Instance?.TriggerEdgeFlash(BlazePopColor, 0.4f, 0.4f);
        }

        public void PlayBladeVFX(Vector3 position, Quaternion rotation = default, float scaleMultiplier = 1f)
        {
            if (rotation == default) rotation = Quaternion.identity;
            
            SpawnVFX("VFX_BladeLightning", BladeLightningVFX, position, rotation, scaleMultiplier);
            // PopstrikeVR.Visuals.ScreenEffectsController.Instance?.FlashScreen(BladePopColor);
        }

        public void PlayTraceVFX(Vector3 position)
        {
            SpawnVFX("VFX_TraceLeaves", TraceLeafVFX, position);
            // PopstrikeVR.Visuals.ScreenEffectsController.Instance?.FlashScreen(TracePopColor);
        }

        public void PlayTrailVFX(Vector3 position)
        {
            SpawnVFX("VFX_TrailConfetti", TrailConfettiVFX, position);
            // PopstrikeVR.Visuals.ScreenEffectsController.Instance?.FlashScreen(TrailPopColor);
        }

        private void SpawnVFX(string poolKey, GameObject prefab, Vector3 position, Quaternion rotation = default, float scaleMultiplier = 1f)
        {
            if (prefab == null) return;
            if (rotation == default) rotation = Quaternion.identity;
            
            // Use Object Pooler to completely eliminate instantiation lag spikes!
            bool usePooler = true; 

            GameObject vfxInstance = null;

            if (usePooler)
            {
                // Reusing PopstrikePooler to avoid instantiation lag spikes
                vfxInstance = PopstrikePooler.SpawnBalloon(poolKey, position, rotation);
                
                if (vfxInstance != null)
                {
                    if (scaleMultiplier != 1f)
                        vfxInstance.transform.localScale = Vector3.one * scaleMultiplier;

                    Debug.Log($"<color=magenta>[FeedbackManager] Pooled VFX: {prefab.name} at {position}. Scale: {vfxInstance.transform.localScale}</color>");
                    
                    // Guarantee the particle system restarts when pulled from the pool
                    var pSystems = vfxInstance.GetComponentsInChildren<ParticleSystem>();
                    foreach (var ps in pSystems)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        ps.Play(true);
                    }

                    PopstrikePooler.DespawnBalloon(vfxInstance, 3f);
                    return; // Exit here if pooler succeeded
                }
            }

            // DYNAMIC INSTANTIATION PATH (Currently Active)
            vfxInstance = Instantiate(prefab, position, rotation);
            
            if (scaleMultiplier != 1f)
                vfxInstance.transform.localScale = Vector3.one * scaleMultiplier;

            Debug.Log($"<color=cyan>[FeedbackManager] Instantiated Dynamic VFX: {prefab.name} at {position}. Scale: {vfxInstance.transform.localScale}</color>");
            
            // Guarantee the particle system plays, even if 'Play On Awake' is accidentally turned off in the prefab!
            var instPSystems = vfxInstance.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in instPSystems)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }

            Destroy(vfxInstance, 3f);
        }

        #endregion

        #region Adaptive Audio & Streaks

        public void PlayStreakFeedback(string message)
        {
            if (message.Contains("Great Movement"))
            {
                PlayGlobalClip(GreatMovementClip);
            }
            else if (message.Contains("Keep Going"))
            {
                PlayGlobalClip(KeepGoingClip);
            }
            else if (message.Contains("Doing Great"))
            {
                PlayGlobalClip(DoingGreatClip);
            }
            else if (message.Contains("Unstoppable"))
            {
                PlayGlobalClip(UnstoppableClip);
            }
            else if (message.Contains("Flawless"))
            {
                PlayGlobalClip(FlawlessClip);
            }
        }

        private void HandleComboChanged(int currentCombo, float multiplier, int basePoints)
        {
            if (currentCombo == 0)
            {
                // Combo broken (Miss or Error)
                // Disabled desaturation pulse per UX feedback
                // PopstrikeVR.Visuals.ScreenEffectsController.Instance?.DesaturateMiss();
            }
            // Optional: Layer music tracks based on the multiplier
        }

        #endregion
    }
}
