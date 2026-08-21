using UnityEngine;
using ArcRoll.UI;

namespace ArcRoll.Core
{
    /// <summary>
    /// Handles showing arcade-style motivational text and playing audio 
    /// every few waves so it feels rewarding but not spammy.
    /// </summary>
    public class ArcRollMotivationalManager : MonoBehaviour
    {
        public static ArcRollMotivationalManager Instance { get; private set; }

        public enum SportType { Bowling, Basketball, Frisbee }

        [Header("UI & Audio Components")]
        [Tooltip("Drag the GameObject with ArcRollFeedbackTextAnimator here")]
        public ArcRollFeedbackTextAnimator textAnimator;
        [Tooltip("AudioSource to play the voice lines (e.g. on the player's head or a global manager)")]
        public AudioSource audioSource;

        [Header("Wave / Cooldown Settings")]
        [Tooltip("Minimum successful scores required before feedback can play again. Set to 1 for instant feedback on every hit.")]
        [SerializeField] private int minScoresBetweenFeedback = 3;
        [Tooltip("Maximum successful scores before feedback is guaranteed. Set to 1 for instant feedback on every hit.")]
        [SerializeField] private int maxScoresBetweenFeedback = 3;

        private int _scoreCounter = 0;
        private int _targetForNextFeedback = 0;

        [Header("🎳 Bowling Options")]
        public string bowlingPerfectText = "Strike!";
        public AudioClip bowlingStrikeClip;
        public string bowlingGoodText1 = "Great Power!";
        public AudioClip bowlingGreatPowerClip;
        public string bowlingGoodText2 = "Perfect Roll!";
        public AudioClip bowlingPerfectRollClip;

        [Header("🏀 Basketball Options")]
        public string hoopsPerfectText = "Swish!";
        public AudioClip hoopsSwishClip;
        public string hoopsGoodText1 = "Nothin' but net!";
        public AudioClip hoopsNothinButNetClip;
        public string hoopsGoodText2 = "Great Shot!";
        public AudioClip hoopsGreatShotClip;

        [Header("🥏 Frisbee Options")]
        public string frisbeePerfectText = "Bullseye!";
        public AudioClip frisbeeBullseyeClip;
        public string frisbeeGoodText1 = "Smooth!";
        public AudioClip frisbeeSmoothClip;
        public string frisbeeGoodText2 = "Great Aim!";
        public AudioClip frisbeeGreatAimClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ResetCounter();
        }

        private void ResetCounter()
        {
            _scoreCounter = 0;
            _targetForNextFeedback = Random.Range(minScoresBetweenFeedback, maxScoresBetweenFeedback + 1);
        }

        /// <summary>
        /// Called by the individual targets (BasketballHoop, BowlingPinFormation, FrisbeeFormation)
        /// when they successfully score.
        /// </summary>
        public void ReportScore(SportType sport, bool isPerfect = false)
        {
            _scoreCounter++;

            // Strict Cooldown Enforcement:
            // We NO LONGER bypass the cooldown for perfect shots. 
            // It will strictly wait for 3 successful hits before playing ANY motivation text/sound!
            if (_scoreCounter >= _targetForNextFeedback) 
            {
                TriggerFeedback(sport, isPerfect);
                ResetCounter();
            }
        }

        private AudioClip _lastPlayedClip = null;

        private void TriggerFeedback(SportType sport, bool isPerfect)
        {
            StartCoroutine(DelayedFeedback(sport, isPerfect, 1.0f));
        }

        private System.Collections.IEnumerator DelayedFeedback(SportType sport, bool isPerfect, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (textAnimator == null) yield break;

            string feedbackText = "";
            AudioClip feedbackClip = null;

            // Pool of possible responses for this sport
            System.Collections.Generic.List<string> textOptions = new System.Collections.Generic.List<string>();
            System.Collections.Generic.List<AudioClip> clipOptions = new System.Collections.Generic.List<AudioClip>();

            // Populate the pool based on the sport and performance (relaxation: perfect scores can use ANY voice line!)
            switch (sport)
            {
                case SportType.Bowling:
                    if (isPerfect) { textOptions.Add(bowlingPerfectText); clipOptions.Add(bowlingStrikeClip); }
                    textOptions.Add(bowlingGoodText1); clipOptions.Add(bowlingGreatPowerClip);
                    textOptions.Add(bowlingGoodText2); clipOptions.Add(bowlingPerfectRollClip);
                    break;

                case SportType.Basketball:
                    if (isPerfect) { textOptions.Add(hoopsPerfectText); clipOptions.Add(hoopsSwishClip); }
                    textOptions.Add(hoopsGoodText1); clipOptions.Add(hoopsNothinButNetClip);
                    textOptions.Add(hoopsGoodText2); clipOptions.Add(hoopsGreatShotClip);
                    break;

                case SportType.Frisbee:
                    if (isPerfect) { textOptions.Add(frisbeePerfectText); clipOptions.Add(frisbeeBullseyeClip); }
                    textOptions.Add(frisbeeGoodText1); clipOptions.Add(frisbeeSmoothClip);
                    textOptions.Add(frisbeeGoodText2); clipOptions.Add(frisbeeGreatAimClip);
                    break;
            }

            // Remove the last played clip from the pool to guarantee we never play the exact same audio twice in a row!
            if (clipOptions.Count > 1 && _lastPlayedClip != null)
            {
                int lastIndex = clipOptions.IndexOf(_lastPlayedClip);
                if (lastIndex != -1)
                {
                    clipOptions.RemoveAt(lastIndex);
                    textOptions.RemoveAt(lastIndex);
                }
            }

            // Pick a random line from the remaining fresh options
            if (clipOptions.Count > 0)
            {
                int randomIndex = Random.Range(0, clipOptions.Count);
                feedbackText = textOptions[randomIndex];
                feedbackClip = clipOptions[randomIndex];
            }

            // Play the chosen feedback!
            if (feedbackClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(feedbackClip);
                _lastPlayedClip = feedbackClip;
            }

            textAnimator.ShowFeedback(feedbackText);
        }
    }
}
