"""Verify the installed OpenWebUI model-scoped token-speed filter."""

from __future__ import annotations

import argparse
import asyncio
import json
import sqlite3

from open_webui.utils.plugin import load_function_module_by_id


async def verify(database_path: str, model_id: str, filter_id: str) -> None:
    with sqlite3.connect(database_path) as database:
        database.row_factory = sqlite3.Row
        function = database.execute(
            """
            SELECT id, type, is_active, is_global
            FROM function
            WHERE id = ?
            """,
            (filter_id,),
        ).fetchone()
        model = database.execute(
            "SELECT meta FROM model WHERE id = ?",
            (model_id,),
        ).fetchone()

    assert function is not None, "Filter function is missing"
    assert function["type"] == "filter", "Function has the wrong type"
    assert function["is_active"] == 1, "Filter is not active"
    assert function["is_global"] == 0, "Filter must not be global"
    assert model is not None, "Workspace model is missing"
    assert filter_id in json.loads(model["meta"] or "{}").get("filterIds", [])

    loaded_filter, function_type, frontmatter = await load_function_module_by_id(filter_id)
    assert function_type == "filter"
    assert frontmatter.get("requirements", "") == ""

    events = []

    async def emit(event):
        events.append(event)

    metadata = {"chat_id": "verification-chat", "message_id": "verification-message"}
    request = {"model": model_id, "messages": [{"role": "user", "content": "test"}]}
    await loaded_filter.inlet(request, __metadata__=metadata)
    await asyncio.sleep(0.02)
    response = {
        "model": model_id,
        "messages": [
            {
                "role": "assistant",
                "content": "test",
                "usage": {"prompt_tokens": 10, "completion_tokens": 20},
            }
        ],
    }
    returned = await loaded_filter.outlet(
        response,
        __event_emitter__=emit,
        __metadata__=metadata,
    )

    assert returned is response
    assert "<!-- ifm-token-speed -->" in response["messages"][0]["content"]
    assert "20 output tokens" in response["messages"][0]["content"]
    assert len(events) == 1, "Filter did not emit exactly one status event"
    assert events[0]["type"] == "status"
    assert events[0]["data"]["done"] is True
    assert "tok/s" in events[0]["data"]["description"]
    assert "20 output tokens" in events[0]["data"]["description"]

    next_request = {
        "model": model_id,
        "messages": [
            {
                "role": "assistant",
                "content": response["messages"][0]["content"],
            }
        ],
    }
    await loaded_filter.inlet(next_request, __metadata__=metadata)
    assert "<!-- ifm-token-speed -->" not in next_request["messages"][0]["content"]
    assert next_request["messages"][0]["content"] == "test"

    print(f"Function: {dict(function)}")
    print(f"Model: {model_id} -> {filter_id}")
    print(f"Event: {events[0]}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--database", required=True)
    parser.add_argument("--model-id", required=True)
    parser.add_argument("--filter-id", required=True)
    args = parser.parse_args()
    asyncio.run(verify(args.database, args.model_id, args.filter_id))


if __name__ == "__main__":
    main()
