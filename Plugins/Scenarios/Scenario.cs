// =============================================================================
//  Scenario · Data Model
// -----------------------------------------------------------------------------
//  A scenario is a JSON-defined NPC interaction script that any community
//  author can write without C#.  Drop a *.json file into
//  %Documents%/GTA5MOD2026/Scenarios/ and Sentience picks it up on startup.
//
//  Schema (kept intentionally small for v1):
//
//  {
//      "id":          "traffic_stop",         // unique slug
//      "name":        "交通盘查",              // display name
//      "author":      "NexusV",
//      "description": "玩家面向坐车里的 NPC ...",
//      "trigger": {
//          "type":      "ped_archetype",     // see TriggerType enum
//          "archetypes": ["wealthy", "business"],
//          "playerInVehicle": false,
//          "minDistanceMeters": 0.0,
//          "maxDistanceMeters": 4.0,
//          "playerArmed":  false,
//          "hotkey":      "G"                // optional: pressed key
//      },
//      "systemPromptAppend":
//          "你是一名被警察拦下的市民。配合或敌意取决于你的人格。",
//      "lines": [
//          { "from": "player", "text": "靠边停车 / Pull over!" },
//          { "from": "npc",    "text": "..." }
//      ],
//      "actions": [
//          { "name": "request_id", "trigger": "驾照", "ai": true }
//      ]
//  }
//
//  Forwards-compatible: unknown fields are ignored by the loader.
// =============================================================================
using System.Collections.Generic;

namespace GTA5MOD2026.Plugins.Scenarios
{
    public enum TriggerType
    {
        Always = 0,           // active for every interaction
        PedArchetype = 1,     // matches when archetype in list
        PedModelHash = 2,     // matches when ped model in list
        Hotkey = 3,           // pressed a specific G/H/T/J/F-key
        ZoneName = 4,         // matches when in named LSPDFR zone
        PlayerWeapon = 5      // matches when player armed/unarmed
    }

    public sealed class ScenarioTrigger
    {
        public TriggerType Type { get; set; } = TriggerType.Always;
        public List<string> Archetypes { get; set; } = new List<string>();
        public List<string> ModelHashes { get; set; } = new List<string>();
        public List<string> Zones { get; set; } = new List<string>();
        public string Hotkey { get; set; } = "";
        public bool? PlayerInVehicle { get; set; }
        public bool? PlayerArmed { get; set; }
        public float MinDistanceMeters { get; set; } = 0f;
        public float MaxDistanceMeters { get; set; } = 50f;
    }

    public sealed class ScenarioLine
    {
        public string From { get; set; } = "";   // "npc" | "player"
        public string Text { get; set; } = "";
    }

    public sealed class ScenarioAction
    {
        public string Name { get; set; } = "";
        public string Trigger { get; set; } = "";
        public bool AI { get; set; }
    }

    public sealed class Scenario
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public string SourceFile { get; set; } = "";
        public bool   Enabled { get; set; } = true;

        public ScenarioTrigger Trigger { get; set; } = new ScenarioTrigger();
        public string SystemPromptAppend { get; set; } = "";
        public List<ScenarioLine> Lines { get; set; } = new List<ScenarioLine>();
        public List<ScenarioAction> Actions { get; set; } = new List<ScenarioAction>();

        public override string ToString()
            => $"Scenario[{Id} \"{Name}\" by {Author}]";
    }
}
