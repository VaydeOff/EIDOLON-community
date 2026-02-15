"""
eidolon_core.brain
~~~~~~~~~~~~~~~~~~
Core logic ("The Brain") for EIDOLON — an AI-NPC SDK by VAYDE.
Uses the google-genai SDK with Gemini as the LLM backbone
to generate in-character NPC responses.
"""

import json
import logging
from typing import Any

from google import genai
from google.genai import types

logger = logging.getLogger(__name__)


class EidolonBrain:
    """Central reasoning engine that powers NPC interactions via Gemini."""

    # Default model — Gemini 2.0 Flash (fast & stable for production use)
    DEFAULT_MODEL = "models/gemini-2.5-flash"  

    def __init__(self, api_key: str, *, model: str | None = None) -> None:
        """
        Initialize the brain with a Google AI API key.

        Args:
            api_key: Google AI Studio API key.
            model: Optional model override (defaults to models/gemini-2.5-flash).
        """
        if not api_key:
            raise ValueError("A valid Google AI API key is required.")

        self._client = genai.Client(api_key=api_key)
        self._model_name: str = model or self.DEFAULT_MODEL
        logger.info("EidolonBrain initialized with model '%s'.", self._model_name)

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def process_interaction(
        self,
        personality: dict[str, Any],
        context: list[dict[str, str]],
        user_input: str,
    ) -> dict[str, str]:
        """
        Generate an in-character NPC response.

        Args:
            personality: NPC identity descriptor with keys:
                - name (str): Character name.
                - traits (list[str]): Personality traits.
                - background (str): Backstory summary.
                - speech_style (str): How the character speaks.
            context: Conversation history as a list of
                     {"role": "user"|"npc", "text": "..."} dicts.
            user_input: Latest message from the player.

        Returns:
            A dictionary with three keys:
                - response_text (str): What the NPC says.
                - emotional_state (str): Current emotion label.
                - visual_cue (str): Physical action description.
        """
        system_instruction = self._build_system_instruction(personality)
        contents = self._build_contents(context, user_input)

        try:
            response = self._client.models.generate_content(
                model=self._model_name,
                contents=contents,
                config=types.GenerateContentConfig(
                    system_instruction=system_instruction,
                ),
            )

            return self._parse_response(response.text)

        except genai.errors.ClientError as exc:
            logger.warning("Client error (blocked/invalid request): %s", exc)
            return self._fallback_response("blocked")

        except genai.errors.ServerError as exc:
            logger.error("Gemini server error: %s", exc, exc_info=True)
            return self._fallback_response("error")

        except Exception as exc:
            logger.error("Gemini API call failed: %s", exc, exc_info=True)
            return self._fallback_response("error")

    # ------------------------------------------------------------------
    # Internal helpers
    # ------------------------------------------------------------------

    @staticmethod
    def _build_system_instruction(personality: dict[str, Any]) -> str:
        """
        Construct the system-level prompt that locks the model into character.
        """
        name = personality.get("name", "Unknown")
        traits = ", ".join(personality.get("traits", []))
        background = personality.get("background", "")
        speech_style = personality.get("speech_style", "")

        return (
            f"You are {name}, a living character in a game world.\n"
            f"Personality traits: {traits}.\n"
            f"Background: {background}\n"
            f"Speech style: {speech_style}\n\n"
            "ABSOLUTE RULES — follow these without exception:\n"
            "1. You ARE this character. You are NOT an AI, language model, or assistant.\n"
            "2. NEVER break character. NEVER reveal you are artificial in any way.\n"
            "3. NEVER use phrases like 'As an AI…', 'I'm a language model…', or similar.\n"
            "4. Stay consistent with your background, traits, and speech style at all times.\n"
            "5. React emotionally as your character would — show suspicion, joy, anger, etc.\n\n"
            "RESPONSE FORMAT — you MUST reply with valid JSON and nothing else:\n"
            "{\n"
            '  "response_text": "<what you say in character>",\n'
            '  "emotional_state": "<one-word or short emotion label>",\n'
            '  "visual_cue": "<brief physical action description>"\n'
            "}\n"
        )

    @staticmethod
    def _build_contents(
        context: list[dict[str, str]], user_input: str
    ) -> list[types.Content]:
        """
        Convert the conversation history + new input into the google-genai
        Content format.
        """
        contents: list[types.Content] = []

        for msg in context:
            role = "user" if msg.get("role") == "user" else "model"
            contents.append(
                types.Content(role=role, parts=[types.Part(text=msg.get("text", ""))])
            )

        # Append the latest player message
        contents.append(
            types.Content(role="user", parts=[types.Part(text=user_input)])
        )

        return contents

    @staticmethod
    def _parse_response(raw_text: str) -> dict[str, str]:
        """
        Parse the model's raw text output into a structured dictionary.
        Handles cases where the model wraps JSON in markdown code fences.
        """
        cleaned = raw_text.strip()

        # Strip optional markdown code fences
        if cleaned.startswith("```"):
            # Remove opening fence (```json or ```)
            first_newline = cleaned.index("\n")
            cleaned = cleaned[first_newline + 1 :]
            # Remove closing fence
            if cleaned.endswith("```"):
                cleaned = cleaned[: -len("```")]
            cleaned = cleaned.strip()

        try:
            data = json.loads(cleaned)
        except json.JSONDecodeError:
            logger.warning("Failed to parse JSON from model output: %s", raw_text)
            return {
                "response_text": raw_text.strip(),
                "emotional_state": "neutral",
                "visual_cue": "stands still",
            }

        return {
            "response_text": data.get("response_text", ""),
            "emotional_state": data.get("emotional_state", "neutral"),
            "visual_cue": data.get("visual_cue", "stands still"),
        }

    @staticmethod
    def _fallback_response(reason: str) -> dict[str, str]:
        """
        Return a safe in-character fallback when the API call fails.
        """
        fallbacks = {
            "blocked": {
                "response_text": "*stares silently, unwilling to continue the conversation*",
                "emotional_state": "guarded",
                "visual_cue": "crosses arms and looks away",
            },
            "stopped": {
                "response_text": "*pauses mid-sentence, lost in thought*",
                "emotional_state": "distracted",
                "visual_cue": "gazes into the distance",
            },
            "error": {
                "response_text": "*seems momentarily dazed, then shakes it off*",
                "emotional_state": "confused",
                "visual_cue": "rubs temple and blinks",
            },
        }
        return fallbacks.get(reason, fallbacks["error"])
