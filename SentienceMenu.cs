// ─────────────────────────────────────────────────────────────────────────────
//  SentienceMenu.cs
//
//  F5 (hotkey configurable) LemonUI menu wiring up every knob in
//  ModConfig.UI / ModConfig.Behavior plus a few quick toggles
//  (voice / autonomous talk / actions enabled).
//
//  Lifecycle is driven by NPCManager:
//      _menu = new SentienceMenu(config, onChanged: () => config.Save());
//      // OnTick:    _menu.Process();
//      // KeyDown:   _menu.HandleKey(e.KeyCode);
// ─────────────────────────────────────────────────────────────────────────────

using LemonUI;
using LemonUI.Menus;
using System;
using System.Windows.Forms;

namespace GTA5MOD2026
{
    public class SentienceMenu
    {
        private readonly ObjectPool _pool = new ObjectPool();
        private readonly NativeMenu _root;
        private readonly ModConfig _config;
        private readonly Action _onChanged;

        // The hotkey that toggles the menu.  Defaults to F5; can be remapped
        // via the menu itself in the "热键" submenu.
        public Keys HotKey { get; set; } = Keys.F5;

        public bool IsVisible => _root != null && _root.Visible;

        public SentienceMenu(ModConfig config, Action onChanged)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _onChanged = onChanged ?? (() => { });

            _root = new NativeMenu(
                "Sentience",
                "V5.1 · Animus 控制台",
                "全部设置即时生效，关闭菜单后自动保存");
            _pool.Add(_root);

            BuildOverheadSection();
            BuildResponseSection();
            BuildHudSection();
            BuildBehaviorSection();
            BuildHotkeySection();
            BuildPluginsSection();    // V5.1 · Animus
            BuildQuickToggles();
        }

        // ─── Tick / input ────────────────────────────────────────────────────

        public void Process()
        {
            _pool.Process();
        }

        public void HandleKey(Keys key)
        {
            if (key == HotKey)
            {
                _root.Visible = !_root.Visible;
            }
        }

        // ─── Submenu builders ────────────────────────────────────────────────

        private NativeMenu AddSubmenu(NativeMenu parent,
            string title, string subtitle = "")
        {
            var sub = new NativeMenu("Sentience",
                title, subtitle);
            _pool.Add(sub);
            parent.AddSubMenu(sub);
            return sub;
        }

        private void BuildOverheadSection()
        {
            var menu = AddSubmenu(_root,
                "NPC 头顶绘制",
                "悬浮标签 / 觉醒图标 / 对话气泡");

            var ui = _config.UI;

            // Enable
            var cbOn = new NativeCheckboxItem(
                "启用悬浮标签", "关闭后 NPC 头顶完全不绘制",
                ui.OverheadEnabled);
            cbOn.CheckboxChanged += (s, e) =>
            {
                ui.OverheadEnabled = cbOn.Checked;
                _onChanged();
            };
            menu.Add(cbOn);

            // Scale slider (50 .. 200 → 0.5x .. 2.0x)
            var sScale = new NativeSliderItem(
                "缩放", "0.5x 至 2.0x",
                150, (int)Math.Round(ui.OverheadScale * 100f) - 50);
            sScale.ValueChanged += (s, e) =>
            {
                ui.OverheadScale = (sScale.Value + 50) / 100f;
                _onChanged();
            };
            menu.Add(sScale);

            // Style
            var listStyle = new NativeListItem<string>("风格",
                "default", "minimal", "bold", "cinematic");
            listStyle.SelectedIndex = Math.Max(0,
                Array.IndexOf(new[] { "default", "minimal", "bold", "cinematic" },
                    ui.OverheadStyle ?? "default"));
            listStyle.ItemChanged += (s, e) =>
            {
                ui.OverheadStyle = listStyle.SelectedItem;
                _onChanged();
            };
            menu.Add(listStyle);

            // Color preset (when not using personality color)
            var listColor = new NativeListItem<string>("颜色预设",
                "白", "金", "青", "粉", "红", "绿", "紫");
            listColor.SelectedIndex = ColorNameToIndex(ui.OverheadColor);
            listColor.ItemChanged += (s, e) =>
            {
                ui.OverheadColor = IndexToColorHex(listColor.SelectedIndex);
                _onChanged();
            };
            menu.Add(listColor);

            var cbPers = new NativeCheckboxItem(
                "按性格自动配色",
                "勾选时覆盖上面的颜色预设",
                ui.UsePersonalityColor);
            cbPers.CheckboxChanged += (s, e) =>
            {
                ui.UsePersonalityColor = cbPers.Checked;
                _onChanged();
            };
            menu.Add(cbPers);

            var cbBubble = new NativeCheckboxItem(
                "显示对话气泡", "在 NPC 头顶上方浮现刚说的话",
                ui.ShowFloatingDialogue);
            cbBubble.CheckboxChanged += (s, e) =>
            {
                ui.ShowFloatingDialogue = cbBubble.Checked;
                _onChanged();
            };
            menu.Add(cbBubble);
        }

