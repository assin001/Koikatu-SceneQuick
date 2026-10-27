using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KK_SceneQuick
{
    /// <summary>
    /// 右侧「快捷」面板：标题栏 + 三栏（人物替换 / 效果 / 场景物品）+ 底部状态条。
    /// 视觉沿用 SceneBrowser 的扁平深色圆角风格。
    /// </summary>
    internal sealed class QuickPanel
    {
        private readonly SceneQuickPlugin _p;

        private GameObject _canvasGo;
        private RectTransform _panel;
        private RectTransform _viewport;
        private RectTransform _content;
        private RectTransform _tab;
        private Text _status;
        private RectTransform _scrollBar;

        private float _scroll;
        private float _contentH;
        private float _viewH;
        private float _panelW = 430f;
        private float _panelH;
        private float _cursor;
        private float _contentW;

        private int _builtW;
        private int _builtH;
        private bool _built;
        private bool _visible;
        private bool _diagLogged;

        public QuickPanel(SceneQuickPlugin plugin)
        {
            _p = plugin;
        }

        internal bool IsBuilt
        {
            get { return _built; }
        }

        internal Transform CanvasTransform
        {
            get { return _canvasGo != null ? _canvasGo.transform : null; }
        }

        internal void EnsureBuilt()
        {
            if (_built && _builtW == Screen.width && _builtH == Screen.height)
            {
                return;
            }
            if (_canvasGo != null)
            {
                try
                {
                    Object.Destroy(_canvasGo);
                }
                catch (Exception)
                {
                }
                _canvasGo = null;
                _built = false;
            }
            Build();
        }

        internal void SetVisible(bool show)
        {
            _visible = show;
            if (_panel != null)
            {
                _panel.gameObject.SetActive(show);
            }
            if (show)
            {
                _scroll = 0f;
                ApplyScroll();
            }
        }

        // ── 框架 ──────────────────────────────────────────────
        private void Build()
        {
            _canvasGo = new GameObject("KK_SceneQuick_Canvas");
            Object.DontDestroyOnLoad(_canvasGo);
            Canvas canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            CanvasScaler scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            _canvasGo.AddComponent<GraphicRaycaster>();
            RectTransform root = _canvasGo.GetComponent<RectTransform>();

            float ratio = 0.72f;
            try
            {
                if (_p.HeightRatio != null)
                {
                    ratio = Mathf.Clamp(_p.HeightRatio.Value, 0.4f, 1f);
                }
            }
            catch (Exception)
            {
            }
            _panelW = 430f;
            _panelH = Mathf.Clamp(Screen.height * ratio, 420f, Screen.height - 40f);
            _contentW = _panelW - 20f;

            // 右侧「快捷」竖条（常驻）
            _tab = UiKit.Box(root, "Tab", 0f, 0f, 28f, 148f, UiTheme.Accent, UiTheme.RadiusCard, true);
            _tab.anchorMin = new Vector2(1f, 0.5f);
            _tab.anchorMax = new Vector2(1f, 0.5f);
            _tab.pivot = new Vector2(1f, 0.5f);
            _tab.anchoredPosition = new Vector2(-2f, 0f);
            UiKit.Hit(_tab, () => _p.Toggle());
            Text tabText = UiKit.Label(_tab, "快\n捷", 13, Color.white, TextAnchor.MiddleCenter, 0f, 0f, 28f, 148f);
            tabText.lineSpacing = 1.25f;
            tabText.fontStyle = FontStyle.Bold;

            // 面板
            _panel = UiKit.Box(root, "Panel", 0f, 0f, _panelW, _panelH, UiTheme.Window, UiTheme.RadiusPanel, true);
            _panel.anchorMin = new Vector2(1f, 0.5f);
            _panel.anchorMax = new Vector2(1f, 0.5f);
            _panel.pivot = new Vector2(1f, 0.5f);
            _panel.anchoredPosition = new Vector2(-30f, 0f);
            _panel.gameObject.SetActive(_visible);

            BuildHead();
            BuildBody();
            BuildFoot();

            _built = true;
            _builtW = Screen.width;
            _builtH = Screen.height;
            _diagLogged = false;
        }

        private void BuildHead()
        {
            RectTransform head = UiKit.Box(_panel, "Head", 0f, 0f, _panelW, 52f, UiTheme.Page, UiTheme.RadiusPanel, true);
            // 补掉头部底边的圆角，让标题区与内容区自然衔接
            UiKit.Box(head, "HeadFill", 0f, 38f, _panelW, 14f, UiTheme.Page, 0, false);

            UiKit.Box(head, "TitleBar", 14f, 14f, 3f, 18f, UiTheme.Accent, 2, false);
            Text title = UiKit.Label(head, "场景快捷", 16, UiTheme.Text, TextAnchor.MiddleLeft, 24f, 8f, 200f, 24f);
            title.fontStyle = FontStyle.Bold;
            Text sub = UiKit.Label(head, "CharaStudio", 11, UiTheme.TextDim, TextAnchor.MiddleLeft, 24f, 29f, 220f, 16f);
            sub.name = "SubTitle";

            UiKit.Btn(head, "刷新", 11, UiTheme.SurfaceAlt, UiTheme.Text, _panelW - 140f, 13f, 54f, 26f,
                () => _p.NeedsRebuild = true);
            UiKit.Btn(head, "关闭", 11, UiTheme.DangerSoft, UiTheme.Text, _panelW - 78f, 13f, 54f, 26f,
                () => _p.Hide());
        }

        private void BuildBody()
        {
            _viewH = _panelH - 52f - 34f - 12f;
            _viewport = UiKit.Box(_panel, "View", 10f, 58f, _panelW - 20f, _viewH, new Color(0f, 0f, 0f, 0f), 0, true);
            _viewport.gameObject.AddComponent<RectMask2D>();
            UiKit.AddScroll(_viewport, OnWheel);

            _content = UiKit.Node(_viewport, "Content");
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(0f, 1f);
            _content.pivot = new Vector2(0f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(_contentW, _viewH);

            _scrollBar = UiKit.Box(_panel, "ScrollBar", _panelW - 6f, 58f, 3f, 40f, UiTheme.Border, 2, false);
            _scrollBar.gameObject.SetActive(false);
        }

        private void BuildFoot()
        {
            RectTransform foot = UiKit.Box(_panel, "Foot", 0f, _panelH - 34f, _panelW, 34f, UiTheme.Sunken, 0, false);
            _status = UiKit.Label(foot, "", 10, UiTheme.TextDim, TextAnchor.MiddleLeft, 14f, 0f, _panelW - 28f, 34f);
        }

        private void OnWheel(float delta)
        {
            if (Mathf.Abs(delta) < 0.01f)
            {
                return;
            }
            float max = Mathf.Max(0f, _contentH - _viewH);
            _scroll = Mathf.Clamp(_scroll - delta * 60f, 0f, max);
            ApplyScroll();
        }

        private void ApplyScroll()
        {
            if (_content != null)
            {
                _content.anchoredPosition = new Vector2(0f, _scroll);
            }
            if (_scrollBar != null)
            {
                float max = Mathf.Max(0f, _contentH - _viewH);
                bool show = max > 1f;
                _scrollBar.gameObject.SetActive(show);
                if (show && _viewH > 0f)
                {
                    float h = Mathf.Max(28f, _viewH * (_viewH / Mathf.Max(1f, _contentH)));
                    float y = 58f + (1f - (_scroll + _viewH) / Mathf.Max(1f, _contentH)) * (_viewH - h);
                    _scrollBar.sizeDelta = new Vector2(3f, h);
                    _scrollBar.anchoredPosition = new Vector2(_panelW - 6f, -y);
                }
            }
        }

        // ── 内容重绘 ──────────────────────────────────────────
        internal void Rebuild()
        {
            if (!_built || _content == null)
            {
                return;
            }
            UiTheme.DestroyChildren(_content);
            _cursor = 4f;

            BuildCharSection();
            BuildEffectSection();
            BuildItemSection();

            _contentH = _cursor + 10f;
            _content.sizeDelta = new Vector2(_contentW, Mathf.Max(_contentH, _viewH));
            _scroll = Mathf.Clamp(_scroll, 0f, Mathf.Max(0f, _contentH - _viewH));
            ApplyScroll();
            if (!_diagLogged)
            {
                _diagLogged = true;
                string info = "面板 " + _panelW + "x" + _panelH + "  内容高 " + _contentH +
                              "  角色 " + _p.Chars.Count + "  物品 " + _p.AllItems.Count;
                try
                {
                    Vector3[] corners = new Vector3[4];
                    _panel.GetWorldCorners(corners);
                    Vector2 a = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                    Vector2 b = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                    info += "  active=" + _panel.gameObject.activeInHierarchy + "/" + _panel.gameObject.activeSelf +
                            "  canvas=" + (_canvasGo != null && _canvasGo.activeInHierarchy) +
                            "  screen=" + Screen.width + "x" + Screen.height +
                            "  rect=" + a.x.ToString("0") + "," + a.y.ToString("0") + " - " +
                            b.x.ToString("0") + "," + b.y.ToString("0");
                }
                catch (Exception ex)
                {
                    info += " diag失败:" + ex.Message;
                }
                SceneQuickPlugin.Log.LogInfo("[ui] " + info);
            }
            if (_status != null)
            {
                _status.text = _p.Chars.Count + " 角色  ·  " + _p.Items.Count + " 物品  ·  " +
                               _p.AllItems.Count + " 含挂载  ·  " + HotkeyText();
            }
            Text sub = _panel.Find("Head/SubTitle") != null
                ? _panel.Find("Head/SubTitle").GetComponent<Text>()
                : null;
            if (sub != null)
            {
                sub.text = _p.Chars.Count > 0
                    ? "当前场景 " + _p.Chars.Count + " 角色 / " + _p.AllItems.Count + " 物件"
                    : "场景里还没有角色";
            }
            BuildCtx();
        }

        private string HotkeyText()
        {
            try
            {
                return _p.HotkeyLabel + " 开关";
            }
            catch (Exception)
            {
                return "";
            }
        }

        private RectTransform Row(float h, Color bg, bool raycast, int radius)
        {
            RectTransform rt = UiKit.Box(_content, "Row", 0f, _cursor, _contentW, h, bg, radius, raycast);
            _cursor += h + 2f;
            return rt;
        }

        private void Note(string text)
        {
            Text t = UiKit.LabelClip(_content, text, 10, UiTheme.TextDim, TextAnchor.UpperLeft, 8f, _cursor,
                _contentW - 16f, 30f);
            _cursor += 32f;
        }

        private void Gap(float h)
        {
            _cursor += h;
        }

        private void SectionHeader(string title, string hint, bool folded, Action onToggle)
        {
            RectTransform rt = UiKit.Box(_content, "Sec", 0f, _cursor, _contentW, 34f, UiTheme.Surface, UiTheme.RadiusCard, true);
            UiKit.Hit(rt, onToggle);
            UiKit.Box(rt, "Bar", 0f, 9f, 3f, 16f, folded ? UiTheme.Border : UiTheme.Accent, 2, false);
            Text t = UiKit.Label(rt, title, 13, UiTheme.Text, TextAnchor.MiddleLeft, 12f, 0f, 240f, 34f);
            t.fontStyle = FontStyle.Bold;
            UiKit.Label(rt, hint, 10, UiTheme.TextDim, TextAnchor.MiddleRight, 12f, 0f, _contentW - 60f, 34f);
            if (onToggle != null)
            {
                UiKit.Label(rt, folded ? "▸" : "▾", 12, UiTheme.TextSub, TextAnchor.MiddleCenter, _contentW - 26f, 0f,
                    22f, 34f);
            }
            _cursor += 38f;
        }

        private void ToolBar(Action build)
        {
            RectTransform rt = UiKit.Box(_content, "Tools", 0f, _cursor, _contentW, 28f, new Color(0f, 0f, 0f, 0f), 0, false);
            build();
            _cursor += 32f;
        }

        private Button Chip(Transform parent, string label, float x, float y, float w, float h, Color bg, Color fg,
            Action onClick, int fontSize)
        {
            return UiKit.Btn(parent, label, fontSize, bg, fg, x, y, w, h, onClick, UiTheme.RadiusChip);
        }

        // ── 1 人物替换 ────────────────────────────────────────
        private void BuildCharSection()
        {
            SectionHeader("1  人物替换", _p.Chars.Count + " 个角色", _p.FoldChars, () =>
            {
                _p.FoldChars = !_p.FoldChars;
                _p.NeedsRebuild = true;
            });
            if (_p.FoldChars)
            {
                return;
            }
            if (_p.Chars.Count == 0)
            {
                Note("当前场景里没有角色。");
                return;
            }

            List<string> order = new List<string>();
            Dictionary<string, List<CharRow>> groups = new Dictionary<string, List<CharRow>>();
            for (int i = 0; i < _p.Chars.Count; i++)
            {
                CharRow c = _p.Chars[i];
                string key = "c|" + c.Sex + "|" + NameZh.NormName(c.Name);
                List<CharRow> list;
                if (!groups.TryGetValue(key, out list))
                {
                    list = new List<CharRow>();
                    groups[key] = list;
                    order.Add(key);
                }
                list.Add(c);
            }
            for (int i = 0; i < order.Count; i++)
            {
                List<CharRow> list = groups[order[i]];
                if (list.Count == 1)
                {
                    CharRowRow(list[0]);
                }
                else
                {
                    CharGroupRow(order[i], list);
                }
            }

            // 工具条
            CharRow sel = _p.FindChar(_p.SelCharKey);
            bool anyVisible = false;
            for (int i = 0; i < _p.Chars.Count; i++)
            {
                if (SceneQuickPlugin.SafeCharVisible(_p.Chars[i].Ctrl))
                {
                    anyVisible = true;
                    break;
                }
            }
            RectTransform tools = UiKit.Box(_content, "Tools", 0f, _cursor, _contentW, 28f, new Color(0f, 0f, 0f, 0f), 0, false);
            Chip(tools, anyVisible ? "全部隐藏" : "全部显示", 0f, 1f, 92f, 26f,
                anyVisible ? UiTheme.DangerSoft : UiTheme.SurfaceAlt, UiTheme.Text, () =>
                {
                    for (int i = 0; i < _p.Chars.Count; i++)
                    {
                        _p.SetCharVisible(_p.Chars[i], !anyVisible);
                    }
                    _p.NeedsRebuild = true;
                }, 11);
            string cardLabel = _p.ShowCards
                ? "收起角色卡"
                : (_p.MultiSel.Count > 0 ? "换所选 " + _p.MultiSel.Count + " 个…" : "换角色…");
            Chip(tools, cardLabel, 100f, 1f, 150f, 26f,
                _p.ShowCards ? UiTheme.SurfaceAlt : UiTheme.Accent, Color.white, () =>
                {
                    _p.ShowCards = !_p.ShowCards;
                    _p.NeedsRebuild = true;
                }, 11);
            if (_p.MultiSel.Count > 0)
            {
                Chip(tools, "清空选择", 258f, 1f, 82f, 26f, UiTheme.Surface, UiTheme.TextSub, () =>
                {
                    _p.MultiSel.Clear();
                    _p.NeedsRebuild = true;
                }, 10);
            }
            _cursor += 32f;

            if (_p.ShowCards)
            {
                BuildCardList(sel);
            }

            // 挂载物
            List<ItemRow> acc = new List<ItemRow>();
            for (int i = 0; i < _p.AllItems.Count; i++)
            {
                ItemRow r = _p.AllItems[i];
                if ((r.Slot >= 0 || r.Attached) && sel != null &&
                    SceneQuickPlugin.FindOwnerChar(r.Ctrl) == sel.Ctrl)
                {
                    acc.Add(r);
                }
            }
            SectionHeader("挂载物", acc.Count + " 个", false, null);
            if (acc.Count == 0)
            {
                Note("这个角色身上没有挂载物件。");
                return;
            }
            bool anyAcc = false;
            for (int i = 0; i < acc.Count; i++)
            {
                if (SceneQuickPlugin.CurrentVisible(acc[i]))
                {
                    anyAcc = true;
                    break;
                }
            }
            RectTransform accTools = UiKit.Box(_content, "Tools", 0f, _cursor, _contentW, 28f, new Color(0f, 0f, 0f, 0f), 0, false);
            Chip(accTools, anyAcc ? "全部隐藏" : "全部显示", 0f, 1f, 92f, 26f,
                anyAcc ? UiTheme.DangerSoft : UiTheme.SurfaceAlt, UiTheme.Text, () =>
                {
                    for (int i = 0; i < acc.Count; i++)
                    {
                        _p.SetItemVisible(acc[i], !anyAcc);
                    }
                    _p.NeedsRebuild = true;
                }, 11);
            _cursor += 32f;
            for (int i = 0; i < acc.Count; i++)
            {
                ItemRowRow(acc[i], true, 12f);
            }
        }

        private void CharRowRow(CharRow c)
        {
            bool selected = c.Key == _p.SelCharKey;
            bool visible = SceneQuickPlugin.SafeCharVisible(c.Ctrl);
            Color bg = selected ? UiTheme.AccentSoft : (visible ? UiTheme.Surface : UiTheme.Sunken);
            RectTransform rt = Row(30f, bg, true, UiTheme.RadiusItem);
            if (selected)
            {
                UiKit.Box(rt, "Sel", 0f, 6f, 3f, 18f, UiTheme.Accent, 2, false);
            }
            int key = c.Key;
            Chip(rt, visible ? "显" : "隐", 8f, 4f, 42f, 22f,
                visible ? UiTheme.GreenSoft : UiTheme.SurfaceAlt, visible ? UiTheme.Text : UiTheme.TextDim, () =>
                {
                    CharRow row = _p.FindChar(key);
                    if (row != null)
                    {
                        _p.SetCharVisible(row, !SceneQuickPlugin.SafeCharVisible(row.Ctrl));
                        _p.NeedsRebuild = true;
                    }
                }, 10);
            bool picked = _p.MultiSel.Contains(key);
            Chip(rt, picked ? "已选" : "选", 54f, 4f, 42f, 22f,
                picked ? UiTheme.Accent : UiTheme.SurfaceAlt, picked ? Color.white : UiTheme.TextSub, () =>
                {
                    if (!_p.MultiSel.Remove(key))
                    {
                        _p.MultiSel.Add(key);
                    }
                    _p.NeedsRebuild = true;
                }, 10);
            Chip(rt, "删", _contentW - 40f, 4f, 32f, 22f, UiTheme.DangerSoft, UiTheme.Text, () =>
                {
                    CharRow row = _p.FindChar(key);
                    if (row != null)
                    {
                        _p.DeleteObject(row.Ctrl, row.Key);
                        _p.NeedsRebuild = true;
                    }
                }, 10);
            string label = (c.Sex == 0 ? "男" : "女") + "   " + c.Name;
            Color fg = visible ? (selected ? UiTheme.Text : UiTheme.TextSub) : UiTheme.TextDim;
            UiKit.Label(rt, label, 12, fg, TextAnchor.MiddleLeft, 102f, 0f, _contentW - 146f, 30f);
            UiKit.Hit(rt, () =>
            {
                _p.SelectChar(key);
                _p.ShowCards = true;
                _p.NeedsRebuild = true;
            });
        }

        private void CharGroupRow(string key, List<CharRow> list)
        {
            bool expanded = _p.Expanded.Contains(key);
            bool visible = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (SceneQuickPlugin.SafeCharVisible(list[i].Ctrl))
                {
                    visible = true;
                    break;
                }
            }
            bool picked = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (_p.MultiSel.Contains(list[i].Key))
                {
                    picked = true;
                    break;
                }
            }
            RectTransform rt = Row(30f, visible ? UiTheme.Surface : UiTheme.Sunken, true, UiTheme.RadiusItem);
            List<CharRow> grp = list;
            Chip(rt, visible ? "显" : "隐", 8f, 4f, 42f, 22f,
                visible ? UiTheme.GreenSoft : UiTheme.SurfaceAlt, visible ? UiTheme.Text : UiTheme.TextDim, () =>
                {
                    for (int i = 0; i < grp.Count; i++)
                    {
                        _p.SetCharVisible(grp[i], !visible);
                    }
                    _p.NeedsRebuild = true;
                }, 10);
            Chip(rt, picked ? "已选" : "选", 54f, 4f, 42f, 22f,
                picked ? UiTheme.Accent : UiTheme.SurfaceAlt, picked ? Color.white : UiTheme.TextSub, () =>
                {
                    bool add = !picked;
                    for (int i = 0; i < grp.Count; i++)
                    {
                        if (add)
                        {
                            _p.MultiSel.Add(grp[i].Key);
                        }
                        else
                        {
                            _p.MultiSel.Remove(grp[i].Key);
                        }
                    }
                    _p.NeedsRebuild = true;
                }, 10);
            Chip(rt, "删", _contentW - 40f, 4f, 32f, 22f, UiTheme.DangerSoft, UiTheme.Text, () =>
                {
                    for (int i = 0; i < grp.Count; i++)
                    {
                        _p.DeleteObject(grp[i].Ctrl, grp[i].Key);
                    }
                    _p.NeedsRebuild = true;
                }, 10);
            Chip(rt, expanded ? "收起" : "展开", _contentW - 104f, 4f, 58f, 22f, UiTheme.SurfaceAlt, UiTheme.TextSub, () =>
                {
                    if (!_p.Expanded.Remove(key))
                    {
                        _p.Expanded.Add(key);
                    }
                    _p.NeedsRebuild = true;
                }, 10);
            CharRow first = list[0];
            string label = (first.Sex == 0 ? "男" : "女") + "   " + NameZh.NormName(first.Name) + "   ×" + list.Count;
            UiKit.Label(rt, label, 12, visible ? UiTheme.TextSub : UiTheme.TextDim, TextAnchor.MiddleLeft, 102f, 0f,
                _contentW - 214f, 30f);
            if (expanded)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    CharRowRow(list[i]);
                }
            }
        }

        private void BuildCardList(CharRow sel)
        {
            int sex = _p.CurrentSex();
            if (_p.Cards.Count == 0)
            {
                _p.LoadCards(sex);
            }
            RectTransform head = UiKit.Box(_content, "Cards", 0f, _cursor, _contentW, 28f, new Color(0f, 0f, 0f, 0f), 0, false);
            UiKit.Label(head, "角色卡（" + (sex == 0 ? "男" : "女") + "）  " + _p.Cards.Count + " 张", 11,
                UiTheme.TextSub, TextAnchor.MiddleLeft, 2f, 0f, 200f, 28f);
            _cursor += 30f;

            string[] sorts = new string[4] { "名字", "最近", "修改", "创建" };
            RectTransform sortRow = UiKit.Box(_content, "Sort", 0f, _cursor, _contentW, 26f, new Color(0f, 0f, 0f, 0f), 0, false);
            for (int i = 0; i < sorts.Length; i++)
            {
                int mode = i;
                bool on = _p.CardSort == mode;
                Chip(sortRow, sorts[i], i * 66f, 2f, 60f, 22f,
                    on ? UiTheme.Accent : UiTheme.SurfaceAlt, on ? Color.white : UiTheme.TextSub, () =>
                    {
                        _p.SetCardSort(mode);
                        _p.LoadCards(_p.CurrentSex());
                    }, 10);
            }
            _cursor += 30f;

            if (_p.Cards.Count == 0)
            {
                Note("这个性别的角色卡目录里没找到卡。");
                return;
            }
            int total = _p.Cards.Count;
            int pages = Mathf.Max(1, (total + _p.CardPageSize - 1) / _p.CardPageSize);
            _p.CardPage = Mathf.Clamp(_p.CardPage, 0, pages - 1);
            int start = _p.CardPage * _p.CardPageSize;
            int end = Mathf.Min(start + _p.CardPageSize, total);

            RectTransform listRt = UiKit.Box(_content, "CardList", 0f, _cursor, _contentW,
                (end - start) * 30f, UiTheme.Sunken, UiTheme.RadiusItem, true);
            UiKit.AddScroll(listRt, d =>
            {
                if (Mathf.Abs(d) < 0.01f)
                {
                    return;
                }
                _p.CardPage = Mathf.Clamp(_p.CardPage + (d > 0f ? -1 : 1), 0, pages - 1);
                _p.NeedsRebuild = true;
            });
            for (int i = start; i < end; i++)
            {
                CardRow card = _p.Cards[i];
                int idx = i;
                RectTransform cr = UiKit.Box(listRt, "Card", 2f, (i - start) * 30f + 2f, _contentW - 6f, 26f,
                    (i - start) % 2 == 0 ? UiTheme.Surface : UiTheme.SurfaceAlt, 6, true);
                string date = "";
                if (_p.CardSort == 1 && card.LastUsed > 0)
                {
                    date = new DateTime(card.LastUsed, DateTimeKind.Utc).ToLocalTime().ToString("yy-MM-dd");
                }
                else if (_p.CardSort == 2 && card.Modified.Year > 1)
                {
                    date = card.Modified.ToLocalTime().ToString("yy-MM-dd");
                }
                else if (_p.CardSort == 3 && card.Created.Year > 1)
                {
                    date = card.Created.ToLocalTime().ToString("yy-MM-dd");
                }
                UiKit.Label(cr, card.Name, 11, UiTheme.TextSub, TextAnchor.MiddleLeft, 8f, 0f, _contentW - 90f, 26f);
                if (date.Length > 0)
                {
                    UiKit.Label(cr, date, 10, UiTheme.TextDim, TextAnchor.MiddleRight, 52f, 0f, _contentW - 64f, 26f);
                }
                string path = card.Path;
                UiKit.Hit(cr, () => _p.ChangeChara(path));
            }
            _cursor += (end - start) * 30f + 6f;

            RectTransform pageRow = UiKit.Box(_content, "Pager", 0f, _cursor, _contentW, 26f, new Color(0f, 0f, 0f, 0f), 0, false);
            Chip(pageRow, "▲", 0f, 1f, 34f, 24f, UiTheme.SurfaceAlt, UiTheme.TextSub, () =>
                {
                    _p.CardPage = Mathf.Max(0, _p.CardPage - 1);
                    _p.NeedsRebuild = true;
                }, 10);
            Chip(pageRow, "▼", 38f, 1f, 34f, 24f, UiTheme.SurfaceAlt, UiTheme.TextSub, () =>
                {
                    _p.CardPage = Mathf.Min(pages - 1, _p.CardPage + 1);
                    _p.NeedsRebuild = true;
                }, 10);
            UiKit.Label(pageRow, (_p.CardPage + 1) + " / " + pages + "   滚轮翻页", 10, UiTheme.TextDim,
                TextAnchor.MiddleLeft, 80f, 1f, 200f, 24f);
            _cursor += 32f;
        }

        // ── 2 效果 ────────────────────────────────────────────
        private void BuildEffectSection()
        {
            List<ItemRow> effects = new List<ItemRow>();
            for (int i = 0; i < _p.AllItems.Count; i++)
            {
                if (_p.AllItems[i].Effect && !_p.AllItems[i].Attached)
                {
                    effects.Add(_p.AllItems[i]);
                }
            }
            SectionHeader("2  效果", effects.Count + " 个效果物件", _p.FoldEffects, () =>
            {
                _p.FoldEffects = !_p.FoldEffects;
                _p.NeedsRebuild = true;
            });
            if (_p.FoldEffects)
            {
                return;
            }
            if (effects.Count == 0)
            {
                Note("没识别到单独的效果物件。若作者用普通物品做滤镜，它们会出现在「场景物品」栏里，同样可以逐个开关。");
                return;
            }

            RectTransform t1 = UiKit.Box(_content, "Tools", 0f, _cursor, _contentW, 28f, new Color(0f, 0f, 0f, 0f), 0, false);
            Chip(t1, "全开", 0f, 1f, 48f, 26f, UiTheme.GreenSoft, UiTheme.Text, () =>
                {
                    for (int i = 0; i < effects.Count; i++)
                    {
                        _p.SetItemVisible(effects[i], true);
                    }
                    _p.NeedsRebuild = true;
                }, 11);
            Chip(t1, "全关", 52f, 1f, 48f, 26f, UiTheme.SurfaceAlt, UiTheme.Text, () =>
                {
                    for (int i = 0; i < effects.Count; i++)
                    {
                        _p.SetItemVisible(effects[i], false);
                    }
                    _p.NeedsRebuild = true;
                }, 11);
            Chip(t1, "重置", 104f, 1f, 48f, 26f, UiTheme.Surface, UiTheme.TextSub, () => _p.ResetItems(true), 11);
            Chip(t1, "存预设", 160f, 1f, 62f, 26f, UiTheme.Accent, Color.white, () =>
                {
                    string name = _p.PresetWin != null ? _p.PresetWin.DraftName : "";
                    _p.AddPreset(name);
                    if (_p.PresetWin != null)
                    {
                        _p.PresetWin.Show();
                    }
                }, 10);
            Chip(t1, "预设管理", 226f, 1f, 74f, 26f, UiTheme.SurfaceAlt, UiTheme.TextSub, () =>
                {
                    if (_p.PresetWin != null)
                    {
                        _p.PresetWin.Show();
                    }
                }, 10);
            Chip(t1, "勾全部", 304f, 1f, 58f, 26f, UiTheme.Surface, UiTheme.TextSub, () =>
                {
                    for (int i = 0; i < effects.Count; i++)
                    {
                        _p.PresetSel.Add(effects[i].Key);
                    }
                    _p.NeedsRebuild = true;
                }, 10);
            Chip(t1, "清勾选", 366f, 1f, 58f, 26f, UiTheme.Surface, UiTheme.TextSub, () =>
                {
                    _p.PresetSel.Clear();
                    _p.NeedsRebuild = true;
                }, 10);
            _cursor += 32f;

            // 分组
            List<string> order = new List<string>();
            Dictionary<string, List<ItemRow>> groups = new Dictionary<string, List<ItemRow>>();
            for (int i = 0; i < effects.Count; i++)
            {
                string key = "e|" + NameZh.NormName(effects[i].Name);
                List<ItemRow> list;
                if (!groups.TryGetValue(key, out list))
                {
                    list = new List<ItemRow>();
                    groups[key] = list;
                    order.Add(key);
                }
                list.Add(effects[i]);
            }
            for (int i = 0; i < order.Count; i++)
            {
                List<ItemRow> list = groups[order[i]];
                if (list.Count == 1)
                {
                    ItemRowRow(list[0], false, 0f);
                }
                else
                {
                    ItemGroupRow(order[i], list, true);
                }
            }
            Note("右键效果行可以把它改判成「普通物品」，或恢复自动判断。");
        }

        private void ItemGroupRow(string key, List<ItemRow> list, bool effect)
        {
            bool expanded = _p.Expanded.Contains(key);
            bool anyVis = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (SceneQuickPlugin.CurrentVisible(list[i]))
                {
                    anyVis = true;
                    break;
                }
            }
            bool selected = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Key == _p.SelItemKey)
                {
                    selected = true;
                    break;
                }
            }
            RectTransform rt = Row(30f, selected ? UiTheme.AccentSoft : (anyVis ? UiTheme.Surface : UiTheme.Sunken),
                true, UiTheme.RadiusItem);
            if (selected)
            {
                UiKit.Box(rt, "Sel", 0f, 6f, 3f, 18f, UiTheme.Accent, 2, false);
            }
            List<ItemRow> grp = list;
            Chip(rt, anyVis ? (effect ? "开" : "显") : (effect ? "关" : "隐"), 8f, 4f, 42f, 22f,
                anyVis ? UiTheme.GreenSoft : UiTheme.SurfaceAlt, anyVis ? UiTheme.Text : UiTheme.TextDim, () =>
                {
                    for (int i = 0; i < grp.Count; i++)
                    {
                        _p.SetItemVisible(grp[i], !anyVis);
                    }
                    _p.NeedsRebuild = true;
                }, 10);
            float nameX = 54f;
            if (effect)
            {
                bool allPicked = true;
                for (int i = 0; i < list.Count; i++)
                {
                    if (!_p.PresetSel.Contains(list[i].Key))
                    {
                        allPicked = false;
                        break;
                    }
                }
                Chip(rt, allPicked ? "已勾" : "勾", 54f, 4f, 42f, 22f,
                    allPicked ? UiTheme.Accent : UiTheme.SurfaceAlt, allPicked ? Color.white : UiTheme.TextSub, () =>
                    {
                        bool add = !allPicked;
                        for (int i = 0; i < grp.Count; i++)
                        {
                            if (add)
                            {
                                _p.PresetSel.Add(grp[i].Key);
                            }
                            else
                            {
                                _p.PresetSel.Remove(grp[i].Key);
                            }
                        }
                        _p.NeedsRebuild = true;
                    }, 10);
                nameX = 100f;
            }
            Chip(rt, "删", _contentW - 40f, 4f, 32f, 22f, UiTheme.DangerSoft, UiTheme.Text, () =>
                {
                    for (int i = 0; i < grp.Count; i++)
                    {
                        _p.DeleteObject(grp[i].Ctrl, grp[i].Key);
                    }
                    _p.NeedsRebuild = true;
                }, 10);
            Chip(rt, expanded ? "收起" : "展开", _contentW - 104f, 4f, 58f, 22f, UiTheme.SurfaceAlt,
                UiTheme.TextSub, () =>
                {
                    if (!_p.Expanded.Remove(key))
                    {
                        _p.Expanded.Add(key);
                    }
                    _p.NeedsRebuild = true;
                }, 10);
            ItemRow first = list[0];
            string name = string.IsNullOrEmpty(first.Zh) ? NameZh.NormName(first.Name) : first.Zh;
            UiKit.Label(rt, name + "   ×" + list.Count, 12, anyVis ? UiTheme.TextSub : UiTheme.TextDim,
                TextAnchor.MiddleLeft, nameX, 0f, _contentW - 214f, 30f);
            if (expanded)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    ItemRowRow(list[i], false, 12f);
                }
            }
        }

        private void ItemRowRow(ItemRow r, bool accessory, float indent)
        {
            bool visible = SceneQuickPlugin.CurrentVisible(r);
            bool selected = r.Key == _p.SelItemKey;
            bool tall = !accessory;
            float h = tall ? 36f : 30f;
            RectTransform rt = Row(h, selected ? UiTheme.AccentSoft : (visible ? UiTheme.Surface : UiTheme.Sunken),
                true, UiTheme.RadiusItem);
            if (selected)
            {
                UiKit.Box(rt, "Sel", 0f, 6f, 3f, 18f, UiTheme.Accent, 2, false);
            }
            ItemRow row = r;
            int key = r.Key;
            Chip(rt, visible ? (r.Effect && !accessory ? "开" : "显") : (r.Effect && !accessory ? "关" : "隐"),
                8f + indent, 4f, 42f, 22f,
                visible ? UiTheme.GreenSoft : UiTheme.SurfaceAlt, visible ? UiTheme.Text : UiTheme.TextDim, () =>
                {
                    ItemRow cur = _p.FindItem(key);
                    if (cur != null)
                    {
                        _p.SetItemVisible(cur, !SceneQuickPlugin.CurrentVisible(cur));
                        _p.NeedsRebuild = true;
                    }
                }, 10);
            float textX = 54f + indent;
            float textW = _contentW - textX - 46f;
            string title;
            if (accessory)
            {
                string slot = string.IsNullOrEmpty(r.SlotName) ? ("槽 " + r.Slot) : ("槽 " + r.Slot + " · " + r.SlotName);
                title = (string.IsNullOrEmpty(r.Zh) ? r.Name : r.Zh) + "   " + slot;
            }
            else
            {
                title = string.IsNullOrEmpty(r.Zh) ? r.Name : r.Zh;
            }
            UiKit.Label(rt, title, 12, visible ? UiTheme.Text : UiTheme.TextDim, TextAnchor.MiddleLeft,
                textX, tall ? 0f : 0f, textW, tall ? 18f : h);
            if (tall)
            {
                string sub = r.Path;
                if (!string.IsNullOrEmpty(r.Mod))
                {
                    sub += "   ·   " + NameZh.ShortMod(r.Mod);
                }
                if (!string.IsNullOrEmpty(r.Zh))
                {
                    sub = "原名 " + r.Name + "   ·   " + sub;
                }
                UiKit.Label(rt, sub, 9, UiTheme.TextDim, TextAnchor.MiddleLeft, textX, 19f, textW, 15f);
            }
            Chip(rt, "删", _contentW - 40f, tall ? 7f : 4f, 32f, 22f, UiTheme.DangerSoft, UiTheme.Text, () =>
                {
                    ItemRow cur = _p.FindItem(key);
                    if (cur != null)
                    {
                        _p.DeleteObject(cur.Ctrl, cur.Key);
                        _p.NeedsRebuild = true;
                    }
                }, 10);
            UiKit.Hit(rt, () => _p.SelectItem(_p.FindItem(key)));
            if (!accessory)
            {
                UiKit.AddRightClick(rt, () => _p.OpenCtx(_p.FindItem(key)));
            }
        }

        // ── 3 场景物品 ────────────────────────────────────────
        private void BuildItemSection()
        {
            List<ItemRow> items = new List<ItemRow>();
            for (int i = 0; i < _p.Items.Count; i++)
            {
                if (!_p.Items[i].Effect)
                {
                    items.Add(_p.Items[i]);
                }
            }
            SectionHeader("3  场景物品", items.Count + " 个", _p.FoldItems, () =>
            {
                _p.FoldItems = !_p.FoldItems;
                _p.NeedsRebuild = true;
            });
            if (_p.FoldItems)
            {
                return;
            }
            if (items.Count == 0)
            {
                Note("场景里没有独立物品（角色挂载物见上方「挂载物」）。");
                return;
            }
            List<ItemRow> ordered = new List<ItemRow>();
            for (int i = 0; i < items.Count; i++)
            {
                if (_p.BaseVisible(items[i]))
                {
                    ordered.Add(items[i]);
                }
            }
            for (int i = 0; i < items.Count; i++)
            {
                if (!_p.BaseVisible(items[i]))
                {
                    ordered.Add(items[i]);
                }
            }
            bool anyVis = false;
            for (int i = 0; i < items.Count; i++)
            {
                if (SceneQuickPlugin.CurrentVisible(items[i]))
                {
                    anyVis = true;
                    break;
                }
            }
            RectTransform t = UiKit.Box(_content, "Tools", 0f, _cursor, _contentW, 28f, new Color(0f, 0f, 0f, 0f), 0, false);
            Chip(t, anyVis ? "全部隐藏" : "全部显示", 0f, 1f, 92f, 26f,
                anyVis ? UiTheme.DangerSoft : UiTheme.SurfaceAlt, UiTheme.Text, () =>
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        _p.SetItemVisible(items[i], !anyVis);
                    }
                    _p.NeedsRebuild = true;
                }, 11);
            Chip(t, "重置", 100f, 1f, 62f, 26f, UiTheme.Surface, UiTheme.TextSub, () => _p.ResetItems(false), 11);
            UiKit.Label(t, "重置 = 恢复读卡时的显隐", 10, UiTheme.TextDim, TextAnchor.MiddleLeft, 172f, 1f, 220f, 26f);
            _cursor += 32f;

            List<string> order = new List<string>();
            Dictionary<string, List<ItemRow>> groups = new Dictionary<string, List<ItemRow>>();
            for (int i = 0; i < ordered.Count; i++)
            {
                string key = "i|" + NameZh.NormName(ordered[i].Name);
                List<ItemRow> list;
                if (!groups.TryGetValue(key, out list))
                {
                    list = new List<ItemRow>();
                    groups[key] = list;
                    order.Add(key);
                }
                list.Add(ordered[i]);
            }
            for (int i = 0; i < order.Count; i++)
            {
                List<ItemRow> list = groups[order[i]];
                if (list.Count == 1)
                {
                    ItemRowRow(list[0], false, 0f);
                }
                else
                {
                    ItemGroupRow(order[i], list, false);
                }
            }
        }

        // ── 右键改判菜单 ──────────────────────────────────────
        private void BuildCtx()
        {
            if (_panel == null)
            {
                return;
            }
            Transform old = _panel.Find("Ctx");
            if (old != null)
            {
                old.gameObject.SetActive(false);
                Object.Destroy(old.gameObject);
            }
            if (string.IsNullOrEmpty(_p.CtxKey))
            {
                return;
            }
            RectTransform overlay = UiKit.Box(_panel, "Ctx", 0f, 0f, _panelW, _panelH, new Color(0f, 0f, 0f, 0.45f), 0, true);
            UiKit.Hit(overlay, () =>
            {
                _p.CtxKey = null;
                _p.NeedsRebuild = true;
            });
            float w = Mathf.Min(300f, _panelW - 40f);
            float h = 196f;
            RectTransform box = UiKit.Box(overlay, "Box", (_panelW - w) * 0.5f, (_panelH - h) * 0.5f, w, h,
                UiTheme.Surface, UiTheme.RadiusCard, true);
            UiKit.Label(box, "改判物件", 13, UiTheme.Text, TextAnchor.MiddleLeft, 16f, 12f, w - 32f, 20f).fontStyle =
                FontStyle.Bold;
            string name = _p.CtxName ?? "";
            UiKit.Label(box, name.Length > 30 ? name.Substring(0, 30) + "…" : name, 10, UiTheme.TextDim,
                TextAnchor.MiddleLeft, 16f, 32f, w - 32f, 16f);
            ItemRow target = null;
            for (int i = 0; i < _p.AllItems.Count; i++)
            {
                if (SceneQuickPlugin.ItemKey(_p.AllItems[i].Ctrl) == _p.CtxKey)
                {
                    target = _p.AllItems[i];
                    break;
                }
            }
            Chip(box, "标记为效果物品", 16f, 60f, w - 32f, 28f, UiTheme.AccentSoft, UiTheme.Text, () =>
                {
                    ApplyCtx(target, 1);
                }, 11);
            Chip(box, "标记为普通物品", 16f, 94f, w - 32f, 28f, UiTheme.SurfaceAlt, UiTheme.Text, () =>
                {
                    ApplyCtx(target, 2);
                }, 11);
            Chip(box, "恢复自动判断", 16f, 128f, w - 32f, 28f, UiTheme.Surface, UiTheme.TextSub, () =>
                {
                    ApplyCtx(target, 0);
                }, 11);
            Chip(box, "取消", w - 76f, 160f, 60f, 24f, UiTheme.SurfaceAlt, UiTheme.TextSub, () =>
                {
                    _p.CtxKey = null;
                    _p.NeedsRebuild = true;
                }, 10);
        }

        private void ApplyCtx(ItemRow target, int mode)
        {
            _p.SetOverride(target, mode);
            _p.CtxKey = null;
            _p.CtxName = null;
            _p.NeedsRebuild = true;
        }
    }
}
