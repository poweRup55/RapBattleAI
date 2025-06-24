// --- Used for deserializing JSON from OpenAI APIs ---
using System.Collections.Generic;

[System.Serializable]
public class WhisperResponse
{
    public string text;
}

[System.Serializable]
public class GptRequest
{
    public string model;
    public List<ChatMessage> messages;
}

[System.Serializable]
public class GptResponse
{
    public List<GptChoice> choices;
}

[System.Serializable]
public class GptChoice
{
    public ChatMessage message;
}

[System.Serializable]
public class ChatMessage
{
    public string role;
    public string content;
}

[System.Serializable]
public class TtsRequest
{
    public string model;
    public string input;
    public string voice;
}

[System.Serializable]
public class OpenAISessionResponse
{
    public ClientSecret client_secret;
}

[System.Serializable]
public class ClientSecret
{
    public string value;
    public long expires_at;
}

[System.Serializable]
public class BaseOAIEvent
{
    public string event_id;
    public string type;
}

[System.Serializable]
public class InputAudioBufferCommittedEvent : BaseOAIEvent
{
    public string item_id;
    public string previous_item_id;
}

[System.Serializable]
public class InputAudioBufferClearedEvent : BaseOAIEvent { }

[System.Serializable]
public class InputAudioBufferSpeechStartedEvent : BaseOAIEvent
{
    public int audio_start_ms;
    public string item_id;
}

[System.Serializable]
public class InputAudioBufferSpeechStoppedEvent : BaseOAIEvent
{
    public int audio_end_ms;
    public string item_id;
}

[System.Serializable]
public class ConversationItemCreatedEvent : BaseOAIEvent
{
    public string conversation_id;
    public string item_id;
    public string role;
    public string content;
    public long created_at;
}

[System.Serializable]
public class ResponseCreatedEvent : BaseOAIEvent
{
    public string response_id;
    public string conversation_id;
    public long created_at;
}

[System.Serializable]
public class ResponseOutputItemAddedEvent : BaseOAIEvent
{
    public string response_id;
    public string item_id;
    public string content_type;
    public string content;
}

[System.Serializable]
public class ResponseContentPartAddedEvent : BaseOAIEvent
{
    public string response_id;
    public string part_id;
    public string content;
}

[System.Serializable]
public class ResponseAudioTranscriptDeltaEvent : BaseOAIEvent
{
    public string response_id;
    public string transcript;
    public bool is_final;
    public string delta;
}

[System.Serializable]
public class OutputAudioBufferStartedEvent : BaseOAIEvent
{
    public string response_id;
    public long started_at;
}

[System.Serializable]
public class ResponseAudioDoneEvent : BaseOAIEvent
{
    public string response_id;
    public long finished_at;
}

[System.Serializable]
public class ResponseAudioTranscriptDoneEvent : BaseOAIEvent
{
    public string response_id;
    public string transcript;
}

[System.Serializable]
public class ResponseContentPartDoneEvent : BaseOAIEvent
{
    public string response_id;
    public string part_id;
}

[System.Serializable]
public class ResponseOutputItemDoneEvent : BaseOAIEvent
{
    public string response_id;
    public string item_id;
}

[System.Serializable]
public class ResponseDoneEvent : BaseOAIEvent
{
    public string response_id;
}

[System.Serializable]
public class RateLimitsUpdatedEvent : BaseOAIEvent
{
    public int requests_remaining;
    public int tokens_remaining;
    public long reset_at;
}
