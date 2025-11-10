using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles audio response processing including queue management, AudioClip creation, and playback coordination.
/// Manages audio data queues, coroutine queues, and AudioClip lifecycle.
/// </summary>
public class AudioResponseProcessor
{
    private const float AUDIO_CHECK_INTERVAL = 0.05f;
    private const float MAX_WAIT_TIME = 2f;

    private readonly Queue<byte[]> audioResponseQueue = new Queue<byte[]>();
    private readonly Queue<IEnumerator> audioCoroutineQueue = new Queue<IEnumerator>();
    private readonly Queue<AudioClip> audioClipQueue = new Queue<AudioClip>();
    private readonly object audioResponseQueueLock = new object();
    private readonly object audioCoroutineQueueLock = new object();
    private readonly object audioClipQueueLock = new object();

    private AudioSource audioSource;
    private int originalSampleRate = 24000;
    private int audioBufferFlushThreshold = 30;
    private bool finishedAudioStreamIn = false;
    private bool enableDebugLogs;
    private bool hasReceivedFirstAudio = false;
    private bool isProcessingQueue = false;

    public bool IsPlaying => audioSource != null && audioSource.isPlaying;
    public bool IsAudioActive =>
        IsPlaying
        || GetCoroutineQueueCount() > 0
        || GetResponseQueueCount() > 0
        || !finishedAudioStreamIn;

    public AudioResponseProcessor(
        AudioSource audioSource,
        int originalSampleRate = 24000,
        int audioBufferFlushThreshold = 70,
        bool enableDebugLogs = true
    )
    {
        this.audioSource = audioSource;
        this.originalSampleRate = originalSampleRate;
        this.audioBufferFlushThreshold = audioBufferFlushThreshold;
        this.enableDebugLogs = enableDebugLogs;
    }

    /// <summary>
    /// Enqueues audio data bytes for processing.
    /// </summary>
    public void EnqueueAudioData(byte[] audioData)
    {
        if (audioData == null || audioData.Length == 0)
        {
            if (enableDebugLogs)
                Debug.LogWarning(
                    "AudioResponseProcessor: Attempted to enqueue null or empty audio data"
                );
            return;
        }

        bool isFirstAudio = !hasReceivedFirstAudio;
        hasReceivedFirstAudio = true;

        lock (audioResponseQueueLock)
        {
            audioResponseQueue.Enqueue(audioData);
        }

        if (enableDebugLogs)
            Debug.Log(
                $"AudioResponseProcessor: Enqueued audio data: {audioData.Length} bytes, queue size: {GetResponseQueueCount()}, isFirst: {isFirstAudio}"
            );
    }

    /// <summary>
    /// Marks the audio stream as finished.
    /// </summary>
    public void MarkAudioStreamFinished()
    {
        finishedAudioStreamIn = true;
    }

    /// <summary>
    /// Resets the audio stream state.
    /// </summary>
    public void Reset()
    {
        finishedAudioStreamIn = false;
        hasReceivedFirstAudio = false;
        isProcessingQueue = false;
        lock (audioResponseQueueLock)
        {
            audioResponseQueue.Clear();
        }
        lock (audioCoroutineQueueLock)
        {
            audioCoroutineQueue.Clear();
        }
    }

    /// <summary>
    /// Processes audio queue and creates AudioClips when threshold is reached.
    /// </summary>
    public IEnumerator CreateAudioCoroutines(MonoBehaviour coroutineRunner)
    {
        float timeSinceLastFlush = 0f;

        while (true)
        {
            bool shouldProcess = false;
            int queueCount;

            lock (audioResponseQueueLock)
            {
                queueCount = audioResponseQueue.Count;
                // Flush immediately for first audio, or when threshold/timeout is reached
                shouldProcess =
                    (!hasReceivedFirstAudio && queueCount > 0) // Immediate flush for first audio
                    || queueCount > audioBufferFlushThreshold
                    || (timeSinceLastFlush >= MAX_WAIT_TIME && queueCount > 0);
            }

            if (shouldProcess && !isProcessingQueue)
            {
                isProcessingQueue = true;
                yield return coroutineRunner.StartCoroutine(ProcessAudioQueue());
                isProcessingQueue = false;
                timeSinceLastFlush = 0f;
            }
            else
            {
                timeSinceLastFlush += AUDIO_CHECK_INTERVAL;
            }

            yield return new WaitForSeconds(AUDIO_CHECK_INTERVAL);
        }
    }

