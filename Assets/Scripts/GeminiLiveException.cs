using System;

/// <summary>
/// Custom exception class for handling errors in the Gemini Live WebRTC implementation.
/// Provides detailed error information including error type, context, action, and message.
/// </summary>
[Serializable]
public class GeminiLiveException : Exception
{
    public string ErrorType { get; private set; }
    public string Context { get; private set; }
    public string Action { get; private set; }

    public GeminiLiveException()
        : base() { }

    public GeminiLiveException(string message)
        : base(message) { }

    public GeminiLiveException(string message, Exception innerException)
        : base(message, innerException) { }

    public GeminiLiveException(string errorType, string context, string action, string message)
        : base($"[{errorType}] {message} | Context: {context} | Action: {action}")
    {
        ErrorType = errorType;
        Context = context;
        Action = action;
    }

    public GeminiLiveException(
        string errorType,
        string context,
        string action,
        string message,
        Exception innerException
    )
        : base($"[{errorType}] {message} | Context: {context} | Action: {action}", innerException)
    {
        ErrorType = errorType;
        Context = context;
        Action = action;
    }
}
