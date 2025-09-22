using System;
using System.Collections.Generic;

[Serializable]
public class BidiGenerateContentClientMessage
{
    public BidiGenerateContentSetup setup;
    public BidiGenerateContentClientContent clientContent;
    public BidiGenerateContentRealtimeInput realtimeInput;
    public BidiGenerateContentToolResponse toolResponse;
}

[Serializable]
public class BidiGenerateContentRealtimeInput
{
    public Blob[] mediaChunks;
    public Blob audio;
    public Blob video;
    public ActivityStart activityStart;
    public ActivityEnd activityEnd;
    public bool audioStreamEnd;
    public string text;
}

[Serializable]
public class BidiGenerateContentClientContent
{
    public Content[] turns;
    public bool turnComplete;
}

[Serializable]
public class BidiGenerateContentToolResponse
{
    public FunctionResponse[] functionResponses;
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
    public Content systemInstruction;
    public Tool[] tools;
    public RealtimeInputConfig realtimeInputConfig;
    public SessionResumptionConfig sessionResumption;
    public ContextWindowCompressionConfig contextWindowCompression;
    public AudioTranscriptionConfig inputAudioTranscription;
    public AudioTranscriptionConfig outputAudioTranscription;
    public ProactivityConfig proactivity;
}

[Serializable]
public class AudioTranscriptionConfig { }

[Serializable]
public class GenerationConfig
{
    public string[] stopSequences;
    public string responseMimeType;
    public Schema responseSchema;
    public object responseJsonSchema;
    public string[] responseModalities;
    public int candidateCount;
    public int maxOutputTokens;
    public float temperature;
    public float topP;
    public int topK;
    public int seed;
    public float presencePenalty;
    public float frequencyPenalty;
    public bool responseLogprobs;
    public int logprobs;
    public bool enableEnhancedCivicAnswers;
    public SpeechConfig speechConfig;
    public ThinkingConfig thinkingConfig;
    public MediaResolution mediaResolution;
    public bool enableAffectiveDialog;
    public ContextWindowCompressionConfig contextWindowCompression;
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
    public ActivityHandling activityHandling;
    public TurnCoverage turnCoverage;
}

[Serializable]
public class AutomaticActivityDetection
{
    public bool disabled;
    public StartSensitivity startOfSpeechSensitivity;
    public int prefixPaddingMs;
    public EndSensitivity endOfSpeechSensitivity;
    public int silenceDurationMs;
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
public class BidiGenerateContentServerMessage
{
    public UsageMetadata usageMetadata;
    public BidiGenerateContentSetupComplete setupComplete;
    public BidiGenerateContentServerContent serverContent;
    public BidiGenerateContentToolCall toolCall;
    public BidiGenerateContentToolCallCancellation toolCallCancellation;
    public GoAway goAway;
    public SessionResumptionUpdate sessionResumptionUpdate;
    public bool turnComplete;
    public bool interrupted;
}

[Serializable]
public class BidiGenerateContentSetupComplete { }

[Serializable]
public class BidiGenerateContentServerContent
{
    public ModelTurn modelTurn;
    public bool generationComplete;
    public bool turnComplete;
    public bool interrupted;
    public GroundingMetadata groundingMetadata;
    public BidiGenerateContentTranscription inputTranscription;
    public BidiGenerateContentTranscription outputTranscription;
    public UrlContextMetadata urlContextMetadata;
}

[Serializable]
public class BidiGenerateContentTranscription
{
    public string text;
}

// Missing types that need to be defined
[Serializable]
public class FunctionResponse
{
    public string name;
    public object response;
}

[Serializable]
public class Tool
{
    public FunctionDeclaration[] functionDeclarations;
}

[Serializable]
public class FunctionDeclaration
{
    public string name;
    public string description;
    public Schema parameters;
}

[Serializable]
public class SessionResumptionConfig
{
    public string handle;
}

[Serializable]
public class ContextWindowCompressionConfig
{
    public SlidingWindow slidingWindow;
    public long triggerTokens;
}

[Serializable]
public class SlidingWindow
{
    public long targetTokens;
}

[Serializable]
public class ProactivityConfig
{
    public bool proactiveAudio;
}

public enum MediaResolution
{
    LOW,
    MEDIUM,
    HIGH,
}

[Serializable]
public enum ActivityHandling
{
    ACTIVITY_HANDLING_UNSPECIFIED,
    START_OF_ACTIVITY_INTERRUPTS,
    NO_INTERRUPTION,
}

[Serializable]
public enum TurnCoverage
{
    TURN_COVERAGE_UNSPECIFIED,
    TURN_INCLUDES_ONLY_ACTIVITY,
    TURN_INCLUDES_ALL_INPUT,
}

[Serializable]
public enum StartSensitivity
{
    START_SENSITIVITY_UNSPECIFIED,
    START_SENSITIVITY_HIGH,
    START_SENSITIVITY_LOW,
}

[Serializable]
public enum EndSensitivity
{
    END_SENSITIVITY_UNSPECIFIED,
    END_SENSITIVITY_HIGH,
    END_SENSITIVITY_LOW,
}

[Serializable]
public class UsageMetadata
{
    public int promptTokenCount;
    public int cachedContentTokenCount;
    public int responseTokenCount;
    public int toolUsePromptTokenCount;
    public int thoughtsTokenCount;
    public int totalTokenCount;
    public ModalityTokenCount[] promptTokensDetails;
    public ModalityTokenCount[] cacheTokensDetails;
    public ModalityTokenCount[] responseTokensDetails;
    public ModalityTokenCount[] toolUsePromptTokensDetails;
}

[Serializable]
public class ModalityTokenCount
{
    public string modality;
    public int tokenCount;
}

[Serializable]
public class BidiGenerateContentToolCall
{
    public FunctionCall[] functionCalls;
}

[Serializable]
public class FunctionCall
{
    public string name;
    public object args;
    public string id;
}

[Serializable]
public class BidiGenerateContentToolCallCancellation
{
    public string[] ids;
}

[Serializable]
public class GoAway
{
    public string timeLeft;
}

[Serializable]
public class SessionResumptionUpdate
{
    public string newHandle;
    public bool resumable;
}

[Serializable]
public class GroundingMetadata { }

[Serializable]
public class UrlContextMetadata
{
    public UrlMetadata[] urlMetadata;
}

[Serializable]
public class UrlMetadata
{
    public string url;
    public string title;
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

[Serializable]
public class Schema
{
    public TYPE type;
    public string format;
    public string title;
    public string description;
    public bool nullable;
    public Dictionary<string, Schema> properties;
    public Schema items;
    public string[] enumValues;
    public object additionalProperties;
    public string[] required;
    public string maxItems;
    public string minItems;
    public string minProperties;
    public string maxProperties;
    public string minLength;
    public string maxLength;
    public string pattern;
    public object example;
    public Schema[] anyOf;
    public string[] propertyOrdering;
    public object defaultValue;
    public float minimum;
    public float maximum;
}

[Serializable]
public enum TYPE
{
    TYPE_UNSPECIFIED,
    STRING,
    NUMBER,
    INTEGER,
    BOOLEAN,
    ARRAY,
    OBJECT,
    NULL,
}

public enum Modality
{
    TEXT,
    AUDIO,
    VIDEO,
}

[Serializable]
public class ThinkingConfig { }
