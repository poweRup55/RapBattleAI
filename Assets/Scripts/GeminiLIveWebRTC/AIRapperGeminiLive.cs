using System;
using UnityEngine;

public class AIRapperGeminiLive : GeminiLiveWebRTCAudio
{
    [SerializeField]
    private UIManager uIManager;

    protected override BidiGenerateContentClientMessage GetSetupMessage(
        string modelString,
        string voiceName
    )
    {
        return new BidiGenerateContentClientMessage
        {
            setup = new BidiGenerateContentSetup
            {
                model = $"models/{modelString}",
                generationConfig = new GenerationConfig
                {
                    responseModalities = new string[] { Modality.AUDIO.ToString() },
                    speechConfig = new SpeechConfig
                    {
                        voiceConfig = new VoiceConfig
                        {
                            prebuiltVoiceConfig = new PrebuiltVoiceConfig { voiceName = voiceName },
                        },
                    },
                    temperature = temperature,
                    // topP = topP,
                    // topK = topK,
                    maxOutputTokens = maxOutputTokens,
                    candidateCount = candidateCount,
                },
                realtimeInputConfig = new RealtimeInputConfig
                {
                    automaticActivityDetection = new AutomaticActivityDetection { disabled = true },
                    activityHandling = ActivityHandling.NO_INTERRUPTION,
                },
                systemInstruction = new Content
                {
                    parts = new Part[] { new Part { text = aiConfig.AIPrompt } },
                },
                proactivity = new ProactivityConfig { proactiveAudio = false },
                outputAudioTranscription = new AudioTranscriptionConfig { },
            },
        };
    }

    protected override void OnTextResponseReceived(string text)
    {
        uIManager.AppendComputerText(text);
    }

    protected override void OnTranscriptionReceived(string text)
    {
        uIManager.AppendComputerText(text);
    }
}
