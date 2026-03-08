using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GTA;
using GTA.Math;
using Newtonsoft.Json;

namespace GTA5MOD2026
{
    public class MemoryEntry
    {
        public string PlayerAction { get; set; }
        public string NpcResponse { get; set; }
        public string Emotion { get; set; }
        public int ThreatLevel { get; set; }
        public string Time { get; set; }
        public float Timestamp { get; set; }
    }

    public class NPCMemory
    {
        public string StableId { get; set; }
        public string Personality { get; set; }
        public List<MemoryEntry> ShortTerm { get; set; }
            = new List<MemoryEntry>();
        public List<string> LongTerm { get; set; }
            = new List<string>();
        public int TotalInteractions { get; set; } = 0;
        public int TimesAttacked { get; set; } = 0;
        public int TimesFriendly { get; set; } = 0;
        public string PlayerReputation { get; set; } = "stranger";
        public int Relationship { get; set; } = 0;
    }

    public class MemoryManager
    {
        private readonly Dictionary<string, NPCMemory> _memories
            = new Dictionary<string, NPCMemory>();
        private readonly Dictionary<int, string> _handleToStableId
            = new Dictionary<int, string>();

        private const int MAX_SHORT_TERM = 5;
        private const int MAX_LONG_TERM = 10;
        private const int STORAGE_VERSION = 2;

        private static readonly string SaveDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "GTA5MOD2026", "memory");

        public MemoryManager()
        {
            if (!Directory.Exists(SaveDir))
                Directory.CreateDirectory(SaveDir);

            LoadAll();
        }

        public static string MakeStableId(Ped ped)
        {
            return NPCState.MakeStableId(ped);
        }

        public void BindPed(Ped ped, string personality = "冷漠")
        {
            if (ped == null || !ped.Exists())
                return;

            string stableId = MakeStableId(ped);
            _handleToStableId[ped.Handle] = stableId;
            EnsureMemory(stableId, personality);
        }

        public void UnbindHandle(int npcHandle)
        {
            _handleToStableId.Remove(npcHandle);
        }

        public NPCMemory GetMemory(Ped ped, string personality)
        {
            if (ped == null || !ped.Exists())
                return EnsureMemory("INVALID_PED", personality);

            string stableId = MakeStableId(ped);
            _handleToStableId[ped.Handle] = stableId;
            return EnsureMemory(stableId, personality);
        }

        public NPCMemory GetMemory(int npcHandle, string personality)
        {
            string stableId;
            if (!_handleToStableId.TryGetValue(npcHandle, out stableId))
                stableId = $"runtime_{npcHandle}";
            return EnsureMemory(stableId, personality);
        }

        private NPCMemory EnsureMemory(string stableId,
            string personality)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                stableId = "UNKNOWN";

