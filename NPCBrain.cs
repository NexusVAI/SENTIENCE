using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;

namespace GTA5MOD2026
{
    public enum NPCAction
    {
        Idle,
        Wander,
        WalkToPoint,
        RunToPoint,
        FindCover,
        Flee,
        FleeFromDanger,
        TalkToNearby,
        TalkToPlayer,
        ApproachPlayer,
        FollowPlayer,
        Wave,
        AvoidPlayer,
        Fight,
        Investigate,
        LookAround,
        EnterVehicle,
        DriveAround,
        ExitVehicle,
        PickNewGoal,
        ContinueGoal,
        Rest
    }

    public class NPCBrain
    {
        private readonly Random _rand = new Random();
        private readonly NPCGoalManager _goalManager
            = new NPCGoalManager();

        public NPCAction DecideAction(NPCState state,
            PerceptionData perception)
        {
            if (state.IsVehicleStolen && (Game.GameTime / 1000f - state.VehicleStolenTime) < 60f)
            {
                // Immediate reaction to vehicle theft
                switch (state.Personality)
                {
                    case "暴躁":
                        return NPCAction.Fight;
                    case "胆小":
                        return NPCAction.Flee;
                    case "冷漠":
                        return NPCAction.LookAround;
                    default:
                        // Yell at player
                        return NPCAction.TalkToPlayer;
                }
            }

            if (perception.PlayerVisible
                && perception.PlayerDistance < 10f)
            {
                switch (state.Personality)
                {
                    case "暴躁":
                        if (state.PlayerReputation < -30f)
                            return NPCAction.Fight;
                        break;
                    case "胆小":
                        if (state.PlayerReputation < -40f)
                            return NPCAction.Flee;
                        return NPCAction.AvoidPlayer;
                    case "冷漠":
                        if (state.PlayerReputation < -80f)
                            return NPCAction.Fight;
                        break;
                    case "友善":
                        if (state.PlayerReputation < -90f)
                            return NPCAction.Fight;
                        break;
                    case "搞笑":
                        if (state.PlayerReputation < -70f
                            && _rand.Next(100) < 40)
                            return NPCAction.Fight;
                        break;
                }
            }

            if (perception.DangerLevel > 80f)
            {
                if (state.Personality == "暴躁"
                    && state.Needs.Aggression > 60f)
                    return NPCAction.Fight;

                if (state.Personality == "胆小"
                    || state.Needs.Safety < 20f)
                    return NPCAction.Flee;

                return NPCAction.FindCover;
            }

            if (state.KnowsPlayerIsDangerous
                && perception.PlayerVisible)
            {
                float dist = perception.PlayerDistance;

                if (state.SawPlayerKill && dist < 30f)
                {
                    if (state.Personality == "暴躁")
                        return dist < 10f
                            ? NPCAction.Fight
                            : NPCAction.LookAround;
                    if (state.Personality == "胆小")
                        return NPCAction.Flee;
                    return dist < 15f
                        ? NPCAction.AvoidPlayer
                        : NPCAction.LookAround;
                }

                if (state.SawPlayerShoot && dist < 20f)
                {
                    if (state.Personality != "暴躁")
                        return NPCAction.AvoidPlayer;
                }

                if (state.SawPlayerSpeeding
                    && perception.PlayerInVehicle
                    && perception.PlayerSpeed > 15f
                    && dist < 15f)
                {
                    return NPCAction.AvoidPlayer;
                }
            }

            if (perception.HeardGunshots)
            {
                switch (state.Personality)
                {
                    case "胆小":
                        return NPCAction.Flee;
                    case "暴躁":
                        return NPCAction.Investigate;
                    case "搞笑":
                        return NPCAction.LookAround;
                    default:
                        if (perception.DangerLevel > 40f)
                            return NPCAction.FindCover;
                        return NPCAction.LookAround;
                }
            }

            if (perception.SawPedDie)
            {
                if (state.Personality == "胆小")
                    return NPCAction.Flee;
                if (state.Personality == "冷漠")
                    return NPCAction.LookAround;
                return NPCAction.AvoidPlayer;
            }

            if (perception.NearbyNPCs.Count > 0
                && perception.DangerLevel < 30f
                && !state.IsInteracting)
            {
                int roll = _rand.Next(100);
                int threshold;
                switch (state.Personality)
                {
                    case "友善":
                        threshold = 35;
                        break;
                    case "搞笑":
                        threshold = 25;
                        break;
                    case "暴躁":
                        threshold = 8;
                        break;
                    case "冷漠":
                        threshold = 3;
                        break;
                    case "胆小":
                        threshold = 6;
                        break;
                    default:
                        threshold = 15;
                        break;
                }
                if (roll < threshold)
                    return NPCAction.TalkToNearby;
            }

            string urgentNeed =
                state.Needs.GetMostUrgentNeed();

            switch (urgentNeed)
            {
                case "safety":
                    if (perception.DangerLevel > 30f)
                        return NPCAction.FindCover;
                    return NPCAction.AvoidPlayer;
                case "social":
                    if (perception.NearbyNPCs.Count > 0)
                        return NPCAction.TalkToNearby;

                    // Reduce player swarming
                    bool approach = perception.PlayerVisible
                        && perception.PlayerDistance < 8f
                        && !state.KnowsPlayerIsDangerous
                        && (state.PlayerReputation > 20 || _rand.Next(100) < 15); // Only 15% chance if neutral

                    if (approach)
                        return NPCAction.ApproachPlayer;

                    return NPCAction.Wander;
                case "curiosity":
                    if (perception.PlayerVisible
                        && !state.KnowsPlayerIsDangerous
                        && perception.PlayerDistance < 10f
                        && _rand.Next(100) < 20) // Reduced curiosity
                        return NPCAction.ApproachPlayer;
                    return NPCAction.Wander;
                case "aggression":
                    if (perception.PlayerVisible
                        && perception.PlayerDistance < 15f)
                        return NPCAction.TalkToPlayer;
                    return NPCAction.Wander;
            }

            if (urgentNeed == "rest")
                return NPCAction.Rest;

            if (state.ActiveGoal != null
                && !state.ActiveGoal.Completed
                && !state.ActiveGoal.Failed)
                return NPCAction.ContinueGoal;

            return NPCAction.PickNewGoal;
        }

