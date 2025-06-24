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
