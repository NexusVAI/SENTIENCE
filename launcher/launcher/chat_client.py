"""Lightweight streaming chat client for OpenAI-compatible endpoints.

Used by the launcher's "操练场" (playground) view to talk to either:
- the local llama-server running on http://host:port/v1/chat/completions
- a cloud provider (DeepSeek, OpenAI, Qwen, ...) via cfg.cloud_endpoint

Pure stdlib (urllib + json), no extra deps.
"""
from __future__ import annotations

import json
import threading
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from typing import Callable, List, Optional


@dataclass
class ChatMessage:
    role: str          # "system" / "user" / "assistant"
    content: str


@dataclass
class ChatRequest:
    endpoint: str
    model: str
    messages: List[ChatMessage]
    api_key: str = ""
    temperature: float = 0.7
    top_p: float = 0.9
    max_tokens: int = 512
    stream: bool = True
    extra_headers: dict = field(default_factory=dict)
    timeout: float = 60.0


# ───────────────────────────────────────────────────────────────────────────


def stream_chat(
    req: ChatRequest,
    on_delta: Callable[[str], None],
    on_done: Callable[[str], None],
    on_error: Callable[[str], None],
    cancel_flag: Optional[threading.Event] = None,
) -> threading.Thread:
    """Fire-and-forget streaming chat. Returns the worker thread.

    Args:
        req: ChatRequest spec.
        on_delta: called with each token chunk (str). Called from worker thread.
        on_done: called with the full concatenated response when streaming ends.
        on_error: called with the error message on failure.
        cancel_flag: threading.Event; if set, the worker stops reading.
    """
    if cancel_flag is None:
        cancel_flag = threading.Event()

    def worker() -> None:
        full = []
        try:
            payload = {
                "model": req.model,
                "messages": [{"role": m.role, "content": m.content}
                             for m in req.messages],
                "temperature": req.temperature,
                "top_p": req.top_p,
                "max_tokens": req.max_tokens,
                "stream": bool(req.stream),
            }
            body = json.dumps(payload).encode("utf-8")
            headers = {
                "Content-Type": "application/json",
                "Accept": ("text/event-stream" if req.stream
                           else "application/json"),
            }
            if req.api_key:
                headers["Authorization"] = f"Bearer {req.api_key}"
            for k, v in req.extra_headers.items():
                headers[k] = v

            request = urllib.request.Request(
                req.endpoint, data=body, headers=headers, method="POST",
            )

            with urllib.request.urlopen(request, timeout=req.timeout) as resp:
                if not req.stream:
                    raw = resp.read().decode("utf-8", errors="replace")
                    try:
                        obj = json.loads(raw)
                        content = (
                            obj.get("choices", [{}])[0]
                            .get("message", {})
                            .get("content", "")
                        )
                    except Exception:
                        content = raw
                    if content:
                        on_delta(content)
                        full.append(content)
                    on_done("".join(full))
                    return

                # Streaming SSE
                for line in resp:
                    if cancel_flag.is_set():
                        break
                    if not line:
                        continue
                    s = line.decode("utf-8", errors="replace").strip()
                    if not s or not s.startswith("data:"):
                        continue
                    chunk = s[5:].strip()
                    if chunk == "[DONE]":
                        break
                    try:
                        obj = json.loads(chunk)
                    except Exception:
                        continue
                    choices = obj.get("choices") or []
                    if not choices:
                        continue
                    delta = choices[0].get("delta") or {}
                    piece = delta.get("content") or ""
                    if piece:
                        full.append(piece)
                        on_delta(piece)
            on_done("".join(full))
        except urllib.error.HTTPError as e:
            try:
                detail = e.read().decode("utf-8", errors="replace")[:400]
            except Exception:
                detail = ""
            on_error(f"HTTP {e.code} {e.reason}: {detail}")
        except urllib.error.URLError as e:
            on_error(f"Connection error: {e.reason}")
        except Exception as e:
            on_error(f"{type(e).__name__}: {e}")

    t = threading.Thread(target=worker, daemon=True)
    t.start()
    return t


# ───────────────────────────────────────────────────────────────────────────
# Default system prompt used by the playground when nothing is overridden.

DEFAULT_SYSTEM_PROMPT = (
    "你是 GTA5 洛圣都街头上的一个普通 NPC。请用一句简短的中文回应玩家，"
    "然后 pipe 分隔输出动作和情绪，例如：\n"
    "  你看什么看，滚开！|aim|angry\n"
    "可选动作：speak/idle/wave/flee/walk/attack/aim/cower/point/nod/"
    "shake_head/follow/call_cops/salute/dance/drink。\n"
    "可选情绪：neutral/happy/angry/sad/fear/surprise/disgust。"
)
