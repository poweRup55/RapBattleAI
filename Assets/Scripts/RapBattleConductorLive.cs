using System;
using System.Collections;
using EpicRapBattle.Config;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class RapBattleConductorLive : MonoBehaviour
{
    [System.Serializable]
    private struct AudioStreamConfig
    {
        public string startText;
        public string endText;
        public bool addActivityMarkers;
        public Func<bool> waitCondition;
        public const float sendInterval = 0.1f;
    }

    private struct AudioStreamState
    {
        public int lastSamplePosition;
        public float lastSendTime;
        public AudioClip processedClip;
    }

    [Header("Animation Controller")]
    [Tooltip("Animation controller for the rapper.")]
    [SerializeField]
    private Animator animationController;

    [Header("Audio Sources")]
    [Tooltip("Audio source for background music.")]
    [SerializeField]
    private AudioSource musicSource;

    [Tooltip("Audio source for AI rapper.")]
    [SerializeField]
    private AudioSource AIRapperAAudioSource;

    [SerializeField]
    private UIManager uiManager;

    [SerializeField]
    private GeminiLiveWebRTCAudio geminiLiveAIRapper;

    [SerializeField]
    private GeminiLiveWebRTC geminiLiveAIJudge;

    [SerializeField]
    private float maxPlayerRecordingLengthInSeconds = 30f;

    [SerializeField]
    private float minRecordingLengthInSeconds = 5f;

    [Header("Game Configuration")]
    [Tooltip("Total Rap Rounds")]
    [SerializeField]
    private const int totalRounds = 3;

    [Tooltip("Rap submission Time")]
    [SerializeField]
    private const int submitRapPeriodInSeconds = 5;

    [Header("UI")]
    [SerializeField]
    private UIMainMenuController uIMenuController;

    [SerializeField]
    private Button recordButton;

    [Header("Dev and Debugging")]
    [SerializeField]
    private bool playBackRecording;

    [SerializeField]
    private bool useFileSubmission = false;

    [SerializeField]
    private AudioClip fileSubmissionClip;

    private BattleState currentState = BattleState.WaitingStart;
    private AudioClip playerRecordingClip;
    private float recordingLengthInSeconds;
    private string microphoneDevice;
    private bool isRecording = false;
    private int currentRound = 0;

    private void Start()
    {
        microphoneDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        // if (useFileSubmission)
        // {
        //     fileSubmissionClip = AudioClipResampler.ResampleAudio(
        //         fileSubmissionClip,
        //         AILiveConfig.inputSampleRate
        //     );
        // }
        BeginRapBattle();
    }

    public void BeginRapBattle()
    {
        currentRound = 0;
        currentState = BattleState.WaitingStart;
        uiManager.clearText();
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
            TerminateLiveSession();
        }
    }

    private void TerminateLiveSession()
    {
        animationController.SetTrigger("ReturnToIdle");
        geminiLiveAIRapper.Destroy();
        geminiLiveAIJudge.Destroy();
        uIMenuController.ShowMainMenu();
    }

    private IEnumerator InitializeWithStatus()
    {
        uiManager.UpdateStatus("Connecting...");
        Coroutine init = StartCoroutine(geminiLiveAIRapper.Initialize());
        Coroutine initJudge = StartCoroutine(geminiLiveAIJudge.Initialize());
        yield return new WaitForSeconds(2f);
        yield return init;
        yield return initJudge;
        uiManager.UpdateStatus("Connected to Rap Battle Servers!");
        yield return new WaitForSeconds(2f);
    }

    private IEnumerator BattleLoop()
    {
        yield return StartCoroutine(InitializeWithStatus());

        while (totalRounds > currentRound)
        {
            uiManager.ClearComputerText();
            uiManager.ClearAIJudgeText();
            yield return StartCoroutine(PlayerRapSubmissionLoop());

            // NPC Turn
            currentState = BattleState.NPCTurn;

            // Debug.Log("NPC is rapping...");
            var createAudioCoroutine = StartCoroutine(geminiLiveAIRapper.CreateAudioCoroutines());
            var playAudioCoroutine = StartCoroutine(geminiLiveAIRapper.PlayAudioCoroutine());
            StartCoroutine(
                StreamAudioToGemini(
                    geminiLiveAIJudge,
                    AIRapperAAudioSource,
                    () => geminiLiveAIRapper.IsAudioActive(),
                    false,
                    "attempt rapper 2",
                    $"finalized, round {currentRound} ended. Give round score.",
                    () => geminiLiveAIRapper.IsAudioActive()
                )
            );
            yield return new WaitUntil(() => geminiLiveAIRapper.IsPlaying);
            uiManager.UpdateStatus($"NPC is rapping!");
            animationController.SetTrigger("StartRapping");
            yield return StartCoroutine(geminiLiveAIRapper.waitForAudioStreamFinish());
            // yield return new WaitForSeconds(2f);
            animationController.SetTrigger("ReturnToIdle");
            StopCoroutine(createAudioCoroutine);
            StopCoroutine(playAudioCoroutine);
            yield return StartCoroutine(uiManager.ShowJudgePanelTemporarily(15.0f));
            // Rest Turn
            currentState = BattleState.Rest;
            currentRound++;
            if (totalRounds > currentRound)
            {
                uiManager.UpdateStatus(
                    $"Finished round number {currentRound + 1} of {totalRounds}"
                );
                yield return new WaitForSeconds(3f);
            }
        }
        StartCoroutine(geminiLiveAIJudge.SendTextToGeminiCoroutine("rap session over."));
        uiManager.UpdateStatus("Finished! Let's wait for the judge to decide the winner!");
        yield return new WaitForSeconds(2f);
        yield return StartCoroutine(uiManager.ShowJudgePanelTemporarily(15.0f));

        uiManager.UpdateStatus("Press space or tap anywhere to return to the main menu.");
        while (!Input.GetKeyDown(KeyCode.Space) && !Input.GetMouseButtonDown(0))
        {
            yield return null;
        }
        TerminateLiveSession();
    }

    private IEnumerator PlayerRapSubmissionLoop()
    {
        while (true)
        {
            currentState = BattleState.PlayerTurn;
            uiManager.UpdateStatus(
                "Hold the space bar or tap and hold anywhere to begin recording your rap!"
            );

            // if (useFileSubmission && fileSubmissionClip != null)
            // {
            //     uiManager.UpdateStatus("Using file submission for this round.");
            //     playerRecordingClip = fileSubmissionClip;
            // }

            yield return StartCoroutine(GetRapRecording());

            if (playBackRecording)
            {
                yield return PlayBackRecording();
            }
            currentState = BattleState.WaitingTurn;
            animationController.SetTrigger("StartThinking");
            uiManager.UpdateStatus("Waiting for your opponent to respond...");
            float startTime = Time.time;
            geminiLiveAIRapper.WaitForAudioReception();
            while (Time.time - startTime < 20f)
            {
                if (geminiLiveAIRapper.IsReceivingAudioData)
                {
                    yield break;
                }
                yield return new WaitForSeconds(0.1f);
            }
            uiManager.UpdateStatus("Opponent response timed out. Try again.");
            yield return new WaitForSeconds(2f);
        }
    }

    private IEnumerator GetRapRecording()
    {
        bool rapSubmitted = false;
        while (!rapSubmitted)
        {
            yield return StartCoroutine(RecordPlayerRap());
            float submitEndTime = Time.time + submitRapPeriodInSeconds;
            bool retakeRequested = false;

            while (Time.time < submitEndTime)
            {
                uiManager.UpdateStatus(
                    $"Submitting rap in {Mathf.CeilToInt(submitEndTime - Time.time)} seconds."
                );
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
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
        StartCoroutine(geminiLiveAIRapper.SendTextToGeminiCoroutine("finalized"));
        // StartCoroutine(geminiLiveAIRapper.SendActivityEndToGeminiCoroutine());

        StartCoroutine(geminiLiveAIJudge.SendTextToGeminiCoroutine("finalized rapper 1"));
        // TrimRecordingToActualLength();
        // ResamplePlayerRecording();
    }

    private IEnumerator WaitForRecordingStart()
    {
        recordButton.interactable = true;

        while (!Input.GetKeyDown(KeyCode.Space) && !Input.GetMouseButtonDown(0))
        {
            yield return null;
        }
        recordButton.interactable = false;
        yield break;
    }

    private IEnumerator RecordPlayerRap()
    {
        bool submitRecording = false;
        while (!submitRecording)
        {
            uiManager.UpdateStatus("Press and hold the button to record your rap.");
            yield return StartCoroutine(WaitForRecordingStart());
            StartMicrophoneRecording();
            var startTime = Time.time;

            StartCoroutine(
                StreamAudioToGemini(
                    geminiLiveAIRapper,
                    playerRecordingClip,
                    () => Microphone.GetPosition(microphoneDevice),
                    () => isRecording,
                    false,
                    "attempt"
                )
            );

            StartCoroutine(
                StreamAudioToGemini(
                    geminiLiveAIJudge,
                    playerRecordingClip,
                    () => Microphone.GetPosition(microphoneDevice),
                    () => isRecording,
                    false,
                    "attempt rapper 1"
                )
            );

            uiManager.UpdateStatus("Recording your rap! Release to stop recording.");

            while (!Input.GetKeyUp(KeyCode.Space) && !Input.GetMouseButtonUp(0) && isRecording)
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
                        $"Stopping recording in {Mathf.CeilToInt(secondsLeft)} seconds."
                    );
                }
                yield return null;
            }
            recordingLengthInSeconds = Time.time - startTime;
            // Debug.Log(
            //     $"Recording length: {recordingLengthInSeconds} seconds, max allowed: {maxPlayerRecordingLengthInSeconds} seconds."
            // );
            StopMicrophoneRecording();

            if (recordingLengthInSeconds < minRecordingLengthInSeconds)
            {
                uiManager.UpdateStatus(
                    $"Recording too short. Please record at least {minRecordingLengthInSeconds} second."
                );
                yield return new WaitForSeconds(2f);
                continue;
            }

            submitRecording = true;
        }
    }

    private bool ValidateAudioStreamInputs(
        GeminiLiveWebRTC agent,
        AudioSource audioSource = null,
        AudioClip clip = null
    )
    {
        if (agent == null)
        {
            Debug.LogError("Gemini agent is null");
            return false;
        }

        if (audioSource == null && clip == null)
        {
            Debug.LogError($"Both audio source and clip are null for agent {agent.name}");
            return false;
        }

        if (audioSource != null && audioSource == null)
        {
            Debug.LogError($"Audio source is null for agent {agent.name}");
            return false;
        }

        if (clip == null && audioSource != null)
        {
            return true;
        }

        if (clip != null)
        {
            return true;
        }

        return false;
    }

    private AudioClip PrepareAudioClip(AudioClip originalClip, string agentName)
    {
        if (originalClip == null)
        {
            Debug.LogError($"Audio clip is null for agent {agentName}");
            return null;
        }

        if (originalClip.frequency != AILiveConfig.inputSampleRate)
        {
            Debug.Log($"Resampling audio clip {originalClip.name} for agent {agentName}");
            return AudioClipResampler.ResampleAudio(originalClip, AILiveConfig.inputSampleRate);
        }

        return originalClip;
    }

    private float[] ResampleAudioSamples(float[] samples, AudioClip originalClip, int samplesToGet)
    {
        if (originalClip.frequency != AILiveConfig.inputSampleRate)
        {
            AudioClip resampledClip = AudioClipResampler.ResampleAudio(
                originalClip,
                AILiveConfig.inputSampleRate
            );
            float[] resampledSamples = new float[samplesToGet * resampledClip.channels];
            return resampledSamples;
        }
        return samples;
    }

    private IEnumerator SendInitialMessage(GeminiLiveWebRTC agent, string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            Debug.Log($"Sending start text to Gemini agent {agent.name}: {message}");
            yield return StartCoroutine(agent.SendTextToGeminiCoroutine(message));
        }
    }

    private IEnumerator SendEndActivityMarker(GeminiLiveWebRTC agent)
    {
        Debug.Log($"Sending activity end marker for agent {agent.name}");
        yield return StartCoroutine(agent.SendActivityEndToGeminiCoroutine());
    }

    private IEnumerator SendStartActivityMarker(GeminiLiveWebRTC agent)
    {
        Debug.Log($"Sending activity start marker for agent {agent.name}");
        yield return StartCoroutine(agent.SendActivityStartToGeminiCoroutine());
    }

    private IEnumerator ProcessAndSendAudioSamples(
        GeminiLiveWebRTC agent,
        AudioClip clip,
        int lastSamplePosition,
        int currentSamplePosition
    )
    {
        int samplesToGet = currentSamplePosition - lastSamplePosition;
        if (samplesToGet > 0)
        {
            float[] samples = new float[samplesToGet * clip.channels];
            clip.GetData(samples, lastSamplePosition % clip.samples);

            samples = ResampleAudioSamples(samples, clip, samplesToGet);

            Debug.Log(
                $"Sending {samples.Length} audio samples (from position {lastSamplePosition}) to Gemini agent {agent.name}"
            );
            yield return StartCoroutine(agent.SendAudioToGeminiCoroutine(samples));
        }
    }

    private IEnumerator ProcessAndSendAudioSourceSamples(
        GeminiLiveWebRTC agent,
        AudioSource audioSource,
        int lastSamplePosition,
        int currentSamplePosition
    )
    {
        AudioClip currentClip = audioSource.clip;
        int samplesToGet = currentSamplePosition - lastSamplePosition;

        if (samplesToGet > 0)
        {
            float[] samples = new float[samplesToGet * currentClip.channels];
            currentClip.GetData(samples, lastSamplePosition % currentClip.samples);

            if (currentClip.frequency != AILiveConfig.inputSampleRate)
            {
                AudioClip resampledClip = AudioClipResampler.ResampleAudio(
                    currentClip,
                    AILiveConfig.inputSampleRate
                );
                samples = new float[samplesToGet * resampledClip.channels];
                resampledClip.GetData(samples, lastSamplePosition % resampledClip.samples);
            }

            Debug.Log(
                $"Sending {samples.Length} audio samples from AudioSource (position {lastSamplePosition}) to Gemini agent {agent.name}"
            );
            yield return StartCoroutine(agent.SendAudioToGeminiCoroutine(samples));
        }
    }

    private IEnumerator SendFinalAudioData(
        GeminiLiveWebRTC agent,
        AudioClip clip,
        Func<int> getPosition,
        int lastSamplePosition
    )
    {
        int finalSamplePosition = getPosition();
        if (finalSamplePosition < lastSamplePosition)
        {
            finalSamplePosition += clip.samples;
        }

        int finalSamplesToGet = finalSamplePosition - lastSamplePosition;
        if (finalSamplesToGet > 0)
        {
            float[] finalSamples = new float[finalSamplesToGet * clip.channels];
            clip.GetData(finalSamples, lastSamplePosition % clip.samples);

            Debug.Log(
                $"Sending final {finalSamples.Length} audio samples (from position {lastSamplePosition}) to Gemini agent {agent.name}"
            );
            yield return StartCoroutine(agent.SendAudioToGeminiCoroutine(finalSamples));
        }
    }

    private IEnumerator SendFinalAudioSourceData(
        GeminiLiveWebRTC agent,
        AudioSource audioSource,
        int lastSamplePosition
    )
    {
        if (audioSource.clip != null)
        {
            int finalSamplePosition = audioSource.timeSamples;
            AudioClip finalClip = audioSource.clip;

            if (finalSamplePosition < lastSamplePosition)
            {
                finalSamplePosition += finalClip.samples;
            }

            int finalSamplesToGet = finalSamplePosition - lastSamplePosition;
            if (finalSamplesToGet > 0)
            {
                float[] finalSamples = new float[finalSamplesToGet * finalClip.channels];
                finalClip.GetData(finalSamples, lastSamplePosition % finalClip.samples);

                if (finalClip.frequency != AILiveConfig.inputSampleRate)
                {
                    AudioClip resampledClip = AudioClipResampler.ResampleAudio(
                        finalClip,
                        AILiveConfig.inputSampleRate
                    );
                    finalSamples = new float[finalSamplesToGet * resampledClip.channels];
                    resampledClip.GetData(finalSamples, lastSamplePosition % resampledClip.samples);
                }

                Debug.Log(
                    $"Sending final {finalSamples.Length} audio samples from AudioSource (position {lastSamplePosition}) to Gemini agent {agent.name}"
                );
                yield return StartCoroutine(agent.SendAudioToGeminiCoroutine(finalSamples));
            }
        }
    }

    private IEnumerator StreamAudioToGemini(
        GeminiLiveWebRTC agent,
        AudioSource audioSource,
        Func<bool> isActive,
        bool addActivityMarkers = false,
        string startText = null,
        string endText = null,
        Func<bool> waitCondition = null
    )
    {
        yield return StartCoroutine(SendInitialMessage(agent, startText));

        if (!ValidateAudioStreamInputs(agent, audioSource))
        {
            yield break;
        }

        const float sendInterval = 0.1f;
        int lastSamplePosition = 0;

        if (waitCondition != null)
        {
            Debug.Log($"Waiting for condition to start streaming for agent {agent.name}");
            yield return new WaitUntil(waitCondition);
        }

        if (addActivityMarkers)
        {
            yield return StartCoroutine(SendStartActivityMarker(agent));
        }

        float lastSendTime = Time.time;

        while (isActive())
        {
            if (audioSource.clip != null && audioSource.isPlaying)
            {
                int currentSamplePosition = audioSource.timeSamples;
                AudioClip currentClip = audioSource.clip;

                if (currentSamplePosition < lastSamplePosition)
                {
                    currentSamplePosition += currentClip.samples;
                }

                if (Time.time - lastSendTime >= sendInterval)
                {
                    yield return StartCoroutine(
                        ProcessAndSendAudioSourceSamples(
                            agent,
                            audioSource,
                            lastSamplePosition,
                            currentSamplePosition
                        )
                    );
                    lastSamplePosition = currentSamplePosition % currentClip.samples;
                    lastSendTime = Time.time;
                }
            }
            yield return null;
        }

        yield return StartCoroutine(
            SendFinalAudioSourceData(agent, audioSource, lastSamplePosition)
        );

        if (addActivityMarkers)
        {
            yield return StartCoroutine(SendEndActivityMarker(agent));
        }

        yield return StartCoroutine(SendInitialMessage(agent, endText));
    }

    private IEnumerator StreamAudioToGemini(
        GeminiLiveWebRTC agent,
        AudioClip clip,
        Func<int> getPosition,
        Func<bool> isActive,
        bool addActivityMarkers = false,
        string startText = null,
        string endText = null,
        Func<bool> waitCondition = null
    )
    {
        yield return StartCoroutine(SendInitialMessage(agent, startText));

        if (!ValidateAudioStreamInputs(agent, null, clip))
        {
            yield break;
        }

        clip = PrepareAudioClip(clip, agent.name);
        if (clip == null)
        {
            yield break;
        }

        const float sendInterval = 0.1f;
        int lastSamplePosition = 0;

        if (waitCondition != null)
        {
            Debug.Log($"Waiting for condition to start streaming for agent {agent.name}");
            yield return new WaitUntil(waitCondition);
        }

        if (addActivityMarkers)
        {
            yield return StartCoroutine(SendStartActivityMarker(agent));
        }

        float lastSendTime = Time.time;

        while (isActive())
        {
            int currentSamplePosition = getPosition();
            if (currentSamplePosition < lastSamplePosition)
            {
                currentSamplePosition += clip.samples;
            }

            if (Time.time - lastSendTime >= sendInterval)
            {
                yield return StartCoroutine(
                    ProcessAndSendAudioSamples(
                        agent,
                        clip,
                        lastSamplePosition,
                        currentSamplePosition
                    )
                );
                lastSamplePosition = currentSamplePosition % clip.samples;
                lastSendTime = Time.time;
            }
            yield return null;
        }

        yield return StartCoroutine(
            SendFinalAudioData(agent, clip, getPosition, lastSamplePosition)
        );

        if (addActivityMarkers)
        {
            yield return StartCoroutine(SendEndActivityMarker(agent));
        }

        yield return StartCoroutine(SendInitialMessage(agent, endText));
    }

    private void StartMicrophoneRecording()
    {
        playerRecordingClip = Microphone.Start(
            microphoneDevice,
            false,
            (int)maxPlayerRecordingLengthInSeconds,
            AILiveConfig.inputSampleRate
        );
        isRecording = true;
    }

    private void StopMicrophoneRecording()
    {
        if (!isRecording)
            return;
        Microphone.End(microphoneDevice);
        isRecording = false;
    }

    private IEnumerator PlayBackRecording()
    {
        uiManager.UpdateStatus("Playing back your recording...");
        if (playerRecordingClip != null)
        {
            musicSource.Pause();
            AudioSource playbackSource = gameObject.AddComponent<AudioSource>();
            playbackSource.clip = playerRecordingClip;
            playbackSource.Play();
            yield return new WaitForSeconds(playerRecordingClip.length);
            Destroy(playbackSource);
            musicSource.UnPause();
        }
        yield break;
    }
}
