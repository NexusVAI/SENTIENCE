# Changelog

All notable changes to Sentience will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [5.0.0] — Anima — 2026-05-27

### Added

- **F5 LemonUI 配置菜单** — 游戏内可视化调节所有参数，即时生效、自动保存。
  - NPC 头顶标签：开关 / 缩放 / 风格（default·minimal·bold·cinematic）/ 颜色 / 性格配色 / 对话气泡
  - 回答显示：notification / subtitle / both 三种模式，字幕停留时长可调
  - HUD 交互提示：开关 / 缩放 / 四角位置 / 前景色·背景色 / 透明度 / 提示音
  - NPC 行为：响应半径 5–50m / 活跃度 0–100 / 主动开口 / 动作总开关 / attack·aim·call_cops 独立开关
  - 热键：F2–F12 可改
  - 快捷开关：TTS 语音 / 觉醒系统
- **自绘 HUD 系统** — 用 DRAW_RECT + DRAW_TEXT 替代 GTA 原生 HELP 队列，彻底修复死亡后交互菜单消失的 bug。
- **玩家死亡/被捕检测** — 死亡时自动清空 NPC 目标，重生后冲刷医院剧本残留、强制重绘 HUD。
- **行为门控** — `ActionsEnabled` 总开关可降级所有动作为 speak；`AllowAttack`/`AllowAim`/`AllowCallCops` 支持逐项禁用。
- **活跃度系统** — 0=完全被动（必须 G/H/T/J 触发），50=默认，100=~3 倍频率。
- **UIConfig + BehaviorConfig** — `config.ini` 新增 `[UI]` 和 `[Behavior]` 段落，完整 load/save。
- **LemonUI.SHVDN3.dll** — 随 mod 分发，F5 菜单的 UI 框架。

### Changed

- 启动器通知文案新增 "F5=菜单"。
- `FindNearestNPC` 改用 `Behavior.ResponseRadius` 替代硬编码距离。
- `ShowNPCResponse` 支持 notification / subtitle / both 三模式。
- `DrawNPCText` 支持风格切换 + 颜色覆盖 + 缩放倍率。
- `ExecuteLLMAction` 接入行为门控。

### Fixed

- **死亡后左上角交互菜单永久消失** — 根因是 GTA 医院剧本持续占用 DISPLAY_HELP 队列。改为自绘 HUD 彻底绕过。

---

## [4.1.0] — Omni — 2026-04-28

### Changed

- 适配最新 ScriptHookV。
- 启动器进一步自动化配置：选服务商 → 填 API Key → 完成。
- 一键启动 llama.cpp，CPU 线程数可调。
- 游戏内性能优化。
- 启动器 UI 优化。

---

## [4.0.0] — Omni Beta — 2026-04-17

### Added

- **专属微调模型** — 基于 Qwen3.5-2B 微调的 GTA5 对话模型，随包分发（1.31 GB）。
- **Sentience Launcher (AI_NPCS.exe)** — 全新 Material Design 3 启动器。
  - 内置 llama.cpp 推理引擎，开箱即用。
  - 一键启动服务器、一键安装 Mod。
  - 自动生成配置文件，零手动配置。
  - 支持自定义端口、线程数、上下文长度。
- 不再依赖 LM Studio，完全独立运行。
- 支持 GTA V Legacy 版和 Enhanced 版。
- 兼容 LM Studio（端口 1234）、KoboldCpp（端口 5001）、DeepSeek / OpenAI 云端 API。

### Removed

- LM Studio 强制依赖。

---

## [4.0.0-rc] — V4C — 2026-03-07

### Added

- **长期记忆系统** — NPC 会记住与玩家的交互历史。
- **实时语音增强** — TTS 质量与响应速度提升。
- **自我认知** — NPC 对自身身份和与玩家关系有持续认知。
- **长上下文记忆** — 支持更长的对话历史注入。

---

## [3.1.1] — 2026-03-03

### Fixed

- 修复游戏内 NPC 无响应的 bug（建议覆盖安装）。

---

## [3.1.0] — 2026-03-03

### Added

- **配置文件系统 (ModConfig.cs)** — 完整 INI 读写。
  - `[LLM]` — Provider / Endpoint / Model / APIKey
  - `[Performance]` — MaxTokens / Temperature / MaxDialogueLength
  - `[TTS]` — TTSProvider / TTSServer / VoiceEnabled
  - `[Awakening]` — Enabled / Speed
  - 首次运行自动生成 `%USERPROFILE%\Documents\GTA5MOD2026\config.ini`
- **云端 API 支持** — DeepSeek / OpenAI 兼容，自动处理 Authorization 头。
- **智能对话截断** — 基于 MaxDialogueLength 配置。
- **VoiceManager 重写** — 支持 ModConfig。
  - edge-tts 进程预热 (PreWarmEdgeTTS)，消除首次对话延迟。
  - TTS 服务器优先 + CLI 回退机制。
  - SSML 参数按情绪（Angry / Scared / Happy 等）动态调整。
  - NAudio 替代 PowerShell 播放音频，大幅降低延迟。
- **AwakenSystem 集成** — NPCManager 注入觉醒上下文。

---

## [3.0.0] — 2026-02-27

### Added

- **语音输入** — 按住 J 键通过麦克风说话（Whisper 离线 STT）。
- **文字输入** — 按 T 键使用 GTA 内置输入法打字。
- **互动菜单** — G（夸奖）/ H（侮辱）/ T（打字）/ J（语音）四键交互。
- **全 NPC 支持** — 作用于游戏世界中所有随机路人。
- **自动反应** — NPC 对枪械、靠近等行为做出回应。
- **一键安装脚本** — 简化部署流程。

---

[5.0.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v5.0.0
[4.1.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v4.1.0
[4.0.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v4.0.0
[4.0.0-rc]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v4c
[3.1.1]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v3.1.1
[3.1.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v3.1.0
[3.0.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v3.0.0
