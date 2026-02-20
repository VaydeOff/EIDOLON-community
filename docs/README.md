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
- **💾 SQL Persistence**: Full integration with MS SQL Server. NPCs now remember every interaction, building a continuous narrative across sessions.
- **🎭 Dynamic Reputation (Affinity)**: A built-in social capital system. Actions and dialogue choices directly influence NPC trust levels and future behavior.

---

## 🏗️ Project Structure

The repository is organized to ensure modularity and scalability across different platforms:

```
EIDOLON-community/
├── .venv/                   # Python virtual environment
├── eidolon_core/            # Core Python logic (The Brain)
│   ├── brain.py             # LLM orchestration and Gemini integration
│   ├── db_manager.py        # MS SQL Server persistence layer
│   ├── schemas.py           # Data structures and Pydantic models
│   └── utils.py             # Utility functions and logging
├── server/                  # FastAPI bridge server
│   ├── eidolon_server.py    # REST API entry point
│   └── .env                 # API keys and DB config (not tracked by git)
├── sdks/                    # Game engine connectors
│   ├── unity_csharp/        # C# SDK for Unity (EidolonBridge, SimpleChatUI)
│   └── web_js/              # JavaScript SDK for Web/Next.js
├── tools/                   # Developer utilities
│   └── check_models.py      # List available Gemini models
├── examples/                # Quick-start demos
│   └── terminal_demo.py     # Interactive CLI NPC demo
├── docs/                    # Documentation and assets
├── .env.example             # Template for API configuration
└── requirements.txt         # Python dependencies
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

### 2. Virtual Environment

Create and activate a virtual environment before installing:

```bash
python -m venv .venv
# Windows
.venv\Scripts\Activate.ps1
# macOS / Linux
source .venv/bin/activate

pip install -r requirements.txt
```

### 3. Configure the Environment

Copy `.env.example` into `server/` and fill in your keys:

```bash
cp .env.example server/.env
```

```env
GOOGLE_API_KEY=your_key
DB_SERVER=your_sql_instance       # e.g., localhost\SQLEXPRESS
DB_NAME=EIDOLON-community
DB_DRIVER={SQL Server}
```

### 4. Run the FastAPI Bridge Server

The server must be launched from the **project root**:

```bash
python server/eidolon_server.py
```

The API will be live at `http://127.0.0.1:8000`. You can also run the CLI demo:

```bash
python examples/terminal_demo.py
```

---

## 🗺️ Roadmap

EIDOLON follows an **Open Core** strategy. While the Community Version provides the "Brain," the upcoming Pro Version will introduce advanced capabilities:

- [x] **Stage 1 — Core Python Brain**: Engine-agnostic library for AI-NPC communication.
- [x] **Stage 1 — Long-term Memory (SQL)**: Persistence for interaction history and reputation across sessions.
- [x] **Stage 2 — Unity SDK (C#) Beta**: Native FastAPI bridge with UI controller, dynamic reputation system, and emotional state output (`EidolonBridge`, `SimpleChatUI`).
- [🚧] **Stage 3 — The Avatar**: Integration of 3D animations and visual effects driven by NPC emotional states and action descriptors.
- [ ] **Vector Memory (RAG)**: Advanced semantic memory for Pro Version.
- [ ] **Visual Personality Designer**: A dashboard for creating and managing NPC personalities.
- [ ] **Voice & Real-time Interaction**: WebSocket support for low-latency voice control.

---

## 🎬 Visual Demo

![EIDOLON Unity Prototype](images/unity_prototype.png)

*Pilgrim's Interface: Gorm reacts to gold — reputation (22) highlighted in green, visual action descriptors are being streamed in real time.*

---

## 🛡️ Team

- **Director / Prompt-Orchestrator**: VAYDE ([@VaydeOff](https://github.com/VaydeOff))
- **Strategy & Architecture**: VAYDE ([@VaydeOff](https://github.com/VaydeOff))
- **Implementation**: VAYDE ([@VaydeOff](https://github.com/VaydeOff))

---

## 📜 License

This project is licensed under the **MIT License**. We believe in open collaboration to push the boundaries of digital consciousness.