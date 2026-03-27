// ============================================================
// 文件名：UIStyleKit.cs
// 功能描述：UI样式工具包 — 提供精美的UI元素创建方法
//          渐变背景、圆角面板、装饰边框、按钮交互态等
//          替代原始的纯色块UI，让界面达到商业品质
// 创建时间：2026-03-27
// 所属模块：Battle/Visual
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Visual

{
    /// <summary>
    /// UI样式工具包 — 静态工具类
    /// 
    /// 提供：
    /// 1. 渐变纹理生成（水平/垂直/径向）
    /// 2. 圆角矩形纹理生成
    /// 3. 精美面板创建（带边框+渐变背景）
    /// 4. 精美按钮创建（带hover/pressed状态）
    /// 5. 装饰性分隔线
    /// 6. 统一的颜色主题
    /// </summary>
    public static class UIStyleKit
    {
        // ========== 颜色主题 ==========

        /// <summary>主色调 — 深蓝紫（面板背景）</summary>
        public static readonly Color PanelBgDark = new Color(0.08f, 0.08f, 0.16f, 0.95f);
        public static readonly Color PanelBgLight = new Color(0.12f, 0.14f, 0.25f, 0.92f);

        /// <summary>边框色 — 金色系</summary>
        public static readonly Color BorderGold = new Color(0.85f, 0.65f, 0.25f, 0.8f);
        public static readonly Color BorderSilver = new Color(0.6f, 0.65f, 0.75f, 0.6f);

        /// <summary>按钮色 — 绿色系（正常态）</summary>
        public static readonly Color BtnGreenNormal = new Color(0.18f, 0.45f, 0.22f, 1f);
        public static readonly Color BtnGreenHover = new Color(0.25f, 0.58f, 0.30f, 1f);
        public static readonly Color BtnGreenPressed = new Color(0.12f, 0.35f, 0.16f, 1f);

        /// <summary>按钮色 — 红色系（危险操作）</summary>
        public static readonly Color BtnRedNormal = new Color(0.55f, 0.18f, 0.15f, 1f);
        public static readonly Color BtnRedHover = new Color(0.70f, 0.25f, 0.20f, 1f);
        public static readonly Color BtnRedPressed = new Color(0.40f, 0.12f, 0.10f, 1f);

        /// <summary>按钮色 — 蓝色系（信息操作）</summary>
        public static readonly Color BtnBlueNormal = new Color(0.15f, 0.30f, 0.55f, 1f);
        public static readonly Color BtnBlueHover = new Color(0.22f, 0.40f, 0.68f, 1f);
        public static readonly Color BtnBluePressed = new Color(0.10f, 0.22f, 0.42f, 1f);

        /// <summary>按钮色 — 灰色系（次要操作）</summary>
        public static readonly Color BtnGrayNormal = new Color(0.28f, 0.30f, 0.35f, 1f);
        public static readonly Color BtnGrayHover = new Color(0.38f, 0.40f, 0.45f, 1f);
        public static readonly Color BtnGrayPressed = new Color(0.20f, 0.22f, 0.26f, 1f);

        /// <summary>塔卡片背景色</summary>
        public static readonly Color TowerCardBg = new Color(0.10f, 0.12f, 0.22f, 0.95f);
        public static readonly Color TowerCardBgSelected = new Color(0.15f, 0.35f, 0.20f, 0.98f);
        public static readonly Color TowerCardBgDisabled = new Color(0.15f, 0.12f, 0.12f, 0.85f);

        /// <summary>文字颜色</summary>
        public static readonly Color TextGold = new Color(1f, 0.85f, 0.35f);
        public static readonly Color TextWhite = new Color(0.95f, 0.95f, 0.97f);
        public static readonly Color TextGray = new Color(0.65f, 0.65f, 0.70f);
        public static readonly Color TextGreen = new Color(0.45f, 0.90f, 0.45f);
        public static readonly Color TextRed = new Color(1f, 0.40f, 0.35f);
        public static readonly Color TextHP = new Color(1f, 0.55f, 0.55f);

        // ========== 纹理缓存 ==========

        private static Texture2D _roundedRectTex;
        private static Texture2D _gradientHorizontalTex;
        private static Texture2D _gradientVerticalTex;
        private static Texture2D _borderTex;
        private static Texture2D _glowTex;

        // ========== 1. 圆角矩形纹理 ==========

        /// <summary>
        /// 生成圆角矩形纹理
        /// </summary>
        public static Texture2D GetRoundedRectTexture(int width = 64, int height = 64, int radius = 12,
            Color fillColor = default, Color borderColor = default, int borderWidth = 2)
        {
            if (fillColor == default) fillColor = PanelBgDark;
            if (borderColor == default) borderColor = BorderSilver;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dist = DistanceToRoundedRect(x, y, width, height, radius);

                    if (dist < -borderWidth)
                    {
                        // 内部填充 — 带轻微垂直渐变
                        float gradientT = (float)y / height;
                        Color fill = Color.Lerp(fillColor, fillColor * 1.15f, gradientT * 0.3f);
                        pixels[y * width + x] = fill;
                    }
                    else if (dist < 0)
                    {
                        // 边框区域
                        float t = Mathf.Clamp01((dist + borderWidth) / borderWidth);
                        pixels[y * width + x] = Color.Lerp(fillColor, borderColor, t * 0.8f);
                    }
                    else if (dist < 1.5f)
                    {
                        // 抗锯齿边缘
                        float alpha = 1f - Mathf.Clamp01(dist / 1.5f);
                        Color c = borderColor;
                        c.a *= alpha;
                        pixels[y * width + x] = c;
                    }
                    else
                    {
                        pixels[y * width + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>计算点到圆角矩形边缘的有符号距离</summary>
        private static float DistanceToRoundedRect(int x, int y, int w, int h, int r)
        {
            // 将坐标映射到以矩形中心为原点
            float px = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
            float py = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);

            if (px <= 0 && py <= 0)
                return Mathf.Max(px, py) - r; // 内部
            else if (px <= 0)
                return py - r;
            else if (py <= 0)
                return px - r;
            else
                return Mathf.Sqrt(px * px + py * py) - r;
        }

        // ========== 2. 渐变纹理 ==========

        /// <summary>生成垂直渐变纹理（从下到上）</summary>
        public static Texture2D CreateVerticalGradient(Color bottom, Color top, int height = 64)
        {
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                tex.SetPixel(0, y, Color.Lerp(bottom, top, t));
            }

            tex.Apply();
            return tex;
        }

        /// <summary>生成水平渐变纹理（从左到右）</summary>
        public static Texture2D CreateHorizontalGradient(Color left, Color right, int width = 64)
        {
            var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int x = 0; x < width; x++)
            {
                float t = (float)x / (width - 1);
                tex.SetPixel(x, 0, Color.Lerp(left, right, t));
            }

            tex.Apply();
            return tex;
        }

        // ========== 3. 发光/光晕纹理 ==========

        /// <summary>生成柔和发光纹理（用于选中态高亮）</summary>
        public static Texture2D CreateGlowTexture(int size = 64, Color glowColor = default)
        {
            if (glowColor == default) glowColor = new Color(0.4f, 0.8f, 0.4f, 0.5f);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxDist = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist), 2f) * glowColor.a;
                    tex.SetPixel(x, y, new Color(glowColor.r, glowColor.g, glowColor.b, alpha));
                }
            }

            tex.Apply();
            return tex;
        }

        // ========== 4. 精美面板创建 ==========

        /// <summary>
        /// 创建精美面板（圆角+边框+渐变背景）
        /// </summary>
        public static Image CreateStyledPanel(RectTransform rect, Color bgColor = default,
            Color borderColor = default, int cornerRadius = 12, int borderWidth = 2)
        {
            if (bgColor == default) bgColor = PanelBgDark;
            if (borderColor == default) borderColor = BorderSilver;

            var tex = GetRoundedRectTexture(128, 128, cornerRadius, bgColor, borderColor, borderWidth);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(cornerRadius + 4, cornerRadius + 4, cornerRadius + 4, cornerRadius + 4));

            var img = rect.gameObject.GetComponent<Image>();
            if (img == null) img = rect.gameObject.AddComponent<Image>();

            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = true;

            Logger.D("UIStyleKit", "CreateStyledPanel完成: obj={0}, active={1}, img.enabled={2}",
                rect.gameObject.name, rect.gameObject.activeInHierarchy, img.enabled);
            return img;
        }


        /// <summary>
        /// 创建渐变背景面板（不带圆角，用于顶部/底部栏）
        /// </summary>
        public static Image CreateGradientPanel(RectTransform rect, Color bottom, Color top)
        {
            var tex = CreateVerticalGradient(bottom, top, 128);
            Logger.D("UIStyleKit", "CreateGradientPanel: tex={0}, size={1}x{2}", tex != null, tex?.width, tex?.height);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, tex.height), new Vector2(0.5f, 0.5f));
            Logger.D("UIStyleKit", "CreateGradientPanel: sprite={0}, rect={1}", sprite != null, sprite?.rect);

            var img = rect.gameObject.GetComponent<Image>();
            if (img == null) img = rect.gameObject.AddComponent<Image>();

            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.white;
            img.raycastTarget = true;

            Logger.D("UIStyleKit", "CreateGradientPanel完成: obj={0}, active={1}, img.enabled={2}, rectSize={3}",
                rect.gameObject.name, rect.gameObject.activeInHierarchy, img.enabled, rect.sizeDelta);
            return img;
        }



        // ========== 5. 精美按钮样式 ==========

        /// <summary>
        /// 为按钮设置完整的交互颜色状态
        /// </summary>
        public static void StyleButton(Button btn, Color normal, Color hover, Color pressed,
            Color disabled = default)
        {
            if (disabled == default) disabled = new Color(0.25f, 0.25f, 0.28f, 0.7f);

            var colors = btn.colors;
            colors.normalColor = normal;
            colors.highlightedColor = hover;
            colors.pressedColor = pressed;
            colors.selectedColor = hover;
            colors.disabledColor = disabled;
            colors.fadeDuration = 0.1f;
            colors.colorMultiplier = 1f;
            btn.colors = colors;

            // 添加圆角背景
            var img = btn.targetGraphic as Image;
            if (img != null)
            {
                var tex = GetRoundedRectTexture(64, 64, 8, Color.white, new Color(1, 1, 1, 0.3f), 1);
                var sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white; // 让btn.colors控制颜色，避免双重乘算
            }

        }

        /// <summary>快捷方法：绿色按钮样式</summary>
        public static void StyleGreenButton(Button btn)
        {
            StyleButton(btn, BtnGreenNormal, BtnGreenHover, BtnGreenPressed);
        }

        /// <summary>快捷方法：红色按钮样式</summary>
        public static void StyleRedButton(Button btn)
        {
            StyleButton(btn, BtnRedNormal, BtnRedHover, BtnRedPressed);
        }

        /// <summary>快捷方法：蓝色按钮样式</summary>
        public static void StyleBlueButton(Button btn)
        {
            StyleButton(btn, BtnBlueNormal, BtnBlueHover, BtnBluePressed);
        }

        /// <summary>快捷方法：灰色按钮样式</summary>
        public static void StyleGrayButton(Button btn)
        {
            StyleButton(btn, BtnGrayNormal, BtnGrayHover, BtnGrayPressed);
        }

        // ========== 6. 装饰元素 ==========

        /// <summary>创建水平分隔线</summary>
        public static RectTransform CreateSeparator(RectTransform parent, float yAnchor,
            Color color = default, float thickness = 1.5f)
        {
            if (color == default) color = new Color(0.5f, 0.5f, 0.6f, 0.3f);

            var obj = new GameObject("Separator");
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, yAnchor);
            rect.anchorMax = new Vector2(0.95f, yAnchor);
            rect.sizeDelta = new Vector2(0, thickness);
            rect.anchoredPosition = Vector2.zero;

            var img = obj.AddComponent<Image>();
            var gradTex = CreateHorizontalGradient(Color.clear, color);
            img.sprite = Sprite.Create(gradTex, new Rect(0, 0, gradTex.width, 1), new Vector2(0.5f, 0.5f));
            img.color = Color.white;
            img.raycastTarget = false;

            return rect;
        }

        /// <summary>创建装饰性角标（用于面板四角）</summary>
        public static void AddCornerDecorations(RectTransform panel, Color color = default)
        {
            if (color == default) color = BorderGold;

            // 四个角各添加一个小L形装饰
            string[] names = { "CornerTL", "CornerTR", "CornerBL", "CornerBR" };
            Vector2[] anchors = {
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, 0), new Vector2(1, 0)
            };
            Vector2[] pivots = {
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, 0), new Vector2(1, 0)
            };

            float cornerSize = 16f;
            float lineWidth = 2f;

            for (int i = 0; i < 4; i++)
            {
                // 水平线
                var hObj = new GameObject($"{names[i]}_H");
                hObj.transform.SetParent(panel, false);
                var hRect = hObj.AddComponent<RectTransform>();
                hRect.anchorMin = hRect.anchorMax = anchors[i];
                hRect.pivot = pivots[i];
                hRect.sizeDelta = new Vector2(cornerSize, lineWidth);
                hRect.anchoredPosition = Vector2.zero;
                var hImg = hObj.AddComponent<Image>();
                hImg.color = color;
                hImg.raycastTarget = false;

                // 垂直线
                var vObj = new GameObject($"{names[i]}_V");
                vObj.transform.SetParent(panel, false);
                var vRect = vObj.AddComponent<RectTransform>();
                vRect.anchorMin = vRect.anchorMax = anchors[i];
                vRect.pivot = pivots[i];
                vRect.sizeDelta = new Vector2(lineWidth, cornerSize);
                vRect.anchoredPosition = Vector2.zero;
                var vImg = vObj.AddComponent<Image>();
                vImg.color = color;
                vImg.raycastTarget = false;
            }
        }

        // ========== 7. 文字阴影效果 ==========

        /// <summary>为Text添加阴影效果</summary>
        public static Shadow AddTextShadow(Text text, Color shadowColor = default, Vector2 offset = default)
        {
            if (shadowColor == default) shadowColor = new Color(0, 0, 0, 0.6f);
            if (offset == default) offset = new Vector2(1.5f, -1.5f);

            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = shadowColor;
            shadow.effectDistance = offset;
            return shadow;
        }

        /// <summary>为Text添加描边效果</summary>
        public static Outline AddTextOutline(Text text, Color outlineColor = default, Vector2 distance = default)
        {
            if (outlineColor == default) outlineColor = new Color(0, 0, 0, 0.8f);
            if (distance == default) distance = new Vector2(1f, 1f);

            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = distance;
            return outline;
        }
    }
}
