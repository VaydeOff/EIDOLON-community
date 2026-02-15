"""
Terminal demo for EIDOLON — interactive NPC conversation in the console.
Requires a valid GOOGLE_API_KEY in a .env file (or environment variable).
"""

import os
import sys

from dotenv import load_dotenv

# Ensure the project root is on the Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from eidolon_core.brain import EidolonBrain
from eidolon_core.utils import log_event


# --- Sample NPC personality ---
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


def main() -> None:
    load_dotenv()

    api_key = os.getenv("GOOGLE_API_KEY")
    if not api_key:
        print("[ERROR] GOOGLE_API_KEY not found. Create a .env file with your key.")
        sys.exit(1)

    brain = EidolonBrain(api_key=api_key)
    log_event("EIDOLON Terminal Demo started.")
    print(f"\nYou approach {BLACKSMITH['name']}. Type 'quit' to leave.\n")

    context: list[dict[str, str]] = []

    while True:
        user_input = input("You: ").strip()
        if not user_input or user_input.lower() in ("quit", "exit"):
            print("\n*You walk away.*")
            break

        result = brain.process_interaction(
            personality=BLACKSMITH,
            context=context,
            user_input=user_input,
        )

        # Display the structured response
        print(f"\n  [{result['emotional_state'].upper()}] {BLACKSMITH['name']}:")
        print(f"  \"{result['response_text']}\"")
        print(f"  *{result['visual_cue']}*\n")

        # Update conversation history
        context.append({"role": "user", "text": user_input})
        context.append({"role": "npc", "text": result["response_text"]})

    log_event("Demo finished.")


if __name__ == "__main__":
    main()