        private void BuildResponseSection()
        {
            var menu = AddSubmenu(_root,
                "NPC 回答显示",
                "短信 / 字幕 / 双轨");

            var ui = _config.UI;

            var listMode = new NativeListItem<string>("显示方式",
                "notification", "subtitle", "both");
            listMode.SelectedIndex = Math.Max(0,
                Array.IndexOf(
                    new[] { "notification", "subtitle", "both" },
                    ui.ResponseDisplayMode ?? "notification"));
            listMode.ItemChanged += (s, e) =>
            {
                ui.ResponseDisplayMode = listMode.SelectedItem;
                _onChanged();
            };
            menu.Add(listMode);

            // Subtitle duration 2-15 sec
            var sDur = new NativeSliderItem(
                "字幕停留秒数", "适用于 subtitle / both",
                13, (int)Math.Round(ui.SubtitleDuration) - 2);
            sDur.ValueChanged += (s, e) =>
            {
                ui.SubtitleDuration = sDur.Value + 2f;
                _onChanged();
            };
            menu.Add(sDur);
        }

        private void BuildHudSection()
        {
            var menu = AddSubmenu(_root,
                "交互菜单 (HUD)",
                "左上角 G/H/T/J 提示");

            var ui = _config.UI;

            var cbOn = new NativeCheckboxItem(
                "启用 HUD", "关闭后不再绘制交互提示",
                ui.HudEnabled);
            cbOn.CheckboxChanged += (s, e) =>
            {
                ui.HudEnabled = cbOn.Checked;
                _onChanged();
            };
            menu.Add(cbOn);

            // HUD scale 50..200 → 0.5x..2.0x
            var sScale = new NativeSliderItem(
                "HUD 缩放", "0.5x 至 2.0x",
                150, (int)Math.Round(ui.HudScale * 100f) - 50);
            sScale.ValueChanged += (s, e) =>
            {
                ui.HudScale = (sScale.Value + 50) / 100f;
                _onChanged();
            };
            menu.Add(sScale);

            var listPos = new NativeListItem<string>("位置",
                "top_left", "top_right", "bottom_left", "bottom_right");
            listPos.SelectedIndex = Math.Max(0,
                Array.IndexOf(
                    new[] { "top_left", "top_right", "bottom_left", "bottom_right" },
                    ui.HudPosition ?? "top_left"));
            listPos.ItemChanged += (s, e) =>
            {
                ui.HudPosition = listPos.SelectedItem;
                _onChanged();
            };
            menu.Add(listPos);

            var listFg = new NativeListItem<string>("前景色",
                "白", "金", "青", "粉", "红", "绿", "紫");
            listFg.SelectedIndex = ColorNameToIndex(ui.HudColor);
            listFg.ItemChanged += (s, e) =>
            {
                ui.HudColor = IndexToColorHex(listFg.SelectedIndex);
                _onChanged();
            };
            menu.Add(listFg);

            var listBg = new NativeListItem<string>("背景色",
                "黑", "深灰", "深蓝", "深紫", "深红");
            listBg.SelectedIndex = BgNameToIndex(ui.HudBgColor);
            listBg.ItemChanged += (s, e) =>
            {
                ui.HudBgColor = IndexToBgHex(listBg.SelectedIndex);
                _onChanged();
            };
            menu.Add(listBg);

            var sAlpha = new NativeSliderItem(
                "背景透明度", "0=透明 255=不透明",
                255, ui.HudBgAlpha);
            sAlpha.ValueChanged += (s, e) =>
            {
                ui.HudBgAlpha = sAlpha.Value;
                _onChanged();
            };
            menu.Add(sAlpha);

            var cbBeep = new NativeCheckboxItem(
                "提示音", "HUD 弹出时是否 beep",
                ui.HudBeep);
            cbBeep.CheckboxChanged += (s, e) =>
            {
                ui.HudBeep = cbBeep.Checked;
                _onChanged();
            };
            menu.Add(cbBeep);
        }

