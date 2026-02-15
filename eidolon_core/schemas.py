"""
eidolon_core.schemas
~~~~~~~~~~~~~~~~~~~~
Pydantic models describing the data structures used across EIDOLON.
"""

from pydantic import BaseModel, Field
from typing import Optional


class NPCPersonality(BaseModel):
    """Defines who an NPC is — fed into the Brain as context."""
    name: str
    traits: list[str] = Field(default_factory=list)
    background: str = ""
    speech_style: str = ""


class NPCResponse(BaseModel):
    """Structured output returned by EidolonBrain.process_interaction."""
    response_text: str
    emotional_state: str = "neutral"
    visual_cue: str = "stands still"


class ConversationMessage(BaseModel):
    """A single message in the conversation history."""
    role: str  # "user" or "npc"
    text: str
