using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EpicRapBattle.Config;
using EpicRapBattle.Managers;
using Newtonsoft.Json;
using Unity.WebRTC;
using UnityEngine;
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
        string jsonBody = JsonConvert.SerializeObject(sessionRequest);

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
        OpenAISessionResponse sessionResponse =
            JsonConvert.DeserializeObject<OpenAISessionResponse>(json);
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
        dataChannel.OnOpen = () =>
        {
            Debug.Log("DataChannel opened successfully.");
        };
        dataChannel.OnClose = () =>
        {
            Debug.LogWarning("DataChannel closed.");
        };
        dataChannel.OnError = error =>
        {
            Debug.LogError($"DataChannel error: {error}");
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

    private bool isRecording = false;
    private List<float> audioBuffer = new List<float>();
    private int lastMicrophonePosition = 0;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            StartRecording();
        }

        if (Input.GetKeyUp(KeyCode.T))
        {
            StopAndCommitRecording();
        }

        // Capture audio data while recording
        if (isRecording && micAudioSource != null && micAudioSource.clip != null)
        {
            CaptureAudioData();
        }
    }

    private void CaptureAudioData()
    {
        int currentPosition = Microphone.GetPosition(null);
        if (currentPosition < 0)
            return; // Microphone not available

        AudioClip clip = micAudioSource.clip;
        if (clip == null)
            return;

        // Handle wrap-around
        int samplesToRead;
        if (currentPosition >= lastMicrophonePosition)
        {
            samplesToRead = currentPosition - lastMicrophonePosition;
        }
        else
        {
            // Handle buffer wrap-around
            samplesToRead = (clip.samples - lastMicrophonePosition) + currentPosition;
        }

        if (samplesToRead > 0)
        {
            float[] samples = new float[samplesToRead * clip.channels];

            if (currentPosition >= lastMicrophonePosition)
            {
                // Simple case - no wrap around
                clip.GetData(samples, lastMicrophonePosition);
            }
            else
            {
                // Handle wrap around
                int samplesBeforeWrap = (clip.samples - lastMicrophonePosition) * clip.channels;
                int samplesAfterWrap = currentPosition * clip.channels;

                float[] beforeWrap = new float[samplesBeforeWrap];
                float[] afterWrap = new float[samplesAfterWrap];

                clip.GetData(beforeWrap, lastMicrophonePosition);
                clip.GetData(afterWrap, 0);

                Array.Copy(beforeWrap, 0, samples, 0, samplesBeforeWrap);
                Array.Copy(afterWrap, 0, samples, samplesBeforeWrap, samplesAfterWrap);
            }

            // Add samples to buffer
            audioBuffer.AddRange(samples);
            lastMicrophonePosition = currentPosition;
        }
    }

    private void StartRecording()
    {
        if (micAudioSource == null || !micAudioSource.isPlaying)
        {
            Debug.LogError("Microphone is not initialized or not playing.");
            return;
        }

        isRecording = true;
        audioBuffer.Clear();
        lastMicrophonePosition = Microphone.GetPosition(null);
        Debug.Log("Started recording audio.");
    }

    private void StopAndCommitRecording()
    {
        if (!isRecording)
        {
            Debug.LogWarning("Recording was not started.");
            return;
        }

        isRecording = false;

        // Check if we have any audio data
        if (audioBuffer.Count == 0)
        {
            Debug.LogWarning("No audio data recorded.");
            return;
        }

        // Convert audio data to Base64 and send input_audio_buffer.append event
        byte[] audioBytes = ConvertFloatArrayToByteArray(audioBuffer.ToArray());
        string base64Audio = Convert.ToBase64String(audioBytes);
        bool appendSuccess = SafeSendDataChannelMessage(
            Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(
                    new
                    {
                        event_id = Guid.NewGuid().ToString(),
                        type = "input_audio_buffer.append",
                        audio = base64Audio,
                    }
                )
            ),
            "input_audio_buffer.append"
        );

        if (!appendSuccess)
        {
            Debug.LogError("Failed to send audio buffer append event. Aborting recording commit.");
            return;
        }

        // Commit the audio buffer
        bool commitSuccess = SafeSendDataChannelMessage(
            Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(
                    new { event_id = Guid.NewGuid().ToString(), type = "input_audio_buffer.commit" }
                )
            ),
            "input_audio_buffer.commit"
        );

        if (!commitSuccess)
        {
            Debug.LogError("Failed to send audio buffer commit event.");
            return;
        }

        // Send response.create event
        bool responseSuccess = SafeSendDataChannelMessage(
            Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(
                    new { event_id = Guid.NewGuid().ToString(), type = "response.create" }
                )
            ),
            "response.create"
        );

        if (responseSuccess)
        {
            Debug.Log(
                "Successfully stopped recording, committed audio buffer, and requested response."
            );
        }
        else
        {
            Debug.LogError("Failed to send response create event.");
        }
    }

    private void SendAudioBufferAppendEvent(string base64Audio)
    {
        var appendEvent = new
        {
            event_id = Guid.NewGuid().ToString(),
            type = "input_audio_buffer.append",
            audio = base64Audio,
        };

        string json = JsonConvert.SerializeObject(appendEvent);
        SafeSendDataChannelMessage(Encoding.UTF8.GetBytes(json), "input_audio_buffer.append");
    }

    /// <summary>
    /// Safely sends data through the DataChannel with proper state validation
    /// </summary>
    /// <param name="data">The data to send</param>
    /// <param name="eventType">The type of event being sent (for logging)</param>
    /// <returns>True if the data was sent successfully, false otherwise</returns>
    private bool SafeSendDataChannelMessage(byte[] data, string eventType)
    {
        if (dataChannel == null)
        {
            Debug.LogWarning($"DataChannel is null. Cannot send {eventType} event.");
            return false;
        }

        if (dataChannel.ReadyState != RTCDataChannelState.Open)
        {
            Debug.LogWarning(
                $"DataChannel is not open (State: {dataChannel.ReadyState}). Cannot send {eventType} event."
            );
            return false;
        }

        try
        {
            dataChannel.Send(data);
            Debug.Log($"Sent {eventType} event successfully.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send {eventType} event: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Converts float audio samples to byte array (16-bit PCM format)
    /// </summary>
    /// <param name="samples">Float audio samples (range -1.0 to 1.0)</param>
    /// <returns>Byte array representing 16-bit PCM audio data</returns>
    private byte[] ConvertFloatArrayToByteArray(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * sizeof(short)];
        int byteIndex = 0;

        for (int i = 0; i < samples.Length; i++)
        {
            // Clamp the float value to [-1.0, 1.0] and convert to 16-bit signed integer
            float clampedSample = Mathf.Clamp(samples[i], -1.0f, 1.0f);
            short sample16Bit = (short)(clampedSample * short.MaxValue);

            // Convert to little-endian byte array
            bytes[byteIndex++] = (byte)(sample16Bit & 0xFF);
            bytes[byteIndex++] = (byte)((sample16Bit >> 8) & 0xFF);
        }

        return bytes;
    }

    [Serializable]
    public class TurnDetectionConfig
    {
        public string type = "semantic_vad"; // "server_vad" or "semantic_vad"

        public bool? create_response = true;

        public string eagerness = "auto"; // Used only for semantic_vad: "low", "medium", "high", "auto"
        public bool? interrupt_response = false;

        // public int? prefix_padding_ms = 300; // Used only for server_vad
        // public int? silence_duration_ms = 1000; // Used only for server_vad
        // public float? threshold = 0.8f; // Used only for server_vad

        public TurnDetectionConfig() { }
    }

    [Serializable]
    public class OpenAISessionRequest
    {
        public string model;
        public string[] modalities = new[] { "audio", "text" };
        public string instructions;
        public string voice;

        [SerializeField]
        public TurnDetectionConfig turn_detection = null;

        public OpenAISessionRequest(string model, string instructions, string voice = "alloy")
        {
            this.model = model;
            this.instructions = instructions;
            this.voice = voice;
        }
    }
}
