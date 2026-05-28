"""Lifecycle manager for the bundled Edge-TTS voice server.

Spawns ``voice_server.py`` (the small HTTP wrapper around ``edge_tts`` that the
GTA5MOD2026 in-game mod talks to on port 5111) as a child process, captures its
stdout, and surfaces ready/error state to the UI thread via callbacks.

The launcher's main window owns one ``VoiceServerManager`` instance which is
started/stopped by the "启动语音 / 停止语音" buttons on the dashboard.
"""
from __future__ import annotations

import os
import shutil
import socket
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Callable, List, Optional


# Status callbacks: (level, message) and a coarse status string.
LogCallback = Callable[[str, str], None]
StatusCallback = Callable[[str], None]


def discover_voice_script(search_roots: List[Path]) -> Optional[Path]:
    """Find ``voice_server.py`` under the provided roots.

    Returns the first existing match. The launcher tries:
        <APP_ROOT>/voice_server.py
        <APP_ROOT>/launcher/voice_server.py
        <APP_ROOT>/../GTA5MOD2026/GTA5MOD2026/voice_server.py
    """
    for root in search_roots:
        try:
            if not root.exists():
                continue
            direct = root / "voice_server.py"
            if direct.is_file():
                return direct.resolve()
            for hit in root.rglob("voice_server.py"):
                # Guard against picking unrelated copies (e.g. in node_modules).
                if hit.is_file():
                    return hit.resolve()
        except Exception:
            continue
    return None


def _resolve_python_for_voice() -> Optional[str]:
    """Return a Python interpreter that can run ``voice_server.py``.

    The launcher itself is frozen (PyInstaller); we cannot reuse ``sys.executable``
    in that case because it would re-invoke the launcher binary. Try common
    candidates in order. The interpreter must have ``edge_tts`` installed.
    """
    if not getattr(sys, "frozen", False):
        # Dev mode: use whatever python is running us.
        return sys.executable

    # Frozen mode: look for a real Python on PATH or in well-known install dirs.
    candidates: List[str] = []
    for name in ("pythonw.exe", "python.exe"):
        which = shutil.which(name)
        if which:
            candidates.append(which)
    # Common Windows installer locations.
    home = Path.home()
    for guess in (
        home / "AppData/Local/Programs/Python/Python313/pythonw.exe",
        home / "AppData/Local/Programs/Python/Python312/pythonw.exe",
        home / "AppData/Local/Programs/Python/Python311/pythonw.exe",
        Path("C:/Python313/pythonw.exe"),
        Path("C:/Python312/pythonw.exe"),
    ):
        if guess.is_file():
            candidates.append(str(guess))
    return candidates[0] if candidates else None


