using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using EpicRapBattle.Config;
using UnityEngine;
using UnityEngine.Networking;

namespace EpicRapBattle.Managers
{
    public class RapBattleConductor : MonoBehaviour
    {
        [Header("Audio Sources")]
        [Tooltip("Audio source for background music.")]
        [SerializeField]
        private AudioSource musicSource;

        [SerializeField]
        private const float bpm = 90f;

        [SerializeField]
        [Tooltip("Audio source for NPC responses.")]
        private AudioSource npcAudioSource;

        [SerializeField]
        private UIManager uiManager;

        [Header("AI Integration")]
        [SerializeField]
        private AIConfig aiConfig;

        [Header("Game Configuration")]
        [Tooltip("Number of bars to wait at the start of the game.")]
        [SerializeField]
        private const int startOfGameRestLenInBars = 2;

        [Tooltip("Number of bars per turn for the player.")]
        [SerializeField]
        private const int countDownLenInBars = 2;

        [Tooltip("Number of bars for the rest turn.")]
        [SerializeField]
        private const int restTurnLenInBars = 2;

        [Tooltip("Number of bars for the player turn.")]
        [SerializeField]
        private const int playerTurnBars = 4;

        [Header("Debugging")]
        [Tooltip("Play back microphone recording after player turn.")]
        [SerializeField]
        private bool playBackRecording = false;
        private float secondsPerBeat;
        private float secondsPerBar;
        private BattleState currentState = BattleState.WaitingStart;
        private AudioClip playerClip;
        private string microphoneDevice;
        private bool isRecording = false;
        private const int sampleRate = 16000;
        private List<Message> messages = new List<Message>();

        public BattleState CurrentState => currentState;

        private string npcResponseText;
        private string npcResponseAudio;

        private void Start()
        {
            uiManager.clearText();
            AddSystemMessage(aiConfig.RapPersonality);
            secondsPerBeat = 60f / bpm;
            secondsPerBar = secondsPerBeat * 4f;
            InitializeMicrophone();
            StartCoroutine(BattleLoop());
        }

        private void InitializeMicrophone()
        {
            microphoneDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
            if (microphoneDevice == null)
            {
                Debug.LogError("No microphone detected! Please connect a microphone.");
                throw new InvalidOperationException(
                    "No microphone detected! Please connect a microphone."
                );
            }
            if (musicSource != null)
                musicSource.Play();
        }

        private IEnumerator BattleLoop()
        {
            uiManager.UpdateStatus("Get ready to rap!");
            yield return StartCoroutine(WaitBars(startOfGameRestLenInBars));
            // Wait for the player to start the battle

            while (true)
            {
                // Player Countdown
                currentState = BattleState.PlayerCountdown;
                yield return StartCoroutine(PlayerCountdown(countDownLenInBars));

                // Player Turn
                currentState = BattleState.PlayerTurn;
                yield return StartCoroutine(PlayerTurnWithRecording(playerTurnBars));

                // Waiting Turn
                currentState = BattleState.WaitingTurn;
                uiManager.UpdateStatus("Your Opponent is thinking...");
                yield return StartCoroutine(ProcessSystemResponseTurn());

                // NPC Turn
                currentState = BattleState.NPCTurn;
                uiManager.UpdateStatus("Opponents turn!");
                yield return StartCoroutine(PlayNpcResponse());

                // Rest Turn
                currentState = BattleState.Rest;
                uiManager.UpdateStatus("Rest...");
                yield return StartCoroutine(WaitBars(restTurnLenInBars));
            }
        }

        private IEnumerator WaitBars(int barCount)
        {
            float totalTime = secondsPerBar * barCount;
            float elapsed = 0f;
            while (elapsed < totalTime)
            {
                yield return new WaitForSeconds(secondsPerBeat);
                elapsed += secondsPerBeat;
            }
        }

        private IEnumerator PlayerCountdown(int bars)
        {
            int beats = bars * 4;
            for (int i = beats; i > 0; i--)
            {
                uiManager.UpdateStatus($"Your turn in: {i} beats");
                yield return new WaitForSeconds(secondsPerBeat);
            }
        }

        private IEnumerator PlayerTurnWithRecording(int bars)
        {
            int totalBeats = bars * 4;
            StartMicrophoneRecording();
            for (int i = totalBeats; i > 0; i--)
            {
                uiManager.UpdateStatus($"Your turn! Time left: {i} beats");
                yield return new WaitForSeconds(secondsPerBeat);
            }
            StopMicrophoneRecording();
            uiManager.UpdateStatus("Recording stopped.");
        }

