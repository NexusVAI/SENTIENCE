# 🧠 Sentience V5.1 · Animus

> *"From Soul to Will — Anima gave NPCs a soul. Animus gives them a shape that **you** can mold."*

**Yo. Welcome, legends.**

You are currently looking at what we genuinely believe is the **most unhinged, most ambitious, most goated offline AI mod** ever built for Grand Theft Auto V. No cloud APIs. No subscriptions. No "please connect to Wi-Fi so Google can read your NPC dialogue." Just **pure, local, sentient chaos** running on your own machine.

We built this for the global modding community — and yeah, that means **you**, reading this in English, probably at 2 AM, probably with a energy drink nearby. You are the reason we didn't stop at V5. We kept going. **For you.**

---

## 🎯 What Even Is This? (One-Liner)

**Sentience turns every single pedestrian in GTA V into a real-time AI-driven character** with long-term memory, emotional states, personality archetypes, voice synthesis, voice recognition, and a full plugin SDK so *you* can write custom behavior scripts.

Think **LSPDFR-level ecosystem**, but for AI NPCs. And entirely offline.

---

## 🔥 Feature Breakdown (Why This Is Literally Insane)

| Feature | What It Does | Why It Hits Different |
|---|---|---|
| 🤖 **Local LLM Brain** | Custom fine-tuned Qwen model (q4_k_m, ~1.27 GB) running via llama.cpp | Zero latency. Zero API keys. Zero "server overloaded" errors. Runs on CPU *or* GPU. |
| 🎭 **16 Actions + 7 Emotions** | NPCs can attack, flee, hug, point, call cops, smoke, dance, laugh, cry, surrender... | Not scripted animations — the AI **chooses** these based on context. |
| 🗣️ **TTS Voices** | edge-tts with Chinese emotional voices, auto gender-mapped | Every NPC sounds different. Every. Single. One. |
| 🎤 **STT Voice Input** (V5.1.1 Fixed!) | Hold `J` and talk to NPCs through your mic | **Now ships with whisper-tiny in the box.** No extra downloads. Just plug & play. |
| 🧠 **Long-Term Memory + Awakening** | NPCs remember you. Repeated interactions trigger "Awakening" — behavior permanently changes. | That random biker you insulted three days ago? He remembers. He holds grudges. |
| 🎨 **F5 LemonUI Menu** | Full in-game HUD: overhead tags, subtitles, behavior toggles, debug info | Every setting is tweakable without alt-tabbing. |
| 📱 **Material 3 Launcher** | Beautiful flet-based desktop UI, light/dark theme, one-click backend start | Your launcher looks like it belongs in 2026, not 2014. |
| 🧩 **Plugin SDK 1.0** (V5.1 NEW) | Full C# plugin API with events, logging, fault isolation | Write a 100-line DLL and NPCs will react to *your* custom logic. |

---

## 💻 System Requirements

| Component | Minimum | Recommended |
|---|---|---|
| **OS** | Windows 10 64-bit | Windows 10/11 64-bit |
| **Game** | GTA V + ScriptHookV + ScriptHookVDotNet 3.6.0 | Latest SHVDN3 nightly |
| **CPU** | 6-core / AVX2 (Intel 6th Gen+ / AMD Ryzen) | 8-core+ modern CPU |
| **RAM** | 16 GB | 32 GB |
| **GPU** | NVIDIA (optional, but 3-5x speedup with CUDA) | RTX 3060+ for butter-smooth local inference |
| **Mic** | Only if using STT voice input | Any USB headset works |
| **Disk** | ~3 GB free | SSD strongly recommended |

> **No internet required after download.** This is a fully offline package. We respect your privacy like we respect a perfect heist setup.

---

## ⚡ Quick Start (5 Minutes to Sentient NPCs)

### Step 1: Install the Mod

