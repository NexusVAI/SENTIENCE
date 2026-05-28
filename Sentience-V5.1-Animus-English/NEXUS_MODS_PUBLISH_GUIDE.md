# 📤 Nexus Mods 发布指南 — Sentience V5.1 Animus

> 截图里看你已经在编辑页面了，下面是可以**直接复制粘贴**的内容。

---

## 1. Short Description（已填，可以优化）

**当前**: *"What if GTA V NPCs were not following scripts, but thinking? This mod replaces traditional scripted behavior with AI-driven NPCs powered by Large Language Models. NPCs can remember players, react to events, and make dynamic decisions based on the world around them."*

**建议改成**（更吸引点击）:

```
Sentience turns every pedestrian in GTA V into a real-time AI character with memory, emotions, voice synthesis & plugin SDK. Fully offline. No API keys.
```

---

## 2. Full Description（直接全选复制粘贴）

```markdown
[h1]Sentience V5.1 · Animus[/h1]
[h2]From Soul to Will — NPCs that remember you.[/h2]

[i]"What if GTA V NPCs were not following scripts, but thinking?"[/i]

[b]Sentience[/b] replaces traditional scripted NPC behavior with a fully offline AI system powered by a local Large Language Model. Every pedestrian in Los Santos becomes a sentient character with:

[list]
[*] Long-term memory (they remember your face, your actions, your reputation)
[*] Emotional states (angry, happy, scared, suspicious...)
[*] Personality archetypes (bikers, cops, businessmen, hipsters, homeless, and 25+ more)
[*] Real-time voice synthesis (TTS) — NPCs actually speak their lines aloud
[*] Voice recognition (STT) — hold [b]J[/b] and talk to NPCs through your microphone
[*] Plugin SDK — write your own C# plugins to customize NPC behavior
[*] Scenario engine — drop JSON files to create custom NPC interaction scripts
[/list]

[h2]🎮 How It Works[/h2]

Walk up to any NPC. Press:
[list]
[*][b]G[/b] — Compliment them
[*][b]H[/b] — Insult them
[*][b]T[/b] — Type dialogue
[*][b]J[/b] — Voice input (hold to talk, release to send)
[*][b]F5[/b] — Configuration menu
[/list]

The NPC processes your input through a local LLM brain, decides how to react, speaks their response aloud, and physically acts it out (attack, flee, hug, call cops, smoke, dance, etc.).

[h2]🔥 What's New in V5.1 Animus[/h2]

[h3]LSPDFR-Class Plugin Ecosystem[/h3]
Sentience is no longer just a mod — it's a [b]platform[/b].

[list]
[*] [b]SDK 1.0[/b] — Stable public API for third-party C# plugins (signature frozen, never breaks)
[*] [b]Plugin Loader[/b] — Drop .dll files into scripts/SentiencePlugins/. Auto-load, SemVer check, fault isolation, quarantine.
[*] [b]Scenario Engine[/b] — JSON-driven behavior scripts. No coding required.
[*] [b]30+ Archetypes[/b] — Built-in personalities with Chinese-localized prompts, edge-tts voices, and walkstyles. Override via INI.
[/list]

[h3]V5.1.1 Hotfix — Voice Input Fixed[/h3]
[list]
[*] [b]whisper-tiny now ships in the box[/b] — no extra downloads needed
[*] [b]Auto-discovery[/b] — model path and Python command are automatically detected
[*] [b]Human-readable errors[/b] — if something's missing, the mod tells you exactly how to fix it
[/list]

[h2]💻 Requirements[/h2]

[list]
[*] GTA V (Story Mode)
[*] [url=https://www.gta5-mods.com/tools/script-hook-v]ScriptHookV[/url]
[*] [url=https://github.com/scripthookvdotnet/scripthookvdotnet]ScriptHookVDotNet 3.6.0+[/url]
[*] Windows 10/11 64-bit
[*] 16 GB RAM (32 GB recommended)
[*] 6-core CPU with AVX2 (Intel 6th Gen+ / AMD Ryzen)
[*] NVIDIA GPU optional but strongly recommended for CUDA acceleration
[*] Microphone (only if using voice input)
[*] [b]No internet required after download.[/b] This is a fully offline package.
[/list]

[h2]⚡ Quick Start[/h2]

[olist]
[*] Download [b]Sentience-V5.1-Animus.zip[/b] (~1.38 GB)
[*] Extract anywhere
[*] Copy [b]Install_To_GTA_scripts/[/b] contents to [code]<GTA V>\scripts\[/code]
[*] Double-click [b]SentienceV5.exe[/b] to launch the backend
[*] Click [b]"Start All"[/b] in the launcher
[*] Launch GTA V Story Mode
[*] Wait for the blue notification: [i]"Sentience V5.1 Animus loaded"[/i]
[*] Find an NPC. Press G/H/T/J. Watch them become alive.
[/olist]

[h2]🧩 Plugin Development[/h2]

Want to write your own plugin? The SDK is intentionally tiny:

[list]
[*] [b]6 interfaces[/b] — ISentiencePlugin, IPluginContext, ISentienceEvents, INPCContext, ISentienceLogger, SentienceSdk
[*] [b]1 static class[/b] — version constants
[*] [b]0 framework lock-in[/b] — plain C# + SHVDN3
[/list]

Full guide included in the download: [code]docs/Plugin-Development-Guide.md[/code]

Sample plugin included: [b]Sentience.Plugins.PoliceRP[/b] — NPCs react to you drawing weapons based on their archetype.

[h2]🌐 About In-Game Language[/h2]

[b]Important:[/b] The AI currently outputs NPC dialogue primarily in [b]Chinese[/b]. This is a technical artifact of the training data (Qwen model, Chinese NPC dialogue corpus), not a design choice.

[list]
[*] [b]UI, menus, documentation, logs, and launcher are fully English-ready.[/b]
[*] NPC action/emotion tags ([code]attack|flee|happy|angry[/code]) are English keywords and work perfectly.
[*] A proper English model requires re-fine-tuning on an English NPC dialogue dataset. We are actively exploring this.
[/list]

See [code]ABOUT_LANGUAGE.md[/code] in the download for full details and roadmap.

[h2]📜 License[/h2]

MIT License. Fork it, modify it, sell it. Just maybe give us a shoutout.

[h2]🙏 Credits[/h2]

[list]
[*] ggml / llama.cpp team — for making local LLMs possible
[*] SHVDN team — scripting backbone
[*] LemonUI team — in-game menu framework
[*] The Chinese GTA modding community — dataset, testing, and memes
[*] [b]You[/b] — for downloading this and giving our NPCs a reason to exist
[/list]

[i]"We didn't just make NPCs smarter. We made Los Santos feel like it has a pulse."[/i]

— The Sentience Team
```

