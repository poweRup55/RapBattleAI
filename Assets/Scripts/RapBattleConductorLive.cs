using System;
using System.Collections;
using EpicRapBattle.Config;
using UnityEngine;
using UnityEngine.UI;

public class RapBattleConductorLive : MonoBehaviour
{
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
    private int totalRounds = 3;

    [Tooltip("Rap submission Time")]
    [SerializeField]
    private int submitRapPeriodInSeconds = 5;

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
    private AudioClip playerRecordingClip;
    private float recordingLengthInSeconds;
    private string microphoneDevice;
    private bool isRecording = false;
    private int currentRound = 0;

    public void BeginRapBattle()
    {
        StopAllCoroutines();
        currentRound = 0;
        if (isRecording)
        {
            StopMicrophoneRecording();
        }
        isRecording = false;
        recordingLengthInSeconds = 0f;
        playerRecordingClip = null;
        if (animationController != null)
        {
            if (animationController.GetCurrentAnimatorStateInfo(0).IsName("Rapping"))
                animationController.SetTrigger("ReturnToIdle");
        }
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Stop();
        }
        if (AIRapperAAudioSource != null && AIRapperAAudioSource.isPlaying)
        {
            AIRapperAAudioSource.Stop();
        }
        if (uiManager != null)
        {
            uiManager.clearText();
            uiManager.ClearComputerText();
            uiManager.ClearAIJudgeText();
            uiManager.ClearAllReactions();
            uiManager.RemovePlayingReactions();
        }
        if (recordButton != null)
        {
            recordButton.interactable = false;
        }
        if (geminiLiveAIRapper != null && geminiLiveAIRapper.IsWebSocketConnected())
        {
            geminiLiveAIRapper.Destroy();
        }
        if (geminiLiveAIJudge != null && geminiLiveAIJudge.IsWebSocketConnected())
        {
            geminiLiveAIJudge.Destroy();
        }
        PlayMusic();

        // Start the battle loop
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

    public void TerminateLiveSession(string errorMessage = null)
    {
        animationController.SetTrigger("ReturnToIdle");
        geminiLiveAIRapper.Destroy();
        geminiLiveAIJudge.Destroy();
        uIMenuController.ShowMainMenu(errorMessage);
    }

