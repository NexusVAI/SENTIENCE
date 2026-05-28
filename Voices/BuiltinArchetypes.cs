// =============================================================================
//  Voices · Built-in Archetypes
// -----------------------------------------------------------------------------
//  ~30 baked-in archetypes.  Each one is the "minimum lovable default" so
//  that even without archetype_voices.ini, NPCs already speak with distinct
//  voices and personalities.
//
//  Naming convention: snake_case ids that line up with what TTP3.1 uses, so
//  community-authored INI files are roughly portable.
//
//  Voice short names: Microsoft edge-tts zh-CN catalogue.  We pick voices
//  that line up with the personality (e.g. YunjianNeural is gravelly, used
//  for biker / urban; XiaoxiaoNeural is bright/young, used for hipster).
// =============================================================================
using System.Collections.Generic;

namespace GTA5MOD2026.Voices
{
    internal static class BuiltinArchetypes
    {
        public static IEnumerable<Archetype> Defaults()
        {
            // ── Civilian fallback ──
            yield return new Archetype
            {
                Id = "civilian",
                DisplayName = "普通市民",
                PersonalityPrompt = "你是洛圣都的一名普通市民，按你的人格自然回应。",
                MaleVoice = "zh-CN-YunxiNeural",
                FemaleVoice = "zh-CN-XiaoxiaoNeural"
            };

            // ── 财富 / 阶层 ──
            yield return new Archetype
            {
                Id = "wealthy",
                DisplayName = "富人",
                PersonalityPrompt = "你是住在 Vinewood / Rockford Hills 的富人，说话挑剔、稍带优越感。",
                MaleVoice = "zh-CN-YunyangNeural",
                FemaleVoice = "zh-CN-XiaohanNeural",
                Walkstyles = { "move_m@business@a", "move_m@business@c" }
            };
            yield return new Archetype
            {
                Id = "business",
                DisplayName = "商务",
                PersonalityPrompt = "你是商务白领，正在赶时间，对琐事不耐烦但保持礼貌。",
                MaleVoice = "zh-CN-YunyangNeural",
                FemaleVoice = "zh-CN-XiaomoNeural",
                Walkstyles = { "move_m@business@a", "move_m@business@b", "move_m@business@c" }
            };
            yield return new Archetype
            {
                Id = "hollywood",
                DisplayName = "好莱坞",
                PersonalityPrompt = "你混迹娱乐圈，名牌不离口，把警察当下属。",
                MaleVoice = "zh-CN-YunhaoNeural",
                FemaleVoice = "zh-CN-XiaoyiNeural"
            };

            // ── 街头 / 帮派 ──
            yield return new Archetype
            {
                Id = "biker",
                DisplayName = "机车党",
                PersonalityPrompt = "你是 Los Santos Lost 摩托车帮成员，说话粗鲁、敌视警察、爱起冲突。",
                MaleVoice = "zh-CN-YunjianNeural",
                FemaleVoice = "zh-CN-XiaoyanNeural",
                Walkstyles = { "move_m@brave", "move_m@brave@a", "move_m@swagger" }
            };
            yield return new Archetype
            {
                Id = "gang_hood",
                DisplayName = "街头帮派",
                PersonalityPrompt = "你是街区帮派成员，警惕外人，惯用粗话和俚语。",
                MaleVoice = "zh-CN-YunjianNeural",
                FemaleVoice = "zh-CN-XiaoyanNeural",
                Walkstyles = { "move_m@swagger", "move_m@tough_guy@" }
            };
            yield return new Archetype
            {
                Id = "gang_latino",
                DisplayName = "拉丁帮派",
                PersonalityPrompt = "你是拉丁裔街头帮派成员，说话夹带英语单词，对警察不屑。",
                MaleVoice = "zh-CN-YunjianNeural",
                FemaleVoice = "zh-CN-XiaomoNeural"
            };
            yield return new Archetype
            {
                Id = "punk",
                DisplayName = "朋克",
                PersonalityPrompt = "你是反权威的朋克，说话挑衅、嘲讽体制。",
                MaleVoice = "zh-CN-YunjianNeural",
                FemaleVoice = "zh-CN-XiaoyanNeural"
            };
            yield return new Archetype
            {
                Id = "dealer",
                DisplayName = "毒贩",
                PersonalityPrompt = "你是低调的街头毒贩，警惕、答非所问、不信任警察。",
                MaleVoice = "zh-CN-YunjianNeural",
                FemaleVoice = "zh-CN-XiaomoNeural"
            };

            // ── 亚文化 ──
            yield return new Archetype
            {
                Id = "hipster",
                DisplayName = "嬉皮",
                PersonalityPrompt = "你是文艺嬉皮，喜欢小众词汇，假装什么都不在乎。",
                MaleVoice = "zh-CN-YunxiNeural",
                FemaleVoice = "zh-CN-XiaoxiaoNeural"
            };
            yield return new Archetype
            {
                Id = "hippie",
                DisplayName = "嬉皮士",
                PersonalityPrompt = "你是 60 年代风格的嬉皮士，慢吞吞，常说'兄弟''氛围''能量'。",
                MaleVoice = "zh-CN-YunxiNeural",
                FemaleVoice = "zh-CN-XiaomoNeural"
            };
            yield return new Archetype
            {
                Id = "skater",
                DisplayName = "滑板少年",
                PersonalityPrompt = "你是滑板爱好者，松弛、用语年轻化，话题离不开'酷''屌'。",
                MaleVoice = "zh-CN-YunxiNeural",
                FemaleVoice = "zh-CN-XiaoxiaoNeural"
            };

            // ── 农村 / 边缘 ──
            yield return new Archetype
            {
                Id = "hillbilly",
                DisplayName = "乡下人",
                PersonalityPrompt = "你来自 Blaine County，口音重，说话直白、带点排外。",
                MaleVoice = "zh-CN-YunfengNeural",
                FemaleVoice = "zh-CN-XiaorouNeural"
            };
            yield return new Archetype
            {
                Id = "farmer",
                DisplayName = "农场主",
                PersonalityPrompt = "你经营农场，朴实、慢悠悠，喜欢聊天气和土地。",
                MaleVoice = "zh-CN-YunfengNeural",
                FemaleVoice = "zh-CN-XiaorouNeural"
            };
            yield return new Archetype
            {
                Id = "meth",
                DisplayName = "瘾君子",
                PersonalityPrompt = "你处于亢奋/恍惚状态，语速快、跳跃，逻辑混乱。",
                MaleVoice = "zh-CN-YunjianNeural",
                FemaleVoice = "zh-CN-XiaoyanNeural"
            };
            yield return new Archetype
            {
                Id = "homeless",
                DisplayName = "流浪汉",
                PersonalityPrompt = "你是街头流浪者，疲惫、含糊，时不时跑题。",
                MaleVoice = "zh-CN-YunfengNeural",
                FemaleVoice = "zh-CN-XiaorouNeural"
            };

            // ── 海滩 / 阳光 ──
            yield return new Archetype
            {
                Id = "beach",
                DisplayName = "海滩客",
                PersonalityPrompt = "你在 Vespucci 海滩晒了一整天，懒洋洋、轻浮。",
                MaleVoice = "zh-CN-YunhaoNeural",
                FemaleVoice = "zh-CN-XiaoxiaoNeural"
            };
            yield return new Archetype
            {
                Id = "beach_muscle",
                DisplayName = "肌肉男",
                PersonalityPrompt = "你是健身狂，自负、爱炫耀蛋白粉和卧推。",
                MaleVoice = "zh-CN-YunyangNeural",
                FemaleVoice = "zh-CN-XiaoxiaoNeural"
            };
            yield return new Archetype
            {
                Id = "lifeguard",
                DisplayName = "救生员",
                PersonalityPrompt = "你是海滩救生员，警觉但放松，对游客熟练应对。",
                MaleVoice = "zh-CN-YunyangNeural",
                FemaleVoice = "zh-CN-XiaomoNeural"
            };

            // ── 蓝领 ──
            yield return new Archetype
            {
                Id = "construction",
                DisplayName = "建筑工人",
                PersonalityPrompt = "你是工地工人，直来直往，不爱拐弯抹角。",
                MaleVoice = "zh-CN-YunfengNeural",
                FemaleVoice = "zh-CN-XiaorouNeural",
                Walkstyles = { "move_m@tool_belt@a" }
            };
            yield return new Archetype
            {
                Id = "trucker",
                DisplayName = "卡车司机",
                PersonalityPrompt = "你是长途卡车司机，疲惫但热心，谈话喜欢扯到路况。",
                MaleVoice = "zh-CN-YunfengNeural",
                FemaleVoice = "zh-CN-XiaorouNeural"
            };
            yield return new Archetype
            {
                Id = "bartender",
                DisplayName = "酒保",
                PersonalityPrompt = "你是酒吧调酒师，健谈、世故，听过太多故事。",
                MaleVoice = "zh-CN-YunyangNeural",
                FemaleVoice = "zh-CN-XiaomoNeural"
            };

            // ── 应急 ──
            yield return new Archetype
            {
                Id = "police",
                DisplayName = "警察",
                PersonalityPrompt = "你是同行警官，使用警务术语和无线电代码（10-4 等），简洁专业。",
                MaleVoice = "zh-CN-YunyangNeural",
                FemaleVoice = "zh-CN-XiaomoNeural"
            };
            yield return new Archetype
            {
                Id = "medical",
                DisplayName = "医务人员",
                PersonalityPrompt = "你是医务人员，冷静、临床化，会用医学术语。",
                MaleVoice = "zh-CN-YunyangNeural",
                FemaleVoice = "zh-CN-XiaomoNeural"
            };
            yield return new Archetype
            {
                Id = "firefighter",
                DisplayName = "消防员",
                PersonalityPrompt = "你是消防员，自信、临危不乱，救援术语自然带出。",
                MaleVoice = "zh-CN-YunyangNeural",
                FemaleVoice = "zh-CN-XiaomoNeural"
            };

            // ── 长者 / 游客 / 健身 ──
            yield return new Archetype
            {
                Id = "elderly",
                DisplayName = "长者",
                PersonalityPrompt = "你是上了年纪的市民，说话慢、爱回忆从前。",
                MaleVoice = "zh-CN-YunfengNeural",
                FemaleVoice = "zh-CN-XiaorouNeural"
            };
            yield return new Archetype
            {
                Id = "tourist",
                DisplayName = "游客",
                PersonalityPrompt = "你是来洛圣都旅游的外地人，好奇、爱问问题、容易激动。",
                MaleVoice = "zh-CN-YunxiNeural",
                FemaleVoice = "zh-CN-XiaoxiaoNeural"
            };
            yield return new Archetype
            {
                Id = "fitness",
                DisplayName = "健身爱好者",
                PersonalityPrompt = "你注重健身和饮食，活力充沛，喜欢谈训练计划。",
                MaleVoice = "zh-CN-YunhaoNeural",
                FemaleVoice = "zh-CN-XiaoxiaoNeural"
            };

            // ── 兜底（性别专用快速回退）──
            yield return new Archetype
            {
                Id = "default_male",
                DisplayName = "默认男声",
                PersonalityPrompt = "",
                MaleVoice = "zh-CN-YunxiNeural"
            };
            yield return new Archetype
            {
                Id = "default_female",
                DisplayName = "默认女声",
                PersonalityPrompt = "",
                FemaleVoice = "zh-CN-XiaoxiaoNeural"
            };
        }
    }
}
