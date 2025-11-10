using System;
using System.Collections;
using EpicRapBattle.Config;
using UnityEngine;
using UnityEngine.UI;

public class RapBattleConductorLive : MonoBehaviour
{
    #region Constants
    private const float INITIALIZATION_DELAY = 2f;
    private const float CONNECTION_STATUS_DISPLAY_TIME = 2f;
    private const float ROUND_COMPLETE_DISPLAY_TIME = 3f;
    private const float JUDGE_PANEL_DISPLAY_TIME = 15f;
    private const float OPPONENT_RESPONSE_TIMEOUT = 20f;
    private const float RECORDING_WARNING_THRESHOLD = 10f;
    private const float SHORT_RECORDING_WARNING_TIME = 2f;
    private const float AUDIO_SEND_INTERVAL = 0.1f;
    private const float POSITION_CHECK_INTERVAL = 0.05f;
    private const float INPUT_CHECK_INTERVAL = 0.05f;
    private const float REACTION_POLL_INTERVAL = 0.3f;
    private const float FINAL_SILENCE_DURATION = 2f;
    private const float MESSAGE_WAIT_TIMEOUT = 5f;
    private const float OPPONENT_CHECK_INTERVAL = 0.1f;
    #endregion

    #region Serialized Fields
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
    #endregion

    #region Private Fields
    private AudioClip playerRecordingClip;
    private float recordingLengthInSeconds;
    private string microphoneDevice;
    private bool isRecording = false;
    private int currentRound = 0;
    #endregion

    #region Public Methods
    public void BeginRapBattle()
    {
        StopAllCoroutines();
        ResetBattleState();
        CleanupResources();
        PlayMusic();
        StartCoroutine(BattleLoop());
    }

    public void TerminateLiveSession(string errorMessage = null)
    {
        if (animationController != null)
        {
            animationController.SetTrigger("ReturnToIdle");
        }

        if (geminiLiveAIRapper != null)
        {
            geminiLiveAIRapper.Destroy();
        }

        if (geminiLiveAIJudge != null)
        {
            geminiLiveAIJudge.Destroy();
        }

        if (uIMenuController != null)
        {
            uIMenuController.ShowMainMenu(errorMessage);
        }
    }
    #endregion

    #region Initialization & Cleanup
    private void ResetBattleState()
    {
        currentRound = 0;
        recordingLengthInSeconds = 0f;

        if (isRecording)
        {
            StopMicrophoneRecording();
        }
        isRecording = false;
    }

    private void CleanupResources()
    {
        CleanupPlayerRecording();
        ResetAnimation();
        StopAllAudio();
        ClearUI();
        DisconnectGeminiAgents();
    }

    private void CleanupPlayerRecording()
    {
        if (playerRecordingClip != null)
        {
            playerRecordingClip.UnloadAudioData();
            Destroy(playerRecordingClip);
            playerRecordingClip = null;
        }
    }

    private void ResetAnimation()
    {
        if (
            animationController != null
            && animationController.GetCurrentAnimatorStateInfo(0).IsName("Rapping")
        )
        {
            animationController.SetTrigger("ReturnToIdle");
        }
    }

