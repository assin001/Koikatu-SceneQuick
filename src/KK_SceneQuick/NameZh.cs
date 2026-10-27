using System;
using System.Collections.Generic;
using System.Text;

namespace KK_SceneQuick
{
    /// <summary>场景物件名 → 中文的内置词表（原生物件名大多是日文 / 英文）。</summary>
    internal static class NameZh
    {
        private static readonly string[] ExactRows = new string[]
        {
            "postprocessing=后处理", "post processing=后处理", "post-processing=后处理", "postprocess=后处理",
            "color correction=色彩校正", "colorcorrection=色彩校正", "color grading=调色", "colorgrading=调色",
            "color gradation=调色", "colorgradation=调色", "depth of field=景深", "depth of field (z)=景深 (Z)",
            "depthoffield=景深", "contrast enhancement=对比度增强", "contrastenhencement=对比度增强",
            "ambient occlusion=环境光遮蔽", "screen space ambient occlusion=屏幕空间环境光遮蔽",
            "sun shafts=太阳光束", "sunshafts=太阳光束", "bloom and flares=泛光", "bloomandflares=泛光",
            "vignette and chromatic aberration=暗角与色差", "vignette=暗角", "global fog=雾", "globalfog=雾",
            "cold night=寒冷夜晚", "warm night=温暖夜晚", "diffusion=散射", "fade in=淡入", "fadein=淡入",
            "fade out=淡出", "fadeout=淡出", "lut=滤镜", "filter=滤镜", "camera=相机", "camera lighting=相机灯光",
            "map light=地图灯光", "character light=角色灯光", "skybox=天空盒", "sky box=天空盒",
            "screen effect=屏幕效果", "screen effects=屏幕效果", "post effect=后处理效果", "post effects=后处理效果"
        };

        private static readonly string[] WordRows = new string[]
        {
            "bloom=泛光", "depth=景深", "dof=景深", "fog=雾", "sun=太阳", "shaft=光束", "shafts=光束",
            "shadow=阴影", "light=光", "lighting=灯光", "exposure=曝光", "contrast=对比度", "saturation=饱和度",
            "brightness=亮度", "gamma=伽马", "tone=色调", "color=颜色", "colours=颜色", "hue=色相",
            "grading=调色", "gradation=调色", "correction=校正", "filter=滤镜", "effect=效果", "effects=效果",
            "blur=模糊", "noise=噪点", "grain=颗粒", "flare=光晕", "glow=辉光", "halo=光环", "outline=描边",
            "toon=卡通", "cel=赛璐璐", "anime=动画", "shader=着色器", "material=材质", "texture=贴图",
            "mesh=网格", "model=模型", "prefab=预设", "particle=粒子", "particles=粒子", "smoke=烟",
            "fire=火焰", "flame=火焰", "spark=火花", "dust=尘埃", "mist=薄雾", "steam=蒸汽", "bubble=气泡",
            "water=水", "rain=雨", "snow=雪", "leaves=叶子", "leaf=叶子", "petal=花瓣", "lamp=灯",
            "lantern=灯笼", "candle=蜡烛", "candles=蜡烛", "spotlight=聚光灯", "pointlight=点光源",
            "moon=月亮", "star=星星", "stars=星星", "sky=天空", "cloud=云", "clouds=云", "night=夜晚",
            "day=白天", "evening=傍晚", "morning=早晨", "cold=寒冷", "warm=温暖", "hot=炎热", "dark=黑暗",
            "bright=明亮", "soft=柔和", "wood=木头", "wooden=木制", "metal=金属", "glass=玻璃",
            "plastic=塑料", "fabric=布料", "cloth=布", "paper=纸", "leather=皮革", "ceramic=陶瓷",
            "concrete=混凝土", "brick=砖", "marble=大理石", "stone=石头", "rock=石头", "roof=屋顶",
            "wall=墙壁", "wall2=墙壁", "floor=地板", "ceiling=天花板", "door=门", "window=窗户", "gate=大门",
            "stairs=楼梯", "pillar=柱子", "column=柱子", "beam=横梁", "fence=栅栏", "railing=栏杆",
            "bridge=桥", "road=道路", "path=小径", "grass=草地", "tree=树", "branch=树枝", "flower=花",
            "flowers=花", "plant=植物", "pot=花盆", "vase=花瓶", "chair=椅子", "stool=凳子", "table=桌子",
            "desk=书桌", "bed=床", "sofa=沙发", "cushion=靠垫", "pillow=枕头", "carpet=地毯",
            "curtain=窗帘", "shelf=架子", "cabinet=柜子", "drawer=抽屉", "mirror=镜子", "picture=画",
            "frame=画框", "screen=屏风", "banner=横幅", "flag=旗", "sign=标志", "poster=海报",
            "book=书", "books=书", "box=箱子", "barrel=木桶", "basket=篮子", "ladder=梯子", "rope=绳子",
            "chain=锁链", "sword=剑", "knife=刀", "gun=枪", "shield=盾牌", "hat=帽子", "helmet=头盔",
            "mask=面具", "glasses=眼镜", "shoes=鞋", "dress=连衣裙", "skirt=裙子", "shirt=衬衫",
            "hair=头发", "tail=尾巴", "wing=翅膀", "horn=角", "statue=雕像", "fountain=喷泉", "stage=舞台",
            "garden=庭园", "furniture=家具", "decor=装饰", "decoration=装饰", "ceremony=仪式",
            "festival=祭典", "shrine=神社", "house=房子", "room=房间", "building=建筑", "school=学校",
            "japanese=日式", "modern=现代", "old=旧", "broken=破损", "torn=破损", "small=小", "large=大",
            "big=大", "long=长", "short=短", "thin=薄", "thick=厚", "round=圆", "square=方", "red=红",
            "blue=蓝", "green=绿", "white=白", "black=黑", "gray=灰", "grey=灰", "gold=金", "golden=金色",
            "silver=银", "brown=棕", "pink=粉", "purple=紫", "yellow=黄", "orange=橙", "clear=透明",
            "main=主", "sub=副", "base=基础", "default=默认", "simple=简单", "complex=复杂", "scene=场景",
            "object=物体", "item=物品", "items=物品", "unit=单位", "cube=立方体", "sphere=球体",
            "plane=平面", "quad=四边形", "line=线", "graph=图表", "text=文字", "title=标题", "logo=标志",
            "icon=图标", "ambient=环境", "lights=灯", "direction=方向", "point=点", "stand=台", "case=箱",
            "mat=垫", "board=板", "panel=板", "wave=波", "splash=溅", "blood=血", "slime=史莱姆",
            "fluid=液体", "liquid=液体"
        };

