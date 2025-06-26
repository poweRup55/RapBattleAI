using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EpicRapBattle.Config;
using UnityEngine;
using UnityEngine.Networking;

public class OpenAIService
{
    private AIConfig aiConfig;

    public OpenAIService(AIConfig config)
    {
        aiConfig = config;
    }

    private List<Message> messages = new List<Message>();

    public IEnumerator SendToOpenAI(
        Action<ChatCompletionResponse> onSuccess,
        Action<Exception> onError
    )
    {
        var payload = new OpenAIPayload
        {
            model = aiConfig.GptModelString,
            messages = messages,
            modalities = new[] { "text", "audio" },
            audio = new OpenAIAudio { voice = aiConfig.TtsVoiceString, format = "wav" },
        };
        string json = JsonUtility.ToJson(payload);
        json = System.Text.RegularExpressions.Regex.Replace(json, ",?\"\\w+\":\"\"", "");
        json = System.Text.RegularExpressions.Regex.Replace(json, ",(?=\\s*[}\\]])", "");

        using (
            UnityWebRequest req = new UnityWebRequest(
                "https://api.openai.com/v1/chat/completions",
                "POST"
            )
        )
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {aiConfig.ApiKey}");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"OpenAI API error: {req.error} \n{req.downloadHandler.text} ");
                onError?.Invoke(
                    new Exception($"OpenAI API error: {req.error} \n{req.downloadHandler.text}")
                );
                yield break;
            }
            ChatCompletionResponse response = JsonUtility.FromJson<ChatCompletionResponse>(
                req.downloadHandler.text
            );
            onSuccess?.Invoke(response);
        }
    }

    public void AddSystemMessage(string textMessage)
    {
        messages.Add(
            new Message
            {
                role = "system",
                content = new MessageContent[]
                {
                    new MessageContent { text = textMessage, type = "text" },
                },
            }
        );
    }

    public void AddUserAudioMessage(string base64Audio)
    {
        messages.Add(
            new Message
            {
                role = "user",
                content = new MessageContent[]
                {
                    new MessageContent
                    {
                        type = "input_audio",
                        input_audio = new InputAudio { data = base64Audio, format = "wav" },
                    },
                },
            }
        );
    }

    public void AddAssistantAudioMessage(string audioId)
    {
        messages.Add(
            new Message
            {
                role = "assistant",
                audio = new AssistantResponseAudio { id = audioId },
            }
        );
    }

    [Serializable]
    public class Message
    {
        public string role = null;
        public MessageContent[] content = null;
        public AssistantResponseAudio audio = null;
    }

    [Serializable]
    public class AssistantResponseAudio
    {
        public string id = null;
    }

    [Serializable]
    public class MessageContent
    {
        public string type = null;
        public string text = null;
        public InputAudio input_audio = null;
    }

    [Serializable]
    public class InputAudio
    {
        public string data = null;
        public string format = null;
    }

    [Serializable]
    public class ChatCompletionResponse
    {
        public string id = null;
        public List<ChatCompletionChoice> choices = null;
    }

    [Serializable]
    public class ChatCompletionChoice
    {
        public int index = 0;
        public ChatCompletionMessage message = null;
        public string finish_reason = null;
    }

    [Serializable]
    public class ChatCompletionMessage
    {
        public string role = null;
        public string refusal = null;
        public ChatCompletionAudio audio = null;
    }

    [Serializable]
    public class ChatCompletionAudio
    {
        public string data = null;
        public int expires_at = 0;
        public string id = null;
        public string transcript = null;
    }

    [Serializable]
    public class OpenAIPayload
    {
        public string model = null;
        public List<Message> messages = null;
        public string[] modalities = null;
        public OpenAIAudio audio = null;
    }

    [Serializable]
    public class OpenAIAudio
    {
        public string voice = null;
        public string format = null;
    }
}
