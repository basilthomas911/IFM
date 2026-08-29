"""Print non-content diagnostics for recent OpenWebUI chat messages."""

from __future__ import annotations

import argparse
import json
import sqlite3


def message_summary(message: dict) -> dict:
    return {
        "id": message.get("id"),
        "role": message.get("role"),
        "model": message.get("model"),
        "usage": message.get("usage"),
        "statusHistory": message.get("statusHistory"),
        "status_history": message.get("status_history"),
        "done": message.get("done"),
        "timestamp": message.get("timestamp"),
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--database", required=True)
    parser.add_argument("--limit", type=int, default=5)
    args = parser.parse_args()

    with sqlite3.connect(args.database) as database:
        database.row_factory = sqlite3.Row
        chats = database.execute(
            "SELECT id, chat, updated_at FROM chat ORDER BY updated_at DESC LIMIT ?",
            (args.limit,),
        ).fetchall()

    for row in chats:
        payload = json.loads(row["chat"] or "{}")
        history = payload.get("history") or {}
        message_map = history.get("messages") or {}
        messages = [value for value in message_map.values() if isinstance(value, dict)]
        messages.sort(key=lambda value: value.get("timestamp") or 0)
        print(
            json.dumps(
                {
                    "chat_id": row["id"],
                    "updated_at": row["updated_at"],
                    "top_level_model": payload.get("model"),
                    "top_level_models": payload.get("models"),
                    "params": payload.get("params"),
                    "last_messages": [message_summary(value) for value in messages[-4:]],
                },
                indent=2,
                sort_keys=True,
            )
        )


if __name__ == "__main__":
    main()
