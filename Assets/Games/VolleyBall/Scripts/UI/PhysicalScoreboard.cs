using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Rehab.Volleyball.Core;

namespace Rehab.Volleyball.UI
{
    public class PhysicalScoreboard : MonoBehaviour
    {
        [Header("Dog Score Texts")]
        public TMP_Text dogTensText;
        public TMP_Text dogOnesText;

        [Header("Player Score Texts")]
        public TMP_Text playerTensText;
        public TMP_Text playerOnesText;

        [Header("Rally & Win Streak Texts")]
        public TMP_Text rallyText;
        public TMP_Text winStreakText;

        public float slideDuration = 0.3f;
        public float slideDistance = 300f;

        // Tracks the displayed value per text so we know when something truly changed
        private Dictionary<TMP_Text, string> displayedValues  = new Dictionary<TMP_Text, string>();
        private Dictionary<TMP_Text, Coroutine> activeCoroutines = new Dictionary<TMP_Text, Coroutine>();

        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            // Wait safely until the GameManager is fully loaded and ready
            while (VolleyballGameManager.Instance == null)
            {
                yield return null;
            }

            // Subscribe to the event safely
            VolleyballGameManager.Instance.OnScoreUpdated -= UpdateAllScores;
            VolleyballGameManager.Instance.OnScoreUpdated += UpdateAllScores;

            // Force-set all texts instantly (no animation) to match the current game state
            ForceRefreshAll();
        }

        private void OnEnable()
        {
            // Just in case it gets disabled and re-enabled later
            if (VolleyballGameManager.Instance != null)
            {
                VolleyballGameManager.Instance.OnScoreUpdated -= UpdateAllScores;
                VolleyballGameManager.Instance.OnScoreUpdated += UpdateAllScores;
                ForceRefreshAll();
            }
        }

        private void OnDisable()
        {
            if (VolleyballGameManager.Instance != null)
            {
                VolleyballGameManager.Instance.OnScoreUpdated -= UpdateAllScores;
            }
        }

        private void OnDestroy()
        {
            if (VolleyballGameManager.Instance != null)
            {
                VolleyballGameManager.Instance.OnScoreUpdated -= UpdateAllScores;
            }
        }

        private void UpdateAllScores()
        {
            if (VolleyballGameManager.Instance == null) return;

            int dog    = VolleyballGameManager.Instance.AIScore;
            int player = VolleyballGameManager.Instance.PlayerScore;
            int rally  = VolleyballGameManager.Instance.CurrentRallyCount;
            int streak = VolleyballGameManager.Instance.BestWinStreak;

            TryAnimate(dogTensText,    (dog    / 10).ToString());
            TryAnimate(dogOnesText,    (dog    % 10).ToString());
            TryAnimate(playerTensText, (player / 10).ToString());
            TryAnimate(playerOnesText, (player % 10).ToString());
            TryAnimate(rallyText,      rally.ToString("D2"));
            TryAnimate(winStreakText,  streak.ToString("D2"));
        }

        // Instantly writes all current scores to the text objects (no animation)
        private void ForceRefreshAll()
        {
            if (VolleyballGameManager.Instance == null) return;

            int dog    = VolleyballGameManager.Instance.AIScore;
            int player = VolleyballGameManager.Instance.PlayerScore;
            int rally  = VolleyballGameManager.Instance.CurrentRallyCount;
            int streak = VolleyballGameManager.Instance.BestWinStreak;

            ForceSet(dogTensText,    (dog    / 10).ToString());
            ForceSet(dogOnesText,    (dog    % 10).ToString());
            ForceSet(playerTensText, (player / 10).ToString());
            ForceSet(playerOnesText, (player % 10).ToString());
            ForceSet(rallyText,      rally.ToString("D2"));
            ForceSet(winStreakText,  streak.ToString("D2"));
        }

        private void ForceSet(TMP_Text t, string value)
        {
            if (t == null) return;

            // Clean up any orphaned clones from interrupted animations!
            foreach (Transform child in t.transform)
            {
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            t.text  = value;
            Color c = t.color; c.a = 1f; t.color = c;
            displayedValues[t] = value;
        }

        // ─────────────────────────────────────────────────────────────────────

        private void TryAnimate(TMP_Text t, string newValue)
        {
            if (t == null) return;

            // Nothing changed — skip
            string current = displayedValues.ContainsKey(t) ? displayedValues[t] : t.text;
            if (current == newValue) return;

            // Stop any in-progress animation on this text
            if (activeCoroutines.ContainsKey(t) && activeCoroutines[t] != null)
                StopCoroutine(activeCoroutines[t]);

            // Remove orphaned clones from a killed coroutine.
            // MUST un-parent them first so that Instantiate(original) below doesn't clone them!
            foreach (Transform child in t.transform)
            {
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            // Restore alpha in case a previous coroutine left it invisible
            Color c = t.color; c.a = 1f; t.color = c;

            string oldValue = current;
            displayedValues[t] = newValue;   // mark new target immediately

            activeCoroutines[t] = StartCoroutine(AnimateSlide(t, oldValue, newValue));
        }

        private IEnumerator AnimateSlide(TMP_Text original, string oldValue, string newValue)
        {
            // Read the fully-opaque color
            Color col = original.color;
            col.a = 1f;

            // Hide the real text object; clones do all the visible work
            original.color = new Color(col.r, col.g, col.b, 0f);

            // ── PHASE 1 : old number exits downward ─────────────────────────
            TMP_Text cloneOld = Instantiate(original, original.transform);
            cloneOld.text  = oldValue;
            cloneOld.color = col;
            cloneOld.transform.localPosition = Vector3.zero;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / slideDuration;
                float e = EaseOut(t);
                if (cloneOld) cloneOld.transform.localPosition = new Vector3(0f, Mathf.Lerp(0f, -slideDistance, e), 0f);
                if (cloneOld) cloneOld.color = new Color(col.r, col.g, col.b, Mathf.Lerp(1f, 0f, e));
                yield return null;
            }
            if (cloneOld) Destroy(cloneOld.gameObject);

            // ── PHASE 2 : new number enters from above ───────────────────────
            TMP_Text cloneNew = Instantiate(original, original.transform);
            cloneNew.text  = newValue;
            cloneNew.color = new Color(col.r, col.g, col.b, 0f);
            cloneNew.transform.localPosition = new Vector3(0f, slideDistance, 0f);

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / slideDuration;
                float e = EaseOut(t);
                if (cloneNew) cloneNew.transform.localPosition = new Vector3(0f, Mathf.Lerp(slideDistance, 0f, e), 0f);
                if (cloneNew) cloneNew.color = new Color(col.r, col.g, col.b, Mathf.Lerp(0f, 1f, e));
                yield return null;
            }
            if (cloneNew) Destroy(cloneNew.gameObject);

            // ── DONE : restore original text ────────────────────────────────
            original.text  = newValue;
            original.color = col;

            activeCoroutines.Remove(original);
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }
    }
}
