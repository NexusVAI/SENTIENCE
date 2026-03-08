using GTA;
using GTA.Math;
using System;
using System.Collections.Generic;

namespace GTA5MOD2026
{
    public class NPCGoal
    {
        public string Name;
        public GoalType Type;
        public Vector3 Destination;
        public float TimeStarted;
        public float TimeLimit = 120f;
        public bool Completed;
        public bool Failed;

        public bool IsExpired(float gameTime)
        {
            return (gameTime - TimeStarted) > TimeLimit;
        }
    }

    public enum GoalType
    {
        Wander,
        GoToLocation,
        DriveToLocation,
        FindSomeoneToTalk,
        FollowPlayer,
        FleeFromArea,
        FindCover,
        Investigate,
        StandGuard,
        Rest
    }

    public class NPCGoalManager
    {
        private readonly Random _rand = new Random();
        private static readonly Random _staticRand
            = new Random();

        private static readonly Dictionary<string, Vector3>
            LOCATIONS = new Dictionary<string, Vector3>
        {
            ["海滩"] = new Vector3(-1184f, -1510f, 4f),
            ["市中心"] = new Vector3(215f, -800f, 30f),
            ["码头"] = new Vector3(-1600f, -1050f, 13f),
            ["公园"] = new Vector3(-1065f, -490f, 36f),
            ["医院"] = new Vector3(300f, -585f, 43f),
            ["警察局"] = new Vector3(440f, -980f, 30f),
            ["加油站"] = new Vector3(-70f, -1761f, 29f),
            ["便利店"] = new Vector3(25f, -1345f, 29f),
        };

        public NPCGoal CreateGoal(NPCState state,
            PerceptionData perception, float gameTime)
        {
            string urgentNeed =
                state.Needs.GetMostUrgentNeed();

            switch (urgentNeed)
            {
                case "safety":
                    return CreateSafetyGoal(
                        state, perception, gameTime);
                case "social":
                    return CreateSocialGoal(
                        state, perception, gameTime);
                case "curiosity":
                    return CreateCuriosityGoal(
                        state, perception, gameTime);
                case "aggression":
                    return CreateAggressionGoal(
                        state, perception, gameTime);
                case "purpose":
                default:
                    return CreatePurposeGoal(
                        state, perception, gameTime);
            }
        }

        private NPCGoal CreateSafetyGoal(NPCState state,
            PerceptionData perception, float gameTime)
        {
            if (perception.DangerLevel > 60f)
            {
                return new NPCGoal
                {
                    Name = "逃离危险区域",
                    Type = GoalType.FleeFromArea,
                    Destination = GetSafePosition(
                        state.Ped.Position,
                        perception.LastDangerPos),
                    TimeStarted = gameTime,
                    TimeLimit = 30f
                };
            }

            return new NPCGoal
            {
                Name = "找安全的地方",
                Type = GoalType.GoToLocation,
                Destination = GetNearestSafeLocation(
                    state.Ped.Position),
                TimeStarted = gameTime,
                TimeLimit = 60f
            };
        }

        private NPCGoal CreateSocialGoal(NPCState state,
            PerceptionData perception, float gameTime)
        {
            if (perception.NearbyNPCs.Count > 0)
            {
                var target = perception.NearbyNPCs[
                    _rand.Next(perception.NearbyNPCs.Count)];
                return new NPCGoal
                {
                    Name = $"和{target.NpcName}聊天",
                    Type = GoalType.FindSomeoneToTalk,
                    Destination = target.Ped.Position,
                    TimeStarted = gameTime,
                    TimeLimit = 30f
                };
            }

            if (perception.PlayerVisible
                && perception.PlayerDistance < 20f
                && state.Personality != "冷漠")
            {
                return new NPCGoal
                {
                    Name = "和玩家打招呼",
                    Type = GoalType.FindSomeoneToTalk,
                    Destination = Game.Player.Character.Position,
                    TimeStarted = gameTime,
                    TimeLimit = 20f
                };
            }

            return new NPCGoal
            {
                Name = "去人多的地方",
                Type = GoalType.GoToLocation,
                Destination = GetCrowdedArea(
                    state.Ped.Position),
                TimeStarted = gameTime,
                TimeLimit = 90f
            };
        }

