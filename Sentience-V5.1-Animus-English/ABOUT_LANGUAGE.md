# 🌐 About In-Game Language & Localization

**Last Updated**: 2026-05-28 · **Applies to**: Sentience V5.1 Animus and all prior versions

---

## The Honest Truth (No Copium)

If you're reading this in English and just installed the mod, you might be asking:

> *"Why are the NPCs speaking Chinese? I thought this was an English release."*

Fair question. Deserves an honest answer. No corporate PR speak. Just facts.

---

## Why The AI Speaks Chinese

### 1. The Base Model

Sentience runs on a **fine-tuned Qwen model** (Qwen is Alibaba's LLM family, primarily trained on Chinese and English corpora, with a strong structural bias toward Chinese expression).

The fine-tuning dataset we used consists of:
- Chinese NPC dialogue from GTA V roleplay servers
- Chinese personality prompts, emotion tags, and action labels
- Chinese system instructions that define "who the NPC is"

When you load the model, its "instinct" is to respond in Chinese because **that's what it was taught to do**.

### 2. The System Prompt

Every NPC interaction starts with a `system` message that looks roughly like this:

```
你正在扮演 GTA5 里的一个 NPC。你的性格是 [archetype]。
玩家对你说: [input]
请按格式输出: dialogue|action|emotion|intent
```

The model receives this in **Chinese**. It thinks in Chinese. It generates in Chinese. The `dialogue` segment of its output is Chinese text.

### 3. The Output Parser

The mod's parser splits the AI response on `|`:
```
"你干什么?!|attack|angry|hostile"
   ↑dialogue  ↑action  ↑emotion  ↑intent
```

The `action`, `emotion`, and `intent` fields are **language-agnostic keywords** (`attack`, `flee`, `happy`, `suspicious`). Those work fine regardless of language.

But the `dialogue` field — the actual spoken line — is whatever the model generates. And right now, that's Chinese.

---

## Can I Force English Output?

**Technically?** Yes. **Reliably?** Not really.

### What You CAN Do (Advanced Users)

1. **Edit the system prompt to be fully English.**  
   In the source code (or via a plugin that intercepts `NPCRequestingAI`), rewrite the system prompt:
   ```
   You are an NPC in GTA V. Your personality is [archetype].
   The player says: [input]
   Output format: dialogue|action|emotion|intent
   ```

2. **Set the launcher / backend to prefer English.**  
   Some players have reported partial success by setting `Temperature` lower (~0.5) and explicitly instructing English in the prompt. Your mileage will vary.

3. **Use a translation layer.**  
   In theory, a plugin could intercept `NPCDialogue` events and run the Chinese text through a local translation model. We haven't built this yet, but the SDK supports it.

### Why It Doesn't Work Reliably

- **Model weights are optimized for Chinese.** Even with an English prompt, the model will frequently "drift" back to Chinese vocabulary and grammar patterns.
- **The fine-tuning data is 95% Chinese.** The model has seen vastly more Chinese NPC dialogue than English. English outputs tend to be shorter, less expressive, and sometimes grammatically odd.
- **Emotional nuance is lost.** A lot of what makes Sentience special is the *personality depth* in the Chinese prompts. Direct translation often sounds robotic.

---

## Our Roadmap to Proper English Support

We are not ignoring the English-speaking community. Far from it. Here's the actual plan:

| Phase | Goal | Status | Blockers |
|---|---|---|---|
| **V5.1.x** | Document the limitation transparently | ✅ Done (you're reading it) | None |
| **V5.2** | Bilingual prompt engineering + optional English system prompt toggle | 🔄 Planned | Needs testing dataset |
| **V5.3** | Community English scenario pack (JSON scenarios with English prompts) | 🔄 Planned | Community contributions |
| **V6.0** | Full English fine-tuned model (re-train on English NPC dialogue corpus) | ⏳ Future | Needs English dataset + compute |

### What Would Speed This Up

1. **English NPC dialogue datasets.** If you have access to English GTA RP logs, English video game NPC scripts, or any structured English character dialogue data — share it. That's the single biggest blocker.
2. **Community translation plugins.** The SDK is public. If someone writes a `TranslationPlugin` that hooks `NPCDialogue` and pipes it through a local NLLB model, we will feature it in the next release.
3. **Feedback on bilingual prompting.** If you experiment with English prompts and get good results, document it. We'll integrate working patterns into the official build.

---

## What IS in English Right Now?

To be absolutely clear about what you're getting:

| Component | Language | Notes |
|---|---|---|
| **Launcher UI** | English / Chinese bilingual | All labels, buttons, logs |
| **In-game F5 Menu** | English / Chinese bilingual | Toggle via config |
| **`ScriptHookVDotNet.log`** | English | All debug/error logging |
| **Plugin SDK docs** | English | Full developer guide |
| **This documentation** | English | You're reading it |
| **NPC Action Tags** | English | `attack`, `flee`, `dance`, etc. |
| **NPC Emotion Tags** | English | `angry`, `happy`, `sad`, etc. |
| **NPC Spoken Dialogue** | **Chinese** | The actual voice lines |

So: the *infrastructure* is fully English-ready. The *content* (what NPCs say) is Chinese.

Think of it like watching a subtitled foreign film. The UI is in your language. The characters speak another. It adds flavor — but we fully understand if you want full English, and we're building toward it.

---

## A Message to Our English-Speaking Players

We see you. We know this is frustrating. We know "just learn Chinese" is not a valid answer.

**The Chinese output is a technical artifact, not a design choice.** We didn't make the NPCs speak Chinese to exclude anyone. We made them speak Chinese because that's the training data we had access to, and that's the team that built the first version.

But Sentience is meant to be **global**. The fact that you're reading this document means we care enough to write it. The fact that the SDK, the docs, the launcher, and the menus are all bilingual means we're investing in accessibility.

**The NPCs will speak English.** It's not an "if." It's a "when." And the more the community engages, the faster "when" becomes "now."

Thank you for your patience. Thank you for trying the mod anyway. Thank you for being part of this.

> *"Every legend starts somewhere. For Sentience, it started in Chinese. But it won't end there."*

---

**Questions?** Open an issue and tag it `[LANGUAGE]`.

**Want to help?** Check the roadmap above or ping us about dataset contributions.

**Sentience Team · 2026-05-28**
