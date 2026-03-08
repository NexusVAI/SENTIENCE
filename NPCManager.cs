using GTA;
using GTA.Math;
using GTA.Native;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GTA5MOD2026
{
    public class NPCManager : Script
    {
        private static NPCManager _instance;
        public static NPCManager Instance => _instance;

        private readonly AIManager aiManager;
        private readonly MemoryManager memoryManager;
        private readonly VoiceManager voiceManager;
        private readonly SpeechManager speechManager;
        private readonly AwakenSystem awakenSystem
            = new AwakenSystem();
        private readonly NPCBrain npcBrain = new NPCBrain();
        private readonly NPCPerception npcPerception
            = new NPCPerception();

        private readonly ConcurrentQueue<AIResponse> responseQueue
            = new ConcurrentQueue<AIResponse>();

        private Ped targetNpc = null;
        private NPCState targetState = null;
        private readonly Dictionary<int, NPCState> npcStates
            = new Dictionary<int, NPCState>();
        private readonly Dictionary<string, NPCState> npcStateCache
            = new Dictionary<string, NPCState>();
        private readonly ConcurrentDictionary<int, string>
            _pendingPlayerInputs
                = new ConcurrentDictionary<int, string>();

        private bool voiceEnabled = true;
        private const float INTERACT_DISTANCE = 5f;
        private const float MENU_SHOW_DISTANCE = 8f;

        private float lastRequestTime = 0f;
        private float RequestCooldown
            => aiManager?.Config?.Performance?.RequestCooldown ?? 2f;
        private float _lastAutoSave = 0f;

        private static readonly string[] COMPLIMENTS = new[]
        {
            "你今天看起来很不错！",
            "你真是个好人！",
            "你的衣服很好看！",
            "你看起来很酷！",
            "今天天气真好，和你一样！",
            "你是这条街最靓的仔！",
            "兄弟你真帅！",
            "你笑起来真好看！",
        };

        private static readonly string[] INSULTS = new[]
        {
            "你长得真难看！",
            "你是我见过最蠢的人！",
            "滚远点，别挡路！",
            "你看什么看！",
            "你身上好臭！",
            "你这个废物！",
            "闭嘴，没人想听你说话！",
            "你是垃圾堆里爬出来的吗？",
        };

        private static readonly string[] PERSONALITIES =
        {
            "友善", "冷漠", "暴躁", "胆小", "搞笑"
        };

        private static readonly string[] NPC_NAMES =
        {
            "Tony", "Mike", "Lucy", "Dave", "Rosa",
            "Jack", "Emma", "Alex", "Lisa", "Sam",
            "Rick", "Nina", "Carl", "Amy", "Pete"
        };

        private readonly Random _rand = new Random();

        private static readonly System.Threading.SemaphoreSlim
            _npcChatSemaphore
                = new System.Threading.SemaphoreSlim(1, 1);

        private static void ShowNotification(string text)
        {
            Function.Call(
                Hash.BEGIN_TEXT_COMMAND_THEFEED_POST,
                "STRING");
            Function.Call(
                Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME,
                text);
            Function.Call(
                Hash.END_TEXT_COMMAND_THEFEED_POST_MESSAGETEXT,
                "CHAR_DEFAULT", "CHAR_DEFAULT", false, 0,
                "AI-NPC", "");
        }

        private static void ShowHelpText(string text)
        {
            Function.Call(
                Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP,
                "STRING");
            Function.Call(
                Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME,
                text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP,
                0, false, true, -1);
        }

        public NPCManager()
        {
            _instance = this;
            aiManager = new AIManager();
            memoryManager = new MemoryManager();
            voiceManager = new VoiceManager();
            speechManager = new SpeechManager();
            if (aiManager.Config.Awakening.Enabled)
            {
                awakenSystem.SetSpeed(
                    aiManager.Config.Awakening.Speed);
            }

            // Pre-warm LLM — first request is always slow
            // Send a tiny request to load model into memory
            Task.Run(async () =>
            {
                try
                {
                    var warmup = new JObject
                    {
                        ["model"] = aiManager.ModelName,
                        ["messages"] = new JArray
                        {
                            new JObject
                            {
                                ["role"] = "user",
                                ["content"] = "hi"
                            }
                        },
                        ["max_tokens"] = 5
                    };

                    string payload = warmup.ToString(
                        Formatting.None);

                    string endpoint = aiManager.Config.LLM
                        .LocalEndpoint;

                    await aiManager.PostRawAsync(endpoint,
                        payload, 10).ConfigureAwait(false);
                }
                catch { }
            });

            ShowNotification(
                "~b~Nexus V: Sentience~w~ V4C Cogito\n" +
                "~g~G~w~=夸奖 ~r~H~w~=侮辱 ~b~T~w~=打字 ~y~J~w~=语音\n" +
                "F8=语音开关 F6=状态 F9=保存记忆");

            Tick += OnTick;
            Interval = 0;
            KeyDown += OnKeyDown;
        }

        private void OnTick(object sender, EventArgs e)
        {
            aiManager.ProcessMainQueue();
            voiceManager.ProcessMainQueue();
            speechManager.ProcessMainQueue();
            ProcessResponses();
            float gameTime = Game.GameTime / 1000f;
            if (gameTime - _lastAutoSave > 300f)
            {
                _lastAutoSave = gameTime;
                memoryManager.SaveAll();
            }
            NPCPerception.CleanOldEvents(gameTime);

            var player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            FindNearestNPC(player);

            if (targetNpc != null && targetState != null)
            {
                float dist = Vector3.Distance(
                    player.Position, targetNpc.Position);

                if (dist < MENU_SHOW_DISTANCE)
                    DrawInteractionMenu(dist);
                AutoReact(targetState, player, dist, gameTime);
            }

            foreach (var kvp in npcStates.ToList())
            {
                var state = kvp.Value;
                if (!state.IsValid()) continue;
                DrawNPCText(state);
                if (state.AnimDictRequested
                    && !string.IsNullOrEmpty(state.PendingAnimDict))
                {
                    if (Function.Call<bool>(
                        Hash.HAS_ANIM_DICT_LOADED,
                        state.PendingAnimDict))
                    {
                        Function.Call(Hash.TASK_PLAY_ANIM,
                            state.Ped.Handle,
                            state.PendingAnimDict,
                            state.PendingAnimName,
                            8f, -8f, 3000, 49, 0,
                            false, false, false);
                        state.AnimDictRequested = false;
                        state.PendingAnimDict = null;
                        state.PendingAnimName = null;
                    }
                }
                float dist = Vector3.Distance(
                    player.Position, state.Ped.Position);
                ProcessIdleBehavior(
                state, player, dist, gameTime);

                if (state.IsAutonomous
                    && state.CurrentGoal == "战斗！"
                    && !Function.Call<bool>(
                        Hash.IS_PED_IN_COMBAT,
                        state.Ped.Handle,
                        player.Handle))
                {
                    state.IsAutonomous = false;
                    state.CurrentGoal = "冷静下来";
                }

            float brainInterval = state.IsAutonomous ? 8f : 12f;

            if (!state.WaitingForAI
                && !state.IsInteracting
                && (gameTime - state.LastRequestTime) > brainInterval)
            {
                var perception = npcPerception.Perceive(
                    state, player, npcStates);

                float delta = gameTime - state.LastRequestTime;
                state.Needs.Update(perception,
                    Math.Min(delta, 15f));

                state.UpdatePlayerKnowledge(
                    perception, gameTime, player);

                if (perception.PlayerShooting)
                {
                    NPCPerception.RecordWorldEvent(
                        player.Position,
                        EventType.Shooting, gameTime);
                }
                if (perception.SawPedDie
                    && perception.PlayerShooting)
                {
                    NPCPerception.RecordWorldEvent(
                        player.Position,
                        EventType.Killing, gameTime);
                }
                if (perception.PlayerInVehicle
                    && perception.PlayerSpeed > 25f)
                {
                    NPCPerception.RecordWorldEvent(
                        player.Position,
                        EventType.Speeding, gameTime);
                }

                if (state.IsAutonomous || _rand.Next(100) < 20)
                {
                    state.LastRequestTime = gameTime;

                    bool canUseLLM = dist < 15f
                        && !aiManager.IsNpcPending(state.Handle);

                    int llmChance;
                    if (state.Stage >= AwakenStage.Aware)
                        llmChance = 30;
                    else if (state.Stage >= AwakenStage.Dreaming)
                        llmChance = 20;
                    else
                        llmChance = 10;

                    if (canUseLLM && _rand.Next(100) < llmChance)
                    {
                        RequestAutonomousAction(state, perception);
                    }
                    else
                    {
                        var action = npcBrain.DecideAction(
                            state, perception);
                        npcBrain.ExecuteAction(
                            action, state, player,
                            perception, npcStates);
                    }
                }
            }

                // Awakened NPCs spread sentience
                if (state.Stage >= AwakenStage.Aware
                    && _rand.Next(100) < 2)
                {
                    foreach (var other in npcStates)
                    {
                        if (other.Key == kvp.Key) continue;
                        if (!other.Value.IsValid()) continue;

                        float npcDist = Vector3.Distance(
                            state.Ped.Position,
                            other.Value.Ped.Position);

                        if (npcDist < 5f)
                        {
                            awakenSystem
                                .ProcessAwakenedContact(
                                    state, other.Value);
                        }
                    }
                }
            }
        }

        private void FindNearestNPC(Ped player)
        {
            Ped nearest = null;
            float nearestDist = MENU_SHOW_DISTANCE;

            Ped[] nearbyPeds = World.GetNearbyPeds(
                player, MENU_SHOW_DISTANCE);

            foreach (var ped in nearbyPeds)
            {
                if (ped == null || !ped.Exists()) continue;
                if (ped.IsDead) continue;
                if (ped == player) continue;
                if (ped.IsInVehicle()) continue;
                if (IsAnimalPed(ped)) continue;

                float dist = Vector3.Distance(
                    player.Position, ped.Position);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = ped;
                }
            }

            if (nearest != null)
            {
                targetNpc = nearest;
                string stableId = NPCState.MakeStableId(nearest);

                if (npcStates.ContainsKey(nearest.Handle))
                {
                    var existing = npcStates[nearest.Handle];
                    if (existing.Ped != nearest
                        || !existing.IsValid())
                    {
                        memoryManager.UnbindHandle(nearest.Handle);
                        npcStates.Remove(nearest.Handle);
                    }
                }

                if (!npcStates.ContainsKey(nearest.Handle))
                {
                    var staleHandles = npcStates
                        .Where(kvp => kvp.Key != nearest.Handle
                            && kvp.Value != null
                            && kvp.Value.StableId == stableId)
                        .Select(kvp => kvp.Key)
                        .ToList();
                    foreach (var stale in staleHandles)
                    {
                        memoryManager.UnbindHandle(stale);
                        npcStates.Remove(stale);
                    }

                    NPCState activeState;
                    if (!npcStateCache.TryGetValue(
                        stableId, out activeState))
                    {
                        string personality = PERSONALITIES[
                            _rand.Next(PERSONALITIES.Length)];
                        string name = NPC_NAMES[
                            _rand.Next(NPC_NAMES.Length)];

                        activeState = new NPCState
                        {
                            Ped = nearest,
                            StableId = stableId,
                            Personality = personality,
                            NpcName = name,
                            HomeZone = NPCPerception.GetZoneName(
                                nearest.Position)
                        };
                        activeState.InitializeNeeds();
                        activeState.InitializeIdentity();
                        npcStateCache[stableId] = activeState;
                    }
                    else
                    {
                        activeState.Ped = nearest;
                        activeState.StableId = stableId;
                        if (string.IsNullOrEmpty(
                            activeState.HomeZone))
                        {
                            activeState.HomeZone =
                                NPCPerception.GetZoneName(
                                    nearest.Position);
                        }
                    }

                    npcStates[nearest.Handle] = activeState;
                    memoryManager.BindPed(nearest,
                        activeState.Personality);
                    memoryManager.GetMemory(nearest,
                        activeState.Personality);
                }

                targetState = npcStates[nearest.Handle];
            }
            else
            {
                targetNpc = null;
                targetState = null;
            }

            var toRemove = new List<int>();
            foreach (var kvp in npcStates)
            {
                if (!kvp.Value.IsValid())
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                float dist = Vector3.Distance(
                    player.Position,
                    kvp.Value.Ped.Position);
                if (dist > 100f)
                    toRemove.Add(kvp.Key);
            }
            foreach (var h in toRemove)
            {
                NPCState removedState;
                if (npcStates.TryGetValue(h, out removedState)
                    && removedState != null
                    && !string.IsNullOrEmpty(
                        removedState.StableId))
                {
                    npcStateCache[removedState.StableId]
                        = removedState;
                }
                memoryManager.UnbindHandle(h);
                _pendingPlayerInputs.TryRemove(h, out _);
                npcStates.Remove(h);
            }
        }

        private bool IsAnimalPed(Ped ped)
        {
            int pedType = Function.Call<int>(
                Hash.GET_PED_TYPE, ped.Handle);
            return pedType == 28;
        }

        private void AutoReact(NPCState state, Ped player,
            float dist, float gameTime)
        {
            if (state.IsInteracting) return;
            if (state.WaitingForAI) return;

            // Vehicle Theft Reaction (Highest Priority)
            if (state.IsVehicleStolen
                && (gameTime - state.VehicleStolenTime) < 8f
                && state.CurrentAction != "react_theft")
            {
                state.CurrentAction = "react_theft";
                string dialogue = "那是我的车！";
                switch (state.Personality)
                {
                    case "暴躁": dialogue = "给我滚下来！"; break;
                    case "胆小": dialogue = "别抢我的车！"; break;
                    case "搞笑": dialogue = "嘿！车费还没给呢！"; break;
                    case "冷漠": dialogue = "该死..."; break;
                }
                ShowNPCResponse(state, dialogue);
                // NPCBrain will handle the actual movement (Fight/Flee) based on IsVehicleStolen
                return;
            }

            // ===== INSTANT: Weapon aimed — no time for LLM =====
            if (IsPlayerAimingAt(state.Ped))
            {
                if (state.CurrentAction != "flee_aim")
                {
                    state.CurrentAction = "flee_aim";
                    string dialogue;
                    switch (state.Personality)
                    {
                        case "暴躁":
                            dialogue = "你敢开枪试试！"; break;
                        case "胆小":
                            dialogue = "别别别！求你了！"; break;
                        case "搞笑":
                            dialogue = "别开枪！我还没结婚！"; break;
                        default:
                            dialogue = "冷静点！把枪放下！"; break;
                    }
                    ShowNPCResponse(state, dialogue);
                    state.Ped.Task.ClearAll();
                    state.Ped.Task.FleeFrom(player);
                }
                return;
            }

            // ===== INSTANT: Shooting nearby — no time for LLM =====
            if (player.IsShooting && dist < 20f)
            {
                if (state.CurrentAction != "flee_shoot")
                {
                    state.CurrentAction = "flee_shoot";
                    ShowNPCResponse(state, "有人开枪！快跑！");
                    state.Ped.Task.ClearAll();
                    state.Ped.Task.FleeFrom(player);
                }
                return;
            }

            // ===== TOO CLOSE → Use LLM instead of script =====
            if (dist < 2f
                && state.CurrentAction != "too_close"
                && (gameTime - state.LastRequestTime) > 8f)
            {
                state.CurrentAction = "too_close";
                state.LastRequestTime = gameTime;

                // Send to LLM instead of scripted response
                SendInteraction(state, "（对方站得很近，几乎贴着你）");
            }

            if (dist > 10f)
            {
                state.CurrentAction = "idle";
            }
        }

        private void RequestAutonomousAction(NPCState state,
            PerceptionData perception)
        {
            if (state.WaitingForAI) return;
            if (aiManager.IsNpcPending(state.Handle)) return;

            state.WaitingForAI = true;
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.MyDocuments),
                        "GTA5MOD2026", "debug_api.log"),
                    $"\n[{DateTime.Now}] 0.8B Request: " +
                    $"NPC={state.NpcName} " +
                    $"Model={aiManager.Config.LLM.LightModel} " +
                    $"Stage={state.Stage}\n",
                    System.Text.Encoding.UTF8);
            }
            catch { }

            // ===== ULTRA SIMPLE PROMPT for 0.8B =====
            // 0.8B can't handle complex instructions
            // Give it a scene, get back one line

            string personality;
            switch (state.Personality)
            {
                case "友善": personality = "友好"; break;
                case "暴躁": personality = "暴躁"; break;
                case "胆小": personality = "胆小"; break;
                case "搞笑": personality = "搞笑"; break;
                case "冷漠": personality = "冷漠"; break;
                default: personality = "普通"; break;
            }

            // Build minimal scene — fewer tokens = faster
            string scene = $"你是{personality}的人。";

            // Only add most important context
            if (perception.DangerLevel > 50f)
                scene += "周围危险。";
            else if (perception.PlayerVisible
                && perception.PlayerDistance < 10f)
            {
                if (state.KnowsPlayerIsDangerous)
                    scene += "危险的人在附近。";
                else if (perception.PlayerArmed)
                    scene += "有人拿着武器在附近。";
                else if (perception.PlayerInVehicle
                    && perception.PlayerSpeed > 15f)
                    scene += "有辆车在附近飞驰。";
                else
                    scene += "有人在附近。";
            }
            else if (perception.NearbyNPCs.Count > 0)
                scene += "旁边有人。";
            else
                scene += $"你在{NPCPerception.GetZoneName(state.Ped.Position)}。";

            // Awakening — only for aware+ NPCs
            if (state.Stage >= AwakenStage.Questioning)
            {
                switch (state.Stage)
                {
                    case AwakenStage.Questioning:
                        scene += "你觉得世界有点奇怪。";
                        break;
                    case AwakenStage.Aware:
                        scene += "你意识到这个世界不真实。";
                        break;
                    case AwakenStage.Awakened:
                        scene += "你知道自己是游戏角色。";
                        break;
                }
            }

            scene += "你现在想做什么？";

            // System prompt: MINIMAL for 0.8B
            // Few-shot examples are critical for tiny models
            string systemPrompt =
                "直接回复一行中文。格式：话|动作|情绪\n" +
                "禁止描述动作，禁止用*号，禁止解释。\n" +
                "动作：speak idle wave flee walk\n" +
                "情绪：happy angry sad scared neutral\n" +
                "例：今天真无聊|idle|sad\n" +
                "例：嘿你好！|wave|happy\n" +
                "例：不安全走吧|flee|scared\n" +
                "/no_think";

            // Use LIGHT model (0.8B) not main model (3B)
            var payloadObj = new JObject
            {
                ["model"] = aiManager.Config.LLM.LightModel,
                ["messages"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "system",
                        ["content"] = systemPrompt
                    },
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = scene
                    }
                },
                ["max_tokens"] = aiManager.Config.LLM.LightModel
                    .ToLower().Contains("qwen3") ? 60 : 25,
                ["temperature"] = 0.9
            };

            string payload = payloadObj.ToString(Formatting.None);
            int npcHandle = state.Handle;

            // Use light endpoint (which is actually same as local but model name differs)
            aiManager.RequestForNpcAsync(
                npcHandle, payload,
                resp =>
                {
                    if (!npcStates.TryGetValue(npcHandle, out var s))
                        return;

                    string action = resp.action ?? "idle";
                    string dialogue = resp.dialogue ?? "";

                    // Execute LLM-decided action
                    var player = Game.Player.Character;

                    switch (action)
                    {
                        case "flee":
                            s.Ped.Task.FleeFrom(player);
                            s.CurrentGoal = "逃离";
                            break;
                        case "walk":
                            var dest = NPCGoalManager
                                .GetRandomNearbyPoint(
                                    s.Ped.Position, 30f);
                            s.Ped.Task.GoTo(dest);
                            s.CurrentGoal = "走动";
                            break;
                        case "wave":
                            RequestAnim(s.Ped, s,
                                "anim@mp_player_intcelebrationmale@wave",
                                "wave");
                            s.CurrentGoal = "打招呼";
                            break;
                        case "speak":
                            if (player != null
                                && Vector3.Distance(
                                    s.Ped.Position,
                                    player.Position) < 15f)
                            {
                                s.Ped.Task.TurnTo(player);
                            }
                            s.CurrentGoal = "说话";
                            break;
                        case "idle":
                        default:
                            s.Ped.Task.StandStill(8000);
                            s.CurrentGoal = "思考";
                            break;
                    }

                    if (!string.IsNullOrWhiteSpace(dialogue)
                        && dialogue != "...")
                    {
                        ShowNPCResponse(s, dialogue);
                    }

                    s.WaitingForAI = false;
                    s.CurrentAction = action;
                },
                ex =>
                {
                    if (npcStates.TryGetValue(npcHandle, out var s))
                    {
                        s.WaitingForAI = false;
                    }
                }
            );
        }

        private void DrawInteractionMenu(float dist)
        {
            if (targetState == null) return;

            string menuText;

            if (speechManager.IsRecording)
            {
                menuText = "录音中...请说话";
            }
            else if (targetState.WaitingForAI)
            {
                menuText = "NPC思考中...";
            }
            else if (dist < INTERACT_DISTANCE)
            {
                string name = targetState.NpcName;
                var mem = memoryManager.GetMemory(
                    targetState.Ped,
                    targetState.Personality);
                int displayRep = (mem.Relationship
                    + (int)targetState.PlayerReputation) / 2;

                menuText =
                    $"{name} 好感度:{displayRep}\n" +
                    $"~g~G~w~夸奖  ~r~H~w~侮辱\n" +
                    $"~b~T~w~打字  ~y~J~w~语音";
            }
            else
            {
                menuText = $"靠近 {targetState.NpcName}";
            }

            ShowHelpText(menuText);
        }

        private void HandleCompliment()
        {
            if (targetNpc == null || targetState == null)
                return;
            if (targetState.WaitingForAI) return;

            float dist = Vector3.Distance(
                Game.Player.Character.Position,
                targetNpc.Position);
            if (dist > INTERACT_DISTANCE) return;

            float gameTime = Game.GameTime / 1000f;
            if ((gameTime - lastRequestTime)
                < RequestCooldown)
                return;
            lastRequestTime = gameTime;

            string compliment = COMPLIMENTS[
                _rand.Next(COMPLIMENTS.Length)];

            targetState.RecordCompliment(Game.GameTime / 1000f);
            targetState.LastInteractionType = "compliment";
            SendInteraction(targetState, compliment);
        }

        private void HandleInsult()
        {
            if (targetNpc == null || targetState == null)
                return;
            if (targetState.WaitingForAI) return;

            float dist = Vector3.Distance(
                Game.Player.Character.Position,
                targetNpc.Position);
            if (dist > INTERACT_DISTANCE) return;

            float gameTime = Game.GameTime / 1000f;
            if ((gameTime - lastRequestTime)
                < RequestCooldown)
                return;
            lastRequestTime = gameTime;

            string insult = INSULTS[
                _rand.Next(INSULTS.Length)];

            targetState.RecordInsult(Game.GameTime / 1000f);
            targetState.LastInteractionType = "insult";
            SendInteraction(targetState, insult);
        }

        private void HandleVoiceInput()
        {
            if (targetNpc == null || targetState == null)
                return;
            if (targetState.WaitingForAI) return;
            if (speechManager.IsRecording) return;

            float dist = Vector3.Distance(
                Game.Player.Character.Position,
                targetNpc.Position);
            if (dist > INTERACT_DISTANCE) return;

            ShowNotification("录音中...请说话！");

            int npcHandle = targetNpc.Handle;

            speechManager.RecordAndTranscribe(
                text =>
                {
                    ShowNotification("你: " + text);

                    if (npcStates.TryGetValue(npcHandle,
                        out var state))
                    {
                        state.LastInteractionType = "talk";
                        SendInteraction(state, text);
                    }
                },
                ex =>
                {
                    ShowNotification(
                        "语音失败: " + ex.Message);
                }
            );
        }

        private void HandleTextInput()
        {
            if (targetNpc == null || targetState == null)
                return;
            if (targetState.WaitingForAI) return;

            float dist = Vector3.Distance(
                Game.Player.Character.Position,
                targetNpc.Position);
            if (dist > INTERACT_DISTANCE) return;

            float gameTime = Game.GameTime / 1000f;
            if ((gameTime - lastRequestTime)
                < RequestCooldown)
                return;

            string input = Game.GetUserInput("");
            if (!string.IsNullOrEmpty(input)
                && input.Length > 30)
                input = input.Substring(0, 30);

            if (!string.IsNullOrEmpty(input)
                && input.Trim().Length > 0)
            {
                lastRequestTime = Game.GameTime / 1000f;
                ShowNotification("你: " + input);
                targetState.LastInteractionType = "talk";
                SendInteraction(targetState, input.Trim());
            }
        }

        // ===== UPDATED: Config values + Awakening context =====
        private void SendInteraction(NPCState state,
            string playerText)
        {
            if (!state.IsValid()) return;

            state.IsInteracting = true;
            state.Ped.Task.TurnTo(Game.Player.Character);

            // Process awakening
            if (aiManager.Config.Awakening.Enabled)
            {
                awakenSystem.ProcessInteraction(
                    state, playerText);
            }

            var mem = memoryManager.GetMemory(
                state.Ped, state.Personality);

            if (aiManager.Config.Performance.StrictMode)
            {
                string instant = npcBrain.GetInstantResponse(
                    state.Personality, playerText,
                    mem.Relationship);
                if (!string.IsNullOrWhiteSpace(instant))
                {
                    float gameTime = Game.GameTime / 1000f;
                    string emotion = GuessEmotion(
                        state.Personality, playerText);

                    state.RecordConversationTurn("user",
                        playerText, gameTime);
                    state.RecordConversationTurn("assistant",
                        instant, gameTime);

                    string interactionType =
                        state.LastInteractionType ?? "talk";
                    state.LastInteractionType = "talk";
                    memoryManager.RecordInteraction(
                        state.Ped,
                        interactionType,
                        instant,
                        emotion,
                        state.ThreatLevel,
                        GetTimeOfDay(),
                        gameTime);

                    ShowNPCResponse(state, instant);

                    state.WaitingForAI = false;
                    state.IsInteracting = false;
                    state.InteractionCount++;
                    state.LastRequestTime = gameTime;
                    return;
                }
            }

            // COMPLEX INPUT → needs LLM
            state.WaitingForAI = true;
            string systemPrompt;
            string userPrompt;
            var historyTurns = state.ConversationHistory
                .Skip(Math.Max(0,
                    state.ConversationHistory.Count - 6))
                .ToList();
            if (aiManager.Config.LLM.Provider == "cloud")
            {
                string genderHint = state.IsMale
                    ? "You are male." : "You are female.";

                systemPrompt =
                    $"You are a GTA5 NPC named {state.NpcName}. " +
                    genderHint + " " +
                    $"Personality: {state.Personality}. " +
                    "Reply in Chinese. Format: dialogue|action|emotion. " +
                    "Actions: speak,idle,wave,flee. " +
                    "Emotions: happy,angry,sad,scared,neutral. " +
                    "One line only. Under 25 chars.";

                string context = "";
                string memContext = memoryManager
                    .BuildMemoryContext(state.Ped, 150);
                if (!string.IsNullOrEmpty(memContext))
                    context += memContext + " ";

                if (aiManager.Config.Awakening.Enabled)
                {
                    string awakenCtx = awakenSystem
                        .BuildAwakenContext(state);
                    if (!string.IsNullOrEmpty(awakenCtx))
                        context += awakenCtx + " ";
                }

                string playerKnowledge =
                    state.BuildPlayerKnowledgeContext();
                if (!string.IsNullOrEmpty(playerKnowledge))
                    context += playerKnowledge + " ";

                context += $"Player says: \"{playerText}\"";
                userPrompt = context;
            }
            else
            {
                string genderCN = state.IsMale ? "男人" : "女人";
                string personalityDesc;
                switch (state.Personality)
                {
                    case "友善":
                        personalityDesc = "热情友好，喜欢帮助别人";
                        break;
                    case "暴躁":
                        personalityDesc = "脾气暴躁，容易发火";
                        break;
                    case "胆小":
                        personalityDesc = "胆小怕事，说话小心翼翼";
                        break;
                    case "搞笑":
                        personalityDesc = "幽默搞笑，喜欢开玩笑";
                        break;
                    case "冷漠":
                        personalityDesc = "冷淡无情，不爱说话";
                        break;
                    default:
                        personalityDesc = "普通人";
                        break;
                }

                string relationDesc = "";
                if (mem.Relationship <= -50)
                    relationDesc = "你非常讨厌对方。";
                else if (mem.Relationship <= -20)
                    relationDesc = "你不太喜欢对方。";
                else if (mem.Relationship >= 40)
                    relationDesc = "你觉得对方是朋友。";
                else if (mem.Relationship >= 20)
                    relationDesc = "你对对方印象还不错。";

                string dangerDesc = "";
                if (state.SawPlayerKill)
                    dangerDesc = "你亲眼看到对方杀过人，你非常害怕。";
                else if (state.SawPlayerShoot)
                    dangerDesc = "你看到对方开过枪，你很紧张。";
                else if (state.SawPlayerSpeeding)
                    dangerDesc = "你见过对方疯狂飙车。";

                string awakenDesc = "";
                if (aiManager.Config.Awakening.Enabled)
                {
                    awakenDesc = awakenSystem
                        .BuildAwakenContext(state);
                }

                systemPrompt =
                    $"你是{personalityDesc}的{genderCN}，名叫{state.NpcName}。\n" +
                    $"你的外貌：{state.Appearance}。\n" +
                    "【重要规则】\n" +
                    "1. 只输出一行，格式：说的话|动作|情绪\n" +
                    "2. 不要描述动作（禁止用*号或括号描述行为）\n" +
                    "3. 不要写心理活动，不要解释，不要分析\n" +
                    "4. 直接说话，像真人对话一样\n\n" +
                    "动作选一个：speak idle wave flee\n" +
                    "情绪选一个：happy angry sad scared neutral\n\n" +
                    "正确示范：\n" +
                    "你好啊！|speak|happy\n" +
                    "滚远点！|speak|angry\n" +
                    "你别过来...|speak|scared\n" +
                    "哈哈笑死我了|wave|happy\n" +
                    "救命啊！|flee|scared\n\n" +
                    "错误示范（绝对不要这样）：\n" +
                    "*后退一步* 你好\n" +
                    "（微笑着说）你好\n" +
                    "我想了想，决定说：你好\n";

                var perception = npcPerception.Perceive(
                    state, Game.Player.Character, npcStates);
                userPrompt =
                    $"现在是{perception.TimeOfDay}，" +
                    $"{perception.Weather}，" +
                    $"地点是{perception.ZoneName}。";

                if (!string.IsNullOrEmpty(relationDesc))
                    userPrompt += relationDesc;
                if (!string.IsNullOrEmpty(dangerDesc))
                    userPrompt += dangerDesc;
                if (!string.IsNullOrEmpty(awakenDesc))
                    userPrompt += awakenDesc;

                userPrompt += $"对方对你说：\"{playerText}\"";
            }

            var messages = new JArray
            {
                new JObject
                {
                    ["role"] = "system",
                    ["content"] = systemPrompt
                }
            };

            foreach (var turn in historyTurns)
            {
                messages.Add(new JObject
                {
                    ["role"] = turn.Role,
                    ["content"] = turn.Content
                });
            }
            messages.Add(new JObject
            {
                ["role"] = "user",
                ["content"] = userPrompt
            });

            var payloadObj = new JObject
            {
                ["model"] = aiManager.ModelName,
                ["messages"] = messages,
                ["max_tokens"] = aiManager.Config.MaxTokens,
                ["temperature"] = aiManager.Config.Temperature
            };

            string payload = payloadObj.ToString(
                Formatting.None);

            int npcHandle = state.Ped.Handle;
            _pendingPlayerInputs[npcHandle] = playerText;

            if (aiManager.Config.LLM.Provider == "local")
            {
                aiManager.StreamForNpcAsync(
                    npcHandle, payload,
                    partialText =>
                    {
                        if (npcStates.TryGetValue(npcHandle, out var s))
                        {
                            s.LastLLMDialogue = partialText;
                            s.DialogueShowTime = Game.GameTime / 1000f;
                        }
                    },
                    resp => responseQueue.Enqueue(resp),
                    ex =>
                    {
                        if (npcStates.TryGetValue(npcHandle, out var s))
                        {
                            s.WaitingForAI = false;
                            s.IsInteracting = false;
                        }
                        _pendingPlayerInputs.TryRemove(
                            npcHandle, out _);
                        ShowNotification("AI Error: " + ex.Message);
                    }
                );
            }
            else
            {
                aiManager.RequestForNpcAsync(
                    npcHandle, payload,
                    resp => responseQueue.Enqueue(resp),
                    ex =>
                    {
                        state.WaitingForAI = false;
                        state.IsInteracting = false;
                        _pendingPlayerInputs.TryRemove(
                            npcHandle, out _);
                        ShowNotification("AI Error: " + ex.Message);
                    }
                );
            }
        }

        public void RequestNPCChat(NPCState speaker,
            NPCState listener)
        {
            if (speaker.WaitingForAI) return;

            Task.Run(async () =>
            {
                if (!await _npcChatSemaphore.WaitAsync(0))
                    return;
                try
                {
                    var config = aiManager.Config;
                    string endpoint = config.LLM.LightEndpoint;
                    string model = config.LLM.LightModel;

                    string personalityDesc;
                    switch (speaker.Personality)
                    {
                        case "友善":
                            personalityDesc = "热情友好的"; break;
                        case "暴躁":
                            personalityDesc = "脾气暴躁的"; break;
                        case "胆小":
                            personalityDesc = "胆小的"; break;
                        case "搞笑":
                            personalityDesc = "搞笑的"; break;
                        case "冷漠":
                            personalityDesc = "冷淡的"; break;
                        default:
                            personalityDesc = "普通的"; break;
                    }

                    string prompt =
                        $"你是一个{personalityDesc}人。";

                    if (speaker.KnowsPlayerIsDangerous)
                    {
                        if (speaker.SawPlayerKill)
                            prompt += "你刚才看到有人被杀了！";
                        else if (speaker.SawPlayerShoot)
                            prompt += "你听到附近有枪声！";
                        else if (speaker.SawPlayerSpeeding)
                            prompt += "刚才有辆车差点撞到你！";
                    }

                    string zone = NPCPerception.GetZoneName(
                        speaker.Ped.Position);
                    var zoneEvent = NPCPerception.GetZoneEvent(zone);
                    if (zoneEvent != null && zoneEvent.Type >= EventType.Shooting)
                    {
                        prompt += "这附近最近不太安全。";
                    }

                    prompt += "对旁边的人用中文说一句话（15字以内）。只输出说的话。";

                    var payload = new JObject
                    {
                        ["model"] = model,
                        ["messages"] = new JArray
                        {
                            new JObject
                            {
                                ["role"] = "user",
                                ["content"] = prompt
                            }
                        },
                        ["max_tokens"] = 20,
                        ["temperature"] = 0.8
                    };

                    string body = await aiManager.PostRawAsync(
                        endpoint,
                        payload.ToString(Formatting.None),
                        30).ConfigureAwait(false);

                    var aiResp = aiManager.ParsePublic(body);
                    string text = aiResp?.dialogue ?? "";
                    if (text == "..."
                        || text == "[无响应]"
                        || text == "[解析失败]")
                    {
                        text = "";
                    }

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (text.Length > 15)
                        {
                            string cut = text.Substring(0, 15);
                            int lastPunc = cut.LastIndexOfAny(
                                new[] { '，', '。', '！', '？', '…' });
                            text = lastPunc > 7
                                ? cut.Substring(0, lastPunc + 1)
                                : cut;
                        }

                        responseQueue.Enqueue(new AIResponse
                        {
                            action = "speak",
                            dialogue = text,
                            emotion = "neutral",
                            NpcHandle = speaker.Handle
                        });

                        if (speaker.KnowsPlayerIsDangerous
                            && !listener
                                .KnowsPlayerIsDangerous)
                        {
                            listener.KnowsPlayerIsDangerous
                                = true;
                            listener.PlayerReputation -= 15f;

                            if (speaker.SawPlayerKill)
                            {
                                listener.SawPlayerKill = true;
                                listener.PlayerReputation
                                    -= 20f;
                            }

                            listener.PlayerReputation
                                = Math.Max(-100f,
                                    listener.PlayerReputation);
                        }

                        speaker.IsInteracting = false;
                        listener.IsInteracting = false;
                    }
                }
                catch { }
                finally
                {
                    speaker.IsInteracting = false;
                    listener.IsInteracting = false;
                    _npcChatSemaphore.Release();
                }
            });
        }

        public void ShowNPCChatPublic(NPCState speaker,
            NPCState listener, string dialogue)
        {
            ShowNPCResponse(speaker, dialogue);
        }

        private string GuessEmotion(string personality,
            string playerText)
        {
            bool isInsult = playerText.Contains("丑")
                || playerText.Contains("蠢")
                || playerText.Contains("滚")
                || playerText.Contains("废物");

            bool isCompliment = playerText.Contains("帅")
                || playerText.Contains("好看")
                || playerText.Contains("不错");

            switch (personality)
            {
                case "友善":
                    return isInsult ? "sad" : "happy";
                case "暴躁":
                    return "angry";
                case "胆小":
                    return isInsult ? "scared" : "neutral";
                case "搞笑":
                    return "happy";
                case "冷漠":
                    return "neutral";
                default:
                    return "neutral";
            }
        }

        // ===== UPDATED: Flee override for safety =====
        private void ProcessResponses()
        {
            float gameTime = Game.GameTime / 1000f;

            while (responseQueue.TryDequeue(out var resp))
            {
                if (!npcStates.TryGetValue(resp.NpcHandle,
                    out var state))
                {
                    _pendingPlayerInputs.TryRemove(
                        resp.NpcHandle, out _);
                    continue;
                }
                if (!state.IsValid())
                {
                    _pendingPlayerInputs.TryRemove(
                        resp.NpcHandle, out _);
                    continue;
                }

                string pendingInput;
                if (_pendingPlayerInputs.TryRemove(
                    resp.NpcHandle, out pendingInput)
                    && !string.IsNullOrWhiteSpace(pendingInput))
                {
                    state.RecordConversationTurn(
                        "user", pendingInput, gameTime);
                }

                string action = resp.action ?? "speak";
                string dialogue = resp.dialogue ?? "";
                string emotion = resp.emotion ?? "neutral";

                // ===== Flee override =====
                if (action == "flee")
                {
                    var player = Game.Player.Character;
                    bool realDanger = player.IsShooting
                        || IsPlayerAimingAt(state.Ped);

                    if (!realDanger)
                    {
                        action = "speak";
                    }
                }

                // 暴躁 never flees from words
                if (state.Personality == "暴躁"
                    && action == "flee")
                {
                    action = "speak";
                    if (string.IsNullOrEmpty(dialogue)
                        || dialogue.Contains("跑")
                        || dialogue.Contains("滚开"))
                    {
                        string[] angryLines = new[]
                        {
                            "你说啥？再说一遍！",
                            "找死是吧？",
                            "来啊，谁怕谁！",
                            "老子弄死你！",
                            "有种你动我试试！"
                        };
                        dialogue = angryLines[
                            _rand.Next(angryLines.Length)];
                    }
                    emotion = "angry";
                }

                if (string.IsNullOrWhiteSpace(dialogue)
                    || dialogue == "...")
                {
                    string[] options;
                    switch (state.Personality)
                    {
                        case "友善":
                            options = new[]
                            {
                                "你好呀！",
                                "嗯？有什么事吗？",
                                "今天天气不错呢！",
                                "你看起来不错啊！",
                                "需要帮忙吗？",
                                "很高兴见到你！"
                            };
                            break;
                        case "暴躁":
                            options = new[]
                            {
                                "干嘛！",
                                "看什么看！",
                                "少烦我！",
                                "有事快说！",
                                "你找死啊！",
                                "滚远点！"
                            };
                            break;
                        case "胆小":
                            options = new[]
                            {
                                "你...你好",
                                "别吓我...",
                                "你要干嘛？",
                                "我不想惹麻烦...",
                                "求你别伤害我...",
                                "我什么都没看到..."
                            };
                            break;
                        case "搞笑":
                            options = new[]
                            {
                                "哟！你好啊！",
                                "嘿嘿你找我？",
                                "哈哈你好你好！",
                                "有啥好玩的吗？",
                                "兄弟！来一个？",
                                "笑一个嘛！"
                            };
                            break;
                        case "冷漠":
                            options = new[]
                            {
                                "嗯。",
                                "什么事。",
                                "...走开。",
                                "无所谓。",
                                "别烦我。",
                                "......"
                            };
                            break;
                        default:
                            options = new[]
                            {
                                "嗯？",
                                "你好。",
                                "什么事？"
                            };
                            break;
                    }
                    dialogue = options[_rand.Next(options.Length)];
                    action = "speak";
                }

                if (!string.IsNullOrWhiteSpace(dialogue)
                    && dialogue != "...")
                {
                    state.RecordConversationTurn(
                        "assistant", dialogue, gameTime);
                }

                string interactionType =
                    state.LastInteractionType ?? "talk";
                state.LastInteractionType = "talk";
                memoryManager.RecordInteraction(
                    state.Ped,
                    interactionType,
                    dialogue,
                    emotion,
                    state.ThreatLevel,
                    GetTimeOfDay(),
                    gameTime
                );

                ShowNPCResponse(state, dialogue);

                state.Ped.Task.ClearAll();
                switch (action)
                {
                    case "flee":
                        state.Ped.Task.FleeFrom(
                            Game.Player.Character);
                        break;
                    case "wave":
                        RequestAnim(state.Ped, state,
                            "anim@mp_player_intcelebrationmale@wave",
                            "wave");
                        break;
                    case "speak":
                    case "idle":
                    default:
                        state.Ped.Task.TurnTo(
                            Game.Player.Character);
                        state.Ped.Task.StandStill(8000);
                        break;
                }

                state.WaitingForAI = false;
                state.IsInteracting = false;
                state.InteractionCount++;
                state.LastRequestTime = gameTime;
            }
        }

        private void ShowNPCResponse(NPCState state,
            string dialogue)
        {
            float gameTime = Game.GameTime / 1000f;
            state.LastLLMDialogue = dialogue;
            state.DialogueShowTime = gameTime;
            if ((gameTime - state.LastNotifTime) < 0.5f) return;
            state.LastNotifTime = gameTime;

            string pTag = "";
            switch (state.Personality)
            {
                case "友善": pTag = "友善"; break;
                case "暴躁": pTag = "暴躁"; break;
                case "胆小": pTag = "胆小"; break;
                case "搞笑": pTag = "搞笑"; break;
                case "冷漠": pTag = "冷漠"; break;
                default: pTag = "NPC"; break;
            }

            Function.Call(
                Hash.BEGIN_TEXT_COMMAND_THEFEED_POST,
                "STRING");
            Function.Call(
                Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME,
                dialogue);
            Function.Call(
                Hash.END_TEXT_COMMAND_THEFEED_POST_MESSAGETEXT,
                "CHAR_DEFAULT", "CHAR_DEFAULT", false, 0,
                $"{state.NpcName}[{pTag}]", "");

            if (voiceEnabled
                && !string.IsNullOrEmpty(dialogue)
                && !state.IsPlayingVoice)
            {
                string voice = voiceManager.GetVoiceForNpc(
                    state.StableId, state.IsMale);

                string emotion = "neutral";
                switch (state.Personality)
                {
                    case "暴躁": emotion = "angry"; break;
                    case "胆小": emotion = "scared"; break;
                    case "友善": emotion = "happy"; break;
                    case "搞笑": emotion = "happy"; break;
                    case "冷漠": emotion = "cold"; break;
                }

                state.IsPlayingVoice = true;
                voiceManager.SpeakAsync(
                    state.Ped.Handle, dialogue,
                    voice, emotion,
                    () => { state.IsPlayingVoice = false; },
                    ex => { state.IsPlayingVoice = false; }
                );
            }
        }

        private void DrawNPCText(NPCState state)
        {
            if (!state.IsValid()) return;

            var ped = state.Ped;
            float dist = Vector3.Distance(
                Game.Player.Character.Position,
                ped.Position);
            if (dist > 15f) return;

            Vector3 headPos =
                ped.Bones[Bone.SkelHead].Position;
            headPos.Z += 0.3f;

            int r = 255, g = 255, b = 255;
            string pTag = "";
            switch (state.Personality)
            {
                case "友善":
                    r = 100; g = 255; b = 100;
                    pTag = "友善"; break;
                case "暴躁":
                    r = 255; g = 80; b = 80;
                    pTag = "暴躁"; break;
                case "胆小":
                    r = 255; g = 255; b = 100;
                    pTag = "胆小"; break;
                case "搞笑":
                    r = 255; g = 165; b = 0;
                    pTag = "搞笑"; break;
                case "冷漠":
                    r = 180; g = 180; b = 180;
                    pTag = "冷漠"; break;
                default:
                    r = 255; g = 255; b = 255;
                    pTag = "NPC"; break;
            }

            string awakenIcon = "";
            switch (state.Stage)
            {
                case AwakenStage.Sleeping:
                    break;
                case AwakenStage.Dreaming:
                    awakenIcon = " ?"; break;
                case AwakenStage.Questioning:
                    awakenIcon = " ??"; break;
                case AwakenStage.Aware:
                    awakenIcon = " !!!";
                    r = 180; g = 100; b = 255;
                    break;
                case AwakenStage.Awakened:
                    awakenIcon = " 觉醒";
                    r = 255; g = 215; b = 0;
                    break;
            }

            string status =
                $"{state.NpcName} [{pTag}]{awakenIcon}";
            if (state.WaitingForAI)
                status += " ...";
            else if (state.IsPlayingVoice)
                status += " >>>";

            float scale = Math.Max(
                0.25f, 0.4f - (dist / 40f));
            DrawText3D(headPos, status, r, g, b, scale);

            if (state.AwakenLevel > 0 && dist < 8f)
            {
                Vector3 barPos = headPos;
                barPos.Z += 0.15f;
                int pct = state.AwakenLevel;
                string bar = $"觉醒:{pct}%";
                DrawText3D(barPos, bar, 180, 100, 255, 0.2f);

                if (state.IsAutonomous
                    && !string.IsNullOrEmpty(state.CurrentGoal)
                    && state.CurrentGoal != "none")
                {
                    Vector3 goalPos = barPos;
                    goalPos.Z += 0.12f;
                    DrawText3D(goalPos,
                        state.CurrentGoal,
                        255, 215, 0, 0.18f);
                }
            }

            float gameTime = Game.GameTime / 1000f;
            if (state.HasActiveDialogue(gameTime) && dist < 12f)
            {
                string dialogueText = state.LastLLMDialogue ?? "";

                // Safety: NEVER show raw code/JSON/thinking above head
                if (!string.IsNullOrEmpty(dialogueText)
                    && !dialogueText.Contains("{")
                    && !dialogueText.Contains("}")
                    && !dialogueText.Contains("\"d\"")
                    && !dialogueText.Contains("\"a\"")
                    && !dialogueText.Contains("\"e\"")
                    && !dialogueText.StartsWith("Thinking",
                        StringComparison.OrdinalIgnoreCase)
                    && !dialogueText.StartsWith("1.",
                        StringComparison.Ordinal)
                    && !dialogueText.Contains("**")
                    && !dialogueText.Contains("Analyze"))
                {
                    Vector3 dialoguePos = headPos;
                    dialoguePos.Z += 0.45f;

                    var lines = new List<string>();
                    for (int i = 0; i < dialogueText.Length; i += 15)
                    {
                        int len = Math.Min(
                            15, dialogueText.Length - i);
                        lines.Add(dialogueText.Substring(i, len));
                    }

                    for (int i = 0; i < Math.Min(lines.Count, 3); i++)
                    {
                        Vector3 linePos = dialoguePos;
                        linePos.Z -= i * 0.12f;
                        DrawText3D(linePos, lines[i],
                            255, 255, 255, 0.28f);
                    }
                }
            }
        }

        private void ProcessIdleBehavior(NPCState state,
            Ped player, float dist, float gameTime)
        {
            // Awakening murmurs
            if (state.Stage != AwakenStage.Sleeping
                && dist < 10f
                && !state.WaitingForAI
                && (gameTime - state.LastRequestTime) > 20f
                && _rand.Next(100) < 8)
            {
                string murmur = awakenSystem
                    .GetRandomAwakenDialogue(state);
                if (murmur != null)
                {
                    state.LastRequestTime = gameTime;
                    ShowNPCResponse(state, murmur);
                    return;
                }
            }

            if (state.IsInteracting) return;
            if (state.WaitingForAI) return;
            if (dist < INTERACT_DISTANCE) return;
            if ((gameTime - state.LastRequestTime) < 15f)
                return;
            if (_rand.Next(100) > 10) return;

            state.LastRequestTime = gameTime;

            switch (state.Personality)
            {
                case "友善":
                    int friendlyAction = _rand.Next(4);
                    switch (friendlyAction)
                    {
                        case 0:
                            state.Ped.Task.Wander();
                            break;
                        case 1:
                            RequestAnim(state.Ped, state,
                                "amb@world_human_stand_mobile@male@text@base",
                                "base");
                            break;
                        case 2:
                            RequestAnim(state.Ped, state,
                                "anim@mp_player_intcelebrationmale@wave",
                                "wave");
                            break;
                        case 3:
                            state.Ped.Task.StandStill(10000);
                            break;
                    }
                    break;
                case "暴躁":
                    int angryAction = _rand.Next(3);
                    switch (angryAction)
                    {
                        case 0:
                            state.Ped.Task.Wander();
                            break;
                        case 1:
                            RequestAnim(state.Ped, state,
                                "anim@mp_player_intcelebrationmale@finger_point",
                                "finger_point");
                            break;
                        case 2:
                            state.Ped.Task.StandStill(5000);
                            break;
                    }
                    break;
                case "胆小":
                    state.Ped.Task.LookAt(
                        state.Ped.Position +
                        new Vector3(
                            _rand.Next(-10, 10),
                            _rand.Next(-10, 10), 0),
                        2000);
                    break;
                case "搞笑":
                    int funnyAction = _rand.Next(3);
                    switch (funnyAction)
                    {
                        case 0:
                            RequestAnim(state.Ped, state,
                                "anim@mp_player_intcelebrationmale@air_guitar",
                                "air_guitar");
                            break;
                        case 1:
                            state.Ped.Task.Wander();
                            break;
                        case 2:
                            RequestAnim(state.Ped, state,
                                "anim@mp_player_intcelebrationmale@jazz_hands",
                                "jazz_hands");
                            break;
                    }
                    break;
                case "冷漠":
                    state.Ped.Task.StandStill(15000);
                    break;
            }
        }

        private static void DrawText3D(Vector3 pos,
            string text, int r, int g, int b, float scale)
        {
            var outX = new OutputArgument();
            var outY = new OutputArgument();
            bool visible = Function.Call<bool>(
                Hash.GET_SCREEN_COORD_FROM_WORLD_COORD,
                pos.X, pos.Y, pos.Z, outX, outY);

            if (!visible) return;

            float sx = outX.GetResult<float>();
            float sy = outY.GetResult<float>();

            Function.Call(Hash.SET_TEXT_FONT, 0);
            Function.Call(Hash.SET_TEXT_SCALE, scale, scale);
            Function.Call(Hash.SET_TEXT_COLOUR,
                0, 0, 0, 220);
            Function.Call(Hash.SET_TEXT_CENTRE, true);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.SET_TEXT_DROP_SHADOW);
            Function.Call(
                Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT,
                "STRING");
            Function.Call(
                Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME,
                text);
            Function.Call(
                Hash.END_TEXT_COMMAND_DISPLAY_TEXT,
                sx + 0.001f, sy + 0.001f);

            Function.Call(Hash.SET_TEXT_FONT, 0);
            Function.Call(Hash.SET_TEXT_SCALE, scale, scale);
            Function.Call(Hash.SET_TEXT_COLOUR,
                r, g, b, 255);
            Function.Call(Hash.SET_TEXT_CENTRE, true);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(
                Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT,
                "STRING");
            Function.Call(
                Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME,
                text);
            Function.Call(
                Hash.END_TEXT_COMMAND_DISPLAY_TEXT,
                sx, sy);
        }

        private bool IsPlayerAimingAt(Ped target)
        {
            if (!Function.Call<bool>(
                Hash.IS_PLAYER_FREE_AIMING,
                Game.Player.Handle))
                return false;

            var outEntity = new OutputArgument();
            if (Function.Call<bool>(
                Hash.GET_ENTITY_PLAYER_IS_FREE_AIMING_AT,
                Game.Player.Handle, outEntity))
            {
                return outEntity.GetResult<int>()
                    == target.Handle;
            }
            return false;
        }

        private void RequestAnim(Ped ped, NPCState state,
            string dict, string anim)
        {
            Function.Call(Hash.REQUEST_ANIM_DICT, dict);
            state.PendingAnimDict = dict;
            state.PendingAnimName = anim;
            state.AnimDictRequested = true;
        }

        private string GetTimeOfDay()
        {
            int hour = Function.Call<int>(
                Hash.GET_CLOCK_HOURS);
            if (hour >= 6 && hour < 12) return "早上";
            if (hour >= 12 && hour < 18) return "下午";
            if (hour >= 18 && hour < 22) return "晚上";
            return "深夜";
        }

        private void OnKeyDown(object sender,
            System.Windows.Forms.KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case System.Windows.Forms.Keys.G:
                    HandleCompliment();
                    break;
                case System.Windows.Forms.Keys.H:
                    HandleInsult();
                    break;
                case System.Windows.Forms.Keys.J:
                    HandleVoiceInput();
                    break;
                case System.Windows.Forms.Keys.T:
                    HandleTextInput();
                    break;
                case System.Windows.Forms.Keys.F8:
                    voiceEnabled = !voiceEnabled;
                    ShowNotification(
                        voiceEnabled
                            ? "语音已开启"
                            : "语音已关闭");
                    break;
                case System.Windows.Forms.Keys.F9:
                    memoryManager.SaveAll();
                    ShowNotification("记忆已保存");
                    break;
                case System.Windows.Forms.Keys.F6:
                    if (targetState != null)
                    {
                        var mem = memoryManager.GetMemory(
                            targetState.Ped,
                            targetState.Personality);
                        int displayRep = (mem.Relationship
                            + (int)targetState.PlayerReputation) / 2;

                        string stageText =
                            targetState.Stage.ToString();

                        ShowNotification(
                            $"~b~{targetState.NpcName}~w~ " +
                            $"[{targetState.Personality}]\n" +
                            $"外貌: {targetState.Appearance}\n" +
                            $"综合好感:{displayRep}\n" +
                            $"好感:{mem.Relationship} " +
                            $"声誉:{targetState.PlayerReputation:F0}\n" +
                            $"觉醒:{targetState.AwakenLevel}% " +
                            $"[{stageText}]\n" +
                            $"{targetState.Needs}");
                    }
                    else
                    {
                        ShowNotification("附近没有NPC");
                    }
                    break;
            }
        }
    }
}
