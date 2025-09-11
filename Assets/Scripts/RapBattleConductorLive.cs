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
    public BattleState CurrentState => currentState;
    private AudioClip playerRecordingClip;
    private float recordingLengthInSeconds;

    private string microphoneDevice;
    private bool isRecording = false;
    private int currentRound = 0;

    private void Start()
    {
        microphoneDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        if (useFileSubmission)
        {
            fileSubmissionClip = AudioClipResampler.ResampleAudio(
                fileSubmissionClip,
                AILiveConfig.inputSampleRate
            );
        }
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

    private IEnumerator BattleLoop()
    {
        yield return StartCoroutine(InitializeWithStatus());

        while (totalRounds > currentRound)
        {
            uiManager.ClearComputerText();
            yield return StartCoroutine(PlayerRapSubmissionLoop());

            // NPC Turn
            currentState = BattleState.NPCTurn;

            Debug.Log("NPC is rapping...");
            var createAudioCoroutine = StartCoroutine(geminiLiveAIRapper.CreateAudioCoroutines());
            var playAudioCoroutine = StartCoroutine(geminiLiveAIRapper.PlayAudioCoroutine());
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
        uiManager.UpdateStatus("Finished! Let's wait for the judge to decide the winner!");

        uiManager.UpdateStatus("Press space or tap anywhere to return to the main menu.");
        while (!Input.GetKeyDown(KeyCode.Space) && !Input.GetMouseButtonDown(0))
        {
            yield return null;
        }
        TerminateLiveSession();
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
            while (Time.time - startTime < 60f)
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
        uiManager.UpdateStatus("Press and hold the button to record your rap.");
        yield return StartCoroutine(WaitForRecordingStart());
        StartCoroutine(geminiLiveAIRapper.SendActivityStartToGeminiCoroutine());

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
        StartCoroutine(geminiLiveAIRapper.SendActivityEndToGeminiCoroutine());

        StartCoroutine(geminiLiveAIJudge.SendTextToGeminiCoroutine("finalized"));
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

    private void ResamplePlayerRecording()
    {
        playerRecordingClip = AudioClipResampler.ResampleAudio(
            playerRecordingClip,
            AILiveConfig.inputSampleRate
        );
    }

    private IEnumerator RecordPlayerRap()
    {
        StartMicrophoneRecording();
        var startTime = Time.time;

        StartCoroutine(geminiLiveAIRapper.SendActivityStartToGeminiCoroutine());
        StartCoroutine(geminiLiveAIRapper.SendTextToGeminiCoroutine("attempt"));
        StartCoroutine(StreamAudioToGemini(geminiLiveAIRapper));

        StartCoroutine(geminiLiveAIJudge.SendTextToGeminiCoroutine("attempt"));
        StartCoroutine(StreamAudioToGemini(geminiLiveAIJudge));

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
        Debug.Log(
            $"Recording length: {recordingLengthInSeconds} seconds, max allowed: {maxPlayerRecordingLengthInSeconds} seconds."
        );
        StopMicrophoneRecording();

        if (recordingLengthInSeconds < minRecordingLengthInSeconds)
        {
            uiManager.UpdateStatus(
                $"Recording too short. Please record at least {minRecordingLengthInSeconds} second."
            );
            yield return new WaitForSeconds(2f);
            yield return StartCoroutine(RecordPlayerRap());
        }
    }

    private IEnumerator StreamAudioToGemini(GeminiLiveWebRTC geminiLiveAgent)
    {
        if (microphoneDevice == null || playerRecordingClip == null)
        {
            Debug.LogError("Microphone device or recording clip is null");
            yield break;
        }
        const float sendInterval = 0.1f; // Send audio data every 0.1 seconds
        int lastSamplePosition = 0;
        float lastSendTime = Time.time;

        while (isRecording)
        {
            int currentSamplePosition = Microphone.GetPosition(microphoneDevice);
            if (currentSamplePosition < lastSamplePosition)
            {
                currentSamplePosition += playerRecordingClip.samples;
            }
            int samplesToGet = currentSamplePosition - lastSamplePosition;

            if (samplesToGet > 0 && Time.time - lastSendTime >= sendInterval)
            {
                float[] samples = new float[samplesToGet * playerRecordingClip.channels];
                playerRecordingClip.GetData(
                    samples,
                    lastSamplePosition % playerRecordingClip.samples
                );
                StartCoroutine(geminiLiveAgent.SendAudioToGeminiCoroutine(samples));
                lastSamplePosition = currentSamplePosition % playerRecordingClip.samples;
                lastSendTime = Time.time;
            }

            yield return null;
        }

        // Send any remaining audio data after recording stops
        int finalSamplePosition = Microphone.GetPosition(microphoneDevice);
        if (finalSamplePosition < lastSamplePosition)
        {
            finalSamplePosition += playerRecordingClip.samples;
        }
        int finalSamplesToGet = finalSamplePosition - lastSamplePosition;

        if (finalSamplesToGet > 0)
        {
            float[] finalSamples = new float[finalSamplesToGet * playerRecordingClip.channels];
            playerRecordingClip.GetData(
                finalSamples,
                lastSamplePosition % playerRecordingClip.samples
            );
            StartCoroutine(geminiLiveAgent.SendAudioToGeminiCoroutine(finalSamples));
        }
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

    private void TrimRecordingToActualLength()
    {
        if (playerRecordingClip == null || recordingLengthInSeconds <= 0f)
            return;

        int trimmedSamples = Mathf.FloorToInt(
            recordingLengthInSeconds * playerRecordingClip.frequency
        );
        trimmedSamples = Mathf.Min(trimmedSamples, playerRecordingClip.samples); // Don't exceed original
        if (trimmedSamples <= 0)
            trimmedSamples = playerRecordingClip.samples;

        float[] samples = new float[trimmedSamples * playerRecordingClip.channels];
        playerRecordingClip.GetData(samples, 0);

        var newPlayerRecordingClip = AudioClip.Create(
            "PlayerRecordingTrimmed",
            trimmedSamples,
            playerRecordingClip.channels,
            playerRecordingClip.frequency,
            false
        );
        newPlayerRecordingClip.SetData(samples, 0);
        playerRecordingClip = newPlayerRecordingClip;
    }
}
