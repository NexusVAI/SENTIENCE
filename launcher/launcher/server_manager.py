"""llama-server.exe lifecycle manager.

Spawns llama-server as a subprocess, captures stdout/stderr line-by-line,
exposes status events to the UI thread via callbacks.
"""
from __future__ import annotations

import os
import shlex
import subprocess
import threading
import time
import urllib.error
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, List, Optional


@dataclass
class ServerArgs:
    engine_path: str
    model_path: str
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
    temperature: float = 0.7
    top_p: float = 0.9
    top_k: int = 40
    repeat_penalty: float = 1.1
    min_p: float = 0.05
    extra_args: str = ""

    def build_command(self) -> List[str]:
        cmd: List[str] = [
            self.engine_path,
            "-m", self.model_path,
            "--host", self.host,
            "--port", str(self.port),
            "-t", str(self.threads),
            "-tb", str(self.threads_batch),
            "-c", str(self.context_size),
            "--parallel", str(self.parallel_slots),
            "--temp", str(self.temperature),
            "--top-p", str(self.top_p),
            "--top-k", str(self.top_k),
            "--repeat-penalty", str(self.repeat_penalty),
            "--min-p", str(self.min_p),
            "--verbosity", "1",
        ]
        if self.use_mmap:
            cmd.append("--mmap")
        else:
            cmd.append("--no-mmap")
        if self.use_mlock:
            cmd.append("--mlock")
        if self.cont_batching:
            cmd.append("--cont-batching")
        if self.flash_attn:
            cmd.append("-fa")
        if self.n_gpu_layers > 0:
            cmd.extend(["-ngl", str(self.n_gpu_layers)])
        if self.extra_args.strip():
            for tok in shlex.split(self.extra_args.strip()):
                cmd.append(tok)
        return cmd


LogCallback = Callable[[str, str], None]      # (level, message)
StatusCallback = Callable[[str], None]         # status string


