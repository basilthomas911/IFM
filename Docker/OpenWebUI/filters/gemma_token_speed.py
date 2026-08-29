"""
title: Gemma Token Speed
author: IFM
version: 1.0.0
description: Shows persistent completion-token throughput for the model response.
requirements:
"""

import time


class Filter:
    """Measure a response and keep the generated footer out of later prompts."""

    FOOTER_MARKER = "\n\n---\n<!-- ifm-token-speed -->\n"

    def __init__(self):
        self._started_at = {}

    @staticmethod
    def _request_key(metadata):
        if not isinstance(metadata, dict):
            return None

        chat_id = metadata.get("chat_id")
        message_id = metadata.get("message_id")
        if not chat_id or not message_id:
            return None

        return f"{chat_id}:{message_id}"

    async def inlet(self, body: dict, __metadata__: dict | None = None) -> dict:
        for message in body.get("messages", []):
            if not isinstance(message, dict) or message.get("role") != "assistant":
                continue

            content = message.get("content")
            if isinstance(content, str) and self.FOOTER_MARKER in content:
                message["content"] = content.partition(self.FOOTER_MARKER)[0]

        request_key = self._request_key(__metadata__)
        if request_key:
            self._started_at[request_key] = time.monotonic()

        return body

    async def outlet(
        self,
        body: dict,
        __event_emitter__=None,
        __metadata__: dict | None = None,
    ) -> dict:
        request_key = self._request_key(__metadata__)
        started_at = self._started_at.pop(request_key, None) if request_key else None

        if started_at is None or __event_emitter__ is None:
            return body

        usage = None
        for message in reversed(body.get("messages", [])):
            if isinstance(message, dict) and message.get("role") == "assistant":
                usage = message.get("usage")
                break

        if not isinstance(usage, dict):
            return body

        output_tokens = usage.get("completion_tokens", usage.get("output_tokens"))
        if not isinstance(output_tokens, (int, float)) or output_tokens < 0:
            return body

        elapsed_seconds = max(time.monotonic() - started_at, 0.001)
        tokens_per_second = output_tokens / elapsed_seconds
        description = (
            f"Speed: {tokens_per_second:.1f} tok/s | "
            f"{int(output_tokens)} output tokens | {elapsed_seconds:.2f} s"
        )

        for message in reversed(body.get("messages", [])):
            if not isinstance(message, dict) or message.get("role") != "assistant":
                continue

            content = message.get("content")
            if isinstance(content, str):
                content = content.partition(self.FOOTER_MARKER)[0]
                message["content"] = (
                    f"{content}{self.FOOTER_MARKER}"
                    f"_Response speed: **{tokens_per_second:.1f} tok/s** | "
                    f"{int(output_tokens)} output tokens | {elapsed_seconds:.2f} s_"
                )
            break

        await __event_emitter__(
            {
                "type": "status",
                "data": {"description": description, "done": True},
            }
        )

        return body
