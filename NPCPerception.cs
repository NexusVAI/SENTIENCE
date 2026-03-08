using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;

namespace GTA5MOD2026
{
    public class PerceptionData
    {
        public int NearbyPedCount;
        public float NearestPedDistance = 999f;
        public bool NearbyPedArmed;
        public bool PlayerVisible;
        public float PlayerDistance = 999f;
        public bool PlayerArmed;
        public bool PlayerAiming;
        public bool PlayerInVehicle;
        public bool PlayerShooting;
        public float PlayerSpeed;
        public string TimeOfDay = "白天";
        public string Weather = "晴天";
        public string ZoneName = "未知";
        public bool HeardGunshots;
        public bool NearbyExplosion;
        public bool SawPedDie;
        public float DangerLevel;
        public Vector3 LastDangerPos;
        public bool EmptyVehicleNearby;
        public float NearestVehicleDistance = 999f;
        public Vehicle NearestVehicle;
        public List<NPCState> NearbyAwakened
            = new List<NPCState>();
        public List<NPCState> NearbyNPCs
            = new List<NPCState>();
    }

    public class NPCPerception
    {
        private static readonly Dictionary<string, WorldEvent>
            _worldEvents = new Dictionary<string, WorldEvent>();

        private static float _lastEventCleanup = 0f;

        public PerceptionData Perceive(NPCState state,
            Ped player, Dictionary<int, NPCState> allNPCs)
        {
            var data = new PerceptionData();
            if (!state.IsValid()) return data;

            Vector3 npcPos = state.Ped.Position;

            if (player != null && player.Exists())
            {
                data.PlayerDistance = Vector3.Distance(
                    npcPos, player.Position);
                data.PlayerVisible = data.PlayerDistance < 50f
                    && HasLineOfSight(state.Ped, player);
                data.PlayerArmed = player.Weapons.Current != null
                    && player.Weapons.Current.Hash
                        != WeaponHash.Unarmed;
                data.PlayerAiming = Function.Call<bool>(
                    Hash.IS_PLAYER_FREE_AIMING,
                    Game.Player.Handle);
                data.PlayerInVehicle = player.IsInVehicle();
                data.PlayerShooting = player.IsShooting;
                data.PlayerSpeed = player.IsInVehicle()
                    ? player.CurrentVehicle?.Speed ?? 0f
                    : 0f;
            }

            Ped[] nearby = World.GetNearbyPeds(
                state.Ped, 25f);
            data.NearbyPedCount = 0;
            int processed = 0;
            const int MAX_PED_SCAN = 15;

            foreach (var ped in nearby)
            {
                if (processed >= MAX_PED_SCAN) break;
                if (ped == null || !ped.Exists()) continue;
                if (ped == state.Ped) continue;

                float dist = Vector3.Distance(
                    npcPos, ped.Position);

                if (ped.IsDead)
                {
                    if (dist < 15f
                        && !state.SeenDeadPeds.Contains(
                            ped.Handle))
                    {
                        data.SawPedDie = true;
                        state.SeenDeadPeds.Add(ped.Handle);
                        if (state.SeenDeadPeds.Count > 200)
                            state.SeenDeadPeds.Clear();
                    }
                    continue;
                }

                processed++;
                data.NearbyPedCount++;

                if (dist < data.NearestPedDistance)
                    data.NearestPedDistance = dist;

                if (ped.Weapons.Current != null
                    && ped.Weapons.Current.Hash
                        != WeaponHash.Unarmed)
                    data.NearbyPedArmed = true;
            }

            foreach (var kvp in allNPCs)
            {
                if (kvp.Key == state.Handle) continue;
                if (!kvp.Value.IsValid()) continue;

                float dist = Vector3.Distance(
                    npcPos, kvp.Value.Ped.Position);
                if (dist < 20f)
                {
                    data.NearbyNPCs.Add(kvp.Value);
                    if (kvp.Value.Stage >= AwakenStage.Aware)
                        data.NearbyAwakened.Add(kvp.Value);
                }
            }

            Vehicle[] vehicles = World.GetNearbyVehicles(
                npcPos, 30f);
            foreach (var veh in vehicles)
            {
                if (veh == null || !veh.Exists()) continue;

                float dist = Vector3.Distance(
                    npcPos, veh.Position);

                bool isEmpty = veh.Driver == null
                    || !veh.Driver.Exists();

                if (isEmpty && dist < data.NearestVehicleDistance)
                {
                    data.NearestVehicleDistance = dist;
                    data.NearestVehicle = veh;
                    data.EmptyVehicleNearby = true;
                }
            }

            int hour = Function.Call<int>(
                Hash.GET_CLOCK_HOURS);
            if (hour >= 6 && hour < 12)
                data.TimeOfDay = "早上";
            else if (hour >= 12 && hour < 18)
                data.TimeOfDay = "下午";
            else if (hour >= 18 && hour < 22)
                data.TimeOfDay = "晚上";
            else
                data.TimeOfDay = "深夜";

            data.ZoneName = GetZoneName(npcPos);
            data.Weather = GetWeather();

            data.HeardGunshots = player != null
                && player.IsShooting
                && data.PlayerDistance < 50f;

            data.NearbyExplosion = Function.Call<bool>(
                Hash.IS_EXPLOSION_IN_AREA,
                -1,
                npcPos.X - 30f, npcPos.Y - 30f,
                npcPos.Z - 10f,
                npcPos.X + 30f, npcPos.Y + 30f,
                npcPos.Z + 10f);

            data.DangerLevel = CalculateDanger(data, state);
            if (data.DangerLevel > 50f && player != null)
                data.LastDangerPos = player.Position;

            return data;
        }

