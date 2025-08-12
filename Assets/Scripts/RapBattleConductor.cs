using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using EpicRapBattle.Config;
using UnityEngine;
using UnityEngine.Networking;

namespace EpicRapBattle.Managers
{
    public class RapBattleConductor : MonoBehaviour
    {
        [Header("Animation Controller")]
        [Tooltip("Animation controller for the rapper.")]
        [SerializeField]
        private Animator animationController;

        [Header("Audio Sources")]
        [Tooltip("Audio source for background music.")]
        [SerializeField]
        private AudioSource musicSource;

        [SerializeField]
        private float bpm = 90f;

        [SerializeField]
        [Tooltip("Audio source for NPC responses.")]
        private AudioSource npcAudioSource;

        [SerializeField]
        private UIManager uiManager;

        [Header("AI Integration")]
        [SerializeField]
        private AIConfig aiConfig;

        [SerializeField]
        private MatchJudge matchJudge;

        [SerializeField]
        private int maxPlayerRecordingLengthInSeconds = 30;

        [Header("Game Configuration")]
        [Tooltip("Total Rap Rounds")]
        [SerializeField]
        private int totalRounds = 1;

        [Tooltip("Number of bars to wait at the start of the game.")]
        [SerializeField]
        private int startOfGameRestLenInBars = 2;

        [Tooltip("Number of bars per turn for the player.")]
        [SerializeField]
        private int countDownLenInBars = 2;

        [Tooltip("Number of bars for the rest turn.")]
        [SerializeField]
        private int restTurnLenInBars = 2;

        // [Tooltip("Number of bars for the player turn.")]
        // [SerializeField]
        // private int playerTurnBars = 4;

        [Header("Debugging")]
        [Tooltip("Play back microphone recording after player turn.")]
        [SerializeField]
        private bool playBackRecording = false;

        [Tooltip("Prompt for NPC speaking style.")]
        [SerializeField]
        private string npcSpeakingPrompt =
            " Say it all like a rapper. Very Fast and with a flow. Use the instructions that stars with [] to guide your response.";

        [Header("UI Controllers")]
        [SerializeField]
        private UIMenuController uIMenuController;
        private int currentRound = 0;
        private int submitRapPeriodInSeconds = 5;
        private float secondsPerBeat;
        private float secondsPerBar;
        private BattleState currentState = BattleState.WaitingStart;
        private AudioClip playerClip;
        private string microphoneDevice;
        private bool isRecording = false;
        private const int sampleRate = 24000;
        public BattleState CurrentState => currentState;
        private string npcResponseText;
        private string npcResponseAudio;
        private OpenAIService gameAIService;
        private List<string> responseHistory = new List<string>();

        public void BeginRapBattle()
        {
            npcAudioSource.Stop();
            currentRound = 0;
            npcResponseText = string.Empty;
            npcResponseAudio = string.Empty;
            responseHistory.Clear();
            currentState = BattleState.WaitingStart;
            gameAIService = new OpenAIService(aiConfig);
            uiManager.clearText();
            gameAIService.AddSystemMessage(aiConfig.RapPersonality);
            secondsPerBeat = 60f / bpm;
            secondsPerBar = secondsPerBeat * 4f;
            PlayMusic();
            StartCoroutine(BattleLoop());
        }

        private void PlayMusic()
        {
            if (musicSource != null)
            {
                musicSource.time = 0f;
                musicSource.Play();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                uIMenuController.ShowMainMenu();
            }
        }

        private IEnumerator BattleLoop()
        {
            uiManager.UpdateStatus("Get ready to rap!");
            yield return StartCoroutine(WaitBars(startOfGameRestLenInBars));
            // Wait for the player to start the battle

            while (totalRounds > currentRound)
            {
                // Player Countdown
                // currentState = BattleState.PlayerCountdown;
                // yield return StartCoroutine(PlayerCountdown(countDownLenInBars));

                // Player Turn
                currentState = BattleState.PlayerTurn;
                uiManager.UpdateStatus("Press and hold the space bar to start recording your rap!");

                yield return StartCoroutine(HandleRapSubmission());

                matchJudge.RecordPlayerInput(WavUtility.FromAudioClip(playerClip));

                // Waiting Turn
                currentState = BattleState.WaitingTurn;
                animationController.SetTrigger("StartThinking");
                uiManager.UpdateStatus("Waiting for your opponent to respond...");
                yield return StartCoroutine(ProcessSystemResponseTurn());

                // NPC Turn
                currentState = BattleState.NPCTurn;
                yield return StartCoroutine(GetAndPlayNpcResponse());

                // Rest Turn
                currentState = BattleState.Rest;
                currentRound++;
                if (totalRounds > currentRound)
                {
                    uiManager.UpdateStatus(
                        $"Finished round number {currentRound + 1} of {totalRounds}"
                    );

                    yield return StartCoroutine(WaitBars(restTurnLenInBars));
                }
            }
            uiManager.UpdateStatus("Finished! Let's wait for the judge to decide the winner!");
            // matchJudge.SaveMatchRecording("./Assets/matchRecording.wav");
            yield return StartCoroutine(matchJudge.JudgeMatch());
            uiManager.UpdateStatus("Press space to return to the main menu.");
            var judgeVerdict = matchJudge.GetJudgeVerdict();
            uiManager.UpdateComputerText(judgeVerdict);
            if (judgeVerdict.StartsWith("AI"))
            {
                animationController.SetTrigger("StartCheering");
            }
            else if (judgeVerdict.StartsWith("Player"))
            {
                animationController.SetTrigger("StartCrying");
            }
            while (!Input.GetKeyDown(KeyCode.Space))
            {
                yield return null;
            }
            animationController.SetTrigger("ReturnToIdle");
            uIMenuController.ShowMainMenu();
        }

