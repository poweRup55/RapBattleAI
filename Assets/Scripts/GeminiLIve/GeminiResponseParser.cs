using System;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Centralized JSON deserialization and response parsing for Gemini Live API responses.
/// Provides consistent JSON settings and type-safe parsing methods.
/// </summary>
public static class GeminiResponseParser
{
    private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
    {
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Ignore,
    };

    /// <summary>
    /// Parses a JSON string into a BidiGenerateContentServerMessage object.
    /// </summary>
    public static BidiGenerateContentServerMessage ParseServerMessage(
        string json,
        bool enableDebugLogs = true
    )
    {
        if (string.IsNullOrEmpty(json))
        {
            if (enableDebugLogs)
                Debug.LogWarning("GeminiResponseParser: Attempted to parse null or empty JSON");
            return null;
        }

        try
        {
            BidiGenerateContentServerMessage response =
                JsonConvert.DeserializeObject<BidiGenerateContentServerMessage>(json, jsonSettings);

            if (response == null && enableDebugLogs)
            {
                Debug.LogWarning(
                    "GeminiResponseParser: Failed to parse Gemini response as JSON object"
                );
            }

            return response;
        }
        catch (JsonException ex)
        {
            string errorMessage = $"Error parsing JSON response: {ex.Message}";
            if (enableDebugLogs)
                Debug.LogError($"GeminiResponseParser: {errorMessage}");

            throw new GeminiLiveException(
                "JSON_PARSE_ERROR",
                "GeminiResponseParser",
                "ParseServerMessage",
                errorMessage,
                ex
            );
        }
        catch (Exception ex)
        {
            string errorMessage = $"Unexpected error parsing response: {ex.Message}";
            if (enableDebugLogs)
                Debug.LogError($"GeminiResponseParser: {errorMessage}");

            throw new GeminiLiveException(
                "RESPONSE_PROCESSING_ERROR",
                "GeminiResponseParser",
                "ParseServerMessage",
                errorMessage,
                ex
            );
        }
    }

    /// <summary>
    /// Serializes an object to JSON string using consistent settings.
    /// </summary>
    public static string SerializeMessage(object message, bool enableDebugLogs = true)
    {
        if (message == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("GeminiResponseParser: Attempted to serialize null message");
            return null;
        }

        try
        {
            return JsonConvert.SerializeObject(message, jsonSettings);
        }
        catch (JsonException ex)
        {
            string errorMessage = $"Error serializing message: {ex.Message}";
            if (enableDebugLogs)
                Debug.LogError($"GeminiResponseParser: {errorMessage}");

            throw new GeminiLiveException(
                "JSON_SERIALIZE_ERROR",
                "GeminiResponseParser",
                "SerializeMessage",
                errorMessage,
                ex
            );
        }
        catch (Exception ex)
        {
            string errorMessage = $"Unexpected error serializing message: {ex.Message}";
            if (enableDebugLogs)
                Debug.LogError($"GeminiResponseParser: {errorMessage}");

            throw new GeminiLiveException(
                "MESSAGE_SERIALIZE_ERROR",
                "GeminiResponseParser",
                "SerializeMessage",
                errorMessage,
                ex
            );
        }
    }

    /// <summary>
    /// Extracts text content from a server message.
    /// </summary>
    public static string ExtractText(BidiGenerateContentServerMessage response)
    {
        if (response?.serverContent?.modelTurn?.parts == null)
            return null;

        foreach (var part in response.serverContent.modelTurn.parts)
        {
            if (!string.IsNullOrEmpty(part.text))
            {
                return part.text;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts transcription text from a server message.
    /// </summary>
    public static string ExtractTranscription(BidiGenerateContentServerMessage response)
    {
        return response?.serverContent?.outputTranscription?.text;
    }

    /// <summary>
    /// Checks if the response indicates setup completion.
    /// </summary>
    public static bool IsSetupComplete(BidiGenerateContentServerMessage response)
    {
        return response?.setupComplete != null;
    }

    /// <summary>
    /// Checks if the response indicates turn completion.
    /// </summary>
    public static bool IsTurnComplete(BidiGenerateContentServerMessage response)
    {
        return response?.turnComplete == true || response?.serverContent?.turnComplete == true;
    }

    /// <summary>
    /// Checks if the response indicates interruption.
    /// </summary>
    public static bool IsInterrupted(BidiGenerateContentServerMessage response)
    {
        return response?.interrupted == true || response?.serverContent?.interrupted == true;
    }

    /// <summary>
    /// Gets the JSON serializer settings used by this parser.
    /// </summary>
    public static JsonSerializerSettings GetJsonSettings()
    {
        return jsonSettings;
    }
}
