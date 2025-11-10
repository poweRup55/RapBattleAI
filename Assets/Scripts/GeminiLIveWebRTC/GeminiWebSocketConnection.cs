using System;
using System.Collections;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Manages WebSocket connection lifecycle, state, and async operations for Gemini Live API.
/// Provides thread-safe connection management and message sending/receiving.
/// </summary>
public class GeminiWebSocketConnection
{
    private const string USER_AGENT = "Rap-Against-The-Machine";
    private const int CONNECTION_WAIT_TIME = 100; // milliseconds
    private const int SETUP_WAIT_TIME = 1000; // milliseconds

    private ClientWebSocket webSocket;
    private CancellationTokenSource cancellationTokenSource;
    private bool isConnected = false;
    private bool enableDebugLogs;
    private string agentName;

    public bool IsConnected =>
        isConnected && webSocket != null && webSocket.State == WebSocketState.Open;
    public WebSocketState State => webSocket?.State ?? WebSocketState.None;

    public event Action<string> OnMessageReceived;
    public event Action OnConnected;
    public event Action<string> OnError;

    public GeminiWebSocketConnection(string agentName = "Unknown", bool enableDebugLogs = true)
    {
        this.agentName = agentName;
        this.enableDebugLogs = enableDebugLogs;
    }

    /// <summary>
    /// Connects to the Gemini WebSocket endpoint.
    /// </summary>
    public IEnumerator ConnectCoroutine(string url, string ephemeralKey)
    {
        cancellationTokenSource = new CancellationTokenSource();
        webSocket = new ClientWebSocket();

        webSocket.Options.SetRequestHeader("User-Agent", USER_AGENT);
        webSocket.Options.SetRequestHeader("Authorization", $"Token {ephemeralKey}");

        Uri uri = new Uri(url);

        if (enableDebugLogs)
            Debug.Log($"[{agentName}] Connecting to Gemini Live API: {url}");

        ConnectWebSocketAsync(uri);

        if (webSocket?.State == WebSocketState.Open)
        {
            yield return new WaitForSeconds(CONNECTION_WAIT_TIME / 1000f);
            isConnected = true;
            OnConnected?.Invoke();
        }
        else
        {
            string errorMessage = $"Failed to connect - State: {webSocket?.State}";
            if (enableDebugLogs)
                Debug.LogError(
                    $"[{agentName}] GeminiLive: Connection timeout - State: {webSocket?.State}"
                );

            OnError?.Invoke(errorMessage);
        }
    }

    private void ConnectWebSocketAsync(Uri uri)
    {
        if (cancellationTokenSource == null)
        {
            string errorMessage = "CancellationTokenSource is null";
            OnError?.Invoke(errorMessage);
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

        if (enableDebugLogs)
            Debug.LogError($"[{agentName}] GeminiLive: {errorMessage}");

        OnError?.Invoke(errorMessage);
    }

    /// <summary>
    /// Receives messages from the WebSocket connection.
    /// </summary>
    public IEnumerator ReceiveMessagesCoroutine(MonoBehaviour coroutineRunner)
    {
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
                var localCancellationTokenSource = cancellationTokenSource;
                var localWebSocket = webSocket;

                if (localCancellationTokenSource == null || localWebSocket == null)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning(
                            $"[{agentName}] GeminiLive: CancellationTokenSource or WebSocket is null, stopping receive loop"
                        );
                    break;
                }

                if (localCancellationTokenSource.IsCancellationRequested)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning(
                            $"[{agentName}] GeminiLive: Cancellation requested, stopping receive loop"
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
                            $"[{agentName}] GeminiLive: WebSocket disposed during ReceiveAsync, stopping receive loop"
                        );
                    break;
                }
                catch (Exception e)
                {
                    if (enableDebugLogs)
                        Debug.LogError(
                            $"[{agentName}] GeminiLive: Error starting ReceiveAsync: {e.Message}"
                        );
                    break;
                }