        public void ExecuteAction(NPCAction action,
            NPCState state, Ped player,
            PerceptionData perception,
            Dictionary<int, NPCState> allNPCs)
        {
            switch (action)
            {
                case NPCAction.Idle:
                    state.Ped.Task.StandStill(5000);
                    state.CurrentGoal = "待命";
                    break;
                case NPCAction.Wander:
                    state.Ped.Task.Wander();
                    state.CurrentGoal = "闲逛";
                    break;
                case NPCAction.WalkToPoint:
                    if (state.ActiveGoal != null)
                    {
                        state.Ped.Task.GoTo(
                            state.ActiveGoal.Destination);
                        state.CurrentGoal =
                            state.ActiveGoal.Name;
                    }
                    break;
                case NPCAction.RunToPoint:
                    if (state.ActiveGoal != null)
                    {
                        state.Ped.Task.RunTo(
                            state.ActiveGoal.Destination);
                        state.CurrentGoal =
                            state.ActiveGoal.Name;
                    }
                    break;
                case NPCAction.FindCover:
                    Vector3 coverDir =
                        state.Ped.Position - player.Position;
                    coverDir.Normalize();
                    Vector3 coverPos =
                        state.Ped.Position + coverDir * 20f;
                    state.Ped.Task.RunTo(coverPos);
                    state.CurrentGoal = "寻找掩护";
                    break;
                case NPCAction.Flee:
                    state.Ped.Task.FleeFrom(player);
                    state.CurrentGoal = "逃跑！";
                    break;
                case NPCAction.FleeFromDanger:
                    state.Ped.Task.FleeFrom(player);
                    state.CurrentGoal = "逃离危险";
                    break;
                case NPCAction.AvoidPlayer:
                    ExecuteAvoidPlayer(state, player);
                    break;
                case NPCAction.TalkToNearby:
                    ExecuteTalkToNearby(
                        state, allNPCs);
                    break;
                case NPCAction.TalkToPlayer:
                    state.Ped.Task.TurnTo(player);

                    // If vehicle was stolen, confront player
                    if (state.IsVehicleStolen
                        && (Game.GameTime / 1000f
                            - state.VehicleStolenTime) < 60f)
                    {
                        string theftLine;
                        switch (state.Personality)
                        {
                            case "友善":
                                theftLine = Choose(
                                    "那是我的车啊！能还给我吗？",
                                    "拜托...把车还给我好吗？",
                                    "你怎么能偷别人的车呢？");
                                break;
                            case "搞笑":
                                theftLine = Choose(
                                    "嘿！我车里还有我的零食呢！",
                                    "你偷车至少先问一下嘛！",
                                    "我的车！还有六期贷款没还呢！");
                                break;
                            case "冷漠":
                                theftLine = Choose(
                                    "还我车。",
                                    "...你偷了我的车。",
                                    "我记住你了。");
                                break;
                            default:
                                theftLine = "把车还给我！";
                                break;
                        }

                        if (NPCManager.Instance != null)
                        {
                            NPCManager.Instance.ShowNPCChatPublic(
                                state, null, theftLine);
                        }
                        state.CurrentGoal = "要回车辆";
                    }
                    else
                    {
                        state.CurrentGoal = "找玩家说话";
                    }
                    break;
                case NPCAction.ApproachPlayer:
                    if (perception.PlayerDistance > 5f)
                    {
                        state.Ped.Task.GoTo(
                            player.Position);
                        state.CurrentGoal = "接近玩家";
                    }
                    break;
                case NPCAction.FollowPlayer:
                    state.Ped.Task.FollowToOffsetFromEntity(
                        player, new Vector3(2, -2, 0),
                        1f, -1, 3f, true);
                    state.CurrentGoal = "跟踪玩家";
                    break;
                case NPCAction.Wave:
                    Function.Call(Hash.REQUEST_ANIM_DICT,
                        "anim@mp_player_intcelebrationmale@wave");
                    state.PendingAnimDict =
                        "anim@mp_player_intcelebrationmale@wave";
                    state.PendingAnimName = "wave";
                    state.AnimDictRequested = true;
                    state.CurrentGoal = "打招呼";
                    break;
                case NPCAction.Fight:
                    string fightLine;
                    switch (state.Personality)
                    {
                        case "暴躁":
                            fightLine = Choose(
                                "我忍你很久了！",
                                "去死吧！",
                                "老子今天弄死你！",
                                "你找死！");
                            break;
                        case "冷漠":
                            fightLine = Choose(
                                "...够了。",
                                "你不该惹我。",
                                "消失吧。");
                            break;
                        case "友善":
                            fightLine = Choose(
                                "你逼我的！",
                                "我不想这样...但你太过分了！",
                                "够了！我受不了了！");
                            break;
                        case "搞笑":
                            fightLine = Choose(
                                "哈哈...不好笑了！揍你！",
                                "开玩笑时间结束！",
                                "笑不出来了是吧？挨打吧！");
                            break;
                        default:
                            fightLine = "别逼我！";
                            break;
                    }

                    if (NPCManager.Instance != null)
                    {
                        NPCManager.Instance.ShowNPCChatPublic(
                            state, null, fightLine);
                    }

                    state.Ped.Task.ClearAll();
                    Function.Call(Hash.TASK_COMBAT_PED,
                        state.Ped.Handle, player.Handle, 0, 16);
                    state.CurrentGoal = "战斗！";
                    state.IsAutonomous = true;
                    break;
                case NPCAction.Investigate:
                    if (perception.LastDangerPos
                        != default(Vector3))
                    {
                        state.Ped.Task.GoTo(
                            perception.LastDangerPos);
                        state.CurrentGoal = "调查情况";
                    }
                    break;
                case NPCAction.LookAround:
                    state.Ped.Task.LookAt(
                        player.Position, 3000);
                    state.CurrentGoal = "观察";
                    break;
                case NPCAction.EnterVehicle:
                    if (perception.NearestVehicle != null
                        && perception.NearestVehicle.Exists())
                    {
                        state.Ped.Task.EnterVehicle(
                            perception.NearestVehicle,
                            VehicleSeat.Driver);
                        state.CurrentGoal = "上车";
                    }
                    break;
                case NPCAction.DriveAround:
                    if (state.Ped.IsInVehicle())
                    {
                        Vector3 dest =
                            NPCGoalManager
                                .GetRandomNearbyPoint(
                                    state.Ped.Position, 200f);
                        Function.Call(
                            Hash.TASK_VEHICLE_DRIVE_TO_COORD,
                            state.Ped.Handle,
                            state.Ped.CurrentVehicle.Handle,
                            dest.X, dest.Y, dest.Z,
                            30f, 0, 0, 786603, 5f, 1f);
                        state.CurrentGoal = "开车兜风";
                    }
                    break;
                case NPCAction.ExitVehicle:
                    if (state.Ped.IsInVehicle())
                    {
                        state.Ped.Task.LeaveVehicle();
                        state.CurrentGoal = "下车";
                    }
                    break;
                case NPCAction.PickNewGoal:
                    state.ActiveGoal =
                        _goalManager.CreateGoal(
                            state, perception,
                            Game.GameTime / 1000f);
                    state.CurrentGoal =
                        state.ActiveGoal.Name;
                    ExecuteGoal(state, player);
                    break;
                case NPCAction.ContinueGoal:
                    ExecuteGoal(state, player);
                    break;
                case NPCAction.Rest:
                    state.Ped.Task.StandStill(15000);
                    state.CurrentGoal = "休息";
                    state.Needs.Energy = Math.Min(100,
                        state.Needs.Energy + 10f);
                    break;
            }
        }