        private IEnumerator HandleRapSubmission()
        {
            uiManager.UpdateStatus("Press and hold space to record your rap.");
            while (!Input.GetKeyDown(KeyCode.Space))
            {
                yield return null;
            }

            bool rapSubmitted = false;
            while (!rapSubmitted)
            {
                yield return StartCoroutine(RecordPlayerRap());

                float submitEndTime = Time.time + submitRapPeriodInSeconds;
                bool retakeRequested = false;

                while (Time.time < submitEndTime)
                {
                    uiManager.UpdateStatus(
                        $"Submitting rap in {Mathf.CeilToInt(submitEndTime - Time.time)} seconds. Press and hold space to record again."
                    );
                    if (Input.GetKeyDown(KeyCode.Space))
                    {
                        retakeRequested = true;
                        break;
                    }
                    yield return null;
                }

                if (!retakeRequested)
                {
                    rapSubmitted = true;
                }
            }
        }

        private IEnumerator RecordPlayerRap()
        {
            StartMicrophoneRecording();
            var startTime = Time.time;
            uiManager.UpdateStatus("Recording your rap! Release space to stop recording.");

            while (!Input.GetKeyUp(KeyCode.Space) && isRecording)
            {
                if (Time.time - startTime >= maxPlayerRecordingLengthInSeconds)
                {
                    uiManager.UpdateStatus("Maximum recording length reached. Stopping recording.");
                    break;
                }
                else if (Time.time - startTime >= maxPlayerRecordingLengthInSeconds - 10)
                {
                    float secondsLeft = maxPlayerRecordingLengthInSeconds - (Time.time - startTime);
                    uiManager.UpdateStatus(
                        $"Stopping recording in {Mathf.CeilToInt(secondsLeft)} seconds. Start wrapping up!"
                    );
                }
                yield return null;
            }
            StopMicrophoneRecording();
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
            matchJudge.RecordPlayerInput(WavUtility.FromAudioClip(playerClip));
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
                maxPlayerRecordingLengthInSeconds,
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

            OpenAIService.ChatCompletionResponse response = null;
            Exception error = null;
            int maxRetries = 3;
            float backoff = 1f;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                yield return StartCoroutine(
                    SendToChatCompletionCoroutine((res) => response = res, (ex) => error = ex)
                );

                if (response != null)
                    break;

                Debug.LogWarning($"OpenAI response failed. (attempt {attempt + 1}). Retrying...");
                yield return new WaitForSeconds(backoff);
                backoff *= 2f;
            }

            if (response == null || error != null)
            {
                if (error != null)
                {
                    uiManager.ShowError($"OpenAI API error: {error.Message}");
                }
                else
                {
                    uiManager.ShowError("Failed to get a response from OpenAI after retries.");
                }
                yield break;
            }

            npcResponseText = response.choices[0].message.content;
            responseHistory.Add(npcResponseText);
            gameAIService.ReplaceSystemMessage(
                $"{aiConfig.RapPersonality} - responses history divided by '|': {string.Join(" | ", responseHistory)}"
            );
            // Debug.Log($"NPC response text: {npcResponseText}");
            // Debug.Log($"NPC response audio: {npcResponseAudio}");
        }

        private IEnumerator SendToChatCompletionCoroutine(
            Action<OpenAIService.ChatCompletionResponse> onSuccess,
            Action<Exception> onError
        )
        {
            OpenAIService.ChatCompletionResponse localResponse = null;
            Exception localError = null;

            yield return gameAIService.SendToChatCompletion(
                (res) =>
                {
                    localResponse = res;
                },
                (ex) =>
                {
                    localError = ex;
                }
            );

            if (localResponse != null)
                onSuccess(localResponse);
            else
                onError(localError);
        }

