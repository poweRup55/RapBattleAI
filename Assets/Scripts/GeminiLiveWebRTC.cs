using System;
using System.Collections;
using System.Net.WebSockets;
using System.Text;
using EpicRapBattle.Config;
using Newtonsoft.Json;
using Unity.WebRTC;
using UnityEngine;

public class GeminiLiveWebRTC : MonoBehaviour
{
    static RTCIceServer[] iceServers = new RTCIceServer[]
    {
        new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } },
        new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:5349" } },
        new RTCIceServer { urls = new string[] { "stun:stun1.l.google.com:3478" } },
        new RTCIceServer { urls = new string[] { "stun:stun1.l.google.com:5349" } },
        new RTCIceServer { urls = new string[] { "stun:stun2.l.google.com:19302" } },
        new RTCIceServer { urls = new string[] { "stun:stun2.l.google.com:5349" } },
        new RTCIceServer { urls = new string[] { "stun:stun3.l.google.com:3478" } },
        new RTCIceServer { urls = new string[] { "stun:stun3.l.google.com:5349" } },
        new RTCIceServer { urls = new string[] { "stun:stun4.l.google.com:19302" } },
        new RTCIceServer { urls = new string[] { "stun:stun4.l.google.com:5349" } },
    };

    public Action<string> textGUIUpdater { get; set; }

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

    protected RTCPeerConnection localConnection;
    protected RTCDataChannel sendChannel;
    protected RTCDataChannel receiveChannel;
    protected ClientWebSocket webSocket;
    protected System.Threading.CancellationTokenSource cancellationTokenSource;
    protected bool isConnected = false;
    protected bool isSetupComplete = false;
    public bool IsConnected => isConnected;
    public bool IsSetupComplete => isSetupComplete;

    private const string GEMINI_WEBSOCKET_URL =
        "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateContentConstrained";

    public IEnumerator Initialize()
    {
        if (!aiConfig)
        {
            string errorMessage = "AIConfig is not assigned!";
            ThrowGeminiLiveException(errorMessage);
        }
        if (textGUIUpdater == null)
        {
            string errorMessage = "Text GUI Updater callback is not assigned!";
            ThrowGeminiLiveException(errorMessage);
        }
        ResetConnectionState();
        aiConfig.GenerateEphemeralKey();
        InitializeWebRTC();
        yield return StartCoroutine(ConnectWebSocketCoroutine());
    }

    protected virtual void ResetConnectionState()
    {
        isConnected = false;
        isSetupComplete = false;
    }

    private void ThrowGeminiLiveException(string errorMessage)
    {
        if (enableDebugLogs)
            Debug.LogError(errorMessage);
        throw new GeminiLiveException(errorMessage);
    }

    protected bool IsWebSocketConnected()
    {
        return webSocket != null && webSocket.State == WebSocketState.Open && isConnected;
    }

    private void InitializeWebRTC()
    {
        RTCConfiguration config = default;
        config.iceServers = iceServers;

        localConnection = new RTCPeerConnection(ref config);

        localConnection.OnIceCandidate = OnIceCandidate;
        localConnection.OnIceConnectionChange = OnIceConnectionChange;

        sendChannel = localConnection.CreateDataChannel("sendChannel");
        sendChannel.OnOpen = OnSendChannelOpen;
        sendChannel.OnClose = OnSendChannelClose;

        localConnection.OnDataChannel = OnDataChannel;

        if (enableDebugLogs)
            Debug.Log("WebRTC initialized");
    }

    private IEnumerator ConnectWebSocketCoroutine()
    {
        cancellationTokenSource = new System.Threading.CancellationTokenSource();
        webSocket = new ClientWebSocket();

        webSocket.Options.SetRequestHeader("User-Agent", "Rap-Against-The-Machine");
        webSocket.Options.SetRequestHeader("Authorization", $"Token {aiConfig.EphemeralKey}");

        string url = $"{GEMINI_WEBSOCKET_URL}";
        Uri uri = new Uri(url);

        if (enableDebugLogs)
            Debug.Log($"Connecting to Gemini Live API: {GEMINI_WEBSOCKET_URL}");

        ConnectWebSocketAsync(uri);

        if (webSocket?.State == WebSocketState.Open)
        {
            if (enableDebugLogs)
                Debug.Log("WebSocket connection established successfully");
            yield return new WaitForSeconds(0.1f);
            SendSetupMessage();
            yield return new WaitForSeconds(1f);
            StartCoroutine(ReceiveMessagesCoroutine());
            isConnected = true;
            if (enableDebugLogs)
                Debug.Log("Connected to Gemini Live API");
        }
        else
        {
            string errorMessage =
                $"Connection to Gemini Live API timed out. Final state: {webSocket?.State}";
            ThrowGeminiLiveException(errorMessage);
        }
    }

    private void ConnectWebSocketAsync(Uri uri)
    {
        if (enableDebugLogs)
            Debug.Log($"Attempting WebSocket connection to: {uri}");

        var connectTask = webSocket.ConnectAsync(uri, cancellationTokenSource.Token);

        while (!connectTask.IsCompleted) { }

        if (connectTask.IsFaulted)
        {
            LogWebSocketConnectionError(connectTask);
        }
        else if (connectTask.IsCompletedSuccessfully)
        {
            if (enableDebugLogs)
                Debug.Log($"WebSocket connection successful. State: {webSocket.State}");
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

        Debug.LogError(errorMessage);

        if (enableDebugLogs && connectTask.Exception != null)
        {
            Debug.LogError($"Full exception details: {connectTask.Exception}");
        }

        throw new GeminiLiveException(
            "WEBSOCKET_CONNECTION_ERROR",
            "LogWebSocketConnectionError",
            "ConnectToGeminiWebSocket",
            errorMessage,
            baseException
        );
    }

    private void SendSetupMessage()
    {
        if (enableDebugLogs)
            Debug.Log("Preparing setup message for Gemini Live API");

        // Get the appropriate model string for live API
        string modelString = aiConfig.GeminiLiveModel;
        string voiceName = aiConfig.SelectedGeminiTtsVoice.ToString();

        if (enableDebugLogs)
            Debug.Log($"Using model: {modelString}, voice: {voiceName}");

        var setupMessage = GetSetupMessage(modelString, voiceName);

        // Validate the setup message
        if (string.IsNullOrEmpty(modelString))
        {
            string errorMessage = "Model string is null or empty!";
            Debug.LogError(errorMessage);

            throw new GeminiLiveException(
                "SETUP_ERROR",
                "SendSetupMessage",
                "ValidateModelString",
                errorMessage
            );
        }

        StartCoroutine(SendWebSocketMessageCoroutine(setupMessage));

        if (enableDebugLogs)
            Debug.Log("Setup message sent to Gemini Live API");
    }

    protected virtual BidiGenerateContentClientMessage GetSetupMessage(
        string modelString,
        string voiceName
    )
    {
        return new BidiGenerateContentClientMessage
        {
            setup = new BidiGenerateContentSetup
            {
                model = $"models/{modelString}",
                generationConfig = new GenerationConfig
                {
                    responseModalities = new string[] { "text" },
                    temperature = temperature,
                    // topP = topP,
                    // topK = topK,
                    maxOutputTokens = maxOutputTokens,
                    candidateCount = candidateCount,
                },
                realtimeInputConfig = new RealtimeInputConfig
                {
                    automaticActivityDetection = new AutomaticActivityDetection { disabled = true },
                    activityHandling = ActivityHandling.NO_INTERRUPTION,
                },
                systemInstruction = new Content
                {
                    parts = new Part[] { new Part { text = aiConfig.AIPrompt } },
                },
            },
        };
    }

    private IEnumerator ReceiveMessagesCoroutine()
    {
        byte[] buffer = new byte[1024 * 16];
        StringBuilder messageBuilder = new StringBuilder();

        if (enableDebugLogs)
            Debug.Log("Started listening for messages from Gemini");

        while (webSocket.State == WebSocketState.Open)
        {
            var receiveTask = webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationTokenSource.Token
            );

            while (!receiveTask.IsCompleted)
            {
                yield return null;
            }

            if (receiveTask.IsFaulted)
            {
                LogWebSocketReceiveError(receiveTask);
                break;
            }
            else if (receiveTask.IsCompletedSuccessfully)
            {
                var result = receiveTask.Result;

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

    private void LogWebSocketReceiveError(
        System.Threading.Tasks.Task<WebSocketReceiveResult> receiveTask
    )
    {
        if (receiveTask.Exception?.GetBaseException() is OperationCanceledException)
        {
            if (enableDebugLogs)
                Debug.Log("WebSocket receive operation cancelled");
            return;
        }
        else
        {
            Exception baseException = receiveTask.Exception?.GetBaseException();
            string errorMessage = $"Error receiving message: {baseException?.Message}";

            if (enableDebugLogs)
                Debug.LogError(errorMessage);

            throw new GeminiLiveException(
                "WEBSOCKET_RECEIVE_ERROR",
                "LogWebSocketReceiveError",
                "ReceiveMessage",
                errorMessage,
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

            throw new GeminiLiveException(
                "BINARY_DECODE_ERROR",
                "ParseWebSocketBinaryMessage",
                "DecodeBinaryData",
                errorMessage,
                ex
            );
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

        if (enableDebugLogs)
            Debug.Log($"Received text chunk: {chunk.Substring(0, Math.Min(chunk.Length, 100))}...");

        // If this is the end of the message, process it
        if (result.EndOfMessage)
        {
            string completeMessage = messageBuilder.ToString();
            if (enableDebugLogs)
                Debug.Log($"Complete message received, length: {completeMessage.Length}");
            ProcessGeminiResponse(completeMessage);
            messageBuilder.Clear();
        }
    }

    private void ThrowWebSocketClosureDetails(WebSocketReceiveResult result)
    {
        string closeDescription = result.CloseStatusDescription ?? "No description provided";
        Debug.LogWarning($"WebSocket connection closed by server. Status: {result.CloseStatus}");
        Debug.LogWarning($" Description: {closeDescription}");

        // Provide more specific error information
        if (result.CloseStatus == WebSocketCloseStatus.InvalidPayloadData) { }
        else if (result.CloseStatus == WebSocketCloseStatus.PolicyViolation) { }
        else if (result.CloseStatus == WebSocketCloseStatus.ProtocolError) { }
        else if (result.CloseStatus == WebSocketCloseStatus.InternalServerError) { }
        else { }
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
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }
                );

            if (response == null)
            {
                string errorMessage = "Failed to parse Gemini response as JSON object";
                if (enableDebugLogs)
                    Debug.LogWarning(errorMessage);

                throw new GeminiLiveException(
                    "JSON_PARSE_ERROR",
                    "ProcessGeminiResponse",
                    "ParseGeminiResponse",
                    errorMessage
                );
            }

            // Check for setup completion
            if (response.setupComplete != null && !isSetupComplete)
            {
                isSetupComplete = true;
                if (enableDebugLogs)
                    Debug.Log($"Setup completed by Gemini Live API {response.setupComplete}");
            }
            if (!string.IsNullOrEmpty(response.serverContent?.outputTranscription?.text))
            {
                Debug.Log($"Transcription : {response.serverContent?.outputTranscription?.text}");
                textGUIUpdater?.Invoke(response.serverContent.outputTranscription.text);
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
                        textGUIUpdater?.Invoke(part.text);
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

            throw new GeminiLiveException(
                "RESPONSE_PROCESSING_ERROR",
                "ProcessGeminiResponse",
                "ProcessResponse",
                errorMessage,
                e
            );
        }
    }

    public IEnumerator SendAudioToGeminiCoroutine(AudioClip recordingClip)
    {
        if (recordingClip == null)
        {
            string errorMessage = "Recording clip is null";
            Debug.LogError($"GeminiLiveWebRTC: {errorMessage}");

            throw new GeminiLiveException(
                "AUDIO_INPUT_ERROR",
                "SendAudioToGeminiCoroutine",
                "ValidateAudioClip",
                errorMessage
            );
        }

        if (recordingClip.samples == 0)
        {
            string errorMessage = "Recording clip has no audio samples";
            Debug.LogError($"GeminiLiveWebRTC: {errorMessage}");

            throw new GeminiLiveException(
                "AUDIO_INPUT_ERROR",
                "SendAudioToGeminiCoroutine",
                "ValidateAudioSamples",
                errorMessage
            );
        }

        float[] samples = new float[recordingClip.samples * recordingClip.channels];
        recordingClip.GetData(samples, 0);
        byte[] pcmData = WavUtility.ConvertToPCM16(samples);

        string base64Audio = Convert.ToBase64String(pcmData);

        var activityStartMessage = new BidiGenerateContentClientMessage
        {
            realtimeInput = new BidiGenerateContentRealtimeInput
            {
                activityStart = new ActivityStart(),
            },
        };

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

        var activityEndMessage = new BidiGenerateContentClientMessage
        {
            realtimeInput = new BidiGenerateContentRealtimeInput
            {
                activityEnd = new ActivityEnd(),
            },
        };

        yield return StartCoroutine(SendWebSocketMessageCoroutine(activityStartMessage));
        yield return StartCoroutine(SendWebSocketMessageCoroutine(message));
        yield return StartCoroutine(SendWebSocketMessageCoroutine(activityEndMessage));
    }

    public IEnumerator SendTextToGeminiCoroutine(string inputText)
    {
        if (string.IsNullOrEmpty(inputText))
        {
            string errorMessage = "Text is null or empty";
            Debug.LogError($"GeminiLiveWebRTC: {errorMessage}");

            throw new GeminiLiveException(
                "TEXT_INPUT_ERROR",
                "SendTextToGeminiCoroutine",
                "ValidateTextInput",
                errorMessage
            );
        }

        var message = new BidiGenerateContentRealtimeInput { text = inputText };

        yield return StartCoroutine(SendWebSocketMessageCoroutine(message));
    }

    protected IEnumerator SendWebSocketMessageCoroutine(object message)
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            string errorMessage = $"Cannot send message - WebSocket state: {webSocket?.State}";
            if (enableDebugLogs)
                Debug.LogWarning(errorMessage);

            throw new GeminiLiveException(
                "WEBSOCKET_STATE_ERROR",
                "SendWebSocketMessageCoroutine",
                "CheckWebSocketState",
                errorMessage
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

        if (enableDebugLogs)
            Debug.Log($"Sending message to Gemini: {json}...");

        byte[] bytes = Encoding.UTF8.GetBytes(json);

        if (enableDebugLogs)
        {
            Debug.Log($"Sending {bytes.Length} bytes to Gemini");
        }
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
        var sendTask = webSocket.SendAsync(
            chunk,
            WebSocketMessageType.Text,
            endOfMessage,
            cancellationTokenSource.Token
        );

        while (!sendTask.IsCompleted)
        {
            yield return null;
        }

        if (sendTask.IsFaulted)
        {
            throw new GeminiLiveException(
                "WEBSOCKET_SEND_ERROR",
                "SendWebSocketMessageCoroutine",
                "SendMessage",
                $"Error sending WebSocket message: {sendTask.Exception?.GetBaseException()?.Message}",
                sendTask.Exception
            );
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"Successfully sent {chunk.Count} bytes to Gemini");
        }
    }

    private void OnIceCandidate(RTCIceCandidate candidate)
    {
        if (enableDebugLogs)
            Debug.Log($"ICE Candidate: {candidate.Candidate}");
    }

    private void OnIceConnectionChange(RTCIceConnectionState state)
    {
        if (enableDebugLogs)
            Debug.Log($"ICE Connection State: {state}");
    }

    private void OnSendChannelOpen()
    {
        if (enableDebugLogs)
            Debug.Log("Send channel opened");
    }

    private void OnSendChannelClose()
    {
        if (enableDebugLogs)
            Debug.Log("Send channel closed");
    }

    private void OnDataChannel(RTCDataChannel channel)
    {
        receiveChannel = channel;
        receiveChannel.OnMessage = OnReceiveMessage;

        if (enableDebugLogs)
            Debug.Log("Data channel received");
    }

    private void OnReceiveMessage(byte[] data)
    {
        string message = Encoding.UTF8.GetString(data);

        if (enableDebugLogs)
            Debug.Log($"Received message: {message}");
    }

    private void OnDestroy()
    {
        Destroy();
    }

    public async void Destroy()
    {
        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            try
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    System.Threading.CancellationToken.None
                );
            }
            catch (Exception e)
            {
                string errorMessage = $"Error closing WebSocket: {e.Message}";
                if (enableDebugLogs)
                    Debug.LogWarning(errorMessage);

                throw new GeminiLiveException(
                    "WEBSOCKET_CLOSE_ERROR",
                    "OnDestroy",
                    "CloseWebSocket",
                    errorMessage,
                    e
                );
            }
        }

        cancellationTokenSource?.Cancel();

        sendChannel?.Close();
        receiveChannel?.Close();
        localConnection?.Close();

        isConnected = false;
        isSetupComplete = false;

        if (enableDebugLogs)
            Debug.Log("GeminiLiveWebRTC disposed");
    }
}