        private static readonly Dictionary<string, string> Exact = Build(ExactRows);
        private static readonly Dictionary<string, string> Word = Build(WordRows);

        private static Dictionary<string, string> Build(string[] rows)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.Ordinal);
            if (rows == null)
            {
                return d;
            }
            for (int i = 0; i < rows.Length; i++)
            {
                int idx = rows[i].IndexOf('=');
                if (idx > 0)
                {
                    d[rows[i].Substring(0, idx)] = rows[i].Substring(idx + 1);
                }
            }
            return d;
        }

        /// <summary>把物件名翻成中文；翻不出来返回空串。</summary>
        public static string Translate(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }
            if (HasCjk(name))
            {
                return string.Empty;
            }
            string text = name.Trim();
            int slash = text.LastIndexOf('/');
            if (slash >= 0 && slash < text.Length - 1)
            {
                text = text.Substring(slash + 1);
            }
            if (text.Length == 0)
            {
                return string.Empty;
            }
            string hit;
            if (Exact.TryGetValue(text.ToLowerInvariant(), out hit))
            {
                return hit;
            }
            string[] words = SplitWords(text).Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> parts = new List<string>();
            bool any = false;
            for (int i = 0; i < words.Length; i++)
            {
                string low = words[i].ToLowerInvariant();
                if (i + 1 < words.Length)
                {
                    string two = low + " " + words[i + 1].ToLowerInvariant();
                    if (Exact.TryGetValue(two, out hit) || Word.TryGetValue(two, out hit))
                    {
                        parts.Add(hit);
                        any = true;
                        i++;
                        continue;
                    }
                }
                if (Word.TryGetValue(low, out hit))
                {
                    parts.Add(hit);
                    any = true;
                    continue;
                }
                if (IsDigits(words[i]))
                {
                    parts.Add(words[i]);
                    continue;
                }
                int cut = low.Length;
                while (cut > 0 && char.IsDigit(low[cut - 1]))
                {
                    cut--;
                }
                if (cut > 0 && cut < low.Length && Word.TryGetValue(low.Substring(0, cut), out hit))
                {
                    parts.Add(hit + words[i].Substring(cut));
                    any = true;
                }
                else
                {
                    parts.Add(words[i]);
                }
            }
            return any ? string.Concat(parts.ToArray()) : string.Empty;
        }

        /// <summary>把结尾的编号去掉，用于同名合并：「椅子1 / 椅子2」→「椅子」。</summary>
        public static string NormName(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "?";
            }
            string text = s.Trim();
            while (text.Length > 0)
            {
                char c = text[text.Length - 1];
                bool digit = (c >= '0' && c <= '9') || (c >= '０' && c <= '９');
                bool sep = c == ' ' || c == '_' || c == '-' || c == '.';
                if (!(digit || sep))
                {
                    break;
                }
                text = text.Substring(0, text.Length - 1);
            }
            return text.Length == 0 ? s.Trim() : text.Trim();
        }

        private static bool IsDigits(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }
            for (int i = 0; i < s.Length; i++)
            {
                if (!char.IsDigit(s[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool HasCjk(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] >= '\u4e00' && s[i] <= '\u9fff')
                {
                    return true;
                }
            }
            return false;
        }

        private static string SplitWords(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '_' || c == '-' || c == '/' || c == '.')
                {
                    sb.Append(' ');
                    continue;
                }
                if (i > 0 && char.IsUpper(c))
                {
                    char prev = s[i - 1];
                    if (char.IsLower(prev) || char.IsDigit(prev))
                    {
                        sb.Append(' ');
                    }
                    else if (char.IsUpper(prev) && i + 1 < s.Length && char.IsLower(s[i + 1]))
                    {
                        sb.Append(' ');
                    }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static string ShortMod(string manifest)
        {
            if (string.IsNullOrEmpty(manifest))
            {
                return string.Empty;
            }
            string text = manifest;
            if (text.StartsWith("Sideloader/", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(11);
            }
            int dot = text.LastIndexOf('.');
            if (dot > 0 && text.Length > 24)
            {
                text = text.Substring(0, 20) + "…";
            }
            return text;
        }
    }
}