class VoiceServerManager:
    """Manages the ``voice_server.py`` subprocess.

    Lifecycle mirrors ``ServerManager`` for consistency:

    * ``start(script_path, port=5111)`` → spawn the script as a child process.
    * ``stop()`` → terminate it and reap.
    * Health probe runs in a background thread; surfaces ``"running"`` once
      port 5111 accepts a TCP connection, ``"error"`` if the process dies.
    """

    DEFAULT_PORT = 5111

    def __init__(
        self,
        on_log: Optional[LogCallback] = None,
        on_status: Optional[StatusCallback] = None,
    ) -> None:
        self._on_log = on_log or (lambda level, msg: None)
        self._on_status = on_status or (lambda s: None)
        self._proc: Optional[subprocess.Popen] = None
        self._reader_thread: Optional[threading.Thread] = None
        self._health_thread: Optional[threading.Thread] = None
        self._ready_event = threading.Event()
        self._stop_event = threading.Event()
        self._lock = threading.Lock()
        self._script_path: Optional[Path] = None
        self._port: int = self.DEFAULT_PORT
        self._started_at: float = 0.0

    # ── public state ───────────────────────────────────────────────────

    @property
    def is_running(self) -> bool:
        with self._lock:
            return self._proc is not None and self._proc.poll() is None

    @property
    def port(self) -> int:
        return self._port

    @property
    def uptime_seconds(self) -> float:
        if not self.is_running or self._started_at == 0:
            return 0.0
        return time.time() - self._started_at

    @property
    def script_path(self) -> Optional[Path]:
        return self._script_path

    # ── control ────────────────────────────────────────────────────────

    def start(
        self,
        script_path: Path,
        port: int = DEFAULT_PORT,
        python_exe: Optional[str] = None,
    ) -> None:
        with self._lock:
            if self._proc is not None and self._proc.poll() is None:
                self._on_log("warn", "语音服务已在运行")
                return

            if not script_path.is_file():
                self._on_log("error", f"找不到 voice_server.py: {script_path}")
                self._on_status("error")
                return

            python = python_exe or _resolve_python_for_voice()
            if not python:
                self._on_log(
                    "error",
                    "找不到 Python 解释器（请安装 Python 3.10+ 并把它加入 PATH）",
                )
                self._on_status("error")
                return

            self._ready_event.clear()
            self._stop_event.clear()
            self._script_path = script_path
            self._port = port

            cmd = [python, str(script_path)]
            self._on_log("info", "启动语音服务: " + " ".join(cmd))

            creationflags = 0
            if os.name == "nt":
                creationflags = subprocess.CREATE_NO_WINDOW

            try:
                self._proc = subprocess.Popen(
                    cmd,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.STDOUT,
                    bufsize=1,
                    universal_newlines=True,
                    encoding="utf-8",
                    errors="replace",
                    cwd=str(script_path.parent),
                    creationflags=creationflags,
                )
            except Exception as exc:
                self._on_log("error", f"语音服务启动失败: {exc}")
                self._on_status("error")
                return

            self._started_at = time.time()

        self._on_status("starting")
        self._reader_thread = threading.Thread(
            target=self._read_loop, name="voice-reader", daemon=True
        )
        self._reader_thread.start()
        self._health_thread = threading.Thread(
            target=self._health_loop, name="voice-health", daemon=True
        )
        self._health_thread.start()

    def stop(self) -> None:
        with self._lock:
            proc = self._proc
            self._stop_event.set()
        if proc is None:
            return
        try:
            self._on_log("info", "停止语音服务...")
            proc.terminate()
            try:
                proc.wait(timeout=3)
            except subprocess.TimeoutExpired:
                proc.kill()
                proc.wait(timeout=2)
        except Exception as exc:
            self._on_log("error", f"语音服务停止失败: {exc}")
        finally:
            with self._lock:
                self._proc = None
                self._started_at = 0.0
            self._on_status("stopped")

    # ── internals ──────────────────────────────────────────────────────

    def _read_loop(self) -> None:
        proc = self._proc
        if proc is None or proc.stdout is None:
            return
        for line in iter(proc.stdout.readline, ""):
            line = line.rstrip()
            if not line:
                continue
            level = "error" if ("error" in line.lower()
                                or "traceback" in line.lower()) else "info"
            self._on_log(level, "[voice] " + line)
        rc = proc.wait()
        stop_requested = self._stop_event.is_set()
        self._stop_event.set()
        if stop_requested:
            self._on_log("info", "语音服务退出（用户停止）")
            self._on_status("stopped")
        elif rc == 0:
            self._on_log("info", "语音服务正常退出")
            self._on_status("stopped")
        else:
            self._on_log("error", f"语音服务异常退出，返回码 {rc}")
            self._on_status("error")
        with self._lock:
            self._proc = None
            self._started_at = 0.0

    def _health_loop(self) -> None:
        deadline = time.time() + 30
        while time.time() < deadline:
            if self._stop_event.is_set() or self._ready_event.is_set():
                return
            if not self.is_running:
                return
            if self._port_open("127.0.0.1", self._port, timeout=0.4):
                self._ready_event.set()
                self._on_log(
                    "info", f"语音服务就绪 · http://127.0.0.1:{self._port}"
                )
                self._on_status("running")
                return
            time.sleep(0.4)
        if self.is_running and not self._ready_event.is_set():
            self._on_log(
                "warn",
                "语音服务进程在运行，但端口 30s 内未就绪。检查 edge_tts 是否已安装。",
            )

    @staticmethod
    def _port_open(host: str, port: int, timeout: float = 0.5) -> bool:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(timeout)
        try:
            sock.connect((host, port))
            return True
        except OSError:
            return False
        finally:
            try:
                sock.close()
            except Exception:
                pass