    private void StopAllAudio()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Stop();
        }

        if (AIRapperAAudioSource != null && AIRapperAAudioSource.isPlaying)
        {
            AIRapperAAudioSource.Stop();
        }
    }

    private void ClearUI()
    {
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
    }

    private void DisconnectGeminiAgents()
    {
        if (geminiLiveAIRapper != null && geminiLiveAIRapper.IsWebSocketConnected())
        {
            geminiLiveAIRapper.Destroy();
        }

        if (geminiLiveAIJudge != null && geminiLiveAIJudge.IsWebSocketConnected())
        {
            geminiLiveAIJudge.Destroy();
        }
    }

    private void PlayMusic()
    {
        if (musicSource != null)
        {
            musicSource.time = 0f;
            musicSource.Play();
        }
    }
    #endregion

    #region Unity Lifecycle
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TerminateLiveSession();
        }
    }
    #endregion

    #region Battle Flow
    private IEnumerator InitializeWithStatus()
    {
        if (uiManager != null)
        {
            uiManager.UpdateStatus("Connecting...");
        }

        SetupGeminiAgents();

        Coroutine initRapper = StartCoroutine(geminiLiveAIRapper.Initialize());
        Coroutine initJudge = StartCoroutine(geminiLiveAIJudge.Initialize());

        yield return new WaitForSeconds(INITIALIZATION_DELAY);
        yield return initRapper;
        yield return initJudge;

        if (uiManager != null)
        {
            uiManager.UpdateStatus("Connected to Rap Battle Servers!");
        }

        yield return new WaitForSeconds(CONNECTION_STATUS_DISPLAY_TIME);
    }

    private void SetupGeminiAgents()
    {
        if (geminiLiveAIRapper != null)
        {
            geminiLiveAIRapper.SetRapBattleConductor(this);
        }

        if (geminiLiveAIJudge != null)
        {
            geminiLiveAIJudge.SetRapBattleConductor(this);
        }
    }

    private IEnumerator BattleLoop()
    {
        yield return StartCoroutine(InitializeWithStatus());

        while (totalRounds > currentRound)
        {
            yield return StartCoroutine(PlayRound());
            currentRound++;

            if (totalRounds > currentRound)
            {
                yield return StartCoroutine(ShowRoundCompleteMessage());
            }
        }

        yield return StartCoroutine(EndBattle());
    }

    private IEnumerator PlayRound()
    {
        ClearRoundUI();
        Coroutine showReactions = StartCoroutine(ShowReactionsCoroutine());

        yield return StartCoroutine(PlayerRapSubmissionLoop());
        StopCoroutine(showReactions);

        yield return StartCoroutine(PlayNPCTurn());
        yield return StartCoroutine(ShowJudgeFeedback());
    }

    private void ClearRoundUI()
    {
        if (uiManager != null)
        {
            uiManager.ClearComputerText();
            uiManager.ClearAIJudgeText();
            uiManager.ClearAllReactions();
        }
    }

    private IEnumerator PlayNPCTurn()
    {
        Coroutine showReactions = StartCoroutine(ShowReactionsCoroutine());
        Coroutine createAudio = StartCoroutine(geminiLiveAIRapper.CreateAudioCoroutines());
        Coroutine playAudio = StartCoroutine(geminiLiveAIRapper.PlayAudioCoroutine());
        Coroutine streamAudio = StartCoroutine(
            geminiLiveAIRapper.StreamToOtherAgent(geminiLiveAIJudge)
        );

        yield return new WaitUntil(() => geminiLiveAIRapper.IsPlaying);

        if (uiManager != null)
        {
            uiManager.UpdateStatus("NPC is rapping!");
        }

        if (animationController != null)
        {
            animationController.SetTrigger("StartRapping");
        }

        yield return StartCoroutine(geminiLiveAIRapper.waitForAudioStreamFinish());

        if (animationController != null)
        {
            animationController.SetTrigger("ReturnToIdle");
        }

        StopCoroutine(createAudio);
        StopCoroutine(playAudio);
        StopCoroutine(showReactions);
    }

    private IEnumerator ShowJudgeFeedback()
    {
        if (geminiLiveAIJudge != null)
        {
            yield return StartCoroutine(
                geminiLiveAIJudge.SendTextToGeminiCoroutine(
                    $"Round {currentRound + 1} ended. Give round score."
                )
            );
        }

        if (uiManager != null)
        {
            uiManager.UpdateStatus("Let's see what the judge thinks! wait for it...");
        }

        yield return StartCoroutine(WaitForAllMessages());
        yield return StartCoroutine(uiManager.ShowJudgePanelTemporarily(JUDGE_PANEL_DISPLAY_TIME));

        if (uiManager != null)
        {
            uiManager.ClearAllReactions();
            uiManager.RemovePlayingReactions();
        }
    }

    private IEnumerator WaitForAllMessages()
    {
        if (geminiLiveAIJudge != null)
        {
            yield return StartCoroutine(
                geminiLiveAIJudge.WaitForAllMessagesToBeSent(MESSAGE_WAIT_TIMEOUT)
            );
        }

        if (geminiLiveAIRapper != null)
        {
            yield return StartCoroutine(
                geminiLiveAIRapper.WaitForAllMessagesToBeSent(MESSAGE_WAIT_TIMEOUT)
            );
        }
    }

    private IEnumerator ShowRoundCompleteMessage()
    {
        if (uiManager != null)
        {
            uiManager.UpdateStatus($"Finished round number {currentRound} of {totalRounds}");
        }
        yield return new WaitForSeconds(ROUND_COMPLETE_DISPLAY_TIME);
    }

    private IEnumerator EndBattle()
    {
        if (uiManager != null)
        {
            uiManager.ClearAIJudgeText();
            uiManager.ClearComputerText();
        }

        if (geminiLiveAIJudge != null)
        {
            yield return StartCoroutine(
                geminiLiveAIJudge.SendTextToGeminiCoroutine("rap session over.")
            );
        }

        if (uiManager != null)
        {
            uiManager.UpdateStatus("Finished! Let's wait for the judge to decide the winner!");
        }

        yield return new WaitForSeconds(CONNECTION_STATUS_DISPLAY_TIME);
        yield return StartCoroutine(uiManager.ShowJudgePanelTemporarily(JUDGE_PANEL_DISPLAY_TIME));

        if (uiManager != null)
        {
            uiManager.UpdateStatus("Press space or tap anywhere to return to the main menu.");
        }

        yield return new WaitUntil(() =>
            Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)
        );

        TerminateLiveSession();
    }
    #endregion

    #region UI Reactions
    private IEnumerator ShowReactionsCoroutine()
    {
        if (uiManager == null)
        {
            yield break;
        }

        uiManager.ClearAllReactions();
        uiManager.RemovePlayingReactions();

        PopUpGameText firstReaction = null;
        yield return new WaitUntil(() =>
        {
            firstReaction = uiManager.PopNextReaction();
            return firstReaction != null;
        });

        if (firstReaction != null && firstReaction.gameObject != null)
        {
            firstReaction.gameObject.SetActive(false);
        }

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
                PopUpGameText nextReaction = uiManager.PopNextReaction();
                if (nextReaction != null && nextReaction.gameObject != null)
                {
                    nextReaction.gameObject.SetActive(false);
                }
            }
            yield return new WaitForSeconds(REACTION_POLL_INTERVAL);
        }
    }
    #endregion

    #region Player Recording
    private IEnumerator PlayerRapSubmissionLoop()
    {
        while (true)
        {
            if (useFileSubmission && fileSubmissionClip != null)
            {
                HandleFileSubmission();
            }

            yield return StartCoroutine(GetRapRecording());

            if (playBackRecording)
            {
                yield return StartCoroutine(PlayBackRecording());
            }

            bool opponentResponded = false;
            yield return StartCoroutine(
                WaitForOpponentResponse((responded) => opponentResponded = responded)
            );

            if (opponentResponded)
            {
                break;
            }
        }
    }

    private void HandleFileSubmission()
    {
        if (uiManager != null)
        {
            uiManager.UpdateStatus("Using file submission for this round.");
        }
        playerRecordingClip = fileSubmissionClip;
    }

    private IEnumerator WaitForOpponentResponse(System.Action<bool> resultCallback)
    {
        if (animationController != null)
        {
            animationController.SetTrigger("StartThinking");
        }

        if (uiManager != null)
        {
            uiManager.UpdateStatus("Waiting for your opponent to respond...");
        }

        float startTime = Time.time;
        while (Time.time - startTime < OPPONENT_RESPONSE_TIMEOUT)
        {
            if (geminiLiveAIRapper != null && geminiLiveAIRapper.IsAudioActive())
            {
                resultCallback?.Invoke(true);
                yield break;
            }
            yield return new WaitForSeconds(OPPONENT_CHECK_INTERVAL);
        }

        if (uiManager != null)
        {
            uiManager.UpdateStatus("Opponent response timed out. Try again.");
        }
        yield return new WaitForSeconds(SHORT_RECORDING_WARNING_TIME);
        resultCallback?.Invoke(false);
    }

    private IEnumerator GetRapRecording()
    {
        if (geminiLiveAIRapper == null)
        {
            yield break;
        }

        yield return StartCoroutine(geminiLiveAIRapper.SendActivityStartToGeminiCoroutine());

        bool rapSubmitted = false;
        while (!rapSubmitted)
        {
            yield return StartCoroutine(RecordPlayerRap());
            bool retakeRequested = false;
            yield return StartCoroutine(
                WaitForSubmissionConfirmation((result) => retakeRequested = result)
            );

            if (!retakeRequested)
            {
                rapSubmitted = true;
            }
        }

        yield return StartCoroutine(FinalizeRapSubmission());
    }

    private IEnumerator WaitForSubmissionConfirmation(System.Action<bool> resultCallback)
    {
        float submitEndTime = Time.time + submitRapPeriodInSeconds;
        bool retakeRequested = false;

        while (Time.time < submitEndTime)
        {
            if (uiManager != null)
            {
                uiManager.UpdateStatus(
                    $"Submitting rap in {Mathf.CeilToInt(submitEndTime - Time.time)} seconds."
                );
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                retakeRequested = true;
                break;
            }
            yield return new WaitForSeconds(INPUT_CHECK_INTERVAL);
        }

        resultCallback?.Invoke(retakeRequested);
    }

    private IEnumerator FinalizeRapSubmission()
    {
        if (geminiLiveAIRapper != null)
        {
            yield return StartCoroutine(geminiLiveAIRapper.SendTextToGeminiCoroutine("finalized"));
            yield return StartCoroutine(geminiLiveAIRapper.SendActivityEndToGeminiCoroutine());
        }

        if (geminiLiveAIJudge != null)
        {
            yield return StartCoroutine(
                geminiLiveAIJudge.SendTextToGeminiCoroutine("finalized rapper 1")
            );
        }
    }

    private IEnumerator WaitForRecordingStart()
    {
        if (recordButton != null)
        {
            recordButton.interactable = true;
        }

        yield return new WaitUntil(() =>
            Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)
        );

        if (recordButton != null)
        {
            recordButton.interactable = false;
        }
    }

    private IEnumerator RecordPlayerRap()
    {
        bool submitRecording = false;
        bool firstAttempt = true;

        while (!submitRecording)
        {
            if (!firstAttempt)
            {
                yield return StartCoroutine(DiscardPreviousAttempt());
            }

            yield return StartCoroutine(StartRecordingProcess());

            yield return StartCoroutine(MonitorRecording());

            if (!ValidateRecordingLength(recordingLengthInSeconds))
            {
                yield return new WaitForSeconds(SHORT_RECORDING_WARNING_TIME);
                firstAttempt = false;
                continue;
            }

            submitRecording = true;
            firstAttempt = false;
        }
    }

    private IEnumerator DiscardPreviousAttempt()
    {
        if (geminiLiveAIRapper != null)
        {
            yield return StartCoroutine(geminiLiveAIRapper.SendTextToGeminiCoroutine("discard"));
        }

        if (geminiLiveAIJudge != null)
        {
            yield return StartCoroutine(
                geminiLiveAIJudge.SendTextToGeminiCoroutine("discard rapper 1")
            );
        }
    }

    private IEnumerator StartRecordingProcess()
    {
        if (uiManager != null)
        {
            uiManager.UpdateStatus("Press and hold the button to record your rap.");
        }

        yield return StartCoroutine(WaitForRecordingStart());
        StartMicrophoneRecording();

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

        if (uiManager != null)
        {
            uiManager.UpdateStatus("Recording your rap! Release to stop recording.");
        }
    }

    private IEnumerator MonitorRecording()
    {
        float startTime = Time.time;
        float warningThreshold = maxPlayerRecordingLengthInSeconds - RECORDING_WARNING_THRESHOLD;

        while (!Input.GetKeyUp(KeyCode.Space) && !Input.GetMouseButtonUp(0) && isRecording)
        {
            float elapsed = Time.time - startTime;

            if (elapsed >= maxPlayerRecordingLengthInSeconds)
            {
                if (uiManager != null)
                {
                    uiManager.UpdateStatus("Maximum recording length reached. Stopping recording.");
                }
                break;
            }
            else if (elapsed >= warningThreshold)
            {
                float secondsLeft = maxPlayerRecordingLengthInSeconds - elapsed;
                if (uiManager != null)
                {
                    uiManager.UpdateStatus(
                        $"Stopping recording in {Mathf.CeilToInt(secondsLeft)} seconds."
                    );
                }
            }
            yield return null;
        }

        StopMicrophoneRecording();
        recordingLengthInSeconds = Time.time - startTime;
    }

    private bool ValidateRecordingLength(float duration)
    {
        if (duration < minRecordingLengthInSeconds)
        {
            if (uiManager != null)
            {
                uiManager.UpdateStatus(
                    $"Recording too short. Please record at least {minRecordingLengthInSeconds} second."
                );
            }
            return false;
        }
        return true;
    }
    #endregion

    #region Audio Streaming
    private bool ValidateAudioStreamInputs(GeminiLiveWebRTC agent, AudioClip clip = null)
    {
        if (agent == null)
        {
            Debug.LogError("Gemini agent is null");
            return false;
        }

        return clip != null;
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
        if (agent != null && !string.IsNullOrEmpty(message))
        {
            yield return StartCoroutine(agent.SendTextToGeminiCoroutine(message));
        }
    }

    private IEnumerator ProcessAndSendAudioSamples(
        GeminiLiveWebRTC agent,
        AudioClip clip,
        int lastSamplePosition,
        int currentSamplePosition
    )
    {
        int samplesToGet = currentSamplePosition - lastSamplePosition;
        if (samplesToGet > 0 && agent != null && clip != null)
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
        if (agent == null || clip == null)
        {
            yield break;
        }

        if (resizeClip > 0 && clip.samples != resizeClip)
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
            agent.SendSilenceToGeminiCoroutine(
                FINAL_SILENCE_DURATION,
                clip.frequency,
                clip.channels
            )
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

        if (waitCondition != null)
        {
            yield return new WaitUntil(waitCondition);
        }

        yield return StartCoroutine(StreamAudioLoop(agent, clip, getPosition, isActive));
        yield return StartCoroutine(
            SendFinalAudioData(agent, clip, getPosition, 0, recordingLengthInSeconds)
        );
        yield return StartCoroutine(SendInitialMessage(agent, endText));
    }

    private IEnumerator StreamAudioLoop(
        GeminiLiveWebRTC agent,
        AudioClip clip,
        Func<int> getPosition,
        Func<bool> isActive
    )
    {
        int lastSamplePosition = 0;
        float lastSendTime = Time.time;
        float lastPositionCheckTime = Time.time;

        while (isActive())
        {
            if (Time.time - lastPositionCheckTime >= POSITION_CHECK_INTERVAL)
            {
                int currentSamplePosition = getPosition();
                if (currentSamplePosition < lastSamplePosition)
                {
                    currentSamplePosition += clip.samples;
                }

                if (Time.time - lastSendTime >= AUDIO_SEND_INTERVAL)
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
                lastPositionCheckTime = Time.time;
            }
            yield return new WaitForSeconds(POSITION_CHECK_INTERVAL);
        }
    }
    #endregion

    #region Microphone Recording
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
        {
            return;
        }

        Microphone.End(microphoneDevice);
        isRecording = false;
    }
    #endregion

    #region Audio Playback
    private IEnumerator PlayBackRecording()
    {
        if (uiManager != null)
        {
            uiManager.UpdateStatus("Playing back your recording...");
        }

        if (playerRecordingClip == null)
        {
            yield break;
        }

        if (musicSource != null)
        {
            musicSource.Pause();
        }

        AudioSource playbackSource = gameObject.AddComponent<AudioSource>();
        playbackSource.clip = playerRecordingClip;
        playbackSource.Play();

        yield return new WaitForSeconds(recordingLengthInSeconds);

        Destroy(playbackSource);

        if (musicSource != null)
        {
            musicSource.UnPause();
        }
    }
    #endregion
}
