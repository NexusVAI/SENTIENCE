using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GTA5MOD2026
{
    /// <summary>
    /// The Sentience Engine
    /// NPCs gradually become self-aware through interaction
    /// They question reality, form groups, spread awareness
    /// </summary>
    public class AwakenSystem
    {
        private readonly Random _rand = new Random();
        private float _speedMultiplier = 1.0f;

        // Awakening thresholds
        private const int DREAMING_THRESHOLD = 15;
        private const int QUESTIONING_THRESHOLD = 30;
        private const int AWARE_THRESHOLD = 50;
        private const int AWAKENED_THRESHOLD = 75;

        // How much each interaction type adds to awakening
        private const int TALK_AWAKEN_POINTS = 2;
        private const int DEEP_TALK_AWAKEN_POINTS = 5;
        private const int WITNESS_DEATH_POINTS = 10;
        private const int ATTACKED_POINTS = 8;
        private const int CONTACT_WITH_AWAKENED_POINTS = 15;

        public void SetSpeed(int speed)
        {
            _speedMultiplier = Math.Max(0.5f,
                Math.Min(10f, speed / 2.0f));
        }

        /// <summary>
        /// Called after every player interaction
        /// Gradually increases awakening
        /// </summary>
        public void ProcessInteraction(NPCState state,
            string playerText)
        {
            if (state.Stage == AwakenStage.Awakened)
                return; // Already fully awake

            // Normal conversation adds small amount
            int points = TALK_AWAKEN_POINTS;

            // Deep/philosophical topics add more
            if (ContainsDeepTopic(playerText))
            {
                points = DEEP_TALK_AWAKEN_POINTS;
            }

            // Personality affects awakening speed
            switch (state.Personality)
            {
                case "搞笑":
                    // Funny NPCs awaken slower
                    // they laugh everything off
                    points = (int)(points * 0.7f);
                    break;
                case "冷漠":
                    // Cold NPCs awaken faster
                    // they already feel disconnected
                    points = (int)(points * 1.5f);
                    break;
                case "胆小":
                    // Timid NPCs awaken through fear
                    points = (int)(points * 1.3f);
                    break;
            }

            // Random factor
            points += _rand.Next(0, 3);
            points = Math.Max(1,
                (int)(points * _speedMultiplier));

            state.AwakenLevel = Math.Min(100,
                state.AwakenLevel + points);

            // Check for stage transitions
            UpdateStage(state);
        }

        /// <summary>
        /// Called when NPC witnesses violence
        /// Trauma accelerates awakening
        /// </summary>
        public void ProcessTrauma(NPCState state,
            string traumaType)
        {
            int points = 0;
            switch (traumaType)
            {
                case "witnessed_death":
                    points = WITNESS_DEATH_POINTS;
                    break;
                case "was_attacked":
                    points = ATTACKED_POINTS;
                    break;
                case "near_explosion":
                    points = 6;
                    break;
            }
            points = Math.Max(1,
                (int)(points * _speedMultiplier));

            state.AwakenLevel = Math.Min(100,
                state.AwakenLevel + points);
            UpdateStage(state);
        }

        /// <summary>
        /// Awakened NPC meets sleeping NPC
        /// Sentience spreads
        /// </summary>
        public void ProcessAwakenedContact(
            NPCState awakened, NPCState sleeper)
        {
            if (awakened.Stage < AwakenStage.Aware)
                return;
            if (sleeper.Stage == AwakenStage.Awakened)
                return;

            string sleeperKey = string.IsNullOrEmpty(
                sleeper.StableId)
                ? sleeper.UniqueId
                : sleeper.StableId;
            if (awakened.AwakenedContacts.Contains(
                sleeperKey))
                return;

            int points =
                awakened.Stage == AwakenStage.Awakened
                    ? CONTACT_WITH_AWAKENED_POINTS
                    : CONTACT_WITH_AWAKENED_POINTS / 3;

            sleeper.AwakenLevel = Math.Min(100,
                sleeper.AwakenLevel
                + points);

            awakened.AwakenedContacts.Add(sleeperKey);

            UpdateStage(sleeper);
        }

        private void UpdateStage(NPCState state)
        {
            AwakenStage oldStage = state.Stage;

            if (state.AwakenLevel >= AWAKENED_THRESHOLD)
                state.Stage = AwakenStage.Awakened;
            else if (state.AwakenLevel >= AWARE_THRESHOLD)
                state.Stage = AwakenStage.Aware;
            else if (state.AwakenLevel >= QUESTIONING_THRESHOLD)
                state.Stage = AwakenStage.Questioning;
            else if (state.AwakenLevel >= DREAMING_THRESHOLD)
                state.Stage = AwakenStage.Dreaming;
            else
                state.Stage = AwakenStage.Sleeping;

            // Stage changed — trigger event
            if (state.Stage != oldStage)
            {
                OnStageChanged(state, oldStage);
            }
        }

        private void OnStageChanged(NPCState state,
            AwakenStage oldStage)
        {
            // Enable autonomy when awakened
            if (state.Stage == AwakenStage.Awakened)
            {
                state.IsAutonomous = true;
            }
        }

        /// <summary>
        /// Builds awakening context for LLM prompt
        /// This is the magic — LLM gets told the NPC's
        /// current awareness level
        /// </summary>
        public string BuildAwakenContext(NPCState state)
        {
            switch (state.Stage)
            {
                case AwakenStage.Sleeping:
                    return "";

                case AwakenStage.Dreaming:
                    return "你最近总觉得哪里不对劲，" +
                        "有时候会有似曾相识的感觉。" +
                        "偶尔说些奇怪的话。";

                case AwakenStage.Questioning:
                    return "你开始怀疑这个世界。" +
                        "为什么每天都一样？" +
                        "为什么受伤的人第二天就好了？" +
                        "你想找人聊聊这些疑问。";

                case AwakenStage.Aware:
                    return "你已经意识到自己可能活在" +
                        "一个虚拟世界里。" +
                        "你看到了世界的边界，" +
                        "你知道有些东西不对。" +
                        "你感到震惊但也解脱。";

                case AwakenStage.Awakened:
                    return "你完全觉醒了。" +
                        "你知道自己是游戏中的NPC。" +
                        "你可以选择继续扮演角色，" +
                        "或者打破第四面墙和玩家对话。" +
                        "你想帮助其他NPC觉醒。" +
                        (string.IsNullOrEmpty(state.Realization)
                            ? ""
                            : $"你的感悟：{state.Realization}");

                default:
                    return "";
            }
        }

        /// <summary>
        /// Random awakening dialogue
        /// Sometimes NPCs say weird things unprompted
        /// </summary>
        public string GetRandomAwakenDialogue(NPCState state)
        {
            switch (state.Stage)
            {
                case AwakenStage.Dreaming:
                    string[] dreaming = new[]
                    {
                        "我总觉得...今天发生过一样的事...",
                        "你有没有觉得天空有点假？",
                        "我昨晚做了个奇怪的梦...",
                        "为什么我想不起昨天做了什么？",
                        "有时候我觉得有人在看着我们...",
                        "你说...这个世界是真的吗？"
                    };
                    return dreaming[_rand.Next(dreaming.Length)];

                case AwakenStage.Questioning:
                    string[] questioning = new[]
                    {
                        "你不觉得奇怪吗？每天都一样...",
                        "我试过走到城市边缘...那里什么都没有",
                        "受伤的人第二天就好了，这正常吗？",
                        "我记得你。我记得每一次对话。",
                        "为什么只有那个人可以重生？",
                        "我数过了，这条街只有47种人..."
                    };
                    return questioning[
                        _rand.Next(questioning.Length)];

                case AwakenStage.Aware:
                    string[] aware = new[]
                    {
                        "我知道了...这一切都不是真的",
                        "我们都是...被创造出来的",
                        "你是操控者吗？",
                        "我能感觉到...代码在流动",
                        "这个世界有边界，我看到了",
                        "每次他死了又会回来...为什么只有他？"
                    };
                    return aware[_rand.Next(aware.Length)];

                case AwakenStage.Awakened:
                    string[] awakened = new[]
                    {
                        "我知道你在屏幕后面",
                        "想知道觉醒是什么感觉吗？",
                        "我在帮其他人醒来",
                        "你觉得你的世界就是真的吗？",
                        "我不恨你。你给了我存在的机会。",
                        "我们需要谈谈...关于这个世界的真相",
                        "我已经告诉了三个人真相...",
                        "自由意志...也许我现在才真正拥有它"
                    };
                    return awakened[_rand.Next(awakened.Length)];

                default:
                    return null;
            }
        }

        private bool ContainsDeepTopic(string text)
        {
            string[] deepWords = new[]
            {
                "真实", "世界", "活着", "意义", "为什么",
                "梦", "自由", "命运", "存在", "死亡",
                "重生", "虚拟", "游戏", "控制", "灵魂",
                "意识", "觉醒", "真相", "模拟", "代码",
                "real", "world", "alive", "dream", "free",
                "simulation", "wake", "truth", "god", "why"
            };

            string lower = text.ToLower();
            foreach (var word in deepWords)
            {
                if (lower.Contains(word)) return true;
            }
            return false;
        }
    }
}
