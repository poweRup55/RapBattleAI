using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.iOS;
using UnityEngine.UI;

public class MicrophoneController : MonoBehaviour
{
    public TextMeshProUGUI micButtonText;

    private bool microphoneAuthorized = false;
    private int currentMicIndex = 0;

    public string GetMicrophoneDevice()
    {
        if (Microphone.devices.Length == 0)
        {
            return null;
        }
        return Microphone.devices[currentMicIndex];
    }

    void Start()
    {
        StartCoroutine(RequestMicrophoneAuthorization());
    }

    IEnumerator RequestMicrophoneAuthorization()
    {
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);

        if (Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            microphoneAuthorized = true;
            micButtonText.text = "Chosen Microphone: " + Microphone.devices[currentMicIndex];
        }
        else
        {
            micButtonText.text = "Microphone Access Denied. Press to Retry";
        }
    }

    public void MicButtonPressed()
    {
        if (microphoneAuthorized)
        {
            currentMicIndex = (currentMicIndex + 1) % Microphone.devices.Length;
            micButtonText.text = "Chosen Microphone: " + Microphone.devices[currentMicIndex];
        }
        else
        {
            StartCoroutine(RequestMicrophoneAuthorization());
        }
    }
}
