using UnityEngine;

namespace EpicRapBattle.Config
{
    /// <summary>
    /// Configuration component for AI provider, model, and voice selection in Rap Battle AI.
    /// Attach to a GameObject to expose settings in the Unity Inspector.
    /// </summary>
    public class AIConfig : MonoBehaviour
    {
        // --- Enums ---
        public enum Provider
        {
            OpenAI,
            Gemini,
        }

        [Header("Provider Selection")]
        [Tooltip("Select the AI provider (OpenAI or Gemini)")]
        [SerializeField]
        private Provider provider = Provider.Gemini;

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
            achernar,
            achird,
            algenib,
            algieba,
            alnilam,
            aoede,
            autonoe,
            callirrhoe,
            charon,
            despina,
            enceladus,
            erinome,
            fenrir,
            gacrux,
            iapetus,
            kore,
            laomedeia,
            leda,
            orus,
            puck,
            pulcherrima,
            rasalgethi,
            sadachbia,
            sadaltager,
            schedar,
            sulafat,
            umbriel,
            vindemiatrix,
            zephyr,
            zubenelgenubi,
        }

        // --- Serialized Fields ---
        [Header("Open AI API Configuration")]
        [Tooltip("Your secret API key from OpenAI.")]
        [SerializeField]
        private string openAiKey;

        [Header("Gemini API Configuration")]
        [Tooltip("Your Gemini API key from Google AI Studio.")]
        [SerializeField]
        private string geminiApiKey;

        [Header("Model & Voice Selection")]
        [Tooltip("The GPT model to use for generating responses.")]
        [SerializeField]
        private GptModel openAiGPTModel = GptModel.gpt_4o_mini_audio_preview;

        [Header("Gemini Model Variant")]
        [Tooltip("Select the Gemini model variant (only those supporting audio input are listed)")]
        [SerializeField]
        private GeminiModelVariant geminiModelVariant = GeminiModelVariant.gemini_2_5_flash;

        [Header("Gemini TTS Voice")]
        [Tooltip("Select the Gemini TTS voice for speech synthesis.")]
        [SerializeField]
        private GeminiTtsVoice geminiTtsVoice = GeminiTtsVoice.achernar;

        [Tooltip("The personality and context for the AI. This guides its responses.")]
        [TextArea(3, 10)]
        [SerializeField]
        private string rapPersonality =
            "You are a legendary old-school rapper. Your one and only rule is this: you MUST answer every single question in rhyming couplets. Your flow is untouchable, your wordplay is clever, and your confidence is sky-high. Never break character. Let's begin.";

        // --- Properties ---
        public string ApiKey => provider == Provider.OpenAI ? openAiKey : geminiApiKey;
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
        public string RapPersonality => rapPersonality;
        public Provider SelectedProvider => provider;
        public string GeminiApiKey => geminiApiKey;
        public GeminiModelVariant SelectedGeminiModelVariant => geminiModelVariant;
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
    }
}
