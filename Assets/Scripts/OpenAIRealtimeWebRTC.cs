using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using EpicRapBattle.Config;
using EpicRapBattle.Managers;
using Unity.VisualScripting;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

public class OpenAIRealtimeWebRTC : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource remoteAudioSource;
    private RTCPeerConnection peerConnection;
    private RTCDataChannel dataChannel;
    private AudioStreamTrack localAudioTrack;
    private MediaStream remoteStream;
    private const string baseUrl = "https://api.openai.com/v1/realtime";
    private const string sessionUrl = "https://api.openai.com/v1/realtime/sessions";
    private string ephemeralKey;
    private AudioSource micAudioSource;

    [Header("OpenAI API Key (from AIConfig)")]
    [Tooltip("Reference to the AIConfig component holding the OpenAI API key.")]
    [SerializeField]
    private AIConfig aiConfig;

    [Tooltip("Reference to the UIManager for updating UI elements.")]
    [SerializeField]
    private UIManager uiManager;

    [Header("Transcription Events")]
    public UnityEvent<string> OnInputTranscription = new UnityEvent<string>();
    public UnityEvent<string> OnOutputTranscription = new UnityEvent<string>();

    private void Start()
    {
        if (aiConfig == null)
        {
            Debug.LogError("AIConfig is not assigned. Please assign it in the inspector.");
            return;
        }
        uiManager.clearText();
        uiManager.UpdateStatus("Initializing OpenAI Realtime WebRTC...");
        StartCoroutine(InitCoroutine());
    }

    private IEnumerator InitCoroutine()
    {
        yield return CreateOpenAISession();
        yield return InitWebRTC();
        uiManager.UpdateStatus(
            "OpenAI Realtime WebRTC initialized successfully. Start The Rap Battle!"
        );
    }

    private IEnumerator CreateOpenAISession()
    {
        var sessionRequest = new OpenAISessionRequest(
            aiConfig.GptModelString,
            aiConfig.RapPersonality
        );
        string jsonBody = JsonUtility.ToJson(sessionRequest);

        UnityWebRequest req = new UnityWebRequest(sessionUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", $"Bearer {aiConfig.ApiKey}");
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(
                "Failed to create OpenAI session: " + req.error + " " + req.downloadHandler.text
            );
            yield break;
        }

        string json = req.downloadHandler.text;
        OpenAISessionResponse sessionResponse = JsonUtility.FromJson<OpenAISessionResponse>(json);
        if (
            sessionResponse == null
            || sessionResponse.client_secret == null
            || string.IsNullOrEmpty(sessionResponse.client_secret.value)
        )
        {
            Debug.LogError("Failed to parse ephemeral key from OpenAI session response: " + json);
            yield break;
        }
        ephemeralKey = sessionResponse.client_secret.value;
        Debug.Log("Obtained ephemeral key: " + ephemeralKey);
    }

    private IEnumerator InitWebRTC()
    {
        var config = GetSelectedSdpSemantics();
        peerConnection = new RTCPeerConnection(ref config);

        remoteStream = new MediaStream();

        yield return StartCoroutine(InitializeMicrophone());

        StartSoundStream();

        dataChannel = peerConnection.CreateDataChannel("oai-events");
        dataChannel.OnMessage = bytes =>
        {
            string message = Encoding.UTF8.GetString(bytes);
            OAIEventController.HandleOAIEvent(message);
        };

        var offerOp = peerConnection.CreateOffer();
        yield return offerOp;
        if (offerOp.IsError)
        {
            Debug.LogError("CreateOffer error: " + offerOp.Error.message);
            yield break;
        }
        var offerDesc = offerOp.Desc;
        var setLocalOp = peerConnection.SetLocalDescription(ref offerDesc);
        yield return setLocalOp;
        if (setLocalOp.IsError)
        {
            Debug.LogError("SetLocalDescription error: " + setLocalOp.Error.message);
            yield break;
        }

        yield return SendOfferAndSetAnswer(offerDesc.sdp);
    }

    private void StartSoundStream()
    {
        peerConnection.OnTrack = e =>
        {
            if (e.Track is AudioStreamTrack audioTrack)
            {
                remoteStream.AddTrack(audioTrack);
                remoteAudioSource.SetTrack(audioTrack);
            }
        };
        localAudioTrack = new AudioStreamTrack(micAudioSource);
        peerConnection.AddTrack(localAudioTrack);
    }

    private IEnumerator InitializeMicrophone()
    {
        string micDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        if (micDevice == null)
        {
            Debug.LogError("No microphone found.");
            yield break;
        }
        AudioClip micClip = Microphone.Start(micDevice, true, 1, 48000);
        yield return new WaitForSeconds(0.1f);
        if (micAudioSource == null)
        {
            micAudioSource = gameObject.AddComponent<AudioSource>();
            micAudioSource.loop = true;
        }
        micAudioSource.clip = micClip;
        micAudioSource.Play();
    }

    private IEnumerator SendOfferAndSetAnswer(string offerSdp)
    {
        string url = $"{baseUrl}?model={aiConfig.GptModelString}";
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(offerSdp);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", $"Bearer {ephemeralKey}");
        req.SetRequestHeader("Content-Type", "application/sdp");

        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("SDP exchange failed: " + req.error + "\n" + req.downloadHandler.text);
            yield break;
        }

        string answerSdp = req.downloadHandler.text;
        var answerDesc = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = answerSdp };
        var setRemoteOp = peerConnection.SetRemoteDescription(ref answerDesc);
        yield return setRemoteOp;
        if (setRemoteOp.IsError)
        {
            Debug.LogError("SetRemoteDescription error: " + setRemoteOp.Error.message);
        }
    }

    private RTCConfiguration GetSelectedSdpSemantics()
    {
        return new RTCConfiguration
        {
            iceServers = new[]
            {
                new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } },
            },
        };
    }

    private void OnDestroy()
    {
        localAudioTrack?.Dispose();
        peerConnection?.Close();
        peerConnection?.Dispose();
    }
}