        private void submitUserAudioMessage()
        {
            var playerRecordingBase64 = playerClip
                ? Convert.ToBase64String(WavUtility.FromAudioClip(playerClip))
                : null;
            if (string.IsNullOrEmpty(playerRecordingBase64))
            {
                uiManager.ShowError("No microphone audio recording found!");
            }
            // Add current user message
            gameAIService.AddUserAudioMessage(playerRecordingBase64);
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

        private IEnumerator GetAndPlayNpcResponse()
        {
            if (!string.IsNullOrEmpty(npcResponseText))
            {
                yield return StartCoroutine(GenerateNpcResponseAudio());
                if (!string.IsNullOrEmpty(npcResponseAudio))
                {
                    byte[] npcAudioBytes = Convert.FromBase64String(npcResponseAudio);
                    matchJudge.RecordNPCInput(npcAudioBytes);
                    AudioClip clip = WavUtility.AudioClipFromCorruptWav(npcAudioBytes, sampleRate);
                    uiManager.UpdateComputerText(npcResponseText);
                    npcAudioSource.clip = clip;
                    npcAudioSource.Play();
                    uiManager.UpdateStatus("Opponents turn!");
                    animationController.SetTrigger("StartRapping");
                    yield return new WaitForSeconds(clip.length);
                    animationController.SetTrigger("ReturnToIdle");
                }
                else
                {
                    uiManager.UpdateStatus("No audio response from NPC.");
                    yield return StartCoroutine(WaitBars(countDownLenInBars));
                }
            }
        }

        private IEnumerator GenerateNpcResponseAudio()
        {
            npcResponseAudio = null;
            string geminiApiKey = aiConfig.GeminiApiKey;
            string geminiTtsUrl =
                "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-tts:generateContent";

            var requestBody = new GeminiTtsRequest
            {
                contents = new[]
                {
                    new GeminiTtsRequest.Content
                    {
                        parts = new GeminiTtsRequest.Part[]
                        {
                            new GeminiTtsRequest.Part
                            {
                                text = $"[PROMPT: {npcSpeakingPrompt}]: {npcResponseText}",
                            },
                        },
                    },
                },
                generationConfig = new GeminiTtsRequest.GenerationConfig
                {
                    responseModalities = new[] { "AUDIO" },
                    speechConfig = new GeminiTtsRequest.SpeechConfig
                    {
                        voiceConfig = new GeminiTtsRequest.VoiceConfig
                        {
                            prebuiltVoiceConfig = new GeminiTtsRequest.PrebuiltVoiceConfig
                            {
                                voiceName = aiConfig.SelectedGeminiTtsVoice.ToString(),
                            },
                        },
                    },
                },
                model = "gemini-2.5-flash-preview-tts",
            };

            string jsonBody = JsonUtility.ToJson(requestBody);
            using (UnityWebRequest request = new UnityWebRequest(geminiTtsUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-goog-api-key", geminiApiKey);

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    uiManager.ShowError($"Gemini TTS failed: {request.error}");
                    Debug.LogError(
                        $"Gemini TTS with request: {jsonBody} \nfailed: {request.error} \nResponse: {request.downloadHandler.text}"
                    );
                    yield break;
                }

                try
                {
                    var responseJson = request.downloadHandler.text;
                    var response = JsonUtility.FromJson<GeminiTtsResponse>(responseJson);
                    if (
                        response.candidates != null
                        && response.candidates.Length > 0
                        && response.candidates[0].content.parts != null
                        && response.candidates[0].content.parts.Length > 0
                        && response.candidates[0].content.parts[0].inlineData != null
                    )
                    {
                        npcResponseAudio = response.candidates[0].content.parts[0].inlineData.data;
                    }
                    else
                    {
                        Debug.LogError("Gemini TTS response missing audio data.");
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to parse Gemini TTS response: {ex}");
                    yield break;
                }
            }
        }
    }
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

// Gemini TTS request classes
[Serializable]
public class GeminiTtsRequest
{
    public Content[] contents;
    public GenerationConfig generationConfig;
    public string model;

    [Serializable]
    public class Content
    {
        public Part[] parts;
    }

    [Serializable]
    public class Part
    {
        public string text;
    }

    [Serializable]
    public class GenerationConfig
    {
        public string[] responseModalities;
        public SpeechConfig speechConfig;
    }

    [Serializable]
    public class SpeechConfig
    {
        public VoiceConfig voiceConfig;
    }

    [Serializable]
    public class VoiceConfig
    {
        public PrebuiltVoiceConfig prebuiltVoiceConfig;
    }

    [Serializable]
    public class PrebuiltVoiceConfig
    {
        public string voiceName;
    }
}

// Gemini TTS response classes
[Serializable]
public class GeminiTtsResponse
{
    public Candidate[] candidates;

    [Serializable]
    public class Candidate
    {
        public Content content;
    }

    [Serializable]
    public class Content
    {
        public Part[] parts;
    }

    [Serializable]
    public class Part
    {
        public InlineData inlineData;
    }

    [Serializable]
    public class InlineData
    {
        public string data;
    }
}