        private void ExecuteAvoidPlayer(NPCState state,
            Ped player)
        {
            Vector3 away = state.Ped.Position
                - player.Position;
            away.Normalize();

            Vector3 dest = state.Ped.Position
                + away * 15f;

            if (state.SawPlayerKill)
            {
                state.Ped.Task.RunTo(dest);
                state.CurrentGoal = "快速远离危险人物";
            }
            else
            {
                state.Ped.Task.GoTo(dest);
                state.CurrentGoal = "保持距离";
            }
        }

        private void ExecuteTalkToNearby(NPCState state,
            Dictionary<int, NPCState> allNPCs)
        {
            NPCState nearest = null;
            float nearestDist = 20f;

            foreach (var kvp in allNPCs)
            {
                if (kvp.Key == state.Handle) continue;
                if (!kvp.Value.IsValid()) continue;

                float dist = Vector3.Distance(
                    state.Ped.Position,
                    kvp.Value.Ped.Position);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = kvp.Value;
                }
            }

            if (nearest != null)
            {
                if (nearestDist > 3f)
                {
                    state.Ped.Task.GoTo(
                        nearest.Ped.Position);
                    state.CurrentGoal =
                        $"走向{nearest.NpcName}";
                }
                else
                {
                    state.Ped.Task.TurnTo(nearest.Ped);
                    nearest.Ped.Task.TurnTo(state.Ped);
                    state.CurrentGoal =
                        $"和{nearest.NpcName}聊天";
                    nearest.CurrentGoal =
                        $"和{state.NpcName}聊天";

                    if (NPCManager.Instance != null
                        && !state.WaitingForAI
                        && !nearest.WaitingForAI)
                    {
                        NPCManager.Instance.RequestNPCChat(
                            state, nearest);
                        state.LastRequestTime =
                            Game.GameTime / 1000f;
                        nearest.LastRequestTime =
                            Game.GameTime / 1000f;
                    }
                }
            }
            else
            {
                state.Ped.Task.Wander();
                state.CurrentGoal = "找人聊天";
            }
        }

