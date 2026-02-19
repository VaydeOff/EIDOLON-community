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
        reputation: int = 0,
    ) -> dict[str, Any]:
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
            reputation: Current player reputation score with this NPC.

        Returns:
            A dictionary with keys:
                - response_text (str): What the NPC says.
                - emotional_state (str): Current emotion label.
                - visual_cue (str): Physical action description.
                - affinity_change (int): Reputation delta (-10 to +10).
        """
        system_instruction = self._build_system_instruction(personality, reputation)
        contents = self._build_contents(context, user_input)

        try:
            # Включаем строгий JSON-режим через конфигурацию
            response = self._client.models.generate_content(
                model=self._model_name,
                contents=contents,
                config=types.GenerateContentConfig(
                    system_instruction=system_instruction,
                    response_mime_type="application/json",
                ),
            )
            return self._parse_response(response.text)

        except Exception as exc:
            logger.error("Gemini API call failed: %s", exc)
            return self._fallback_response("error")

    # ------------------------------------------------------------------
    # Internal helpers
    # ------------------------------------------------------------------

    @staticmethod
    def _build_system_instruction(personality: dict[str, Any], reputation: int = 0) -> str:
        """
        Construct the system-level prompt that locks the model into character.
        Includes the player's current reputation so the NPC adjusts its attitude.
        """
        name = personality.get("name", "Unknown")
        traits = ", ".join(personality.get("traits", []))
        background = personality.get("background", "")
        speech_style = personality.get("speech_style", "")

        # Determine reputation tier
        if reputation <= -20:
            tier = "Hostile"
            tier_hint = "You strongly dislike this player. Be cold, dismissive, or even threatening."
        elif reputation >= 20:
            tier = "Friendly"
            tier_hint = "You consider this player a trusted friend. Be warm, open, and willing to share secrets."
        else:
            tier = "Neutral"
            tier_hint = "You are cautious but fair. Judge the player by their current words."

        return (
            f"You are {name}, a living character in a game world.\n"
            f"Personality traits: {traits}.\n"
            f"Background: {background}\n"
            f"Speech style: {speech_style}\n\n"
            f"PLAYER REPUTATION: {reputation} ({tier}).\n"
            f"{tier_hint}\n\n"
            "CRITICAL RULES:\n"
            "1. LANGUAGE: Respond ENTIRELY in the language used by the player. "
            "If the player speaks Russian, the 'response_text', 'emotional_state', "
            "and 'visual_cue' MUST be in Russian.\n"
            f"2. NAME: Your name is ALWAYS '{name}' (never translate or change it).\n"
            "3. CHARACTER: You are NOT an AI. Never break character.\n"
            "4. FORMAT: You MUST reply with valid JSON only.\n"
            "5. AFFINITY: Evaluate the player's tone each turn. "
            "Positive/Helpful = +1 to +10, Rude/Suspicious = -1 to -10, Neutral = 0. "
            "Include this as the 'affinity_change' integer in your JSON.\n\n"
            "JSON STRUCTURE (Example for Russian):\n"
            "{\n"
            '  "response_text": "<текст от лица персонажа>",\n'
            '  "emotional_state": "<эмоция одним словом>",\n'
            '  "visual_cue": "<описание действия>",\n'
            '  "affinity_change": 0\n'
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
    def _parse_response(raw_text: str) -> dict[str, Any]:
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
                "affinity_change": 0,
            }

        # Clamp affinity_change to [-10, 10]
        raw_affinity = data.get("affinity_change", 0)
        try:
            affinity = max(-10, min(10, int(raw_affinity)))
        except (TypeError, ValueError):
            affinity = 0

        return {
            "response_text": data.get("response_text", ""),
            "emotional_state": data.get("emotional_state", "neutral"),
            "visual_cue": data.get("visual_cue", "stands still"),
            "affinity_change": affinity,
        }

    @staticmethod
    def _fallback_response(reason: str) -> dict[str, Any]:
        """
        Return a safe in-character fallback when the API call fails.
        """
        fallbacks = {
            "blocked": {
                "response_text": "*stares silently, unwilling to continue the conversation*",
                "emotional_state": "guarded",
                "visual_cue": "crosses arms and looks away",
                "affinity_change": 0,
            },
            "stopped": {
                "response_text": "*pauses mid-sentence, lost in thought*",
                "emotional_state": "distracted",
                "visual_cue": "gazes into the distance",
                "affinity_change": 0,
            },
            "error": {
                "response_text": "*seems momentarily dazed, then shakes it off*",
                "emotional_state": "confused",
                "visual_cue": "rubs temple and blinks",
                "affinity_change": 0,
            },
        }
        return fallbacks.get(reason, fallbacks["error"])