        private void StartMicrophoneRecording()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("No microphone detected!");
                return;
            }
            playerClip = Microphone.Start(
                microphoneDevice,
                false,
                Mathf.CeilToInt(playerTurnBars * secondsPerBar),
                sampleRate
            );
            isRecording = true;
            // Debug.Log("Microphone recording started.");
        }

        private void StopMicrophoneRecording()
        {
            if (!isRecording)
                return;
            Microphone.End(microphoneDevice);
            isRecording = false;
            // Debug.Log("Microphone recording stopped.");
        }

        private IEnumerator ProcessSystemResponseTurn()
        {
            yield return StartCoroutine(SendToOpenAI());
        }

        private IEnumerator SendToOpenAI()
        {
            if (playBackRecording)
            {
                yield return StartCoroutine(PlayBackRecording());
            }
            submitUserAudioMessage();

            var payload = new OpenAIPayload
            {
                model = aiConfig.GptModelString,
                messages = messages,
                modalities = new[] { "text", "audio" },
                audio = new OpenAIAudio { voice = aiConfig.TtsVoiceString, format = "wav" },
            };
            // Debug.Log($"Sending OpenAI payload: {payload}");
            string json = JsonUtility.ToJson(payload);
            json = System.Text.RegularExpressions.Regex.Replace(json, ",?\"\\w+\":\"\"", "");
            json = System.Text.RegularExpressions.Regex.Replace(json, ",(?=\\s*[}\\]])", "");

            // Debug.Log($"Sending to OpenAI: {json}");

            using (
                UnityWebRequest req = new UnityWebRequest(
                    "https://api.openai.com/v1/chat/completions",
                    "POST"
                )
            )
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {aiConfig.ApiKey}");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"OpenAI API error: {req.error} \n{req.downloadHandler.text} ");
                    yield break;
                }
                ChatCompletionResponse response = JsonUtility.FromJson<ChatCompletionResponse>(
                    req.downloadHandler.text
                );
                // Debug.Log($"OpenAI response: {req.downloadHandler.text}");

                npcResponseText = "";
                npcResponseAudio = "";
                if (response != null && response.choices != null && response.choices.Count > 0)
                {
                    npcResponseText = response.choices[0].message.audio.transcript;
                    if (response.choices[0].message.audio != null)
                    {
                        npcResponseAudio = response.choices[0].message.audio.data;
                    }
                }
                AddAssistantAudioMessage(response?.choices?[0]?.message?.audio?.id);
                // Debug.Log($"NPC response text: {npcResponseText}");
                // Debug.Log($"NPC response audio: {npcResponseAudio}");
            }
        }

        private void submitUserAudioMessage()
        {
            var playerRecordingBase64 = playerClip
                ? Convert.ToBase64String(WavUtility.FromAudioClip(playerClip))
                : null;

            if (string.IsNullOrEmpty(playerRecordingBase64))
            {
                Debug.LogError("No audio recording found!");
            }
            // Add current user message
            AddUserAudioMessage(playerRecordingBase64);
        }

        private IEnumerator PlayBackRecording()
        {
            uiManager.UpdateStatus("Playing back your recording...");
            if (playerClip != null)
            {
                musicSource.Pause();
                AudioSource playbackSource = gameObject.AddComponent<AudioSource>();
                playbackSource.clip = playerClip;
                playbackSource.Play();
                // Debug.Log("CLip length: " + playerClip.length);
                yield return new WaitForSeconds(playerClip.length);
                Destroy(playbackSource);
                musicSource.UnPause();
            }
            yield break;
        }

        private IEnumerator PlayNpcResponse()
        {
            if (!string.IsNullOrEmpty(npcResponseAudio))
            {
                byte[] audioBytes = Convert.FromBase64String(npcResponseAudio);
                AudioClip clip = WavUtility.AudioClipFromCorruptWav(audioBytes);
                npcAudioSource.clip = clip;
                uiManager.UpdateComputerText(npcResponseText);
                float timeToNextBar = secondsPerBar - (musicSource.time % secondsPerBar);
                if (timeToNextBar > 0.05f)
                {
                    // Debug.Log($"Waiting {timeToNextBar:F2}s to sync NPC response to the beat.");
                    yield return new WaitForSeconds(timeToNextBar);
                }
                npcAudioSource.Play();
                yield return new WaitForSeconds(clip.length);
            }
            else
            {
                Debug.LogWarning("No audio response from NPC.");
                yield return StartCoroutine(WaitBars(countDownLenInBars));
            }
        }

        // --- Message Helper Methods ---
        private void AddSystemMessage(string textMessage)
        {
            messages.Add(
                new Message
                {
                    role = "system",
                    content = new MessageContent[]
                    {
                        new MessageContent { text = textMessage, type = "text" },
                    },
                }
            );
        }

        private void AddUserAudioMessage(string base64Audio)
        {
            messages.Add(
                new Message
                {
                    role = "user",
                    content = new MessageContent[]
                    {
                        new MessageContent
                        {
                            type = "input_audio",
                            input_audio = new InputAudio { data = base64Audio, format = "wav" },
                        },
                    },
                }
            );
        }

        private void AddAssistantAudioMessage(string audioId)
        {
            messages.Add(
                new Message
                {
                    role = "assistant",
                    audio = new AssistantResponseAudio { id = audioId },
                }
            );
        }
    }

    [Serializable]
    public class Message
    {
        public string role = null;
        public MessageContent[] content = null;
        public AssistantResponseAudio audio = null;
    }

    [Serializable]
    public class AssistantResponseAudio
    {
        public string id = null;
    }

    [Serializable]
    public class MessageContent
    {
        public string type = null;
        public string text = null;
        public InputAudio input_audio = null;
    }

    [Serializable]
    public class InputAudio
    {
        public string data = null;
        public string format = null;
    }

    [Serializable]
    public class ChatCompletionResponse
    {
        public string id = null;
        public List<ChatCompletionChoice> choices = null;
    }

    [Serializable]
    public class ChatCompletionChoice
    {
        public int index = 0;
        public ChatCompletionMessage message = null;
        public string finish_reason = null;
    }

    [Serializable]
    public class ChatCompletionMessage
    {
        public string role = null;
        public string refusal = null;
        public ChatCompletionAudio audio = null;
    }

    [Serializable]
    public class ChatCompletionAudio
    {
        public string data = null;
        public int expires_at = 0;
        public string id = null;
        public string transcript = null;
    }

    [Serializable]
    public class OpenAIPayload
    {
        public string model = null;
        public List<Message> messages = null;
        public string[] modalities = null;
        public OpenAIAudio audio = null;
    }

    [Serializable]
    public class OpenAIAudio
    {
        public string voice = null;
        public string format = null;
    }

    public enum BattleState
    {
        WaitingStart,
        PlayerCountdown,
        PlayerTurn,
        WaitingTurn,
        NPCTurn,
        Rest,
    }
}
