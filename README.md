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
      ║     SENTIENCE v4 OmniBata Installer      ║
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

# 🧠 SENTIENCE: AI NPCs for GTA V

<div align="center">

# ⚡ NEXUS V: SENTIENCE Omni

### *What happens when NPCs start questioning their existence?*

[![GTA5](https://img.shields.io/badge/GTA5-Enhanced-green?style=for-the-badge&logo=rockstargames&logoColor=white)]()
[![AI-Sentient](https://img.shields.io/badge/AI-Local%20LLM-red?style=for-the-badge&logo=openai&logoColor=white)]()
[![License](https://img.shields.io/badge/License-MIT-purple?style=for-the-badge)]()

**Solo developer. $30 GPU. NPCs that wake up.**

*Built on hardware people throw away.*
*Doing what AAA studios haven't.*

</div>

---

## ✨ 新版本特性 (v4 Omni)

### 🖥️ AI Launcher - 一键启动的 AI 服务端

- **内置 llama.cpp 推理服务器**：无需额外配置，接管即用
- **可视化界面**：点击启动，自动管理模型和服务器
- **多模型支持**：本地模型（CPU/GPU）或云端 API（DeepSeek/OpenAI）
- **智能路径处理**：自动检测模型位置，跨平台分发无忧

### 🎮 GTA5 Mod - 游戏内的 AI NPC

- **NPC 觉醒系统**：NPC 拥有自主意识和行为决策
- **实时语音交互**：TTS 语音合成 + STT 语音识别
- **行为记忆系统**：NPC 记住与玩家的交互历史
- **感知引擎**：NPC 能感知周围环境和玩家行为

---

## 🏗️ 系统架构

```
┌─────────────────────────────────────────────────────────────┐
│                      AI NPCS Launcher                        │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────────┐  │
│  │ GUI 控制面板 │  │ LlamaServer  │  │   Config 管理器    │  │
│  └─────────────┘  └──────────────┘  └───────────────────┘  │
│         │                 │                    │            │
│         └───────────────┬─┴────────────────────┘            │
│                         │                                   │
│              ┌──────────▼──────────┐                        │
│              │  OpenAI Compatible │                        │
│              │   HTTP API :8080   │                        │
│              └──────────┬──────────┘                        │
└─────────────────────────┼───────────────────────────────────┘
                          │
              ┌───────────▼───────────┐
              │    GTA5 Mod (C#)     │
              │  ┌─────────────────┐  │
              │  │ NPC Brain/State │  │
              │  │ Voice Manager   │  │
              │  │ Memory System  │  │
              │  └─────────────────┘  │
              └───────────────────────┘
```

**两种运行模式：**

| 模式 | 配置 | 优点 |
|------|------|------|
| **本地模型** | 使用内置 llama.cpp | 离线可用，零 API 成本 |
| **云端 API** | DeepSeek/OpenAI | 更强模型，更低延迟 |

---

## 📦 快速安装

###方式一：一键安装器（推荐）

1. 下载 `OmniO_v4.1_Setup.exe`
2. 运行安装程序，选择安装路径
3. 启动 **AI NPCS.exe**
4. 点击「启动推理服务器」
5. 进入 GTA5 游戏

###方式二：手动部署

#### 1) 克隆仓库

```bash
git clone https://github.com/NexusVAI/SENTIENCE.git
```

#### 2) 部署 GTA5 Mod

```bash
# 编译 Mod
cd SENTIENCE
dotnet build GTA5MOD2026/GTA5MOD2026.csproj -c Release

# 复制到 GTA5 scripts 目录
copy GTA5MOD2026\bin\Release\net48\GTA5MOD2026.dll "C:\Program Files\Rockstar Games\GTA V\scripts\"
```

#### 3) 部署 AI Launcher

```bash
# 编译 Launcher
dotnet build AI NPCS/AI NPCS.csproj -c Release -o publish

# 或直接使用 publish 目录中的 AI_NPCS.exe
```

---

## ⚙️ 配置说明

首次运行会在以下路径生成配置文件：

```
%USERPROFILE%\Documents\GTA5MOD2026\config.ini
```

### 主要配置项

```ini
[LLM]
# 使用本地模型（内置 llama.cpp）
Provider=local
LocalEndpoint=http://127.0.0.1:8080/v1/chat/completions

# 或使用云端 API
# Provider=cloud
# CloudEndpoint=https://api.deepseek.com/v1/chat/completions
# CloudModel=deepseek-chat
# CloudAPIKey=your_api_key

[Performance]
MaxTokens=80
Temperature=0.7
MaxDialogueLength=45

[TTS]
TTSProvider=edge
VoiceEnabled=true

[Awakening]
Enabled=true
Speed=2
```

---

## 🔧 项目结构

```
SENTIENCE/
├── GTA5MOD2026/                # GTA5 Mod 源码
│   ├── GTA5MOD2026.csproj
│   ├── NPCManager.cs           # NPC 管理器
│   ├── NPCBrain.cs             # NPC 思维逻辑
│   ├── VoiceManager.cs         # 语音管理
│   ├── MemoryManager.cs        # 记忆系统
│   └── ...
│
└── README.md
```

---

## 💡 使用指南

### 启动流程

1. 运行 `AI_NPCS.exe`
2. 点击「🧠 启动推理服务器」
3. 等待日志显示「推理服务器就绪」
4. 启动 GTA5 游戏
5. 开始与 NPC 对话！

### 游戏内操作

- NPC 会自动与玩家进行语音对话
- NPC 具有记忆功能，记得之前的交互
- 可通过配置调整 NPC 的「觉醒速度」

---

## 🐛 常见问题

### 1. 推理服务器启动失败

- 检查 `启动项` 目录下是否有 `gta5_2b_q4km.gguf` 模型文件
- 检查端口 8080 是否被占用：`netstat -ano | findstr 8080`

### 2. Mod 未加载

- 确认 ScriptHookV 和 ScriptHookVDotNet3 已安装
- 确认 `GTA5MOD2026.dll` 在 GTA5 的 `scripts` 目录

### 3. 语音无声音

- 确认 `voice_server.py` 正在运行（或使用 Launcher 的内置 TTS）
- 检查 `config.ini` 中 `VoiceEnabled=true`

---

## 🗺️ Roadmap

- [ ] 多 NPC 同时对话支持
- [ ] NPC 性格自定义系统
- [ ] 对话记录导出与分析
- [ ] 移动端远程控制面板
- [ ] 更多 TTS 语音包

---

## 📄 许可证

MIT License - 可自由使用、修改和分发

---

## 🙏 致谢

- [llama.cpp](https://github.com/ggerganov/llama.cpp) - 高效本地推理
- [ScriptHookVDotNet3](https://github.com/scripthookvdotnet/scripthookvdotnet) - GTA5 Mod 开发框架
- [Qwen](https://github.com/QwenLM) - 本地模型支持

---

<div align="center">

**NEXUS V: SENTIENCE**

*Made with 🧠 and a GT 730*

[官网](https://nexusvai.github.io/NexusV/) | [GitHub](https://github.com/NexusVAI/SENTIENCE)

</div>
