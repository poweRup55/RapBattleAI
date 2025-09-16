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
        responseBuffer += text;

        // Process all complete reactions first
        while (responseBuffer.Contains("[reaction]") && responseBuffer.Contains("[/reaction]"))
        {
            int startIndex = responseBuffer.IndexOf("[reaction]");
            int endIndex = responseBuffer.IndexOf("[/reaction]", startIndex);

            if (endIndex > startIndex)
            {
                endIndex += "[/reaction]".Length;
                string reaction = responseBuffer.Substring(startIndex, endIndex - startIndex);
                string reactionContent = reaction
                    .Replace("[reaction]", "")
                    .Replace("[/reaction]", "")
                    .Trim();

                if (!string.IsNullOrEmpty(reactionContent))
                {
                    uIManager.ShowPopUp(reactionContent);
                }

                responseBuffer = responseBuffer.Remove(startIndex, endIndex - startIndex);
            }
            else
            {
                break; // Incomplete tag, wait for more data
            }
        }

        // Process all complete round scores
        while (
            responseBuffer.Contains("[round score]") && responseBuffer.Contains("[/round score]")
        )
        {
            int startIndex = responseBuffer.IndexOf("[round score]");
            int endIndex = responseBuffer.IndexOf("[/round score]", startIndex);

            if (endIndex > startIndex)
            {
                endIndex += "[/round score]".Length;
                string score = responseBuffer.Substring(startIndex, endIndex - startIndex);
                string scoreContent = score
                    .Replace("[round score]", "")
                    .Replace("[/round score]", "")
                    .Trim();

                if (!string.IsNullOrEmpty(scoreContent))
                {
                    uIManager.UpdateAIJudgeText(scoreContent);
                }

                responseBuffer = responseBuffer.Remove(startIndex, endIndex - startIndex);
            }
            else
            {
                break; // Incomplete tag, wait for more data
            }
        }
    }

    protected override void OnTranscriptionReceived(string text) { }
}