[Serializable]
public class OpenAISessionRequest
{
    public string model;
    public string[] modalities = new[] { "audio", "text" };
    public string instructions;

    // [Serializable]
    // public class InputAudioNoiseReductionConfig { }

    // public InputAudioNoiseReductionConfig input_audio_noise_reduction = null; // Optional, can be null
    public string voice;

    // [Serializable]
    // public class InputAudioTranscriptionConfig
    // {
    //     public string language;
    //     public string model;
    //     public string prompt;

    //     public InputAudioTranscriptionConfig(
    //         string language = null,
    //         string model = null,
    //         string prompt = null
    //     )
    //     {
    //         this.language = language ?? "en";
    //         this.model = model ?? "whisper-1";
    //         this.prompt = prompt;
    //     }
    // }

    // public InputAudioTranscriptionConfig input_audio_transcription = null; // Optional, can be null

    public OpenAISessionRequest(string model, string instructions, string voice = "alloy")
    {
        this.model = model;
        this.instructions = instructions;
        this.voice = voice;
        // this.input_audio_transcription = input_audio_transcription;
    }
}

[Serializable]
public class OpenAISessionResponse
{
    public ClientSecret client_secret;
}

[Serializable]
public class ClientSecret
{
    public string value;
    public long expires_at;
}

[Serializable]
public class BaseOAIEvent
{
    public string event_id;
    public string type;
}

[Serializable]
public class InputAudioBufferCommittedEvent : BaseOAIEvent
{
    public string item_id;
    public string previous_item_id;
}

[Serializable]
public class InputAudioBufferClearedEvent : BaseOAIEvent { }

[Serializable]
public class InputAudioBufferSpeechStartedEvent : BaseOAIEvent
{
    public int audio_start_ms;
    public string item_id;
}

[Serializable]
public class InputAudioBufferSpeechStoppedEvent : BaseOAIEvent
{
    public int audio_end_ms;
    public string item_id;
}

[Serializable]
public class ConversationItemCreatedEvent : BaseOAIEvent
{
    public string conversation_id;
    public string item_id;
    public string role;
    public string content;
    public long created_at;
}

[Serializable]
public class ResponseCreatedEvent : BaseOAIEvent
{
    public string response_id;
    public string conversation_id;
    public long created_at;
}

[Serializable]
public class ResponseOutputItemAddedEvent : BaseOAIEvent
{
    public string response_id;
    public string item_id;
    public string content_type;
    public string content;
}

[Serializable]
public class ResponseContentPartAddedEvent : BaseOAIEvent
{
    public string response_id;
    public string part_id;
    public string content;
}

[Serializable]
public class ResponseAudioTranscriptDeltaEvent : BaseOAIEvent
{
    public string response_id;
    public string transcript;
    public bool is_final;
    public string delta;
}

[Serializable]
public class OutputAudioBufferStartedEvent : BaseOAIEvent
{
    public string response_id;
    public long started_at;
}

[Serializable]
public class ResponseAudioDoneEvent : BaseOAIEvent
{
    public string response_id;
    public long finished_at;
}

[Serializable]
public class ResponseAudioTranscriptDoneEvent : BaseOAIEvent
{
    public string response_id;
    public string transcript;
}

[Serializable]
public class ResponseContentPartDoneEvent : BaseOAIEvent
{
    public string response_id;
    public string part_id;
}

[Serializable]
public class ResponseOutputItemDoneEvent : BaseOAIEvent
{
    public string response_id;
    public string item_id;
}

[Serializable]
public class ResponseDoneEvent : BaseOAIEvent
{
    public string response_id;
}

[Serializable]
public class RateLimitsUpdatedEvent : BaseOAIEvent
{
    public int requests_remaining;
    public int tokens_remaining;
    public long reset_at;
}
