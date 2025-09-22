using System.Linq;
using UnityEngine;

namespace EpicRapBattle.Config
{
    public class AILiveConfig : MonoBehaviour
    {
        public static readonly int inputSampleRate = 16000;

        public enum GeminiLiveModels
        {
            gemini_2_5_flash_preview_native_audio_dialog,
            gemini_2_5_flash_exp_native_audio_thinking_dialog,
            gemini_live_2_5_flash_preview,
            gemini_2_0_flash_live_001,
        };

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

        [Tooltip("Select the Gemini model variant (only those supporting audio input are listed)")]
        [SerializeField]
        private GeminiLiveModels geminiLiveModelVariant =
            GeminiLiveModels.gemini_live_2_5_flash_preview;

        [Tooltip("Select the Gemini TTS voice for speech synthesis")]
        [SerializeField]
        private GeminiTtsVoice geminiTtsVoice = GeminiTtsVoice.Achernar;

        [SerializeField]
        [TextArea(3, 10)]
        private string AiPromptOverride;
        private string[] AiPrompts;
        private int AiPromptIndex = 0;
        private string ephemeralKey;

        public string EphemeralKey => ephemeralKey;

        public string GenerateEphemeralKey()
        {
            using (var webClient = new System.Net.WebClient())
            {
                try
                {
                    string response = webClient.UploadString(
                        "https://rap-against-the-machine-token-gen-1099115138457.europe-west1.run.app/api/token",
                        "POST",
                        string.Empty
                    );
                    var json = JsonUtility.FromJson<TokenResponse>(response);
                    Debug.Log($"Received token: {json.token}");
                    ephemeralKey = json.token;
                    return ephemeralKey;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to get ephemeral key: {ex.Message}");
                    throw new System.Exception("Failed to get ephemeral key", ex);
                }
            }
        }

        [System.Serializable]
        private class TokenResponse
        {
            public bool success;
            public string token;
        }

        public string AIPrompt => AiPromptOverride ?? AiPrompts[AiPromptIndex];

        public string GeminiLiveModel
        {
            get
            {
                switch (geminiLiveModelVariant)
                {
                    case GeminiLiveModels.gemini_2_5_flash_preview_native_audio_dialog:
                        return "gemini-2.5-flash-preview-native-audio-dialog";
                    case GeminiLiveModels.gemini_2_5_flash_exp_native_audio_thinking_dialog:
                        return "gemini-2.5-flash-exp-native-audio-thinking-dialog";
                    case GeminiLiveModels.gemini_live_2_5_flash_preview:
                        return "gemini-live-2.5-flash-preview";
                    case GeminiLiveModels.gemini_2_0_flash_live_001:
                        return "gemini-2.0-flash-live-001";
                    default:
                        return "gemini-live-2.5-flash-preview";
                }
            }
        }

        public GeminiTtsVoice SelectedGeminiTtsVoice => geminiTtsVoice;

        void Start()
        {
            AiPrompts = Resources
                .LoadAll<TextAsset>("RapperAIInstructions")
                .Select(asset => asset.text)
                .ToArray();
        }

        public void RandomizePersonality()
        {
            AiPromptIndex = Random.Range(0, AiPrompts.Length);
        }
    }
}
