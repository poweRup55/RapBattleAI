using UnityEngine;

public class AudioClipResampler
{
    public static AudioClip ResampleAudio(AudioClip clip, int outputSampleRate)
    {
        if (clip.frequency == outputSampleRate)
        {
            Debug.Log("Audio clip is already at the desired sample rate.");
            return clip;
        }
        int inputSampleRate = clip.frequency;
        int inputChannels = clip.channels;
        int inputSamples = clip.samples;

        Debug.Log($"Resampling audio from {inputSampleRate}Hz to {outputSampleRate}Hz");
        float[] inputData = new float[inputSamples * inputChannels];
        clip.GetData(inputData, 0);

        int outputSamples = Mathf.RoundToInt(
            inputSamples * (float)outputSampleRate / inputSampleRate
        );
        float[] outputData = new float[outputSamples];

        float resampleRatio = (float)inputSamples / outputSamples;

        for (int i = 0; i < outputSamples; i++)
        {
            float srcIndex = i * resampleRatio;
            int indexFloor = Mathf.FloorToInt(srcIndex);
            int indexCeil = Mathf.Min(indexFloor + 1, inputSamples - 1);
            float t = srcIndex - indexFloor;

            float sampleSum = 0f;
            for (int channel = 0; channel < inputChannels; channel++)
            {
                float sample1 = inputData[indexFloor * inputChannels + channel];
                float sample2 = inputData[indexCeil * inputChannels + channel];
                float interpolatedSample = Mathf.Lerp(sample1, sample2, t);
                sampleSum += interpolatedSample;
            }

            outputData[i] = sampleSum / inputChannels;
        }

        var outputClip = AudioClip.Create(
            clip.name + "_Resampled",
            outputSamples,
            1,
            outputSampleRate,
            false
        );
        outputClip.SetData(outputData, 0);
        return outputClip;
    }
}
