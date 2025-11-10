using System;
using System.Collections;
using EpicRapBattle.Config;
using UnityEngine;

#region Configuration
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

    private const string GEMINI_WEBSOCKET_URL =
        "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateContentConstrained";

    private const float CONNECTION_SETUP_DELAY = 0.1f;
    private const float SETUP_MESSAGE_DELAY = 1f;
    #endregion

    #region Components
    protected RapBattleConductorLive rapBattleConductorLive;
    protected GeminiWebSocketConnection webSocketConnection;
    protected MessageQueueProcessor messageQueueProcessor;
    #endregion

    #region State
    private string ephemeralKey;
    protected bool isConnected = false;
    protected bool isSetupComplete = false;
    private bool isSetupSent = false;
    private Coroutine receiveCoroutine = null;
    private Coroutine messageQueueCoroutine = null;

    public bool IsConnected => isConnected;
    public bool IsSetupComplete => isSetupComplete;
    #endregion

    #region Public Methods
    public void SetRapBattleConductor(RapBattleConductorLive conductor)
    {
        rapBattleConductorLive = conductor;
    }

    public bool IsWebSocketConnected()
    {
        return webSocketConnection != null && webSocketConnection.IsConnected;
    }

    public IEnumerator Initialize()
    {
        if (!aiConfig)
        {
            string errorMessage = "AIConfig is not assigned!";
            HandleError("CONFIG_ERROR", "Initialize", "ValidateAIConfig", errorMessage);
        }

        ResetConnectionState();
        InitializeComponents();

        try
        {
            ephemeralKey = aiConfig.GenerateEphemeralKey();
        }
        catch (Exception ex)
        {
            HandleError("CONFIG_ERROR", "Initialize", "GenerateEphemeralKey", ex.Message, ex);
        }

        yield return StartCoroutine(ConnectWebSocketCoroutine());
        StartMessageProcessing();

        if (enableDebugLogs)
            Debug.Log($"[{name}] GeminiLive: Initialized - Connected: {isConnected}");
    }

    public IEnumerator WaitForAllMessagesToBeSent(float timeoutSeconds = 30f)
    {
        if (messageQueueProcessor != null)
        {
            yield return StartCoroutine(messageQueueProcessor.WaitForEmpty(timeoutSeconds));
        }
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        webSocketConnection = new GeminiWebSocketConnection(name, enableDebugLogs);
        messageQueueProcessor = new MessageQueueProcessor(enableDebugLogs);

        webSocketConnection.OnMessageReceived += HandleWebSocketMessage;
        webSocketConnection.OnConnected += HandleWebSocketConnected;
        webSocketConnection.OnError += HandleWebSocketError;
    }

    private IEnumerator ConnectWebSocketCoroutine()
    {
        yield return StartCoroutine(
            webSocketConnection.ConnectCoroutine(GEMINI_WEBSOCKET_URL, ephemeralKey)
        );

        if (webSocketConnection.IsConnected)
        {
            yield return new WaitForSeconds(CONNECTION_SETUP_DELAY);
            SendSetupMessage();
            yield return new WaitForSeconds(SETUP_MESSAGE_DELAY);

            if (receiveCoroutine == null)
            {
                receiveCoroutine = StartCoroutine(
                    webSocketConnection.ReceiveMessagesCoroutine(this)
                );
            }
        }
    }

    private void StartMessageProcessing()
    {
        if (messageQueueCoroutine == null)
        {
            messageQueueCoroutine = StartCoroutine(ProcessMessagesQueue());
        }
    }
    #endregion

    #region Message Processing
    private IEnumerator ProcessMessagesQueue()
    {
        const float checkInterval = 0.05f; // Check every 50ms to reduce CPU usage

        while (IsWebSocketConnected())
        {
            object message = messageQueueProcessor.Dequeue();
            if (message != null)
            {
                yield return StartCoroutine(SendWebSocketMessageCoroutine(message));
            }
            else
            {
                yield return new WaitForSeconds(checkInterval);
            }
        }
    }

    private IEnumerator SendWebSocketMessageCoroutine(object message)
    {
        if (!IsWebSocketConnected())
        {
            string errorMessage = $"Cannot send message - State: {webSocketConnection?.State}";
            if (enableDebugLogs)
                Debug.LogWarning($"GeminiLive: {errorMessage}");

            ClearMessageQueue();
            HandleError(
                "CONNECTION_ERROR",
                "SendWebSocketMessageCoroutine",
                "IsWebSocketConnected",
                errorMessage
            );
            yield break;
        }

        string json = GeminiResponseParser.SerializeMessage(message, enableDebugLogs);
        if (json != null)
        {
            yield return StartCoroutine(
                webSocketConnection.SendMessageCoroutine(json, maxChunkSize, this)
            );
        }
    }
    #endregion

    #region Setup
    private void SendSetupMessage()
    {
        if (isSetupSent)
        {
            if (enableDebugLogs)
                Debug.LogWarning("GeminiLive: Setup message already sent, skipping duplicate");
            return;
        }

        string modelString = aiConfig.GeminiLiveModel;
        string voiceName = aiConfig.SelectedGeminiTtsVoice.ToString();

        if (string.IsNullOrEmpty(modelString))
        {
            string errorMessage = "Model string is null or empty!";
            Debug.LogError($"GeminiLive: {errorMessage}");
            HandleError("SETUP_ERROR", "SendSetupMessage", "ValidateModelString", errorMessage);
            return;
        }

        var setupMessage = GetSetupMessage(modelString, voiceName);
        messageQueueProcessor.Enqueue(setupMessage);
        isSetupSent = true;
    }

    protected abstract BidiGenerateContentClientMessage GetSetupMessage(
        string modelString,
        string voiceName
    );
    #endregion

    #region WebSocket Event Handlers
    private void HandleWebSocketMessage(string json)
    {
        ProcessGeminiResponse(json);
    }

    private void HandleWebSocketConnected()
    {
        isConnected = true;
        if (enableDebugLogs)
            Debug.Log($"[{name}] GeminiLive: Connected successfully");
    }

    private void HandleWebSocketError(string errorMessage)
    {
        ClearMessageQueue();
        HandleError("CONNECTION_ERROR", "HandleWebSocketError", "WebSocketError", errorMessage);
    }
    #endregion

    #region Response Processing
    protected virtual void ProcessGeminiResponse(string json)
    {
        try
        {
            if (enableDebugLogs)
                Debug.Log(
                    $"Received Gemini response: {json.Substring(0, Math.Min(json.Length, 200))}..."
                );

            BidiGenerateContentServerMessage response = GeminiResponseParser.ParseServerMessage(
                json,
                enableDebugLogs
            );

            if (response == null)
                return;

            HandleSetupCompletion(response);
            HandleTranscription(response);
            HandleTextResponse(response);
            HandleTurnCompletion(response);
            HandleInterruption(response);
        }
        catch (GeminiLiveException)
        {
            throw; // Re-throw GeminiLiveException as-is
        }
        catch (Exception e)
        {
            string errorMessage = $"Error processing Gemini response: {e.Message}";
            if (enableDebugLogs)
                Debug.LogError(errorMessage);
        }
    }

    private void HandleSetupCompletion(BidiGenerateContentServerMessage response)
    {
        if (GeminiResponseParser.IsSetupComplete(response) && !isSetupComplete)
        {
            isSetupComplete = true;
            if (enableDebugLogs)
                Debug.Log(
                    $"GeminiLive: Setup completed by Gemini Live API. State now - isSetupComplete: {isSetupComplete}, isSetupSent: {isSetupSent}"
                );
        }
    }

    private void HandleTranscription(BidiGenerateContentServerMessage response)
    {
        string transcription = GeminiResponseParser.ExtractTranscription(response);
        if (!string.IsNullOrEmpty(transcription))
        {
            Debug.Log($"Transcription : {transcription}");
            OnTranscriptionReceived(transcription);
        }
    }

    private void HandleTextResponse(BidiGenerateContentServerMessage response)
    {
        if (response.serverContent?.modelTurn?.parts != null)
        {
            if (enableDebugLogs)
                Debug.Log(
                    $"Processing {response.serverContent.modelTurn.parts.Length} parts from Gemini response"
                );

            foreach (var part in response.serverContent.modelTurn.parts)
            {
                if (!string.IsNullOrEmpty(part.text))
                {
                    if (enableDebugLogs)
                        Debug.Log($"Received text response: {part.text}");
                    OnTextResponseReceived(part.text);
                }
            }
        }
    }

    private void HandleTurnCompletion(BidiGenerateContentServerMessage response)
    {
        if (GeminiResponseParser.IsTurnComplete(response))
        {
            if (enableDebugLogs)
                Debug.Log("Gemini turn complete");
        }
    }

    private void HandleInterruption(BidiGenerateContentServerMessage response)
    {
        if (GeminiResponseParser.IsInterrupted(response))
        {
            if (enableDebugLogs)
                Debug.Log("Gemini response interrupted");
        }
    }

    protected abstract void OnTextResponseReceived(string text);

    protected abstract void OnTranscriptionReceived(string text);
    #endregion

    #region Audio Methods
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
            yield break;
        }

        ValidateAndFixAudioSamples(samples);

        byte[] pcmData = WavUtility.ConvertToPCM16(samples);

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

        messageQueueProcessor.Enqueue(message);
        yield return null;
    }

    private void ValidateAndFixAudioSamples(float[] samples)
    {
        bool hasInvalidSamples = false;
        for (int i = 0; i < samples.Length; i++)
        {
            if (
                float.IsNaN(samples[i])
                || float.IsInfinity(samples[i])
                || Mathf.Abs(samples[i]) > 1.0f
            )
            {
                samples[i] = 0f;
                hasInvalidSamples = true;
            }
        }

        if (hasInvalidSamples && enableDebugLogs)
        {
            Debug.LogWarning("GeminiLive: Invalid audio samples detected and corrected");
        }
    }
    #endregion

    #region Activity Methods
    public IEnumerator SendActivityStartToGeminiCoroutine()
    {
        var activityStartMessage = new BidiGenerateContentClientMessage
        {
            realtimeInput = new BidiGenerateContentRealtimeInput { activityStart = new ActivityStart() },
        };

        messageQueueProcessor.Enqueue(activityStartMessage);
        yield return null;
    }

    public IEnumerator SendActivityEndToGeminiCoroutine()
    {
        var activityEndMessage = new BidiGenerateContentClientMessage
        {
            realtimeInput = new BidiGenerateContentRealtimeInput { activityEnd = new ActivityEnd() },
        };

        messageQueueProcessor.Enqueue(activityEndMessage);
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
            yield break;
        }

        var message = new BidiGenerateContentClientMessage
        {
            realtimeInput = new BidiGenerateContentRealtimeInput { text = inputText },
        };

        messageQueueProcessor.Enqueue(message);
        yield return null;
    }
    #endregion

    #region Error Handling
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
    #endregion

    #region State Management
    protected virtual void ResetConnectionState()
    {
        isConnected = false;
        isSetupComplete = false;
        isSetupSent = false;

        if (receiveCoroutine != null)
        {
            StopCoroutine(receiveCoroutine);
            receiveCoroutine = null;
        }

        if (messageQueueCoroutine != null)
        {
            StopCoroutine(messageQueueCoroutine);
            messageQueueCoroutine = null;
        }
    }

    protected virtual void ClearMessageQueue()
    {
        if (messageQueueProcessor != null)
        {
            messageQueueProcessor.Clear();
        }
    }
    #endregion

    #region Cleanup
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
            if (receiveCoroutine != null)
            {
                StopCoroutine(receiveCoroutine);
                receiveCoroutine = null;
            }

            if (messageQueueCoroutine != null)
            {
                StopCoroutine(messageQueueCoroutine);
                messageQueueCoroutine = null;
            }

            ClearMessageQueue();

            if (webSocketConnection != null)
            {
                webSocketConnection.Disconnect();
                webSocketConnection = null;
            }

            ResetConnectionState();

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
    #endregion
}
