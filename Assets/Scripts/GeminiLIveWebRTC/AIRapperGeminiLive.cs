using System;
using UnityEngine;

public class AIRapperGeminiLive : GeminiLiveWebRTCAudio
{
    [SerializeField]
    private UIManager uIManager;

    protected override void OnTextResponseReceived(string text)
    {
        uIManager.AppendComputerText(text);
    }

    protected override void OnTranscriptionReceived(string text)
    {
        uIManager.AppendComputerText(text);
    }
}
