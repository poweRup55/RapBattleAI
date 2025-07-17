using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MatchJudge : MonoBehaviour
{
    private List<byte[]> MatchRecording = new List<byte[]>();
    private const int sampleRate = 24000;

    void Start() { }

    void Update() { }

    public void RecordPlayerInput(byte[] playerInputAudio)
    {
        MatchRecording.Add(WavUtility.RemoveHeaderFromWavByteArray(playerInputAudio));
        Add64ByteSineWaveToRecording(110f, 0.5f, 0.5f); // Low-pitched sine wave at 110Hz for 1 second
    }

    public void RecordNPCInput(byte[] npcInputAudio)
    {
        MatchRecording.Add(WavUtility.RemoveHeaderFromWavByteArray(npcInputAudio));
        Add64ByteSineWaveToRecording(880f, 0.5f, 0.5f); // High-pitched sine wave at 880Hz for 1 second
    }

    private byte[] GenerateSineWave(float frequency, float durationSeconds, float amplitude = 0.5f)
    {
        int samplesCount = (int)(durationSeconds * sampleRate);
        byte[] sineWaveData = new byte[samplesCount * 2];

        for (int i = 0; i < samplesCount; i++)
        {
            float time = (float)i / sampleRate;
            float sineValue = amplitude * Mathf.Sin(2 * Mathf.PI * frequency * time);

            short sample = (short)(sineValue * 32767);

            byte[] sampleBytes = BitConverter.GetBytes(sample);
            sineWaveData[i * 2] = sampleBytes[0];
            sineWaveData[i * 2 + 1] = sampleBytes[1];
        }

        return sineWaveData;
    }

    private void Add64ByteSineWaveToRecording(
        float frequency,
        float durationSeconds = 1,
        float amplitude = 0.5f
    )
    {
        var sineWaveData = GenerateSineWave(frequency, durationSeconds, amplitude);
        MatchRecording.Add(sineWaveData);
        Debug.Log($"64-byte sine wave ({frequency}Hz) added to match recording");
    }

    public List<byte[]> GetMatchRecording()
    {
        return MatchRecording;
    }

    public void ClearMatchRecording()
    {
        MatchRecording.Clear();
        Debug.Log("Match recording cleared");
    }

    public void SaveMatchRecording(string filePath)
    {
        try
        {
            int totalDataLength = 0;
            foreach (var audioData in MatchRecording)
            {
                totalDataLength += audioData.Length;
            }

            byte[] combinedAudioData = new byte[totalDataLength];
            int offset = 0;
            foreach (var audioData in MatchRecording)
            {
                Buffer.BlockCopy(audioData, 0, combinedAudioData, offset, audioData.Length);
                offset += audioData.Length;
            }

            byte[] wavData = WavUtility.AddHeaderToWavByteArray(combinedAudioData, sampleRate, 1);

            using (var fileStream = System.IO.File.Open(filePath, System.IO.FileMode.Create))
            {
                fileStream.Write(wavData, 0, wavData.Length);
            }
            Debug.Log($"Match recording saved to {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save match recording: {ex.Message}");
        }
    }
}
