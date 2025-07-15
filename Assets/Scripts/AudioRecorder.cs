using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AudioRecorder : MonoBehaviour
{
    private List<float> currentAudioData = new List<float>();

    private List<float> totalAudioData = new List<float>();

    private bool isRecording = false;

    public void StartRecording()
    {
        isRecording = true;
    }

    public void StopRecording()
    {
        isRecording = false;
        CommitAudioData();
    }

    public void StopRecording(byte[] audioBytes)
    {
        isRecording = false;
        if (audioBytes == null || audioBytes.Length == 0)
            return;

        float[] samples = new float[audioBytes.Length / 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = System.BitConverter.ToInt16(audioBytes, i * 2);
            samples[i] = sample / 32768f; // Convert to float
        }
        lock (currentAudioData)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                if (i < currentAudioData.Count)
                    currentAudioData[i] += samples[i];
                else
                    currentAudioData.Add(samples[i]);
            }
        }
        CommitAudioData();
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (isRecording)
        {
            // Copy audio data
            lock (currentAudioData)
                currentAudioData.AddRange(data);
        }
    }

    private void CommitAudioData()
    {
        lock (currentAudioData)
        {
            totalAudioData.AddRange(currentAudioData);
            currentAudioData.Clear();
        }
    }

    // Call this when done to save WAV file
    public byte[] Save(int frequency, int channels, string path = "")
    {
        lock (totalAudioData)
        {
            Debug.Log("Started Saving recording");
            float[] samples = totalAudioData.ToArray();

            byte[] wavData = ConvertToWavByteArray(samples, frequency, channels);

            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path, wavData);
                Debug.Log("Saved recording to " + path);
            }

            totalAudioData.Clear();

            return wavData;
        }
    }

    private byte[] ConvertToWavByteArray(float[] samples, int frequency, int channels)
    {
        int sampleCount = samples.Length;
        int byteCount = sampleCount * 2; // 16-bit audio
        int headerSize = 44;
        byte[] wav = new byte[headerSize + byteCount];

        // RIFF header
        System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        System.BitConverter.GetBytes(headerSize + byteCount - 8).CopyTo(wav, 4);
        System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
        System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
        System.BitConverter.GetBytes(16).CopyTo(wav, 16); // Subchunk1Size for PCM
        System.BitConverter.GetBytes((short)1).CopyTo(wav, 20); // AudioFormat PCM
        System.BitConverter.GetBytes((short)channels).CopyTo(wav, 22);
        System.BitConverter.GetBytes(frequency).CopyTo(wav, 24);
        System.BitConverter.GetBytes(frequency * channels * 2).CopyTo(wav, 28); // ByteRate
        System.BitConverter.GetBytes((short)(channels * 2)).CopyTo(wav, 32); // BlockAlign
        System.BitConverter.GetBytes((short)16).CopyTo(wav, 34); // BitsPerSample
        System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
        System.BitConverter.GetBytes(byteCount).CopyTo(wav, 40);

        // Convert float samples to 16-bit PCM and write to wav
        int offset = headerSize;
        for (int i = 0; i < sampleCount; i++)
        {
            short intSample = (short)Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f);
            byte[] byteArr = System.BitConverter.GetBytes(intSample);
            wav[offset++] = byteArr[0];
            wav[offset++] = byteArr[1];
        }

        return wav;
    }
}