        private void BuildBehaviorSection()
        {
            var menu = AddSubmenu(_root,
                "NPC 行为",
                "响应半径 / 活跃度 / 动作开关");

            var b = _config.Behavior;

            // ResponseRadius 5..50m
            var sRadius = new NativeSliderItem(
                "响应半径 (米)",
                "玩家走到多近时 NPC 会被纳入候选",
                45, (int)Math.Round(b.ResponseRadius) - 5);
            sRadius.ValueChanged += (s, e) =>
            {
                b.ResponseRadius = sRadius.Value + 5f;
                _onChanged();
            };
            menu.Add(sRadius);

            // Activity 0..100
            var sAct = new NativeSliderItem(
                "活跃度",
                "0=只有你主动找他才说话，100=非常主动",
                100, b.ActivityLevel);
            sAct.ValueChanged += (s, e) =>
            {
                b.ActivityLevel = sAct.Value;
                _onChanged();
            };
            menu.Add(sAct);

            var cbAutoTalk = new NativeCheckboxItem(
                "允许主动开口",
                "关闭后即便活跃度高也不会主动搭话",
                b.AutonomousTalk);
            cbAutoTalk.CheckboxChanged += (s, e) =>
            {
                b.AutonomousTalk = cbAutoTalk.Checked;
                _onChanged();
            };
            menu.Add(cbAutoTalk);

            menu.Add(new NativeSeparatorItem("动作开关"));

            var cbActions = new NativeCheckboxItem(
                "执行 LLM 动作",
                "关闭后所有动作降级为 speak (NPC 只说话不行动)",
                b.ActionsEnabled);
            cbActions.CheckboxChanged += (s, e) =>
            {
                b.ActionsEnabled = cbActions.Checked;
                _onChanged();
            };
            menu.Add(cbActions);

            var cbAttack = new NativeCheckboxItem(
                "允许 attack", "动手 / 打架 / 攻击玩家",
                b.AllowAttack);
            cbAttack.CheckboxChanged += (s, e) =>
            {
                b.AllowAttack = cbAttack.Checked;
                _onChanged();
            };
            menu.Add(cbAttack);

            var cbAim = new NativeCheckboxItem(
                "允许 aim", "举枪 / 瞄准玩家",
                b.AllowAim);
            cbAim.CheckboxChanged += (s, e) =>
            {
                b.AllowAim = cbAim.Checked;
                _onChanged();
            };
            menu.Add(cbAim);

            var cbCops = new NativeCheckboxItem(
                "允许 call_cops", "掏手机报警",
                b.AllowCallCops);
            cbCops.CheckboxChanged += (s, e) =>
            {
                b.AllowCallCops = cbCops.Checked;
                _onChanged();
            };
            menu.Add(cbCops);
        }

        private void BuildHotkeySection()
        {
            var menu = AddSubmenu(_root,
                "热键",
                "更改打开本菜单的按键");

            var keys = new[] {
                Keys.F2, Keys.F3, Keys.F4, Keys.F5,
                Keys.F7, Keys.F10, Keys.F11, Keys.F12,
            };
            var labels = new[] {
                "F2", "F3", "F4", "F5",
                "F7", "F10", "F11", "F12",
            };

            var listKey = new NativeListItem<string>("菜单热键", labels);
            int idx = Array.IndexOf(keys, HotKey);
            listKey.SelectedIndex = idx >= 0 ? idx : 3; // default F5
            listKey.ItemChanged += (s, e) =>
            {
                HotKey = keys[listKey.SelectedIndex];
                // We don't persist hotkey to ini in this build to keep
                // ModConfig clean; menu still remembers within the session.
            };
            menu.Add(listKey);
        }

        private void BuildQuickToggles()
        {
            _root.Add(new NativeSeparatorItem("快捷开关"));

            var cbVoice = new NativeCheckboxItem(
                "TTS 语音",
                "总开关：关闭后游戏内 NPC 不再朗读",
                _config.TTS.VoiceEnabled);
            cbVoice.CheckboxChanged += (s, e) =>
            {
                _config.TTS.VoiceEnabled = cbVoice.Checked;
                _onChanged();
            };
            _root.Add(cbVoice);

            var cbWake = new NativeCheckboxItem(
                "觉醒系统",
                "NPC 在长期交互后会觉醒并改变行为",
                _config.Awakening.Enabled);
            cbWake.CheckboxChanged += (s, e) =>
            {
                _config.Awakening.Enabled = cbWake.Checked;
                _onChanged();
            };
            _root.Add(cbWake);
        }

