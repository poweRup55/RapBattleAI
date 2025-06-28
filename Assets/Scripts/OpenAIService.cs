using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
        var model =
            aiConfig.SelectedProvider == AIConfig.Provider.Gemini
                ? aiConfig.GeminiModelVariantString
                : aiConfig.GptModelString;
        Debug.Log($"Sending request to {aiConfig.SelectedProvider} with model {model}");

        var payload = new OpenAIPayload
        {
            model = model,
            messages = messages,
            modalities = new[] { "text" },
        };
        string json = JsonUtility.ToJson(payload);
        json = System.Text.RegularExpressions.Regex.Replace(json, ",?\"\\w+\":\"\"", "");
        json = System.Text.RegularExpressions.Regex.Replace(json, ",(?=\\s*[}\\]])", "");

        string apiUrl =
            aiConfig.SelectedProvider == AIConfig.Provider.Gemini
                ? "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions"
                : "https://api.openai.com/v1/chat/completions";

        using (UnityWebRequest req = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {aiConfig.ApiKey}");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"{aiConfig.SelectedProvider} API error: {req.error} \n{req.downloadHandler.text} \nRequest: {json}"
                );
                onError?.Invoke(
                    new Exception(
                        $"{aiConfig.SelectedProvider} API error: {req.error} \n{req.downloadHandler.text}"
                    )
                );
                yield break;
            }
            ChatCompletionResponse response = JsonUtility.FromJson<ChatCompletionResponse>(
                req.downloadHandler.text
            );
            Debug.Log($"{aiConfig.SelectedProvider} API response: {req.downloadHandler.text}");
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

    public void ReplaceSystemMessage(string newSystemPrompt)
    {
        var systemMessage = messages.Find(m => m.role == "system");
        if (systemMessage != null)
        {
            systemMessage.content = new MessageContent[]
            {
                new MessageContent { text = newSystemPrompt, type = "text" },
            };
        }
        else
        {
            AddSystemMessage(newSystemPrompt);
        }
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

    public void AddAssistantTextMessage(string text)
    {
        messages.Add(
            new Message
            {
                role = "assistant",
                content = new MessageContent[]
                {
                    new MessageContent { text = text, type = "text" },
                },
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
        public string content = null;
    }

    [Serializable]
    public class OpenAIPayload
    {
        public string model = null;
        public List<Message> messages = null;
        public string[] modalities = null;
    }
}
