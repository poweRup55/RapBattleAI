using System;

[Serializable]
public class BidiGenerateContentSetupMessage
{
    public BidiGenerateContentSetup setup;
}

[Serializable]
public class BidiGenerateContentRealtimeMessage
{
    public BidiGenerateContentRealtimeInput realtimeInput;
}

[Serializable]
public class BidiGenerateContentActivityStartMessage
{
    public BidiGenerateContentActivityStartInput realtimeInput;
}

[Serializable]
public class BidiGenerateContentActivityEndMessage
{
    public BidiGenerateContentActivityEndInput realtimeInput;
}

[Serializable]
public class BidiGenerateContentAudioMessage
{
    public BidiGenerateContentAudioInput realtimeInput;
}

[Serializable]
public class BidiGenerateContentRealtimeInput
{
    public ActivityStart activityStart;
    public ActivityEnd activityEnd;
    public Blob audio;
    public string text;
}

[Serializable]
public class BidiGenerateContentActivityStartInput
{
    public ActivityStart activityStart;
}

[Serializable]
public class BidiGenerateContentActivityEndInput
{
    public ActivityEnd activityEnd;
}

[Serializable]
public class BidiGenerateContentAudioInput
{
    public Blob audio;
}

[Serializable]
public class Blob
{
    public string mimeType;
    public string data;
}

[Serializable]
public class BidiGenerateContentSetup
{
    public string model;
    public GenerationConfig generationConfig;
    public RealtimeInputConfig realtimeInputConfig;
    public Content systemInstruction;
    public AudioTranscriptionConfig outputAudioTranscription;
}

[Serializable]
public class AudioTranscriptionConfig { }

[Serializable]
public class GenerationConfig
{
    public string[] responseModalities;
    public SpeechConfig speechConfig;
}

[Serializable]
public class SpeechConfig
{
    public VoiceConfig voiceConfig;
}

[Serializable]
public class VoiceConfig
{
    public PrebuiltVoiceConfig prebuiltVoiceConfig;
}

[Serializable]
public class PrebuiltVoiceConfig
{
    public string voiceName;
}

[Serializable]
public class RealtimeInputConfig
{
    public AutomaticActivityDetection automaticActivityDetection;
}

[Serializable]
public class AutomaticActivityDetection
{
    public bool disabled;
}

[Serializable]
public class ActivityStart { }

[Serializable]
public class ActivityEnd { }

[Serializable]
public class Content
{
    public Part[] parts;
}

[Serializable]
public class Part
{
    public string text;
}

// Response types for parsing Gemini responses
[Serializable]
public class GeminiResponse
{
    public ServerContent serverContent;
    public SetupComplete setupComplete;
    public bool turnComplete;
    public bool interrupted;
}

[Serializable]
public class SetupComplete
{
    // Empty class to match Gemini's response format
}

[Serializable]
public class ServerContent
{
    public ModelTurn modelTurn;
    public bool turnComplete;
    public bool interrupted;
    public OutputTranscription outputTranscription;
}

[Serializable]
public class OutputTranscription
{
    public string text;
}

[Serializable]
public class ModelTurn
{
    public ResponsePart[] parts;
}

[Serializable]
public class ResponsePart
{
    public string text;
    public InlineData inlineData;
}

[Serializable]
public class InlineData
{
    public string mimeType;
    public string data;
}