        // ─── Color presets ───────────────────────────────────────────────────

        private static readonly string[] FG_HEX = {
            "FFFFFF", "FFD700", "00FFFF", "FF80C0",
            "FF4040", "40FF40", "C040FF",
        };

        private static int ColorNameToIndex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return 0;
            string norm = hex.ToUpperInvariant().TrimStart('#');
            int i = Array.IndexOf(FG_HEX, norm);
            return i < 0 ? 0 : i;
        }

        private static string IndexToColorHex(int i)
        {
            if (i < 0 || i >= FG_HEX.Length) return FG_HEX[0];
            return FG_HEX[i];
        }

        private static readonly string[] BG_HEX = {
            "000000", "202020", "001830",
            "200030", "300010",
        };

        private static int BgNameToIndex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return 0;
            string norm = hex.ToUpperInvariant().TrimStart('#');
            int i = Array.IndexOf(BG_HEX, norm);
            return i < 0 ? 0 : i;
        }

        private static string IndexToBgHex(int i)
        {
            if (i < 0 || i >= BG_HEX.Length) return BG_HEX[0];
            return BG_HEX[i];
        }

        // ─── Static helpers used by NPCManager rendering ─────────────────────

        public static void ParseColor(string hex,
            out int r, out int g, out int b)
        {
            r = 255; g = 255; b = 255;
            if (string.IsNullOrWhiteSpace(hex)) return;
            string h = hex.TrimStart('#');
            if (h.Length < 6) return;
            try
            {
                r = Convert.ToInt32(h.Substring(0, 2), 16);
                g = Convert.ToInt32(h.Substring(2, 2), 16);
                b = Convert.ToInt32(h.Substring(4, 2), 16);
            }
            catch
            {
                r = 255; g = 255; b = 255;
            }
        }

        // ─── V5.1 · Animus — Plugins / Scenarios / Archetypes submenu ────────

        private void BuildPluginsSection()
        {
            var menu = AddSubmenu(_root,
                "插件 & 场景",
                "Sentience SDK 加载结果（只读概览）");

            var svc = GTA5MOD2026.Plugins.SentienceServices.Instance;

            // ── Plugin list ──────────────────────────────────────────────
            if (svc == null)
            {
                menu.Add(new NativeItem("[未初始化]",
                    "SentienceServices 尚未启动"));
            }
            else
            {
                int pluginCount = svc.Plugins?.Plugins?.Count ?? 0;
                menu.Add(new NativeItem(
                    $"已加载插件: {pluginCount}",
                    "扫描位置: " + (svc.PluginDirectoryPath ?? "")));

                if (svc.Plugins != null)
                {
                    foreach (var p in svc.Plugins.Plugins)
                    {
                        string label = $"[{p.Status}] {p.Name} v{p.Version}";
                        string desc = string.IsNullOrEmpty(p.StatusMessage)
                            ? $"作者: {p.Author}"
                            : $"作者: {p.Author} | {p.StatusMessage}";
                        menu.Add(new NativeItem(label, desc));
                    }
                }

                // ── Scenario list ────────────────────────────────────────
                int scenarioCount = svc.Scenarios?.Scenarios?.Count ?? 0;
                menu.Add(new NativeItem(
                    $"已加载场景: {scenarioCount}",
                    "扫描位置: " + (svc.ScenarioDirectoryPath ?? "")));

                if (svc.Scenarios != null)
                {
                    foreach (var s in svc.Scenarios.Scenarios)
                    {
                        string label = $"{(s.Enabled ? "[✓]" : "[ ]")} " +
                            $"{s.Name}";
                        var item = new NativeCheckboxItem(label,
                            $"id={s.Id} | 作者={s.Author}",
                            s.Enabled);
                        var captured = s;
                        item.CheckboxChanged += (sender, e) =>
                        {
                            captured.Enabled = item.Checked;
                        };
                        menu.Add(item);
                    }
                }

                // ── Archetype info ───────────────────────────────────────
                int archetypeCount = 0;
                if (svc.Archetypes != null)
                {
                    foreach (var _ in svc.Archetypes.All) archetypeCount++;
                }
                string iniDesc = string.IsNullOrEmpty(svc.LoadedArchetypeIniPath)
                    ? "未发现 archetype_voices.ini （使用内置默认）"
                    : "已加载: " + svc.LoadedArchetypeIniPath;
                menu.Add(new NativeItem(
                    $"声线档案数: {archetypeCount}", iniDesc));
            }
        }
    }
}
