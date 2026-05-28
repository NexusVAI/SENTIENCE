# 🔧 Sentience V5.1.1 · Animus Hotfix

> *"We heard you. We fixed it. Your voice is no longer ignored."*

**Tag**: `v5.1.1` · **Date**: 2026-05-28 · **Hotfix for V5.1 Animus**

---

## 📦 Downloads

| File | Size | Purpose |
|---|---|---|
| **`Sentience-V5.1-Animus.zip`** | ~1.38 GB | Full player release (LLM model + whisper-tiny + launcher + DLL) |
| **`Sentience-V5.1-Animus-GitHub.zip`** | ~470 KB | Source code only (for developers / forkers) |

> The release is ~180 MB bigger than V5.1.0 because **`whisper-tiny\`** (the offline STT model) is now **included in the box**. No more "go download this from HuggingFace and pray it fits." We got you.

---

## 🐛 What We Fixed

After V5.1.0 dropped, the community feedback was laser-focused on **one high-priority bug**:

> **"I press J to talk and literally nothing happens. It says it can't find whisper-tiny."**

We investigated. It wasn't one bug. It was a **triple combo of pain**:

1. **Hardcoded whisper-tiny path** — `SpeechManager` was only looking at `C:\whisper-tiny\`. If you didn't manually copy the model there, the mod just... gave up. Silent failure. No error. Just sadness.
2. **Hardcoded Python command** — The mod only tried `python`. If you installed Python via the Microsoft Store, or used `py`, or had it in a venv? "Python not found." That's it. Game over.
3. **whisper-tiny wasn't even in the release package** — V5.1.0 didn't ship the model. You had to hunt it down yourself. In 2026. Like animals.

**V5.1.1 ends this nonsense permanently.**

### Fixed · Voice Input End-to-End

- ✅ **Multi-path auto-discovery for whisper-tiny**: The mod now searches in this exact priority order:
  1. `config.ini` `[STT] WhisperModelPath` (explicit override — your choice, your rules)
  2. `%USERPROFILE%\Documents\GTA5MOD2026\whisper-tiny\` ← **recommended**
  3. `<GTA V>\scripts\whisper-tiny\` (drop-in next to the DLL)
  4. `<GTA V>\whisper-tiny\` (game root)
  5. `C:\whisper-tiny\` (legacy V5 path — we never abandon old users)

- ✅ **Python command auto-detection**: Tries `python`, then falls back to `py -3`. Uses the first one that actually responds. No more "did you add it to PATH?" gatekeeping.

- ✅ **Human-readable error messages**: If the model or Python is missing, the mod **immediately** tells you exactly what's wrong and exactly how to fix it — in plain language, not hex codes.

### Added · Release Packaging

- ✅ **`whisper-tiny\` (~75 MB) is now bundled in the release root.** Unzip. Play. Talk to NPCs. Done.
- ✅ **Packager (`PACKAGE_V5.1.ps1`)** now has idempotent whisper-tiny detection (same-size files are skipped automatically, so re-runs are instant).
- ✅ **Fixed a V5.1.0 packaging omission**: The `_internal\` folder (PyInstaller --onedir Python runtime, 3316 files / ~250 MB) was accidentally excluded from some builds, causing `Failed to load python313.dll` on launcher startup. It is now **mandatory** and auto-synced with the executable.

### Changed

- `ModConfig.STTConfig.WhisperModelPath` default changed from `"C:\whisper-tiny"` to `""` (empty = auto-discover).
- **Fully backward compatible**: If your existing `config.ini` has an explicit path, it will still be honored. Empty or legacy values fall back to the auto-discovery chain.
- `SpeechManager.RecordAndTranscribe` now uses the resolved `_pythonCmd` to spawn the STT subprocess, supporting both `python` and `py -3` invocation styles.

---

## 🆙 Upgrading from V5.1.0

> **Zero migration cost.** Pick your path:

### A. Full Clean Install (Recommended)

1. Close GTA V and the launcher.
2. Delete `<GTA V>\scripts\GTA5MOD2026.dll` (leave everything else — configs, plugins, scenarios).
3. Copy `Install_To_GTA_scripts\GTA5MOD2026.dll` from the new release over.
4. Copy `whisper-tiny\` from the release to `%USERPROFILE%\Documents\GTA5MOD2026\`.
5. Done. Your `config.ini` doesn't need changes. The launcher doesn't need changes.

### B. DLL-Only Swap (Minimal)

If you already have `whisper-tiny\` somewhere the auto-discovery chain can find it: literally just replace the DLL. That's it. The new discovery logic will pick up your existing model.

### C. Launcher Fix (Only if V5.1.0 launcher throws python313.dll errors)

Overwrite your local `_internal\` folder with the one from this release (~250 MB / 3316 files).

---

## 🔬 Internal Changes

- `SpeechManager.cs`: Added `ResolveWhisperModelPath()` and `ResolvePythonCommand()` static helpers. Results are cached at construction time — zero runtime overhead.
- **Zero intrusion into the main loop**: The call site `HandleVoiceInput → speechManager.RecordAndTranscribe(...)` is completely untouched. All fixes are internal to `SpeechManager`.
- **SDK / Plugin API / Scenarios / Archetypes are unchanged** — every V5.1.0 plugin remains 100% compatible. No recompilation needed.

---

## 🙏 Thank You

To every single player who reported the voice input bug: **you made this hotfix happen.**

The promise of V5.1 Animus is "shape the world." Your feedback is the first proof that this actually works — you spoke, we listened, and the world got better.

---

## 📜 Related Docs

- [CHANGELOG.md](CHANGELOG.md) — Full project history
- [README.md](README.md) — Installation, FAQ, configuration (includes V5.1.1 voice troubleshooting)
- [docs/Plugin-Development-Guide.md](docs/Plugin-Development-Guide.md) — Write your own plugins
- [ABOUT_LANGUAGE.md](ABOUT_LANGUAGE.md) — Why NPCs speak Chinese and our roadmap to English support

---

**Sentience V5.1.1 — Now NPCs can actually hear you.**

*Built for the community. Fixed by the community. Played by legends.*