            NPCMemory mem;
            if (!_memories.TryGetValue(stableId, out mem))
            {
                mem = new NPCMemory
                {
                    StableId = stableId,
                    Personality = string.IsNullOrEmpty(personality)
                        ? "冷漠"
                        : personality
                };
                _memories[stableId] = mem;
            }
            else
            {
                mem.StableId = stableId;
                if (!string.IsNullOrEmpty(personality))
                    mem.Personality = personality;
            }
            return mem;
        }

        public void RecordInteraction(Ped ped,
            string playerAction, string npcResponse,
            string emotion, int threat, string time,
            float gameTime)
        {
            var mem = GetMemory(ped, "冷漠");

            RecordInteractionCore(mem, playerAction, npcResponse,
                emotion, threat, time, gameTime);
        }

        public void RecordInteraction(int npcHandle,
            string playerAction, string npcResponse,
            string emotion, int threat, string time,
            float gameTime)
        {
            var mem = GetMemory(npcHandle, "冷漠");

            RecordInteractionCore(mem, playerAction, npcResponse,
                emotion, threat, time, gameTime);
        }

        private void RecordInteractionCore(NPCMemory mem,
            string playerAction, string npcResponse,
            string emotion, int threat, string time,
            float gameTime)
        {
            mem.ShortTerm.Add(new MemoryEntry
            {
                PlayerAction = playerAction,
                NpcResponse = npcResponse,
                Emotion = emotion,
                ThreatLevel = threat,
                Time = time,
                Timestamp = gameTime
            });

            while (mem.ShortTerm.Count > MAX_SHORT_TERM)
            {
                var oldest = mem.ShortTerm[0];
                mem.ShortTerm.RemoveAt(0);
                SummarizeToLongTerm(mem, oldest);
            }

            mem.TotalInteractions++;

            if (threat >= 4)
            {
                mem.TimesAttacked++;
                mem.Relationship = Math.Max(-100,
                    mem.Relationship - 15);
            }
            else if (playerAction == "insult")
            {
                mem.Relationship = Math.Max(-100,
                    mem.Relationship - 10);
            }
            else if (playerAction == "compliment")
            {
                mem.TimesFriendly++;
                mem.Relationship = Math.Min(100,
                    mem.Relationship + 8);
            }
            else if (playerAction == "talk")
            {
                mem.TimesFriendly++;
                mem.Relationship = Math.Min(100,
                    mem.Relationship + 3);
            }

            mem.PlayerReputation = ComputeReputation(mem);
        }

        private void SummarizeToLongTerm(NPCMemory mem,
            MemoryEntry entry)
        {
            string summary;
            if (entry.ThreatLevel >= 4)
                summary = $"玩家曾{entry.PlayerAction}，很危险";
            else if (entry.ThreatLevel >= 2)
                summary = $"玩家曾靠近，有点紧张";
            else
                summary = $"玩家曾友好地{entry.PlayerAction}";

            mem.LongTerm.Add(summary);

            while (mem.LongTerm.Count > MAX_LONG_TERM)
                mem.LongTerm.RemoveAt(0);
        }

        private string ComputeReputation(NPCMemory mem)
        {
            if (mem.Relationship <= -50) return "enemy";
            if (mem.Relationship <= -20) return "hostile";
            if (mem.Relationship <= 10) return "stranger";
            if (mem.Relationship <= 40) return "acquaintance";
            if (mem.Relationship <= 70) return "friend";
            return "close_friend";
        }

        public int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int chinese = 0;
            int other = 0;
            foreach (char c in text)
            {
                if (c >= 0x4e00 && c <= 0x9fff)
                    chinese++;
                else if (c >= 0x3400 && c <= 0x4dbf)
                    chinese++;
                else if (!char.IsWhiteSpace(c))
                    other++;
            }

            return (int)(chinese * 1.5 + other * 0.4);
        }

        public string BuildMemoryContext(Ped ped, int tokenBudget = 150)
        {
            if (ped == null || !ped.Exists())
                return "";

            var mem = GetMemory(ped, "冷漠");
            return BuildMemoryContextInternal(mem, tokenBudget);
        }

        public string BuildMemoryContext(int npcHandle,
            int tokenBudget = 150)
        {
            string stableId;
            if (!_handleToStableId.TryGetValue(npcHandle, out stableId))
                return "";

            NPCMemory mem;
            if (!_memories.TryGetValue(stableId, out mem))
                return "";

            return BuildMemoryContextInternal(mem, tokenBudget);
        }

        private string BuildMemoryContextInternal(NPCMemory mem,
            int tokenBudget)
        {
            var parts = new List<string>();
            int usedTokens = 0;

            AddPartWithBudget(parts, ref usedTokens, tokenBudget,
                $"关系:{mem.PlayerReputation}({mem.Relationship})",
                true);

            if (mem.TimesAttacked > 0)
                AddPartWithBudget(parts, ref usedTokens, tokenBudget,
                    $"被攻击{mem.TimesAttacked}次", false);
            if (mem.TotalInteractions > 3)
                AddPartWithBudget(parts, ref usedTokens, tokenBudget,
                    $"见过{mem.TotalInteractions}次", false);

            var recent = mem.ShortTerm
                .Skip(Math.Max(0, mem.ShortTerm.Count - 2))
                .ToList();

            foreach (var entry in recent)
            {
                if (!AddPartWithBudget(parts, ref usedTokens,
                    tokenBudget,
                    $"上次:玩家{entry.PlayerAction}→你{entry.NpcResponse}",
                    false))
                    break;
            }

            for (int i = mem.LongTerm.Count - 1; i >= 0; i--)
            {
                if (!AddPartWithBudget(parts, ref usedTokens,
                    tokenBudget, $"记忆:{mem.LongTerm[i]}", false))
                    break;
            }

            return string.Join("。", parts);
        }

        private bool AddPartWithBudget(List<string> parts,
            ref int usedTokens, int tokenBudget, string part,
            bool forceAdd)
        {
            if (string.IsNullOrWhiteSpace(part))
                return true;

            int partTokens = EstimateTokens(part) + 2;
            if (!forceAdd
                && tokenBudget > 0
                && usedTokens + partTokens > tokenBudget)
                return false;

            parts.Add(part);
            usedTokens += partTokens;
            return true;
        }

        public void LoadAll()
        {
            try
            {
                string filePath = Path.Combine(
                    SaveDir, "npc_memory.json");
                if (!File.Exists(filePath))
                    return;

                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                    return;

                var root = JsonConvert.DeserializeObject
                    <Dictionary<string, object>>(json);
                if (root != null
                    && root.ContainsKey("memories"))
                {
                    var store = JsonConvert.DeserializeObject<
                        MemoryStore>(json);
                    if (store?.Memories != null)
                    {
                        _memories.Clear();
                        foreach (var kv in store.Memories)
                        {
                            var mem = kv.Value ?? new NPCMemory();
                            mem.StableId = kv.Key;
                            if (string.IsNullOrEmpty(mem.Personality))
                                mem.Personality = "冷漠";
                            _memories[kv.Key] = mem;
                        }
                    }
                    return;
                }

                var stableDict = JsonConvert.DeserializeObject<
                    Dictionary<string, NPCMemory>>(json);
                if (stableDict != null && stableDict.Count > 0)
                {
                    _memories.Clear();
                    foreach (var kv in stableDict)
                    {
                        var mem = kv.Value ?? new NPCMemory();
                        mem.StableId = kv.Key;
                        if (string.IsNullOrEmpty(mem.Personality))
                            mem.Personality = "冷漠";
                        _memories[kv.Key] = mem;
                    }
                    return;
                }

                var legacy = JsonConvert.DeserializeObject<
                    Dictionary<int, NPCMemory>>(json);
                if (legacy != null && legacy.Count > 0)
                {
                    _memories.Clear();
                    foreach (var kv in legacy)
                    {
                        string stableId = $"legacy_{kv.Key}";
                        var mem = kv.Value ?? new NPCMemory();
                        mem.StableId = stableId;
                        if (string.IsNullOrEmpty(mem.Personality))
                            mem.Personality = "冷漠";
                        _memories[stableId] = mem;
                    }
                }
            }
            catch { }
        }
        public void SaveAll()
        {
            try
            {
                var store = new MemoryStore
                {
                    Version = STORAGE_VERSION,
                    SavedAtUtc = DateTime.UtcNow,
                    Memories = _memories
                };
                string json = JsonConvert.SerializeObject(store,
                    Formatting.Indented);
                File.WriteAllText(
                    Path.Combine(SaveDir, "npc_memory.json"), json);
            }
            catch { }
        }

        public void ClearMemory(int npcHandle)
        {
            string stableId;
            if (_handleToStableId.TryGetValue(npcHandle,
                out stableId))
            {
                _memories.Remove(stableId);
                _handleToStableId.Remove(npcHandle);
            }
        }

        public void ClearAll()
        {
            _memories.Clear();
            _handleToStableId.Clear();
        }

        private class MemoryStore
        {
            public int Version { get; set; }
            public DateTime SavedAtUtc { get; set; }
            public Dictionary<string, NPCMemory> Memories
                { get; set; }
                = new Dictionary<string, NPCMemory>();
        }
    }
}
