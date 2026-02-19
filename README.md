# 👁️ EIDOLON

> **"Breathing life into virtual souls."**  
> Moving from scripted dialogues to emergent, sentient gameplay.

**EIDOLON** is a high-performance AI-NPC SDK designed to bridge the gap between Large Language Models and interactive game worlds. Our goal is to set the industrial standard for "living" intelligence, providing NPCs with memory, character, and individual will.

Developed by **VAYDE** ([@VaydeOff](https://github.com/VaydeOff)).

---

## ✨ Features (Community Version)

The **Community Version** provides the core architectural foundation for connecting your game engine to cutting-edge AI:

- **🧠 Personality Engine**: Define NPCs through traits, backgrounds, and speech styles rather than rigid scripts
- **🎭 Structured Emotional Output**: Every response includes emotional states and visual cues for seamless animation integration
- **🚀 LLM-Agnostic Core**: Built to leverage the power of Gemini 3 Pro, Gemini 2.5 Flash, and Claude 4.5
- **🔗 Flexible Integration**: Ready for Unity (C#) and Web (JS/Next.js) environments

---

## 🏗️ Project Structure

The repository is organized to ensure modularity and scalability across different platforms:

```
EIDOLON-community/
├── eidolon_core/            # Core Python logic (The Brain)
│   ├── brain.py             # LLM orchestration and logic
│   ├── schemas.py           # Data structures and Pydantic models
│   └── utils.py             # Utility functions and logging
├── examples/                # Quick-start implementation examples
│   └── terminal_demo.py     # Interactive CLI NPC demo
├── sdks/                    # Game engine connectors
│   ├── unity_csharp/        # C# SDK for Unity integration
│   └── web_js/              # JavaScript SDK for Web/Next.js
├── docs/                    # Technical documentation
├── .env.example             # Template for API configuration
├── requirements.txt         # Python dependencies
└── README.md                # Project overview
```

---

## 🚀 Getting Started

### 1. Installation

Clone the repository and install the required Python dependencies:

```bash
git clone https://github.com/VaydeOff/EIDOLON-community.git
cd EIDOLON-community
pip install -r requirements.txt
```

### 2. Configuration

Copy the example environment file and add your API key from [Google AI Studio](https://aistudio.google.com/):

```bash
cp .env.example .env
# Edit .env and add: GOOGLE_API_KEY=your_actual_key
```

### 3. Run the Demo

Engage with your first AI-powered NPC (Gorm the Blacksmith) directly in your terminal:

```bash
python examples/terminal_demo.py
```

---

## 🗺️ Roadmap

EIDOLON follows an **Open Core** strategy. While the Community Version provides the "Brain," the upcoming Pro Version will introduce advanced capabilities:

- [x] **Core Python Brain**: Base library for AI-NPC communication
- [x] **Unity SDK (C#)**: Native bridge for real-time Unity integration
- [ ] **Long-term Memory**: Vector database (RAG) implementation for persistent NPC memories
- [ ] **Visual Personality Designer**: A dashboard for creating and managing NPC personalities
- [ ] **Voice & Real-time Interaction**: WebSocket support for low-latency voice control

---

## 🛡️ Team

- **Director / Prompt-Orchestrator**: VAYDE ([@VaydeOff](https://github.com/VaydeOff))
- **Strategy & Architecture**: VAYDE ([@VaydeOff](https://github.com/VaydeOff))
- **Implementation**: VAYDE ([@VaydeOff](https://github.com/VaydeOff))

---

## 📜 License

This project is licensed under the **MIT License**. We believe in open collaboration to push the boundaries of digital consciousness.