        private float CalculateDanger(PerceptionData data,
            NPCState state)
        {
            float danger = 0f;

            if (data.PlayerShooting) danger += 40f;
            if (data.PlayerAiming) danger += 30f;
            if (data.PlayerArmed && data.PlayerDistance < 10f)
                danger += 20f;
            if (data.HeardGunshots) danger += 25f;
            if (data.NearbyExplosion) danger += 50f;
            if (data.SawPedDie) danger += 35f;
            if (data.NearbyPedArmed) danger += 15f;

            if (data.PlayerInVehicle
                && data.PlayerSpeed > 20f
                && data.PlayerDistance < 15f)
                danger += 20f;

            string zone = data.ZoneName;
            if (_worldEvents.ContainsKey(zone))
            {
                var evt = _worldEvents[zone];
                if (evt.Type == EventType.Shooting)
                    danger += 20f;
                if (evt.Type == EventType.Killing)
                    danger += 40f;
            }

            switch (state.Personality)
            {
                case "暴躁":
                    danger *= 0.5f;
                    break;
                case "胆小":
                    danger *= 1.5f;
                    break;
            }

            return Math.Min(100f, danger);
        }

        public static void RecordWorldEvent(Vector3 position,
            EventType type, float gameTime)
        {
            string zone = GetZoneName(position);

            if (_worldEvents.ContainsKey(zone))
            {
                var existing = _worldEvents[zone];
                existing.Count++;
                existing.LastTime = gameTime;
                if (type > existing.Type)
                    existing.Type = type;
            }
            else
            {
                _worldEvents[zone] = new WorldEvent
                {
                    Zone = zone,
                    Position = position,
                    Type = type,
                    Count = 1,
                    FirstTime = gameTime,
                    LastTime = gameTime
                };
            }
        }

        public static WorldEvent GetZoneEvent(string zone)
        {
            _worldEvents.TryGetValue(zone, out var evt);
            return evt;
        }

        public static Dictionary<string, WorldEvent>
            GetAllEvents() => _worldEvents;

        public static void CleanOldEvents(float gameTime)
        {
            if (gameTime - _lastEventCleanup < 60f) return;
            _lastEventCleanup = gameTime;

            var toRemove = new List<string>();
            foreach (var kvp in _worldEvents)
            {
                if (gameTime - kvp.Value.LastTime > 1800f)
                    toRemove.Add(kvp.Key);
            }
            foreach (var key in toRemove)
                _worldEvents.Remove(key);
        }

