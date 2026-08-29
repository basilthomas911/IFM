"""Enable usage and a model-scoped filter on an OpenWebUI upstream model."""

from __future__ import annotations

import argparse
import json
import sqlite3
import time
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--database", required=True, type=Path)
    parser.add_argument("--model-id", required=True)
    parser.add_argument("--filter-id", required=True)
    parser.add_argument("--name", required=True)
    args = parser.parse_args()

    timestamp = int(time.time())
    backup_path = args.database.with_name(
        f"{args.database.name}.before-{args.filter_id}-base-override-{timestamp}.bak"
    )

    with sqlite3.connect(args.database) as database:
        with sqlite3.connect(backup_path) as backup:
            database.backup(backup)

        database.row_factory = sqlite3.Row
        admin = database.execute(
            "SELECT id FROM user WHERE role = 'admin' ORDER BY created_at LIMIT 1"
        ).fetchone()
        function = database.execute(
            "SELECT id FROM function WHERE id = ? AND is_active = 1",
            (args.filter_id,),
        ).fetchone()

        if admin is None:
            raise RuntimeError("No OpenWebUI administrator exists")
        if function is None:
            raise RuntimeError(f"Active filter not found: {args.filter_id}")

        metadata = {
            "description": "Gemma 4 served locally by vLLM with usage metrics enabled.",
            "capabilities": {
                "usage": True,
                "status_updates": True,
                "citations": True,
            },
            "filterIds": [args.filter_id],
        }

        database.execute(
            """
            INSERT INTO model (
                id, user_id, base_model_id, name, params, meta,
                is_active, updated_at, created_at
            ) VALUES (?, ?, NULL, ?, ?, ?, 1, ?, ?)
            ON CONFLICT(id) DO UPDATE SET
                user_id = excluded.user_id,
                base_model_id = NULL,
                name = excluded.name,
                params = excluded.params,
                meta = excluded.meta,
                is_active = 1,
                updated_at = excluded.updated_at
            """,
            (
                args.model_id,
                admin["id"],
                args.name,
                json.dumps({}),
                json.dumps(metadata),
                timestamp,
                timestamp,
            ),
        )
        database.commit()

        installed = database.execute(
            """
            SELECT id, base_model_id, name, meta, is_active
            FROM model
            WHERE id = ?
            """,
            (args.model_id,),
        ).fetchone()

    print(f"Backup: {backup_path}")
    print(f"Model override: {dict(installed)}")


if __name__ == "__main__":
    main()
