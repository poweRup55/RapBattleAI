using UnityEngine;

namespace EpicRapBattle.Config
{
    public class AIConfig : MonoBehaviour
    {
        public enum GptModel
        {
            [Tooltip("Realtime audio model for interactive applications")]
            gpt_4o_mini_audio_preview,

            [Tooltip(
                "Realtime audio model for interactive applications with enhanced capabilities"
            )]
            gpt_4o_audio_preview,
        }

        public enum TtsVoice
        {
            alloy,
            ash,
            ballad,
            coral,
            echo,
            fable,
            nova,
            onyx,
            sage,
            shimmer,
        }

        [Header("API Configuration")]
        [Tooltip("Your secret API key from OpenAI.")]
        [SerializeField]
        private string apiKey;

        [Header("Model & Voice Selection")]
        [Tooltip("The GPT model to use for generating responses.")]
        [SerializeField]
        private GptModel gptModel = GptModel.gpt_4o_mini_audio_preview;

        [Tooltip("The voice to use for the NPC's speech.")]
        [SerializeField]
        private TtsVoice ttsVoice = TtsVoice.alloy;

        [Tooltip("The personality and context for the AI. This guides its responses.")]
        [TextArea(3, 10)]
        [SerializeField]
        private string rapPersonality =
            "You are a legendary old-school rapper. Your one and only rule is this: you MUST answer every single question in rhyming couplets. Your flow is untouchable, your wordplay is clever, and your confidence is sky-high. Never break character. Let's begin.";

        public string ApiKey => apiKey;
        public string GptModelString
        {
            get
            {
                switch (gptModel)
                {
                    case GptModel.gpt_4o_mini_audio_preview:
                        return "gpt-4o-mini-audio-preview"; // If this is not valid, use "gpt-4o" or the correct model name from OpenAI docs
                    case GptModel.gpt_4o_audio_preview:
                        return "gpt-4o-audio-preview";
                    default:
                        return "gpt-4o";
                }
            }
        }
        public string TtsVoiceString => ttsVoice.ToString();
        public string RapPersonality => rapPersonality;
    }
}
