using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AudioRecorder : MonoBehaviour
{
    private List<float> audioData = new List<float>();
    private bool isRecording = false;
    private const int targetSampleRate = 24000;

    public void StartRecording() => isRecording = true;

    public void StopRecording() => isRecording = false;

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (isRecording)
        {
            lock (audioData)
                audioData.AddRange(data);
        }
    }

    public byte[] GetAudioDataAsByteArray()
    {
        lock (audioData)
        {
            Debug.Log($"Audio data length: {audioData.Count}");
            var floatArray = audioData.ToArray();
            int unitySampleRate = AudioSettings.outputSampleRate;
            // if (unitySampleRate != targetSampleRate)
            // {
            //     floatArray = Resample(floatArray, unitySampleRate, targetSampleRate);
            // }
            var byteArray = new byte[floatArray.Length * sizeof(float)];
            System.Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
            audioData.Clear();
            return byteArray;
        }
    }

    private float[] Resample(float[] source, int sourceRate, int targetRate)
    {
        int targetLength = Mathf.RoundToInt((float)source.Length * targetRate / sourceRate);
        float[] resampled = new float[targetLength];
        float ratio = (float)source.Length / targetLength;
        for (int i = 0; i < targetLength; i++)
        {
            int srcIndex = Mathf.FloorToInt(i * ratio);
            resampled[i] = source[srcIndex];
        }
        return resampled;
    }
}
