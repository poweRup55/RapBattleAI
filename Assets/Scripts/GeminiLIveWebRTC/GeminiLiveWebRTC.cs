using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using EpicRapBattle.Config;
using Newtonsoft.Json;
using UnityEngine;

public abstract class GeminiLiveWebRTC : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField]
    protected AILiveConfig aiConfig;

    [Header("Gemini Generation Config")]
    [Tooltip("Controls randomness of generation. Typical range: 0.0 - 2.0")]
    protected float temperature = 0.8f;

    [Tooltip("Controls nucleus sampling. Typical range: 0.0 - 1.0")]
    protected float topP = 0.9f;

    [Tooltip("Maximum output tokens for Gemini response")]
    protected int maxOutputTokens = 6000;

    [Tooltip("Number of candidates to generate")]
    protected int candidateCount = 1;

    [Header("Debug")]
    [SerializeField]
    protected bool enableDebugLogs = true;

    protected const int maxChunkSize = 1920;

    private static readonly JsonSerializerSettings cachedJsonSettings = new JsonSerializerSettings
    {
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Ignore,
    };

    protected RapBattleConductorLive rapBattleConductorLive;
    protected ClientWebSocket webSocket;
    protected System.Threading.CancellationTokenSource cancellationTokenSource;
    protected bool isConnected = false;
    protected bool isSetupComplete = false;
    protected bool isSetupSent = false;
    protected bool isReceiving = false;
    protected Coroutine receiveCoroutine = null;
    public bool IsConnected => isConnected;
    public bool IsSetupComplete => isSetupComplete;

    public void SetRapBattleConductor(RapBattleConductorLive conductor)
    {
        rapBattleConductorLive = conductor;
    }

    protected void HandleError(
        string errorType,
        string method,
        string operation,
        string message,
        Exception innerException = null
    )
    {
        if (rapBattleConductorLive != null)
        {
            rapBattleConductorLive.TerminateLiveSession($"Connection Error: {message}");
        }

        throw new GeminiLiveException(errorType, method, operation, message, innerException);
    }

    private Queue<object> messagesQueue = new Queue<object>();

    private string ephemeralKey;

    private const string GEMINI_WEBSOCKET_URL =
        "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateContentConstrained";

    public IEnumerator Initialize()
    {
        if (!aiConfig)
        {
            string errorMessage = "AIConfig is not assigned!";
            HandleError("CONFIG_ERROR", "Initialize", "ValidateAIConfig", errorMessage);
        }

        ResetConnectionState();
        try
        {
            ephemeralKey = aiConfig.GenerateEphemeralKey();
        }
        catch (Exception ex)
        {
            HandleError("CONFIG_ERROR", "Initialize", "GenerateEphemeralKey", ex.Message, ex);
        }
        yield return StartCoroutine(ConnectWebSocketCoroutine());
        StartCoroutine(ProcessMessagesQueue());

        if (enableDebugLogs)
            Debug.Log($"GeminiLive: Initialized - Connected: {isConnected}");
    }

    private IEnumerator ProcessMessagesQueue()
    {
        while (
            IsWebSocketConnected()
            && cancellationTokenSource != null
            && !cancellationTokenSource.IsCancellationRequested
        )
        {
            yield return new WaitUntil(() =>
            {
                lock (messagesQueue)
                {
                    return messagesQueue.Count > 0
                        || !IsWebSocketConnected()
                        || (cancellationTokenSource?.IsCancellationRequested == true);
                }
            });

            object message = null;
            lock (messagesQueue)
            {
                if (messagesQueue.Count > 0)
                {
                    message = messagesQueue.Dequeue();
                }
            }

            if (message != null)
            {
                yield return StartCoroutine(SendWebSocketMessageCoroutine(message));
            }
            message = null;
        }
    }

    public IEnumerator WaitForAllMessagesToBeSent(float timeoutSeconds = 30f)
    {
        float elapsedTime = 0f;
        int initialMessageCount = 0;

        lock (messagesQueue)
        {
            initialMessageCount = messagesQueue.Count;
        }

        if (initialMessageCount == 0)
        {
            yield break;
        }

        while (elapsedTime < timeoutSeconds)
        {
            int currentMessageCount = 0;
            lock (messagesQueue)
            {
                currentMessageCount = messagesQueue.Count;
            }

            // Check if all messages have been sent
            if (currentMessageCount == 0)
            {
                yield break;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Timeout reached
        int remainingMessages = 0;
        lock (messagesQueue)
        {
            remainingMessages = messagesQueue.Count;
        }

        if (enableDebugLogs && remainingMessages > 0)
            Debug.LogWarning($"GeminiLive: Timeout - {remainingMessages} messages still pending");
    }

    protected virtual void ResetConnectionState()
    {
        isConnected = false;
        isSetupComplete = false;
        isSetupSent = false;
        isReceiving = false;
        if (receiveCoroutine != null)
        {
            StopCoroutine(receiveCoroutine);
            receiveCoroutine = null;
        }
    }

    protected virtual void ClearMessageQueue()
    {
        int messageCount = 0;
        lock (messagesQueue)
        {
            messageCount = messagesQueue.Count;
            messagesQueue.Clear();
        }

        if (enableDebugLogs && messageCount > 0)
            Debug.Log($"GeminiLive: Cleared {messageCount} pending messages due to error");
    }

    public bool IsWebSocketConnected()
    {
        return webSocket != null && webSocket.State == WebSocketState.Open && isConnected;
    }

    private IEnumerator ConnectWebSocketCoroutine()
    {
        cancellationTokenSource = new System.Threading.CancellationTokenSource();
        webSocket = new ClientWebSocket();

        webSocket.Options.SetRequestHeader("User-Agent", "Rap-Against-The-Machine");
        webSocket.Options.SetRequestHeader("Authorization", $"Token {ephemeralKey}");

        string url = $"{GEMINI_WEBSOCKET_URL}";
        Uri uri = new Uri(url);

        if (enableDebugLogs)
            Debug.Log($"Connecting to Gemini Live API: {GEMINI_WEBSOCKET_URL}");

        ConnectWebSocketAsync(uri);

        if (webSocket?.State == WebSocketState.Open)
        {
            yield return new WaitForSeconds(0.1f);
            SendSetupMessage();
            yield return new WaitForSeconds(1f);

            if (!isReceiving && receiveCoroutine == null)
            {
                receiveCoroutine = StartCoroutine(ReceiveMessagesCoroutine());
            }
            isConnected = true;

            if (enableDebugLogs)
                Debug.Log("GeminiLive: Connected successfully");
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogError($"GeminiLive: Connection timeout - State: {webSocket?.State}");

            ClearMessageQueue();
            HandleError(
                "CONNECTION_ERROR",
                "ConnectWebSocketCoroutine",
                "ConnectWebSocketAsync",
                $"Failed to connect - State: {webSocket?.State}"
            );
        }
    }

    private void ConnectWebSocketAsync(Uri uri)
    {
        if (cancellationTokenSource == null)
        {
            HandleError(
                "CONNECTION_ERROR",
                "ConnectWebSocketAsync",
                "NullCancellationToken",
                "CancellationTokenSource is null"
            );
            return;
        }

        var connectTask = webSocket.ConnectAsync(uri, cancellationTokenSource.Token);

        while (!connectTask.IsCompleted) { }

        if (connectTask.IsFaulted)
        {
            LogWebSocketConnectionError(connectTask);
        }
    }

    private void LogWebSocketConnectionError(System.Threading.Tasks.Task connectTask)
    {
        Exception baseException = connectTask.Exception?.GetBaseException();
        string errorMessage = $"WebSocket connection failed: {baseException?.Message}";

        if (baseException is System.Net.WebException webEx)
        {
            errorMessage += $" (WebException: {webEx.Status})";
        }
        else if (baseException is System.Net.HttpListenerException httpEx)
        {
            errorMessage += $" (HTTP Status: {httpEx.ErrorCode})";
        }
        HandleError(
            "CONNECTION_ERROR",
            "ConnectWebSocketAsync",
            "ConnectAsync",
            errorMessage,
            baseException
        );
    }

    private void SendSetupMessage()
    {
        if (isSetupSent)
        {
            if (enableDebugLogs)
                Debug.LogWarning("GeminiLive: Setup message already sent, skipping duplicate");
            return;
        }

        // Get the appropriate model string for live API
        string modelString = aiConfig.GeminiLiveModel;
        string voiceName = aiConfig.SelectedGeminiTtsVoice.ToString();

        var setupMessage = GetSetupMessage(modelString, voiceName);

        // Validate the setup message
        if (string.IsNullOrEmpty(modelString))
        {
            string errorMessage = "Model string is null or empty!";
            Debug.LogError($"GeminiLive: {errorMessage}");

            HandleError("SETUP_ERROR", "SendSetupMessage", "ValidateModelString", errorMessage);
        }

        isSetupSent = true;
        lock (messagesQueue)
        {
            messagesQueue.Enqueue(setupMessage);
        }
    }

    protected abstract BidiGenerateContentClientMessage GetSetupMessage(
        string modelString,
        string voiceName
    );

    private IEnumerator ReceiveMessagesCoroutine()
    {
        if (isReceiving)
        {
            if (enableDebugLogs)
                Debug.LogWarning("GeminiLive: ReceiveMessagesCoroutine already running, skipping");
            yield break;
        }

        isReceiving = true;
        byte[] buffer = new byte[1024 * 16];
        StringBuilder messageBuilder = new StringBuilder();

        try
        {
            while (
                webSocket != null
                && webSocket.State == WebSocketState.Open
                && cancellationTokenSource != null
                && !cancellationTokenSource.IsCancellationRequested
            )
            {
                // Store local reference to prevent race conditions
                var localCancellationTokenSource = cancellationTokenSource;
                var localWebSocket = webSocket;

                if (localCancellationTokenSource == null || localWebSocket == null)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning(
                            "GeminiLive: CancellationTokenSource or WebSocket is null, stopping receive loop"
                        );
                    break;
                }

                if (localCancellationTokenSource.IsCancellationRequested)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning(
                            "GeminiLive: Cancellation requested, stopping receive loop"
                        );
                    break;
                }

                System.Threading.Tasks.Task<WebSocketReceiveResult> receiveTask = null;

                try
                {
                    receiveTask = localWebSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        localCancellationTokenSource.Token
                    );
                }
                catch (ObjectDisposedException)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning(
                            "GeminiLive: WebSocket disposed during ReceiveAsync, stopping receive loop"
                        );
                    break;
                }
                catch (Exception e)
                {
                    if (enableDebugLogs)
                        Debug.LogError($"GeminiLive: Error starting ReceiveAsync: {e.Message}");
                    break;
                }

                if (receiveTask == null)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning(
                            "GeminiLive: ReceiveAsync task is null, stopping receive loop"
                        );
                    break;
                }

                while (!receiveTask.IsCompleted)
                {
                    // Check if we should stop waiting due to cleanup
                    if (
                        webSocket == null
                        || cancellationTokenSource == null
                        || cancellationTokenSource.IsCancellationRequested
                    )
                    {
                        if (enableDebugLogs)
                            Debug.LogWarning(
                                "GeminiLive: Cleanup initiated while waiting for receive task, stopping"
                            );
                        throw new GeminiLiveException(
                            "RECEIVE_CANCELED",
                            "ReceiveMessagesCoroutine",
                            "ReceiveAsync",
                            "Receive operation was canceled due to cleanup."
                        );
                    }
                    yield return null;
                }

                if (receiveTask.IsFaulted)
                {
                    LogWebSocketReceiveError(receiveTask);
                    break;
                }
                else if (receiveTask.IsCompletedSuccessfully)
                {
                    WebSocketReceiveResult result = null;
                    try
                    {
                        result = receiveTask.Result;
                    }
                    catch (Exception e)
                    {
                        if (enableDebugLogs)
                            Debug.LogError(
                                $"GeminiLive: Error getting receive task result: {e.Message}"
                            );
                        break;
                    }

                    if (result == null)
                    {
                        if (enableDebugLogs)
                            Debug.LogWarning(
                                "GeminiLive: Receive task result is null, stopping receive loop"
                            );
                        break;
                    }

                    if (enableDebugLogs)
                        Debug.Log(
                            $"Received message type: {result.MessageType}, Count: {result.Count}, EndOfMessage: {result.EndOfMessage}"
                        );

                    switch (result.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            ParseWebSocketTextMessage(buffer, messageBuilder, result);
                            break;
                        case WebSocketMessageType.Binary:
                            ParseWebSocketBinaryMessage(buffer, result);
                            break;
                        case WebSocketMessageType.Close:
                            ThrowWebSocketClosureDetails(result);
                            break;
                    }
                }
            }
        }
        finally
        {
            isReceiving = false;
            receiveCoroutine = null;

            // Cleanup notification when loop exits
            if (enableDebugLogs)
                Debug.Log("GeminiLive: ReceiveMessagesCoroutine exited");
        }
    }

    private void LogWebSocketReceiveError(
        System.Threading.Tasks.Task<WebSocketReceiveResult> receiveTask
    )
    {
        if (receiveTask.Exception?.GetBaseException() is OperationCanceledException)
        {
            return;
        }
        else
        {
            Exception baseException = receiveTask.Exception?.GetBaseException();

            if (enableDebugLogs)
                Debug.LogError($"GeminiLive: Receive error - {baseException?.Message}");

            ClearMessageQueue();
            HandleError(
                "RECEIVE_ERROR",
                "ReceiveMessagesCoroutine",
                "ReceiveAsync",
                $"Receive error - {baseException?.Message}",
                baseException
            );
        }
    }

    private void ParseWebSocketBinaryMessage(byte[] buffer, WebSocketReceiveResult result)
    {
        if (enableDebugLogs)
            Debug.Log($"Received binary data, count: {result.Count} bytes");

        byte[] binaryData = new byte[result.Count];
        Array.Copy(buffer, 0, binaryData, 0, result.Count);

        try
        {
            string jsonMessage = Encoding.UTF8.GetString(binaryData);

            if (jsonMessage.TrimStart().StartsWith("{") && jsonMessage.TrimEnd().EndsWith("}"))
            {
                ProcessGeminiResponse(jsonMessage);
            }
        }
        catch (DecoderFallbackException ex)
        {
            string errorMessage =
                $"Failed to decode binary as UTF-8: {ex.Message}, treating as audio data";
            if (enableDebugLogs)
                Debug.Log(errorMessage);
        }
    }

    private void ParseWebSocketTextMessage(
        byte[] buffer,
        StringBuilder messageBuilder,
        WebSocketReceiveResult result
    )
    {
        string chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
        messageBuilder.Append(chunk);

        // If this is the end of the message, process it
        if (result.EndOfMessage)
        {
            string completeMessage = messageBuilder.ToString();
            ProcessGeminiResponse(completeMessage);
            messageBuilder.Clear();
        }
    }

    private void ThrowWebSocketClosureDetails(WebSocketReceiveResult result)
    {
        if (enableDebugLogs)
            Debug.LogWarning($"GeminiLive: WebSocket closed - {result.CloseStatus}");

        HandleError(
            "CONNECTION_CLOSED",
            "ReceiveMessagesCoroutine",
            "WebSocketClosed",
            $"WebSocket closed - {result.CloseStatus}: {result.CloseStatusDescription}"
        );
    }

    protected virtual void ProcessGeminiResponse(string json)
    {
        try
        {
            if (enableDebugLogs)
                Debug.Log(
                    $"Received Gemini response: {json.Substring(0, Math.Min(json.Length, 200))}..."
                );

            // Parse JSON into object
            BidiGenerateContentServerMessage response =
                JsonConvert.DeserializeObject<BidiGenerateContentServerMessage>(
                    json,
                    cachedJsonSettings
                );

            if (response == null)
            {
                string errorMessage = "Failed to parse Gemini response as JSON object";
                if (enableDebugLogs)
                    Debug.LogWarning(errorMessage);
            }

            // Check for setup completion
            if (response.setupComplete != null && !isSetupComplete)
            {
                isSetupComplete = true;
                if (enableDebugLogs)
                    Debug.Log(
                        $"GeminiLive: Setup completed by Gemini Live API - {response.setupComplete}. State now - isSetupComplete: {isSetupComplete}, isSetupSent: {isSetupSent}"
                    );
            }

            if (!string.IsNullOrEmpty(response.serverContent?.outputTranscription?.text))
            {
                Debug.Log($"Transcription : {response.serverContent?.outputTranscription?.text}");
                OnTranscriptionReceived(response.serverContent?.outputTranscription?.text);
                return;
            }

            // Process server content
            if (response.serverContent?.modelTurn?.parts != null)
            {
                if (enableDebugLogs)
                    Debug.Log(
                        $"Processing {response.serverContent.modelTurn.parts.Length} parts from Gemini response"
                    );
                foreach (var part in response.serverContent.modelTurn.parts)
                {
                    // Handle text response
                    if (!string.IsNullOrEmpty(part.text))
                    {
                        // uiManager?.UpdateComputerText(part.text);
                        if (enableDebugLogs)
                            Debug.Log($"Received text response: {part.text}");
                        OnTextResponseReceived(part.text);
                    }
                }
            }

            // Check for turn completion
            if (response.turnComplete || (response.serverContent?.turnComplete == true))
            {
                if (enableDebugLogs)
                    Debug.Log("Gemini turn complete");
            }

            // Check for interrupted responses
            if (response.interrupted || (response.serverContent?.interrupted == true))
            {
                if (enableDebugLogs)
                    Debug.Log("Gemini response interrupted");
            }
        }
        catch (Exception e)
        {
            string errorMessage = $"Error processing Gemini response: {e.Message}";
            if (enableDebugLogs)
                Debug.LogError(errorMessage);
        }
    }

    protected abstract void OnTextResponseReceived(string text);

    protected abstract void OnTranscriptionReceived(string text);

    public IEnumerator SendSilenceToGeminiCoroutine(
        float durationSeconds,
        int sampleRate,
        int channels
    )
    {
        int totalSamples = (int)(durationSeconds * sampleRate * channels);
        float[] samples = new float[totalSamples];
        yield return StartCoroutine(SendAudioToGeminiCoroutine(samples));
    }

    public IEnumerator SendAudioToGeminiCoroutine(float[] samples)
    {
        if (samples == null || samples.Length == 0)
        {
            string errorMessage = "Recording clip is null or empty";
            Debug.LogError($"GeminiLiveWebRTC: {errorMessage}");

            HandleError(
                "AUDIO_INPUT_ERROR",
                "SendAudioToGeminiCoroutine",
                "ValidateAudioClip",
                errorMessage
            );
        }

        // Validate audio samples for NaN or extreme values
        bool hasInvalidSamples = false;
        for (int i = 0; i < samples.Length; i++)
        {
            if (
                float.IsNaN(samples[i])
                || float.IsInfinity(samples[i])
                || Mathf.Abs(samples[i]) > 1.0f
            )
            {
                samples[i] = 0f; // Replace invalid samples with silence
                hasInvalidSamples = true;
            }
        }

        if (hasInvalidSamples && enableDebugLogs)
        {
            Debug.LogWarning("GeminiLive: Invalid audio samples detected and corrected");
        }

        byte[] pcmData = WavUtility.ConvertToPCM16(samples);

        // Validate PCM data before converting to base64
        if (pcmData == null || pcmData.Length == 0)
        {
            string errorMessage = "Failed to convert audio samples to PCM data";
            Debug.LogError($"GeminiLiveWebRTC: {errorMessage}");
            yield break;
        }

        string base64Audio = Convert.ToBase64String(pcmData);

        var message = new BidiGenerateContentClientMessage
        {
            realtimeInput = new BidiGenerateContentRealtimeInput
            {
                audio = new Blob
                {
                    data = base64Audio,
                    mimeType = $"audio/pcm;rate={AILiveConfig.inputSampleRate}",
                },
            },
        };

        lock (messagesQueue)
        {
            messagesQueue.Enqueue(message);
        }
        yield return null;
    }

    public IEnumerator SendActivityStartToGeminiCoroutine()
    {
        var activityStartMessage = new BidiGenerateContentClientMessage
        {
            realtimeInput = new BidiGenerateContentRealtimeInput
            {
                activityStart = new ActivityStart(),
            },
        };

        lock (messagesQueue)
        {
            messagesQueue.Enqueue(activityStartMessage);
        }
        yield return null;
    }

    public IEnumerator SendActivityEndToGeminiCoroutine()
    {
        var activityEndMessage = new BidiGenerateContentClientMessage
        {
            realtimeInput = new BidiGenerateContentRealtimeInput
            {
                activityEnd = new ActivityEnd(),
            },
        };

        lock (messagesQueue)
        {
            messagesQueue.Enqueue(activityEndMessage);
        }
        yield return null;
    }

    public IEnumerator SendTextToGeminiCoroutine(string inputText)
    {
        if (string.IsNullOrEmpty(inputText))
        {
            string errorMessage = "Text is null or empty";
            Debug.LogError($"GeminiLiveWebRTC: {errorMessage}");

            HandleError(
                "TEXT_INPUT_ERROR",
                "SendTextToGeminiCoroutine",
                "ValidateTextInput",
                errorMessage
            );
        }

        var message = new BidiGenerateContentClientMessage
        {
            realtimeInput = new BidiGenerateContentRealtimeInput { text = inputText },
        };

        lock (messagesQueue)
        {
            messagesQueue.Enqueue(message);
        }
        yield return null;
    }

    protected IEnumerator SendWebSocketMessageCoroutine(object message)
    {
        if (!IsWebSocketConnected())
        {
            if (enableDebugLogs)
                Debug.LogWarning($"GeminiLive: Cannot send message - State: {webSocket?.State}");

            ClearMessageQueue();
            HandleError(
                "CONNECTION_ERROR",
                "SendWebSocketMessageCoroutine",
                "IsWebSocketConnected",
                $"Cannot send message - State: {webSocket?.State}"
            );
        }

        string json = JsonConvert.SerializeObject(message, cachedJsonSettings);

        byte[] bytes = Encoding.UTF8.GetBytes(json);
        int totalBytes = bytes.Length;
        int offset = 0;

        while (offset < totalBytes)
        {
            int chunkSize = Math.Min(maxChunkSize, totalBytes - offset);
            bool endOfMessage = (offset + chunkSize) >= totalBytes;
            var chunk = new ArraySegment<byte>(bytes, offset, chunkSize);

            yield return StartCoroutine(SendWebSocketMessageChunk(endOfMessage, chunk));
            offset += chunkSize;
        }
    }

    private IEnumerator SendWebSocketMessageChunk(bool endOfMessage, ArraySegment<byte> chunk)
    {
        // Store local references to prevent race conditions
        var localCancellationTokenSource = cancellationTokenSource;
        var localWebSocket = webSocket;

        if (
            localCancellationTokenSource == null
            || localCancellationTokenSource.IsCancellationRequested
            || localWebSocket == null
        )
        {
            if (enableDebugLogs)
                Debug.LogWarning(
                    "GeminiLive: Cannot send message chunk - cancellation requested, token is null, or WebSocket is null"
                );
            yield break;
        }

        System.Threading.Tasks.Task sendTask = null;

        try
        {
            sendTask = localWebSocket.SendAsync(
                chunk,
                WebSocketMessageType.Text,
                endOfMessage,
                localCancellationTokenSource.Token
            );
        }
        catch (ObjectDisposedException)
        {
            if (enableDebugLogs)
                Debug.LogWarning("GeminiLive: WebSocket disposed during SendAsync");
            yield break;
        }
        catch (Exception e)
        {
            if (enableDebugLogs)
                Debug.LogError($"GeminiLive: Error starting SendAsync: {e.Message}");
            yield break;
        }

        if (sendTask == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("GeminiLive: SendAsync task is null");
            yield break;
        }

        while (!sendTask.IsCompleted)
        {
            // Check if we should stop waiting due to cleanup
            if (
                webSocket == null
                || cancellationTokenSource == null
                || cancellationTokenSource.IsCancellationRequested
            )
            {
                if (enableDebugLogs)
                    Debug.LogWarning(
                        "GeminiLive: Cleanup initiated while waiting for send task, stopping"
                    );
                yield break;
            }
            yield return null;
        }

        if (sendTask.IsFaulted)
        {
            if (enableDebugLogs)
                Debug.LogError(
                    $"Error sending message: {sendTask.Exception?.GetBaseException()?.Message}"
                );

            ClearMessageQueue();
            HandleError(
                "SEND_MESSAGE_ERROR",
                "SendWebSocketMessageChunk",
                "SendAsync",
                sendTask.Exception?.GetBaseException()?.Message
            );
        }
    }

    protected virtual void OnDestroy()
    {
        if (enableDebugLogs)
            Debug.Log("GeminiLive: OnDestroy called, cleaning up resources");

        CleanupConnection();
    }

    protected virtual void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && IsWebSocketConnected())
        {
            if (enableDebugLogs)
                Debug.Log("GeminiLive: Application paused, cleaning up connection");

            CleanupConnection();
        }
    }

    private void CleanupConnection()
    {
        try
        {
            // Stop receive coroutine first
            if (receiveCoroutine != null)
            {
                StopCoroutine(receiveCoroutine);
                receiveCoroutine = null;
            }
            isReceiving = false;

            // Cancel all ongoing operations first
            if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
            }

            // Clear message queue to prevent further processing
            ClearMessageQueue();

            // Close WebSocket connection if open
            if (webSocket != null)
            {
                if (
                    webSocket.State == WebSocketState.Open
                    || webSocket.State == WebSocketState.Connecting
                )
                {
                    try
                    {
                        // Use a timeout to prevent hanging
                        var closeTask = webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Cleanup",
                            System.Threading.CancellationToken.None
                        );

                        // Don't await in synchronous cleanup - just fire and forget
                        _ = closeTask;
                    }
                    catch (Exception e)
                    {
                        if (enableDebugLogs)
                            Debug.LogWarning(
                                $"GeminiLive: Error during WebSocket close: {e.Message}"
                            );
                    }
                }

                // Dispose WebSocket
                try
                {
                    webSocket.Dispose();
                }
                catch (Exception e)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning($"GeminiLive: Error disposing WebSocket: {e.Message}");
                }
                finally
                {
                    webSocket = null;
                }
            }

            // Dispose cancellation token source
            if (cancellationTokenSource != null)
            {
                try
                {
                    cancellationTokenSource.Dispose();
                }
                catch (Exception e)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning(
                            $"GeminiLive: Error disposing CancellationTokenSource: {e.Message}"
                        );
                }
                finally
                {
                    cancellationTokenSource = null;
                }
            }

            // Reset connection state
            isConnected = false;
            isSetupComplete = false;
            isSetupSent = false;
            isReceiving = false;

            if (enableDebugLogs)
                Debug.Log("GeminiLive: Connection cleanup completed");
        }
        catch (Exception e)
        {
            if (enableDebugLogs)
                Debug.LogError($"GeminiLive: Critical error during cleanup: {e.Message}");
        }
    }

    public void Destroy()
    {
        if (enableDebugLogs)
            Debug.Log("GeminiLive: Manual Destroy called");

        CleanupConnection();
    }
}
