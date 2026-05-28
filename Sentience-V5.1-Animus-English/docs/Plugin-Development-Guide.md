# 🧩 Sentience Plugin Development Guide

> *"Welcome to the SDK. If you can write 100 lines of C#, you can make Los Santos feel alive."*

**Version**: SDK 1.0.0 (Frozen) · **Target Framework**: .NET Framework 4.8 · **Game**: GTA V via ScriptHookVDotNet 3.6+

---

## Table of Contents

1. [Why Plugins?](#why-plugins)
2. [Getting Started in 5 Minutes](#getting-started-in-5-minutes)
3. [SDK API Reference](#sdk-api-reference)
4. [Project Skeleton](#project-skeleton)
5. [Events Deep Dive](#events-deep-dive)
6. [Scenario JSON Format](#scenario-json-format)
7. [Archetype INI Format](#archetype-ini-format)
8. [Building & Deploying](#building--deploying)
9. [Compatibility & Versioning](#compatibility--versioning)
10. [FAQ](#faq)

---

## Why Plugins?

Sentience V5.1 Animus ships with 30+ built-in archetypes, a scenario engine, and a local LLM brain. That's a lot. But it's still *our* vision of Los Santos.

**Plugins let you write *your* vision.**

Want every NPC to panic when it rains? Write a plugin.  
Want a custom gang that only spawns at night and quotes Shakespeare? Write a plugin.  
Want NPCs to react to your Twitch chat? Write a plugin.

The SDK is intentionally minimal — 6 interfaces, 1 static class — because we believe constraints breed creativity. You don't need to learn a new framework. You need C#, 30 minutes, and an idea.

---

## Getting Started in 5 Minutes

### Step 1: Create a C# Class Library

```bash
dotnet new classlib -n MyCoolPlugin -f net48
```

Or use Visual Studio: `File → New → Project → Class Library (.NET Framework)`.

### Step 2: Reference the SDK

Add a reference to **`GTA5MOD2026.dll`** (the mod itself — the SDK is compiled into it):

```xml
<ItemGroup>
  <Reference Include="GTA5MOD2026">
    <HintPath>..\..\GTA5MOD2026.dll</HintPath>
  </Reference>
</ItemGroup>
```

> The SDK lives in the `GTA5MOD2026.SDK` namespace. No separate NuGet package — the mod *is* the SDK.

### Step 3: Implement `ISentiencePlugin`

```csharp
using GTA5MOD2026.SDK;
using System;

namespace MyCoolPlugin
{
    public class MyPlugin : ISentiencePlugin
    {
        public string Name => "My Cool Plugin";
        public string Author => "YourName";
        public string Version => "1.0.0";
        public string MinSdkVersion => "1.0.0";

        private IPluginContext _ctx;

        public void OnLoad(IPluginContext context)
        {
            _ctx = context;
            _ctx.Logger.Info($"{Name} loaded! SDK={context.HostSdkVersion}, Mod={context.HostModVersion}");

            // Subscribe to events
            _ctx.Events.NPCSpawned += OnNpcSpawned;
            _ctx.Events.PlayerWeaponChanged += OnWeaponChanged;
        }

        public void OnUnload()
        {
            _ctx.Events.NPCSpawned -= OnNpcSpawned;
            _ctx.Events.PlayerWeaponChanged -= OnWeaponChanged;
            _ctx.Logger.Info($"{Name} unloaded.");
        }

        private void OnNpcSpawned(object sender, NPCSpawnedEventArgs e)
        {
            _ctx.Logger.Debug($"NPC spawned: {e.Context.Name} ({e.Context.Archetype})");
        }

        private void OnWeaponChanged(object sender, PlayerWeaponChangedEventArgs e)
        {
            if (e.WeaponCategory == WeaponCategory.Handgun)
                _ctx.Logger.Info("Player pulled a handgun — plugins can react here!");
        }
    }
}
```

### Step 4: Build & Drop

```bash
dotnet build -c Release
```

Copy `MyCoolPlugin.dll` to:
```
<GTA V>\scripts\SentiencePlugins\MyCoolPlugin.dll
```

Launch GTA V. Open the F5 menu → **Plugins & Scenarios**. You'll see your plugin listed.

That's it. You're now a Sentience plugin developer. Welcome to the club.

---

## SDK API Reference

### `ISentiencePlugin`

The contract every plugin must implement.

```csharp
public interface ISentiencePlugin
{
    string Name { get; }              // Display name (keep it short)
    string Author { get; }            // Your name or tag
    string Version { get; }           // SemVer, e.g. "1.0.0"
    string MinSdkVersion { get; }     // Minimum SDK version required, e.g. "1.0.0"

    void OnLoad(IPluginContext context);
    void OnUnload();
}
```

**Important**: `OnLoad` must not throw. If it throws, the plugin is immediately quarantined.

---

### `IPluginContext`

Your plugin's window into the Sentience world.

```csharp
public interface IPluginContext
{
    ISentienceEvents Events { get; }           // Subscribe to game events
    ISentienceLogger Logger { get; }           // Per-plugin logging
    string ConfigSnapshot { get; }             // Raw config.ini content (read-only)
    string PluginDataDirectory { get; }        // %Documents%\GTA5MOD2026\PluginData\<YourPluginName>\
    string HostSdkVersion { get; }             // e.g. "1.0.0"
    string HostModVersion { get; }             // e.g. "5.1.1"
}
```

The `PluginDataDirectory` is created automatically. Use it to store JSON, SQLite, INI, or whatever your plugin needs.

---

### `ISentienceEvents`

The event hub. Subscribe to what you care about. Unsubscribe in `OnUnload`.

```csharp
public interface ISentienceEvents
{
    event EventHandler<NPCSpawnedEventArgs> NPCSpawned;
    event EventHandler<NPCDespawnedEventArgs> NPCDespawned;
    event EventHandler<NPCRequestingAIEventArgs> NPCRequestingAI;   // Writable!
    event EventHandler<NPCDialogueEventArgs> NPCDialogue;
    event EventHandler<PlayerInteractionEventArgs> PlayerInteraction;
    event EventHandler<PlayerWeaponChangedEventArgs> PlayerWeaponChanged;
}
```

**Fault isolation**: If your handler throws, the hub catches it, logs it, and **permanently removes your handler** for that event type. The rest of the mod keeps running. Don't throw.

---

### Event Args Deep Dive

#### `NPCSpawnedEventArgs`

```csharp
public class NPCSpawnedEventArgs : EventArgs
{
    public INPCContext Context { get; }
    public DateTime GameTime { get; }
}
```

Fired once per NPC spawn. `Context` is a read-only snapshot. Good for: logging, analytics, applying custom overrides.

---

#### `NPCDespawnedEventArgs`

```csharp
public class NPCDespawnedEventArgs : EventArgs
{
    public INPCContext Context { get; }
    public DateTime GameTime { get; }
}
```

Fired once per NPC despawn. Good for: cleanup, saving state, batch analytics.

---

#### `NPCRequestingAIEventArgs` ⭐ Writable

```csharp
public class NPCRequestingAIEventArgs : EventArgs
{
    public INPCContext Context { get; }
    public string PlayerInput { get; }
    public string Kind { get; }               // "compliment", "insult", "dialogue", "voice"
    public List<string> MemoryFacts { get; }
    public string ExtraSystemPrompt { get; set; }  // ← WRITE TO THIS
    public DateTime GameTime { get; }
}
```

This is where the magic happens. The mod is about to send a request to the LLM. You can **inject additional system prompt context** by writing to `ExtraSystemPrompt`.

**Example** — Make all cops extra hostile when the player has a wanted level:

```csharp
_ctx.Events.NPCRequestingAI += (s, e) =>
{
    if (e.Context.Archetype == "cop" && Game.Player.WantedLevel > 0)
    {
        e.ExtraSystemPrompt += "\nThe player currently has a wanted level. You are aggressively pursuing arrest.";
    }
};
```

Multiple plugins can write to `ExtraSystemPrompt`. They're concatenated with `\n\n`.

---

#### `NPCDialogueEventArgs`

```csharp
public class NPCDialogueEventArgs : EventArgs
{
    public INPCContext Context { get; }
    public string RawResponse { get; }        // Full pipe-delimited response
    public string Dialogue { get; }           // Just the spoken line
    public string Action { get; }             // Parsed action tag
    public string Emotion { get; }            // Parsed emotion tag
    public string Intent { get; }             // Parsed intent tag
    public DateTime GameTime { get; }
}
```

Fired after the AI response is parsed but before the NPC acts on it. Good for: translation plugins, logging dialogue, custom action overrides.

---

#### `PlayerInteractionEventArgs`

```csharp
public class PlayerInteractionEventArgs : EventArgs
{
    public INPCContext Context { get; }
    public string Kind { get; }               // "compliment" or "insult"
    public DateTime GameTime { get; }
}
```

Fired when the player presses `G` (compliment) or `H` (insult).

---

#### `PlayerWeaponChangedEventArgs`

```csharp
public class PlayerWeaponChangedEventArgs : EventArgs
{
    public WeaponCategory WeaponCategory { get; }  // Unarmed, Melee, Handgun, Shotgun, SMG, Rifle, Sniper, Heavy, Thrown
    public string WeaponHashName { get; }
    public DateTime GameTime { get; }
}
```

Fired only on **transitions** (e.g., unarmed → handgun). Cheap to subscribe to — it doesn't fire every frame.

---

### `INPCContext`

Read-only snapshot of an NPC at the moment the event fired.

```csharp
public interface INPCContext
{
    string UniqueId { get; }          // Persistent across sessions
    string StableId { get; }          // Hash-based, survives game restarts
    int PedHandle { get; }            // GTA ped handle (volatile)
    string Name { get; }
    string Personality { get; }       // Raw personality prompt
    string Archetype { get; }         // Archetype ID, e.g. "biker", "cop"
    bool IsMale { get; }
    int AwakenStage { get; }          // 0 = dormant, 1+ = awakened
    int ThreatLevel { get; }
    int PlayerReputation { get; }     // -100 to +100
    Vector3 Position { get; }
    string ZoneName { get; }
    bool IsWaitingForAI { get; }     // True if request in flight
}
```

> **Note**: `PedHandle` is volatile. If the ped is despawned and respawned, the handle changes but `UniqueId` and `StableId` remain the same.

---

### `ISentienceLogger`

Per-plugin logging with automatic file rotation.

```csharp
public interface ISentienceLogger
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}
```

Logs go to `%Documents%\GTA5MOD2026\logs\plugins.log` (2 MB auto-rotation, thread-safe). Each line is prefixed with timestamp, plugin name, and level.

---

### `SentienceSdk` (Static)

```csharp
public static class SentienceSdk
{
    public static string Version => "1.0.0";
    public static string PluginFolderName => "SentiencePlugins";
    public static string ScenarioFolderName => "Scenarios";
}
```

Use these constants instead of hardcoding paths.

---

## Project Skeleton

Here's a fully functional project layout:

```
MyCoolPlugin/
├── MyCoolPlugin.csproj
├── MyCoolPlugin.sln
└── src/
    ├── MyPlugin.cs          // ISentiencePlugin implementation
    ├── Config.cs            // (Optional) Your plugin settings
    └── Handlers/
        ├── SpawnHandler.cs  // NPCSpawned logic
        └── WeaponHandler.cs // PlayerWeaponChanged logic
```

### `MyCoolPlugin.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <AssemblyName>MyCoolPlugin</AssemblyName>
    <RootNamespace>MyCoolPlugin</RootNamespace>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="GTA5MOD2026">
      <HintPath>..\..\GTA5MOD2026.dll</HintPath>
    </Reference>
    <Reference Include="ScriptHookVDotNet3">
      <HintPath>..\..\ScriptHookVDotNet3.dll</HintPath>
    </Reference>
    <Reference Include="System.Windows.Forms" />
  </ItemGroup>

</Project>
```

> **Do NOT** reference `GTA5MOD2026` as a project reference. Use a binary reference to the compiled DLL. This prevents circular dependencies and keeps your plugin buildable standalone.

---

## Events Deep Dive

### Best Practices

1. **Always unsubscribe in `OnUnload`.** Leaked event handlers = memory leaks = crashes.
2. **Never block the thread.** Event handlers run on the game thread. If you need heavy work, queue it to a background thread.
3. **Catch your own exceptions.** The hub removes throwing handlers permanently. Wrap risky code in `try/catch`.
4. **Use `ExtraSystemPrompt` sparingly.** Every plugin can write to it. Keep your additions concise. The LLM has a token limit.

### Advanced: Modifying AI Behavior

The most powerful hook is `NPCRequestingAI`. Here's a full example:

```csharp
public class WeatherMoodPlugin : ISentiencePlugin
{
    public string Name => "Weather Mood";
    public string Author => "WeatherGang";
    public string Version => "1.0.0";
    public string MinSdkVersion => "1.0.0";

    private IPluginContext _ctx;

    public void OnLoad(IPluginContext ctx)
    {
        _ctx = ctx;
        _ctx.Events.NPCRequestingAI += OnNpcRequestingAI;
    }

    public void OnUnload()
    {
        _ctx.Events.NPCRequestingAI -= OnNpcRequestingAI;
    }

    private void OnNpcRequestingAI(object sender, NPCRequestingAIEventArgs e)
    {
        var weather = World.Weather;
        string mood = weather switch
        {
            Weather.Raining or Weather.ThunderStorm => "It's pouring rain. NPCs are miserable and irritable.",
            Weather.Clearing or Weather.Clouds => "The sky is grey. NPCs are contemplative and subdued.",
            Weather.Smog => "The air is thick with smog. NPCs are coughing and anxious.",
            _ => null
        };

        if (mood != null)
            e.ExtraSystemPrompt += $"\n{mood}";
    }
}
```

Install this plugin, change the weather in-game, and watch NPCs literally become grumpy when it rains. That's the power of the SDK.

---

## Scenario JSON Format

Don't want to write C#? Write JSON instead.

Scenarios are declarative behavior scripts. Drop them in:
```
%USERPROFILE%\Documents\GTA5MOD2026\Scenarios\*.json
```

### Schema

```json
{
  "id": "traffic_stop",
  "name": "Traffic Stop Panic",
  "version": "1.0.0",
  "enabled": true,
  "triggers": [
    {
      "type": "PedArchetype",
      "value": "civilian_nervous"
    },
    {
      "type": "PlayerWeapon",
      "value": "Handgun"
    }
  ],
  "systemPromptAddendum": "You are a nervous civilian who just got pulled over. You have something illegal in the trunk. You are sweating and stuttering.",
  "lines": [
    {
      "condition": "player_approached",
      "text": "O-oh, officer... is there a problem?|nervous|fear|evasive",
      "weight": 1.0
    },
    {
      "condition": "player_threatened",
      "text": "Please, I don't want any trouble!|hands_up|panic|surrender",
      "weight": 2.0
    }
  ],
  "actions": [
    {
      "trigger": "on_dialogue",
      "command": "play_anim",
      "params": { "anim": "hands_up" }
    }
  ],
  "cooldownSeconds": 30.0
}
```

### Trigger Types

| Type | Value Example | Fires When... |
|---|---|---|
| `Always` | `""` | Every eligible NPC |
| `PedArchetype` | `"biker"` | NPC's archetype matches |
| `PedModelHash` | `"0x1234ABCD"` | NPC model hash matches |
| `Hotkey` | `"F6"` | Player presses key |
| `ZoneName` | `"DESRT"` | NPC is in zone |
| `PlayerWeapon` | `"Handgun"` | Player holds weapon category |

Multiple triggers are **AND**-ed together. All must match.

### How It Works

When an NPC spawns, the scenario engine evaluates triggers. If matched, the scenario's `systemPromptAddendum` is appended to the base system prompt. This temporarily reshapes the NPC's personality for the duration of the interaction.

---

## Archetype INI Format

Want to override built-in archetypes or add your own? Create:
```
%USERPROFILE%\Documents\GTA5MOD2026\archetype_voices.ini
```

### Format

```ini
; Comments start with semicolon
; Each section is an archetype ID

[biker]
Personality=You are a ruthless biker gang member. You hate cops and love anarchy. You speak in short, aggressive sentences.
Voice=YunjianNeural
SpeakingRate=1.2
WalkStyles=swagger,tough

[hipster]
Personality=You are an irony-poisoned hipster who only listens to vinyl and judges everyone's taste. You are passive-aggressive.
Voice=YunxiNeural
SpeakingRate=1.0
WalkStyles=arrogant,hipster

; Ped hash overrides — force specific models to use an archetype
[PedHashOverrides]
0x1234ABCD=biker
0x5678EFGH=hipster
```

### Fields

| Field | Required | Description |
|---|---|---|
| `Personality` | Yes | The system prompt fragment defining this archetype |
| `Voice` | Yes | TTS voice name (edge-tts compatible) |
| `SpeakingRate` | No | Speed multiplier (0.5 = slow, 2.0 = fast) |
| `WalkStyles` | No | Comma-separated walkstyle names. One is chosen at random on spawn. |

### Ped Hash Overrides

Use `[PedHashOverrides]` to force specific ped models to specific archetypes regardless of the default mapping. Great for ensuring your favorite model always acts a certain way.

---

## Building & Deploying

### Build

```bash
cd MyCoolPlugin
dotnet build -c Release
```

Output: `bin/Release/net48/MyCoolPlugin.dll`

### Deploy

Copy to:
```
<GTA V>\scripts\SentiencePlugins\MyCoolPlugin.dll
```

**No restart required** — Sentience scans the plugin folder periodically and loads new DLLs at runtime. (Unloading requires a game restart, because .NET Framework doesn't support assembly unloading.)

### Debug

Check `%USERPROFILE%\Documents\GTA5MOD2026\logs\plugins.log` for your plugin's output.

If your plugin crashes on load, the log will show the exception and quarantine decision.

---

## Compatibility & Versioning

### SemVer

The SDK follows strict SemVer:

- **Patch** (1.0.0 → 1.0.1): Bug fixes. Plugins compiled against 1.0.0 work without recompilation.
- **Minor** (1.0.0 → 1.1.0): New features, backward-compatible. Old plugins work. New plugins may use new APIs.
- **Major** (1.0.0 → 2.0.0): Breaking changes. Plugins must be recompiled.

**We will do everything in our power to never ship a major SDK version.** The mod can rev to V6, V7, V8 — but if your plugin compiled against SDK 1.0, it should keep working.

### Plugin Version Check

When loading, the mod checks:
```
plugin.MinSdkVersion <= host.SdkVersion
```

If your plugin says `MinSdkVersion = "1.1.0"` but the host is still on `1.0.0`, your plugin is **rejected** with a log message. This prevents crashes from missing APIs.

### Quarantine System

If a plugin throws during:
- `OnLoad`
- Any event handler
- Any unexpected callback

It accumulates "strikes." After **3 strikes**, the plugin is **quarantined**:
- All its event handlers are removed
- It receives no further events
- It remains in memory but is effectively dead

This protects the mod from bad plugins. **Don't be a bad plugin.** Catch your exceptions.

---

## FAQ

**Q: Can I sell my plugin?**

A: The SDK is MIT-licensed. Your plugin is yours. Sell it, give it away, print it on a t-shirt. We only ask that you don't claim you built Sentience itself.

**Q: Can my plugin spawn vehicles / weapons / peds?**

A: Yes — you have full SHVDN3 access via `GTA` namespace. But be careful. Spawning 100 cars in `OnTick` will crash the game. Use timers and limits.

**Q: Can my plugin show UI?**

A: Yes — SHVDN3 supports `GTA.UI` and native draws. However, we recommend keeping UI minimal. The F5 menu is the player's sacred space.

**Q: Can two plugins conflict?**

A: They can both write to `ExtraSystemPrompt`, which might create confusing AI behavior. We recommend coordinating via a shared JSON config in `%Documents%\GTA5MOD2026\` if you plan to release plugin packs.

**Q: My plugin won't load. Log says "assembly not found"?**

A: You probably referenced something that isn't in `<GTA V>\scripts\`. All plugin dependencies must either be IL-merged into your DLL or copied to the scripts folder alongside it.

**Q: Can I use NuGet packages?**

A: Yes, but the package DLLs must end up in `<GTA V>\scripts\` or be IL-merged. The game's .NET runtime won't search NuGet caches.

**Q: Will my plugin work in future versions?**

A: If you compiled against SDK 1.0 and we don't ship a breaking SDK change — yes. That's the entire point of the SDK freeze. We take backward compatibility seriously because we respect your time.

---

## Final Words

You now have everything you need to bend Los Santos to your will.

The SDK is small on purpose. The game is already complex — we didn't want to add another framework on top. Write C#. Hook events. Inject prompts. Watch the world react.

**If you build something cool, tell us.** We feature community plugins in release notes. We retweet screenshots. We genuinely get excited when someone takes this little SDK and builds something we never imagined.

> *"The best plugins won't be written by us. They'll be written by you."*

**— The Sentience Team**

---

*Sentience SDK 1.0.0 · GTA5MOD2026 V5.1 Animus · 2026-05-28*