    private IEnumerator InitializeWithStatus()
    {
        uiManager.UpdateStatus("Connecting...");

        // Set reference to this conductor for error handling
        geminiLiveAIRapper.SetRapBattleConductor(this);
        geminiLiveAIJudge.SetRapBattleConductor(this);

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
            uiManager.ClearAllReactions();
            Coroutine showReactions = StartCoroutine(ShowReactionsCoroutine());
            yield return StartCoroutine(PlayerRapSubmissionLoop());
            StopCoroutine(showReactions);

            // NPC Turn

            // Debug.Log("NPC is rapping...");

            showReactions = StartCoroutine(ShowReactionsCoroutine());
            var createAudioCoroutine = StartCoroutine(geminiLiveAIRapper.CreateAudioCoroutines());
            var playAudioCoroutine = StartCoroutine(geminiLiveAIRapper.PlayAudioCoroutine());
            var streamAudioToJudge = StartCoroutine(
                geminiLiveAIRapper.StreamToOtherAgent(
                    geminiLiveAIJudge,
                    AILiveConfig.inputSampleRate
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
            StopCoroutine(showReactions);
            yield return StartCoroutine(
                geminiLiveAIJudge.SendTextToGeminiCoroutine(
                    $"Round {currentRound + 1} ended. Give round score."
                )
            );
            uiManager.UpdateStatus($"Let's see what the judge thinks! wait for it...");
            yield return StartCoroutine(geminiLiveAIJudge.WaitForAllMessagesToBeSent(5f));
            yield return StartCoroutine(geminiLiveAIRapper.WaitForAllMessagesToBeSent(5f));
            yield return StartCoroutine(uiManager.ShowJudgePanelTemporarily(15.0f));
            uiManager.ClearAllReactions();
            uiManager.RemovePlayingReactions();
            // Rest Turn
            currentRound++;
            if (totalRounds > currentRound)
            {
                uiManager.UpdateStatus(
                    $"Finished round number {currentRound + 1} of {totalRounds}"
                );
                yield return new WaitForSeconds(3f);
            }
        }
        uiManager.ClearAIJudgeText();
        uiManager.ClearComputerText();
        yield return StartCoroutine(
            geminiLiveAIJudge.SendTextToGeminiCoroutine("rap session over.")
        );
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

    private IEnumerator ShowReactionsCoroutine()
    {
        uiManager.ClearAllReactions();
        uiManager.RemovePlayingReactions();

        // Discard first reaction
        PopUpGameText firstReaction = null;
        yield return new WaitUntil(() =>
        {
            firstReaction = uiManager.PopNextReaction();
            return firstReaction != null;
        });
        Destroy(firstReaction.gameObject);

        PopUpGameText currentReaction = null;
        while (true)
        {
            if (currentReaction == null || currentReaction.gameObject == null)
            {
                currentReaction = uiManager.PopNextReaction();
                if (currentReaction != null)
                {
                    currentReaction.ShowText();
                }
            }
            else
            {
                Destroy(uiManager.PopNextReaction()?.gameObject);
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator PlayerRapSubmissionLoop()
    {
        while (true)
        {
            uiManager.UpdateStatus(
                "Hold the space bar or tap and hold anywhere to begin recording your rap!"
            );

            if (useFileSubmission && fileSubmissionClip != null)
            {
                uiManager.UpdateStatus("Using file submission for this round.");
                playerRecordingClip = fileSubmissionClip;
            }

            yield return StartCoroutine(GetRapRecording());

            if (playBackRecording)
            {
                yield return PlayBackRecording();
            }
            animationController.SetTrigger("StartThinking");
            uiManager.UpdateStatus("Waiting for your opponent to respond...");
            float startTime = Time.time;
            while (Time.time - startTime < 20f)
            {
                if (geminiLiveAIRapper.IsAudioActive())
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
        yield return StartCoroutine(geminiLiveAIRapper.SendActivityStartToGeminiCoroutine());

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
        yield return StartCoroutine(geminiLiveAIRapper.SendTextToGeminiCoroutine("finalized"));
        yield return StartCoroutine(geminiLiveAIRapper.SendActivityEndToGeminiCoroutine());
        yield return StartCoroutine(
            geminiLiveAIJudge.SendTextToGeminiCoroutine("finalized rapper 1")
        );
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
        bool firstAttempt = true;
        while (!submitRecording)
        {
            if (!firstAttempt)
            {
                yield return StartCoroutine(
                    geminiLiveAIRapper.SendTextToGeminiCoroutine("discard")
                );
                yield return StartCoroutine(
                    geminiLiveAIJudge.SendTextToGeminiCoroutine("discard rapper 1")
                );
            }
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
                    "attempt"
                )
            );

            StartCoroutine(
                StreamAudioToGemini(
                    geminiLiveAIJudge,
                    playerRecordingClip,
                    () => Microphone.GetPosition(microphoneDevice),
                    () => isRecording,
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
                firstAttempt = false;
                continue;
            }

            submitRecording = true;
            firstAttempt = false;
        }
    }

    private bool ValidateAudioStreamInputs(GeminiLiveWebRTC agent, AudioClip clip = null)
    {
        if (agent == null)
        {
            Debug.LogError("Gemini agent is null");
            return false;
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
            return AudioClipResampler.ResampleAudio(originalClip, AILiveConfig.inputSampleRate);
        }

        return originalClip;
    }

    private IEnumerator SendInitialMessage(GeminiLiveWebRTC agent, string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            yield return StartCoroutine(agent.SendTextToGeminiCoroutine(message));
        }
    }

    private IEnumerator SendEndActivityMarker(GeminiLiveWebRTC agent)
    {
        yield return StartCoroutine(agent.SendActivityEndToGeminiCoroutine());
    }

    private IEnumerator SendStartActivityMarker(GeminiLiveWebRTC agent)
    {
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

            yield return StartCoroutine(agent.SendAudioToGeminiCoroutine(samples));
        }
    }

    private IEnumerator SendFinalAudioData(
        GeminiLiveWebRTC agent,
        AudioClip clip,
        Func<int> getPosition,
        int lastSamplePosition,
        float resizeClip = -1f
    )
    {
        if (resizeClip > 0 && clip != null && clip.samples != resizeClip)
        {
            clip = WavUtility.TrimClipToLength(clip, resizeClip);
        }
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
        yield return StartCoroutine(
            agent.SendSilenceToGeminiCoroutine(2f, clip.frequency, clip.channels)
        );
    }

    private IEnumerator StreamAudioToGemini(
        GeminiLiveWebRTC agent,
        AudioClip clip,
        Func<int> getPosition,
        Func<bool> isActive,
        string startText = null,
        string endText = null,
        Func<bool> waitCondition = null
    )
    {
        yield return StartCoroutine(SendInitialMessage(agent, startText));

        if (!ValidateAudioStreamInputs(agent, clip))
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
            yield return new WaitUntil(waitCondition);
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
            SendFinalAudioData(
                agent,
                clip,
                getPosition,
                lastSamplePosition,
                recordingLengthInSeconds
            )
        );

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
            yield return new WaitForSeconds(recordingLengthInSeconds);
            Destroy(playbackSource);
            musicSource.UnPause();
        }
        yield break;
    }
}
