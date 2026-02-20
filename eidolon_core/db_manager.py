"""
eidolon_core.db_manager
~~~~~~~~~~~~~~~~~~~~~~~
Database integration layer for EIDOLON — logs every NPC interaction
to a local MS SQL Server (SQLEXPRESS) via pyodbc.
"""

import logging
import os
from typing import Any

import pyodbc

logger = logging.getLogger(__name__)


class DatabaseManager:
    """Manages the connection to MS SQL Server and persists interaction logs."""

    def __init__(self) -> None:
        """
        Initialize the database manager.

        Connection details are read from environment variables:
            DB_SERVER  — SQL Server instance name  (e.g. "DESKTOP-XXX\\SQLEXPRESS")
            DB_NAME    — target database name       (e.g. "EIDOLON-community")
            DB_DRIVER  — ODBC driver string         (e.g. "{SQL Server}")

        Raises:
            RuntimeError: If any required variable is missing.
        """
        self.server = os.getenv("DB_SERVER")
        self.database = os.getenv("DB_NAME")
        self.driver = os.getenv("DB_DRIVER")

        missing = [
            name
            for name, val in [
                ("DB_SERVER", self.server),
                ("DB_NAME", self.database),
                ("DB_DRIVER", self.driver),
            ]
            if not val
        ]
        if missing:
            raise RuntimeError(
                f"Database configuration missing in .env: {', '.join(missing)}"
            )

        self._conn_str = (
            f"Driver={self.driver};"
            f"Server={self.server};"
            f"Database={self.database};"
            "Trusted_Connection=yes;"
        )
        logger.info("DatabaseManager ready (server=%s, db=%s).", self.server, self.database)

    # Public API

    def save_interaction(
        self,
        npc_name: str,
        player_input: str,
        response_data: dict[str, Any],
    ) -> None:
        """
        Write one interaction record to the InteractionLogs table.

        Args:
            npc_name:      The NPC's character name.
            player_input:  What the player said.
            response_data: Dictionary returned by EidolonBrain.process_interaction()
                           with keys 'response_text', 'emotional_state', 'visual_cue'.

        Note:
            All database errors are caught and logged so the main
            application never crashes due to a DB outage.
        """
        try:
            with pyodbc.connect(self._conn_str) as conn:
                cursor = conn.cursor()
                cursor.execute(
                    """
                    INSERT INTO InteractionLogs
                        (CharacterName, PlayerMessage, CharacterResponse,
                         Emotion, ActionCue)
                    VALUES (?, ?, ?, ?, ?)
                    """,
                    npc_name,
                    player_input,
                    response_data.get("response_text", ""),
                    response_data.get("emotional_state", ""),
                    response_data.get("visual_cue", ""),
                )
                conn.commit()
                logger.debug("Interaction logged for '%s'.", npc_name)

        except pyodbc.Error as exc:
            logger.error("DB write failed (pyodbc): %s", exc)
        except Exception as exc:  # noqa: BLE001
            logger.error("Unexpected error while logging interaction: %s", exc)

    def get_recent_history(
        self,
        npc_name: str,
        limit: int = 10,
    ) -> list[dict[str, str]]:
        """
        Retrieve the most recent interactions for a given NPC.

        Args:
            npc_name: The NPC's character name to filter by.
            limit:    Maximum number of interaction pairs to return.

        Returns:
            A list of dicts in chronological order (oldest first):
            [{"role": "user", "text": "..."}, {"role": "npc", "text": "..."}, ...]
            Returns an empty list if the table is empty or on any DB error.
        """
        try:
            with pyodbc.connect(self._conn_str) as conn:
                cursor = conn.cursor()
                cursor.execute(
                    """
                    SELECT TOP (?) PlayerMessage, CharacterResponse
                    FROM InteractionLogs
                    WHERE CharacterName = ?
                    ORDER BY Timestamp DESC
                    """,
                    limit,
                    npc_name,
                )
                rows = cursor.fetchall()

            # Build pairs (newest-first from the query) then reverse to chronological order.
            history: list[dict[str, str]] = []
            for row in rows:
                history.append({"role": "user", "text": row.PlayerMessage})
                history.append({"role": "npc", "text": row.CharacterResponse})

            history.reverse()
            logger.debug(
                "Retrieved %d history pairs for '%s'.", len(rows), npc_name,
            )
            return history

        except pyodbc.Error as exc:
            logger.error("DB read failed (pyodbc): %s", exc)
            return []
        except Exception as exc:  # noqa: BLE001
            logger.error("Unexpected error while reading history: %s", exc)
            return []

    def get_reputation(self, npc_name: str) -> int:
        """Return the current reputation score for the given NPC."""
        try:
            with pyodbc.connect(self._conn_str) as conn:
                cursor = conn.cursor()
                cursor.execute("SELECT ReputationScore FROM NPCRelationships WHERE NPCName = ?", npc_name)
                row = cursor.fetchone()
                return row.ReputationScore if row else 0
        except Exception:
            return 0

    def update_reputation(self, npc_name: str, change: int) -> None:
        """Apply a reputation delta to the given NPC (positive or negative)."""
        try:
            with pyodbc.connect(self._conn_str) as conn:
                cursor = conn.cursor()
                cursor.execute("""
                    UPDATE NPCRelationships
                    SET ReputationScore = ReputationScore + ?,
                        LastInteraction = GETDATE()
                    WHERE NPCName = ?""", change, npc_name)
                conn.commit()
        except Exception as e:
            logger.error("Failed to update reputation: %s", e)