                if (receiveTask == null)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning(
                            $"[{agentName}] GeminiLive: ReceiveAsync task is null, stopping receive loop"
                        );
                    break;
                }

                while (!receiveTask.IsCompleted)
                {
                    if (
                        webSocket == null
                        || cancellationTokenSource == null
                        || cancellationTokenSource.IsCancellationRequested
                    )
                    {
                        if (enableDebugLogs)
                            Debug.LogWarning(
                                $"[{agentName}] GeminiLive: Cleanup initiated while waiting for receive task, stopping"
                            );
                        yield break;
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
                                $"[{agentName}] GeminiLive: Error getting receive task result: {e.Message}"
                            );
                        break;
                    }

                    if (result == null)
                    {
                        if (enableDebugLogs)
                            Debug.LogWarning(
                                $"[{agentName}] GeminiLive: Receive task result is null, stopping receive loop"
                            );
                        break;
                    }

                    if (enableDebugLogs)
                        Debug.Log(
                            $"[{agentName}] Received message type: {result.MessageType}, Count: {result.Count}, EndOfMessage: {result.EndOfMessage}"
                        );

                    switch (result.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            ParseTextMessage(buffer, messageBuilder, result);
                            break;
                        case WebSocketMessageType.Binary:
                            ParseBinaryMessage(buffer, result);
                            break;
                        case WebSocketMessageType.Close:
                            HandleCloseMessage(result);
                            break;
                    }
                }
            }
        }
        finally
        {
            if (enableDebugLogs)
                Debug.Log($"[{agentName}] GeminiLive: ReceiveMessagesCoroutine exited");
        }
    }

    private void ParseTextMessage(
        byte[] buffer,
        StringBuilder messageBuilder,
        WebSocketReceiveResult result
    )
    {
        string chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
        messageBuilder.Append(chunk);

        if (result.EndOfMessage)
        {
            string completeMessage = messageBuilder.ToString();
            OnMessageReceived?.Invoke(completeMessage);
            messageBuilder.Clear();
        }
    }

    private void ParseBinaryMessage(byte[] buffer, WebSocketReceiveResult result)
    {
        if (enableDebugLogs)
            Debug.Log($"[{agentName}] Received binary data, count: {result.Count} bytes");

        byte[] binaryData = new byte[result.Count];
        Array.Copy(buffer, 0, binaryData, 0, result.Count);

        try
        {
            string jsonMessage = Encoding.UTF8.GetString(binaryData);

            if (jsonMessage.TrimStart().StartsWith("{") && jsonMessage.TrimEnd().EndsWith("}"))
            {
                OnMessageReceived?.Invoke(jsonMessage);
            }
        }
        catch (DecoderFallbackException ex)
        {
            if (enableDebugLogs)
                Debug.Log(
                    $"[{agentName}] Failed to decode binary as UTF-8: {ex.Message}, treating as audio data"
                );
        }
    }

    private void HandleCloseMessage(WebSocketReceiveResult result)
    {
        if (enableDebugLogs)
            Debug.LogWarning($"[{agentName}] GeminiLive: WebSocket closed - {result.CloseStatus}");

        string errorMessage =
            $"WebSocket closed - {result.CloseStatus}: {result.CloseStatusDescription}";
        OnError?.Invoke(errorMessage);
    }

    private void LogWebSocketReceiveError(
        System.Threading.Tasks.Task<WebSocketReceiveResult> receiveTask
    )
    {
        if (receiveTask.Exception?.GetBaseException() is OperationCanceledException)
        {
            return;
        }

        Exception baseException = receiveTask.Exception?.GetBaseException();

        if (enableDebugLogs)
            Debug.LogError($"[{agentName}] GeminiLive: Receive error - {baseException?.Message}");

        OnError?.Invoke($"Receive error - {baseException?.Message}");
    }

    /// <summary>
    /// Sends a message through the WebSocket connection.
    /// </summary>
    public IEnumerator SendMessageCoroutine(
        string json,
        int maxChunkSize,
        MonoBehaviour coroutineRunner
    )
    {
        if (!IsConnected)
        {
            string errorMessage = $"Cannot send message - State: {State}";
            if (enableDebugLogs)
                Debug.LogWarning($"[{agentName}] GeminiLive: {errorMessage}");

            OnError?.Invoke(errorMessage);
            yield break;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(json);
        int totalBytes = bytes.Length;
        int offset = 0;

        while (offset < totalBytes)
        {
            int chunkSize = Math.Min(maxChunkSize, totalBytes - offset);
            bool endOfMessage = (offset + chunkSize) >= totalBytes;
            var chunk = new ArraySegment<byte>(bytes, offset, chunkSize);

            yield return coroutineRunner.StartCoroutine(SendMessageChunk(endOfMessage, chunk));
            offset += chunkSize;
        }
    }

    private IEnumerator SendMessageChunk(bool endOfMessage, ArraySegment<byte> chunk)
    {
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
                    $"[{agentName}] GeminiLive: Cannot send message chunk - cancellation requested, token is null, or WebSocket is null"
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
                Debug.LogWarning($"[{agentName}] GeminiLive: WebSocket disposed during SendAsync");
            yield break;
        }
        catch (Exception e)
        {
            if (enableDebugLogs)
                Debug.LogError($"[{agentName}] GeminiLive: Error starting SendAsync: {e.Message}");
            yield break;
        }

        if (sendTask == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[{agentName}] GeminiLive: SendAsync task is null");
            yield break;
        }

        while (!sendTask.IsCompleted)
        {
            if (
                webSocket == null
                || cancellationTokenSource == null
                || cancellationTokenSource.IsCancellationRequested
            )
            {
                if (enableDebugLogs)
                    Debug.LogWarning(
                        $"[{agentName}] GeminiLive: Cleanup initiated while waiting for send task, stopping"
                    );
                yield break;
            }
            yield return null;
        }

        if (sendTask.IsFaulted)
        {
            string errorMessage = sendTask.Exception?.GetBaseException()?.Message;
            if (enableDebugLogs)
                Debug.LogError($"[{agentName}] Error sending message: {errorMessage}");

            OnError?.Invoke($"Send message error: {errorMessage}");
        }
    }

    /// <summary>
    /// Disconnects and cleans up the WebSocket connection.
    /// </summary>
    public void Disconnect()
    {
        try
        {
            if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
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
                        if (enableDebugLogs)
                            Debug.LogWarning(
                                $"[{agentName}] GeminiLive: Error during WebSocket close: {e.Message}"
                            );
                    }
                }

                try
                {
                    webSocket.Dispose();
                }
                catch (Exception e)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning(
                            $"[{agentName}] GeminiLive: Error disposing WebSocket: {e.Message}"
                        );
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
                    if (enableDebugLogs)
                        Debug.LogWarning(
                            $"[{agentName}] GeminiLive: Error disposing CancellationTokenSource: {e.Message}"
                        );
                }
                finally
                {
                    cancellationTokenSource = null;
                }
            }

            isConnected = false;

            if (enableDebugLogs)
                Debug.Log($"[{agentName}] GeminiLive: WebSocket connection cleanup completed");
        }
        catch (Exception e)
        {
            if (enableDebugLogs)
                Debug.LogError(
                    $"[{agentName}] GeminiLive: Critical error during cleanup: {e.Message}"
                );
        }
    }
}
