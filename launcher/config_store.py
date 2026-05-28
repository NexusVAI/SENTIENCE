"""Persistent JSON config for the launcher."""
from __future__ import annotations

import json
import os
from dataclasses import dataclass, asdict, field
from pathlib import Path
from typing import Optional


def _default_config_dir() -> Path:
    base = os.environ.get("APPDATA") or str(Path.home() / ".config")
    p = Path(base) / "SentienceV5-Anima"
    p.mkdir(parents=True, exist_ok=True)
    return p


CONFIG_PATH = _default_config_dir() / "launcher.json"


@dataclass
class LauncherConfig:
    # Engine + model
    engine_path: str = ""           # llama-server.exe path
    model_path: str = ""            # .gguf path

    # Server
    host: str = "127.0.0.1"
    port: int = 5001
    threads: int = 8
    threads_batch: int = 8
    context_size: int = 8192
    parallel_slots: int = 1
    use_mmap: bool = True
    use_mlock: bool = False
    cont_batching: bool = True
    flash_attn: bool = False
    n_gpu_layers: int = 0
    extra_args: str = ""

    # Inference defaults
    temperature: float = 0.7
    top_p: float = 0.9
    top_k: int = 40
    repeat_penalty: float = 1.1
    min_p: float = 0.05

    # UI
    theme_mode: str = "system"      # system / light / dark

    # Cloud / 3rd-party AI provider (used when provider == "cloud")
    # The launcher writes these into the GTA5 mod's config.ini so the
    # in-game NPCs talk to the cloud endpoint instead of the local
    # llama-server. Local mode (provider == "local") points the mod at
    # http://<host>:<port>/v1/chat/completions on this machine.
    provider: str = "local"          # "local" or "cloud"
    cloud_endpoint: str = "https://api.deepseek.com/v1/chat/completions"
    cloud_model: str = "deepseek-chat"
    cloud_api_key: str = ""

    @classmethod
    def load(cls) -> "LauncherConfig":
        if not CONFIG_PATH.exists():
            return cls()
        try:
            data = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
            cfg = cls()
            for k, v in data.items():
                if hasattr(cfg, k):
                    setattr(cfg, k, v)
            return cfg
        except Exception:
            return cls()

    def save(self) -> None:
        try:
            CONFIG_PATH.write_text(
                json.dumps(asdict(self), indent=2, ensure_ascii=False),
                encoding="utf-8",
            )
        except Exception:
            pass


# ───────────────────────────────────────────────────────────────────────────
# GTA5 mod config.ini writer
# ───────────────────────────────────────────────────────────────────────────

def mod_config_ini_path() -> Path:
    """Where the GTA5MOD2026 mod expects its config.ini."""
    docs = Path.home() / "Documents" / "GTA5MOD2026"
    return docs / "config.ini"


_LLM_KEYS = {
    "Provider", "LocalEndpoint", "LocalModel", "LightEndpoint", "LightModel",
    "CloudEndpoint", "CloudModel", "CloudAPIKey",
}


def write_mod_config(cfg: LauncherConfig) -> Optional[Path]:
    """Update the [LLM] section of the mod's config.ini in-place.

    Preserves all other sections/keys/comments. Creates the file with a
    sensible default scaffold if it doesn't exist yet.

    Returns the path written on success, None on failure.
    """
    target = mod_config_ini_path()
    try:
        target.parent.mkdir(parents=True, exist_ok=True)

        # Build the [LLM] key/value pairs we want.
        if cfg.provider == "cloud":
            local_ep = cfg.cloud_endpoint
            local_model = cfg.cloud_model
        else:
            local_ep = f"http://{cfg.host}:{cfg.port}/v1/chat/completions"
            local_model = "gta5_npc_v2_q4km"

        desired = {
            "Provider": cfg.provider,
            "LocalEndpoint": local_ep,
            "LocalModel": local_model,
            "LightEndpoint": local_ep,
            "LightModel": local_model,
            "CloudEndpoint": cfg.cloud_endpoint,
            "CloudModel": cfg.cloud_model,
            "CloudAPIKey": cfg.cloud_api_key,
        }

        if not target.exists():
            scaffold = _default_config_ini(desired)
            target.write_text(scaffold, encoding="utf-8")
            return target

        # In-place [LLM] section rewrite, preserving the rest.
        lines = target.read_text(encoding="utf-8").splitlines()
        out: list[str] = []
        in_llm = False
        seen: set[str] = set()
        for raw in lines:
            stripped = raw.strip()
            if stripped.startswith("[") and stripped.endswith("]"):
                # Closing previous [LLM] section: emit any keys we haven't seen.
                if in_llm:
                    for k in desired:
                        if k not in seen:
                            out.append(f"{k}={desired[k]}")
                in_llm = (stripped.lower() == "[llm]")
                seen = set()
                out.append(raw)
                continue
            if in_llm and "=" in raw and not stripped.startswith(("#", ";")):
                key = raw.split("=", 1)[0].strip()
                if key in desired:
                    out.append(f"{key}={desired[key]}")
                    seen.add(key)
                    continue
            out.append(raw)

        # If file ended while still inside [LLM], emit any missing keys.
        if in_llm:
            for k in desired:
                if k not in seen:
                    out.append(f"{k}={desired[k]}")

        # If [LLM] section never existed, append it.
        if not any(l.strip().lower() == "[llm]" for l in out):
            out.append("")
            out.append("[LLM]")
            for k, v in desired.items():
                out.append(f"{k}={v}")

        target.write_text("\n".join(out) + "\n", encoding="utf-8")
        return target
    except Exception:
        return None


def _default_config_ini(llm_desired: dict) -> str:
    """A minimal config.ini scaffold for first-time setup."""
    llm_block = "\n".join(f"{k}={v}" for k, v in llm_desired.items())
    return (
        "; GTA5MOD2026 config.ini (auto-generated by SentienceV5 launcher)\n"
        "\n"
        "[LLM]\n"
        f"{llm_block}\n"
        "\n"
        "[Performance]\n"
        "MaxTokens=120\n"
        "MaxTokensThinking=160\n"
        "Temperature=0.7\n"
        "MaxDialogueLength=45\n"
        "RequestCooldown=2.0\n"
        "StrictMode=True\n"
    )
