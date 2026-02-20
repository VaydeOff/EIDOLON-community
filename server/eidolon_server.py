import os
import sys

# Add project root to sys.path so that eidolon_core is importable
# regardless of the working directory the script is launched from.
_PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

import uvicorn
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from dotenv import load_dotenv
from eidolon_core.brain import EidolonBrain
from eidolon_core.db_manager import DatabaseManager

# Load .env from the server/ folder (next to this file)
load_dotenv(os.path.join(os.path.dirname(__file__), ".env"))

app = FastAPI(title="EIDOLON Bridge Server")

try:
    db = DatabaseManager()
    brain = EidolonBrain(api_key=os.getenv("GOOGLE_API_KEY"))
    print("[EIDOLON] Server components initialized. Gorm is ready.")
except Exception as e:
    print(f"[CRITICAL ERROR] Failed to start engine: {e}")
    exit(1)

class ChatRequest(BaseModel):
    user_id: str = "Piligrim"
    message: str

# NPC personality definition
BLACKSMITH = {
    "name": "Gorm the Blacksmith",
    "traits": ["gruff", "honest", "proud of his craft", "distrustful of strangers"],
    "background": (
        "Gorm has worked the forge in the village of Ashenvale for over 30 years. "
        "He lost his left eye in a bandit raid and trusts very few people. "
        "He secretly knows the location of an ancient dwarven anvil."
    ),
    "speech_style": (
        "Short, blunt sentences. Rarely uses fancy words. "
        "Occasionally swears under his breath. Refers to the player as 'stranger' "
        "until trust is earned."
    ),
}

@app.post("/chat")
async def chat_with_npc(request: ChatRequest):
    try:
        # Fetch conversation history and current reputation from the DB
        context = db.get_recent_history(BLACKSMITH["name"])
        reputation = db.get_reputation(BLACKSMITH["name"])

        result = brain.process_interaction(
            personality=BLACKSMITH,
            context=context,
            user_input=request.message,
            reputation=reputation,
        )

        # Persist the interaction
        db.save_interaction(BLACKSMITH["name"], request.message, result)

        # Update reputation if Gorm's affinity changed this turn
        affinity_change = result.get("affinity_change", 0)
        if affinity_change != 0:
            db.update_reputation(BLACKSMITH["name"], affinity_change)

        return {
            "response_text": result.get("response_text", ""),
            "emotional_state": result.get("emotional_state", "neutral"),
            "visual_cue": result.get("visual_cue", "stands still"),
            "reputation": db.get_reputation(BLACKSMITH["name"]),
        }
    except Exception as e:
        print(f"[SERVER ERROR] {e}")
        raise HTTPException(status_code=500, detail=str(e))

if __name__ == "__main__":
    print("[EIDOLON] Server running at http://127.0.0.1:8000")
    uvicorn.run(app, host="127.0.0.1", port=8000)
