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
    private GeminiLiveWebRTC geminiLiveWebRTC;

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
    private UIMenuController uIMenuController;

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

    private readonly string microphoneDevice;
    private bool isRecording = false;
    private int currentRound = 0;

    private void Start()
    {
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
        geminiLiveWebRTC.Destroy();
        uIMenuController.ShowMainMenu();
    }

    private IEnumerator BattleLoop()
    {
        yield return StartCoroutine(InitializeWithStatus());

        while (totalRounds > currentRound)
        {
            uiManager.ClearComputerText();
            yield return StartCoroutine(BeginPlayerRapSubmission());

            // NPC Turn
            currentState = BattleState.NPCTurn;

            Debug.Log("NPC is rapping...");
            var createAudioCoroutine = StartCoroutine(geminiLiveWebRTC.CreateAudioCoroutines());
            var playAudioCoroutine = StartCoroutine(geminiLiveWebRTC.PlayAudioCoroutine());
            yield return new WaitUntil(() => geminiLiveWebRTC.IsPlaying);
            uiManager.UpdateStatus($"NPC is rapping!");
            animationController.SetTrigger("StartRapping");
            yield return StartCoroutine(geminiLiveWebRTC.waitForAudioStreamFinish());
            yield return new WaitForSeconds(2f);
            animationController.SetTrigger("ReturnToIdle");
            StopCoroutine(createAudioCoroutine);
            StopCoroutine(playAudioCoroutine);
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
        Coroutine init = StartCoroutine(geminiLiveWebRTC.Initialize());
        yield return new WaitForSeconds(2f);
        yield return init;
        uiManager.UpdateStatus("Connected to Rap Battle Servers!");
        yield return new WaitForSeconds(2f);
    }

    private IEnumerator BeginPlayerRapSubmission()
    {
        while (true)
        {
            currentState = BattleState.PlayerTurn;
            uiManager.UpdateStatus(
                "Hold the space bar or tap and hold anywhere to begin recording your rap!"
            );
            if (useFileSubmission && fileSubmissionClip != null)
            {
                uiManager.UpdateStatus("Using file submission for this round.");
                playerRecordingClip = fileSubmissionClip;
            }
            else
            {
                yield return StartCoroutine(GetRapRecording());
            }
            if (playBackRecording)
            {
                yield return PlayBackRecording();
            }
            currentState = BattleState.WaitingTurn;
            animationController.SetTrigger("StartThinking");
            uiManager.UpdateStatus("Waiting for your opponent to respond...");
            yield return StartCoroutine(
                geminiLiveWebRTC.SendAudioToGeminiCoroutine(playerRecordingClip)
            );
            float startTime = Time.time;
            geminiLiveWebRTC.WaitForAudioReception();
            while (Time.time - startTime < 60f)
            {
                if (geminiLiveWebRTC.IsReceivingAudioData)
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
        recordButton.interactable = true;

        while (!Input.GetKeyDown(KeyCode.Space) && !Input.GetMouseButtonDown(0))
        {
            yield return null;
        }
        recordButton.interactable = false;

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
        TrimRecordingToActualLength();
        ResamplePlayerRecording();
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