        private NPCGoal CreateCuriosityGoal(NPCState state,
            PerceptionData perception, float gameTime)
        {
            if (perception.HeardGunshots
                && state.Personality != "胆小")
            {
                return new NPCGoal
                {
                    Name = "调查枪声",
                    Type = GoalType.Investigate,
                    Destination = perception.LastDangerPos,
                    TimeStarted = gameTime,
                    TimeLimit = 30f
                };
            }

            if (perception.PlayerVisible)
            {
                return new NPCGoal
                {
                    Name = "跟着那个人看看",
                    Type = GoalType.FollowPlayer,
                    Destination = Game.Player.Character.Position,
                    TimeStarted = gameTime,
                    TimeLimit = 45f
                };
            }

            var locations = new List<string>(
                LOCATIONS.Keys);
            string dest = locations[
                _rand.Next(locations.Count)];

            return new NPCGoal
            {
                Name = $"去{dest}看看",
                Type = GoalType.GoToLocation,
                Destination = LOCATIONS[dest],
                TimeStarted = gameTime,
                TimeLimit = 120f
            };
        }

        private NPCGoal CreateAggressionGoal(NPCState state,
            PerceptionData perception, float gameTime)
        {
            if (perception.PlayerVisible
                && perception.PlayerDistance < 15f)
            {
                return new NPCGoal
                {
                    Name = "找那个人麻烦",
                    Type = GoalType.FindSomeoneToTalk,
                    Destination = Game.Player.Character.Position,
                    TimeStarted = gameTime,
                    TimeLimit = 20f
                };
            }

            return new NPCGoal
            {
                Name = "巡视地盘",
                Type = GoalType.Wander,
                Destination = state.Ped.Position,
                TimeStarted = gameTime,
                TimeLimit = 60f
            };
        }

        private NPCGoal CreatePurposeGoal(NPCState state,
            PerceptionData perception, float gameTime)
        {
            string[] goals;
            switch (state.Personality)
            {
                case "友善":
                    goals = new[] { "散步", "去公园", "逛街", "去海滩" };
                    break;
                case "暴躁":
                    goals = new[] { "巡视地盘", "找酒喝", "闲逛", "锻炼" };
                    break;
                case "胆小":
                    goals = new[] { "找安全的地方", "回家", "躲起来" };
                    break;
                case "搞笑":
                    goals = new[] { "去海滩玩", "找乐子", "到处逛逛", "找人聊天" };
                    break;
                case "冷漠":
                    goals = new[] { "独自散步", "发呆", "无所事事" };
                    break;
                default:
                    goals = new[] { "闲逛" };
                    break;
            }

            string goalName = goals[
                _rand.Next(goals.Length)];

            GoalType type = GoalType.Wander;
            Vector3 dest = state.Ped.Position;

            if (goalName.Contains("海滩"))
            {
                type = GoalType.GoToLocation;
                dest = LOCATIONS["海滩"];
            }
            else if (goalName.Contains("公园"))
            {
                type = GoalType.GoToLocation;
                dest = LOCATIONS["公园"];
            }
            else if (goalName.Contains("散步")
                || goalName.Contains("逛"))
            {
                type = GoalType.Wander;
                dest = GetRandomNearbyPoint(
                    state.Ped.Position, 80f);
            }
            else if (goalName.Contains("发呆")
                || goalName.Contains("躲"))
            {
                type = GoalType.Rest;
            }

            return new NPCGoal
            {
                Name = goalName,
                Type = type,
                Destination = dest,
                TimeStarted = gameTime,
                TimeLimit = 90f
            };
        }

        private Vector3 GetSafePosition(Vector3 from,
            Vector3 danger)
        {
            Vector3 direction = from - danger;
            if (direction.Length() < 0.1f)
                direction = new Vector3(1, 0, 0);
            direction.Normalize();
            return from + direction * 50f;
        }

        private Vector3 GetNearestSafeLocation(Vector3 pos)
        {
            float nearest = float.MaxValue;
            Vector3 result = pos;

            foreach (var loc in LOCATIONS)
            {
                float dist = Vector3.Distance(
                    pos, loc.Value);
                if (dist < nearest && dist > 20f)
                {
                    nearest = dist;
                    result = loc.Value;
                }
            }
            return result;
        }

        private Vector3 GetCrowdedArea(Vector3 pos)
        {
            return LOCATIONS["市中心"];
        }

        public static Vector3 GetRandomNearbyPoint(
            Vector3 center, float radius)
        {
            float angle = (float)(_staticRand.NextDouble()
                * Math.PI * 2);
            float dist = (float)(_staticRand.NextDouble()
                * radius);

            Vector3 point = center + new Vector3(
                (float)Math.Cos(angle) * dist,
                (float)Math.Sin(angle) * dist,
                0f);

            float groundZ = World.GetGroundHeight(
                new Vector2(point.X, point.Y));
            if (groundZ > 0)
                point.Z = groundZ;

            return point;
        }
    }
}
