# Sentience V5.1 · Plugin Development Guide

> *从 V5.1 Animus 起，Sentience 不再只是一个 mod —— 它是一个**平台**。任何人都可以用 C# 或一份 JSON 文件让 GTA5 的 NPC 按你的意志演戏。*

本文面向想要为 Sentience 写扩展的开发者 / mod 作者。读完你能：

- 知道 Sentience 暴露的稳定接口范围（**SDK 1.0**）
- 写一个最小可用的 C# 插件并 drop-in 部署
- 写一个不需要 C# 的 JSON 场景脚本
- 了解错误隔离 / 兼容性 / 调试日志的规则

---

## 📚 目录

- [架构总览](#架构总览)
- [选择你的扩展方式](#选择你的扩展方式)
- [Path A · C# 插件](#path-a--c-插件)
  - [SDK 公开接口清单](#sdk-公开接口清单)
  - [项目骨架](#项目骨架)
  - [事件清单](#事件清单)
  - [构建 & 部署](#构建--部署)
- [Path B · JSON 场景](#path-b--json-场景)
  - [Scenario 完整 schema](#scenario-完整-schema)
  - [触发器类型](#触发器类型)
- [Path C · Archetype 声线 INI](#path-c--archetype-声线-ini)
- [运行时行为 & 故障处理](#运行时行为--故障处理)
- [版本与兼容性](#版本与兼容性)
- [完整示例：PoliceRP 插件](#完整示例policerp-插件)
- [FAQ](#faq)

---

## 架构总览

```
┌─────────────────────────────────────────────────────────────────────┐
│                    GTA5MOD2026.dll (host mod)                       │
│                                                                     │
│  ┌────────────┐   ┌────────────────┐   ┌────────────────────────┐   │
│  │ NPCManager │──▶│ SentienceServices│──▶│ ArchetypeRegistry    │  │
│  │            │   │   (singleton)  │   │  ScenarioLoader        │   │
│  │ OnTick     │   │                │   │  PluginLoader          │   │
│  │ OnKeyDown  │   │  Hub.RaiseXxx  │   │     │                  │   │
│  └────────────┘   └────────────────┘   │     │ reflects         │   │
│                          │             │     ▼                  │   │
│                          │             │  *.dll plugins         │   │
│                          ▼             └────────────────────────┘   │
│                  ISentienceEvents ──────────────┐                   │
└─────────────────────────────────────────────────┼───────────────────┘
                                                  │
            ┌─────────────────────────────────────┴────────────────┐
            │  scripts/SentiencePlugins/YourPlugin.dll              │
            │  implements ISentiencePlugin                          │
            │  subscribes to ctx.Events                             │
            │  appends ExtraSystemPrompt or executes side-effects   │
            └───────────────────────────────────────────────────────┘
```

**关键点**：

- 插件 / 场景 **完全运行在 host 进程内**，没有 IPC、没有沙箱开销。
- 异常会被 host **隔离**：插件 OnLoad 抛 → 立即 quarantine；事件处理抛 → 累计 3 次自动禁用。任何一个插件挂掉都不影响主 mod 与其他插件。
- 主 mod 通过 SDK 单点接入，**主流程零侵入**，所以你可以放心激进地扩展。

---

## 选择你的扩展方式

| 你的目标 | 推荐路径 | 需要会什么 |
|---|---|---|
| 改 NPC 说什么 / 怎么反应 / 加新动作 | **A · C# 插件** | C# 基础 + .NET Framework 4.8 |
| 写一个"剧本"（盘查、抢劫、表白、酒吧搭讪） | **B · JSON 场景** | 会写 JSON 就行 |
| 让某类 NPC 用不同声音 / 不同人格说话 | **C · Archetype INI** | 改 INI 文件 |

三条路径可以叠加。一个高级 mod 可能同时使用全部三种。

---

## Path A · C# 插件

### SDK 公开接口清单

`GTA5MOD2026.SDK` 命名空间是**唯一**对插件稳定承诺的 API 面。一旦发布即冻结，只新增、不破坏。

| 类型 | 角色 |
|---|---|
| `ISentiencePlugin` | 你必须实现的入口接口 |
| `IPluginContext` | host 在 `OnLoad` 时塞给你的服务包 |
| `ISentienceEvents` | 事件中心，订阅 `+=` / 取消 `-=` |
| `ISentienceLogger` | 写 `%Documents%\GTA5MOD2026\logs\plugins.log` |
| `INPCContext` | 只读 NPC 快照（事件里拿到的） |
| `SentienceSdk` | 静态常量：`Version`、`MinSupportedPluginSdk`、`PluginFolderName` |

⚠️ **不要 reflect 进 host 内部类型**。比如 `NPCState`、`AIManager`、`MemoryManager` 都是 internal-style 实现细节，下个版本可能改名或重写。

### 项目骨架

```xml
<!-- YourPlugin.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>7.3</LangVersion>
    <AssemblyName>YourPlugin</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="GTA5MOD2026">
      <!-- host mod DLL 必须能被解析；只读引用，不复制到输出 -->
      <HintPath>..\..\GTA5MOD2026\bin\Release\net48\GTA5MOD2026.dll</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>
</Project>
```

```csharp
using System;
using GTA5MOD2026.SDK;

namespace YourMod.Plugins
{
    public sealed class MyPlugin : ISentiencePlugin
    {
        public string Name          => "MyPlugin";
        public string Author        => "你的名字";
        public string Version       => "1.0.0";
        public string MinSdkVersion => "1.0.0";

        private IPluginContext _ctx;

        public void OnLoad(IPluginContext context)
        {
            _ctx = context;
            _ctx.Logger.Info("Hello from MyPlugin!");
            _ctx.Events.NPCRequestingAI += OnNpcRequesting;
        }

        public void OnUnload()
        {
            _ctx.Events.NPCRequestingAI -= OnNpcRequesting;
        }

        private void OnNpcRequesting(object sender,
            NPCRequestingAIEventArgs e)
        {
            // 给某个 archetype 加情绪偏置
            if (e.Npc.Archetype == "biker")
                e.ExtraSystemPrompt = "你今天心情极差，对任何提问都要敷衍。";
        }
    }
}
```

### 事件清单

| 事件 | 何时触发 | 可写字段 |
|---|---|---|
| `NPCSpawned` | NPC 进入感知半径并创建 state | — |
| `NPCDespawned` | NPC 超出半径或被销毁 | — |
| `NPCRequestingAI` | LLM 请求即将发出 | **`ExtraSystemPrompt`** — 你的字符串会拼到 system prompt 末尾 |
| `NPCDialogue` | LLM 回复已解析、动作即将执行 | — |
| `PlayerInteraction` | 玩家按 G / H / T / J | — |
| `PlayerWeaponChanged` | 玩家拔枪 / 收枪 | — |

所有事件参数都继承 `SentienceEventArgs`：
- `.GameTime` — 当前游戏秒
- `.Handled` — 暂未消费，留作 v1.x 扩展

### 构建 & 部署

```powershell
dotnet build YourPlugin.csproj -c Release
# 拷贝输出到 GTA 安装目录
copy bin\Release\net48\YourPlugin.dll "D:\Games\GTA V\scripts\SentiencePlugins\"
```

下次启动 Sentience（重启 GTA），按 **F5 → 插件 & 场景** 可以看到你的插件。

---

## Path B · JSON 场景

文件放到：`%USERPROFILE%\Documents\GTA5MOD2026\Scenarios\*.json`

### Scenario 完整 schema

```json
{
  "id":          "my_scenario",
  "name":        "我的场景",
  "author":      "你的名字",
  "description": "做什么的",
  "enabled":     true,

  "trigger": {
    "type":              "ped_archetype",
    "archetypes":        ["biker", "punk"],
    "modelHashes":       ["0x12345678"],
    "zones":             ["VINEWD"],
    "hotkey":            "G",
    "playerInVehicle":   false,
    "playerArmed":       true,
    "minDistanceMeters": 0.0,
    "maxDistanceMeters": 8.0
  },

  "systemPromptAppend": "【场景：xxx】\n你应该...",

  "lines": [
    { "from": "player", "text": "示例台词" },
    { "from": "npc",    "text": "..." }
  ],

  "actions": [
    { "name": "show_id", "trigger": "驾照", "ai": false }
  ]
}
```

最少必填：`id` + `trigger` + `systemPromptAppend`。其余字段都可省略。

### 触发器类型

| `trigger.type` | 含义 | 关键字段 |
|---|---|---|
| `always` | 所有交互都注入 | — |
| `ped_archetype` | 对方 archetype 命中列表 | `archetypes` |
| `ped_model_hash` | 对方 ped 模型 hash 命中 | `modelHashes` |
| `hotkey` | 玩家按了某个键 | `hotkey` |
| `zone_name` | 玩家所在 LSPDFR 区域 | `zones` |
| `player_weapon` | 玩家持枪状态 | `playerArmed` |

所有类型都额外受 `playerInVehicle / playerArmed / minDistanceMeters / maxDistanceMeters` 全局过滤。

---

## Path C · Archetype 声线 INI

文件放到（两个位置任选其一，前者优先）：

1. `%USERPROFILE%\Documents\GTA5MOD2026\archetype_voices.ini`
2. `<GTA V>\scripts\archetype_voices.ini`

### 段落格式

```ini
[biker]
DisplayName       = 机车党
PersonalityPrompt = 你是 Lost 摩托车帮成员，敌视警察。
MaleVoice         = zh-CN-YunjianNeural
FemaleVoice       = zh-CN-XiaoyanNeural
SpeakingRate      = default
Walkstyles        = move_m@brave, move_m@swagger

[PedHashOverrides]
0x12345678 = biker
0xABCDEF01 = hipster
```

可覆盖的 archetype id 见 `Voices/BuiltinArchetypes.cs`，包括：`civilian`、`wealthy`、`business`、`hollywood`、`biker`、`gang_hood`、`gang_latino`、`punk`、`dealer`、`hipster`、`hippie`、`skater`、`hillbilly`、`farmer`、`meth`、`homeless`、`beach`、`beach_muscle`、`lifeguard`、`construction`、`trucker`、`bartender`、`police`、`medical`、`firefighter`、`elderly`、`tourist`、`fitness`。

---

## 运行时行为 & 故障处理

| 情况 | host 行为 |
|---|---|
| 插件 DLL 加载失败 | 跳过，日志记录，继续加载其他 |
| 插件 `OnLoad` 抛异常 | 立即 quarantine，事件订阅清空 |
| 事件 handler 抛异常 | 累计 fault；3 次后 quarantine 该插件 |
| 场景 JSON 解析失败 | 跳过该文件，日志记录 |
| `MinSdkVersion` 高于 host | 标记 `IncompatibleSdk`，不调用 OnLoad |
| 同一 scenario id 出现多次 | 保留第一个，警告 |

所有信息都写到 `%Documents%\GTA5MOD2026\logs\plugins.log`（自动 2MB 轮转）。F5 → 插件 & 场景 子菜单也能看到每个插件的状态。

---

## 版本与兼容性

- **SDK 版本**：`SentienceSdk.Version` 当前是 `"1.0.0"`。
- **插件兼容性规则**：你的 `MinSdkVersion` 不能高于 host 的 `Version`，否则拒绝加载。
- **承诺**：v1.x 期间，`GTA5MOD2026.SDK` 命名空间只新增成员，不改签名、不删类型。
- **跨小版本编译**：针对 1.0 编译的插件，能在 host 1.0、1.1、1.2... 上跑。反之不保证。

---

## 完整示例：PoliceRP 插件

仓库自带 `samples/Sentience.Plugins.PoliceRP/`：

- `Sentience.Plugins.PoliceRP.csproj` — 项目文件
- `PoliceRPPlugin.cs` — 约 100 行，展示三个事件订阅 + 条件 prompt 注入

行为：

- 玩家拔枪 → 标记此后开始请求 AI 的 NPC 为「受惊」
- 平民：注入 prompt → 倾向 `call_cops` / `flee` / `fear`
- 警察 archetype：相反，倾向 `aim` / `attack`
- 玩家收枪 → 清空全部受惊标记，回归正常

---

## FAQ

<details>
<summary><b>Q：插件可以调用 SHVDN3 / ScriptHookV native 吗？</b></summary>

可以。所有事件都在 GTA 主线程上回调，`Function.Call(Hash.XXX, ...)` 直接用就行。但记得**别阻塞**——长任务用 `Task.Run`，结果回到主线程再调 GTA API。

</details>

<details>
<summary><b>Q：如何在多个插件之间共享数据？</b></summary>

v1.0 没有内置跨插件 IPC。临时方案：插件双方都引用一个共享的 contract 程序集（你自己定义的接口），或者用 `IPluginContext.PluginDataDirectory` 做文件交换。后续 SDK 可能引入 `ISharedRegistry`。

</details>

<details>
<summary><b>Q：场景 JSON 想引用其他场景怎么办？</b></summary>

v1.0 不支持引用 / include。建议把通用 prompt 段抄进每个 JSON。下版本会加 `extends` 字段。

</details>

<details>
<summary><b>Q：插件能修改 NPC 的 personality / archetype 吗？</b></summary>

`INPCContext` 是只读的。要让 NPC 长期变化，建议通过 `ExtraSystemPrompt` 提供"行为指导"，让 LLM 自然演化。强力修改（直接改 personality 字段）属于 host 私有 API，未开放。

</details>

<details>
<summary><b>Q：插件 DLL 体积太大怎么办？</b></summary>

Sentience 已经携带了 Newtonsoft.Json、NAudio 等常见依赖。插件只需要引用 `GTA5MOD2026.dll` 一个就够，自己别带重复的。

</details>

---

**有问题来这里：** <https://github.com/NexusVAI/SENTIENCE/discussions>
**贡献 PR：** <https://github.com/NexusVAI/SENTIENCE/pulls>
