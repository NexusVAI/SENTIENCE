# Changelog

All notable changes to Sentience are documented in this file.

Format loosely follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and we try to stick to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) — because breaking your plugins would be a jerk move.

---

## [5.1.1] — Animus Hotfix — 2026-05-28

> *"We tightened one screw. Voice input now works out of the box. No cap."*

### Fixed · Voice Input (STT) Actually Works Now

- **Multi-path auto-discovery for whisper-tiny** — Previously the mod only looked at `C:\whisper-tiny\`. If you didn't manually copy the model there, every voice input failed with `ERROR:no_speech`. The new version searches a priority chain:
  1. `config.ini` `[STT] WhisperModelPath` (explicit override)
  2. `%USERPROFILE%\Documents\GTA5MOD2026\whisper-tiny\`
  3. `<GTA V>\scripts\whisper-tiny\` (drop-in, next to the mod DLL)
  4. `<GTA V>\whisper-tiny\` (game root)
  5. `C:\whisper-tiny\` (legacy V5 default — we don't abandon old users)
- **Python command auto-discovery** — Previously hardcoded `FileName = "python"`. If Python wasn't in your PATH, you got an immediate failure. Now probes `python` → `py -3` in order and uses the first one that responds.
- **Short-circuit + human-readable errors** — Missing model or Python no longer hangs for 5 seconds. The mod immediately displays a localized error message telling you exactly what's wrong and how to fix it.

### Added · Release Packaging

- **`whisper-tiny/` (~75 MB STT model)** is now bundled directly in the release root. Unzip and play. No separate download required.
- **`PACKAGE_V5.1.ps1`** now includes multi-path whisper-tiny detection + idempotent copy (skips if same size, instant re-runs).
- **`PACKAGE_V5.1.ps1`** fixes a V5.1.0 omission: the `_internal/` folder (PyInstaller --onedir Python runtime, ~250 MB / 3316 files) was previously not always copied, causing `Failed to load python313.dll` on launcher startup. It is now mandatory and auto-synced with the executable.

### Changed

- `ModConfig.STTConfig.WhisperModelPath` default changed from `"C:\whisper-tiny"` to `""` (empty = let SpeechManager auto-discover). Existing `config.ini` files are **fully backward compatible**: explicit path → uses your path; empty/legacy → auto-discovery.
- `SpeechManager.RecordAndTranscribe` now uses the resolved `_pythonCmd` to launch the STT subprocess, supporting both `python` and `py -3` invocation styles.

### Internal

- `SpeechManager.cs`: Added `ResolveWhisperModelPath()` and `ResolvePythonCommand()` static helper methods. Resolved once at construction time and cached — zero runtime overhead.
- **Zero main-loop intrusion**: The call site `HandleVoiceInput → speechManager.RecordAndTranscribe(...)` is completely untouched. All changes are contained within `SpeechManager`.

---

## [5.1.0] — Animus — 2026-05-28

> *"From Soul to Will — Anima gave NPCs a soul. Animus gives them a shape that the world can reshape."*

### Added · Ecosystem Layer (LSPDFR-Class Plugin Ecosystem)

- **Sentience SDK 1.0** — Third-party developers can write C# plugins against a stable public interface. Located in `GTA5MOD2026.SDK` namespace. Once published, signatures never break:
  - `ISentiencePlugin` — Entry point (`Name / Author / Version / MinSdkVersion / OnLoad / OnUnload`)
  - `IPluginContext` — Service locator (`Events / Logger / ConfigSnapshot / PluginDataDirectory`)
  - `ISentienceEvents` — Event hub: `NPCSpawned`, `NPCDespawned`, `NPCRequestingAI`, `NPCDialogue`, `PlayerInteraction`, `PlayerWeaponChanged`
  - `INPCContext` — Read-only NPC snapshot (`UniqueId`, `PedHandle`, `Name`, `Personality`, `Archetype`, `ThreatLevel`, `Position`, etc.)
  - `ISentienceLogger` (`Debug / Info / Warn / Error`)
  - `SentienceSdk.Version = "1.0.0"`, `PluginFolderName = "SentiencePlugins"`, `ScenarioFolderName = "Scenarios"`

- **PluginLoader** — Assembly reflection over `scripts/SentiencePlugins/*.dll`:
  - SemVer compatibility check (`plugin.MinSdkVersion <= host SDK version`)
  - Fault quarantine (3 strikes per plugin, then auto-quarantine with `DropHandlersForAssembly`)
  - Clean unload with handler cleanup

- **SentienceEventHub** — Internal event dispatcher with fault isolation. If a plugin handler throws, the hub catches it, logs it, and removes the offending handler without crashing the mod.

- **SentienceServices** — Singleton bootstrap composing `ArchetypeRegistry + ScenarioLoader + PluginLoader`. `ComposeExtraSystemPrompt()` aggregates archetype + scenario + plugin contributions into the final system prompt.

- **Scenario Engine** — JSON-driven NPC behavior scripts:
  - Location: `%Documents%/GTA5MOD2026/Scenarios/*.json`
  - `TriggerType` enum: `Always`, `PedArchetype`, `PedModelHash`, `Hotkey`, `ZoneName`, `PlayerWeapon`
  - Schema: triggers, system prompt override, dialogue lines, actions, cooldowns

- **Archetype Voice System** — 30+ built-in archetypes with Chinese-localized personality prompts, edge-tts voice mappings, and walkstyle selectors:
  - `ArchetypeRegistry` loads built-ins + user INI overrides
  - `PedArchetypeMap` maps ped model hashes to archetype IDs
  - `WalkStyleSelector` assigns archetype-appropriate walkstyles on spawn

- **NPCManager Integration**:
  - Constructor: `SentienceServices.Initialize(aiManager.Config)` — null-safe, never throws
  - Spawn hook: archetype resolution + walkstyle application + `RaiseNpcSpawned`
  - Despawn hook: `RaiseNpcDespawned`
  - `OnTick`: cheap player weapon state polling (`PlayerWeaponChanged` only on transitions)
  - System prompt build: `ComposeExtraSystemPrompt` appends to the base prompt
  - Dialogue / interaction events raised at parse time

- **F5 Menu** (`SentienceMenu.cs`) — New "Plugins & Scenarios" submenu showing:
  - Loaded plugins (name, author, version)
  - Active scenarios (trigger conditions)
  - Archetype voice count and INI override path

- **Sample Plugin** (`Sentience.Plugins.PoliceRP`) — Demonstrates weapon-draw reaction logic:
  - On `PlayerWeaponChanged`: NPCs call cops or attack based on archetype aggressiveness
  - On `NPCRequestingAI`: injects extra system prompt context for police RP scenarios

- **Sample Scenarios** — JSON files:
  - `traffic_stop.json` — archetype-triggered traffic stop dialogue tree
  - `hostile_biker.json` — player-weapon-triggered street conflict

- **Sample Archetype Override** (`archetype_voices.ini`) — Example of user-defined archetypes with personality prompts, TTS voices, walkstyles, and ped hash overrides.

### Changed

- `NPCState` gained `string Archetype = ""` field for archetype tracking.
- `SentienceMenu.cs` reorganized to include the new plugin/scenario submenu without breaking existing navigation.

### Internal

- Build script (`BUILD.ps1`) expanded to 5 steps: mod → sample plugin → copy scenarios/INI → launcher → sync.
- Packaging script (`PACKAGE_V5.1.ps1`) handles `_internal/` and `whisper-tiny/` inclusion.
- SDK assemblies compile cleanly against `net48` with only pre-existing SHVDN3 deprecation warnings (non-blocking).

---

## [5.0.0] — Anima — 2026-05-20

> *"The Soul Update — NPCs stopped being cardboard cutouts and started remembering your name."*

### Added

- Full local LLM integration via llama.cpp (Qwen fine-tuned model)
- Long-term memory system (SQLite-backed NPC memory per save)
- Awakening system (NPC behavior permanently evolves through repeated interaction)
- TTS integration (edge-tts with gender detection)
- STT voice input (Whisper tiny, initially manual setup)
- F5 LemonUI configuration menu
- Material 3 desktop launcher (flet + PyInstaller)
- 16 NPC actions + 7 emotion states
- Overhead floating tags + subtitle system

### Changed

- Replaced cloud-based AI backend with fully offline local inference
- Reworked dialogue parser to `dialogue|action|emotion|intent` pipe format

---

## [4.1.0] — 2026-05-10

- Added NPC emotion visualization (overhead emojis)
- Improved threat response logic
- Weapon awareness system

## [4.0.0] — Omni — 2026-05-01

> *"The All-Knowing Update — The first stable release with persistent NPC state."*

- Persistent NPC state across game sessions
- Compliment/insult interaction system
- Basic action parser (`action|dialogue` format)

---

[5.1.1]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v5.1.1
[5.1.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v5.1.0
[5.0.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v5.0.0
[4.1.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v4.1.0
[4.0.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v4.0.0
