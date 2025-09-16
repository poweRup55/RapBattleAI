using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public abstract class GeminiLiveWebRTCAudio : GeminiLiveWebRTC
{
    [SerializeField]
    private int audioBufferFlushThreshold = 70;

    [Header("Audio")]
    [SerializeField]
    private AudioSource audioSource;
    private bool finishedAudioStreamIn = false;
    private Queue<byte[]> audioResponseQueue = new Queue<byte[]>();
    private Queue<IEnumerator> audioCoroutineQueue = new Queue<IEnumerator>();
    public List<AudioClip> responseAudioClips = new List<AudioClip>();

    public bool IsPlaying => audioSource.isPlaying;

    protected override void ResetConnectionState()
    {
        base.ResetConnectionState();
        finishedAudioStreamIn = false;
        audioResponseQueue = new Queue<byte[]>();
        audioCoroutineQueue = new Queue<IEnumerator>();
        responseAudioClips.Clear();
    }

    protected override void ProcessGeminiResponse(string json)
    {
        base.ProcessGeminiResponse(json);

        // Additional audio processing
        try
        {
            BidiGenerateContentServerMessage response =
                JsonConvert.DeserializeObject<BidiGenerateContentServerMessage>(
                    json,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }
                );

            if (response != null && response.serverContent?.modelTurn?.parts != null)
            {
                foreach (var part in response.serverContent.modelTurn.parts)
                {
                    // Handle audio response
                    if (part.inlineData != null && !string.IsNullOrEmpty(part.inlineData.data))
                    {
                        try
                        {
                            byte[] audioData = Convert.FromBase64String(part.inlineData.data);

                            lock (audioResponseQueue)
                            {
                                audioResponseQueue.Enqueue(audioData);
                            }

                            if (enableDebugLogs)
                                Debug.Log(
                                    $"Received audio data: {audioData.Length} bytes, MIME: {part.inlineData.mimeType}, queue size: {audioResponseQueue.Count}"
                                );
                        }
                        catch (Exception e)
                        {
                            string errorMessage = $"Error decoding audio data: {e.Message}";
                            if (enableDebugLogs)
                                Debug.LogError(errorMessage);

                            throw new GeminiLiveException(
                                "AUDIO_DECODE_ERROR",
                                "ProcessGeminiResponse",
                                "DecodeBase64Audio",
                                errorMessage,
                                e
                            );
                        }
                    }
                }
            }

            // Check for turn completion
            if (response.turnComplete || (response.serverContent?.turnComplete == true))
            {
                finishedAudioStreamIn = true;
            }
        }
        catch (Exception e)
        {
            string errorMessage = $"Error processing Gemini response: {e.Message}";
            if (enableDebugLogs)
                Debug.LogError(errorMessage);

            throw new GeminiLiveException(
                "RESPONSE_PROCESSING_ERROR",
                "ProcessGeminiResponse",
                "ProcessResponse",
                errorMessage,
                e
            );
        }
    }

    public IEnumerator PlayAudioCoroutine()
    {
        while (IsWebSocketConnected())
        {
            IEnumerator audioCoroutine = null;
            lock (audioCoroutineQueue)
            {
                if (audioCoroutineQueue.Count > 0)
                    audioCoroutine = audioCoroutineQueue.Dequeue();
            }
            if (audioCoroutine != null)
                yield return StartCoroutine(audioCoroutine);
            else
                yield return null;
        }
    }

    public IEnumerator CreateAudioCoroutines()
    {
        finishedAudioStreamIn = false;
        float timeSinceLastFlush = 0f;
        const float maxWaitTime = 1f;

        while (IsWebSocketConnected())
        {
            bool shouldProcess = false;
            int queueCount = 0;

            lock (audioResponseQueue)
            {
                queueCount = audioResponseQueue.Count;
                shouldProcess =
                    queueCount > audioBufferFlushThreshold
                    || (timeSinceLastFlush >= maxWaitTime && queueCount > 0);
            }

            if (shouldProcess)
            {
                StartCoroutine(ProcessAudioQueue());
                timeSinceLastFlush = 0f;
            }
            else
            {
                timeSinceLastFlush += Time.deltaTime;
            }

            yield return null;
        }
    }

    private IEnumerator ProcessAudioQueue()
    {
        int totalBytes = 0;
        byte[][] audioChunks;

        lock (audioResponseQueue)
        {
            if (audioResponseQueue.Count == 0)
            {
                yield break;
            }

            audioChunks = new byte[audioResponseQueue.Count][];
            int index = 0;

            while (audioResponseQueue.Count > 0)
            {
                byte[] chunk = audioResponseQueue.Dequeue();
                audioChunks[index] = chunk;
                totalBytes += chunk.Length;
                index++;
            }
        }

        if (totalBytes > 0)
        {
            byte[] audioData = new byte[totalBytes];
            int offset = 0;

            for (int i = 0; i < audioChunks.Length; i++)
            {
                if (audioChunks[i] != null)
                {
                    Buffer.BlockCopy(audioChunks[i], 0, audioData, offset, audioChunks[i].Length);
                    offset += audioChunks[i].Length;
                }
            }

            try
            {
                float[] samples = WavUtility.ConvertPCMToFloat(audioData);

                AudioClip responseClip = AudioClip.Create(
                    "GeminiResponse",
                    samples.Length,
                    1,
                    24000,
                    false
                );
                responseClip.SetData(samples, 0);

                lock (responseAudioClips)
                {
                    responseAudioClips.Add(responseClip);
                }

                lock (audioCoroutineQueue)
                {
                    audioCoroutineQueue.Enqueue(PlayAudioResponse(responseClip));
                }
            }
            catch (Exception e)
            {
                if (enableDebugLogs)
                    Debug.LogError($"Error processing audio data: {e.Message}");
            }
        }

        yield return null;
    }

    private IEnumerator PlayAudioResponse(AudioClip responseClip)
    {
        if (responseClip == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("Attempted to play null AudioClip");
            yield break;
        }

        audioSource.clip = responseClip;
        audioSource.Play();

        yield return new WaitForSeconds(responseClip.length);
    }

    public IEnumerator waitForAudioStreamFinish()
    {
        while (IsAudioActive())
        {
            yield return new WaitForSeconds(5f);
        }
        yield return null;
    }

    public bool IsAudioActive()
    {
        bool isPlaying = audioSource != null && audioSource.isPlaying;

        int coroutineQueueCount = 0;
        int responseQueueCount = 0;

        lock (audioCoroutineQueue)
        {
            coroutineQueueCount = audioCoroutineQueue.Count;
        }

        lock (audioResponseQueue)
        {
            responseQueueCount = audioResponseQueue.Count;
        }

        return isPlaying
            || coroutineQueueCount > 0
            || responseQueueCount > 0
            || !finishedAudioStreamIn;
    }

    public new void Destroy()
    {
        base.Destroy();
        responseAudioClips.Clear();
    }
}
