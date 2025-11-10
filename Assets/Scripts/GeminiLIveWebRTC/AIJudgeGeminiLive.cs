using System;
using System.Text;
using UnityEngine;

public class AIJudgeGeminiLive : GeminiLiveWebRTC
{
    [SerializeField]
    private UIManager uIManager;

    private StringBuilder responseBuffer = new StringBuilder();

    private const string REACTION_START_TAG = "[reaction]";
    private const string REACTION_END_TAG = "[/reaction]";
    private const string ROUND_SCORE_START_TAG = "[round score]";
    private const string ROUND_SCORE_END_TAG = "[/round score]";
    private const string POSITIVE_TAG = "[positive]";
    private const string NEGATIVE_TAG = "[negative]";

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
        if (string.IsNullOrEmpty(text))
            return;

        responseBuffer.Append(text);
        string bufferString = responseBuffer.ToString();

        ProcessReactionTags(ref bufferString);
        ProcessScoreTags(ref bufferString);
    }

    private void ProcessReactionTags(ref string bufferString)
    {
        int reactionStartIndex = bufferString.IndexOf(REACTION_START_TAG);
        while (reactionStartIndex >= 0)
        {
            int reactionEndIndex = bufferString.IndexOf(REACTION_END_TAG, reactionStartIndex);

            if (reactionEndIndex > reactionStartIndex)
            {
                string reactionContent = ExtractTagContent(
                    bufferString,
                    reactionStartIndex,
                    REACTION_START_TAG,
                    reactionEndIndex,
                    REACTION_END_TAG
                );

                if (!string.IsNullOrEmpty(reactionContent))
                {
                    bool isPositive = reactionContent.StartsWith(POSITIVE_TAG);
                    string cleanReactionText = reactionContent
                        .Replace(POSITIVE_TAG, "")
                        .Replace(NEGATIVE_TAG, "")
                        .Trim();

                    if (!string.IsNullOrEmpty(cleanReactionText))
                    {
                        uIManager.AddReaction(cleanReactionText, isPositive);
                    }
                }

                // Remove processed reaction from buffer
                int removeEndIndex = reactionEndIndex + REACTION_END_TAG.Length;
                responseBuffer.Remove(reactionStartIndex, removeEndIndex - reactionStartIndex);
                bufferString = responseBuffer.ToString();
                reactionStartIndex = bufferString.IndexOf(REACTION_START_TAG);
            }
            else
            {
                break;
            }
        }
    }

    private void ProcessScoreTags(ref string bufferString)
    {
        int scoreStartIndex = bufferString.IndexOf(ROUND_SCORE_START_TAG);
        while (scoreStartIndex >= 0)
        {
            int scoreEndIndex = bufferString.IndexOf(ROUND_SCORE_END_TAG, scoreStartIndex);

            if (scoreEndIndex > scoreStartIndex)
            {
                string scoreContent = ExtractTagContent(
                    bufferString,
                    scoreStartIndex,
                    ROUND_SCORE_START_TAG,
                    scoreEndIndex,
                    ROUND_SCORE_END_TAG
                );

                if (!string.IsNullOrEmpty(scoreContent))
                {
                    uIManager.UpdateAIJudgeText(scoreContent);
                }

                int removeEndIndex = scoreEndIndex + ROUND_SCORE_END_TAG.Length;
                responseBuffer.Remove(scoreStartIndex, removeEndIndex - scoreStartIndex);
                bufferString = responseBuffer.ToString();
                scoreStartIndex = bufferString.IndexOf(ROUND_SCORE_START_TAG);
            }
            else
            {
                break;
            }
        }
    }

    private string ExtractTagContent(
        string buffer,
        int startIdx,
        string startTag,
        int endIdx,
        string endTag
    )
    {
        int contentStart = startIdx + startTag.Length;
        int contentLength = endIdx - contentStart;
        if (contentLength > 0)
        {
            return buffer.Substring(contentStart, contentLength).Trim();
        }
        return string.Empty;
    }

    protected override void OnTranscriptionReceived(string text) { }
}