1. Download **`Sentience-V5.1-Animus.zip`** (~1.38 GB).
2. Extract it anywhere.
3. Copy the contents of `Install_To_GTA_scripts\` into your **`<GTA V>\scripts\`** folder.
   - You should end up with:
     ```
     <GTA V>\scripts\GTA5MOD2026.dll
     <GTA V>\scripts\LemonUI.SHVDN3.dll
     <GTA V>\scripts\Newtonsoft.Json.dll
     <GTA V>\scripts\NAudio*.dll
     ```
4. (Optional) Copy `whisper-tiny\` to `%USERPROFILE%\Documents\GTA5MOD2026\whisper-tiny\` for the cleanest setup.

### Step 2: Launch the Backend

1. Double-click **`SentienceV5.exe`** in the release folder.
2. The launcher will auto-detect your model and TTS server.
3. Hit **"Start All"**.

### Step 3: Enter Los Santos

1. Launch GTA V (Story Mode).
2. Wait for the blue notification: **"Sentience V5.1 Animus loaded"**.
3. Walk up to any NPC. Press:
   - **`G`** — Compliment
   - **`H`** — Insult
   - **`T`** — Type dialogue
   - **`J`** — Voice input (hold to talk, release to send)
   - **`F5`** — Open the config menu

4. Watch a previously braindead pedestrian become a sentient being who remembers your face.

---

## 🧩 Plugin Ecosystem (V5.1 — The Game Changer)

Animus isn't just a mod. It's a **platform**.

We froze the SDK at `1.0.0`. Those signatures are sacred. Write a plugin today, it works in V5.2, V6, V7. No cap.

### What's Included

- **`SDK/`** — Public C# interfaces (`ISentiencePlugin`, `IPluginContext`, `ISentienceEvents`, etc.)
- **`SentiencePlugins/`** — Drop your `.dll` here. The mod loads it, checks SemVer compatibility, isolates faults, and quarantines crashing plugins automatically.
- **`Scenarios/`** — JSON script files that define NPC behavior trees. No coding required.
- **`archetype_voices.ini`** — Override built-in archetypes with your own personalities, voices, and walkstyles.

### Built-In Archetypes (30+)

Every ped model is mapped to a personality:

| Archetype | Personality | TTS Voice | Walkstyle |
|---|---|---|---|
| `biker` | Aggressive, loyal to crew, hates cops | YunjianNeural | Swagger |
| `hipster` | Irony-poisoned, talks about vinyl | YunxiNeural | Arrogant |
| `businessman` | Cold, transactional, always calculating | YunfengNeural | Business |
| `cop` | By-the-book until provoked | YunhaoNeural | Cop |
| `homeless` | World-weary but observant | XiaoxiaoNeural | Sad |

And 25+ more. You can override all of them with a single INI file.

### Sample Plugin: PoliceRP

Included in the package:
```csharp
public class PoliceRPPlugin : ISentiencePlugin
{
    public string Name => "Police RP";
    public void OnLoad(IPluginContext ctx)
    {
        ctx.Events.PlayerWeaponChanged += (s, e) => {
            if (e.WeaponCategory == WeaponCategory.Handgun)
                // NPCs now react to you drawing a weapon based on their archetype
        };
    }
}
```

Full guide: [`docs/Plugin-Development-Guide.md`](docs/Plugin-Development-Guide.md)

---

## 🌐 About In-Game Language (Important!)

Okay, real talk — we need to address the elephant in the room.

**The AI currently outputs dialogue primarily in Chinese.** Here's why, and what we're doing about it:

### Why?

1. **The fine-tuned model is Qwen-based**, trained on a Chinese NPC dialogue corpus. The `system` prompt that defines the NPC's personality is written in Chinese. The model "thinks" in Chinese first.
2. **The output format** (`dialogue|action|emotion|intent`) is language-agnostic — the *action* and *emotion* tags work perfectly regardless of language. But the *spoken dialogue* defaults to Chinese.

### Can I Get English Output?

**Short answer:** Not reliably out-of-the-box with this model.

**Long answer:**
- You *can* try editing the `system` prompt in the code to be fully English. Some sentences may come out in English, but the model will frequently drift back to Chinese because its weights are optimized for Chinese expression.
- A proper English version requires **re-fine-tuning the base model on an English NPC dialogue dataset**. We are actively exploring this. If the community wants it, we will build it.
- **The UI, menus, documentation, logs, and launcher are all fully English-ready.** Only the NPC's spoken lines are Chinese.

### The Plan

| Phase | Goal | ETA |
|---|---|---|
| **V5.1.x** | Document the limitation honestly (this is that documentation) | Now |
| **V5.2** | Experiment with bilingual prompt engineering + optional English model slot | TBD |
| **V6.0** | Full English fine-tuned model if community demand is high enough | Future |

> **We are not gatekeeping English.** We genuinely want every player on Earth to use this. The Chinese output is a technical artifact of the training data, not a design choice. We see you, we hear you, and we're working on it. 🙏

In the meantime, many of our beta testers actually found the Chinese dialogue **charming** — it adds a "visiting a foreign city" vibe to Los Santos. Your mileage may vary, but we wanted to be transparent before you install.

---

## 🐛 FAQ

<details>
<summary><b>Q: Nothing happens in-game. No blue notification?</b></summary>

1. Check that `<GTA V>\scripts\GTA5MOD2026.dll` exists.
2. Check that **all 7 DLLs** are present (especially `LemonUI.SHVDN3.dll`).
3. Check `ScriptHookVDotNet.log` in your GTA V root for errors.
4. Ensure `.NET Framework 4.8` is installed.

</details>

<details>
<summary><b>Q: Launcher says WinError 10060 / can't download flet?</b></summary>

Fixed in V5.1+. If you still see it, your `_internal\` folder is missing. Re-extract the full release zip.

</details>

<details>
<summary><b>Q: Press J to talk, but nothing happens (V5.1.1)</b></summary>

The `whisper-tiny` model is now bundled, but you still need:
- Python 3.10+ in your PATH
- `pip install sounddevice numpy faster-whisper`

The mod will auto-detect your Python (`python` or `py -3`). If both fail, you'll get a friendly error message telling you exactly what to install.

</details>

<details>
<summary><b>Q: NPCs speak Chinese. Can I change this?</b></summary>

See [🌐 About In-Game Language](#-about-in-game-language-important) above. TL;DR: the model is Chinese-trained. We're working on an English variant. For now, embrace the chaos or edit the system prompt at your own risk.

</details>

<details>
<summary><b>Q: Can I use my own LLM model?</b></summary>

Absolutely. Drop any GGUF into `Runtime\` or `%USERPROFILE%\Documents\GTA5MOD2026\models\`. The launcher will detect it. **Warning:** models not fine-tuned on our dataset may not output the required `dialogue|action|emotion|intent` format, so NPC behavior will break.

</details>

<details>
<summary><b>Q: Performance is bad / stuttering</b></summary>

- Set `n_threads` in the launcher to your CPU's physical core count.
- Use a CUDA build of llama.cpp if you have NVIDIA.
- Disable the Awakening system via F5 menu for lower CPU usage.

</details>

---

## 🤝 Contributing & Community

We are a small team, but we read every piece of feedback.

- **Found a bug?** Open an issue with your `ScriptHookVDotNet.log` attached.
- **Wrote a plugin?** Tag us — we feature community plugins in our releases.
- **Want an English model?** React with 👍 on the English Model tracking issue so we can prioritize it.

---

## 📜 License

Sentience is released under the **MIT License**.

You can fork it, modify it, sell it, make a TikTok about it, whatever. Just maybe give us a shoutout. We spent a lot of nights on this.

---

## 🙏 Shoutouts

- **ggml / llama.cpp team** — for making local LLMs possible on consumer hardware
- **SHVDN team** — for the scripting backbone
- **LemonUI team** — for the cleanest in-game menu framework
- **The Chinese GTA modding community** — for the dataset, the testing, and the memes
- **You** — for downloading this, reading this far, and giving our NPCs a reason to exist

> *"We didn't just make NPCs smarter. We made Los Santos feel like it has a pulse."*

**— The Sentience Team**

---

*Sentience V5.1 Animus · 2026-05-28 · Built with rage, caffeine, and respect for the player.*
