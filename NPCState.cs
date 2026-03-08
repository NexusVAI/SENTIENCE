using GTA;
using GTA.Math;
using System;
using System.Collections.Generic;

namespace GTA5MOD2026
{
    public class ConversationTurn
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public float Time { get; set; }
    }

    public enum AwakenStage
    {
        Sleeping = 0,
        Dreaming = 1,
        Questioning = 2,
        Aware = 3,
        Awakened = 4
    }

    public class NPCState
    {
        public Ped Ped;
        public string StableId = "";
        public string UniqueId = Guid.NewGuid().ToString("N")
            .Substring(0, 8);
        public bool IsMale = true;
        public string Appearance = "";
        public float LastNotifTime;
        public string PendingAnimDict;
        public string PendingAnimName;
        public bool AnimDictRequested;
        public string NpcName = "NPC";
        public string Personality = "冷漠";
        public int Handle => Ped?.Handle ?? 0;

        public bool IsInteracting;
        public bool WaitingForAI;
        public int InteractionCount;
        public float LastRequestTime;
        public string CurrentAction = "idle";
        public int LastStateHash;

        public string LastLLMDialogue;
        public float DialogueShowTime;
        private const float DIALOGUE_DURATION = 8f;
        public bool IsPlayingVoice;

        public AwakenStage Stage = AwakenStage.Sleeping;
        public int AwakenLevel;
        public int ThreatLevel;
        public bool IsAutonomous;
        public string CurrentGoal = "none";
        public NPCGoal ActiveGoal;

        public NPCNeeds Needs { get; private set; }
            = new NPCNeeds();

        public float PlayerReputation;
        public bool KnowsPlayerIsDangerous;
        public bool SawPlayerKill;
        public bool SawPlayerShoot;
        public bool SawPlayerSpeeding;
        public string PlayerNickname;
        public int InsultCount;
        public int ConsecutiveInsults;
        public float LastInsultTime;
        public string LastKnownPlayerAction;
        public string HomeZone;
        public float LastSawPlayerTime;

        public string LastPlayerAction = "idle";
        public string LastInteractionType = "talk";
        public List<ConversationTurn> ConversationHistory
            = new List<ConversationTurn>();
        public HashSet<int> SeenDeadPeds
            = new HashSet<int>();
        public List<string> AwakenedContacts
            = new List<string>();
        public string Realization = "";

        // Vehicle Theft Tracking
        public Vehicle LastKnownVehicle { get; set; }
        public bool IsVehicleStolen { get; set; }
        public float VehicleStolenTime { get; set; }

        public void InitializeNeeds()
        {
            Needs.InitFromPersonality(Personality);
        }

        public static string MakeStableId(Ped ped)
        {
            if (ped == null || !ped.Exists())
                return "INVALID_PED";

            uint model = (uint)ped.Model.Hash;
            Vector3 pos = ped.Position;
            int x = (int)Math.Round(pos.X / 2.0) * 2;
            int y = (int)Math.Round(pos.Y / 2.0) * 2;
            int z = (int)Math.Round(pos.Z / 5.0) * 5;

            return $"{model:X8}_{x}_{y}_{z}";
        }

        public void RecordConversationTurn(string role,
            string content, float gameTime)
        {
            if (string.IsNullOrWhiteSpace(role)
                || string.IsNullOrWhiteSpace(content))
                return;

            ConversationHistory.Add(new ConversationTurn
            {
                Role = role,
                Content = content.Trim(),
                Time = gameTime
            });

            while (ConversationHistory.Count > 10)
                ConversationHistory.RemoveAt(0);
        }

        public void InitializeIdentity()
        {
            IsMale = GTA.Native.Function.Call<bool>(
                GTA.Native.Hash.IS_PED_MALE, Ped.Handle);

            string gender = IsMale ? "男性" : "女性";
            string pedType = "普通人";

            try
            {
                string modelName = Ped.Model.ToString().ToLower();

                if (modelName.Contains("a_c_"))
                {
                    if (modelName.Contains("dog")) pedType = "狗";
                    else if (modelName.Contains("cat")) pedType = "猫";
                    else if (modelName.Contains("rat")) pedType = "老鼠";
                    else pedType = "动物";
                    Appearance = pedType;
                    return;
                }

                if (modelName.Contains("business")
                    || modelName.Contains("suit"))
                    pedType = "穿西装的商务人士";
                else if (modelName.Contains("beach")
                    || modelName.Contains("bikini"))
                    pedType = "穿沙滩装的人";
                else if (modelName.Contains("homeless")
                    || modelName.Contains("tramp"))
                    pedType = "衣衫褴褛的流浪汉";
                else if (modelName.Contains("hipster"))
                    pedType = "潮人打扮的年轻人";
                else if (modelName.Contains("tourist"))
                    pedType = "游客";
                else if (modelName.Contains("runner")
                    || modelName.Contains("yoga")
                    || modelName.Contains("fitness"))
                    pedType = "穿运动装的人";
                else if (modelName.Contains("gang")
                    || modelName.Contains("ballas")
                    || modelName.Contains("families")
                    || modelName.Contains("vagos")
                    || modelName.Contains("lost"))
                    pedType = "看起来像帮派成员";
                else if (modelName.Contains("cop")
                    || modelName.Contains("police")
                    || modelName.Contains("sheriff"))
                    pedType = "穿制服的执法人员";
                else if (modelName.Contains("doctor")
                    || modelName.Contains("medic")
                    || modelName.Contains("paramedic"))
                    pedType = "穿白大褂的医务人员";
                else if (modelName.Contains("worker")
                    || modelName.Contains("construct")
                    || modelName.Contains("mechanic"))
                    pedType = "穿工作服的工人";
                else if (modelName.Contains("prostitut")
                    || modelName.Contains("hooker"))
                    pedType = "衣着暴露的人";
                else
                    pedType = IsMale ? "普通男子" : "普通女子";
            }
            catch
            {
                pedType = IsMale ? "普通男子" : "普通女子";
            }

            Appearance = $"{gender}，{pedType}";
        }

        public bool IsValid()
        {
            return Ped != null
                && Ped.Exists()
                && !Ped.IsDead;
        }

        public bool HasActiveDialogue(float gameTime)
        {
            if (string.IsNullOrEmpty(LastLLMDialogue))
                return false;
            return (gameTime - DialogueShowTime)
                < DIALOGUE_DURATION;
        }

        public void UpdatePlayerKnowledge(
            PerceptionData perception, float gameTime, Ped player)
        {
            // Track my vehicle
            if (Ped.IsInVehicle())
            {
                LastKnownVehicle = Ped.CurrentVehicle;
                IsVehicleStolen = false;
            }

            // Detect theft
            if (player.IsInVehicle()
                && player.CurrentVehicle != null
                && player.CurrentVehicle.Exists()
                && LastKnownVehicle != null
                && LastKnownVehicle.Exists())
            {
                if (player.CurrentVehicle.Handle == LastKnownVehicle.Handle && !IsVehicleStolen)
                {
                    IsVehicleStolen = true;
                    VehicleStolenTime = gameTime;
                    PlayerReputation -= 40f;
                    KnowsPlayerIsDangerous = true;
                    LastKnownPlayerAction = "偷了我的车";
                    UpdatePlayerNickname();
                }
            }

            if (!perception.PlayerVisible) return;

            LastSawPlayerTime = gameTime;

            if (perception.PlayerShooting)
            {
                SawPlayerShoot = true;
                KnowsPlayerIsDangerous = true;
                PlayerReputation -= 5f;
                LastKnownPlayerAction = "开枪";
            }

            if (perception.SawPedDie
                && perception.PlayerShooting)
            {
                SawPlayerKill = true;
                KnowsPlayerIsDangerous = true;
                PlayerReputation -= 20f;
                LastKnownPlayerAction = "杀人";
            }

            if (perception.PlayerInVehicle
                && perception.PlayerSpeed > 25f
                && perception.PlayerDistance < 15f)
            {
                SawPlayerSpeeding = true;
                PlayerReputation -= 2f;
                LastKnownPlayerAction = "飙车";
            }

            UpdatePlayerNickname();

            PlayerReputation = Math.Max(-100f,
                Math.Min(100f, PlayerReputation));
        }

        private void UpdatePlayerNickname()
        {
            // Vehicle theft takes priority over other nicknames
            if (IsVehicleStolen)
            {
                switch (Personality)
                {
                    case "友善":
                        PlayerNickname = "偷车的人"; break;
                    case "暴躁":
                        PlayerNickname = "偷车贼"; break;
                    case "胆小":
                        PlayerNickname = "抢车的恶人"; break;
                    case "搞笑":
                        PlayerNickname = "免费代驾"; break;
                    case "冷漠":
                        PlayerNickname = "偷车的"; break;
                }
                return;
            }

            if (SawPlayerKill)
            {
                switch (Personality)
                {
                    case "友善":
                        PlayerNickname = "那个可怕的人";
                        break;
                    case "暴躁":
                        PlayerNickname = "那个杀人犯";
                        break;
                    case "胆小":
                        PlayerNickname = "恶魔";
                        break;
                    case "搞笑":
                        PlayerNickname = "死神本人";
                        break;
                    case "冷漠":
                        PlayerNickname = "危险人物";
                        break;
                }
            }
            else if (SawPlayerShoot)
            {
                switch (Personality)
                {
                    case "友善":
                        PlayerNickname = "拿枪的家伙";
                        break;
                    case "暴躁":
                        PlayerNickname = "找死的";
                        break;
                    case "胆小":
                        PlayerNickname = "坏人";
                        break;
                    case "搞笑":
                        PlayerNickname = "枪神";
                        break;
                    case "冷漠":
                        PlayerNickname = "持枪者";
                        break;
                }
            }
            else if (SawPlayerSpeeding)
            {
                switch (Personality)
                {
                    case "友善":
                        PlayerNickname = "飙车族";
                        break;
                    case "暴躁":
                        PlayerNickname = "不要命的";
                        break;
                    case "胆小":
                        PlayerNickname = "疯子司机";
                        break;
                    case "搞笑":
                        PlayerNickname = "秋名山车神";
                        break;
                    case "冷漠":
                        PlayerNickname = "飙车的";
                        break;
                }
            }
        }

        public string BuildPlayerKnowledgeContext()
        {
            string ctx = "";

            if (!string.IsNullOrEmpty(PlayerNickname))
                ctx += $"你叫玩家\"{PlayerNickname}\"。";

            // ADD: Vehicle theft context
            if (IsVehicleStolen)
            {
                float timeSinceTheft =
                    GTA.Game.GameTime / 1000f - VehicleStolenTime;
                if (timeSinceTheft < 30f)
                    ctx += "这个人刚刚偷了你的车！你非常愤怒！";
                else if (timeSinceTheft < 120f)
                    ctx += "这个人之前偷了你的车。你很恨他。";
                else
                    ctx += "这个人偷过你的车。你记得。";
            }

            if (SawPlayerKill)
                ctx += "你见过这个人杀人，你非常害怕/愤怒。";
            else if (SawPlayerShoot)
                ctx += "你见过这个人开枪。";
            else if (SawPlayerSpeeding)
                ctx += "你见过这个人疯狂飙车。";

            if (PlayerReputation < -50)
                ctx += "你非常讨厌这个人。";
            else if (PlayerReputation < -20)
                ctx += "你不信任这个人。";
            else if (PlayerReputation > 20)
                ctx += "你觉得这个人还不错。";

            return ctx;
        }

        public void RecordInsult(float gameTime)
        {
            InsultCount++;
            PlayerReputation -= 8f;

            if (gameTime - LastInsultTime < 15f)
            {
                ConsecutiveInsults++;
                PlayerReputation -= ConsecutiveInsults * 5f;
            }
            else
            {
                ConsecutiveInsults = 1;
            }

            LastInsultTime = gameTime;

            if (Personality == "暴躁")
                PlayerReputation -= 10f;

            PlayerReputation = Math.Max(-100f, PlayerReputation);

            Needs.Aggression = Math.Min(100f,
                Needs.Aggression + 10f);
            Needs.Safety = Math.Max(0f,
                Needs.Safety - 5f);
        }

        public void RecordCompliment(float gameTime)
        {
            PlayerReputation += 5f;
            ConsecutiveInsults = 0;
            PlayerReputation = Math.Min(100f, PlayerReputation);

            Needs.Aggression = Math.Max(0f,
                Needs.Aggression - 5f);
        }
    }
}
