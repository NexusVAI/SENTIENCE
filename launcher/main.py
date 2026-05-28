"""Sentience V5 - Anima Launcher
Material Design 3 launcher for llama-server backed by the Sentience V5 model.
"""
from __future__ import annotations

import os
import sys
import threading
import time
from pathlib import Path
from typing import Callable, List, Optional

import flet as ft


# ───────────────────────────────────────────────────────────────────────────
# flet API compatibility shims (works across 0.21 → 0.85)
# ───────────────────────────────────────────────────────────────────────────

def _pad_all(v):
    """all-sides padding."""
    return ft.Padding(left=v, right=v, top=v, bottom=v)


def _pad_xy(horizontal=0, vertical=0):
    """horizontal+vertical symmetric padding."""
    return ft.Padding(
        left=horizontal, right=horizontal, top=vertical, bottom=vertical
    )


def _pad_only(left=0, right=0, top=0, bottom=0):
    return ft.Padding(left=left, right=right, top=top, bottom=bottom)


def _border_all(width, color):
    side = ft.BorderSide(width, color)
    return ft.Border(top=side, bottom=side, left=side, right=side)


def _border_bottom(width, color):
    return ft.Border(bottom=ft.BorderSide(width, color))


def _align_center():
    return ft.Alignment(0, 0)


# Local imports
sys.path.insert(0, str(Path(__file__).parent))
from config_store import LauncherConfig, write_mod_config, mod_config_ini_path
from chat_client import (
    ChatMessage,
    ChatRequest,
    DEFAULT_SYSTEM_PROMPT,
    stream_chat,
)
from server_manager import (
    ServerManager,
    ServerArgs,
    discover_engines,
    discover_models,
)
from voice_server_manager import (
    VoiceServerManager,
    discover_voice_script,
)
from theme import (
    APP_TITLE,
    APP_VERSION,
    BRAND_TAGLINE,
    SEED_COLOR,
    STATUS_RUNNING,
    STATUS_STOPPED,
    STATUS_ERROR,
    STATUS_WARNING,
    build_light_theme,
    build_dark_theme,
)


# ───────────────────────────────────────────────────────────────────────────
# Path discovery
# ───────────────────────────────────────────────────────────────────────────


def _app_root() -> Path:
    """Return the SentienceV5-Anima project root, regardless of run mode."""
    if getattr(sys, "frozen", False):
        # PyInstaller --onedir: SentienceV5.exe sits at the project root.
        return Path(sys.executable).parent
    return Path(__file__).resolve().parent.parent


APP_ROOT = _app_root()


def _configure_flet_view_path() -> None:
    """Point flet_desktop at the bundled flet.exe so it never tries to
    download the desktop runtime from GitHub at startup.

    flet_desktop's resolution order (Windows) is:
      1. ``./build/windows/*.exe`` in CWD
      2. ``$FLET_VIEW_PATH/flet.exe``
      3. ``~/.flet/client/...`` (downloads from GitHub if missing)
    Step 3 is the source of the WinError 10060 timeout when the user has
    no network. We always pin step 2 in frozen mode.
    """
    if not getattr(sys, "frozen", False):
        return
    existing = os.environ.get("FLET_VIEW_PATH")
    if existing and (Path(existing) / "flet.exe").is_file():
        return
    roots: List[Path] = []
    exe_dir = Path(sys.executable).parent
    roots.append(exe_dir)
    roots.append(exe_dir / "_internal")
    meipass = getattr(sys, "_MEIPASS", None)
    if meipass:
        roots.append(Path(meipass))
    seen: set[str] = set()
    for root in roots:
        for sub in (
            Path("flet_desktop") / "app" / "flet",
            Path("flet_desktop") / "app",
            Path("flet"),
        ):
            candidate = root / sub
            key = str(candidate).lower()
            if key in seen:
                continue
            seen.add(key)
            if (candidate / "flet.exe").is_file():
                os.environ["FLET_VIEW_PATH"] = str(candidate)
                return


_configure_flet_view_path()


def default_search_roots() -> List[Path]:
    """Roots scanned for llama-server.exe / *.gguf / voice_server.py."""
    candidates = [
        APP_ROOT / "Runtime",
        APP_ROOT,
        APP_ROOT.parent / "SentienceV4.1-Omni" / "runtime",
        APP_ROOT.parent / "模型位置",
        APP_ROOT.parent / "CPU",
        APP_ROOT.parent / "GTA5MOD2026" / "GTA5MOD2026",
        Path.home() / "Documents" / "GTA5MOD2026",
    ]
    return [c for c in candidates if c.exists()]


def asset_path(name: str) -> str:
    """Locate an asset file (icon/image). Works in dev and PyInstaller mode."""
    candidates = [
        Path(__file__).resolve().parent / "assets" / name,
        APP_ROOT / "launcher" / "assets" / name,
        APP_ROOT / "assets" / name,
        APP_ROOT / name,
    ]
    if getattr(sys, "frozen", False):
        # --onefile sets _MEIPASS to a temp dir; --onedir does not but
        # the assets sit in <exe_dir>/_internal/assets/ instead.
        exe_dir = Path(sys.executable).parent
        candidates = [
            exe_dir / "_internal" / "assets" / name,
            exe_dir / "_internal" / name,
            exe_dir / "assets" / name,
            exe_dir / name,
        ] + candidates
        meipass = getattr(sys, "_MEIPASS", None)
        if meipass:
            mp = Path(meipass)
            candidates.insert(0, mp / "assets" / name)
            candidates.insert(0, mp / name)
    for p in candidates:
        if p.exists():
            return str(p)
    return name  # let flet handle the missing file gracefully


# Open a URL in the default browser (used by About page links).
def open_url(url: str) -> None:
    import webbrowser
    try:
        webbrowser.open(url)
    except Exception:
        pass


# ───────────────────────────────────────────────────────────────────────────
# Main app
# ───────────────────────────────────────────────────────────────────────────


