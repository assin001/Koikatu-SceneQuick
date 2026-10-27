using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KK_SceneQuick
{
    /// <summary>效果预设窗口：保存 / 读入 / 覆盖 / 改名 / 删除。</summary>
    internal sealed class PresetWindow
    {
        private readonly SceneQuickPlugin _p;

        private GameObject _canvasGo;
        private RectTransform _panel;
        private RectTransform _viewport;
        private RectTransform _content;
        private Text _hint;
        private InputField _input;

        private float _scroll;
        private float _contentH;
        private float _viewH;
        private bool _built;
        private bool _visible;

        private const float WinW = 620f;
        private const float WinH = 560f;

        public PresetWindow(SceneQuickPlugin plugin)
        {
            _p = plugin;
        }

        internal string DraftName
        {
            get { return _input != null ? _input.text : ""; }
        }

        internal bool IsVisible
        {
            get { return _visible; }
        }

        internal Transform CanvasTransform
        {
            get { return _canvasGo != null ? _canvasGo.transform : null; }
        }

        internal void Toggle()
        {
            if (_visible)
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
                EnsureBuilt();
                _p.EnsurePresets();
                _visible = true;
                _canvasGo.SetActive(true);
                Rebuild();
            }
            catch (Exception ex)
            {
                SceneQuickPlugin.Log.LogWarning("打开预设窗口失败: " + ex.Message);
            }
        }

        internal void Hide()
        {
            _visible = false;
            if (_canvasGo != null)
            {
                _canvasGo.SetActive(false);
            }
        }

        private void EnsureBuilt()
        {
            if (_built && _canvasGo != null)
            {
                return;
            }
            _canvasGo = new GameObject("KK_SceneQuick_PresetCanvas");
            Object.DontDestroyOnLoad(_canvasGo);
            Canvas canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32010;
            CanvasScaler scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            _canvasGo.AddComponent<GraphicRaycaster>();
            RectTransform root = _canvasGo.GetComponent<RectTransform>();

            RectTransform dim = UiKit.Box(root, "Dim", 0f, 0f, Screen.width, Screen.height, new Color(0f, 0f, 0f, 0.55f), 0, true);
            UiKit.Hit(dim, Hide);

            _panel = UiKit.Box(root, "Panel", 0f, 0f, WinW, WinH, UiTheme.Window, UiTheme.RadiusPanel, true);
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;

            RectTransform head = UiKit.Box(_panel, "Head", 0f, 0f, WinW, 56f, UiTheme.Page, UiTheme.RadiusPanel, true);
            UiKit.Box(head, "HeadFill", 0f, 42f, WinW, 14f, UiTheme.Page, 0, false);
            UiKit.Box(head, "Bar", 14f, 16f, 3f, 20f, UiTheme.Accent, 2, false);
            Text title = UiKit.Label(head, "效果预设", 16, UiTheme.Text, TextAnchor.MiddleLeft, 24f, 10f, 300f, 24f);
            title.fontStyle = FontStyle.Bold;
            UiKit.Label(head, "保存 / 读入效果物件的显隐与参数", 10, UiTheme.TextDim, TextAnchor.MiddleLeft,
                24f, 32f, 320f, 16f);
            UiKit.Btn(head, "关闭", 11, UiTheme.DangerSoft, UiTheme.Text, WinW - 76f, 15f, 58f, 26f, Hide);

            // 输入栏
            RectTransform inputRow = UiKit.Box(_panel, "InputRow", 16f, 68f, WinW - 32f, 34f,
                UiTheme.Field, UiTheme.RadiusChip, true);
            _input = inputRow.gameObject.AddComponent<InputField>();
            _input.targetGraphic = inputRow.GetComponent<Image>();
            _input.lineType = InputField.LineType.SingleLine;
            Text inputText = UiKit.Label(inputRow, "", 12, UiTheme.Text, TextAnchor.MiddleLeft, 10f, 0f,
                WinW - 52f, 34f);
            inputText.supportRichText = false;
            inputText.raycastTarget = false;
            _input.textComponent = inputText;
            Text placeholder = UiKit.Label(inputRow, "输入预设名，然后「存为新预设」", 11, UiTheme.TextDim,
                TextAnchor.MiddleLeft, 10f, 0f, WinW - 52f, 34f);
            placeholder.supportRichText = false;
            _input.placeholder = placeholder;
            UiKit.Btn(_panel, "存为新预设", 11, UiTheme.Accent, Color.white, WinW - 132f, 68f, 116f, 34f, () =>
            {
                _p.AddPreset(_input != null ? _input.text : "");
                if (_input != null)
                {
                    _input.text = "";
                }
                Rebuild();
            }, UiTheme.RadiusChip);

            // 列表
            _viewH = WinH - 68f - 34f - 12f - 48f;
            _viewport = UiKit.Box(_panel, "View", 16f, 112f, WinW - 32f, _viewH, new Color(0f, 0f, 0f, 0f), 0, true);
            _viewport.gameObject.AddComponent<RectMask2D>();
            UiKit.AddScroll(_viewport, delta =>
            {
                if (Mathf.Abs(delta) < 0.01f)
                {
                    return;
                }
                float max = Mathf.Max(0f, _contentH - _viewH);
                _scroll = Mathf.Clamp(_scroll - delta * 60f, 0f, max);
                if (_content != null)
                {
                    _content.anchoredPosition = new Vector2(0f, _scroll);
                }
            });
            _content = UiKit.Node(_viewport, "Content");
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(0f, 1f);
            _content.pivot = new Vector2(0f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(WinW - 32f, _viewH);

            // 底栏
            RectTransform foot = UiKit.Box(_panel, "Foot", 0f, WinH - 46f, WinW, 46f, UiTheme.Sunken, 0, false);
            UiKit.Btn(foot, "刷新列表", 10, UiTheme.SurfaceAlt, UiTheme.TextSub, 16f, 10f, 76f, 26f, () =>
            {
                _p.PresetsLoaded = false;
                _p.Presets.Clear();
                _p.EnsurePresets();
                Rebuild();
            });
            UiKit.Btn(foot, "打开预设目录", 10, UiTheme.SurfaceAlt, UiTheme.TextSub, 98f, 10f, 100f, 26f, () =>
            {
                try
                {
                    string dir = Path.GetDirectoryName(SceneQuickPlugin.PresetPath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
                    }
                }
                catch (Exception ex)
                {
                    SceneQuickPlugin.Log.LogWarning("打开目录失败: " + ex.Message);
                }
            });
            _hint = UiKit.Label(foot, "", 10, UiTheme.TextDim, TextAnchor.MiddleRight, 200f, 0f, WinW - 216f, 46f);

            _built = true;
            _canvasGo.SetActive(false);
        }

        internal void Rebuild()
        {
            if (!_built || _content == null)
            {
                return;
            }
            UiTheme.DestroyChildren(_content);
            float cursor = 4f;
            float w = WinW - 32f;
            List<EffPreset> presets = _p.Presets;

            if (presets.Count == 0)
            {
                Text t = UiKit.Label(_content, "还没有预设。先在「效果」栏勾选效果物件，再存为新预设。", 11,
                    UiTheme.TextDim, TextAnchor.UpperLeft, 8f, cursor, w - 16f, 40f);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                cursor += 44f;
            }

            for (int i = 0; i < presets.Count; i++)
            {
                EffPreset preset = presets[i];
                RectTransform row = UiKit.Box(_content, "Preset", 0f, cursor, w, 42f,
                    i % 2 == 0 ? UiTheme.Surface : UiTheme.SurfaceAlt, UiTheme.RadiusItem, true);
                string when = "";
                if (preset.SavedAt > 0)
                {
                    try
                    {
                        when = new DateTime(preset.SavedAt).ToString("MM-dd HH:mm");
                    }
                    catch (Exception)
                    {
                    }
                }
                UiKit.Label(row, preset.Name, 12, UiTheme.Text, TextAnchor.MiddleLeft, 10f, 2f, w - 340f, 20f);
                UiKit.Label(row, when + "   ·   " + preset.Items.Count + " 件", 9, UiTheme.TextDim,
                    TextAnchor.MiddleLeft, 10f, 21f, w - 340f, 16f);

                float bx = w - 322f;
                bool first = i == 0;
                bool last = i == presets.Count - 1;
                UiKit.Btn(row, "▲", 9, UiTheme.SurfaceAlt, UiTheme.TextSub, bx, 9f, 24f, 24f, () =>
                {
                    if (!first)
                    {
                        MovePreset(preset, -1);
                    }
                });
                UiKit.Btn(row, "▼", 9, UiTheme.SurfaceAlt, UiTheme.TextSub, bx + 26f, 9f, 24f, 24f, () =>
                {
                    if (!last)
                    {
                        MovePreset(preset, 1);
                    }
                });
                UiKit.Btn(row, "读入", 10, UiTheme.Accent, Color.white, bx + 56f, 9f, 52f, 24f, () =>
                {
                    _p.ApplyPreset(preset);
                    _hint.text = _p.PresetHint;
                });
                UiKit.Btn(row, "覆盖", 10, UiTheme.SurfaceAlt, UiTheme.Text, bx + 112f, 9f, 52f, 24f, () =>
                {
                    _p.OverwritePreset(preset);
                    Rebuild();
                });
                UiKit.Btn(row, "改名", 10, UiTheme.SurfaceAlt, UiTheme.Text, bx + 168f, 9f, 52f, 24f, () =>
                {
                    _p.RenamePreset(preset, _input != null ? _input.text : "");
                    Rebuild();
                });
                UiKit.Btn(row, "删", 10, UiTheme.DangerSoft, UiTheme.Text, bx + 224f, 9f, 34f, 24f, () =>
                {
                    _p.DeletePreset(preset);
                    Rebuild();
                });
                cursor += 44f;
            }

            _contentH = cursor + 8f;
            _content.sizeDelta = new Vector2(w, Mathf.Max(_contentH, _viewH));
            _scroll = Mathf.Clamp(_scroll, 0f, Mathf.Max(0f, _contentH - _viewH));
            _content.anchoredPosition = new Vector2(0f, _scroll);
            if (_hint != null)
            {
                _hint.text = string.IsNullOrEmpty(_p.PresetHint)
                    ? "预设文件：" + SceneQuickPlugin.PresetPath
                    : _p.PresetHint;
            }
        }

        private void MovePreset(EffPreset preset, int delta)
        {
            try
            {
                int idx = _p.Presets.IndexOf(preset);
                int target = idx + delta;
                if (idx < 0 || target < 0 || target >= _p.Presets.Count)
                {
                    return;
                }
                _p.Presets.RemoveAt(idx);
                _p.Presets.Insert(target, preset);
                _p.SavePresets();
                Rebuild();
            }
            catch (Exception ex)
            {
                SceneQuickPlugin.Log.LogWarning("调整预设顺序失败: " + ex.Message);
            }
        }
    }
}
