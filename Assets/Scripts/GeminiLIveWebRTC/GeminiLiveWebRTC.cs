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

    [SerializeField]
    protected const int maxChunkSize = 1024;

    [Header("Session Management")]
    [Tooltip("Enable session resumption for unlimited session duration")]
    [SerializeField]
    protected bool enableSessionResumption = true;

    [Tooltip("Enable context window compression to extend sessions")]
    [SerializeField]
    protected bool enableContextWindowCompression = true;

    [Tooltip("Max number of connection retry attempts")]
    [SerializeField]
    protected int maxConnectionRetries = 3;

    [Tooltip("Delay between retry attempts in seconds")]
    [SerializeField]
    protected float retryDelaySeconds = 2f;

    [Header("Agent Identity")]
    [Tooltip("Name of this agent for logging purposes")]
    [SerializeField]
    protected string agentName = "GeminiLive";

    protected RapBattleConductorLive rapBattleConductorLive;
    protected ClientWebSocket webSocket;
    protected System.Threading.CancellationTokenSource cancellationTokenSource;
    protected bool isConnected = false;
    protected bool isSetupComplete = false;
    protected bool isSetupSent = false;
    protected bool isReceiving = false;
    protected Coroutine receiveCoroutine = null;
    protected string currentSessionHandle = null;
    protected bool isReconnecting = false;
    protected int connectionAttempts = 0;
    protected float lastGoAwayTime = 0f;
    protected bool goAwayReceived = false;
    public bool IsConnected => isConnected;
    public bool IsSetupComplete => isSetupComplete;

    protected void LogInfo(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[{agentName}] {message}");
    }

    protected void LogWarning(string message)
    {
        if (enableDebugLogs)
            Debug.LogWarning($"[{agentName}] {message}");
    }

    protected void LogError(string message)
    {
        if (enableDebugLogs)
            Debug.LogError($"[{agentName}] {message}");
    }

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

        LogInfo($"Initialized - Connected: {isConnected}");
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

        if (remainingMessages > 0)
            LogWarning($"Timeout - {remainingMessages} messages still pending");
    }

    protected virtual void ResetConnectionState()
    {
        isConnected = false;
        isSetupComplete = false;
        isSetupSent = false;
        isReceiving = false;
        goAwayReceived = false;
        lastGoAwayTime = 0f;
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

        if (messageCount > 0)
            LogInfo($"Cleared {messageCount} pending messages due to error");
    }

    public bool IsWebSocketConnected()
    {
        return webSocket != null && webSocket.State == WebSocketState.Open && isConnected;
    }

    private IEnumerator ConnectWebSocketCoroutine()
    {
        connectionAttempts++;

        LogInfo($"Connection attempt {connectionAttempts}/{maxConnectionRetries}");

        cancellationTokenSource = new System.Threading.CancellationTokenSource();
        webSocket = new ClientWebSocket();
        webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

        webSocket.Options.SetRequestHeader("User-Agent", "Rap-Against-The-Machine");
        webSocket.Options.SetRequestHeader("Authorization", $"Token {ephemeralKey}");

        string url = $"{GEMINI_WEBSOCKET_URL}";
        Uri uri = new Uri(url);

        string resumptionInfo =
            enableSessionResumption && !string.IsNullOrEmpty(currentSessionHandle)
                ? $" (Resuming session: {currentSessionHandle.Substring(0, Math.Min(8, currentSessionHandle.Length))}...)"
                : "";
        LogInfo($"Connecting to Gemini Live API: {GEMINI_WEBSOCKET_URL}{resumptionInfo}");

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
            connectionAttempts = 0;
            isReconnecting = false;

            LogInfo("Connected successfully");
        }
        else
        {
            LogError($"Connection timeout - State: {webSocket?.State}");

            if (connectionAttempts < maxConnectionRetries)
            {
                LogWarning($"Retrying connection in {retryDelaySeconds} seconds...");

                yield return new WaitForSeconds(retryDelaySeconds);
                yield return StartCoroutine(ConnectWebSocketCoroutine());
            }
            else
            {
                ClearMessageQueue();
                HandleError(
                    "CONNECTION_ERROR",
                    "ConnectWebSocketCoroutine",
                    "ConnectWebSocketAsync",
                    $"Failed to connect after {maxConnectionRetries} attempts - State: {webSocket?.State}"
                );
            }
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
        if (isSetupSent && !isReconnecting)
        {
            LogWarning("Setup message already sent, skipping duplicate");
            return;
        }

        string modelString = aiConfig.GeminiLiveModel;
        string voiceName = aiConfig.SelectedGeminiTtsVoice.ToString();

        var setupMessage = GetSetupMessage(modelString, voiceName);

        if (string.IsNullOrEmpty(modelString))
        {
            string errorMessage = "Model string is null or empty!";
            LogError(errorMessage);
            HandleError("SETUP_ERROR", "SendSetupMessage", "ValidateModelString", errorMessage);
        }

        if (enableSessionResumption && !string.IsNullOrEmpty(currentSessionHandle))
        {
            setupMessage.setup.sessionResumption = new SessionResumptionConfig
            {
                handle = currentSessionHandle,
            };
            LogInfo(
                $"Resuming session with handle: {currentSessionHandle.Substring(0, Math.Min(8, currentSessionHandle.Length))}..."
            );
        }
        else if (enableSessionResumption)
        {
            setupMessage.setup.sessionResumption = new SessionResumptionConfig { handle = null };
            LogInfo("Starting new session with resumption enabled");
        }

        if (enableContextWindowCompression)
        {
            setupMessage.setup.contextWindowCompression = new ContextWindowCompressionConfig
            {
                slidingWindow = new SlidingWindow(),
                triggerTokens = 0,
            };
            LogInfo("Context window compression enabled for unlimited session duration");
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
            LogWarning("ReceiveMessagesCoroutine already running, skipping");
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
                    LogWarning(
                        "CancellationTokenSource or WebSocket is null, stopping receive loop"
                    );
                    break;
                }

                if (localCancellationTokenSource.IsCancellationRequested)
                {
                    LogWarning("Cancellation requested, stopping receive loop");
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
                    LogWarning("WebSocket disposed during ReceiveAsync, stopping receive loop");
                    break;
                }
                catch (Exception e)
                {
                    LogError($"Error starting ReceiveAsync: {e.Message}");
                    break;
                }

                if (receiveTask == null)
                {
                    LogWarning("ReceiveAsync task is null, stopping receive loop");
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
                        LogWarning("Cleanup initiated while waiting for receive task, stopping");
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
                        LogError($"Error getting receive task result: {e.Message}");
                        break;
                    }

                    if (result == null)
                    {
                        LogWarning("Receive task result is null, stopping receive loop");
                        break;
                    }

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

            LogInfo("ReceiveMessagesCoroutine exited");
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

            LogError($"Receive error - {baseException?.Message}");

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
            LogInfo(errorMessage);
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
        LogWarning($"WebSocket closed - {result.CloseStatus}");

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
            LogInfo(
                $"Received Gemini response: {json.Substring(0, Math.Min(json.Length, 200))}..."
            );

            BidiGenerateContentServerMessage response =
                JsonConvert.DeserializeObject<BidiGenerateContentServerMessage>(
                    json,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }
                );

            if (response == null)
            {
                string errorMessage = "Failed to parse Gemini response as JSON object";
                LogWarning(errorMessage);
                return;
            }

            if (response.setupComplete != null && !isSetupComplete)
            {
                isSetupComplete = true;
                LogInfo(
                    $"Setup completed by Gemini Live API - {response.setupComplete}. State now - isSetupComplete: {isSetupComplete}, isSetupSent: {isSetupSent}"
                );
            }

            if (response.sessionResumptionUpdate != null)
            {
                string serializedUpdate = JsonConvert.SerializeObject(
                    response.sessionResumptionUpdate
                );
                if (serializedUpdate != "{}")
                {
                    LogInfo($"Session resumption update: {serializedUpdate}");
                    return;
                }
                HandleSessionResumptionUpdate(response.sessionResumptionUpdate);
            }

            if (response.goAway != null)
            {
                HandleGoAway(response.goAway);
            }

            if (response.serverContent?.generationComplete == true)
            {
                LogInfo("Generation complete");
            }

            if (!string.IsNullOrEmpty(response.serverContent?.outputTranscription?.text))
            {
                LogInfo($"Transcription : {response.serverContent?.outputTranscription?.text}");
                OnTranscriptionReceived(response.serverContent?.outputTranscription?.text);
                return;
            }

            if (response.serverContent?.modelTurn?.parts != null)
            {
                LogInfo(
                    $"Processing {response.serverContent.modelTurn.parts.Length} parts from Gemini response"
                );
                foreach (var part in response.serverContent.modelTurn.parts)
                {
                    if (!string.IsNullOrEmpty(part.text))
                    {
                        LogInfo($"Received text response: {part.text}");
                        OnTextResponseReceived(part.text);
                    }
                }
            }

            if (response.turnComplete || (response.serverContent?.turnComplete == true))
            {
                LogInfo("Gemini turn complete");
            }

            if (response.interrupted || (response.serverContent?.interrupted == true))
            {
                LogInfo("Gemini response interrupted");
            }
        }
        catch (Exception e)
        {
            string errorMessage = $"Error processing Gemini response: {e.Message}";
            LogError(errorMessage);
        }
    }

    protected abstract void OnTextResponseReceived(string text);

    protected abstract void OnTranscriptionReceived(string text);

    private void HandleSessionResumptionUpdate(SessionResumptionUpdate update)
    {
        if (update.resumable && !string.IsNullOrEmpty(update.newHandle))
        {
            currentSessionHandle = update.newHandle;
            LogInfo(
                $"Session resumption handle updated: {currentSessionHandle.Substring(0, Math.Min(8, currentSessionHandle.Length))}..."
            );
        }
        else if (!update.resumable)
        {
            currentSessionHandle = null;
            LogWarning("Session is no longer resumable");
        }
    }

    private void HandleGoAway(GoAway goAway)
    {
        goAwayReceived = true;
        lastGoAwayTime = Time.time;

        LogWarning($"GoAway received - Time left: {goAway.timeLeft}");

        if (!string.IsNullOrEmpty(goAway.timeLeft) && enableSessionResumption)
        {
            LogInfo("Preparing for connection reconnection...");

            StartCoroutine(HandleReconnection());
        }
    }

    private IEnumerator HandleReconnection()
    {
        if (isReconnecting)
        {
            LogWarning("Reconnection already in progress");
            yield break;
        }

        isReconnecting = true;

        yield return StartCoroutine(WaitForAllMessagesToBeSent(10f));

        LogInfo("Starting reconnection process...");

        CleanupConnection();

        yield return new WaitForSeconds(1f);

        try
        {
            ephemeralKey = aiConfig.GenerateEphemeralKey();
        }
        catch (Exception ex)
        {
            LogError($"Failed to generate ephemeral key during reconnection: {ex.Message}");
            isReconnecting = false;
            yield break;
        }

        ResetConnectionState();
        isSetupSent = false;

        yield return StartCoroutine(ConnectWebSocketCoroutine());

        if (isConnected)
        {
            StartCoroutine(ProcessMessagesQueue());
            LogInfo("Reconnection successful");
        }
        else
        {
            LogError("Reconnection failed");
            isReconnecting = false;
        }
    }

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
            LogError(errorMessage);

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

        if (hasInvalidSamples)
        {
            LogWarning("Invalid audio samples detected and corrected");
        }

        byte[] pcmData = WavUtility.ConvertToPCM16(samples);

        // Validate PCM data before converting to base64
        if (pcmData == null || pcmData.Length == 0)
        {
            string errorMessage = "Failed to convert audio samples to PCM data";
            LogError(errorMessage);
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
            LogError(errorMessage);

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
            LogWarning($"Cannot send message - State: {webSocket?.State}");

            ClearMessageQueue();
            HandleError(
                "CONNECTION_ERROR",
                "SendWebSocketMessageCoroutine",
                "IsWebSocketConnected",
                $"Cannot send message - State: {webSocket?.State}"
            );
        }

        string json = JsonConvert.SerializeObject(
            message,
            new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore,
                Formatting = Formatting.None,
            }
        );

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
            LogWarning(
                "Cannot send message chunk - cancellation requested, token is null, or WebSocket is null"
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
            LogWarning("WebSocket disposed during SendAsync");
            yield break;
        }
        catch (Exception e)
        {
            LogError($"Error starting SendAsync: {e.Message}");
            yield break;
        }

        if (sendTask == null)
        {
            LogWarning("SendAsync task is null");
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
                LogWarning("Cleanup initiated while waiting for send task, stopping");
                yield break;
            }
            yield return null;
        }

        if (sendTask.IsFaulted)
        {
            LogError($"Error sending message: {sendTask.Exception?.GetBaseException()?.Message}");

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
        LogInfo("OnDestroy called, cleaning up resources");

        CleanupConnection();
    }

    protected virtual void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && IsWebSocketConnected())
        {
            LogInfo("Application paused, cleaning up connection");

            CleanupConnection();
        }
    }

    private void CleanupConnection()
    {
        try
        {
            if (receiveCoroutine != null)
            {
                StopCoroutine(receiveCoroutine);
                receiveCoroutine = null;
            }
            isReceiving = false;

            if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
            }

            if (!isReconnecting)
            {
                ClearMessageQueue();
            }

            if (webSocket != null)
            {
                if (
                    webSocket.State == WebSocketState.Open
                    || webSocket.State == WebSocketState.Connecting
                )
                {
                    try
                    {
                        var closeTask = webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Cleanup",
                            System.Threading.CancellationToken.None
                        );
                        _ = closeTask;
                    }
                    catch (Exception e)
                    {
                        LogWarning($"Error during WebSocket close: {e.Message}");
                    }
                }

                try
                {
                    webSocket.Dispose();
                }
                catch (Exception e)
                {
                    LogWarning($"Error disposing WebSocket: {e.Message}");
                }
                finally
                {
                    webSocket = null;
                }
            }

            if (cancellationTokenSource != null)
            {
                try
                {
                    cancellationTokenSource.Dispose();
                }
                catch (Exception e)
                {
                    LogWarning($"Error disposing CancellationTokenSource: {e.Message}");
                }
                finally
                {
                    cancellationTokenSource = null;
                }
            }

            isConnected = false;
            isSetupComplete = false;
            if (!isReconnecting)
            {
                isSetupSent = false;
                currentSessionHandle = null;
            }
            isReceiving = false;

            LogInfo("Connection cleanup completed");
        }
        catch (Exception e)
        {
            LogError($"Critical error during cleanup: {e.Message}");
        }
    }

    public void Destroy()
    {
        LogInfo("Manual Destroy called");

        currentSessionHandle = null;
        isReconnecting = false;
        CleanupConnection();
    }

    public void ForceReconnect()
    {
        LogInfo("Force reconnect requested");

        if (!isReconnecting)
        {
            StartCoroutine(HandleReconnection());
        }
    }

    public string GetSessionHandle()
    {
        return currentSessionHandle;
    }

    public bool IsSessionResumable()
    {
        return enableSessionResumption && !string.IsNullOrEmpty(currentSessionHandle);
    }
}
