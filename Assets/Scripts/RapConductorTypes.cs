using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using EpicRapBattle.Config;
using UnityEngine;
using UnityEngine.Networking;

public enum BattleState
{
    WaitingStart,
    PlayerCountdown,
    PlayerTurn,
    WaitingTurn,
    NPCTurn,
    Rest,
}

// Gemini TTS request classes
[Serializable]
public class GeminiTtsRequest
{
    public Content[] contents;
    public GenerationConfig generationConfig;
    public string model;

    [Serializable]
    public class Content
    {
        public Part[] parts;
    }

    [Serializable]
    public class Part
    {
        public string text;
    }

    [Serializable]
    public class GenerationConfig
    {
        public string[] responseModalities;
        public SpeechConfig speechConfig;
    }

    [Serializable]
    public class SpeechConfig
    {
        public VoiceConfig voiceConfig;
    }

    [Serializable]
    public class VoiceConfig
    {
        public PrebuiltVoiceConfig prebuiltVoiceConfig;
    }

    [Serializable]
    public class PrebuiltVoiceConfig
    {
        public string voiceName;
    }
}

// Gemini TTS response classes
[Serializable]
public class GeminiTtsResponse
{
    public Candidate[] candidates;

    [Serializable]
    public class Candidate
    {
        public Content content;
    }

    [Serializable]
    public class Content
    {
        public Part[] parts;
    }

    [Serializable]
    public class Part
    {
        public InlineData inlineData;
    }

    [Serializable]
    public class InlineData
    {
        public string data;
    }
}
