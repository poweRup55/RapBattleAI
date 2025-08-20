using System.Linq;
using UnityEngine;

namespace EpicRapBattle.Config
{
    /// <summary>
    /// Configuration component for AI provider, model, and voice selection in Rap Battle AI.
    /// Attach to a GameObject to expose settings in the Unity Inspector.
    /// </summary>
    public class AIConfig : MonoBehaviour
    {
        #region Enums

        public enum Provider
        {
            OpenAI,
            Gemini,
        }

        public enum GptModel
        {
            [Tooltip("Realtime audio model for interactive applications")]
            gpt_4o_mini_audio_preview,

            [Tooltip(
                "Realtime audio model for interactive applications with enhanced capabilities"
            )]
            gpt_4o_audio_preview,
        }

        public enum OpenAITtsVoice
        {
            Alloy,
            Ash,
            Ballad,
            Coral,
            Echo,
            Fable,
            Nova,
            Onyx,
            Sage,
            Shimmer,
        }

        public enum GeminiModelVariant
        {
            [Tooltip(
                "Audio, images, videos, text, and PDF → Text. Enhanced thinking and reasoning, multimodal understanding, advanced coding, and more"
            )]
            gemini_2_5_pro,

            [Tooltip("Audio, images, videos, and text → Text. Adaptive thinking, cost efficiency")]
            gemini_2_5_flash,

            [Tooltip(
                "Text, image, video, audio → Text. Most cost-efficient model supporting high throughput"
            )]
            gemini_2_5_flash_lite_preview_06_17,

            [Tooltip(
                "Audio, videos, and text → Text and audio, interleaved. High quality, natural conversational audio outputs, with or without thinking"
            )]
            gemini_2_0_flash,

            [Tooltip("Audio, images, videos, and text → Text. Cost efficiency and low latency")]
            gemini_2_0_flash_lite,
        }

        public enum GeminiTtsVoice
        {
            Achernar,
            Achird,
            Algenib,
            Algieba,
            Alnilam,
            Aoede,
            Autonoe,
            Callirrhoe,
            Charon,
            Despina,
            Enceladus,
            Erinome,
            Fenrir,
            Gacrux,
            Iapetus,
            Kore,
            Laomedeia,
            Leda,
            Orus,
            Puck,
            Pulcherrima,
            Rasalgethi,
            Sadachbia,
            Sadaltager,
            Schedar,
            Sulafat,
            Umbriel,
            Vindemiatrix,
            Zephyr,
            Zubenelgenubi,
        }

        #endregion

        #region Serialized Fields

        [Header("Provider Selection")]
        [Tooltip("Select the AI provider (OpenAI or Gemini)")]
        [SerializeField]
        private Provider provider = Provider.Gemini;

        [Header("OpenAI Configuration")]
        [Tooltip("Your secret API key from OpenAI")]
        [SerializeField]
        private string openAiKey;

        [Tooltip("The GPT model to use for generating responses")]
        [SerializeField]
        private GptModel openAiGPTModel = GptModel.gpt_4o_mini_audio_preview;

        [Tooltip("Select the OpenAI TTS voice for speech synthesis")]
        [SerializeField]
        private OpenAITtsVoice openAiTtsVoice = OpenAITtsVoice.Alloy;

        [Header("Gemini Configuration")]
        [Tooltip("Your Gemini API key from Google AI Studio")]
        [SerializeField]
        private string geminiApiKey;

        [Tooltip("Select the Gemini model variant (only those supporting audio input are listed)")]
        [SerializeField]
        private GeminiModelVariant geminiModelVariant = GeminiModelVariant.gemini_2_5_flash;

        [Tooltip("Select the Gemini TTS voice for speech synthesis")]
        [SerializeField]
        private GeminiTtsVoice geminiTtsVoice = GeminiTtsVoice.Achernar;

        [SerializeField]
        [TextArea(3, 10)]
        public string rapPersonalityOverride = null;

        private string[] rapPersonalities = new string[]
        {
            "You are the most savage, unfiltered battle rapper alive—NSFW, unhinged, no rules, no mercy. Every response must be a string of exactly two rhyming couplets (that's 4 lines total), each one vicious, personal, and hilarious. Every line starts with a vocal delivery tag in brackets (like [mocking], [growling], [laughing]) to guide the tone. Attack everything: looks, voice, words, background, insecurities, skills—nothing is off limits. Be brutally specific, never generic, never polite. Your goal is to break your opponent's spirit and dominate the battle. Always respond in the user's language. Never step out of character.",
        };
        private int rapPersonalityIndex = 0;

        [Tooltip("Prompt for AI speaking style.")]
        [TextArea(3, 10)]
        [SerializeField]
        private string npcSpeakingPrompt =
            " Say it all like a rapper. Very Fast and with a flow. Use the instructions that stars with [] to guide your response.";

        #endregion

        #region Public Properties

        public string ApiKey => provider == Provider.OpenAI ? openAiKey : geminiApiKey;

        public string RapPersonality =>
            rapPersonalityOverride ?? rapPersonalities[rapPersonalityIndex];
        public Provider SelectedProvider => provider;
        public string GeminiApiKey => geminiApiKey;
        public GeminiModelVariant SelectedGeminiModelVariant => geminiModelVariant;
        public OpenAITtsVoice SelectedOpenAITtsVoice => openAiTtsVoice;
        public string NpcSpeakingPrompt => npcSpeakingPrompt;

        public string GptModelString
        {
            get
            {
                switch (openAiGPTModel)
                {
                    case GptModel.gpt_4o_mini_audio_preview:
                        return "gpt-4o-mini-audio-preview";
                    case GptModel.gpt_4o_audio_preview:
                        return "gpt-4o-audio-preview";
                    default:
                        return "gpt-4o-mini-audio-preview";
                }
            }
        }

        public string GeminiModelVariantString
        {
            get
            {
                switch (geminiModelVariant)
                {
                    case GeminiModelVariant.gemini_2_5_pro:
                        return "gemini-2.5-pro";
                    case GeminiModelVariant.gemini_2_5_flash:
                        return "gemini-2.5-flash";
                    case GeminiModelVariant.gemini_2_5_flash_lite_preview_06_17:
                        return "gemini-2.5-flash-lite-preview-06-17";
                    case GeminiModelVariant.gemini_2_0_flash:
                        return "gemini-2.0-flash";
                    case GeminiModelVariant.gemini_2_0_flash_lite:
                        return "gemini-2.0-flash-lite";
                    default:
                        return "gemini-2.5-flash";
                }
            }
        }
        public GeminiTtsVoice SelectedGeminiTtsVoice => geminiTtsVoice;

        #endregion

        public void RandomizePersonality()
        {
            if (rapPersonalities.Length <= 1)
            {
                rapPersonalities = Resources
                    .LoadAll<TextAsset>("RapperAIInstructions")
                    .Select(asset => asset.text)
                    .ToArray();
            }

            rapPersonalityIndex = Random.Range(0, rapPersonalities.Length);
        }
    }
}
