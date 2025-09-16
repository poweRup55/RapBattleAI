using System;
using UnityEngine;

public class AIJudgeGeminiLive : GeminiLiveWebRTC
{
    [SerializeField]
    private UIManager uIManager;

    private string responseBuffer = "";

    protected override BidiGenerateContentClientMessage GetSetupMessage(
        string modelString,
        string voiceName
    )
    {
        {
            return new BidiGenerateContentClientMessage
            {
                setup = new BidiGenerateContentSetup
                {
                    model = $"models/{modelString}",
                    generationConfig = new GenerationConfig
                    {
                        responseModalities = new string[] { "text" },
                    },
                    // realtimeInputConfig = new RealtimeInputConfig
                    // {
                    //     automaticActivityDetection = new AutomaticActivityDetection
                    //     {
                    //         disabled = true,
                    //     },
                    //     activityHandling = ActivityHandling.NO_INTERRUPTION,
                    // },
                    systemInstruction = new Content
                    {
                        parts = new Part[] { new Part { text = aiConfig.AIPrompt } },
                    },
                    // proactivity = new ProactivityConfig { proactiveAudio = true },
                },
            };
        }
    }

    protected override void OnTextResponseReceived(string text)
    {
        Debug.Log($"OnTextResponseReceived: {text}");
        responseBuffer += text;

        if (responseBuffer.Contains("[/reaction]"))
        {
            int start = responseBuffer.IndexOf("[reaction]");
            int end = responseBuffer.IndexOf("[/reaction]") + "[/reaction]".Length;
            string reaction = responseBuffer.Substring(start, end - start);
            string reactionContent = reaction
                .Replace("[reaction]", "")
                .Replace("[/reaction]", "")
                .Trim();
            Debug.Log($"Reaction detected: {reactionContent}");
            uIManager.ShowPopUp(reactionContent);
            responseBuffer = responseBuffer.Remove(0, end);
            Debug.Log("Response buffer after reaction processed: " + responseBuffer);
        }
        else if (responseBuffer.Contains("[/round score]"))
        {
            int start = responseBuffer.IndexOf("[round score]");
            int end = responseBuffer.IndexOf("[/round score]") + "[/round score]".Length;
            string score = responseBuffer.Substring(start, end - start);
            string scoreContent = score
                .Replace("[round score]", "")
                .Replace("[/round score]", "")
                .Trim();
            Debug.Log($"Round score detected: {scoreContent}");
            uIManager.UpdateAIJudgeText(scoreContent);
            responseBuffer = responseBuffer.Remove(0, end);
            Debug.Log("Response buffer after round score processed: " + responseBuffer);
        }
    }

    protected override void OnTranscriptionReceived(string text) { }
}
