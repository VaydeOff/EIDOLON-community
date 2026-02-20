import os
import sys

# Добавляем корень проекта в sys.path, чтобы eidolon_core был доступен
# независимо от того, из какой папки запускается скрипт
_PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

import uvicorn
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from dotenv import load_dotenv
from eidolon_core.brain import EidolonBrain
from eidolon_core.db_manager import DatabaseManager

# Загружаем .env из папки server/ (рядом с этим файлом)
load_dotenv(os.path.join(os.path.dirname(__file__), ".env"))

app = FastAPI(title="EIDOLON Bridge Server")

# 2. Инициализируем компоненты
try:
    # База данных сама подтянет настройки из .env
    db = DatabaseManager() 
    
    # Мозгу нужен только API ключ, базу он найдет сам или через внутреннюю логику
    brain = EidolonBrain(api_key=os.getenv("GOOGLE_API_KEY"))
    
    print("[EIDOLON] Server components initialized. Gorm is ready.")
except Exception as e:
    print(f"[CRITICAL ERROR] Failed to start engine: {e}")
    exit(1)

class ChatRequest(BaseModel):
    user_id: str = "Piligrim"
    message: str

# --- NPC personality (same as in terminal_demo.py) ---
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
        # Подтягиваем историю и репутацию из БД
        context = db.get_recent_history(BLACKSMITH["name"])
        reputation = db.get_reputation(BLACKSMITH["name"])

        # brain.process_interaction(personality, context, user_input, reputation)
        result = brain.process_interaction(
            personality=BLACKSMITH,
            context=context,
            user_input=request.message,
            reputation=reputation,
        )

        # Сохраняем взаимодействие в БД
        db.save_interaction(BLACKSMITH["name"], request.message, result)

        # Обновляем репутацию если изменилась
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
