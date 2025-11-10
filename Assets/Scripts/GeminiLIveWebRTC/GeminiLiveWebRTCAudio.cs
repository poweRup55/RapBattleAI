using System;
using System.Collections;
using EpicRapBattle.Config;
using UnityEngine;

public abstract class GeminiLiveWebRTCAudio : GeminiLiveWebRTC
{
    [SerializeField]
    private int audioBufferFlushThreshold = 70;

    [Header("Audio")]
    [SerializeField]
    private AudioSource audioSource;

    private const int ORIGINAL_SAMPLE_RATE = 24000;
    private const float STREAM_CHECK_INTERVAL = 0.1f;
    private const float SILENCE_DURATION = 2f;
    private const int SILENCE_SAMPLE_RATE = 24000;
    private const int SILENCE_CHANNELS = 1;

    private AudioResponseProcessor audioProcessor;
    private Coroutine audioCreateCoroutine;
    private Coroutine audioPlayCoroutine;

    public bool IsPlaying => audioProcessor != null && audioProcessor.IsPlaying;

    protected void Awake()
    {
        InitializeAudioProcessor();
    }

    #region Initialization
    protected override void ResetConnectionState()
    {
        base.ResetConnectionState();
        if (audioProcessor != null)
        {
            audioProcessor.Reset();
        }
    }

    private void InitializeAudioProcessor()
    {
        if (audioSource == null)
        {
            Debug.LogError("GeminiLiveWebRTCAudio: AudioSource is not assigned!");
            return;
        }

        audioProcessor = new AudioResponseProcessor(
            audioSource,
            ORIGINAL_SAMPLE_RATE,
            audioBufferFlushThreshold,
            enableDebugLogs
        );
    }
    #endregion

    #region Response Processing
    protected override void ProcessGeminiResponse(string json)
    {
        base.ProcessGeminiResponse(json);

        if (audioProcessor == null)
        {
            InitializeAudioProcessor();
        }

        ProcessAudioResponse(json);
    }

    private void ProcessAudioResponse(string json)
    {
        try
        {
            BidiGenerateContentServerMessage response = GeminiResponseParser.ParseServerMessage(
                json,
                enableDebugLogs
            );

            if (response?.serverContent?.modelTurn?.parts == null)
                return;

            foreach (var part in response.serverContent.modelTurn.parts)
            {
                if (part.inlineData != null && !string.IsNullOrEmpty(part.inlineData.data))
                {
                    try
                    {
                        byte[] audioData = Convert.FromBase64String(part.inlineData.data);
                        audioProcessor.EnqueueAudioData(audioData);

                        if (enableDebugLogs)
                            Debug.Log(
                                $"Received audio data: {audioData.Length} bytes, MIME: {part.inlineData.mimeType}, queue size: {audioProcessor.ResponseQueueCount}"
                            );
                    }
                    catch (Exception e)
                    {
                        string errorMessage = $"Error decoding audio data: {e.Message}";
                        if (enableDebugLogs)
                            Debug.LogError(errorMessage);

                        throw new GeminiLiveException(
                            "AUDIO_DECODE_ERROR",
                            "ProcessAudioResponse",
                            "DecodeBase64Audio",
                            errorMessage,
                            e
                        );
                    }
                }
            }

            if (GeminiResponseParser.IsTurnComplete(response))
            {
                audioProcessor.MarkAudioStreamFinished();
            }
        }
        catch (GeminiLiveException)
        {
            throw;
        }
        catch (Exception e)
        {
            string errorMessage = $"Error processing audio response: {e.Message}";
            if (enableDebugLogs)
                Debug.LogError(errorMessage);

            throw new GeminiLiveException(
                "RESPONSE_PROCESSING_ERROR",
                "ProcessAudioResponse",
                "ProcessResponse",
                errorMessage,
                e
            );
        }
    }
    #endregion

    #region Audio Playback
    public IEnumerator PlayAudioCoroutine()
    {
        if (audioProcessor == null)
        {
            InitializeAudioProcessor();
        }

        yield return StartCoroutine(audioProcessor.PlayAudioCoroutine(this));
    }

    public IEnumerator CreateAudioCoroutines()
    {
        if (audioProcessor == null)
        {
            InitializeAudioProcessor();
        }

        yield return StartCoroutine(audioProcessor.CreateAudioCoroutines(this));
    }

    public IEnumerator waitForAudioStreamFinish()
    {
        if (audioProcessor == null)
        {
            yield break;
        }

        yield return StartCoroutine(audioProcessor.WaitForAudioStreamFinish());
    }

    public bool IsAudioActive()
    {
        return audioProcessor != null && audioProcessor.IsAudioActive;
    }
    #endregion

    #region Audio Streaming
    public IEnumerator StreamToOtherAgent(GeminiLiveWebRTC agent)
    {
        yield return StartCoroutine(agent.SendTextToGeminiCoroutine("attempt rapper 2"));

        while (IsAudioActive())
        {
            AudioClip clip = audioProcessor?.DequeueAudioClip();
            if (clip != null)
            {
                clip = PrepareAudioClipForStreaming(clip);
                yield return StartCoroutine(StreamAudioClipToAgent(agent, clip));
            }
            else
            {
                yield return new WaitForSeconds(STREAM_CHECK_INTERVAL);
            }
        }

        yield return StartCoroutine(
            agent.SendSilenceToGeminiCoroutine(
                SILENCE_DURATION,
                SILENCE_SAMPLE_RATE,
                SILENCE_CHANNELS
            )
        );
        yield return StartCoroutine(agent.SendTextToGeminiCoroutine("finalized rapper 2"));
    }

    private AudioClip PrepareAudioClipForStreaming(AudioClip clip)
    {
        if (clip.frequency != AILiveConfig.inputSampleRate)
        {
            return AudioClipResampler.ResampleAudio(clip, AILiveConfig.inputSampleRate);
        }
        return clip;
    }

    private IEnumerator StreamAudioClipToAgent(GeminiLiveWebRTC agent, AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);
        yield return StartCoroutine(agent.SendAudioToGeminiCoroutine(samples));

        if (enableDebugLogs)
            Debug.Log($"Streamed audio clip to other agent: {clip.name}");
    }
    #endregion

    #region Cleanup
    public new void Destroy()
    {
        StopAudioCoroutines();

        if (audioProcessor != null)
        {
            audioProcessor.Cleanup();
            audioProcessor = null;
        }

        base.Destroy();
    }

    private void StopAudioCoroutines()
    {
        if (audioCreateCoroutine != null)
        {
            StopCoroutine(audioCreateCoroutine);
            audioCreateCoroutine = null;
        }

        if (audioPlayCoroutine != null)
        {
            StopCoroutine(audioPlayCoroutine);
            audioPlayCoroutine = null;
        }
    }
    #endregion
}
