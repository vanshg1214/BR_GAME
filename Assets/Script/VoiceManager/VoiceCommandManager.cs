using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Voice;
using UnityEngine;

namespace WhackAMole
{
    public class VoiceCommandManager : MonoBehaviour {
        public static VoiceCommandManager Instance;

        [SerializeField] private AppVoiceExperience voiceExperience;

        private readonly Dictionary<string, Action> commands = new();

        private bool isListening;

        private void Awake() {
            if (Instance != null) {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            DontDestroyOnLoad(gameObject);
        }

        private void Start() {
            if (voiceExperience == null) {
                Debug.LogError("Voice Experience is NULL");
                return;
            }

            voiceExperience.VoiceEvents.OnFullTranscription
                .AddListener(OnTranscription);

            voiceExperience.VoiceEvents.OnStartListening
                .AddListener(() => {
                    if (isListening)
                        return;

                    isListening = true;

                    Debug.Log("VOICE STARTED LISTENING");
                });

            voiceExperience.VoiceEvents.OnStoppedListening
                .AddListener(() => {
                    if (!isListening)
                        return;

                    isListening = false;

                    Debug.Log("VOICE STOPPED LISTENING");
                });

            voiceExperience.VoiceEvents.OnError
                .AddListener((error, message) => {
                    Debug.LogError($"VOICE ERROR: {message}");
                });
        }

        public void StartListening() {
            if (isListening)
                return;

            voiceExperience.Activate();
        }

        public void StopListening() {
            if (!isListening)
                return;

            voiceExperience.Deactivate();
        }

        public void RegisterCommand(string command, Action action) {
            command = CleanText(command);

            commands[command] = action;
        }

        public void UnregisterCommand(string command) {
            command = CleanText(command);

            if (commands.ContainsKey(command)) {
                commands.Remove(command);
            }
        }

        private void OnTranscription(string text) {
            string cleanedText = CleanText(text);

            Debug.Log($"VOICE: {cleanedText}");

            foreach (var pair in commands) {
                if (cleanedText.Contains(pair.Key)) {
                    pair.Value.Invoke();
                    break;
                }
            }
        }

        private string CleanText(string text) {
            text = text.ToLower().Trim();

            text = text.Replace(".", "");
            text = text.Replace(",", "");
            text = text.Replace("!", "");
            text = text.Replace("?", "");
            text = text.Replace("'", "");
            text = text.Replace("\"", "");

            return text;
        }

        public void ListenForDuration(float duration = 2f) {
            StartCoroutine(ListenRoutine(duration));
        }
        private IEnumerator ListenRoutine(float duration) {
            StartListening();

            yield return new WaitForSeconds(duration);

            StopListening();
        }
    }
}