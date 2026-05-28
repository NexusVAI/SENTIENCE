# Changelog

All notable changes to Sentience will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [5.1.1] — Animus Hotfix — 2026-05-28

> *补一颗螺丝。语音输入终于能开箱即用。*

### Fixed · 语音输入 (STT) 端到端可用

- **Whisper 模型路径自动发现** —— 旧版只在 `C:\whisper-tiny\` 找模型，没手动复制的玩家全部 `ERROR:no_speech`。新版按优先级链查找：
  1. `config.ini` 的 `[STT] WhisperModelPath`（显式覆盖）
  2. `%USERPROFILE%\Documents\GTA5MOD2026\whisper-tiny\`
  3. `<GTA V>\scripts\whisper-tiny\`（drop-in 位置，跟 mod DLL 同级）
  4. `<GTA V>\whisper-tiny\`（游戏根目录）
  5. `C:\whisper-tiny\`（V5 历史默认，兼容老用户）
- **Python 命令自动发现** —— 旧版硬编码 `FileName = "python"`，没把 Python 加到 PATH 的玩家直接 `Python not found`。新版按 `python` → `py -3` 顺序探测，找到第一个可用的就用。
- **短路 + 友好错误消息** —— 找不到模型 / Python 时不再傻等 5 秒，立刻弹中文提示告诉玩家具体怎么修：
  - 缺模型：`"找不到 whisper-tiny 模型。请把 whisper-tiny 文件夹放到 %USERPROFILE%\Documents\GTA5MOD2026\ 下..."`
  - 缺 Python：`"找不到 Python。请安装 Python 3.10+ 并把它加入 PATH，然后执行: pip install sounddevice numpy faster-whisper"`

### Added · 发布包

- **`whisper-tiny/` 75MB STT 模型** 现在直接打进发布包根目录。玩家解压即用，不需要再去额外下载。
- **`PACKAGE_V5.1.ps1`** 加入 whisper-tiny 多路径检测 + idempotent 拷贝（同尺寸自动跳过）。
- **`PACKAGE_V5.1.ps1`** 修复一处遗漏：之前没拷 `_internal/`（PyInstaller --onedir 的 Python 运行时，250MB / 3316 文件），导致 launcher 双击报 `Failed to load python313.dll`。现在强制随 `SentienceV5.exe` 同步。

### Changed

- `ModConfig.STTConfig.WhisperModelPath` 默认值从 `"C:\whisper-tiny"` 改为 `""`（空 = 让 SpeechManager 自动找）。已存在的 config.ini **完全向后兼容**：写了显式路径 → 用你写的；为空 / 用旧默认 → 走自动发现。
- `SpeechManager.RecordAndTranscribe` 使用解析后的 `_pythonCmd` 启动子进程，支持 `python` 和 `py -3` 两种调用方式。

### Internal

- `SpeechManager.cs`：新增 `ResolveWhisperModelPath()` 和 `ResolvePythonCommand()` 两个静态辅助方法，构造函数一次性解析并缓存结果，运行时零开销。
- 主流程零侵入：`HandleVoiceInput → speechManager.RecordAndTranscribe(...)` 调用点完全没动，所有改动都在 SpeechManager 内部。

---

## [5.1.0] — Animus — 2026-05-28

> *从「灵魂」到「意志」—— Anima 让 NPC 有了魂，Animus 让他们有了「能被外人塑造」的形。*

### Added · 生态层（LSPDFR-Level Plugin Ecosystem）

- **Sentience SDK 1.0** — 第三方可基于稳定的公开接口写 C# 插件，全部位于 `GTA5MOD2026.SDK` 命名空间，签名一旦发布不再变更：
  - `ISentiencePlugin` — 插件入口（`Name / Author / Version / MinSdkVersion / OnLoad / OnUnload`）
  - `IPluginContext` — 服务定位器（`Events / Logger / ConfigSnapshot / PluginDataDirectory`）
  - `ISentienceEvents` — 事件中心：`NPCSpawned`、`NPCDespawned`、`NPCRequestingAI`、`NPCDialogue`、`PlayerInteraction`、`PlayerWeaponChanged`
  - `INPCContext` — 只读 NPC 快照（含 `Archetype` / `Personality` / `ThreatLevel` / `PlayerReputation` / `AwakenStage` 等）
- **PluginLoader** — 启动时自动扫描 `<GTA V>\scripts\SentiencePlugins\*.dll`，反射加载并 sandbox：
  - SemVer 兼容性检查（`MinSdkVersion` 高于 host 即拒绝加载）
  - 异常隔离：插件 `OnLoad` 抛出 → quarantine，不连累主 mod
  - 自动 fault 计数：同一插件连续 3 次事件抛异常即被禁用
  - 公共 `IPluginContext.Logger` 写入 `%Documents%\GTA5MOD2026\logs\plugins.log`，自动 2MB 轮转
- **Scenario 引擎** — 不会 C# 也能写 NPC 剧本，JSON 放到 `%Documents%\GTA5MOD2026\Scenarios\`：
  - 触发器类型：`always` / `ped_archetype` / `ped_model_hash` / `hotkey` / `zone_name` / `player_weapon`
  - 距离过滤、玩家是否驾车、玩家是否持枪可作为条件
  - `systemPromptAppend` 字段注入到 LLM system prompt
  - 自带示例：`traffic_stop.json`（交通盘查）、`hostile_biker.json`（机车党挑衅）
- **Archetype 声线库** — ped 模型 hash 自动映射到 30+ archetype，每个 archetype 提供：
  - 中文人格 prompt（注入 system message，让 NPC 行为更贴合身份）
  - edge-tts 男/女声短名（biker 用 `zh-CN-YunjianNeural`，hipster 用 `zh-CN-YunxiNeural` 等）
  - GTA 行走风格 pool（biker 走得霸气，business 走得正经，TTP3.1 同款）
  - 完全可在 `archetype_voices.ini` 中覆盖，含 `[PedHashOverrides]` 段
- **WalkStyleSelector** — NPC spawn 时按 archetype 随机指派 walkstyle，让街头不再千人一面。

### Added · 玩家可见

- **F5 菜单新增 "插件 & 场景" 子菜单** — 显示：
  - 已加载插件列表（状态 / 版本 / 作者 / 故障信息）
  - 已加载场景列表，每条可勾选启用/禁用，即时生效
  - Archetype 声线档案数 + 加载的 INI 路径
- **NPCState.Archetype 字段** — F6 调试信息可看到当前 NPC 的 archetype 归类。
- **示例插件 `Sentience.Plugins.PoliceRP`** — 演示玩家拔枪 → 附近平民优先 `call_cops` / `flee`，警察 archetype 反过来 `aim` / `attack`。源码 ~100 行，仅依赖公开 SDK。

### Changed

- 启动器和 F5 菜单的版本号 / 标题改为 `V5.1 · Animus`。
- `NPCManager` 在构造时初始化 `SentienceServices`，所有 SDK hook 通过它单点出入，主流程零侵入。
- LLM `system` prompt 构建末尾追加 archetype + scenario + plugin 三方贡献，顺序：archetype → scenario → plugin（先静态后动态）。
- `NPCManager` 在 `OnTick` 每帧检测玩家武器状态变化，触发 `PlayerWeaponChanged` 事件（一次性 enum 比较，性能可忽略）。

### Compatibility

- **SDK ABI 锁定**：`GTA5MOD2026.SDK.*` 1.0 接口此后只能新增，不能改签名。第三方插件可以放心针对 1.0 编译。
- 旧的 `config.ini` 完全向后兼容，无需迁移。
- 5.0 Anima 的功能（F5 LemonUI 菜单 / 自绘 HUD / 行为门控 / 活跃度 / 死亡处理）全部保留并继续生效。

### Internal

- 新文件：
  - `SDK/` — `ISentiencePlugin.cs`、`IPluginContext.cs`、`ISentienceLogger.cs`、`ISentienceEvents.cs`、`INPCContext.cs`、`SentienceSdk.cs`
  - `Plugins/` — `PluginLoader.cs`、`PluginContext.cs`、`SentienceEventHub.cs`、`SentienceLogger.cs`、`NPCContextSnapshot.cs`、`SentienceServices.cs`
  - `Plugins/Scenarios/` — `Scenario.cs`、`ScenarioLoader.cs`
  - `Voices/` — `Archetype.cs`、`ArchetypeRegistry.cs`、`BuiltinArchetypes.cs`、`PedArchetypeMap.cs`、`WalkStyleSelector.cs`
- 新增 `samples/` 子树：示例插件项目 + 两份示例场景 + 示例 archetype INI。
- 编译产物体积影响：mod DLL 约 +30KB（净增 ~1300 行 C#），无新外部依赖。

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

[5.1.1]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v5.1.1
[5.1.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v5.1.0
[5.0.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v5.0.0
[4.1.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v4.1.0
[4.0.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v4.0.0
[4.0.0-rc]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v4c
[3.1.1]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v3.1.1
[3.1.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v3.1.0
[3.0.0]: https://github.com/NexusVAI/SENTIENCE/releases/tag/v3.0.0