        private void ExecuteGoal(NPCState state, Ped player)
        {
            var goal = state.ActiveGoal;
            if (goal == null) return;

            float gameTime = Game.GameTime / 1000f;

            if (goal.IsExpired(gameTime))
            {
                goal.Failed = true;
                state.CurrentGoal = "目标超时";
                state.Needs.Purpose += 20f;
                return;
            }

            float distToGoal = Vector3.Distance(
                state.Ped.Position, goal.Destination);

            if (distToGoal < 5f
                && goal.Type != GoalType.Wander
                && goal.Type != GoalType.Rest)
            {
                goal.Completed = true;
                state.CurrentGoal =
                    $"{goal.Name} ✓";
                state.Needs.Purpose += 30f;
                return;
            }

            switch (goal.Type)
            {
                case GoalType.Wander:
                    if (!IsDoingTask(state.Ped))
                        state.Ped.Task.Wander();
                    break;
                case GoalType.GoToLocation:
                    if (!IsDoingTask(state.Ped))
                        state.Ped.Task.GoTo(
                            goal.Destination);
                    break;
                case GoalType.FleeFromArea:
                    if (!IsDoingTask(state.Ped))
                        state.Ped.Task.RunTo(
                            goal.Destination);
                    break;
                case GoalType.FindSomeoneToTalk:
                    if (distToGoal > 3f)
                        state.Ped.Task.GoTo(
                            goal.Destination);
                    else
                        goal.Completed = true;
                    break;
                case GoalType.FollowPlayer:
                    state.Ped.Task
                        .FollowToOffsetFromEntity(
                            player,
                            new Vector3(3, -3, 0),
                            1f, -1, 3f, true);
                    break;
                case GoalType.Rest:
                    state.Ped.Task.StandStill(10000);
                    break;
                case GoalType.Investigate:
                    if (!IsDoingTask(state.Ped))
                        state.Ped.Task.GoTo(
                            goal.Destination);
                    break;
            }
        }

