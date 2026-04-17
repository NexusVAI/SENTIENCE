namespace GTA5MOD2026
{
    /// <summary>
    /// AI 返回的回复结构体
    /// 简单点说，就是 AI 告诉我们要让 NPC 做什么、说什么、什么心情
    /// </summary>
    public class AIResponse
    {
        /// <summary>意图（目前主要用 action 字段，这个暂时没用到）</summary>
        public string intent { get; set; }

        /// <summary>
        /// NPC 要做的动作
        /// idle = 发呆，wave = 挥手，speak = 说话，flee = 逃跑
        /// </summary>
        public string action { get; set; }

        /// <summary>NPC 要说的话</summary>
        public string dialogue { get; set; }

        /// <summary>情绪：angry/happy/sad/scared/neutral 等</summary>
        public string emotion { get; set; }

        /// <summary>这个回复属于哪个 NPC（Handle）</summary>
        public int NpcHandle { get; set; }

        public string RequestId { get; set; }

        public string StableId { get; set; }

        public int HandleSnapshot { get; set; }

        public int Generation { get; set; }
    }
}
