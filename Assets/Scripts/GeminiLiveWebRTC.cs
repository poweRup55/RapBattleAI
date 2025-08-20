using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using EpicRapBattle.Config;
using Unity.WebRTC;
using UnityEngine;

public class GeminiLiveWebRTC : MonoBehaviour
{
    [SerializeField]
    UIManager uiManager;

    [Header("Configuration")]
    [SerializeField]
    private AIConfig aiConfig;

    [SerializeField]
    private const int audioBufferFlushThreshold = 70;

    [Header("Audio")]
    [SerializeField]
    private AudioSource audioSource;

    [Header("Debug")]
    [SerializeField]
    private bool enableDebugLogs = true;

    private const int sampleRate = 16000;
    private RTCPeerConnection localConnection;
    private RTCDataChannel sendChannel;
    private RTCDataChannel receiveChannel;
    private ClientWebSocket webSocket;
    private System.Threading.CancellationTokenSource cancellationTokenSource;
    private bool isConnected = false;
    private bool isSetupComplete = false;
    public bool IsConnected => isConnected;
    public bool IsSetupComplete => isSetupComplete;

    private bool receivingAudioStreamIn { get; set; } = false;

    public bool IsReceivingAudioData => receivingAudioStreamIn;

    private bool finishedAudioStreamIn = false;
    private Queue<byte[]> audioResponseQueue = new Queue<byte[]>();

    private Queue<IEnumerator> audioCoroutineQueue = new Queue<IEnumerator>();

    private const string GEMINI_WEBSOCKET_URL =
        "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent";

    public IEnumerator Initialize()
    {
        if (!ValidateConfiguration())
        {
            string errorMessage = "Invalid configuration. Please check AIConfig settings.";
            Debug.LogError($"GeminiLiveWebRTC: {errorMessage}");
            throw new GeminiLiveException(
                "CONFIGURATION_ERROR",
                "Initialize",
                "ValidateConfiguration",
                errorMessage
            );
        }

        InitializeWebRTC();
        yield return StartCoroutine(ConnectWebSocketCoroutine());
    }

    private bool ValidateConfiguration()
    {
        if (aiConfig == null)
        {
            string errorMessage = "AIConfig is not assigned!";
            Debug.LogError($"GeminiLiveWebRTC: {errorMessage}");
            throw new GeminiLiveException(
                "CONFIGURATION_ERROR",
                "ValidateConfiguration",
                "CheckAIConfigAssignment",
                errorMessage
            );
        }

        if (aiConfig.SelectedProvider != AIConfig.Provider.Gemini)
        {
            string errorMessage = "AIConfig must be set to Gemini provider!";
            Debug.LogError($"GeminiLiveWebRTC: {errorMessage}");
            throw new GeminiLiveException(
                "CONFIGURATION_ERROR",
                "ValidateConfiguration",
                "CheckProviderSelection",
                errorMessage
            );
        }

        if (string.IsNullOrEmpty(aiConfig.GeminiApiKey))
        {
            string errorMessage = "Gemini API key is not set in AIConfig!";
            Debug.LogError($"GeminiLiveWebRTC: {errorMessage}");
            throw new GeminiLiveException(
                "CONFIGURATION_ERROR",
                "ValidateConfiguration",
                "CheckApiKey",
                errorMessage
            );
        }

        return true;
    }

    private bool IsWebSocketConnected()
    {
        return webSocket != null && webSocket.State == WebSocketState.Open && isConnected;
    }

