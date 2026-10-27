using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Studio;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KK_SceneQuick
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("CharaStudio.exe")]
    [DefaultExecutionOrder(10500)]
    public class SceneQuickPlugin : BaseUnityPlugin
    {
        public const string GUID = "kk.studio.scenequick";
        public const string PluginName = "KK Scene Quick";
        public const string Version = "0.1.0";

        internal static SceneQuickPlugin Instance;
        internal static ManualLogSource Log;

        // ── 配置 ──────────────────────────────────────────────
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<KeyCode> _hotkey;
        internal ConfigEntry<float> HeightRatio;
        internal ConfigEntry<string> CardSortCfg;
        internal ConfigEntry<bool> FoldCharsCfg;
        internal ConfigEntry<bool> FoldEffectsCfg;
        internal ConfigEntry<bool> FoldItemsCfg;

        // ── 行数据 ────────────────────────────────────────────
        internal readonly List<CharRow> Chars = new List<CharRow>();
        internal readonly List<ItemRow> Items = new List<ItemRow>();
        internal readonly List<ItemRow> AllItems = new List<ItemRow>();
        internal readonly List<CardRow> Cards = new List<CardRow>();

        internal readonly HashSet<int> MultiSel = new HashSet<int>();
        internal readonly HashSet<int> PresetSel = new HashSet<int>();
        internal readonly HashSet<string> Expanded = new HashSet<string>();

        internal int SelCharKey = -1;
        internal int SelItemKey = -1;
        internal bool ShowCards;
        internal bool FoldChars;
        internal bool FoldEffects;
        internal bool FoldItems;
        internal int CardSort;
        internal int CardPage;
        internal int CardPageSize = 6;

        internal bool Visible;
        internal bool NeedsRebuild;

        internal string PresetHint = "";
        internal readonly List<EffPreset> Presets = new List<EffPreset>();
        internal bool PresetsLoaded;

        internal string CtxKey;
        internal string CtxName;

        private readonly Dictionary<int, bool> _baseVis = new Dictionary<int, bool>();
        private readonly Dictionary<string, bool> _overrides = new Dictionary<string, bool>();
        private bool _overridesLoaded;

        private readonly Dictionary<string, long> _useHistory =
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private string _useHistoryPath;

        private int _lastSig;
        private bool _sigInit;
        private float _sceneDirtyAt;

        private bool _xuaPatched;
        private bool _xuaGaveUp;
        private int _xuaTries;
        private float _xuaNextTry;
        private bool _uiReady;

        internal QuickPanel Panel;
        internal PresetWindow PresetWin;

        internal static string PresetPath
        {
            get { return Path.Combine(Paths.ConfigPath, "kk.studio.scenequick.presets.txt"); }
        }

        internal static string OverridePath
        {
            get { return Path.Combine(Paths.ConfigPath, "kk.studio.scenequick.effectoverride.txt"); }
        }

        internal static string ItemListPath
        {
            get { return Path.Combine(Paths.ConfigPath, "kk.studio.scenequick.items.txt"); }
        }

        internal static string RecentPath
        {
            get { return Path.Combine(Paths.ConfigPath, "kk.studio.scenequick.recent.txt"); }
        }

        internal string HotkeyLabel
        {
            get
            {
                try
                {
                    return _hotkey.Value.ToString();
                }
                catch (Exception)
                {
                    return "F5";
                }
            }
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            _enabled = Config.Bind("1-General", "Enabled", true, "总开关：关掉后不画任何界面");
            _hotkey = Config.Bind("1-General", "Panel hotkey", KeyCode.F5,
                "开关快捷面板的快捷键（F6~F11 常被别的插件占用，默认 F5）");
            CardSortCfg = Config.Bind("2-Cards", "Sort order", "name",
                "角色卡排序：name 名字 / recent 最近使用 / modified 修改日期 / created 创建日期");
            HeightRatio = Config.Bind("3-Sections", "Panel height ratio", 0.72f,
                new ConfigDescription("面板高度占屏幕高度的比例（0.4~1.0）",
                    new AcceptableValueRange<float>(0.4f, 1f)));
            FoldCharsCfg = Config.Bind("3-Sections", "Fold characters", false, "「人物替换」栏默认收起");
            FoldEffectsCfg = Config.Bind("3-Sections", "Fold effects", false, "「效果」栏默认收起");
            FoldItemsCfg = Config.Bind("3-Sections", "Fold items", false, "「场景物品」栏默认收起");

            CardSort = ParseSort(CardSortCfg.Value);
            FoldChars = FoldCharsCfg.Value;
            FoldEffects = FoldEffectsCfg.Value;
            FoldItems = FoldItemsCfg.Value;
            LoadUseHistory();

            try
            {
                // 界面延迟到第一帧之后再建：此时 XUnity.AutoTranslator 已加载，
                // 可以先挂好「忽略本插件文字」的规则，避免界面文本被自动翻译。
                Panel = new QuickPanel(this);
            }
            catch (Exception ex)
            {
                Log.LogWarning("创建面板失败: " + ex.Message);
            }
            try
            {
                PresetWin = new PresetWindow(this);
            }
            catch (Exception ex)
            {
                Log.LogWarning("预建预设窗口失败: " + ex.Message);
            }

            Log.LogInfo(PluginName + " " + Version + " 已加载（Koikatu / CharaStudio）");
            Log.LogInfo("界面字体: " + (UiTheme.Font != null ? UiTheme.Font.name : "无（文字可能不可见）"));
        }

        /// <summary>让 XUnity.AutoTranslator 跳过本插件界面里的文字。</summary>
        private void TryPatchXUnity()
        {
            if (_xuaPatched || _xuaGaveUp)
            {
                return;
            }
            if (Time.realtimeSinceStartup < _xuaNextTry)
            {
                return;
            }
            _xuaNextTry = Time.realtimeSinceStartup + 2f;
            if (++_xuaTries > 20)
            {
                _xuaGaveUp = true;
                return;
            }
            try
            {
                Type type = null;
                Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < asms.Length && type == null; i++)
                {
                    string name;
                    try
                    {
                        name = asms[i].GetName().Name;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (name == "XUnity.AutoTranslator.Plugin.Core")
                    {
                        type = asms[i].GetType("XUnity.AutoTranslator.Plugin.Core.Extensions.ComponentExtensions");
                    }
                }
                if (type == null)
                {
                    return;
                }
                MethodInfo target = type.GetMethod("ShouldIgnoreTextComponent",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (target == null)
                {
                    _xuaGaveUp = true;
                    return;
                }
                MethodInfo prefix = typeof(SceneQuickPlugin).GetMethod("XuaIgnore",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (prefix == null)
                {
                    _xuaGaveUp = true;
                    return;
                }
                new Harmony(GUID + ".xua").Patch(target, new HarmonyMethod(prefix));
                _xuaPatched = true;
                Log.LogInfo("已挂接 XUnity 忽略规则（界面文字不再被翻译）");
            }
            catch (Exception ex)
            {
                _xuaGaveUp = true;
                Log.LogWarning("XUnity 忽略规则挂接失败: " + ex.Message);
            }
        }

        private static bool XuaIgnore(object ui, ref bool __result)
        {
            try
            {
                Component c = ui as Component;
                if (c != null && IsOurUi(c.transform))
                {
                    __result = true;
                    return false;
                }
            }
            catch (Exception)
            {
            }
            return true;
        }

        internal static bool IsOurUi(Transform t)
        {
            if (t == null || Instance == null)
            {
                return false;
            }
            Transform panel = Instance.Panel != null ? Instance.Panel.CanvasTransform : null;
            Transform preset = Instance.PresetWin != null ? Instance.PresetWin.CanvasTransform : null;
            int guard = 0;
            while (t != null && guard++ < 96)
            {
                if (t == panel || t == preset)
                {
                    return true;
                }
                t = t.parent;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            if (_enabled == null || !_enabled.Value)
            {
                return;
            }
            if (Input.GetKeyDown(_hotkey.Value))
            {
                Toggle();
            }
            TryPatchXUnity();
            if (!_uiReady && Time.realtimeSinceStartup > 1f)
            {
                _uiReady = true;
                try
                {
                    if (Panel == null)
                    {
                        Panel = new QuickPanel(this);
                    }
                    Panel.EnsureBuilt();
                    Panel.SetVisible(false);
                }
                catch (Exception ex)
                {
                    Log.LogWarning("预建界面失败: " + ex.Message);
                }
            }
            if (!Visible)
            {
                return;
            }
            try
            {
                DetectSceneChange();
            }
            catch (Exception)
            {
            }
            if (NeedsRebuild)
            {
                NeedsRebuild = false;
                Collect();
                if (Panel != null)
                {
                    Panel.Rebuild();
                }
            }
        }

        // ── 显示 / 隐藏 ───────────────────────────────────────
        internal void Toggle()
        {
            if (Visible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        internal void Show()
        {
            try
            {
                if (Panel == null)
                {
                    Panel = new QuickPanel(this);
                }
                Panel.EnsureBuilt();
                Collect();
                Panel.SetVisible(true);
                Panel.Rebuild();
                Visible = true;
            }
            catch (Exception ex)
            {
                Log.LogError("打开面板失败: " + ex);
            }
        }

        internal void Hide()
        {
            Visible = false;
            if (Panel != null)
            {
                Panel.SetVisible(false);
            }
        }

        private void DetectSceneChange()
        {
            Studio.Studio studio = Singleton<Studio.Studio>.Instance;
            if (studio == null)
            {
                return;
            }
            int sig = 17;
            try
            {
                foreach (KeyValuePair<int, ObjectCtrlInfo> kv in studio.dicObjectCtrl)
                {
                    int h = 0;
                    try
                    {
                        if (kv.Value != null && kv.Value.objectInfo != null)
                        {
                            h = kv.Value.objectInfo.GetHashCode();
                        }
                    }
                    catch (Exception)
                    {
                    }
                    sig = sig * 31 + kv.Key * 7 + h;
                }
            }
            catch (Exception)
            {
                return;
            }
            if (!_sigInit)
            {
                _sigInit = true;
                _lastSig = sig;
            }
            else if (sig != _lastSig)
            {
                _lastSig = sig;
                _sceneDirtyAt = Time.realtimeSinceStartup + 0.7f;
            }
            else if (_sceneDirtyAt > 0f && Time.realtimeSinceStartup >= _sceneDirtyAt)
            {
                _sceneDirtyAt = 0f;
                NeedsRebuild = true;
            }
        }

        // ── 收集场景对象 ──────────────────────────────────────
        internal void Collect()
        {
            Chars.Clear();
            Items.Clear();
            Cards.Clear();
            Studio.Studio studio = Singleton<Studio.Studio>.Instance;
            if (studio == null)
            {
                return;
            }
            List<CharRow> chars = new List<CharRow>();
            List<ItemRow> items = new List<ItemRow>();
            foreach (KeyValuePair<int, ObjectCtrlInfo> kv in studio.dicObjectCtrl)
            {
                ObjectCtrlInfo ctrl = kv.Value;
                if (ctrl == null)
                {
                    continue;
                }
                OCIChar oc = ctrl as OCIChar;
                if (oc != null)
                {
                    CharRow row = new CharRow();
                    row.Ctrl = oc;
                    row.Key = kv.Key;
                    row.Sex = SafeCharSex(oc);
                    row.Name = SafeCharName(oc);
                    chars.Add(row);
                    continue;
                }
                OCIItem oi = ctrl as OCIItem;
                if (oi == null)
                {
                    continue;
                }
                ItemRow ir = new ItemRow();
                ir.Ctrl = oi;
                ir.Key = kv.Key;
                ir.Name = SafeItemName(oi);
                ir.Path = SafeItemPath(oi);
                ir.Mod = SafeItemMod(oi);
                ir.Zh = NameZh.Translate(ir.Name);
                string why;
                ir.Effect = IsEffectItem(oi, ir, out why);
                ir.Mark = why;
                ir.Slot = -1;
                OCIChar owner = FindOwnerChar(ctrl);
                ir.Attached = owner != null;
                if (owner != null)
                {
                    ir.Slot = FindSlot(owner, oi.objectInfo);
                    ir.SlotName = SafePointName(ir.Slot);
                }
                items.Add(ir);
            }
            chars.Sort(CompareChar);
            items.Sort(CompareItem);
            Chars.AddRange(chars);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Slot < 0 && !items[i].Attached)
                {
                    Items.Add(items[i]);
                }
            }
            AllItems.Clear();
            AllItems.AddRange(items);
            CaptureBaseline(items);
            if (SelCharKey >= 0 && FindChar(SelCharKey) == null)
            {
                SelCharKey = -1;
            }
            if (SelCharKey < 0 && Chars.Count > 0)
            {
                SelCharKey = Chars[0].Key;
            }
            if (ShowCards)
            {
                LoadCards(CurrentSex());
            }
            PruneDeadSelection();
        }

        private void PruneDeadSelection()
        {
            MultiSel.RemoveWhere(k => FindChar(k) == null);
            PresetSel.RemoveWhere(k => FindItem(k) == null);
            if (SelItemKey >= 0 && FindItem(SelItemKey) == null)
            {
                SelItemKey = -1;
            }
        }

        internal CharRow FindChar(int key)
        {
            for (int i = 0; i < Chars.Count; i++)
            {
                if (Chars[i].Key == key)
                {
                    return Chars[i];
                }
            }
            return null;
        }

        internal ItemRow FindItem(int key)
        {
            for (int i = 0; i < AllItems.Count; i++)
            {
                if (AllItems[i].Key == key)
                {
                    return AllItems[i];
                }
            }
            return null;
        }

        internal int CurrentSex()
        {
            CharRow row = FindChar(SelCharKey);
            return row != null ? row.Sex : 0;
        }

        // ── 基线显隐（重置用） ────────────────────────────────
        private void CaptureBaseline(List<ItemRow> items)
        {
            int fresh = 0;
            for (int i = 0; i < items.Count; i++)
            {
                int id = ItemId(items[i]);
                if (id != 0 && !_baseVis.ContainsKey(id))
                {
                    fresh++;
                }
            }
            if (items.Count > 0 && fresh * 2 >= items.Count)
            {
                _baseVis.Clear();
            }
            for (int i = 0; i < items.Count; i++)
            {
                int id = ItemId(items[i]);
                if (id != 0 && !_baseVis.ContainsKey(id))
                {
                    _baseVis[id] = CurrentVisible(items[i]);
                }
            }
        }

        internal void ResetItems(bool effects)
        {
            int n = 0;
            for (int i = 0; i < AllItems.Count; i++)
            {
                ItemRow r = AllItems[i];
                if (r.Effect == effects && r.Slot < 0 && !r.Attached)
                {
                    int id = ItemId(r);
                    bool v;
                    if (id != 0 && _baseVis.TryGetValue(id, out v))
                    {
                        SetItemVisible(r, v);
                        n++;
                    }
                }
            }
            Log.LogInfo("重置" + (effects ? "效果" : "物品") + "显隐: " + n + " 个");
            NeedsRebuild = true;
        }

        internal bool BaseVisible(ItemRow r)
        {
            int id = ItemId(r);
            bool v;
            if (id != 0 && _baseVis.TryGetValue(id, out v))
            {
                return v;
            }
            return CurrentVisible(r);
        }

        internal static int ItemId(ItemRow r)
        {
            try
            {
                if (r == null || r.Ctrl == null || r.Ctrl.objectItem == null)
                {
                    return 0;
                }
                return r.Ctrl.objectItem.GetInstanceID();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // ── 显隐 / 删除 / 选中 ────────────────────────────────
        internal static bool CurrentVisible(ItemRow r)
        {
            try
            {
                return r.Ctrl == null || r.Ctrl.visible;
            }
            catch (Exception)
            {
                return true;
            }
        }

        internal void SetItemVisible(ItemRow r, bool v)
        {
            try
            {
                if (r == null || r.Ctrl == null)
                {
                    return;
                }
                r.Ctrl.visible = v;
                if (r.Ctrl.objectInfo != null)
                {
                    r.Ctrl.objectInfo.visible = v;
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning("设置物件显隐失败 (#" + (r != null ? r.Key : -1) + "): " + ex.Message);
            }
        }

        internal static bool SafeCharVisible(OCIChar oc)
        {
            try
            {
                if (oc != null && oc.objectInfo != null)
                {
                    return oc.objectInfo.visible;
                }
            }
            catch (Exception)
            {
            }
            return true;
        }

        internal void SetCharVisible(CharRow c, bool v)
        {
            try
            {
                if (c == null || c.Ctrl == null)
                {
                    return;
                }
                c.Ctrl.OnVisible(v);
                if (c.Ctrl.objectInfo != null)
                {
                    c.Ctrl.objectInfo.visible = v;
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning("设置角色显隐失败: " + ex.Message);
            }
        }

        internal void DeleteObject(ObjectCtrlInfo ctrl, int key)
        {
            try
            {
                if (ctrl == null || ctrl.objectInfo == null)
                {
                    return;
                }
                Studio.Studio.DeleteInfo(ctrl.objectInfo, true);
                if (SelCharKey == key)
                {
                    SelCharKey = -1;
                }
                MultiSel.Remove(key);
                PresetSel.Remove(key);
                Log.LogInfo("已删除对象 #" + key);
            }
            catch (Exception ex)
            {
                Log.LogWarning("删除失败: " + ex.Message);
            }
        }

        internal void SelectChar(int key)
        {
            try
            {
                CharRow row = FindChar(key);
                if (row == null)
                {
                    return;
                }
                SelCharKey = key;
                Studio.Studio studio = Singleton<Studio.Studio>.Instance;
                if (studio != null && studio.treeNodeCtrl != null && row.Ctrl != null &&
                    row.Ctrl.treeNodeObject != null)
                {
                    studio.treeNodeCtrl.SelectSingle(row.Ctrl.treeNodeObject, true);
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning("选中角色失败: " + ex.Message);
            }
        }

        internal void SelectItem(ItemRow r)
        {
            try
            {
                Studio.Studio studio = Singleton<Studio.Studio>.Instance;
                if (studio == null || studio.treeNodeCtrl == null || r == null || r.Ctrl == null)
                {
                    return;
                }
                if (r.Ctrl.treeNodeObject != null)
                {
                    studio.treeNodeCtrl.SelectSingle(r.Ctrl.treeNodeObject, true);
                }
                SelItemKey = r.Key;
                NeedsRebuild = true;
            }
            catch (Exception ex)
            {
                Log.LogWarning("选中物品失败: " + ex.Message);
            }
        }

        // ── 角色卡 ────────────────────────────────────────────
        internal void LoadCards(int sex)
        {
            Cards.Clear();
            string rel = sex == 0 ? "chara/male" : "chara/female";
            string dir = null;
            try
            {
                dir = UserData.Create(rel);
            }
            catch (Exception)
            {
            }
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return;
            }
            string[] files;
            try
            {
                files = Directory.GetFiles(dir, "*.png");
            }
            catch (Exception)
            {
                return;
            }
            for (int i = 0; i < files.Length; i++)
            {
                CardRow card = new CardRow();
                card.Path = files[i];
                card.Name = Path.GetFileNameWithoutExtension(files[i]);
                try
                {
                    FileInfo fi = new FileInfo(files[i]);
                    if (fi.Exists)
                    {
                        card.Modified = fi.LastWriteTimeUtc;
                        card.Created = fi.CreationTimeUtc;
                    }
                }
                catch (Exception)
                {
                }
                long used;
                if (_useHistory.TryGetValue(files[i], out used))
                {
                    card.LastUsed = used;
                }
                Cards.Add(card);
            }
            Cards.Sort(CompareCard);
            int maxPage = Math.Max(0, (Cards.Count - 1) / CardPageSize);
            if (CardPage > maxPage)
            {
                CardPage = maxPage;
            }
            if (CardPage < 0)
            {
                CardPage = 0;
            }
        }

        private int CompareCard(CardRow a, CardRow b)
        {
            switch (CardSort)
            {
                case 1:
                    if (a.LastUsed != b.LastUsed)
                    {
                        return b.LastUsed.CompareTo(a.LastUsed);
                    }
                    break;
                case 2:
                    if (a.Modified != b.Modified)
                    {
                        return b.Modified.CompareTo(a.Modified);
                    }
                    break;
                case 3:
                    if (a.Created != b.Created)
                    {
                        return b.Created.CompareTo(a.Created);
                    }
                    break;
            }
            return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        }

        internal void SetCardSort(int v)
        {
            CardSort = v;
            CardPage = 0;
            if (CardSortCfg != null)
            {
                string[] names = new string[4] { "name", "recent", "modified", "created" };
                if (v >= 0 && v < names.Length)
                {
                    CardSortCfg.Value = names[v];
                }
            }
            NeedsRebuild = true;
        }

        private static int ParseSort(string s)
        {
            switch ((s ?? "name").Trim().ToLowerInvariant())
            {
                case "recent":
                    return 1;
                case "modified":
                case "mtime":
                    return 2;
                case "created":
                case "ctime":
                    return 3;
                default:
                    return 0;
            }
        }

        internal void ChangeChara(string cardPath)
        {
            try
            {
                if (string.IsNullOrEmpty(cardPath) || !File.Exists(cardPath))
                {
                    Log.LogWarning("换角色：卡不存在 " + cardPath);
                    return;
                }
                List<CharRow> targets = new List<CharRow>();
                if (MultiSel.Count > 0)
                {
                    foreach (int key in MultiSel)
                    {
                        CharRow row = FindChar(key);
                        if (row != null && row.Ctrl != null)
                        {
                            targets.Add(row);
                        }
                    }
                }
                if (targets.Count == 0)
                {
                    CharRow sel = FindChar(SelCharKey);
                    if (sel != null && sel.Ctrl != null)
                    {
                        targets.Add(sel);
                    }
                }
                if (targets.Count == 0)
                {
                    Log.LogWarning("换角色：没有目标角色");
                    return;
                }
                int ok = 0;
                for (int i = 0; i < targets.Count; i++)
                {
                    CharRow row = targets[i];
                    try
                    {
                        string oldName = row.Name;
                        row.Ctrl.ChangeChara(cardPath);
                        ok++;
                        Log.LogInfo("换角色 #" + row.Key + "  " + oldName + " -> " +
                                    Path.GetFileNameWithoutExtension(cardPath));
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning("换角色 #" + row.Key + " 失败: " + ex.Message);
                    }
                }
                RecordUse(cardPath);
                NeedsRebuild = true;
                Log.LogInfo("换角色完成: " + ok + "/" + targets.Count);
            }
            catch (Exception ex)
            {
                Log.LogError("换角色失败: " + ex);
            }
        }

        internal void LoadUseHistory()
        {
            try
            {
                _useHistoryPath = RecentPath;
                if (!File.Exists(_useHistoryPath))
                {
                    return;
                }
                string[] lines = File.ReadAllLines(_useHistoryPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }
                    int idx = line.IndexOf('|');
                    long ts;
                    if (idx > 0 && long.TryParse(line.Substring(0, idx), out ts))
                    {
                        _useHistory[line.Substring(idx + 1)] = ts;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning("读取最近使用记录失败: " + ex.Message);
            }
        }

        internal void RecordUse(string cardPath)
        {
            try
            {
                if (string.IsNullOrEmpty(cardPath))
                {
                    return;
                }
                _useHistory[cardPath] = DateTime.UtcNow.Ticks;
                if (string.IsNullOrEmpty(_useHistoryPath))
                {
                    _useHistoryPath = RecentPath;
                }
                List<KeyValuePair<string, long>> list = new List<KeyValuePair<string, long>>(_useHistory);
                list.Sort((x, y) => y.Value.CompareTo(x.Value));
                int n = Math.Min(list.Count, 300);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < n; i++)
                {
                    sb.Append(list[i].Value).Append('|').Append(list[i].Key).Append('\n');
                }
                File.WriteAllText(_useHistoryPath, sb.ToString());
            }
            catch (Exception ex)
            {
                Log.LogWarning("写入最近使用记录失败: " + ex.Message);
            }
        }

        // ── 效果识别 / 改判 ───────────────────────────────────
        private static readonly string[] EffectKeys = new string[38]
        {
            "post process", "postprocess", "post-processing", "lut", "color correction", "colorgrad", "color grad",
            "grading", "bloom", "depth of field", "dof", "vignette", "globalfog", "sun shaft", "sunshaft",
            "amplifycolor", "amplify occlusion", "alphablend", "alpha blend", "diffusion", "exposure",
            "saturation", "sharpen", "丸影", "色影", "光晕", "泛光", "滤色", "调色", "校正", "柔化",
            "soundmod", "sound mod", "音效", "声音", "语音", "bgm", "audio"
        };

        private bool IsEffectItem(OCIItem oi, ItemRow r, out string why)
        {
            why = "";
            try
            {
                bool forced;
                if (HasOverride(oi, out forced))
                {
                    why = forced ? "手动:效果" : "手动:普通";
                    return forced;
                }
                string text = (r.Name + " " + r.Path + " " + r.Mod).ToLowerInvariant();
                for (int i = 0; i < EffectKeys.Length; i++)
                {
                    if (text.IndexOf(EffectKeys[i], StringComparison.Ordinal) >= 0)
                    {
                        why = "名字:" + EffectKeys[i];
                        return true;
                    }
                }
                GameObject go = oi != null ? oi.objectItem : null;
                if (go != null)
                {
                    Component[] comps = go.GetComponentsInChildren<Component>(true);
                    for (int i = 0; i < comps.Length; i++)
                    {
                        Component c = comps[i];
                        if (c == null)
                        {
                            continue;
                        }
                        string name = c.GetType().Name;
                        if (name == "Sound_Component" || name.StartsWith("Audio", StringComparison.Ordinal))
                        {
                            why = "音频:" + name;
                            return true;
                        }
                        switch (name)
                        {
                            case "PostProcessLayer":
                            case "PostProcessVolume":
                            case "PostProcessingBehaviour":
                            case "AmplifyColorEffect":
                            case "AmplifyOcclusionEffect":
                            case "BloomAndFlares":
                            case "DepthOfField":
                            case "VignetteAndChromaticAberration":
                            case "GlobalFog":
                            case "SunShafts":
                                why = "组件:" + name;
                                return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        internal static string ItemKey(OCIItem oi)
        {
            try
            {
                OIItemInfo info = oi != null ? oi.itemInfo : null;
                if (info == null)
                {
                    return "";
                }
                return info.group + "|" + info.category + "|" + info.no;
            }
            catch (Exception)
            {
                return "";
            }
        }

        private void EnsureOverrides()
        {
            if (_overridesLoaded)
            {
                return;
            }
            _overridesLoaded = true;
            try
            {
                if (!File.Exists(OverridePath))
                {
                    return;
                }
                string[] lines = File.ReadAllLines(OverridePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = (lines[i] ?? "").Trim();
                    if (line.Length == 0)
                    {
                        continue;
                    }
                    string[] p = line.Split('|');
                    if (p.Length >= 4)
                    {
                        _overrides[p[0] + "|" + p[1] + "|" + p[2]] = p[3].Trim() == "1";
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning("读取效果改判失败: " + ex.Message);
            }
        }

        private void SaveOverrides()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, bool> kv in _overrides)
                {
                    lines.Add(kv.Key + "|" + (kv.Value ? "1" : "0"));
                }
                File.WriteAllLines(OverridePath, lines.ToArray());
            }
            catch (Exception ex)
            {
                Log.LogWarning("保存效果改判失败: " + ex.Message);
            }
        }

        private bool HasOverride(OCIItem oi, out bool asEffect)
        {
            asEffect = false;
            EnsureOverrides();
            string key = ItemKey(oi);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            return _overrides.TryGetValue(key, out asEffect);
        }

        internal bool HasOverride(OCIItem oi)
        {
            bool v;
            return HasOverride(oi, out v);
        }

        internal bool OverrideOf(OCIItem oi)
        {
            bool v = false;
            HasOverride(oi, out v);
            return v;
        }

        internal void SetOverride(ItemRow row, int mode)
        {
            // mode: 0 自动 / 1 效果 / 2 普通
            try
            {
                string key = ItemKey(row != null ? row.Ctrl : null);
                if (string.IsNullOrEmpty(key))
                {
                    return;
                }
                EnsureOverrides();
                if (mode == 0)
                {
                    _overrides.Remove(key);
                }
                else
                {
                    _overrides[key] = mode == 1;
                }
                SaveOverrides();
                NeedsRebuild = true;
            }
            catch (Exception ex)
            {
                Log.LogWarning("改判失败: " + ex.Message);
            }
        }

        internal void OpenCtx(ItemRow row)
        {
            CtxKey = ItemKey(row != null ? row.Ctrl : null);
            CtxName = row != null ? row.Name : "";
            NeedsRebuild = true;
        }

        internal void DumpItemList()
        {
            try
            {
                List<string> lines = new List<string>();
                lines.Add("# 效果/普通 分类清单（自动生成，仅供参考；可用右键改判覆盖）");
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int i = 0; i < AllItems.Count; i++)
                    {
                        ItemRow r = AllItems[i];
                        if ((pass == 0) == r.Effect)
                        {
                            lines.Add((r.Effect ? "效果" : "普通") + "\t" + (r.Mark ?? "") + "\t" + r.Name + "\t" + r.Path);
                        }
                    }
                }
                File.WriteAllLines(ItemListPath, lines.ToArray());
            }
            catch (Exception ex)
            {
                Log.LogWarning("导出物品清单失败: " + ex.Message);
            }
        }

        // ── 反射读取：名称 / 性别 / 挂载 ──────────────────────
        internal static string SafeCharName(OCIChar oc)
        {
            try
            {
                ChaControl cha = oc.charInfo;
                if (cha != null && cha.chaFile != null && cha.chaFile.parameter != null)
                {
                    string name = cha.chaFile.parameter.fullname;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return name;
                    }
                }
            }
            catch (Exception)
            {
            }
            try
            {
                if (oc.treeNodeObject != null && !string.IsNullOrEmpty(oc.treeNodeObject.textName))
                {
                    return oc.treeNodeObject.textName;
                }
            }
            catch (Exception)
            {
            }
            return "角色 " + (oc.objectInfo != null ? oc.objectInfo.dicKey : -1);
        }

        internal static int SafeCharSex(OCIChar oc)
        {
            try
            {
                if (oc != null && oc.charInfo != null && oc.charInfo.chaFile != null &&
                    oc.charInfo.chaFile.parameter != null)
                {
                    return oc.charInfo.chaFile.parameter.sex == 1 ? 1 : 0;
                }
            }
            catch (Exception)
            {
            }
            try
            {
                return oc != null && oc.sex == 1 ? 1 : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        internal static string SafeItemName(OCIItem oi)
        {
            try
            {
                OIItemInfo info = oi.itemInfo;
                Info mgr = Singleton<Info>.Instance;
                Dictionary<int, Dictionary<int, Info.ItemLoadInfo>> byGroup;
                if (info != null && mgr != null && mgr.dicItemLoadInfo != null &&
                    mgr.dicItemLoadInfo.TryGetValue(info.group, out byGroup) && byGroup != null)
                {
                    Dictionary<int, Info.ItemLoadInfo> byCat;
                    if (byGroup.TryGetValue(info.category, out byCat) && byCat != null)
                    {
                        Info.ItemLoadInfo load;
                        if (byCat.TryGetValue(info.no, out load) && load != null &&
                            !string.IsNullOrEmpty(load.name))
                        {
                            return load.name;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            try
            {
                if (oi.treeNodeObject != null && !string.IsNullOrEmpty(oi.treeNodeObject.textName))
                {
                    return oi.treeNodeObject.textName;
                }
            }
            catch (Exception)
            {
            }
            return "物品 " + (oi.objectInfo != null ? oi.objectInfo.dicKey : -1);
        }

        internal static string SafeItemPath(OCIItem oi)
        {
            try
            {
                OIItemInfo info = oi.itemInfo;
                Info mgr = Singleton<Info>.Instance;
                if (info == null || mgr == null || mgr.dicItemGroupCategory == null)
                {
                    return string.Empty;
                }
                Info.GroupInfo group;
                if (!mgr.dicItemGroupCategory.TryGetValue(info.group, out group) || group == null)
                {
                    return string.Empty;
                }
                string cat = "?";
                if (group.dicCategory != null && group.dicCategory.ContainsKey(info.category))
                {
                    cat = group.dicCategory[info.category];
                }
                return (group.name ?? "?") + " / " + cat + " / #" + info.no;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        internal static string SafeItemMod(OCIItem oi)
        {
            try
            {
                OIItemInfo info = oi.itemInfo;
                Info mgr = Singleton<Info>.Instance;
                if (info == null || mgr == null || mgr.dicItemLoadInfo == null)
                {
                    return string.Empty;
                }
                Dictionary<int, Dictionary<int, Info.ItemLoadInfo>> byGroup;
                if (!mgr.dicItemLoadInfo.TryGetValue(info.group, out byGroup) || byGroup == null)
                {
                    return string.Empty;
                }
                Dictionary<int, Info.ItemLoadInfo> byCat;
                if (!byGroup.TryGetValue(info.category, out byCat) || byCat == null)
                {
                    return string.Empty;
                }
                Info.ItemLoadInfo load;
                if (!byCat.TryGetValue(info.no, out load) || load == null)
                {
                    return string.Empty;
                }
                return load.manifest ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        internal static OCIChar FindOwnerChar(ObjectCtrlInfo ctrl)
        {
            if (ctrl == null)
            {
                return null;
            }
            ObjectCtrlInfo parent = ctrl.parentInfo;
            int guard = 0;
            while (parent != null && guard++ < 16)
            {
                OCIChar oc = parent as OCIChar;
                if (oc != null)
                {
                    return oc;
                }
                parent = parent.parentInfo;
            }
            return null;
        }

        internal static int FindSlot(OCIChar oc, ObjectInfo info)
        {
            try
            {
                OICharInfo ci = oc != null ? oc.oiCharInfo : null;
                if (ci == null || ci.child == null || info == null)
                {
                    return -1;
                }
                foreach (KeyValuePair<int, List<ObjectInfo>> kv in ci.child)
                {
                    List<ObjectInfo> list = kv.Value;
                    if (list == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] == info)
                        {
                            return kv.Key;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return -1;
        }

        internal static string SafePointName(int slot)
        {
            if (slot < 0)
            {
                return string.Empty;
            }
            try
            {
                Info mgr = Singleton<Info>.Instance;
                Info.AccessoryPointInfo info;
                if (mgr != null && mgr.dicAccessoryPointInfo != null &&
                    mgr.dicAccessoryPointInfo.TryGetValue(slot, out info) && info != null)
                {
                    return info.name;
                }
            }
            catch (Exception)
            {
            }
            return "挂载点 " + slot;
        }

        // ── 排序 ──────────────────────────────────────────────
        private static int CompareChar(CharRow a, CharRow b)
        {
            if (a.Sex != b.Sex)
            {
                return a.Sex.CompareTo(b.Sex);
            }
            return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        }

        private static readonly string[] GenitalKeys = new string[38]
        {
            "penis", "dick", "cock", "phallus", "glans", "testicle", "testis", "scrotum", "foreskin", "phimosis",
            "spermatic", "pubic", "ペニス", "ちんぽ", "チンポ", "ちんこ", "チンコ", "肉棒", "亀頭", "きんたま",
            "睾丸", "陰茎", "陰嚢", "精巣", "包茎", "包皮", "勃起", "男根", "阴茎", "龟头",
            "睾丸", "阴囊", "包茎", "包皮", "肉棒", "性器", "巨根", "肉根"
        };

        private static bool IsGenital(string name, string path)
        {
            string a = (name ?? string.Empty).ToLowerInvariant();
            string b = (path ?? string.Empty).ToLowerInvariant();
            for (int i = 0; i < GenitalKeys.Length; i++)
            {
                if (a.Contains(GenitalKeys[i]) || b.Contains(GenitalKeys[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private static int ItemRank(ItemRow r)
        {
            string name = (r.Name ?? string.Empty).ToLowerInvariant();
            string path = string.IsNullOrEmpty(r.Path) ? string.Empty : r.Path.ToLowerInvariant();
            if (name.Contains("dynamicbonecollider") || name.Contains("dynbonecollider") ||
                name.Contains("bonecollider") || name.Contains("动态骨") || name.Contains("dyncollider"))
            {
                return 0;
            }
            if (IsGenital(name, path))
            {
                return 1;
            }
            if (name.Contains("sound") || name.Contains("音效") || name.Contains("声音") ||
                name.Contains("audio") || name.Contains("voice") || path.Contains("soundmod") || path.Contains("sound"))
            {
                return 2;
            }
            return 3;
        }

        private static int CompareItem(ItemRow a, ItemRow b)
        {
            int ra = ItemRank(a);
            int rb = ItemRank(b);
            if (ra != rb)
            {
                return ra.CompareTo(rb);
            }
            return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        }

        // ── 效果预设 ──────────────────────────────────────────
        internal void EnsurePresets()
        {
            if (PresetsLoaded)
            {
                return;
            }
            PresetsLoaded = true;
            EnsureOverrides();
            PresetIO.Load(Presets, PresetPath);
        }

        internal void SavePresets()
        {
            PresetIO.Save(Presets, PresetPath);
        }

        internal void AddPreset(string name)
        {
            EnsurePresets();
            List<PresetItem> items = PresetIO.SnapshotSelection(this);
            if (items.Count == 0)
            {
                PresetHint = "先勾选效果行左边的复选框，再存预设。";
                NeedsRebuild = true;
                return;
            }
            EffPreset preset = new EffPreset();
            preset.Name = string.IsNullOrEmpty(name) || name.Trim().Length == 0
                ? "预设 " + (Presets.Count + 1)
                : name.Trim();
            preset.Items.AddRange(items);
            preset.SavedAt = DateTime.Now.Ticks;
            Presets.Add(preset);
            SavePresets();
            PresetHint = "已存 " + preset.Name + "（" + items.Count + " 件，含参数）";
            NeedsRebuild = true;
            if (PresetWin != null)
            {
                PresetWin.Rebuild();
            }
        }

        internal void OverwritePreset(EffPreset preset)
        {
            if (preset == null)
            {
                return;
            }
            List<PresetItem> items = PresetIO.SnapshotSelection(this);
            if (items.Count == 0)
            {
                PresetHint = "先勾选效果行左边的复选框，再覆盖。";
                NeedsRebuild = true;
                return;
            }
            preset.Items.Clear();
            preset.Items.AddRange(items);
            preset.SavedAt = DateTime.Now.Ticks;
            SavePresets();
            PresetHint = "已覆盖 " + preset.Name + "（" + items.Count + " 件）";
            NeedsRebuild = true;
        }

        internal void RenamePreset(EffPreset preset, string name)
        {
            if (preset == null)
            {
                return;
            }
            string n = name != null ? name.Trim() : "";
            if (n.Length == 0)
            {
                PresetHint = "先在输入框写新名字，再点「改名」。";
                NeedsRebuild = true;
                return;
            }
            string old = preset.Name;
            preset.Name = n;
            preset.SavedAt = DateTime.Now.Ticks;
            SavePresets();
            PresetHint = "已改名：" + old + " → " + n;
            NeedsRebuild = true;
        }

        internal void DeletePreset(EffPreset preset)
        {
            if (preset == null)
            {
                return;
            }
            Presets.Remove(preset);
            SavePresets();
            PresetHint = "已删除预设";
            NeedsRebuild = true;
        }

        internal void ApplyPreset(EffPreset preset)
        {
            if (preset == null)
            {
                return;
            }
            int added = 0;
            int matched = 0;
            for (int i = 0; i < preset.Items.Count; i++)
            {
                PresetItem item = preset.Items[i];
                string key = item.Group + "|" + item.Category + "|" + item.No;
                ItemRow row = null;
                for (int j = 0; j < AllItems.Count; j++)
                {
                    if (ItemKey(AllItems[j].Ctrl) == key)
                    {
                        row = AllItems[j];
                        break;
                    }
                }
                if (row != null)
                {
                    SetItemVisible(row, item.Vis);
                    PresetIO.ApplySnapshot(row.Ctrl, item);
                    matched++;
                    continue;
                }
                OCIItem spawned = PresetIO.SpawnItem(item.Group, item.Category, item.No);
                if (spawned != null)
                {
                    try
                    {
                        spawned.visible = item.Vis;
                    }
                    catch (Exception)
                    {
                    }
                    PresetIO.ApplySnapshot(spawned, item);
                    added++;
                }
            }
            PresetHint = "已读入 " + preset.Name + "（新加 " + added + "，改显隐 " + matched + "）";
            NeedsRebuild = true;
            Log.LogInfo("应用预设 " + preset.Name + ": 新增 " + added + "，匹配 " + matched);
        }
    }

    internal sealed class CharRow
    {
        public OCIChar Ctrl;
        public int Key;
        public string Name;
        public int Sex;
    }

    internal sealed class ItemRow
    {
        public OCIItem Ctrl;
        public int Key;
        public string Name;
        public string Zh;
        public string Mark;
        public string Path;
        public string Mod;
        public bool Effect;
        public int Slot;
        public bool Attached;
        public string SlotName;
    }

    internal sealed class CardRow
    {
        public string Path;
        public string Name;
        public DateTime Modified;
        public DateTime Created;
        public long LastUsed;
    }
}
