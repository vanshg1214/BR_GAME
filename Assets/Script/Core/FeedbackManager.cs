using UnityEngine;

namespace WhackAMole
{
    public class FeedbackManager : MonoBehaviour
    {
        public static FeedbackManager Instance { get; private set; }

        [Header("Audio Clips")]
        [SerializeField] private AudioSource sfxSource;

        [Header("UI Prefabs & Spawning")]
        [SerializeField] private GameObject hitTextPrefab;
        [SerializeField] private GameObject encouragementTextPrefab;
        [SerializeField] private Transform encouragementSpawnPoint;

        [Header("Mole Sounds & VFX")]
        [SerializeField] private GameObject squirrelEmergenceVFX;
        [SerializeField] private AudioClip popupSound;

        [Header("Motivation Sounds")]
        [SerializeField] private AudioClip greatReachSound;
        [SerializeField] private AudioClip hatTrickSound;
        [SerializeField] private AudioClip keepGoingSound;
        [SerializeField] private AudioClip unstoppableSound;

        private float verbalCooldown = 12f;
        private float lastEncouragementTime = -999f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.spatialBlend = 0f;
                sfxSource.playOnAwake = false;
            }
            if (ScoreManager.Instance != null) ScoreManager.Instance.OnComboChanged += HandleComboChanged;
        }

        private void OnDestroy()
        {
            if (ScoreManager.Instance != null) ScoreManager.Instance.OnComboChanged -= HandleComboChanged;
        }

        public void PlayStandardHit(Vector3 position, int holeIndex = -1) => CheckGreatReach(position, holeIndex);
        public void PlayHeavyHit(Vector3 position, int holeIndex = -1) => CheckGreatReach(position, holeIndex);
        public void PlayFakeHit(Vector3 position, int holeIndex = -1) { }

        public void PlayGroundPopup(Vector3 position)
        {
            if (popupSound != null) AudioSource.PlayClipAtPoint(popupSound, position);
        }

        public void PlayMoleSpawn(Vector3 position)
        {
            if (squirrelEmergenceVFX != null)
            {
                GameObject instance = ObjectPooler.Instance.SpawnOrAddPool("VFX_SquirrelEmergence", squirrelEmergenceVFX, 5, position, Quaternion.Euler(-90f, 0f, 0f));
                if (instance != null)
                {
                    instance.GetComponent<ParticleSystem>()?.Play(true);
                    ObjectPooler.Instance.ReturnToPool(instance, 1.5f);
                }
            }
        }

        private void PlayClip(AudioClip clip)
        {
            if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
        }

        private void SpawnParticles(ParticleSystem prefab, Vector3 position)
        {
            if (prefab == null) return;
            GameObject instance = ObjectPooler.Instance.SpawnOrAddPool("VFX_" + prefab.name, prefab.gameObject, 5, position, Quaternion.identity);
            if (instance == null) return;
            
            instance.GetComponent<ParticleSystem>()?.Play(true);
            ObjectPooler.Instance.ReturnToPool(instance, 3f);
        }

        public void SpawnFloatingText(Vector3 position, string textMessage)
        {
            if (hitTextPrefab == null) return;
            Vector3 spawnPos = position + Vector3.up * 0.2f;
            GameObject instance = ObjectPooler.Instance.SpawnOrAddPool("VFX_HitText", hitTextPrefab, 5, spawnPos, Quaternion.identity);
            if (instance != null) instance.GetComponent<WhackAMole.UI.FloatingText>()?.Initialize(textMessage);
        }

        public void SpawnEncouragementText(string textMessage)
        {
            if (encouragementTextPrefab == null) return;
            Vector3 spawnPos = encouragementSpawnPoint != null ? encouragementSpawnPoint.position : encouragementTextPrefab.transform.position;
            Quaternion spawnRot = encouragementSpawnPoint != null ? encouragementSpawnPoint.rotation : encouragementTextPrefab.transform.rotation;

            GameObject instance = ObjectPooler.Instance.SpawnOrAddPool("VFX_EncourageText", encouragementTextPrefab, 3, spawnPos, spawnRot);
            if (instance != null) instance.GetComponent<WhackAMole.UI.FloatingText>()?.Initialize(textMessage);
        }

        private void CheckGreatReach(Vector3 position, int holeIndex)
        {
            if (holeIndex < 0 || Time.time - lastEncouragementTime < verbalCooldown) return;
            
            if (ScoreManager.Instance != null)
            {
                int combo = ScoreManager.Instance.CurrentCombo;
                if (combo == 3 || combo == 6 || combo == 10) return; 
            }

            HoleLayoutGenerator layout = FindObjectOfType<HoleLayoutGenerator>();
            if (layout != null && layout.Columns > 0)
            {
                if (holeIndex / layout.Columns >= 2) 
                {
                    lastEncouragementTime = Time.time;
                    SpawnEncouragementText("Great Reach!");
                    if (greatReachSound != null) PlayClip(greatReachSound);
                }
            }
        }

        private void HandleComboChanged(int combo, float multiplier)
        {
            if (combo == 0) return;
            string comboMsg = "";
            AudioClip clipToPlay = null;

            switch (combo)
            {
                case 3: comboMsg = "Hat Trick!"; clipToPlay = hatTrickSound; break;
                case 6: comboMsg = "Keep Going!"; clipToPlay = keepGoingSound; break;
                case 10: comboMsg = "Unstoppable!"; clipToPlay = unstoppableSound; break;
            }

            if (!string.IsNullOrEmpty(comboMsg))
            {
                lastEncouragementTime = Time.time;
                SpawnEncouragementText(comboMsg);
                if (clipToPlay != null) PlayClip(clipToPlay);
            }
        }
    }
}
