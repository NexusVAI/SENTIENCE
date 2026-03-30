# 🧠 SENTIENCE: AI NPCs for GTA V (Local & Offline)
      ╔══════════════════════════════════════════╗
      ║                                          ║
      ║   ███████╗███████╗███╗   ██╗████████╗    ║
      ║   ██╔════╝██╔════╝████╗  ██║╚══██╔══╝    ║
      ║   ███████╗█████╗  ██╔██╗ ██║   ██║       ║
      ║   ╚════██║██╔══╝  ██║╚██╗██║   ██║       ║
      ║   ███████║███████╗██║ ╚████║   ██║       ║
      ║   ╚══════╝╚══════╝╚═╝  ╚═══╝   ╚═╝       ║
      ║                                          ║
      ║        SENTIENCE v4Cogito Installer      ║
      ║                                          ║
      ╚══════════════════════════════════════════╝

<div align="center">

# ⚡ NEXUS V: SENTIENCE

### *What happens when NPCs start questioning their existence?*

<img src="https://img.shields.io/badge/GTA5-Enhanced-green?style=for-the-badge&logo=rockstargames&logoColor=white"/>
<img src="https://img.shields.io/badge/AI-Sentient%20NPCs-red?style=for-the-badge&logo=openai&logoColor=white"/>
<img src="https://img.shields.io/badge/GPU-GT%20730-yellow?style=for-the-badge&logo=nvidia&logoColor=white"/>
<img src="https://img.shields.io/badge/.NET-4.8-blue?style=for-the-badge&logo=dotnet&logoColor=white"/>
<img src="https://img.shields.io/badge/License-MIT-purple?style=for-the-badge"/>

---

**Solo developer. $30 GPU. NPCs that wake up.**

*Built on hardware people throw away.*
*Doing what AAA studios haven't.*

[Installation](#-installation) •
[Features](#-features) •
[The Awakening](#-the-awakening) •
[Configuration](#-configuration) •
[Architecture](#-architecture)

#  sentience V4Cogito

一个面向 GTA 5 的 C# 模组项目，聚焦 **NPC AI 行为系统 + 语音交互链路**，适合用于 AI 驱动 NPC 的玩法实验与模组开发学习。

## 功能特性

- NPC 行为系统：包含感知、状态、目标与决策相关模块
- 语音交互能力：支持本地 TTS 服务与游戏内语音播放
- 可配置模型链路：支持本地模型与云端模型的配置切换
- 模块化代码结构：便于二次开发与功能扩展


## 环境要求

| 组件 | 说明 |
|---|---|
| GTA 5（增强版，传承版） | 已安装并可正常运行 |
| ScriptHookV | GTA 模组基础依赖 |
| ScriptHookVDotNet3 | C# 脚本运行依赖（游戏目录） |
| .NET Framework | 4.8（项目目标框架 net48） |
| Visual Studio | 2022+（推荐 2026） |
| Python | 3.9+（用于语音服务） |

## 快速开始

### 1) 克隆仓库

```bash
git clone https://github.com/NexusVAI/SENTIENCE

```

### 2) 安装 Python 依赖（语音可选功能）

```bash
pip install edge-tts
```

### 3) 编译项目

```bash
dotnet build GTA5MOD2026/GTA5MOD2026.csproj -c Release
```

### 4) 部署 DLL 到游戏目录

将生成文件复制到 GTA 5 的 `scripts` 目录（目录不存在请手动创建）：

```text
GTA5MOD2026/bin/Release/net48/GTA5MOD2026.dll
```

### 5) 启动语音服务（如需语音）

在 `GTA5MOD2026/` 子目录中运行：
（注意，已经有.exe应用程序可以可视化操作了，脚本只做备用）
```bash
python voice_server.py
```

或双击运行：

```text
GTA5MOD2026/start_voice_server.bat
```

## 配置说明

首次运行会在以下路径生成配置文件：

```text
%USERPROFILE%/Documents/GTA5MOD2026/config.ini
```

主要配置分组：

- `[LLM]`：模型提供方、接口地址、模型名、云端密钥
- `[PERFORMANCE]`：Token 上限、温度、对话长度、请求冷却
- `[TTS]`：TTS 提供方、TTS 服务地址、语音开关
- `[STT]`：本地 Whisper 模型路径
- `[AWAKENING]`：唤醒系统开关与速度

示例（请使用你自己的配置值）：

```ini
[LLM]
Provider=local
LocalEndpoint=http://127.0.0.1:1234/v1/chat/completions
LocalModel=qwen2.5-3b-instruct
CloudEndpoint=https://api.deepseek.com/v1/chat/completions
CloudModel=deepseek-chat
CloudAPIKey=YOUR_API_KEY

[TTS]
TTSProvider=edge
TTSServer=http://127.0.0.1:5111
VoiceEnabled=true
```

## 使用说明

- 确保 ScriptHookV 与 ScriptHookVDotNet3 已正确安装到 GTA 5 目录
- 确保模组 DLL 位于 `scripts` 目录
- 若启用语音，先启动 `voice_server.py`，再进入游戏
- 在游戏中通过项目逻辑触发 NPC 对话与行为流程

## 项目结构

```text
GTA5MOD2026/
├─ GTA5MOD2026.slnx
├─ README.md
├─ GitHub开源仓库使用教程.md
├─ GTA5MOD2026/
│  ├─ GTA5MOD2026.csproj
│  ├─ NPCManager.cs
│  ├─ AIManager.cs
│  ├─ NPCBrain.cs
│  ├─ NPCGoalManager.cs
│  ├─ NPCPerception.cs
│  ├─ NPCState.cs
│  ├─ SpeechManager.cs
│  ├─ VoiceManager.cs
│  ├─ MemoryManager.cs
│  ├─ ModConfig.cs
│  ├─ voice_server.py
│  └─ start_voice_server.bat
└─ packages/
```

## 常见问题（FAQ）

### 1. 编译时报找不到 `ScriptHookVDotNet3.dll`

当前项目通过本地 `HintPath` 引用该 DLL。请在 `GTA5MOD2026.csproj` 中将 `HintPath` 改为你本机 GTA 5 目录下的实际路径。

### 2. 进游戏后脚本未加载

- 检查 DLL 是否在 GTA 5 的 `scripts` 目录
- 检查 ScriptHookV / ScriptHookVDotNet3 是否版本匹配
- 排查是否与其他脚本冲突

### 3. 语音没有声音

- 确认 `voice_server.py` 正在运行
- 确认 `config.ini` 中 `TTSServer` 与服务端口一致（默认 `5111`）
- 确认本地 Python 已安装 `edge-tts`

## Roadmap

- 优化 NPC 决策稳定性与行为多样性
- 完善语音识别与多角色音色策略
- 增加可观测性与调试面板
- 补充自动化测试与示例场景

## 贡献指南

欢迎通过 Issue 和 Pull Request 参与贡献：

1. Fork 本仓库
2. 创建特性分支（`feature/xxx`）
3. 提交修改并附带说明
4. 发起 Pull Request

建议保持单一主题提交，便于审查与回滚。

## 许可证

当前仓库尚未添加顶层开源许可证文件。建议在开源发布前补充 `LICENSE`（常见选项：MIT / Apache-2.0 / GPL-3.0）。

## 致谢

- ScriptHookVDotNet 社区生态
- GTA 5 Modding 社区与文档维护者

---

仅供学习与研究用途，请遵守所在地法律法规与游戏相关条款。


**NEXUS V: SENTIENCE**

Made with 🧠 and a GT 730
官网: https://nexusvai.github.io/NexusV/
</div>
