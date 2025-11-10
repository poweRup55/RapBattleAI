using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thread-safe message queue processor for managing and processing messages.
/// Handles queuing, dequeuing, batch processing, and timeout handling.
/// </summary>
public class MessageQueueProcessor
{
    private readonly Queue<object> messagesQueue = new Queue<object>();
    private readonly object queueLock = new object();
    private bool enableDebugLogs;

    public int Count
    {
        get
        {
            lock (queueLock)
            {
                return messagesQueue.Count;
            }
        }
    }

    public bool IsEmpty
    {
        get
        {
            lock (queueLock)
            {
                return messagesQueue.Count == 0;
            }
        }
    }

    public MessageQueueProcessor(bool enableDebugLogs = true)
    {
        this.enableDebugLogs = enableDebugLogs;
    }

    /// <summary>
    /// Enqueues a message to be processed.
    /// </summary>
    public void Enqueue(object message)
    {
        if (message == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("MessageQueueProcessor: Attempted to enqueue null message");
            return;
        }

        lock (queueLock)
        {
            messagesQueue.Enqueue(message);
        }
    }

    /// <summary>
    /// Dequeues the next message from the queue.
    /// </summary>
    public object Dequeue()
    {
        lock (queueLock)
        {
            if (messagesQueue.Count > 0)
            {
                return messagesQueue.Dequeue();
            }
            return null;
        }
    }

    /// <summary>
    /// Processes messages from the queue using the provided processor coroutine.
    /// Optimized to reduce lock contention by minimizing lock duration.
    /// </summary>
    public IEnumerator ProcessQueue(
        MonoBehaviour coroutineRunner,
        Func<object, IEnumerator> messageProcessor
    )
    {
        const float checkInterval = 0.05f; // Check every 50ms to reduce CPU usage

        while (true)
        {
            object message = null;

            // Lock only when dequeuing
            lock (queueLock)
            {
                if (messagesQueue.Count > 0)
                {
                    message = messagesQueue.Dequeue();
                }
            }

            if (message != null && messageProcessor != null)
            {
                yield return coroutineRunner.StartCoroutine(messageProcessor(message));
            }
            else
            {
                yield return new WaitForSeconds(checkInterval);
            }
        }
    }

    /// <summary>
    /// Waits for all messages in the queue to be processed, with optional timeout.
    /// </summary>
    public IEnumerator WaitForEmpty(float timeoutSeconds = 30f)
    {
        float elapsedTime = 0f;
        int initialMessageCount = Count;

        if (initialMessageCount == 0)
        {
            yield break;
        }

        while (elapsedTime < timeoutSeconds)
        {
            int currentMessageCount = Count;

            if (currentMessageCount == 0)
            {
                yield break;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        int remainingMessages = Count;

        if (enableDebugLogs && remainingMessages > 0)
            Debug.LogWarning(
                $"MessageQueueProcessor: Timeout - {remainingMessages} messages still pending"
            );
    }

    /// <summary>
    /// Clears all messages from the queue.
    /// </summary>
    public void Clear()
    {
        int messageCount = 0;
        lock (queueLock)
        {
            messageCount = messagesQueue.Count;
            messagesQueue.Clear();
        }

        if (enableDebugLogs && messageCount > 0)
            Debug.Log($"MessageQueueProcessor: Cleared {messageCount} pending messages");
    }

    /// <summary>
    /// Gets a snapshot of the current queue count (thread-safe).
    /// </summary>
    public int GetCount()
    {
        lock (queueLock)
        {
            return messagesQueue.Count;
        }
    }
}