    /// <summary>
    /// Plays audio coroutines from the queue.
    /// </summary>
    public IEnumerator PlayAudioCoroutine(MonoBehaviour coroutineRunner)
    {
        while (true)
        {
            IEnumerator audioCoroutine = null;
            lock (audioCoroutineQueueLock)
            {
                if (audioCoroutineQueue.Count > 0)
                    audioCoroutine = audioCoroutineQueue.Dequeue();
            }

            if (audioCoroutine != null)
                yield return coroutineRunner.StartCoroutine(audioCoroutine);
            else
                yield return null;
        }
    }

    /// <summary>
    /// Waits for audio stream to finish.
    /// </summary>
    public IEnumerator WaitForAudioStreamFinish()
    {
        while (IsAudioActive)
        {
            yield return new WaitForSeconds(5f);
        }
        yield return null;
    }

    private IEnumerator ProcessAudioQueue()
    {
        int totalBytes = 0;
        byte[][] audioChunks;

        lock (audioResponseQueueLock)
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
                AudioClip responseClip = CreateAudioClip(audioData);

                lock (audioCoroutineQueueLock)
                {
                    audioCoroutineQueue.Enqueue(PlayAudioResponse(responseClip));
                }
                lock (audioClipQueueLock)
                {
                    audioClipQueue.Enqueue(responseClip);
                }
            }
            catch (Exception e)
            {
                if (enableDebugLogs)
                    Debug.LogError(
                        $"AudioResponseProcessor: Error processing audio data: {e.Message}"
                    );
            }
        }

        yield return null;
    }

    private AudioClip CreateAudioClip(byte[] audioData)
    {
        float[] samples = WavUtility.ConvertPCMToFloat(audioData);

        AudioClip responseClip = AudioClip.Create(
            "GeminiResponse",
            samples.Length,
            1,
            originalSampleRate,
            false
        );
        responseClip.SetData(samples, 0);

        return responseClip;
    }

    private IEnumerator PlayAudioResponse(AudioClip responseClip)
    {
        if (responseClip == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("AudioResponseProcessor: Attempted to play null AudioClip");
            yield break;
        }

        if (audioSource == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("AudioResponseProcessor: AudioSource is null");
            yield break;
        }

        // Wait for current audio to finish playing before starting new clip
        // Use smaller wait interval and check audioSource.time for more accurate timing
        if (audioSource.isPlaying && audioSource.clip != null)
        {
            float remainingTime = audioSource.clip.length - audioSource.time;
            if (remainingTime > 0)
            {
                yield return new WaitForSeconds(remainingTime + 0.01f); // Wait for current clip to finish
            }
            else
            {
                // Clip should have finished, but wait a tiny bit to ensure
                while (audioSource.isPlaying)
                {
                    yield return new WaitForSeconds(0.01f);
                }
            }
        }

        // Play immediately for seamless transition
        audioSource.clip = responseClip;
        audioSource.Play();

        if (enableDebugLogs)
            Debug.Log(
                $"AudioResponseProcessor: Playing audio clip, length: {responseClip.length}s"
            );

        // Wait for clip to finish playing - use audioSource.time for accurate timing
        while (audioSource.isPlaying && audioSource.clip == responseClip)
        {
            // Check if we've played past the clip length (with small buffer for timing precision)
            if (audioSource.time >= responseClip.length - 0.01f)
            {
                break;
            }
            yield return new WaitForSeconds(0.01f);
        }

        // Small wait to ensure audio system has finished
        yield return new WaitForSeconds(0.02f);

        // Clean up after playback
        if (responseClip != null)
        {
            responseClip.UnloadAudioData();
            UnityEngine.Object.Destroy(responseClip);
        }
    }

    /// <summary>
    /// Dequeues an AudioClip from the queue.
    /// </summary>
    public AudioClip DequeueAudioClip()
    {
        lock (audioClipQueueLock)
        {
            if (audioClipQueue.Count > 0)
                return audioClipQueue.Dequeue();
        }
        return null;
    }

    /// <summary>
    /// Cleans up all audio resources.
    /// </summary>
    public void Cleanup()
    {
        lock (audioClipQueueLock)
        {
            while (audioClipQueue.Count > 0)
            {
                AudioClip clip = audioClipQueue.Dequeue();
                if (clip != null)
                {
                    clip.UnloadAudioData();
                    UnityEngine.Object.Destroy(clip);
                }
            }
        }

        Reset();
    }

    private int GetCoroutineQueueCount()
    {
        lock (audioCoroutineQueueLock)
        {
            return audioCoroutineQueue.Count;
        }
    }

    private int GetResponseQueueCount()
    {
        lock (audioResponseQueueLock)
        {
            return audioResponseQueue.Count;
        }
    }

    /// <summary>
    /// Gets the current response queue count (public accessor).
    /// </summary>
    public int ResponseQueueCount => GetResponseQueueCount();
}
