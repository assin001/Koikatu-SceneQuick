using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KK_SceneQuick
{
    /// <summary>
    /// uGUI 快速搭建工具：统一用「左上角为原点、y 向下」的绝对坐标，
    /// 与 RectTransform 的 pivot(0,1) 配合，方便按行排版。
    /// </summary>
    internal static class UiKit
    {
        public static RectTransform Node(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        /// <summary>在父节点左上角放置一个矩形；y 向下为正。</summary>
        public static RectTransform Box(Transform parent, string name, float x, float y, float w, float h,
            Color color, int radius, bool raycast)
        {
            RectTransform rt = Node(parent, name);
            Place(rt, x, y, w, h);
            if (color.a > 0f || raycast)
            {
                Image img = rt.gameObject.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = raycast;
                if (radius > 0)
                {
                    UiTheme.Round(img, radius);
                }
            }
            return rt;
        }

        public static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        public static void Fill(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        public static Text Label(Transform parent, string content, int size, Color color, TextAnchor align,
            float x, float y, float w, float h)
        {
            RectTransform rt = Node(parent, "Txt");
            Place(rt, x, y, w, h);
            // 先保持未激活，等字体等属性设好再启用：
            // 其它 UI 插件（XUnity.AutoTranslator）会在 Text 启用的瞬间读取 font，此时 font 还是 null。
            rt.gameObject.SetActive(false);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = UiTheme.Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = content;
            rt.gameObject.SetActive(true);
            return t;
        }

        /// <summary>单行文字（超出裁切，不换行）。</summary>
        public static Text LabelClip(Transform parent, string content, int size, Color color, TextAnchor align,
            float x, float y, float w, float h)
        {
            Text t = Label(parent, content, size, color, align, x, y, w, h);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        public static Button Btn(Transform parent, string label, int fontSize, Color bg, Color fg,
            float x, float y, float w, float h, Action onClick, int radius = UiTheme.RadiusChip)
        {
            RectTransform rt = Box(parent, "Btn_" + label, x, y, w, h, bg, radius, true);
            Button b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = rt.GetComponent<Image>();
            UiTheme.Feedback(b);
            if (onClick != null)
            {
                b.onClick.AddListener(() =>
                {
                    try
                    {
                        onClick();
                    }
                    catch (Exception ex)
                    {
                        SceneQuickPlugin.Log.LogWarning("button '" + label + "' failed: " + ex.Message);
                    }
                });
            }
            if (!string.IsNullOrEmpty(label))
            {
                Label(rt, label, fontSize, fg, TextAnchor.MiddleCenter, 0f, 0f, w, h);
            }
            return b;
        }

        /// <summary>只有点击区域、没有底色的按钮（用于整行点击）。</summary>
        public static Button Hit(RectTransform rt, Action onClick)
        {
            Button b = rt.gameObject.GetComponent<Button>();
            if (b == null)
            {
                b = rt.gameObject.AddComponent<Button>();
            }
            Image img = rt.GetComponent<Image>();
            if (img != null)
            {
                b.targetGraphic = img;
            }
            b.transition = Selectable.Transition.None;
            if (onClick != null)
            {
                b.onClick.AddListener(() =>
                {
                    try
                    {
                        onClick();
                    }
                    catch (Exception ex)
                    {
                        SceneQuickPlugin.Log.LogWarning("row click failed: " + ex.Message);
                    }
                });
            }
            return b;
        }

        public static void AddScroll(Transform target, Action<float> onWheel)
        {
            ScrollCatcher sc = target.gameObject.GetComponent<ScrollCatcher>();
            if (sc == null)
            {
                sc = target.gameObject.AddComponent<ScrollCatcher>();
            }
            sc.OnWheel = onWheel;
        }

        public static void AddRightClick(Transform target, Action onRight)
        {
            RightClickCatcher rc = target.gameObject.GetComponent<RightClickCatcher>();
            if (rc == null)
            {
                rc = target.gameObject.AddComponent<RightClickCatcher>();
            }
            rc.OnRight = onRight;
        }
    }

    /// <summary>滚轮转发。</summary>
    public class ScrollCatcher : MonoBehaviour, IScrollHandler
    {
        public Action<float> OnWheel;

        public void OnScroll(PointerEventData eventData)
        {
            if (OnWheel == null || eventData == null)
            {
                return;
            }
            try
            {
                OnWheel(eventData.scrollDelta.y);
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>右键点击转发。</summary>
    public class RightClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        public Action OnRight;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (OnRight == null || eventData == null || eventData.button != PointerEventData.InputButton.Right)
            {
                return;
            }
            try
            {
                OnRight();
            }
            catch (Exception)
            {
            }
        }
    }
}
