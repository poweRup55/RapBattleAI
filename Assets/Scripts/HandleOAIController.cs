using System;
using UnityEngine;

public class OAIEventController
{
    public static event Action<InputAudioBufferCommittedEvent> OnInputAudioBufferCommittedEvent;
    public static event Action<InputAudioBufferClearedEvent> OnInputAudioBufferClearedEvent;
    public static event Action<InputAudioBufferSpeechStartedEvent> OnInputAudioBufferSpeechStartedEvent;
    public static event Action<InputAudioBufferSpeechStoppedEvent> OnInputAudioBufferSpeechStoppedEvent;
    public static event Action<ConversationItemCreatedEvent> OnConversationItemCreatedEvent;
    public static event Action<ResponseCreatedEvent> OnResponseCreatedEvent;
    public static event Action<ResponseOutputItemAddedEvent> OnResponseOutputItemAddedEvent;
    public static event Action<ResponseContentPartAddedEvent> OnResponseContentPartAddedEvent;
    public static event Action<ResponseAudioTranscriptDeltaEvent> OnResponseAudioTranscriptDeltaEvent;
    public static event Action<OutputAudioBufferStartedEvent> OnOutputAudioBufferStartedEvent;
    public static event Action<ResponseAudioDoneEvent> OnResponseAudioDoneEvent;
    public static event Action<ResponseAudioTranscriptDoneEvent> OnResponseAudioTranscriptDoneEvent;
    public static event Action<ResponseContentPartDoneEvent> OnResponseContentPartDoneEvent;
    public static event Action<ResponseOutputItemDoneEvent> OnResponseOutputItemDoneEvent;
    public static event Action<ResponseDoneEvent> OnResponseDoneEvent;
    public static event Action<RateLimitsUpdatedEvent> OnRateLimitsUpdatedEvent;

    public static void HandleOAIEvent(string json)
    {
        BaseOAIEvent baseEvent = JsonUtility.FromJson<BaseOAIEvent>(json);
        switch (baseEvent.type)
        {
            case "input_audio_buffer.committed":
                TriggerEvent(
                    JsonUtility.FromJson<InputAudioBufferCommittedEvent>(json),
                    OnInputAudioBufferCommittedEvent
                );
                break;
            case "input_audio_buffer.cleared":
                TriggerEvent(
                    JsonUtility.FromJson<InputAudioBufferClearedEvent>(json),
                    OnInputAudioBufferClearedEvent
                );
                break;
            case "input_audio_buffer.speech_started":
                TriggerEvent(
                    JsonUtility.FromJson<InputAudioBufferSpeechStartedEvent>(json),
                    OnInputAudioBufferSpeechStartedEvent
                );
                break;
            case "input_audio_buffer.speech_stopped":
                TriggerEvent(
                    JsonUtility.FromJson<InputAudioBufferSpeechStoppedEvent>(json),
                    OnInputAudioBufferSpeechStoppedEvent
                );
                break;
            case "conversation.item.created":
                TriggerEvent(
                    JsonUtility.FromJson<ConversationItemCreatedEvent>(json),
                    OnConversationItemCreatedEvent
                );
                break;
            case "response.created":
                TriggerEvent(
                    JsonUtility.FromJson<ResponseCreatedEvent>(json),
                    OnResponseCreatedEvent
                );
                break;
            case "response.output_item.added":
                TriggerEvent(
                    JsonUtility.FromJson<ResponseOutputItemAddedEvent>(json),
                    OnResponseOutputItemAddedEvent
                );
                break;
            case "response.content_part.added":
                TriggerEvent(
                    JsonUtility.FromJson<ResponseContentPartAddedEvent>(json),
                    OnResponseContentPartAddedEvent
                );
                break;
            case "response.audio_transcript.delta":
                TriggerEvent(
                    JsonUtility.FromJson<ResponseAudioTranscriptDeltaEvent>(json),
                    OnResponseAudioTranscriptDeltaEvent
                );
                break;
            case "output_audio_buffer.started":
                TriggerEvent(
                    JsonUtility.FromJson<OutputAudioBufferStartedEvent>(json),
                    OnOutputAudioBufferStartedEvent
                );
                break;
            case "response.audio.done":
                TriggerEvent(
                    JsonUtility.FromJson<ResponseAudioDoneEvent>(json),
                    OnResponseAudioDoneEvent
                );
                break;
            case "response.audio_transcript.done":
                TriggerEvent(
                    JsonUtility.FromJson<ResponseAudioTranscriptDoneEvent>(json),
                    OnResponseAudioTranscriptDoneEvent
                );
                break;
            case "response.content_part.done":
                TriggerEvent(
                    JsonUtility.FromJson<ResponseContentPartDoneEvent>(json),
                    OnResponseContentPartDoneEvent
                );
                break;
            case "response.output_item.done":
                TriggerEvent(
                    JsonUtility.FromJson<ResponseOutputItemDoneEvent>(json),
                    OnResponseOutputItemDoneEvent
                );
                break;
            case "response.done":
                TriggerEvent(JsonUtility.FromJson<ResponseDoneEvent>(json), OnResponseDoneEvent);
                break;
            case "rate_limits.updated":
                TriggerEvent(
                    JsonUtility.FromJson<RateLimitsUpdatedEvent>(json),
                    OnRateLimitsUpdatedEvent
                );
                break;
            default:
                Debug.LogWarning($"Unknown OAI event type: {baseEvent.type}");
                break;
        }
    }

    private static void TriggerEvent<T>(T evt, Action<T> eventAction)
    {
        if (eventAction != null)
        {
            eventAction(evt);
        }
    }
}
