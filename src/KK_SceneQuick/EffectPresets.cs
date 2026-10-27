using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Studio;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KK_SceneQuick
{
    internal sealed class PresetItem
    {
        public int Group;
        public int Category;
        public int No;
        public bool Vis = true;
        public string Name = "";
        public readonly List<string> Fields = new List<string>();
        public bool HasTransform;
        public Vector3 Pos;
        public Vector3 Scale;
        public Vector3 Rot;
    }

    internal sealed class EffPreset
    {
        public string Name = "";
        public long SavedAt;
        public readonly List<PresetItem> Items = new List<PresetItem>();
    }

    /// <summary>效果预设的文本读写（key|value 行格式，可直接手改）。</summary>
    internal static class PresetIO
    {
        internal static string F(float v)
        {
            return v.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static float PF(string s)
        {
            float v;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
        }

        internal static void Load(List<EffPreset> presets, string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }
                string[] lines = File.ReadAllLines(path);
                EffPreset cur = null;
                PresetItem curItem = null;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = (lines[i] ?? "").Trim();
                    if (line.Length == 0)
                    {
                        continue;
                    }
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        cur = new EffPreset();
                        string head = line.Substring(1, line.Length - 2);
                        int bar = head.LastIndexOf('|');
                        long ts;
                        if (bar > 0 && long.TryParse(head.Substring(bar + 1), out ts))
                        {
                            cur.SavedAt = ts;
                            head = head.Substring(0, bar);
                        }
                        cur.Name = head;
                        presets.Add(cur);
                        curItem = null;
                        continue;
                    }
                    if (cur == null)
                    {
                        continue;
                    }
                    string[] p = line.Split('|');
                    if (p.Length >= 6 && p[0] == "I")
                    {
                        PresetItem item = new PresetItem();
                        if (int.TryParse(p[1], out item.Group) && int.TryParse(p[2], out item.Category) &&
                            int.TryParse(p[3], out item.No))
                        {
                            item.Vis = p[4].Trim() == "1";
                            item.Name = p[5];
                            cur.Items.Add(item);
                            curItem = item;
                        }
                    }
                    else if (p.Length >= 12 && p[0] == "T" && curItem != null)
                    {
                        curItem.HasTransform = true;
                        curItem.Pos = new Vector3(PF(p[3]), PF(p[4]), PF(p[5]));
                        curItem.Rot = new Vector3(PF(p[6]), PF(p[7]), PF(p[8]));
                        curItem.Scale = new Vector3(PF(p[9]), PF(p[10]), PF(p[11]));
                    }
                    else if (p.Length >= 6 && p[0] == "P" && curItem != null)
                    {
                        curItem.Fields.Add(p[3] + "|" + p[4] + "|" + p[5]);
                    }
                    else if (p.Length >= 5)
                    {
                        // 旧格式：g|c|n|vis|name
                        PresetItem item = new PresetItem();
                        if (int.TryParse(p[0], out item.Group) && int.TryParse(p[1], out item.Category) &&
                            int.TryParse(p[2], out item.No))
                        {
                            item.Vis = p[3].Trim() == "1";
                            item.Name = p[4];
                            cur.Items.Add(item);
                            curItem = item;
                        }
                    }
                }
                SceneQuickPlugin.Log.LogInfo("已读取效果预设 " + presets.Count + " 个");
            }
            catch (Exception ex)
            {
                SceneQuickPlugin.Log.LogWarning("读取预设失败: " + ex.Message);
            }
        }

        internal static void Save(List<EffPreset> presets, string path)
        {
            try
            {
                List<string> lines = new List<string>();
                for (int i = 0; i < presets.Count; i++)
                {
                    EffPreset p = presets[i];
                    lines.Add("[" + p.Name + "]" + (p.SavedAt > 0 ? "|" + p.SavedAt : ""));
                    for (int j = 0; j < p.Items.Count; j++)
                    {
                        PresetItem it = p.Items[j];
                        lines.Add("I|" + it.Group + "|" + it.Category + "|" + it.No + "|" + (it.Vis ? "1" : "0") + "|" +
                                  (it.Name ?? "?").Replace("|", " "));
                        if (it.HasTransform)
                        {
                            lines.Add("T|" + it.Group + "|" + it.Category + "|" + F(it.Pos.x) + "|" + F(it.Pos.y) + "|" +
                                      F(it.Pos.z) + "|" + F(it.Rot.x) + "|" + F(it.Rot.y) + "|" + F(it.Rot.z) + "|" +
                                      F(it.Scale.x) + "|" + F(it.Scale.y) + "|" + F(it.Scale.z));
                        }
                        for (int k = 0; k < it.Fields.Count; k++)
                        {
                            lines.Add("P|" + it.Group + "|" + it.Category + "|" + it.Fields[k]);
                        }
                    }
                }
                File.WriteAllLines(path, lines.ToArray());
            }
            catch (Exception ex)
            {
                SceneQuickPlugin.Log.LogWarning("保存预设失败: " + ex.Message);
            }
        }

        internal static List<PresetItem> SnapshotSelection(SceneQuickPlugin plugin)
        {
            List<PresetItem> list = new List<PresetItem>();
            for (int i = 0; i < plugin.AllItems.Count; i++)
            {
                ItemRow row = plugin.AllItems[i];
                if (row.Effect && plugin.PresetSel.Contains(row.Key))
                {
                    PresetItem item = SnapshotItem(row);
                    if (item != null)
                    {
                        list.Add(item);
                    }
                }
            }
            return list;
        }

        internal static PresetItem SnapshotItem(ItemRow row)
        {
            OIItemInfo info = row != null && row.Ctrl != null ? row.Ctrl.itemInfo : null;
            if (info == null)
            {
                return null;
            }
            PresetItem item = new PresetItem();
            item.Group = info.group;
            item.Category = info.category;
            item.No = info.no;
            item.Name = row.Name ?? "?";
            item.Vis = SceneQuickPlugin.CurrentVisible(row);
            GameObject go = row.Ctrl != null ? row.Ctrl.objectItem : null;
            if (go != null)
            {
                Transform t = go.transform;
                item.HasTransform = true;
                item.Pos = t.localPosition;
                item.Rot = t.localEulerAngles;
                item.Scale = t.localScale;
                Component[] comps = go.GetComponentsInChildren<Component>(true);
                int count = 0;
                for (int i = 0; i < comps.Length && count < 200; i++)
                {
                    Component c = comps[i];
                    if (c == null)
                    {
                        continue;
                    }
                    string typeName = c.GetType().Name;
                    if (typeName == "Transform" || typeName == "RectTransform")
                    {
                        continue;
                    }
                    FieldInfo[] fields = c.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
                    for (int j = 0; j < fields.Length && count < 200; j++)
                    {
                        string text;
                        try
                        {
                            text = FieldToString(fields[j].FieldType, fields[j].GetValue(c));
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                        if (text != null)
                        {
                            item.Fields.Add(typeName + "|" + fields[j].Name + "|" + text);
                            count++;
                        }
                    }
                }
            }
            return item;
        }

        private static string FieldToString(Type t, object v)
        {
            if (v == null)
            {
                return null;
            }
            if (t == typeof(float))
            {
                return F((float)v);
            }
            if (t == typeof(int))
            {
                return ((int)v).ToString(CultureInfo.InvariantCulture);
            }
            if (t == typeof(bool))
            {
                return (bool)v ? "1" : "0";
            }
            if (t == typeof(Color))
            {
                Color c = (Color)v;
                return F(c.r) + "," + F(c.g) + "," + F(c.b) + "," + F(c.a);
            }
            if (t == typeof(Vector3))
            {
                Vector3 c = (Vector3)v;
                return F(c.x) + "," + F(c.y) + "," + F(c.z);
            }
            if (t == typeof(Vector2))
            {
                Vector2 c = (Vector2)v;
                return F(c.x) + "," + F(c.y);
            }
            return null;
        }

        private static bool StringToField(Type t, string s, out object v)
        {
            v = null;
            try
            {
                string[] a = s.Split(',');
                if (t == typeof(float))
                {
                    v = PF(a[0]);
                    return true;
                }
                if (t == typeof(int))
                {
                    v = (int)PF(a[0]);
                    return true;
                }
                if (t == typeof(bool))
                {
                    v = s.Trim() == "1";
                    return true;
                }
                if (t == typeof(Color) && a.Length >= 4)
                {
                    v = new Color(PF(a[0]), PF(a[1]), PF(a[2]), PF(a[3]));
                    return true;
                }
                if (t == typeof(Vector3) && a.Length >= 3)
                {
                    v = new Vector3(PF(a[0]), PF(a[1]), PF(a[2]));
                    return true;
                }
                if (t == typeof(Vector2) && a.Length >= 2)
                {
                    v = new Vector2(PF(a[0]), PF(a[1]));
                    return true;
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        internal static void ApplySnapshot(OCIItem oi, PresetItem item)
        {
            if (oi == null || item == null)
            {
                return;
            }
            try
            {
                GameObject go = oi.objectItem;
                if (go == null)
                {
                    return;
                }
                if (item.HasTransform)
                {
                    go.transform.localPosition = item.Pos;
                    go.transform.localEulerAngles = item.Rot;
                    go.transform.localScale = item.Scale;
                }
                Component[] comps = go.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < item.Fields.Count; i++)
                {
                    string text = item.Fields[i];
                    int bar1 = text.IndexOf('|');
                    if (bar1 <= 0)
                    {
                        continue;
                    }
                    int bar2 = text.IndexOf('|', bar1 + 1);
                    if (bar2 <= 0)
                    {
                        continue;
                    }
                    string typeName = text.Substring(0, bar1);
                    string fieldName = text.Substring(bar1 + 1, bar2 - bar1 - 1);
                    string value = text.Substring(bar2 + 1);
                    for (int j = 0; j < comps.Length; j++)
                    {
                        Component c = comps[j];
                        if (c == null || c.GetType().Name != typeName)
                        {
                            continue;
                        }
                        FieldInfo field = c.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
                        if (field == null)
                        {
                            continue;
                        }
                        object v;
                        if (StringToField(field.FieldType, value, out v))
                        {
                            try
                            {
                                field.SetValue(c, v);
                            }
                            catch (Exception)
                            {
                            }
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                SceneQuickPlugin.Log.LogWarning("应用预设参数失败: " + ex.Message);
            }
        }

        internal static OCIItem SpawnItem(int group, int category, int no)
        {
            try
            {
                return AddObjectItem.Add(group, category, no);
            }
            catch (Exception ex)
            {
                SceneQuickPlugin.Log.LogWarning("生成效果物件失败: " + ex.Message);
                return null;
            }
        }
    }
}