    private void InitializeWebRTC()
    {
        RTCConfiguration config = default;
        config.iceServers = new RTCIceServer[]
        {
            new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } },
        };

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

        string url = $"{GEMINI_WEBSOCKET_URL}?key={aiConfig.GeminiApiKey}";
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
            Debug.LogError(errorMessage);
            isConnected = false;
            throw new GeminiLiveException(
                "WEBSOCKET_TIMEOUT_ERROR",
                "ConnectWebSocketCoroutine",
                "EstablishConnection",
                errorMessage
            );
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
        string modelString = GetLiveApiModelString();
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

    private BidiGenerateContentSetupMessage GetSetupMessage(string modelString, string voiceName)
    {
        return new BidiGenerateContentSetupMessage
        {
            setup = new BidiGenerateContentSetup
            {
                model = $"models/{modelString}",
                generationConfig = new GenerationConfig
                {
                    responseModalities = new string[] { "AUDIO" },
                    speechConfig = new SpeechConfig
                    {
                        voiceConfig = new VoiceConfig
                        {
                            prebuiltVoiceConfig = new PrebuiltVoiceConfig { voiceName = voiceName },
                        },
                    },
                },
                realtimeInputConfig = new RealtimeInputConfig
                {
                    automaticActivityDetection = new AutomaticActivityDetection { disabled = true },
                },
                systemInstruction = new Content
                {
                    parts = new Part[]
                    {
                        new Part { text = aiConfig.RapPersonality + aiConfig.NpcSpeakingPrompt },
                    },
                },
            },
        };
    }

    private string GetLiveApiModelString()
    {
        return "gemini-2.5-flash-exp-native-audio-thinking-dialog";
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
                        throwWebSocketClosureDetails(result);
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

    private void throwWebSocketClosureDetails(WebSocketReceiveResult result)
    {
        string closeDescription = result.CloseStatusDescription ?? "No description provided";
        Debug.LogWarning(
            $"WebSocket connection closed by server. Status: {result.CloseStatus}, Description: {closeDescription}"
        );

        // Provide more specific error information
        if (result.CloseStatus == WebSocketCloseStatus.InvalidPayloadData) { }
        else if (result.CloseStatus == WebSocketCloseStatus.PolicyViolation) { }
        else if (result.CloseStatus == WebSocketCloseStatus.ProtocolError) { }
        else if (result.CloseStatus == WebSocketCloseStatus.InternalServerError) { }
        else { }
    }

    private void ProcessGeminiResponse(string json)
    {
        try
        {
            if (enableDebugLogs)
                Debug.Log(
                    $"Received Gemini response: {json.Substring(0, Math.Min(json.Length, 200))}..."
                );

            // Parse JSON into object
            GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(json);

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
            if (response.setupComplete != null)
            {
                isSetupComplete = true;
                if (enableDebugLogs)
                    Debug.Log("Setup completed by Gemini Live API");
            }
            Debug.Log($"Text Response? : {response.serverContent?.outputTranscription?.text}");
            if (!string.IsNullOrEmpty(response.serverContent?.outputTranscription?.text))
            {
                uiManager?.AppendComputerText(response.serverContent.outputTranscription.text);
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
                    }

                    // Handle audio response
                    if (part.inlineData != null && !string.IsNullOrEmpty(part.inlineData.data))
                    {
                        receivingAudioStreamIn = true;
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
                if (enableDebugLogs)
                    Debug.Log("Gemini turn complete");
                finishedAudioStreamIn = true;
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

    public void WaitForAudioReception()
    {
        receivingAudioStreamIn = false;
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

        var activityStartMessage = new BidiGenerateContentActivityStartMessage
        {
            realtimeInput = new BidiGenerateContentActivityStartInput
            {
                activityStart = new ActivityStart(),
            },
        };

        var message = new BidiGenerateContentAudioMessage
        {
            realtimeInput = new BidiGenerateContentAudioInput
            {
                audio = new Blob { data = base64Audio, mimeType = $"audio/pcm;rate={sampleRate}" },
            },
        };

        var activityEndMessage = new BidiGenerateContentActivityEndMessage
        {
            realtimeInput = new BidiGenerateContentActivityEndInput
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

    private IEnumerator SendWebSocketMessageCoroutine(object message)
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

        string json = JsonUtility.ToJson(message);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        var sendTask = webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationTokenSource.Token
        );

        while (!sendTask.IsCompleted)
        {
            yield return null;
        }

        if (sendTask.IsFaulted)
        {
            Exception baseException = sendTask.Exception?.GetBaseException();
            string errorMessage = $"Error sending WebSocket message: {baseException?.Message}";

            if (enableDebugLogs)
                Debug.LogError(errorMessage);

            throw new GeminiLiveException(
                "WEBSOCKET_SEND_ERROR",
                "SendWebSocketMessageCoroutine",
                "SendMessage",
                errorMessage,
                baseException
            );
        }
        else if (sendTask.IsCompletedSuccessfully && enableDebugLogs)
        {
            Debug.Log($"Successfully sent {bytes.Length} bytes to Gemini");
        }
    }

    public IEnumerator PlayAudioCoroutine()
    {
        while (IsWebSocketConnected())
        {
            IEnumerator coroutine = null;
            lock (audioCoroutineQueue)
            {
                if (audioCoroutineQueue.Count > 0)
                    coroutine = audioCoroutineQueue.Dequeue();
            }
            if (coroutine != null)
                yield return StartCoroutine(coroutine);
            else
                yield return null;
        }
    }

    public IEnumerator CreateAudioCoroutines()
    {
        finishedAudioStreamIn = false;
        var elapsedTime = 0f;
        var waitTime = 0.05f;
        var maxWaitTime = 0.1f;
        while (IsWebSocketConnected())
        {
            lock (audioResponseQueue)
            {
                if (audioResponseQueue.Count > audioBufferFlushThreshold)
                {
                    StartCoroutine(ProcessAudioQueue());
                    elapsedTime = 0f;
                    yield return new WaitForSeconds(waitTime);
                    continue;
                }
            }

            yield return new WaitForSeconds(waitTime);
            elapsedTime += waitTime;
            lock (audioResponseQueue)
            {
                if (elapsedTime >= maxWaitTime && audioResponseQueue.Count > 0)
                {
                    StartCoroutine(ProcessAudioQueue());
                    elapsedTime = 0f;
                }
            }
        }
    }

    private IEnumerator ProcessAudioQueue()
    {
        List<byte> audioDataList = new List<byte>();

        // Process all available audio data in the queue
        lock (audioResponseQueue)
        {
            while (audioResponseQueue.Count > 0)
            {
                byte[] dequeued = audioResponseQueue.Dequeue();
                audioDataList.AddRange(dequeued);
            }
        }

        if (audioDataList.Count > 0)
        {
            byte[] audioData = audioDataList.ToArray();
            float[] samples = WavUtility.ConvertPCMToFloat(audioData);

            AudioClip responseClip = AudioClip.Create(
                "GeminiResponse",
                samples.Length,
                1,
                24000,
                false
            );
            responseClip.SetData(samples, 0);
            lock (audioCoroutineQueue)
            {
                audioCoroutineQueue.Enqueue(PlayAudioResponse(responseClip));
            }
        }
        yield return null;
    }

    private IEnumerator PlayAudioResponse(AudioClip responseClip)
    {
        audioSource.clip = responseClip;
        audioSource.Play();

        yield return new WaitForSeconds(responseClip.length);

        if (enableDebugLogs)
            Debug.Log("Played audio response from Gemini");
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

    public IEnumerator waitForAudioStreamFinish()
    {
        while (IsAudioActive())
        {
            yield return new WaitForSeconds(0.1f);
        }
        yield return null;
    }

    private bool IsAudioActive()
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