        private bool IsDoingTask(Ped ped)
        {
            return Function.Call<int>(
                Hash.GET_SCRIPT_TASK_STATUS,
                ped.Handle, 0x811E343C) != 7;
        }

        public string GetInstantResponse(
            string personality, string playerText,
            int relationship)
        {
            bool isGreeting = playerText.Contains("你好")
                || playerText.Contains("嗨")
                || playerText.Contains("hi")
                || playerText.Contains("嘿");

            bool isInsult = playerText.Contains("丑")
                || playerText.Contains("蠢")
                || playerText.Contains("滚")
                || playerText.Contains("废物")
                || playerText.Contains("垃圾");

            if (!isGreeting && !isInsult) return null;

            switch (personality)
            {
                case "友善":
                    if (isGreeting)
                        return Choose("你好啊！", "嗨！今天怎么样？",
                            "你好！很高兴见到你！");
                    if (isInsult)
                        return Choose("别这样嘛...",
                            "你心情不好吗？", "我做错什么了吗？");
                    break;
                case "暴躁":
                    if (isGreeting)
                        return Choose("干嘛？", "看什么看！",
                            "有事说事！");
                    if (isInsult)
                        return Choose("你找死！", "再说一遍试试！",
                            "老子弄死你！");
                    break;
                case "胆小":
                    if (isGreeting)
                        return Choose("你...你好",
                            "啊...你在跟我说话？",
                            "你好...别伤害我");
                    if (isInsult)
                        return Choose("对不起...",
                            "别打我！", "我走还不行吗？");
                    break;
                case "搞笑":
                    if (isGreeting)
                        return Choose("哟！兄弟！",
                            "嘿！你也来这玩啊？",
                            "大帅哥/大美女你好啊！");
                    if (isInsult)
                        return Choose("哈哈你真幽默！",
                            "你是在夸我吗？谢谢！",
                            "说啥呢笑死我了哈哈哈！");
                    break;
                case "冷漠":
                    if (isGreeting) return Choose("嗯。",
                        "...你好。", "...");
                    if (isInsult) return Choose("...",
                        "随便。", "无所谓。");
                    break;
            }
            return null;
        }

        private string Choose(params string[] options)
        {
            return options[_rand.Next(options.Length)];
        }


    }
}