def main(page: ft.Page) -> None:
    page.title = APP_TITLE
    # flet 0.85+: use page.window.* (older releases shim page.window_*)
    icon_path = asset_path("NexusV.ico")
    try:
        page.window.width = 1180
        page.window.height = 760
        page.window.min_width = 980
        page.window.min_height = 640
        page.window.title_bar_hidden = True
        page.window.title_bar_buttons_hidden = True
        if Path(icon_path).exists():
            page.window.icon = icon_path
    except Exception:
        page.window_width = 1180
        page.window_height = 760
        page.window_min_width = 980
        page.window_min_height = 640
    page.padding = 0
    page.bgcolor = ft.Colors.SURFACE
    page.theme = build_light_theme()
    page.dark_theme = build_dark_theme()

    # ─── State ──────────────────────────────────────────────────────────────
    config = LauncherConfig.load()
    page.theme_mode = {
        "light": ft.ThemeMode.LIGHT,
        "dark": ft.ThemeMode.DARK,
    }.get(config.theme_mode, ft.ThemeMode.SYSTEM)

    log_entries: List[ft.Control] = []
    selected_view = {"index": 0}

    # Build status state holder
    status_state = {"value": "stopped"}

    def _run_on_ui_thread(fn: Callable[[], None]) -> None:
        def safe_fn() -> None:
            try:
                fn()
            except Exception:
                pass
        try:
            session = getattr(page, "session", None)
            connection = getattr(session, "connection", None)
            loop = getattr(connection, "loop", None)
            if loop is not None:
                loop.call_soon_threadsafe(safe_fn)
                return
        except Exception:
            pass
        safe_fn()

    # ─── Server callbacks ───────────────────────────────────────────────────

    def append_log(level: str, message: str) -> None:
        def apply() -> None:
            ts = time.strftime("%H:%M:%S")
            color = {
                "error": STATUS_ERROR,
                "warn": STATUS_WARNING,
                "info": ft.Colors.ON_SURFACE_VARIANT,
            }.get(level, ft.Colors.ON_SURFACE_VARIANT)
            entry = ft.Row(
                [
                    ft.Text(ts, size=11, font_family="Consolas",
                            color=ft.Colors.OUTLINE, width=72),
                    ft.Container(
                        content=ft.Text(level.upper(), size=10, weight=ft.FontWeight.W_700,
                                        color=color),
                        padding=_pad_xy(horizontal=6, vertical=2),
                        border_radius=4,
                        width=58,
                    ),
                    ft.Text(message, size=12, font_family="Consolas",
                            selectable=True, expand=True),
                ],
                spacing=8,
                vertical_alignment=ft.CrossAxisAlignment.START,
            )
            log_entries.append(entry)
            # Cap to 1500 entries
            if len(log_entries) > 1500:
                del log_entries[: len(log_entries) - 1500]
            try:
                log_list_view.controls = list(log_entries)
                log_list_view.update()
            except Exception:
                pass
        _run_on_ui_thread(apply)

    def update_status(new_status: str) -> None:
        def apply() -> None:
            status_state["value"] = new_status
            try:
                refresh_status_card()
            except Exception:
                pass
        _run_on_ui_thread(apply)

    server = ServerManager(on_log=append_log, on_status=update_status)

    # Voice TTS server (edge-tts) state + manager.
    voice_status_state = {"value": "stopped"}

    def update_voice_status(new_status: str) -> None:
        def apply() -> None:
            voice_status_state["value"] = new_status
            try:
                refresh_voice_card()
            except Exception:
                pass
        _run_on_ui_thread(apply)

    voice_server = VoiceServerManager(
        on_log=append_log, on_status=update_voice_status,
    )

    # ─── Discover resources ─────────────────────────────────────────────────

    def rescan_resources() -> None:
        roots = default_search_roots()
        engines = discover_engines(roots)
        models = discover_models(roots)

        engine_dropdown.options = [
            ft.dropdown.Option(str(p)) for p in engines
        ]
        if config.engine_path and Path(config.engine_path).exists():
            engine_dropdown.value = config.engine_path
        elif engines:
            engine_dropdown.value = str(engines[0])
            config.engine_path = engine_dropdown.value

        model_dropdown.options = [
            ft.dropdown.Option(str(p)) for p in models
        ]
        if config.model_path and Path(config.model_path).exists():
            model_dropdown.value = config.model_path
        elif models:
            model_dropdown.value = str(models[0])
            config.model_path = model_dropdown.value

        try:
            engine_dropdown.update()
            model_dropdown.update()
            refresh_status_card()
        except Exception:
            pass

        append_log("info",
                   f"扫描完成：引擎 {len(engines)} · 模型 {len(models)}")

    # ─── Top app bar ────────────────────────────────────────────────────────

    def cycle_theme(_: ft.ControlEvent) -> None:
        order = [ft.ThemeMode.LIGHT, ft.ThemeMode.DARK, ft.ThemeMode.SYSTEM]
        try:
            idx = order.index(page.theme_mode)
        except ValueError:
            idx = 0
        page.theme_mode = order[(idx + 1) % len(order)]
        config.theme_mode = {
            ft.ThemeMode.LIGHT: "light",
            ft.ThemeMode.DARK: "dark",
            ft.ThemeMode.SYSTEM: "system",
        }[page.theme_mode]
        config.save()
        theme_btn.icon = theme_icon_for(page.theme_mode)
        theme_btn.tooltip = f"主题：{config.theme_mode}"
        page.update()

    def theme_icon_for(mode: ft.ThemeMode) -> str:
        return {
            ft.ThemeMode.LIGHT: ft.Icons.LIGHT_MODE_ROUNDED,
            ft.ThemeMode.DARK: ft.Icons.DARK_MODE_ROUNDED,
            ft.ThemeMode.SYSTEM: ft.Icons.BRIGHTNESS_AUTO_ROUNDED,
        }.get(mode, ft.Icons.BRIGHTNESS_AUTO_ROUNDED)

    theme_btn = ft.IconButton(
        icon=theme_icon_for(page.theme_mode),
        tooltip=f"主题：{config.theme_mode}",
        on_click=cycle_theme,
    )

    rescan_btn = ft.IconButton(
        icon=ft.Icons.REFRESH_ROUNDED,
        tooltip="重新扫描引擎与模型",
        on_click=lambda _e: rescan_resources(),
    )

    # ─── Custom (frameless) title bar with window controls ──────────────────

    def window_minimize(_e=None) -> None:
        try:
            page.window.minimized = True
            page.update()
        except Exception:
            pass

    def window_maximize_toggle(_e=None) -> None:
        try:
            page.window.maximized = not bool(getattr(page.window, "maximized", False))
            page.update()
            # Update the icon to reflect state
            max_btn.icon = (
                ft.Icons.FILTER_NONE_ROUNDED
                if page.window.maximized
                else ft.Icons.CROP_SQUARE_ROUNDED
            )
            max_btn.update()
        except Exception:
            pass

    def window_close(_e=None) -> None:
        # 1) Stop server in background so we don't block the close.
        threading.Thread(target=server.stop, daemon=True).start()
        # 2) Try the polite flet API first.
        for fn_name in ("destroy", "close"):
            try:
                fn = getattr(page.window, fn_name, None)
                if callable(fn):
                    fn()
            except Exception:
                pass
        # 3) Force-kill the whole process tree (Python + Flutter window).
        #    Frameless flet apps on Windows often refuse to close via the
        #    polite API alone, so we taskkill the entire tree of this PID.
        def _hard_exit() -> None:
            time.sleep(0.25)
            try:
                import subprocess
                subprocess.run(
                    ["taskkill", "/F", "/T", "/PID", str(os.getpid())],
                    creationflags=0x08000000,  # CREATE_NO_WINDOW
                    timeout=3,
                )
            except Exception:
                pass
            try:
                os._exit(0)
            except Exception:
                pass
        threading.Thread(target=_hard_exit, daemon=True).start()

    min_btn = ft.IconButton(
        icon=ft.Icons.REMOVE_ROUNDED,
        tooltip="最小化",
        icon_size=18,
        on_click=window_minimize,
    )
    max_btn = ft.IconButton(
        icon=ft.Icons.CROP_SQUARE_ROUNDED,
        tooltip="最大化 / 还原",
        icon_size=16,
        on_click=window_maximize_toggle,
    )
    close_btn = ft.IconButton(
        icon=ft.Icons.CLOSE_ROUNDED,
        tooltip="关闭",
        icon_size=20,
        on_click=window_close,
        style=ft.ButtonStyle(
            overlay_color={"hovered": ft.Colors.with_opacity(0.18, ft.Colors.ERROR)},
        ),
    )

    # Logo image (falls back to icon if file missing)
    logo_png = asset_path("NexusV_64.png")
    if Path(logo_png).exists():
        brand_logo = ft.Image(
            src=logo_png, width=36, height=36, border_radius=10, fit=ft.BoxFit.COVER,
        )
    else:
        brand_logo = ft.Container(
            content=ft.Icon(ft.Icons.AUTO_AWESOME_ROUNDED, size=22, color=SEED_COLOR),
            width=36, height=36, border_radius=10,
            bgcolor=ft.Colors.with_opacity(0.12, SEED_COLOR),
            alignment=_align_center(),
        )

    drag_area = ft.WindowDragArea(
        content=ft.Container(
            padding=_pad_only(left=20, right=12, top=0, bottom=0),
            content=ft.Row(
                [
                    brand_logo,
                    ft.Column(
                        [
                            ft.Text(APP_TITLE, size=15, weight=ft.FontWeight.W_700),
                            ft.Text(
                                BRAND_TAGLINE, size=11,
                                color=ft.Colors.ON_SURFACE_VARIANT,
                            ),
                        ],
                        spacing=0,
                        alignment=ft.MainAxisAlignment.CENTER,
                    ),
                    ft.Container(expand=True),
                    ft.Container(
                        content=ft.Text(
                            f"v{APP_VERSION}", size=11,
                            color=ft.Colors.ON_SURFACE_VARIANT,
                            weight=ft.FontWeight.W_500,
                        ),
                        padding=_pad_xy(horizontal=10, vertical=4),
                        border_radius=12,
                        bgcolor=ft.Colors.SURFACE_CONTAINER_HIGHEST,
                    ),
                ],
                vertical_alignment=ft.CrossAxisAlignment.CENTER,
                spacing=12,
            ),
            height=60,
        ),
        expand=True,
    )

    app_bar = ft.Container(
        height=60,
        bgcolor=ft.Colors.SURFACE_CONTAINER_LOW,
        border=_border_bottom(1, ft.Colors.OUTLINE_VARIANT),
        content=ft.Row(
            [
                drag_area,
                rescan_btn,
                theme_btn,
                ft.Container(width=8),
                min_btn,
                max_btn,
                close_btn,
                ft.Container(width=4),
            ],
            spacing=0,
            vertical_alignment=ft.CrossAxisAlignment.CENTER,
        ),
    )

    # ─── Status card ────────────────────────────────────────────────────────

    status_dot = ft.Container(
        width=10, height=10, border_radius=5, bgcolor=STATUS_STOPPED,
    )
    status_text = ft.Text(
        "已停止", size=22, weight=ft.FontWeight.W_700,
    )
    status_subtext = ft.Text(
        "服务器未运行 · 点击右侧 启动", size=12,
        color=ft.Colors.ON_SURFACE_VARIANT,
    )
    status_endpoint = ft.Text(
        "", size=12, font_family="Consolas",
        color=ft.Colors.ON_SURFACE_VARIANT, selectable=True,
    )
    uptime_text = ft.Text(
        "", size=12, color=ft.Colors.ON_SURFACE_VARIANT,
    )

    def start_server(_: Optional[ft.ControlEvent] = None) -> None:
        if server.is_running:
            return
        if not config.engine_path or not Path(config.engine_path).exists():
            append_log("error", "未选择有效的 llama-server.exe")
            update_status("error")
            return
        if not config.model_path or not Path(config.model_path).exists():
            append_log("error", "未选择有效的 .gguf 模型")
            update_status("error")
            return
        if not commit_settings_from_ui():
            update_status("error")
            return
        config.save()
        # Keep the GTA5 mod's config.ini in sync whenever we (re)start the
        # server.  Without this, changing host/port in the launcher leaves
        # the in-game mod pointing at the previous endpoint -> AI Error.
        try:
            mod_path = write_mod_config(config)
            if mod_path is not None:
                append_log("info", f"已同步 mod 配置 → {mod_path}")
        except Exception as exc:
            append_log("warn", f"mod 配置同步失败: {exc}")
        args = ServerArgs(
            engine_path=config.engine_path,
            model_path=config.model_path,
            host=config.host,
            port=config.port,
            threads=config.threads,
            threads_batch=config.threads_batch,
            context_size=config.context_size,
            parallel_slots=config.parallel_slots,
            use_mmap=config.use_mmap,
            use_mlock=config.use_mlock,
            cont_batching=config.cont_batching,
            flash_attn=config.flash_attn,
            n_gpu_layers=config.n_gpu_layers,
            temperature=config.temperature,
            top_p=config.top_p,
            top_k=config.top_k,
            repeat_penalty=config.repeat_penalty,
            min_p=config.min_p,
            extra_args=config.extra_args,
        )
        server.start(args)

    def stop_server(_: Optional[ft.ControlEvent] = None) -> None:
        threading.Thread(target=server.stop, daemon=True).start()

    # ─── Voice TTS server controls ──────────────────────────────────────────

    voice_dot = ft.Container(
        width=10, height=10, border_radius=5, bgcolor=STATUS_STOPPED,
    )
    voice_text = ft.Text(
        "语音 TTS 已停止", size=14, weight=ft.FontWeight.W_600,
    )
    voice_subtext = ft.Text(
        "未运行 · 启动后游戏内 NPC 才有语音", size=11,
        color=ft.Colors.ON_SURFACE_VARIANT,
    )
    voice_endpoint = ft.Text(
        "", size=11, font_family="Consolas",
        color=ft.Colors.ON_SURFACE_VARIANT, selectable=True,
    )

    def start_voice(_: Optional[ft.ControlEvent] = None) -> None:
        if voice_server.is_running:
            return
        script = discover_voice_script(default_search_roots())
        if script is None:
            append_log(
                "error",
                "找不到 voice_server.py（请把它放在 SentienceV5-Anima 目录下，"
                "或保留 GTA5MOD2026 源目录）",
            )
            update_voice_status("error")
            return
        threading.Thread(
            target=voice_server.start, args=(script,), daemon=True
        ).start()

    def stop_voice(_: Optional[ft.ControlEvent] = None) -> None:
        threading.Thread(target=voice_server.stop, daemon=True).start()

    voice_start_btn = ft.FilledTonalButton(
        content="启动语音", icon=ft.Icons.RECORD_VOICE_OVER_ROUNDED,
        on_click=start_voice, height=40,
        style=ft.ButtonStyle(padding=_pad_xy(horizontal=18, vertical=8)),
    )
    voice_stop_btn = ft.OutlinedButton(
        content="停止语音", icon=ft.Icons.VOICE_OVER_OFF_ROUNDED,
        on_click=stop_voice, height=40, disabled=True,
        style=ft.ButtonStyle(padding=_pad_xy(horizontal=14, vertical=8)),
    )

    def refresh_voice_card() -> None:
        s = voice_status_state["value"]
        if s == "running":
            voice_dot.bgcolor = STATUS_RUNNING
            voice_text.value = "语音 TTS 运行中"
            voice_subtext.value = "游戏内 NPC 将由 edge-tts 朗读"
            voice_start_btn.disabled = True
            voice_stop_btn.disabled = False
        elif s == "starting":
            voice_dot.bgcolor = STATUS_WARNING
            voice_text.value = "语音 TTS 启动中"
            voice_subtext.value = "正在拉起 edge-tts 子进程..."
            voice_start_btn.disabled = True
            voice_stop_btn.disabled = False
        elif s == "error":
            voice_dot.bgcolor = STATUS_ERROR
            voice_text.value = "语音 TTS 异常"
            voice_subtext.value = "未启动或异常退出，查看日志"
            voice_start_btn.disabled = False
            voice_stop_btn.disabled = True
        else:
            voice_dot.bgcolor = STATUS_STOPPED
            voice_text.value = "语音 TTS 已停止"
            voice_subtext.value = "未运行 · 启动后游戏内 NPC 才有语音"
            voice_start_btn.disabled = False
            voice_stop_btn.disabled = True
        voice_endpoint.value = f"http://127.0.0.1:{voice_server.port}"
        try:
            page.update()
        except Exception:
            pass

    voice_card = ft.Container(
        padding=18,
        border_radius=16,
        bgcolor=ft.Colors.SURFACE_CONTAINER_LOW,
        border=_border_all(1, ft.Colors.OUTLINE_VARIANT),
        content=ft.Row(
            [
                ft.Column(
                    [
                        ft.Row(
                            [voice_dot, voice_text],
                            spacing=10,
                            vertical_alignment=ft.CrossAxisAlignment.CENTER,
                        ),
                        voice_subtext,
                        ft.Container(height=4),
                        ft.Row(
                            [
                                ft.Icon(
                                    ft.Icons.HEADPHONES_ROUNDED,
                                    size=14,
                                    color=ft.Colors.ON_SURFACE_VARIANT,
                                ),
                                voice_endpoint,
                            ],
                            spacing=6,
                        ),
                    ],
                    spacing=4,
                    expand=True,
                ),
                ft.Row(
                    [voice_stop_btn, voice_start_btn],
                    spacing=10,
                ),
            ],
            vertical_alignment=ft.CrossAxisAlignment.CENTER,
        ),
    )

    primary_btn = ft.FilledButton(
        content="启动",
        icon=ft.Icons.PLAY_ARROW_ROUNDED,
        on_click=start_server,
        height=44,
        style=ft.ButtonStyle(
            padding=_pad_xy(horizontal=22, vertical=10),
            text_style=ft.TextStyle(size=14, weight=ft.FontWeight.W_600),
        ),
    )
    secondary_btn = ft.OutlinedButton(
        content="停止",
        icon=ft.Icons.STOP_ROUNDED,
        on_click=stop_server,
        height=44,
        disabled=True,
        style=ft.ButtonStyle(
            padding=_pad_xy(horizontal=18, vertical=10),
        ),
    )

    def refresh_status_card() -> None:
        s = status_state["value"]
        if s == "running":
            status_dot.bgcolor = STATUS_RUNNING
            status_text.value = "运行中"
            status_subtext.value = "llama-server 健康，已就绪"
            primary_btn.disabled = True
            secondary_btn.disabled = False
        elif s == "starting":
            status_dot.bgcolor = STATUS_WARNING
            status_text.value = "启动中"
            status_subtext.value = "正在加载模型权重，请稍候..."
            primary_btn.disabled = True
            secondary_btn.disabled = False
        elif s == "error":
            status_dot.bgcolor = STATUS_ERROR
            status_text.value = "错误"
            status_subtext.value = "服务器未启动或异常退出，查看日志"
            primary_btn.disabled = False
            secondary_btn.disabled = True
        else:
            status_dot.bgcolor = STATUS_STOPPED
            status_text.value = "已停止"
            status_subtext.value = "服务器未运行 · 点击右侧 启动"
            primary_btn.disabled = False
            secondary_btn.disabled = True
        status_endpoint.value = (
            f"http://{config.host}:{config.port}/v1/chat/completions"
        )
        try:
            page.update()
        except Exception:
            pass

    status_card = ft.Container(
        padding=22,
        border_radius=18,
        bgcolor=ft.Colors.SURFACE_CONTAINER_LOW,
        border=_border_all(1, ft.Colors.OUTLINE_VARIANT),
        content=ft.Row(
            [
                ft.Column(
                    [
                        ft.Row(
                            [status_dot, status_text],
                            spacing=10,
                            vertical_alignment=ft.CrossAxisAlignment.CENTER,
                        ),
                        status_subtext,
                        ft.Container(height=8),
                        ft.Row(
                            [
                                ft.Icon(
                                    ft.Icons.LINK_ROUNDED,
                                    size=14,
                                    color=ft.Colors.ON_SURFACE_VARIANT,
                                ),
                                status_endpoint,
                            ],
                            spacing=6,
                        ),
                        uptime_text,
                    ],
                    spacing=4,
                    expand=True,
                ),
                ft.Row(
                    [secondary_btn, primary_btn],
                    spacing=12,
                ),
            ],
            vertical_alignment=ft.CrossAxisAlignment.CENTER,
        ),
    )

    # ─── Engine + model dropdowns + quick stats ─────────────────────────────

    def on_engine_change(_: ft.ControlEvent) -> None:
        config.engine_path = engine_dropdown.value or ""
        config.save()
        refresh_status_card()

    def on_model_change(_: ft.ControlEvent) -> None:
        config.model_path = model_dropdown.value or ""
        config.save()
        refresh_status_card()

    engine_dropdown = ft.Dropdown(
        label="llama-server.exe",
        on_select=on_engine_change,
        width=520,
        border_radius=10,
    )
    model_dropdown = ft.Dropdown(
        label="GGUF 模型",
        on_select=on_model_change,
        width=520,
        border_radius=10,
    )

    def stat_card(icon: str, label: str, value_ctrl: ft.Control) -> ft.Container:
        return ft.Container(
            padding=16,
            border_radius=14,
            bgcolor=ft.Colors.SURFACE_CONTAINER_LOW,
            border=_border_all(1, ft.Colors.OUTLINE_VARIANT),
            content=ft.Column(
                [
                    ft.Row(
                        [
                            ft.Icon(icon, size=16,
                                    color=ft.Colors.ON_SURFACE_VARIANT),
                            ft.Text(
                                label, size=11, weight=ft.FontWeight.W_500,
                                color=ft.Colors.ON_SURFACE_VARIANT,
                            ),
                        ],
                        spacing=6,
                    ),
                    value_ctrl,
                ],
                spacing=8,
            ),
            expand=1,
        )

    port_value = ft.Text(str(config.port), size=20, weight=ft.FontWeight.W_700)
    threads_value = ft.Text(str(config.threads), size=20, weight=ft.FontWeight.W_700)
    ctx_value = ft.Text(
        f"{config.context_size // 1024}K", size=20, weight=ft.FontWeight.W_700,
    )
    host_value = ft.Text(config.host, size=14, weight=ft.FontWeight.W_600)

    quick_stats = ft.Row(
        [
            stat_card(ft.Icons.LAN_ROUNDED, "监听地址", host_value),
            stat_card(ft.Icons.NUMBERS_ROUNDED, "端口", port_value),
            stat_card(ft.Icons.MEMORY_ROUNDED, "线程", threads_value),
            stat_card(ft.Icons.SPACE_DASHBOARD_ROUNDED, "上下文", ctx_value),
        ],
        spacing=12,
    )

    dashboard_view = ft.Container(
        padding=_pad_xy(horizontal=24, vertical=20),
        content=ft.Column(
            [
                ft.Row(
                    [
                        ft.Text(
                            "仪表盘",
                            size=24, weight=ft.FontWeight.W_700,
                        ),
                    ]
                ),
                ft.Container(height=4),
                status_card,
                ft.Container(height=10),
                voice_card,
                ft.Container(height=16),
                ft.Text(
                    "引擎与模型",
                    size=14, weight=ft.FontWeight.W_600,
                    color=ft.Colors.ON_SURFACE_VARIANT,
                ),
                engine_dropdown,
                model_dropdown,
                ft.Container(height=12),
                ft.Text(
                    "快速指标",
                    size=14, weight=ft.FontWeight.W_600,
                    color=ft.Colors.ON_SURFACE_VARIANT,
                ),
                quick_stats,
            ],
            spacing=10,
            scroll=ft.ScrollMode.AUTO,
        ),
    )

    # ─── Settings view ──────────────────────────────────────────────────────

    host_field = ft.TextField(
        label="监听地址", value=config.host, width=240, border_radius=10,
    )
    port_field = ft.TextField(
        label="端口", value=str(config.port), width=160, border_radius=10,
    )
    threads_field = ft.TextField(
        label="线程数 (-t)", value=str(config.threads), width=160, border_radius=10,
    )
    threads_batch_field = ft.TextField(
        label="批处理线程 (-tb)", value=str(config.threads_batch),
        width=180, border_radius=10,
    )
    ctx_field = ft.TextField(
        label="上下文长度", value=str(config.context_size),
        width=200, border_radius=10,
    )
    parallel_field = ft.TextField(
        label="并行槽位", value=str(config.parallel_slots),
        width=160, border_radius=10,
    )
    ngl_field = ft.TextField(
        label="GPU 层数 (-ngl)", value=str(config.n_gpu_layers),
        width=180, border_radius=10,
    )
    extra_field = ft.TextField(
        label="额外参数", value=config.extra_args,
        hint_text="例：--no-warmup",
        expand=True, border_radius=10,
    )

    use_mmap_sw = ft.Switch(label="使用 mmap", value=config.use_mmap)
    use_mlock_sw = ft.Switch(label="使用 mlock", value=config.use_mlock)
    cont_batch_sw = ft.Switch(label="连续批处理", value=config.cont_batching)
    flash_attn_sw = ft.Switch(label="Flash Attention", value=config.flash_attn)

    # Inference params
    def labeled_slider(
        label_text: str,
        value: float,
        min_val: float,
        max_val: float,
        divisions: int,
        suffix: str = "",
    ):
        value_label = ft.Text(
            f"{value:.2f}{suffix}" if isinstance(value, float)
            else f"{value}{suffix}",
            size=12, weight=ft.FontWeight.W_600,
        )
        slider = ft.Slider(
            min=min_val, max=max_val, divisions=divisions, value=value,
            label="{value}",
        )

        def on_change(e: ft.ControlEvent) -> None:
            v = e.control.value
            if isinstance(value, float):
                value_label.value = f"{v:.2f}{suffix}"
            else:
                value_label.value = f"{int(v)}{suffix}"
            value_label.update()

        slider.on_change = on_change

        column = ft.Column(
            [
                ft.Row(
                    [
                        ft.Text(
                            label_text, size=12, weight=ft.FontWeight.W_500,
                            color=ft.Colors.ON_SURFACE_VARIANT,
                        ),
                        ft.Container(expand=True),
                        value_label,
                    ]
                ),
                slider,
            ],
            spacing=2,
        )
        return column, slider

    temp_col, temp_slider = labeled_slider(
        "Temperature", config.temperature, 0.0, 2.0, 200,
    )
    top_p_col, top_p_slider = labeled_slider(
        "Top P", config.top_p, 0.0, 1.0, 100,
    )
    top_k_col, top_k_slider = labeled_slider(
        "Top K", config.top_k, 0, 200, 200,
    )
    rep_pen_col, rep_pen_slider = labeled_slider(
        "Repeat Penalty", config.repeat_penalty, 0.5, 2.0, 150,
    )
    min_p_col, min_p_slider = labeled_slider(
        "Min P", config.min_p, 0.0, 1.0, 100,
    )

    # ─── Cloud AI provider controls ─────────────────────────────────────────

    cloud_endpoint_field = ft.TextField(
        label="Cloud Endpoint",
        value=config.cloud_endpoint,
        hint_text="https://api.deepseek.com/v1/chat/completions",
        expand=True, border_radius=10,
    )
    cloud_model_field = ft.TextField(
        label="Cloud Model",
        value=config.cloud_model,
        hint_text="deepseek-chat / gpt-4o / qwen-plus ...",
        width=320, border_radius=10,
    )
    cloud_api_key_field = ft.TextField(
        label="API Key",
        value=config.cloud_api_key,
        password=True, can_reveal_password=True,
        expand=True, border_radius=10,
    )

    cloud_section_container = ft.Container(
        opacity=0.5 if config.provider == "local" else 1.0,
        animate_opacity=200,
        content=ft.Column(
            [
                ft.Row([cloud_endpoint_field], spacing=12),
                ft.Row([cloud_model_field], spacing=12),
                ft.Row([cloud_api_key_field], spacing=12),
                ft.Text(
                    "提示：选择 Cloud 后，启动器仍会写 mod config.ini，让 NPC 直接走云端。"
                    "本地引擎可保持关闭。",
                    size=11, color=ft.Colors.ON_SURFACE_VARIANT,
                ),
            ],
            spacing=10,
        ),
    )

    def on_provider_change(e: ft.ControlEvent) -> None:
        new_value = e.control.value
        config.provider = new_value
        cloud_section_container.opacity = 0.5 if new_value == "local" else 1.0
        cloud_section_container.update()

    provider_radio = ft.RadioGroup(
        value=config.provider,
        content=ft.Row(
            [
                ft.Radio(value="local", label="本地 (llama-server)"),
                ft.Container(width=16),
                ft.Radio(value="cloud", label="云端 API (DeepSeek / OpenAI 兼容)"),
            ],
            spacing=8,
        ),
        on_change=on_provider_change,
    )

    def section_card(title: str, *contents) -> ft.Container:
        return ft.Container(
            padding=20,
            border_radius=14,
            bgcolor=ft.Colors.SURFACE_CONTAINER_LOW,
            border=_border_all(1, ft.Colors.OUTLINE_VARIANT),
            content=ft.Column(
                [
                    ft.Text(title, size=15, weight=ft.FontWeight.W_700),
                    ft.Container(height=8),
                    *contents,
                ],
                spacing=10,
            ),
        )

    def commit_settings_from_ui() -> bool:
        try:
            config.host = (host_field.value or "").strip() or "127.0.0.1"
            config.port = int(port_field.value or 5001)
            config.threads = int(threads_field.value or 8)
            config.threads_batch = int(threads_batch_field.value or 8)
            config.context_size = int(ctx_field.value or 8192)
            config.parallel_slots = int(parallel_field.value or 1)
            config.n_gpu_layers = int(ngl_field.value or 0)
            config.extra_args = extra_field.value or ""
            config.use_mmap = bool(use_mmap_sw.value)
            config.use_mlock = bool(use_mlock_sw.value)
            config.cont_batching = bool(cont_batch_sw.value)
            config.flash_attn = bool(flash_attn_sw.value)
            config.temperature = float(temp_slider.value)
            config.top_p = float(top_p_slider.value)
            config.top_k = int(top_k_slider.value)
            config.repeat_penalty = float(rep_pen_slider.value)
            config.min_p = float(min_p_slider.value)
            config.provider = provider_radio.value or "local"
            config.cloud_endpoint = (cloud_endpoint_field.value or "").strip()
            config.cloud_model = (cloud_model_field.value or "").strip()
            config.cloud_api_key = (cloud_api_key_field.value or "").strip()
            if not 1 <= config.port <= 65535:
                raise ValueError("端口必须在 1-65535 之间")
            if config.threads < 1:
                raise ValueError("线程数必须大于 0")
            if config.threads_batch < 1:
                raise ValueError("批处理线程必须大于 0")
            if config.context_size < 512:
                raise ValueError("上下文长度至少为 512")
            if config.parallel_slots < 1:
                raise ValueError("并行槽位必须大于 0")
            if config.n_gpu_layers < 0:
                raise ValueError("GPU 层数不能为负数")
            if config.provider not in ("local", "cloud"):
                raise ValueError("AI 服务提供方必须是 local 或 cloud")
        except Exception as exc:
            append_log("error", f"配置项解析失败: {exc}")
            return False

        # Reflect to dashboard
        host_value.value = config.host
        port_value.value = str(config.port)
        threads_value.value = str(config.threads)
        ctx_value.value = f"{config.context_size // 1024}K"
        return True

    def save_settings(_: ft.ControlEvent) -> None:
        if not commit_settings_from_ui():
            snack_msg = "配置项解析失败，未保存"
            try:
                page.open(ft.SnackBar(
                    content=ft.Text(snack_msg),
                    duration=2500,
                ))
            except Exception:
                page.snack_bar = ft.SnackBar(
                    content=ft.Text(snack_msg),
                    open=True, duration=2500,
                )
            page.update()
            return
        config.save()
        # Mirror to GTA5 mod's config.ini so NPCs pick up the new provider.
        mod_path = write_mod_config(config)
        if mod_path is not None:
            append_log("info", f"设置已保存 · mod config.ini → {mod_path}")
            snack_msg = f"设置已保存 · 同步到 {mod_path.name}"
        else:
            append_log("warn", "设置已保存（mod config.ini 写入失败，请检查权限）")
            snack_msg = "设置已保存（mod config.ini 写入失败）"
        refresh_status_card()
        try:
            page.open(ft.SnackBar(
                content=ft.Text(snack_msg),
                duration=2500,
            ))
        except Exception:
            page.snack_bar = ft.SnackBar(
                content=ft.Text(snack_msg),
                open=True, duration=2500,
            )
        page.update()

    settings_view = ft.Container(
        padding=_pad_xy(horizontal=24, vertical=20),
        content=ft.Column(
            [
                ft.Row(
                    [
                        ft.Text("设置", size=24, weight=ft.FontWeight.W_700),
                        ft.Container(expand=True),
                        ft.FilledButton(
                            content="保存",
                            icon=ft.Icons.SAVE_ROUNDED,
                            on_click=save_settings,
                        ),
                    ],
                    vertical_alignment=ft.CrossAxisAlignment.CENTER,
                ),
                ft.Container(height=4),
                section_card(
                    "服务器",
                    ft.Row(
                        [host_field, port_field, threads_field, threads_batch_field],
                        spacing=12, wrap=True,
                    ),
                    ft.Row(
                        [ctx_field, parallel_field, ngl_field],
                        spacing=12, wrap=True,
                    ),
                    ft.Row(
                        [use_mmap_sw, use_mlock_sw, cont_batch_sw, flash_attn_sw],
                        spacing=18, wrap=True,
                    ),
                    ft.Row([extra_field], spacing=12),
                ),
                section_card(
                    "AI 服务提供方",
                    ft.Text(
                        "选择 NPC 对话走本地推理还是第三方云端 API。点击 保存 后会自动写入 "
                        "GTA5MOD2026 mod 的 config.ini。",
                        size=12, color=ft.Colors.ON_SURFACE_VARIANT,
                    ),
                    provider_radio,
                    ft.Container(height=4),
                    cloud_section_container,
                ),
                section_card(
                    "推理参数",
                    temp_col, top_p_col, top_k_col, rep_pen_col, min_p_col,
                ),
            ],
            spacing=16,
            scroll=ft.ScrollMode.AUTO,
        ),
    )

    # ─── Playground (chat) view ────────────────────────────────────────────

    chat_history: List[ChatMessage] = []
    chat_cancel_event = {"event": None}  # type: dict

    chat_messages_list = ft.ListView(
        controls=[],
        spacing=10,
        padding=_pad_xy(horizontal=16, vertical=14),
        auto_scroll=True,
        expand=True,
    )

    system_prompt_field = ft.TextField(
        label="System Prompt（NPC 设定 / 角色卡）",
        value=DEFAULT_SYSTEM_PROMPT,
        multiline=True, min_lines=2, max_lines=6,
        expand=True, border_radius=10,
        text_size=12,
    )

    chat_input_field = ft.TextField(
        hint_text="给 NPC 发一条消息... (Ctrl+Enter 发送)",
        multiline=True, min_lines=1, max_lines=5,
        expand=True, border_radius=12,
        shift_enter=True,
        text_size=13,
    )

    chat_endpoint_label = ft.Text(
        "", size=11, color=ft.Colors.ON_SURFACE_VARIANT,
        font_family="Consolas", selectable=True,
    )
    chat_status_text = ft.Text(
        "就绪", size=11, color=ft.Colors.ON_SURFACE_VARIANT,
        weight=ft.FontWeight.W_500,
    )
    chat_status_dot = ft.Container(
        width=8, height=8, border_radius=4, bgcolor=STATUS_STOPPED,
    )

    def _make_bubble(role: str, text: str) -> ft.Row:
        is_user = (role == "user")
        bubble_text = ft.Text(
            text, size=13, selectable=True,
            color=(ft.Colors.ON_PRIMARY if is_user
                   else ft.Colors.ON_SURFACE),
        )
        bubble = ft.Container(
            content=bubble_text,
            padding=_pad_xy(horizontal=14, vertical=10),
            border_radius=ft.BorderRadius(
                top_left=14, top_right=14,
                bottom_left=(14 if is_user else 4),
                bottom_right=(4 if is_user else 14),
            ),
            bgcolor=(ft.Colors.PRIMARY if is_user
                     else ft.Colors.SURFACE_CONTAINER_HIGHEST),
            data=bubble_text,  # easy ref for streaming updates
        )
        avatar = ft.Container(
            width=28, height=28, border_radius=14,
            bgcolor=(ft.Colors.with_opacity(0.2, ft.Colors.PRIMARY)
                     if is_user else
                     ft.Colors.with_opacity(0.18, SEED_COLOR)),
            alignment=_align_center(),
            content=ft.Icon(
                ft.Icons.PERSON_ROUNDED if is_user
                else ft.Icons.AUTO_AWESOME_ROUNDED,
                size=15,
                color=(ft.Colors.PRIMARY if is_user else SEED_COLOR),
            ),
        )
        if is_user:
            row = ft.Row(
                [
                    ft.Container(expand=True),
                    ft.Container(content=bubble, padding=_pad_only(left=60)),
                    avatar,
                ],
                spacing=8,
                vertical_alignment=ft.CrossAxisAlignment.START,
            )
        else:
            row = ft.Row(
                [
                    avatar,
                    ft.Container(content=bubble, padding=_pad_only(right=60)),
                    ft.Container(expand=True),
                ],
                spacing=8,
                vertical_alignment=ft.CrossAxisAlignment.START,
            )
        return row

    def _set_chat_status(status: str, message: str) -> None:
        color_map = {
            "idle": STATUS_STOPPED,
            "sending": STATUS_WARNING,
            "streaming": STATUS_RUNNING,
            "error": STATUS_ERROR,
        }
        chat_status_dot.bgcolor = color_map.get(status, STATUS_STOPPED)
        chat_status_text.value = message
        # Use page.update() so updates from worker threads are flushed
        # to the Flutter UI immediately (element-level .update() can
        # silently batch when called cross-thread in flet 0.85).
        try:
            page.update()
        except Exception:
            pass

    def _resolve_endpoint() -> tuple:
        """Returns (endpoint_url, model_name, api_key) based on current config."""
        if config.provider == "cloud":
            ep = config.cloud_endpoint or ""
            model = config.cloud_model or "deepseek-chat"
            key = config.cloud_api_key or ""
        else:
            ep = f"http://{config.host}:{config.port}/v1/chat/completions"
            model = "sentience-v5"
            key = ""
        return ep, model, key

    def _refresh_chat_endpoint_label() -> None:
        ep, model, _ = _resolve_endpoint()
        mode_zh = "云端" if config.provider == "cloud" else "本地"
        chat_endpoint_label.value = f"[{mode_zh}] {model}  ·  {ep}"
        try:
            chat_endpoint_label.update()
        except Exception:
            pass

    def clear_chat(_e: Optional[ft.ControlEvent] = None) -> None:
        chat_history.clear()
        chat_messages_list.controls = []
        try:
            page.update()
        except Exception:
            pass
        _set_chat_status("idle", "已清空对话")

    def cancel_streaming() -> None:
        ev = chat_cancel_event.get("event")
        if ev is not None:
            ev.set()

    def send_chat(_e: Optional[ft.ControlEvent] = None) -> None:
        text = (chat_input_field.value or "").strip()
        if not text:
            return
        if not commit_settings_from_ui():  # ensure provider/cloud fields are fresh
            _set_chat_status("error", "配置项解析失败")
            return
        _refresh_chat_endpoint_label()
        ep, model, key = _resolve_endpoint()
        if not ep:
            _set_chat_status("error", "未配置 endpoint")
            return
        if config.provider == "cloud" and not key:
            _set_chat_status("error", "云端模式需要在 设置 中填入 API Key")
            return

        # Append user bubble
        user_msg = ChatMessage(role="user", content=text)
        chat_history.append(user_msg)
        chat_messages_list.controls.append(_make_bubble("user", text))

        # Reserve assistant bubble
        assistant_row = _make_bubble("assistant", "")
        assistant_bubble_text = assistant_row.controls[1].content.data
        chat_messages_list.controls.append(assistant_row)

        chat_input_field.value = ""
        try:
            page.update()
        except Exception:
            pass

        _set_chat_status("sending", "发送中...")

        # Build messages list including system prompt
        sys_prompt = (system_prompt_field.value or "").strip()
        msgs: List[ChatMessage] = []
        if sys_prompt:
            msgs.append(ChatMessage(role="system", content=sys_prompt))
        msgs.extend(chat_history)

        cancel_event = threading.Event()
        chat_cancel_event["event"] = cancel_event
        send_btn.disabled = True
        cancel_btn.disabled = False
        try:
            page.update()
        except Exception:
            pass

        accumulated = {"text": ""}
        first_token = {"received": False}
        last_flush = {"t": 0.0}

        def _flush_ui() -> None:
            try:
                page.update()
            except Exception:
                pass

        def on_delta(piece: str) -> None:
            def apply() -> None:
                accumulated["text"] += piece
                assistant_bubble_text.value = accumulated["text"]
                if not first_token["received"]:
                    first_token["received"] = True
                    # First token: also flips status, _set_chat_status flushes.
                    _set_chat_status("streaming", "生成中 · 流式")
                    last_flush["t"] = time.monotonic()
                    return
                # Throttle flushes to at most ~20 fps to avoid overwhelming flet.
                now = time.monotonic()
                if now - last_flush["t"] >= 0.05:
                    last_flush["t"] = now
                    _flush_ui()
            _run_on_ui_thread(apply)

        def on_done(full_text: str) -> None:
            def apply() -> None:
                chat_history.append(ChatMessage(role="assistant",
                                                content=full_text))
                send_btn.disabled = False
                cancel_btn.disabled = True
                # Final flush: ensure last delta + button state are visible.
                _flush_ui()
                _set_chat_status("idle", f"完成 · {len(full_text)} 字")
            _run_on_ui_thread(apply)

        def on_error(msg: str) -> None:
            def apply() -> None:
                err_text = f"[错误] {msg}"
                assistant_bubble_text.value = err_text
                assistant_bubble_text.color = ft.Colors.ERROR
                send_btn.disabled = False
                cancel_btn.disabled = True
                _flush_ui()
                _set_chat_status("error", "请求失败")
                append_log("error", f"chat: {msg}")
            _run_on_ui_thread(apply)

        req = ChatRequest(
            endpoint=ep,
            model=model,
            messages=msgs,
            api_key=key,
            temperature=config.temperature,
            top_p=config.top_p,
            max_tokens=512,
            stream=True,
        )
        stream_chat(req, on_delta, on_done, on_error, cancel_event)

    send_btn = ft.FilledButton(
        content=ft.Row(
            [
                ft.Icon(ft.Icons.SEND_ROUNDED, size=16),
                ft.Text("发送", size=13, weight=ft.FontWeight.W_600),
            ],
            spacing=6, tight=True,
        ),
        on_click=send_chat,
        height=44,
        style=ft.ButtonStyle(padding=_pad_xy(horizontal=18, vertical=10)),
    )
    cancel_btn = ft.OutlinedButton(
        content=ft.Row(
            [
                ft.Icon(ft.Icons.STOP_CIRCLE_OUTLINED, size=16),
                ft.Text("中断", size=13),
            ],
            spacing=6, tight=True,
        ),
        on_click=lambda _e: cancel_streaming(),
        height=44, disabled=True,
        style=ft.ButtonStyle(padding=_pad_xy(horizontal=14, vertical=10)),
    )
    clear_chat_btn = ft.OutlinedButton(
        content=ft.Row(
            [
                ft.Icon(ft.Icons.DELETE_OUTLINE_ROUNDED, size=16),
                ft.Text("清空", size=13),
            ],
            spacing=6, tight=True,
        ),
        on_click=clear_chat,
        height=44,
        style=ft.ButtonStyle(padding=_pad_xy(horizontal=14, vertical=10)),
    )

    playground_view = ft.Container(
        padding=_pad_xy(horizontal=24, vertical=20),
        content=ft.Column(
            [
                ft.Row(
                    [
                        ft.Text(
                            "操练场", size=24, weight=ft.FontWeight.W_700,
                        ),
                        ft.Container(width=14),
                        chat_status_dot,
                        chat_status_text,
                        ft.Container(expand=True),
                        clear_chat_btn,
                    ],
                    vertical_alignment=ft.CrossAxisAlignment.CENTER,
                ),
                ft.Row(
                    [
                        ft.Icon(ft.Icons.LINK_ROUNDED, size=14,
                                color=ft.Colors.ON_SURFACE_VARIANT),
                        chat_endpoint_label,
                    ],
                    spacing=6,
                ),
                ft.Container(height=4),
                ft.Container(
                    content=system_prompt_field,
                    padding=_pad_xy(horizontal=2, vertical=2),
                ),
                ft.Container(
                    expand=True,
                    border_radius=14,
                    bgcolor=ft.Colors.SURFACE_CONTAINER_LOW,
                    border=_border_all(1, ft.Colors.OUTLINE_VARIANT),
                    content=chat_messages_list,
                ),
                ft.Row(
                    [
                        chat_input_field,
                        cancel_btn,
                        send_btn,
                    ],
                    spacing=10,
                    vertical_alignment=ft.CrossAxisAlignment.END,
                ),
            ],
            spacing=10,
            expand=True,
        ),
        expand=True,
    )

    # ─── Logs view ──────────────────────────────────────────────────────────

    log_list_view = ft.ListView(
        controls=[],
        spacing=2,
        padding=_pad_xy(horizontal=14, vertical=10),
        auto_scroll=True,
        expand=True,
    )

    def clear_logs(_: ft.ControlEvent) -> None:
        log_entries.clear()
        log_list_view.controls = []
        log_list_view.update()

    def copy_logs(_: ft.ControlEvent) -> None:
        text_lines: List[str] = []
        for row in log_entries:
            try:
                ts_text = row.controls[0].value
                level_text = row.controls[1].content.value
                msg_text = row.controls[2].value
                text_lines.append(f"{ts_text} [{level_text}] {msg_text}")
            except Exception:
                continue
        page.set_clipboard("\n".join(text_lines))
        try:
            page.open(ft.SnackBar(
                content=ft.Text(f"已复制 {len(text_lines)} 行日志"),
                duration=2000,
            ))
        except Exception:
            page.snack_bar = ft.SnackBar(
                content=ft.Text(f"已复制 {len(text_lines)} 行日志"),
                open=True, duration=2000,
            )
        page.update()

    logs_view = ft.Container(
        padding=_pad_xy(horizontal=24, vertical=20),
        content=ft.Column(
            [
                ft.Row(
                    [
                        ft.Text("日志", size=24, weight=ft.FontWeight.W_700),
                        ft.Container(expand=True),
                        ft.OutlinedButton(
                            content="复制",
                            icon=ft.Icons.CONTENT_COPY_ROUNDED,
                            on_click=copy_logs,
                        ),
                        ft.OutlinedButton(
                            content="清空",
                            icon=ft.Icons.DELETE_OUTLINE_ROUNDED,
                            on_click=clear_logs,
                        ),
                    ],
                    vertical_alignment=ft.CrossAxisAlignment.CENTER,
                ),
                ft.Container(height=4),
                ft.Container(
                    expand=True,
                    border_radius=14,
                    bgcolor=ft.Colors.SURFACE_CONTAINER_LOW,
                    border=_border_all(1, ft.Colors.OUTLINE_VARIANT),
                    content=log_list_view,
                ),
            ],
            spacing=12,
            expand=True,
        ),
        expand=True,
    )

    # ─── About view ─────────────────────────────────────────────────────────

    def info_row(icon: str, title: str, value: str) -> ft.Row:
        return ft.Row(
            [
                ft.Icon(icon, size=18, color=ft.Colors.ON_SURFACE_VARIANT),
                ft.Text(title, size=13, weight=ft.FontWeight.W_500,
                        color=ft.Colors.ON_SURFACE_VARIANT, width=130),
                ft.Text(value, size=13, selectable=True),
            ],
            spacing=10,
        )

    # About logo: prefer the NexusV image, fall back to icon.
    logo_about_path = asset_path("NexusV_128.png")
    if Path(logo_about_path).exists():
        about_logo = ft.Image(
            src=logo_about_path, width=52, height=52,
            border_radius=14, fit=ft.BoxFit.COVER,
        )
    else:
        about_logo = ft.Container(
            content=ft.Icon(ft.Icons.AUTO_AWESOME_ROUNDED, size=28, color=SEED_COLOR),
            width=52, height=52,
            border_radius=14,
            bgcolor=ft.Colors.with_opacity(0.12, SEED_COLOR),
            alignment=_align_center(),
        )

    github_btn = ft.FilledTonalButton(
        content=ft.Row(
            [
                ft.Icon(ft.Icons.CODE_ROUNDED, size=18),
                ft.Text("GitHub 仓库", size=13, weight=ft.FontWeight.W_600),
            ],
            spacing=8, tight=True,
        ),
        on_click=lambda _e: open_url("https://github.com/NexusVAI/SENTIENCE"),
        height=40,
        style=ft.ButtonStyle(padding=_pad_xy(horizontal=16, vertical=8)),
    )
    homepage_btn = ft.FilledButton(
        content=ft.Row(
            [
                ft.Icon(ft.Icons.PUBLIC_ROUNDED, size=18),
                ft.Text("官网 nexusvai.xyz", size=13, weight=ft.FontWeight.W_600),
            ],
            spacing=8, tight=True,
        ),
        on_click=lambda _e: open_url("https://www.nexusvai.xyz"),
        height=40,
        style=ft.ButtonStyle(padding=_pad_xy(horizontal=16, vertical=8)),
    )

    about_view = ft.Container(
        padding=_pad_xy(horizontal=24, vertical=20),
        content=ft.Column(
            [
                ft.Text("关于", size=24, weight=ft.FontWeight.W_700),
                ft.Container(height=4),
                ft.Container(
                    padding=24,
                    border_radius=18,
                    bgcolor=ft.Colors.SURFACE_CONTAINER_LOW,
                    border=_border_all(1, ft.Colors.OUTLINE_VARIANT),
                    content=ft.Column(
                        [
                            ft.Row(
                                [
                                    about_logo,
                                    ft.Column(
                                        [
                                            ft.Text(APP_TITLE, size=18,
                                                    weight=ft.FontWeight.W_700),
                                            ft.Text(
                                                BRAND_TAGLINE, size=12,
                                                color=ft.Colors.ON_SURFACE_VARIANT,
                                            ),
                                        ],
                                        spacing=2,
                                    ),
                                    ft.Container(expand=True),
                                    homepage_btn,
                                    github_btn,
                                ],
                                spacing=14,
                                vertical_alignment=ft.CrossAxisAlignment.CENTER,
                            ),
                            ft.Container(height=14),
                            ft.Divider(height=1),
                            ft.Container(height=10),
                            info_row(ft.Icons.NUMBERS_ROUNDED, "版本",
                                     APP_VERSION),
                            info_row(ft.Icons.MODEL_TRAINING_ROUNDED, "模型",
                                     "Sentience V5 - Anima (Qwen3.5-2B Fine-tuned)"),
                            info_row(ft.Icons.FOLDER_ROUNDED, "数据根目录",
                                     str(APP_ROOT)),
                            info_row(ft.Icons.SETTINGS_APPLICATIONS_ROUNDED,
                                     "配置文件",
                                     "%APPDATA%/SentienceV5-Anima/launcher.json"),
                            info_row(ft.Icons.LINK_ROUNDED, "GitHub",
                                     "github.com/NexusVAI/SENTIENCE"),
                            info_row(ft.Icons.LANGUAGE_ROUNDED, "官网",
                                     "www.nexusvai.xyz"),
                            ft.Container(height=8),
                            ft.Text(
                                "传承自：Sentience Cogito (我思故我在) → "
                                "Sentience V4.1 Omni (全知) → "
                                "Sentience V5 Anima (灵魂)。",
                                size=12,
                                color=ft.Colors.ON_SURFACE_VARIANT,
                            ),
                        ],
                        spacing=4,
                    ),
                ),
            ],
            spacing=10,
            scroll=ft.ScrollMode.AUTO,
        ),
    )

    # ─── Navigation rail + body ─────────────────────────────────────────────

    body_holder = ft.Container(
        content=dashboard_view,
        expand=True,
        bgcolor=ft.Colors.SURFACE,
    )

    def goto(index: int) -> None:
        selected_view["index"] = index
        body_holder.content = [
            dashboard_view, playground_view, settings_view,
            logs_view, about_view,
        ][index]
        if index == 1:  # playground: refresh endpoint label
            _refresh_chat_endpoint_label()
        page.update()

    rail = ft.NavigationRail(
        selected_index=0,
        label_type=ft.NavigationRailLabelType.ALL,
        min_width=84,
        min_extended_width=200,
        bgcolor=ft.Colors.SURFACE_CONTAINER_LOW,
        leading=ft.Container(height=8),
        destinations=[
            ft.NavigationRailDestination(
                icon=ft.Icons.SPACE_DASHBOARD_OUTLINED,
                selected_icon=ft.Icons.SPACE_DASHBOARD_ROUNDED,
                label="仪表盘",
            ),
            ft.NavigationRailDestination(
                icon=ft.Icons.FORUM_OUTLINED,
                selected_icon=ft.Icons.FORUM_ROUNDED,
                label="操练场",
            ),
            ft.NavigationRailDestination(
                icon=ft.Icons.TUNE_ROUNDED,
                selected_icon=ft.Icons.TUNE_ROUNDED,
                label="设置",
            ),
            ft.NavigationRailDestination(
                icon=ft.Icons.RECEIPT_LONG_OUTLINED,
                selected_icon=ft.Icons.RECEIPT_LONG_ROUNDED,
                label="日志",
            ),
            ft.NavigationRailDestination(
                icon=ft.Icons.INFO_OUTLINE_ROUNDED,
                selected_icon=ft.Icons.INFO_ROUNDED,
                label="关于",
            ),
        ],
        on_change=lambda e: goto(int(e.control.selected_index)),
    )

    body = ft.Row(
        [rail, ft.VerticalDivider(width=1), body_holder],
        expand=True,
        spacing=0,
    )

    page.add(
        ft.Column(
            [app_bar, body],
            expand=True,
            spacing=0,
        )
    )

    # Initial scan + UI sync
    rescan_resources()
    refresh_status_card()
    refresh_voice_card()
    append_log(
        "info",
        f"Sentience V5 · Anima 已启动 · 数据根目录 {APP_ROOT}",
    )
    _voice_script = discover_voice_script(default_search_roots())
    if _voice_script is not None:
        append_log("info", f"已发现 voice_server.py · {_voice_script}")
    else:
        append_log(
            "warn",
            "未找到 voice_server.py — 语音 TTS 按钮可用但启动会失败。"
            "请把脚本放在 SentienceV5-Anima 根目录。",
        )

    # Uptime ticker
    def tick_uptime() -> None:
        while True:
            time.sleep(1)
            try:
                if server.is_running:
                    secs = int(server.uptime_seconds)
                    h, rem = divmod(secs, 3600)
                    m, s = divmod(rem, 60)
                    uptime_text.value = f"运行时长 · {h:02d}:{m:02d}:{s:02d}"
                else:
                    uptime_text.value = ""
                uptime_text.update()
            except Exception:
                pass

    threading.Thread(target=tick_uptime, daemon=True).start()


if __name__ == "__main__":
    # flet 0.80+ prefers ft.run; older versions use ft.app(target=...)
    if hasattr(ft, "run"):
        ft.run(main)
    else:
        ft.app(target=main)