class ServerManager:
    """Manages a single llama-server process."""

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
        self._args: Optional[ServerArgs] = None
        self._started_at: float = 0.0

    @property
    def is_running(self) -> bool:
        with self._lock:
            return self._proc is not None and self._proc.poll() is None

    @property
    def pid(self) -> Optional[int]:
        with self._lock:
            return self._proc.pid if self._proc else None

    @property
    def uptime_seconds(self) -> float:
        if not self.is_running or self._started_at == 0:
            return 0.0
        return time.time() - self._started_at

    @property
    def args(self) -> Optional[ServerArgs]:
        return self._args

    def start(self, args: ServerArgs) -> None:
        with self._lock:
            if self._proc is not None and self._proc.poll() is None:
                self._on_log("warn", "server already running")
                return

            self._ready_event.clear()
            self._stop_event.clear()

            if not Path(args.engine_path).exists():
                self._on_log("error", f"engine not found: {args.engine_path}")
                self._on_status("error")
                return
            if not Path(args.model_path).exists():
                self._on_log("error", f"model not found: {args.model_path}")
                self._on_status("error")
                return

            try:
                cmd = args.build_command()
            except ValueError as exc:
                self._on_log("error", f"invalid extra args: {exc}")
                self._on_status("error")
                return
            self._on_log("info", "starting: " + " ".join(self._safe_quote(c) for c in cmd))

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
                    cwd=str(Path(args.engine_path).parent),
                    creationflags=creationflags,
                )
            except Exception as exc:
                self._on_log("error", f"spawn failed: {exc}")
                self._on_status("error")
                return

            self._args = args
            self._started_at = time.time()

        self._on_status("starting")
        self._reader_thread = threading.Thread(
            target=self._read_loop, name="llama-server-reader", daemon=True
        )
        self._reader_thread.start()
        self._health_thread = threading.Thread(
            target=self._health_loop, name="llama-server-health", daemon=True
        )
        self._health_thread.start()

    def stop(self) -> None:
        with self._lock:
            proc = self._proc
            self._stop_event.set()
        if proc is None:
            return
        try:
            self._on_log("info", "stopping server...")
            if os.name == "nt":
                proc.terminate()
            else:
                proc.terminate()
            try:
                proc.wait(timeout=5)
            except subprocess.TimeoutExpired:
                proc.kill()
                proc.wait(timeout=2)
        except Exception as exc:
            self._on_log("error", f"stop failed: {exc}")
        finally:
            with self._lock:
                self._proc = None
                self._started_at = 0.0
            self._on_status("stopped")

    def _read_loop(self) -> None:
        proc = self._proc
        if proc is None or proc.stdout is None:
            return

        ready_signaled = False
        for line in iter(proc.stdout.readline, ""):
            line = line.rstrip()
            if not line:
                continue
            level = self._classify_level(line)
            self._on_log(level, line)

            if not ready_signaled and self._is_ready_line(line):
                ready_signaled = True
                self._signal_ready()

        rc = proc.wait()
        stop_requested = self._stop_event.is_set()
        self._stop_event.set()
        if stop_requested:
            self._on_log("info", "server exited after stop request")
            self._on_status("stopped")
        elif rc == 0:
            self._on_log("info", "server exited cleanly")
            self._on_status("stopped")
        else:
            self._on_log("error", f"server exited with code {rc}")
            self._on_status("error")
        with self._lock:
            self._proc = None
            self._started_at = 0.0

    def _signal_ready(self, source: str = "") -> None:
        with self._lock:
            if self._ready_event.is_set():
                return
            self._ready_event.set()
        if source:
            self._on_log("info", f"server ready via {source}")
        self._on_status("running")

    def _health_loop(self) -> None:
        args = self._args
        if args is None:
            return
        host = self._url_host(args.host)
        urls = [
            f"http://{host}:{args.port}/health",
            f"http://{host}:{args.port}/v1/models",
        ]
        deadline = time.time() + 300
        while time.time() < deadline:
            if self._stop_event.is_set() or self._ready_event.is_set():
                return
            if not self.is_running:
                return
            for url in urls:
                if self._probe_ready(url):
                    self._signal_ready("HTTP health check")
                    return
            time.sleep(0.5)
        if self.is_running and not self._ready_event.is_set():
            self._on_log(
                "warn",
                "server process is alive but HTTP health check did not become ready",
            )

    @staticmethod
    def _url_host(host: str) -> str:
        h = (host or "127.0.0.1").strip()
        if h in ("0.0.0.0", "::", "[::]"):
            h = "127.0.0.1"
        if ":" in h and not h.startswith("["):
            return f"[{h}]"
        return h

    @staticmethod
    def _probe_ready(url: str) -> bool:
        try:
            req = urllib.request.Request(url, headers={"Accept": "application/json"})
            with urllib.request.urlopen(req, timeout=1.5) as resp:
                code = getattr(resp, "status", resp.getcode())
                body = resp.read(512).decode("utf-8", errors="replace").lower()
                if not 200 <= int(code) < 300:
                    return False
                blocked = ("loading", "starting", "unavailable", "not ready")
                return not any(word in body for word in blocked)
        except urllib.error.HTTPError:
            return False
        except Exception:
            return False

    @staticmethod
    def _is_ready_line(line: str) -> bool:
        low = line.lower()
        markers = (
            "http server listening",
            "server is listening",
            "server listening",
            "listening on http",
            "listening at http",
            "starting the main loop",
            "main: http",
            "all slots are idle",
        )
        return any(m in low for m in markers)

    @staticmethod
    def _classify_level(line: str) -> str:
        low = line.lower()
        if "error" in low or "failed" in low or "fatal" in low:
            return "error"
        if "warn" in low:
            return "warn"
        return "info"

    @staticmethod
    def _safe_quote(arg: str) -> str:
        if " " in arg or "\\" in arg:
            return f'"{arg}"'
        return arg


def discover_engines(search_roots: List[Path]) -> List[Path]:
    """Find llama-server.exe under provided roots."""
    found: List[Path] = []
    seen = set()
    for root in search_roots:
        if not root.exists():
            continue
        try:
            for p in root.rglob("llama-server.exe"):
                resolved = p.resolve()
                if resolved not in seen:
                    seen.add(resolved)
                    found.append(resolved)
        except Exception:
            continue
    return found


def discover_models(search_roots: List[Path]) -> List[Path]:
    """Find .gguf files (excluding mmproj) under provided roots."""
    found: List[Path] = []
    seen = set()
    for root in search_roots:
        if not root.exists():
            continue
        try:
            for p in root.rglob("*.gguf"):
                if "mmproj" in p.name.lower():
                    continue
                resolved = p.resolve()
                if resolved not in seen:
                    seen.add(resolved)
                    found.append(resolved)
        except Exception:
            continue
    return found
