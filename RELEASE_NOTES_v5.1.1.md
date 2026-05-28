# 🔧 Sentience V5.1.1 · Animus Hotfix

> *补一颗螺丝。语音输入终于能开箱即用。*

**Tag**：`v5.1.1` · **Date**：2026-05-28 · **Hotfix for V5.1 Animus**

---

## 📦 下载（Downloads）

| 文件 | 体积 | 用途 |
|---|---|---|
| **`Sentience-V5.1-Animus.zip`** | ~1.38 GB | 玩家完整发布版（含 LLM 模型 + whisper-tiny + 启动器 + DLL） |
| **`Sentience-V5.1-Animus-GitHub.zip`** | ~470 KB | 仅源码（开发者 / fork 用） |

> 体积比 V5.1.0 多 ~180 MB —— 多出来的是 `whisper-tiny\` 离线 STT 模型，**以后按 J 录音不用自己额外下载了**。

---

## 🐛 修复了什么

V5.1.0 发布后社群反馈集中在**一个高优先级 bug**：

> **"按 J 录音没反应 / 提示找不到 whisper-tiny。"**

排查后发现是三连问题：

1. **whisper-tiny 模型路径硬编码** —— `SpeechManager` 旧版只在 `C:\whisper-tiny\` 找模型，没手动复制的玩家全部失败。
2. **Python 命令硬编码** —— 旧版只调 `python`，没把 Python 加到 PATH 的玩家直接报 "Python not found"。
3. **whisper-tiny 没打进发布包** —— V5.1.0 发布包里**没有** `whisper-tiny\` 目录，玩家得自己去 HuggingFace 下载。

V5.1.1 一次性根治：

### Fixed · 语音输入端到端

- ✅ **多路径自动发现 whisper-tiny**：按下面顺序找，找到就用：
  1. `config.ini` 的 `[STT] WhisperModelPath`（显式覆盖）
  2. `%USERPROFILE%\Documents\GTA5MOD2026\whisper-tiny\` ← **推荐**
  3. `<GTA V>\scripts\whisper-tiny\`（drop-in 位置）
  4. `<GTA V>\whisper-tiny\`
  5. `C:\whisper-tiny\`（V5 老路径，兼容老用户）
- ✅ **Python 命令自动探测**：按 `python` → `py -3` 顺序找，找到第一个可用的就用。
- ✅ **友好中文错误提示**：缺模型 / 缺 Python 时立刻弹出具体修复指令（含路径、含 `pip install` 命令）。

### Added · 发布包

- ✅ **`whisper-tiny\` (~75 MB) 现在直接打进发布包根目录** —— 解压即用。
- ✅ Packager (`PACKAGE_V5.1.ps1`) 加入 idempotent whisper-tiny 检测（同尺寸自动跳过）。
- ✅ Packager 同时修复 V5.1.0 的另一处遗漏：之前没拷启动器的 `_internal\`（PyInstaller --onedir 的 Python 运行时，3316 文件 / ~250 MB），导致部分玩家双击 `SentienceV5.exe` 报 `Failed to load python313.dll`。现在强制随 exe 同步。

### Changed

- `ModConfig.STTConfig.WhisperModelPath` 默认值从 `"C:\whisper-tiny"` 改为 `""`（空 = 自动找）。
- **完全向后兼容**：已存在的 `config.ini` 写了显式路径 → 用你写的；为空 / 用旧默认 → 走自动发现。
- `SpeechManager.RecordAndTranscribe` 使用解析后的 `_pythonCmd` 启动子进程，支持 `python` 和 `py -3` 两种调用方式。

---

## 🆙 从 V5.1.0 升级

> **零迁移成本。** 三种升级方式任选其一：

### A. 完整覆盖（推荐 / 最干净）

1. 关游戏 + 启动器
2. 删除 `<GTA V>\scripts\GTA5MOD2026.dll`（其他 DLL / 配置不要动）
3. 把 V5.1.1 发布包里的 `Install_To_GTA_scripts\GTA5MOD2026.dll` 覆盖过去
4. 把发布包里的 `whisper-tiny\` 整个拷到 `%USERPROFILE%\Documents\GTA5MOD2026\` 下
5. 完事。`config.ini` 不需要改，启动器不需要换。

### B. 只换 DLL

如果你早就把 `whisper-tiny\` 放在 `C:\whisper-tiny\` 或者别的位置：直接只换 `GTA5MOD2026.dll` 就行，自动发现链会兜底。

### C. 强制覆盖启动器（仅 V5.1.0 启动器双击报 python313.dll 错的玩家）

把发布包里的 `_internal\` 整个覆盖你本地 `_internal\`（约 250 MB / 3316 文件）。

---

## 🔬 内部改动

- `SpeechManager.cs`：新增 `ResolveWhisperModelPath()` 和 `ResolvePythonCommand()` 两个静态辅助，构造函数一次性解析并缓存结果，运行时零开销。
- 主流程零侵入：`HandleVoiceInput → speechManager.RecordAndTranscribe(...)` 调用点完全没动，所有修复在 SpeechManager 内部。
- SDK / 插件 API / 场景 / 原型语音 **零变动**——V5.1.0 的所有插件 100% 继续可用，不需要重编。

---

## 🙏 致谢

感谢第一时间反馈语音 bug 的所有玩家。V5.1 Animus 的「能被外人塑造」的承诺，从这次 hotfix 真正兑现：你们的反馈直接变成了下一个 release。

---

## 📜 相关文档

- [CHANGELOG.md](CHANGELOG.md) — 完整变更历史
- [README.md](README.md) — 安装 / FAQ / 配置（FAQ 已加 V5.1.1 语音故障排查条目）
- [docs/Plugin-Development-Guide.md](docs/Plugin-Development-Guide.md) — 插件开发指南

---

**Sentience V5.1.1 — 让 NPC 听得见你说的话。**