        private bool HasLineOfSight(Ped from, Ped to)
        {
            return Function.Call<bool>(
                Hash.HAS_ENTITY_CLEAR_LOS_TO_ENTITY,
                from.Handle, to.Handle, 17);
        }

        public static string GetZoneName(Vector3 pos)
        {
            string zoneName = Function.Call<string>(
                Hash.GET_NAME_OF_ZONE,
                pos.X, pos.Y, pos.Z);

            switch (zoneName)
            {
                case "DOWNT": return "市中心";
                case "BEACH": return "海滩";
                case "AIRP": return "机场";
                case "ROCKF": return "洛圣都码头";
                case "DAVIS": return "戴维斯";
                case "GROVE": return "格罗夫街";
                case "SANDY": return "沙漠";
                case "PALETO": return "帕雷托";
                case "MIRR": return "镜子公园";
                case "VINE": return "藤蔓坞";
                case "ALTA": return "阿尔塔";
                case "DELPE": return "德尔佩罗";
                case "LMESA": return "拉美萨";
                case "KOREAT": return "小首尔";
                case "TEXTI": return "纺织城";
                case "STRAW": return "草莓地";
                case "CHAMH": return "张伯伦山";
                case "MURRI": return "默里埃塔";
                case "WINDF": return "温洛克";
                
                // Missing zones added
                case "SKID": return "贫民区";
                case "LEIG": return "莱赫";
                case "PBOX": return "枕头街";
                case "RANCHO": return "兰乔";
                case "CYPRE": return "赛普里斯";
                case "EBURO": return "东伯班克";
                case "ELGO": return "埃尔戈尔多";
                case "HAWICK": return "哈维克";
                case "BURTON": return "伯顿";
                case "RGLEN": return "里奇曼峡谷";
                case "RICHM": return "里奇曼";
                case "DTVINE": return "藤蔓街";
                case "EAST_V": return "东藤蔓坞";
                case "WVINE": return "西藤蔓坞";
                case "MORN": return "晨木";
                case "GOLF": return "高尔夫球场";
                case "BANlham": return "班纳姆";
                case "TONGVAH": return "通瓦山";
                case "TATAMO": return "塔塔维安山";
                case "NOOSE": return "国安局";
                case "MILBASE": return "军事基地";
                case "PRISON": return "监狱";
                case "PORTOS": return "洛圣都港口";
                case "TERMINA": return "航站楼";
                case "ELYSIAN": return "极乐岛";
                case "ZP_ORT": return "赞库多港";
                case "HUMLAB": return "人道实验室";
                case "CHU": return "朱马什";
                case "CMSW": return "乡村小溪";
                case "LAGO": return "赞库多";
                case "DESRT": return "大沙漠";
                case "PALFOR": return "帕雷托森林";
                case "PROCOB": return "普罗科皮奥海滩";
                case "PALCOV": return "帕雷托湾";
                case "MTCHIL": return "奇力亚德山";
                case "MTGORDO": return "哥多山";
                case "MTJOSE": return "约瑟夫林山";
                case "HARMO": return "和谐镇";
                case "GRAPES": return "葡萄籽";

                default:
                    if (!string.IsNullOrEmpty(zoneName) 
                        && zoneName.Length <= 10)
                        return "洛圣都某处";
                    return "未知区域";
            }
        }

        private string GetWeather()
        {
            switch (World.Weather)
            {
                case Weather.Raining:
                case Weather.ThunderStorm:
                    return "下雨";
                case Weather.Clearing:
                    return "阵雨";
                case Weather.Foggy:
                case Weather.Smog:
                    return "雾天";
                case Weather.Overcast:
                    return "阴天";
                case Weather.Snowing:
                case Weather.Snowlight:
                case Weather.Blizzard:
                    return "下雪";
            }
            return "晴天";
        }
    }

    public class WorldEvent
    {
        public string Zone;
        public Vector3 Position;
        public EventType Type;
        public int Count;
        public float FirstTime;
        public float LastTime;
    }

    public enum EventType
    {
        Normal = 0,
        Speeding = 1,
        Shooting = 2,
        Killing = 3,
        Explosion = 4
    }
}
