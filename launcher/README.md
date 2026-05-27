<div align="center">

# 🧠 Sentience V5 · Anima

**第二代自研微调 GTA5 NPC AI Mod**
**Self-aware GTA5 NPCs powered by a fine-tuned local LLM**

> *我思 (Cogito) → 全知 (Omni) → **灵魂 (Anima)***

[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)](https://github.com/NexusVAI/SENTIENCE)
[![GTA V](https://img.shields.io/badge/GTA%20V-Steam%20%7C%20Epic%20%7C%20Rockstar-orange)](https://github.com/NexusVAI/SENTIENCE)
[![.NET](https://img.shields.io/badge/.NET-Framework%204.8-purple)](https://github.com/NexusVAI/SENTIENCE)
[![Python](https://img.shields.io/badge/python-3.10%2B-yellow)](https://github.com/NexusVAI/SENTIENCE)
[![License](https://img.shields.io/badge/license-MIT-green)](https://github.com/NexusVAI/SENTIENCE)
[![Made with](https://img.shields.io/badge/made%20with-llama.cpp%20%C3%97%20Qwen3.5-red)](https://github.com/NexusVAI/SENTIENCE)

![Banner](NexusV.jpg)

</div>

---

## 📚 目录 / Table of Contents

- [📖 这是什么 / What is this](#-这是什么--what-is-this)
- [✨ 核心特性 / Features](#-核心特性--features)
- [🖥️ 系统要求 / System Requirements](#️-系统要求--system-requirements)
- [🎮 玩家安装指南（详细）/ Player Install Guide](#-玩家安装指南详细--player-install-guide)
  - [① 准备 GTA V 与 mod 前置](#-准备-gta-v-与-mod-前置)
  - [② 下载发布包](#-下载发布包)
  - [③ 复制 mod DLL 到 scripts](#-复制-mod-dll-到-scripts)
  - [④ 检查模型文件位置](#-检查模型文件位置)
  - [⑤ 第一次启动](#-第一次启动)
  - [⑥ 进游戏后的操作](#-进游戏后的操作)
- [⌨️ 热键 & F5 菜单](#️-热键--f5-菜单)
- [⚙️ 配置文件说明](#️-配置文件说明)
- [👩‍💻 开发者指南](#-开发者指南)
  - [仓库结构](#仓库结构)
  - [前置开发环境](#前置开发环境)
  - [构建 mod (C# / .NET)](#构建-mod-c--net)
  - [构建启动器 (Python / PyInstaller)](#构建启动器-python--pyinstaller)
  - [一键构建脚本](#一键构建脚本)
  - [开发模式（无需打 exe）](#开发模式无需打-exe)
- [🧠 模型说明](#-模型说明)
- [🔌 自定义后端 / 接入云端 API](#-自定义后端--接入云端-api)
- [🐛 常见问题 (FAQ)](#-常见问题-faq)
- [🤝 贡献](#-贡献)
- [📜 License](#-license)
- [🙏 致谢 / Credits](#-致谢--credits)

---

## 📖 这是什么 / What is this

**Sentience V5 · Anima** 让 GTA V 里的随机路人拥有真正的"灵魂"——他们能：

- 用**自然语言**和你对话（中文为主，支持掺杂英文/俚语）
- 根据你说的话**做出动作**（attack、flee、hug、phone_call、…16 种）
- 表现出**情绪**（happy/angry/sad/fear/surprise/disgust/neutral）
- 拥有**长期记忆**和**人格觉醒系统**——你越是经常打交道，他越会"醒过来"
- 配上**实时 TTS 语音**朗读（中文 edge-tts，无需联网到云端）

> ⚙️ 所有 AI 推理都在**你自己的电脑本地**完成。模型只有 1.27 GB，CPU 也能跑。

---

## ✨ 核心特性 / Features

| 类别 | 说明 |
|---|---|
| 🤖 **本地 LLM** | 基于 Qwen3.5-2B 自研微调，q4_k_m 量化（1.27 GB），CPU/GPU 都能跑 |
| 🎭 **16 种动作 + 7 种情绪** | attack / flee / hug / point / call_cops / smoke / dance / … |
| 🗣️ **TTS 语音** | edge-tts 中文情绪语音，男女声自动按 NPC 性别选择 |
| 🎤 **STT 语音输入**（可选） | whisper-tiny，按 `J` 录音对 NPC 说话 |
| 🧠 **长期记忆 + 觉醒** | NPC 会记住你，长期交互会"觉醒"并改变行为 |
| 🎨 **F5 LemonUI 菜单** | 头顶标签 / HUD / 字幕 / 行为 全部可视化调节 |
| 📱 **Material 3 启动器** | flet 桌面 UI，亮/暗主题，一键启动后端 |
| 🔌 **可插拔后端** | 默认本地 llama.cpp，可一键切到 DeepSeek / OpenAI 兼容 API |
| 🌐 **完全离线可用** | 安装好后无需任何网络连接 |

---

## 🖥️ 系统要求 / System Requirements

**最低配置：**

| 项 | 要求 |
|---|---|
| 操作系统 | Windows 10 (1809+) 或 Windows 11 |
| GTA V 版本 | Steam / Epic / Rockstar 任一正版，最新更新均兼容 |
| 内存 | 8 GB RAM（推理时会占 ~2 GB） |
| 硬盘 | 3 GB 可用空间 |
| .NET | .NET Framework 4.8（Win10/11 自带） |

**推荐配置：**

| 项 | 要求 |
|---|---|
| CPU | 6 核以上 / AVX2 支持（Intel 6 代后 / AMD Ryzen 全系） |
| 内存 | 16 GB RAM |
| GPU | NVIDIA 显卡（可选，能用 CUDA 加速到 3-5 倍速度） |
| 麦克风 | 仅当启用 STT 语音输入功能 |

---

## 🎮 玩家安装指南（详细）/ Player Install Guide

> 第一次装 GTA5 mod？别担心，跟着下面做，**全程不需要会编程**。

### ① 准备 GTA V 与 mod 前置

GTA5 mod 都需要两个底层组件，**只装一次，以后所有 mod 都共用**：

1. **ScriptHookV** —— 让 GTA5 能加载脚本。
   - 下载：<http://www.dev-c.com/gtav/scripthookv/>
   - 把压缩包里的 `ScriptHookV.dll`、`dinput8.dll`、`NativeTrainer.asi` 拷到 GTA V **根目录**（和 `GTA5.exe` 同一个文件夹）。

2. **ScriptHookVDotNet (SHVDN3 v3.6+)** —— 让 GTA5 能加载 C# 写的 mod。
   - 下载：<https://github.com/scripthookvdotnet/scripthookvdotnet/releases>
   - 解压后把 `ScriptHookVDotNet.asi`、`ScriptHookVDotNet3.dll`、`ScriptHookVDotNet3.xml` 也拷到 GTA V 根目录。

3. **建立 `scripts` 文件夹**：在 GTA V 根目录里**手动新建一个**叫 `scripts` 的文件夹（如果还没有的话）。所有 C# mod 都放在这里。

> 📍 GTA V 根目录在哪？
>
> - **Steam 版**：右键库里 GTA V → 管理 → 浏览本地文件
> - **Epic 版**：右键库里 GTA V → 管理 → 安装 → 文件夹图标
> - **Rockstar 版**：通常是 `C:\Program Files\Rockstar Games\Grand Theft Auto V\`

完成后，GTA V 根目录应该长这样：

```
Grand Theft Auto V/
├── GTA5.exe
├── ScriptHookV.dll               ← ① 装的
├── dinput8.dll                   ← ① 装的
├── ScriptHookVDotNet.asi         ← ② 装的
├── ScriptHookVDotNet3.dll        ← ② 装的
├── ScriptHookVDotNet3.xml        ← ② 装的
├── scripts/                      ← ③ 你手动建的
└── ...
```

### ② 下载发布包

从 [Releases 页面](https://github.com/NexusVAI/SENTIENCE/releases) 下载 `SentienceV5-Anima.zip`，解压到**任意位置**（推荐 `D:\SentienceV5-Anima\`，**不要放 C:\Program Files\**，权限会很麻烦）。

解压后应该看到：

```
SentienceV5-Anima/
├── SentienceV5.exe              ← 启动器（双击就能跑）
├── ModFiles/                    ← mod 的 DLL，要拷到 GTA5\scripts\
├── Runtime/
│   ├── llama.cpp/               ← LLM 推理后端
│   └── gta5_npc_v2_q4km.gguf    ← AI 模型（1.27 GB）
├── _internal/                   ← 启动器运行时（别动）
├── voice_server.py              ← 语音服务（启动器自动调用）
├── config.reference.ini         ← 配置说明
├── NexusV.jpg
└── README.md                    ← 本文档
```

### ③ 复制 mod DLL 到 scripts

把 `ModFiles\` 里的**所有文件**（不是文件夹本身）复制到刚才第 ① 步建的 `<GTA V>\scripts\` 里。

需要拷的文件：

| 文件 | 用途 |
|---|---|
| `GTA5MOD2026.dll` | mod 主体 |
| `LemonUI.SHVDN3.dll` | F5 菜单的 UI 框架（**必须放在一起**，否则不显示菜单） |
| `Newtonsoft.Json.dll` | JSON 解析 |
| `NAudio.dll` `NAudio.Core.dll` `NAudio.Wasapi.dll` `NAudio.WinMM.dll` | 音频播放（TTS 语音用） |

完成后 `<GTA V>\scripts\` 里应该有 7 个 `.dll` 文件。

### ④ 检查模型文件位置

模型 `gta5_npc_v2_q4km.gguf`（1.27 GB）默认就在发布包的 `Runtime\` 里，**不需要动**。启动器会自动扫描。

> 💡 如果你想把模型放别处节省空间，**也可以**放到 `%USERPROFILE%\Documents\GTA5MOD2026\models\`（启动器会自动扫描这个目录）。

### ⑤ 第一次启动

1. **双击 `SentienceV5.exe`** —— 启动器界面出现。
2. 检查仪表盘上 4 项：
   - **引擎**：自动识别到 `Runtime\llama.cpp\llama-server.exe` ✅
   - **模型**：自动识别到 `gta5_npc_v2_q4km` ✅
   - **TTS 语音服务**：状态显示"已停止"——**点 `启动 TTS`** 按钮开启
   - **后端**：状态显示"未启动"——**点 `启动` 按钮**开启
3. 日志里出现 `HTTP server listening` 后，最小化启动器（**不要关闭**），启动 GTA5。
4. 进游戏后，左上角应该会弹出蓝色提示：

   ```
   Nexus V: Sentience  V5 Anima
   G=夸奖 H=侮辱 T=打字 J=语音
   F5=菜单 F8=语音开关 F6=状态 F9=保存记忆
   ```

   看到这条 = 装好了 🎉

### ⑥ 进游戏后的操作

| 按键 | 功能 |
|---|---|
| `F5` | **打开/关闭设置菜单**（重点！全部可视化调节都在这） |
| `G` | 夸奖最近的 NPC |
| `H` | 侮辱最近的 NPC |
| `T` | **打字**输入自定义对话 |
| `J` | **按住录音**对 NPC 说话（需配置 STT，可选） |
| `F6` | 显示当前 NPC 状态调试信息 |
| `F8` | 临时开关 TTS 语音 |
| `F9` | 立即保存所有 NPC 记忆 |

走到任意 NPC 附近（默认 8 米内），左上角会出现交互提示，NPC 头顶会出现名字标签。然后按 G/H/T/J 互动即可。

---

## ⌨️ 热键 & F5 菜单

按 **F5** 打开 LemonUI 设置菜单。所有改动**即时生效并自动保存**到 `%USERPROFILE%\Documents\GTA5MOD2026\config.ini`。

### 菜单结构

```
Sentience · V5 Anima 控制台
├── NPC 头顶绘制
│   ├── 启用悬浮标签            [✓/✗]
│   ├── 缩放                     [0.5x – 2.0x]
│   ├── 风格                     [default / minimal / bold / cinematic]
│   ├── 颜色预设                 [白 / 金 / 青 / 粉 / 红 / 绿 / 紫]
│   ├── 按性格自动配色            [✓/✗]
│   └── 显示对话气泡             [✓/✗]
│
├── NPC 回答显示
│   ├── 显示方式                 [notification / subtitle / both]
│   └── 字幕停留秒数             [2 – 15 秒]
│
├── 交互菜单 (HUD)
│   ├── 启用 HUD                [✓/✗]
│   ├── HUD 缩放                [0.5x – 2.0x]
│   ├── 位置                     [top_left / top_right / bottom_left / bottom_right]
│   ├── 前景色 / 背景色          [7 / 5 种]
│   ├── 背景透明度               [0 – 255]
│   └── 提示音                  [✓/✗]
│
├── NPC 行为
│   ├── 响应半径                 [5 – 50 米]
│   ├── 活跃度                   [0 – 100]
│   ├── 允许主动开口             [✓/✗]
│   ├── 执行 LLM 动作（总开关）  [✓/✗]
│   ├── 允许 attack             [✓/✗]
│   ├── 允许 aim                [✓/✗]
│   └── 允许 call_cops          [✓/✗]
│
├── 热键
│   └── 菜单热键                 [F2 / F3 / F4 / F5 / F7 / F10 / F11 / F12]
│
├── ─── 快捷开关 ───
├── TTS 语音                    [✓/✗]
└── 觉醒系统                    [✓/✗]
```

### 活跃度说明

| 值 | 行为 |
|---|---|
| **0** | NPC 完全不主动找你，必须按 G/H/T/J 触发 |
| **50** | 默认节奏，~12 秒一次脑回路 |
| **100** | 非常主动，~4 秒一次（约 3 倍频率） |

### 行为开关用例

- 🛡️ 想做"和平观察"：把 `允许 attack`、`允许 aim`、`允许 call_cops` 全关 → NPC 哪怕被你打也不会还手或报警。
- 🎭 想要"只听不动"：把 `执行 LLM 动作` 关掉 → NPC 只会说话，所有动作降级为站立。
- 🏃 想要"密集互动"：把 `响应半径` 调到 30+米，`活跃度` 拉满 → 街头变剧院。

---

## ⚙️ 配置文件说明

游戏实际读取的配置位于：

```
%USERPROFILE%\Documents\GTA5MOD2026\config.ini
```

启动器和 F5 菜单的所有更改都会写入这里。仓库里 `config.reference.ini` 是带注释的参考。

主要段落：

```ini
[LLM]
Provider=local                # local 或 cloud
LocalEndpoint=http://127.0.0.1:5001/v1/chat/completions
LocalModel=gta5_npc_v2_q4km
# 切云端时填这两项：
CloudEndpoint=https://api.deepseek.com/v1/chat/completions
CloudModel=deepseek-v4-flash
CloudAPIKey=                  # 你的 API Key

[Performance]
MaxTokens=120                 # V5 输出 4 段 pipe，给足空间
Temperature=0.7
RequestCooldown=2.0           # 同一 NPC 两次脑回路最小间隔

[TTS]
TTSProvider=edge              # edge / piper / silero
TTSServer=http://127.0.0.1:5111
VoiceEnabled=True

[STT]
WhisperModelPath=C:\whisper-tiny

[Awakening]
Enabled=True
Speed=2                       # 1=慢 / 2=正常 / 3=快

[UI]                          # ← F5 菜单管理这一节
OverheadEnabled=True
OverheadScale=1.0
OverheadStyle=default
ResponseDisplayMode=notification
SubtitleDuration=4.0
HudEnabled=True
HudScale=1.0
HudPosition=top_left
HudBeep=False

[Behavior]                    # ← F5 菜单管理这一节
ResponseRadius=8.0
ActivityLevel=50
AutonomousTalk=True
ActionsEnabled=True
AllowAttack=True
AllowAim=True
AllowCallCops=True
```

---

## 👩‍💻 开发者指南

### 仓库结构

```
SentienceV5-Anima/                # 发布根目录
├── SentienceV5.exe                #   ↑ 由 launcher/ 构建生成
├── _internal/                     #   ↑ PyInstaller onedir 运行时
├── ModFiles/                      #   ↑ 由 GTA5MOD2026/ 构建生成
├── Runtime/llama.cpp/             # llama-server 二进制（手动放置一次）
├── Runtime/*.gguf                 # 模型文件
├── voice_server.py                # TTS 服务脚本
├── config.reference.ini
├── BUILD.ps1                      # 一键构建（mod + launcher）
├── launcher/                      # ── Python / flet 启动器源码 ──
│   ├── main.py                    #   flet UI 主程序
│   ├── server_manager.py          #   llama-server 进程管理
│   ├── voice_server_manager.py    #   voice_server.py 进程管理
│   ├── chat_client.py             #   /v1/chat/completions 客户端
│   ├── config_store.py            #   持久化配置 + 写 mod config.ini
│   ├── theme.py                   #   Material 3 主题
│   ├── requirements.txt           #   依赖：flet[all]==0.85.1, pyinstaller
│   ├── start.bat                  #   开发模式（venv 自动 + python 跑）
│   └── build_exe.ps1              #   PyInstaller 打 .exe
└── README.md                      # 本文档

../GTA5MOD2026/GTA5MOD2026/        # ── C# mod 源码（独立仓库/子目录）──
├── GTA5MOD2026.csproj             # .NET Framework 4.8
├── AIManager.cs                   # HTTP / 解析 / Normalize
├── NPCManager.cs                  # 主循环 / HUD / 按键
├── NPCState.cs                    # 单个 NPC 的全部状态
├── NPCPerception.cs               # 感知层
├── NPCBrain.cs                    # 自主行为脑
├── NPCGoalManager.cs              # 目标系统
├── MemoryManager.cs               # 长期记忆持久化
├── VoiceManager.cs                # TTS HTTP 调用
├── SpeechManager.cs               # STT 录音
├── ModConfig.cs                   # config.ini I/O（含 UI/Behavior 节）
├── SentienceMenu.cs               # F5 LemonUI 菜单
└── 增强版函数/GTA5_Native_Hash_Enum.cs
```

### 前置开发环境

```bash
git clone https://github.com/NexusVAI/SENTIENCE.git
cd SENTIENCE
```

| 工具 | 版本 | 用途 | 安装 |
|---|---|---|---|
| Python | **3.10–3.13** | 启动器构建 | <https://www.python.org/downloads/> |
| .NET SDK | **8.0+** | mod 编译 | <https://dotnet.microsoft.com/download> |
| PowerShell | 5+ / 7+ | 构建脚本 | Windows 自带 |
| Visual Studio 2022 *(可选)* | Community | 调试 C# mod | <https://visualstudio.microsoft.com/> |
| Git | any | 克隆仓库 | <https://git-scm.com/> |

### 构建 mod (C# / .NET)

```powershell
# 进 mod 源码目录
cd "D:\AI NPCS\GTA5MOD2026\GTA5MOD2026"

# 编译
dotnet build .\GTA5MOD2026.csproj -c Release --nologo --verbosity minimal

# 产物
# .\bin\Release\net48\GTA5MOD2026.dll
# .\bin\Release\net48\LemonUI.SHVDN3.dll
# .\bin\Release\net48\Newtonsoft.Json.dll
# .\bin\Release\net48\NAudio*.dll
```

把上述 DLL 全部拷到 `SentienceV5-Anima\ModFiles\` 即完成 mod 部署。

> ⚠️ `LemonUI.SHVDN3.dll` 必须与 `GTA5MOD2026.dll` 同目录，否则 mod 启动时菜单初始化会抛 FileNotFound。

### 构建启动器 (Python / PyInstaller)

```powershell
cd "D:\AI NPCS\SentienceV5-Anima\launcher"

# 首次：装依赖（脚本会自建 venv）
.\build_exe.ps1
```

脚本会：

1. 优先寻找已装 `flet[all]==0.85.1 + pyinstaller>=6.10` 的系统 Python；找不到就建 `.venv` 并 `pip install -r requirements.txt`
2. 设置 `FLET_VIEW_PATH` 环境变量 → 让 PyInstaller 把 flet desktop 运行时**离线打包进 exe**（避免 GitHub Release 下载失败 WinError 10060）
3. 调 PyInstaller 生成 `dist\SentienceV5\SentienceV5.exe`（onedir 模式）
4. 把 exe 和 `_internal\` 同步到发布根目录

> 📌 **关键 Hack**：launcher/main.py 顶部的 `_configure_flet_view_path()` 在运行时会再校验一次 `FLET_VIEW_PATH`，确保即便 PyInstaller 漏打包，也能找到本地 flet_desktop 副本，**永远不会去 github 拉**。

### 一键构建脚本

最省事的方式 —— 仓库根目录有：

```powershell
cd "D:\AI NPCS\SentienceV5-Anima"
.\BUILD.ps1
```

它会**依次**：

1. `dotnet build` mod → 拷 DLL 到 `ModFiles\`
2. 调 `launcher\build_exe.ps1` 打 launcher exe
3. 镜像 `ModFiles\` → `Install_To_GTA_scripts\`（玩家拖拽用）

构建完成后，**整个 `SentienceV5-Anima\` 目录即为发布包**，可压缩 `.zip` 分发。

### 开发模式（无需打 exe）

```powershell
cd "D:\AI NPCS\SentienceV5-Anima\launcher"
.\start.bat
```

首次运行自动建 venv 并装依赖。修改 `.py` 后直接重启即可——比每次跑 PyInstaller 快很多。

对 mod：用 Visual Studio 打开 `GTA5MOD2026.csproj`，在 Build Events 里加一行 Post-Build：

```bat
copy /Y "$(TargetPath)" "D:\Games\Grand Theft Auto V\scripts\$(TargetFileName)"
copy /Y "$(TargetDir)LemonUI.SHVDN3.dll" "D:\Games\Grand Theft Auto V\scripts\"
```

→ Build 即热部署。SHVDN3 默认 1 秒检测一次 scripts/ 目录，可以**不重启游戏**直接看到新版本。

---

## 🧠 模型说明

- **基座**：Qwen3.5-2B（混合 SSM + 注意力，256K 上下文）
- **训练**：SFT (LoRA r=128) → ORPO (LoRA r=32)
- **数据**：7,300 SFT + 2,600 ORPO，覆盖 16 个动作 / 23 个意图 / 8 个地点 / 7 种情绪
- **量化**：Q4_K_M，模型大小 **1.27 GB**

### 输出格式（pipe 分隔，4 段）

```
对话内容 | 动作 | 情绪 | 意图
```

例子：

```
你他妈别过来！|aim|fear|threaten_back
今天天气真好啊~|wave|happy|small_talk
警察！这里有人持械！|call_cops|fear|seek_help
```

mod 端 `AIManager.NormalizeAction` / `NormalizeEmotion` 已扩展支持中文词汇映射：

| 输入（任意） | 归一化 |
|---|---|
| `掏枪` / `举枪` / `aim` | `aim` |
| `打架` / `揍` / `attack` | `attack` |
| `逃` / `跑` / `flee` | `flee` |
| `愤怒` / `生气` / `angry` | `angry` |
| `害怕` / `恐惧` / `fear` | `fear` |

---

## 🔌 自定义后端 / 接入云端 API

不想用本地模型？想用 DeepSeek / OpenAI / 智谱 / Kimi？

1. 启动器侧栏切到 **后端 / Provider** → 选 **Cloud**
2. 填入 Endpoint + Model + API Key，例如 DeepSeek：

   ```
   Endpoint:  https://api.deepseek.com/v1/chat/completions
   Model:     deepseek-v4-flash
   API Key:   sk-xxxxx
   ```

3. 点 **保存** —— 启动器会同步写入 mod 的 `config.ini`
4. **关闭本地后端**（云端模式下不需要 llama-server）
5. 进游戏即用云端

> ⚠️ 云端模型**没经过本 mod 的微调**，输出格式可能不严格符合 `dialogue|action|emotion|intent` 四段。可通过把 `[Performance] StrictMode=False` 让 mod 容忍宽松格式。

---

## 🐛 常见问题 (FAQ)

<details>
<summary><b>Q: 游戏进去没反应，左上角不弹蓝色提示？</b></summary>

逐项排查：

1. 确认 `<GTA V>\scripts\GTA5MOD2026.dll` 存在
2. 确认**所有 7 个 DLL** 都拷过去了（特别是 `LemonUI.SHVDN3.dll`）
3. 在 `<GTA V>\` 根目录看是否有 `ScriptHookVDotNet.log` —— 用记事本打开看报错
4. `.NET Framework 4.8` 是否安装：开始菜单搜 "启用或关闭 Windows 功能"，确认 `.NET Framework 4.8 高级服务` 已勾选

</details>

<details>
<summary><b>Q: 启动器报 WinError 10060 / 无法下载 flet</b></summary>

旧版 bug，V5 Anima 已修复。如果还遇到，检查：

- `SentienceV5-Anima\_internal\flet_desktop\app\flet\flet.exe` 是否存在
- 不存在 → 重新解压发布包

</details>

<details>
<summary><b>Q: 启动器扫不到 llama-server / 模型</b></summary>

确认目录结构是：

```
SentienceV5-Anima\
├── Runtime\
│   ├── llama.cpp\
│   │   └── llama-server.exe   ← 必须在这
│   └── gta5_npc_v2_q4km.gguf  ← 或这
```

或者把模型放到 `%USERPROFILE%\Documents\GTA5MOD2026\models\`。

</details>

<details>
<summary><b>Q: NPC 不开口 / 头顶名字不显示</b></summary>

按 **F5** 打开菜单 →

- 头顶看不到名字 → `NPC 头顶绘制 → 启用悬浮标签` 是否打开
- NPC 不主动说话 → `NPC 行为 → 活跃度` 是不是被你调到 0 了
- 死过一次以后再也不显示交互 HUD → V5 Anima 已修复死亡-医院剧本冲突 bug，确认你装的是最新版

</details>

<details>
<summary><b>Q: TTS 不出声</b></summary>

1. 启动器**右上角 TTS 卡片**是否显示"运行中"，没有就点"启动 TTS"
2. F5 菜单 → `快捷开关 → TTS 语音` 是否打开
3. `config.ini [TTS] VoiceEnabled=True`
4. edge-tts **依赖网络**（微软 CDN）——纯离线环境请改 piper

</details>

<details>
<summary><b>Q: 输出乱码 / 重复同一句话</b></summary>

启动器调高 `Repeat Penalty` 到 **1.15-1.20**，或调低 `Temperature` 到 **0.5-0.6**。

</details>

<details>
<summary><b>Q: 输出回到 AI 助手腔（"作为 AI 助手，我不能…"）</b></summary>

模型 prompt 被污染了。确认 mod **正确发出**了 `system` prompt——查看启动器日志里 LLM 请求体的 `messages[0].role` 应为 `system`，内容包含 "你正在扮演 GTA5 里的 NPC…"。

</details>

<details>
<summary><b>Q: 我想自己换模型 / 用 7B 模型</b></summary>

把任意 GGUF 模型扔到 `Runtime\` 或 `%USERPROFILE%\Documents\GTA5MOD2026\models\`，启动器会自动列出。
注意：**没经过本 mod 微调的模型可能不会按 `dialogue|action|emotion|intent` 输出**，效果会差很多。建议先把本 mod 的训练脚本（另一个仓库 `gta5-npc-finetune`）跑一遍。

</details>

<details>
<summary><b>Q: 性能太差 / 卡顿</b></summary>

- 启动器把 `n_threads` 调到你的 CPU 物理核数
- 有 NVIDIA 显卡？把 `Runtime\llama.cpp\` 换成 CUDA 版本（<https://github.com/ggml-org/llama.cpp/releases> 选 `bin-win-cuda-*`）
- 关闭觉醒系统（F5 → 快捷开关 → 觉醒系统）
- 降低 `Behavior → 活跃度` 和 `响应半径`

</details>

---

## 🤝 贡献

欢迎 PR / Issue！特别欢迎：

- 🎨 更多 HUD 风格（cyberpunk / vintage / cyrillic …）
- 🌍 多语言 NPC（英文 / 日文 / 西班牙语对话数据）
- 🧩 更多动作（dance_with_player / share_food / vehicle_action …）
- 📦 piper / silero 等本地 TTS 后端集成
- 🐛 各类 bug 修复

### 开发流程

1. [Fork 仓库](https://github.com/NexusVAI/SENTIENCE/fork)
2. 新建分支：`git checkout -b feat/your-feature`
3. 提交前跑：`dotnet build` + `python -m py_compile launcher/main.py`
4. PR 描述里说清楚你测试过哪些场景

### 代码风格

- **C#**：4 空格缩进，PascalCase 公开 / camelCase + `_` 私有
- **Python**：PEP8，行宽 100
- **不要**乱删 / 修改其他无关代码
- **不要**未经讨论引入大依赖

---

## 📜 License

MIT License. 详见 [LICENSE](LICENSE)。

> 模型权重 `gta5_npc_v2_q4km.gguf` 基于 Qwen3.5（Apache 2.0），微调数据集为本项目自有。
> 你可以自由用于个人 / 教学 / 二次创作；商用请在 Issue 联系作者。
>
> **本项目与 Rockstar Games 无任何关联**。请在**单人离线**模式下使用，**严禁**用于 GTA Online（会被封号）。

---

## 🙏 致谢 / Credits

- [ScriptHookV](http://www.dev-c.com/gtav/scripthookv/) by Alexander Blade
- [ScriptHookVDotNet](https://github.com/scripthookvdotnet/scripthookvdotnet) — C# scripting framework
- [LemonUI](https://github.com/justalemon/LemonUI) by justalemon — 游戏内菜单
- [llama.cpp](https://github.com/ggml-org/llama.cpp) by Georgi Gerganov — 本地推理
- [Qwen](https://github.com/QwenLM/Qwen) by Alibaba Cloud — 基座模型
- [flet](https://flet.dev) — Python Material 3 桌面 UI
- [edge-tts](https://github.com/rany2/edge-tts) — 微软 TTS 客户端
- [whisper](https://github.com/openai/whisper) — OpenAI 语音识别
- [NAudio](https://github.com/naudio/NAudio) — Windows 音频
- 所有提供 issue / 测试反馈的玩家 ❤️

---

<div align="center">

**© 2026 Nexus V — Built with `flet` × `llama.cpp` × `Qwen3.5` × `LemonUI`**

*"And on the seventh day, the NPCs awoke."*

[⬆ 回到顶部](#-sentience-v5--anima)

</div>
