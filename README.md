# RapBattleAI

A Unity-based rap battle game where players compete against AI opponents powered by Google's Gemini Live API with real-time audio processing.

## Features

- **Real-time AI Rap Battles**: Face off against AI rappers using Gemini Live's native audio capabilities
- **Voice-to-Voice Interaction**: Speak your verses and hear AI responses in real-time via WebSocket
- **Round-based Competition**: Multi-round battles with AI-powered judging
- **3D City Environment**: Battle in a stylized urban setting with character animations

## Technology Stack

- **Unity** (Universal Render Pipeline)
- **C# / .NET Framework**
- **Google Gemini Live API** (gemini-2.0-flash-live-001 and other variants)

## Getting Started

### Prerequisites

- Unity Editor (compatible with URP)
- Microphone access for voice input
- Internet connection for Gemini Live API

### Setup

1. Open the project in Unity
2. Load the `Main Game` scene from `Assets/Scenes/`
3. Press Play to start

### Configuration

AI models and voices can be configured via the `AILiveConfig` component:
- **Models**: gemini-2.0-flash-live-001, gemini-2.5-flash variants
- **TTS Voices**: 30+ voice options (Achernar, Charon, Puck, etc.)
- **Custom AI prompts** for personality customization


## License

All rights reserved.