---

## 3. Tags（截图里显示 No tags selected，必须填）

在 **Tags** 区域搜索并添加：

| Tag | 搜索关键词 |
|---|---|
| ✅ `Script` | 已默认（Category = Scripts） |
| ✅ `Gameplay` | gameplay |
| ✅ `AI-Generated Content` | ai-generated |
| ✅ `Character Preset` | character preset |
| ✅ `Quality of Life` | quality of life |
| ✅ `Utility for Players` | utility |

**不要勾选** Adult tags（你的 mod 没有 NSFW 内容）。

---

## 4. 文件上传步骤

### A. Main File（主文件 — 玩家下载的）

1. 页面向下滚动到 **Files** 标签（或点击顶部的 Files）
2. 点击 **Upload a new file**
3. 选择文件：`Sentience-V5.1-Animus.zip` (~1.38 GB)
4. 填写信息：
   - **Name**: `Sentience V5.1.1 Animus — Full Release`
   - **Category**: `Main File`
   - **Description**: `Complete player release. Includes LLM model, whisper-tiny STT model, launcher, mod DLL, sample plugin, and documentation.`
5. 点击 **Save**

> ⚠️ Nexus Mods 对免费用户有下载速度限制，1.4GB 文件可能需要 [url=https://users.nexusmods.com/account/billing]Premium[/url] 才能获得最快下载体验。你可以在描述里写：
> *"This is a large file (~1.38 GB) due to the bundled AI model. Premium download recommended for fastest speed."*

### B. Optional File（源码 — 给开发者）

1. 再次点击 **Upload a new file**
2. 选择：`Sentience-V5.1-Animus-GitHub.zip` (~470 KB)
3. 填写信息：
   - **Name**: `Source Code Mirror`
   - **Category**: `Optional File`
   - **Description**: `Full C# source code for developers and contributors. Includes SDK, plugin loader, scenario engine, and archetype system.`
4. 点击 **Save**

---

## 5. Images & Videos（很重要！决定点击量）

在 **Media** 标签页上传：

### 必须有的截图：

| 优先级 | 内容 | 为什么 |
|---|---|---|
| 🔴 **P0** | NPC 头顶标签 + 对话字幕 | 证明 AI 对话是实时的 |
| 🔴 **P0** | F5 菜单界面 | 展示配置丰富度 |
| 🟡 **P1** |  launcher 主界面 | 展示桌面启动器 |
| 🟡 **P1** | 插件菜单（F5 → Plugins & Scenarios） | 证明 SDK 存在 |
| 🟢 **P2** | NPC 做出不同动作（攻击/逃跑/拥抱） | 展示 action 系统 |
| 🟢 **P2** | 夜间的截图（光影好看） | Nexus 玩家喜欢好看的图 |

### 视频（强烈推荐）：

录一段 60-90 秒的演示视频：
1. 你走向一个 NPC
2. 按 J 说话（或 T 打字）
3. NPC 回应，字幕弹出，声音播放
4. NPC 做出动作（比如逃跑或攻击）
5. 展示 F5 菜单的插件列表

上传到 YouTube 后，在 **Videos** 标签页嵌入链接。

---

## 6. 发布前 Checklist

- [ ] Short description ≤ 255 字符（Nexus 限制）
- [ ] Full description 已粘贴并预览检查格式
- [ ] Tags 已添加（至少 3-5 个）
- [ ] Main file 已上传（Sentience-V5.1-Animus.zip）
- [ ] Optional source file 已上传（Sentience-V5.1-Animus-GitHub.zip）
- [ ] 至少 3 张截图已上传（Media 标签）
- [ ] 预览无误，没有 `[h1]` 等标签泄露为纯文本
- [ ] 点击页面底部的 **Save** 或 **Publish**

---

## 7. 发布后维护

### 每次更新时：

1. **Files** → **Upload a new version**
2. 上传新 zip，填写 **Changelog**（可以直接从 `RELEASE_NOTES_v5.1.1.md` 复制）
3. 勾选 **"This is a new version"**
4. Nexus 会自动通知关注者

### 回复评论：

- 英文玩家问 "Why Chinese?" → 复制 `ABOUT_LANGUAGE.md` 里的 explanation
- 问 "How to install?" → 指向 README 的 Quick Start
- Bug report → 要 `ScriptHookVDotNet.log`

---

## 快速复制区

**Short Description（复制这个）:**
```
Sentience turns every pedestrian in GTA V into a real-time AI character with memory, emotions, voice synthesis & plugin SDK. Fully offline. No API keys.
```

**Tags（逐个搜索添加）:**
```
Gameplay, AI-Generated Content, Character Preset, Quality of Life, Utility for Players
```

**Main File Description:**
```
Complete player release (~1.38 GB). Includes Qwen fine-tuned LLM model, whisper-tiny offline STT model, Material 3 desktop launcher, mod DLL, sample PoliceRP plugin, JSON scenarios, and full documentation.
```

**Optional File Description:**
```
Full C# source code mirror for developers. Includes SDK 1.0 interfaces, plugin loader with fault isolation, scenario engine, archetype voice system, and build scripts.
```

---

*祝发布顺利，点击量爆炸。* 🔥
