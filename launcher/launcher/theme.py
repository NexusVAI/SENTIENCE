"""Material Design 3 theme tokens for Sentience V5 Anima launcher."""
import flet as ft

APP_TITLE = "Sentience V5 · Anima"
APP_VERSION = "5.0.0-anima"
BRAND_TAGLINE = "我思 → 全知 → 灵魂"

# Material 3 seed color (Google Indigo, matches their official palette)
SEED_COLOR = "#4F46E5"

# Status colors (Material 3 tonal)
STATUS_RUNNING = "#10B981"   # emerald-500
STATUS_STOPPED = "#94A3B8"   # slate-400
STATUS_ERROR = "#EF4444"     # red-500
STATUS_WARNING = "#F59E0B"   # amber-500


def build_light_theme() -> ft.Theme:
    return ft.Theme(
        color_scheme_seed=SEED_COLOR,
        use_material3=True,
        visual_density=ft.VisualDensity.COMFORTABLE,
        page_transitions=ft.PageTransitionsTheme(
            android=ft.PageTransitionTheme.PREDICTIVE,
            ios=ft.PageTransitionTheme.CUPERTINO,
            windows=ft.PageTransitionTheme.FADE_UPWARDS,
        ),
    )


def build_dark_theme() -> ft.Theme:
    return ft.Theme(
        color_scheme_seed=SEED_COLOR,
        use_material3=True,
        visual_density=ft.VisualDensity.COMFORTABLE,
        page_transitions=ft.PageTransitionsTheme(
            android=ft.PageTransitionTheme.PREDICTIVE,
            ios=ft.PageTransitionTheme.CUPERTINO,
            windows=ft.PageTransitionTheme.FADE_UPWARDS,
        ),
    )
