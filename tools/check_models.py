import os
import sys

# Добавляем корень проекта в sys.path
_PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from google import genai
from dotenv import load_dotenv

# .env лежит в server/
load_dotenv(os.path.join(_PROJECT_ROOT, "server", ".env"))

client = genai.Client(api_key=os.getenv("GOOGLE_API_KEY"))

print("Доступные тебе модели:")
for model in client.models.list():
    print(f"-> {model.name}")