"""Install a local OpenWebUI filter and scope it to one Workspace model."""

from __future__ import annotations

import argparse
import json
import sqlite3
import time
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--database", required=True, type=Path)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--model-id", required=True)
    parser.add_argument("--filter-id", required=True)
    args = parser.parse_args()

    source = args.source.read_text(encoding="utf-8")
    compile(source, str(args.source), "exec")

    timestamp = int(time.time())
    backup_path = args.database.with_name(
        f"{args.database.name}.before-{args.filter_id}-{timestamp}.bak"
    )

    with sqlite3.connect(args.database) as database:
        with sqlite3.connect(backup_path) as backup:
            database.backup(backup)

        database.row_factory = sqlite3.Row
        admin = database.execute(
            "SELECT id FROM user WHERE role = 'admin' ORDER BY created_at LIMIT 1"
        ).fetchone()
        model = database.execute(
            "SELECT meta FROM model WHERE id = ?",
            (args.model_id,),
        ).fetchone()

        if admin is None:
            raise RuntimeError("No OpenWebUI administrator exists")
        if model is None:
            raise RuntimeError(f"Workspace model not found: {args.model_id}")

        metadata = json.loads(model["meta"] or "{}")
        filter_ids = list(metadata.get("filterIds") or [])
        if args.filter_id not in filter_ids:
            filter_ids.append(args.filter_id)
        metadata["filterIds"] = filter_ids

        manifest = {
            "description": "Displays completion-token throughput for one model response.",
            "manifest": {
                "title": "Gemma Token Speed",
                "author": "IFM",
                "version": "1.0.0",
            },
        }

        database.execute(
            """
            INSERT INTO function (
                id, user_id, name, type, content, meta, valves,
                is_active, is_global, updated_at, created_at
            ) VALUES (?, ?, ?, 'filter', ?, ?, NULL, 1, 0, ?, ?)
            ON CONFLICT(id) DO UPDATE SET
                user_id = excluded.user_id,
                name = excluded.name,
                type = excluded.type,
                content = excluded.content,
                meta = excluded.meta,
                valves = NULL,
                is_active = 1,
                is_global = 0,
                updated_at = excluded.updated_at
            """,
            (
                args.filter_id,
                admin["id"],
                "Gemma Token Speed",
                source,
                json.dumps(manifest),
                timestamp,
                timestamp,
            ),
        )
        database.execute(
            "UPDATE model SET meta = ?, updated_at = ? WHERE id = ?",
            (json.dumps(metadata), timestamp, args.model_id),
        )
        database.commit()

        installed = database.execute(
            """
            SELECT id, type, is_active, is_global
            FROM function
            WHERE id = ?
            """,
            (args.filter_id,),
        ).fetchone()

    print(f"Backup: {backup_path}")
    print(f"Function: {dict(installed)}")
    print(f"Model: {args.model_id}")
    print(f"Filter IDs: {filter_ids}")


if __name__ == "__main__":
    main()
