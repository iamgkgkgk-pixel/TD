// ============================================================
// 文件名：UIAtlasGenerator.cs
// 功能描述：运行时生成高品质UI底图纹理
//   面板底（半透明黑+圆角+内发光+边框）
//   按钮底（渐变色+圆角+高光条+投影）
//   标签栏底（毛玻璃效果模拟）
//   货币胶囊（圆角胶囊+内发光）
//   所有纹理足够大（256~512px），缩放后不锯齿
// 创建时间：2026-03-29
// 所属模块：MetaGame
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace AetheraSurvivors.MetaGame
{
    public static class UIAtlasGenerator
    {
        private static Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        /// <summary>获取面板底图（深色半透明+圆角+内发光+边框）</summary>
        public static Sprite GetPanel(string key = "default")
        {
            string cacheKey = $"panel_{key}";
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

            int w = 128, h = 128;
            int r = 24; // 圆角半径
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dist = RoundedRectSDF(x, y, w, h, r);

                if (dist > 1f) // 外部
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    // 背景渐变（上深下更深）
                    float ny = (float)y / h;
                    Color bg = Color.Lerp(
                        new Color(0.04f, 0.04f, 0.10f, 0.92f),
                        new Color(0.02f, 0.02f, 0.06f, 0.95f),
                        1f - ny);

                    // 内发光（边缘附近亮一圈）
                    float innerGlow = Mathf.Clamp01((dist - 0.7f) / 0.3f) * 0.15f;
                    bg.r += innerGlow * 0.4f;
                    bg.g += innerGlow * 0.5f;
                    bg.b += innerGlow * 0.8f;

                    // 边框（最外1~3像素金色边）
                    float borderMask = Mathf.Clamp01((dist - 0.85f) / 0.15f);
                    Color border = new Color(0.7f, 0.55f, 0.2f, 0.6f);
                    bg = Color.Lerp(bg, border, borderMask);

                    // 抗锯齿（边缘1px渐变透明）
                    float edgeAA = Mathf.Clamp01((1f - dist) * (r * 0.5f));
                    bg.a *= edgeAA;

                    tex.SetPixel(x, y, bg);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(r + 4, r + 4, r + 4, r + 4));
            _cache[cacheKey] = sprite;
            return sprite;
        }

        /// <summary>获取按钮底图（渐变+圆角+高光条+底部投影）</summary>
        public static Sprite GetButton(Color topColor, Color bottomColor, string key = "")
        {
            string cacheKey = $"btn_{key}_{ColorToHex(topColor)}";
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

            int w = 256, h = 80;
            int r = 18;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dist = RoundedRectSDF(x, y, w, h, r);

                if (dist > 1f)
                {
                    // 底部投影（按钮外下方2~4像素淡黑）
                    float shadowDist = RoundedRectSDF(x, y + 3, w, h, r);
                    float shadowAlpha = Mathf.Clamp01(1f - shadowDist) * 0.25f;
                    tex.SetPixel(x, y, new Color(0, 0, 0, shadowAlpha));
                }
                else
                {
                    float ny = (float)y / h;

                    // 上下渐变
                    Color bg = Color.Lerp(bottomColor, topColor, ny);

                    // 顶部高光条（y在80%~95%之间，淡白）
                    float highlightMask = Mathf.Clamp01((ny - 0.75f) / 0.15f) * Mathf.Clamp01((0.95f - ny) / 0.05f);
                    float highlightX = 1f - Mathf.Abs(((float)x / w) - 0.5f) * 2f; // 中间亮边缘暗
                    highlightX = Mathf.Clamp01(highlightX * 1.5f);
                    bg.r += highlightMask * highlightX * 0.25f;
                    bg.g += highlightMask * highlightX * 0.25f;
                    bg.b += highlightMask * highlightX * 0.25f;

                    // 边框（金色1px）
                    float borderMask = Mathf.Clamp01((dist - 0.88f) / 0.12f);
                    Color border = new Color(0.85f, 0.7f, 0.3f, 0.5f);
                    bg = Color.Lerp(bg, border, borderMask * 0.6f);

                    // 抗锯齿
                    float edgeAA = Mathf.Clamp01((1f - dist) * (r * 0.5f));
                    bg.a *= edgeAA;

                    tex.SetPixel(x, y, bg);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(r + 6, r + 6, r + 6, r + 6));
            _cache[cacheKey] = sprite;
            return sprite;
        }

        /// <summary>获取CTA大按钮底图（更华丽的渐变+更强的光泽）</summary>
        public static Sprite GetCTAButton()
        {
            string cacheKey = "cta_button";
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

            int w = 512, h = 96;
            int r = 22;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dist = RoundedRectSDF(x, y, w, h, r);

                if (dist > 1f)
                {
                    float shadowDist = RoundedRectSDF(x, y + 4, w, h, r);
                    float sa = Mathf.Clamp01(1f - shadowDist) * 0.35f;
                    tex.SetPixel(x, y, new Color(0, 0, 0, sa));
                }
                else
                {
                    float ny = (float)y / h;
                    float nx = (float)x / w;

                    // 绿色渐变
                    Color bg = Color.Lerp(
                        new Color(0.10f, 0.40f, 0.10f, 1f),
                        new Color(0.15f, 0.60f, 0.18f, 1f),
                        ny);

                    // 顶部大范围高光
                    float hl = Mathf.Clamp01((ny - 0.6f) / 0.3f);
                    float hlX = 1f - Mathf.Pow(Mathf.Abs(nx - 0.5f) * 2f, 2f);
                    bg.r += hl * hlX * 0.20f;
                    bg.g += hl * hlX * 0.30f;
                    bg.b += hl * hlX * 0.12f;

                    // 金色边框（2px）
                    float bm = Mathf.Clamp01((dist - 0.90f) / 0.10f);
                    Color border = new Color(1f, 0.82f, 0.3f, 0.8f);
                    bg = Color.Lerp(bg, border, bm);

                    // 抗锯齿
                    float aa = Mathf.Clamp01((1f - dist) * (r * 0.5f));
                    bg.a *= aa;

                    tex.SetPixel(x, y, bg);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(r + 8, r + 8, r + 8, r + 8));
            _cache[cacheKey] = sprite;
            return sprite;
        }

        /// <summary>获取货币胶囊底图</summary>
        public static Sprite GetCapsule(Color bgColor)
        {
            string cacheKey = $"capsule_{ColorToHex(bgColor)}";
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

            int w = 128, h = 40;
            int r = 20; // 全圆角=胶囊
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dist = RoundedRectSDF(x, y, w, h, r);
                if (dist > 1f)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    float ny = (float)y / h;
                    Color bg = bgColor;
                    // 顶部微亮
                    bg.r += Mathf.Clamp01(ny - 0.6f) * 0.1f;
                    bg.g += Mathf.Clamp01(ny - 0.6f) * 0.1f;
                    bg.b += Mathf.Clamp01(ny - 0.6f) * 0.1f;
                    // 边框
                    float bm = Mathf.Clamp01((dist - 0.85f) / 0.15f);
                    bg = Color.Lerp(bg, new Color(0.6f, 0.6f, 0.8f, 0.3f), bm);
                    float aa = Mathf.Clamp01((1f - dist) * (r * 0.5f));
                    bg.a *= aa;
                    tex.SetPixel(x, y, bg);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            _cache[cacheKey] = sprite;
            return sprite;
        }

        /// <summary>获取底部标签栏底图</summary>
        public static Sprite GetTabBar()
        {
            string cacheKey = "tab_bar";
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

            int w = 64, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float ny = (float)y / h;
                // 顶部有一条金线，然后是半透明黑
                float topLine = Mathf.Clamp01((ny - 0.92f) / 0.06f);
                Color bg = new Color(0.02f, 0.02f, 0.06f, 0.92f);
                Color gold = new Color(0.6f, 0.5f, 0.2f, 0.5f);
                bg = Color.Lerp(bg, gold, topLine);
                tex.SetPixel(x, y, bg);
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
            _cache[cacheKey] = sprite;
            return sprite;
        }

        /// <summary>获取圆形按钮底图</summary>
        public static Sprite GetCircleButton(Color bgColor)
        {
            string cacheKey = $"circle_{ColorToHex(bgColor)}";
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

            int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size / 2f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                if (d > 1f)
                {
                    // 阴影
                    float sd = Vector2.Distance(new Vector2(x, y + 2), new Vector2(half, half)) / half;
                    float sa = Mathf.Clamp01(1f - sd) * 0.2f;
                    tex.SetPixel(x, y, new Color(0, 0, 0, sa));
                }
                else
                {
                    float ny = (float)y / size;
                    Color bg = bgColor;
                    // 顶部高光
                    bg.r += Mathf.Clamp01(ny - 0.5f) * 0.15f;
                    bg.g += Mathf.Clamp01(ny - 0.5f) * 0.15f;
                    bg.b += Mathf.Clamp01(ny - 0.5f) * 0.2f;
                    // 边缘光
                    float rim = Mathf.Clamp01((d - 0.80f) / 0.18f);
                    bg = Color.Lerp(bg, new Color(0.5f, 0.5f, 0.8f, 0.4f), rim);
                    // 抗锯齿
                    float aa = Mathf.Clamp01((1f - d) * (half * 0.4f));
                    bg.a *= aa;
                    tex.SetPixel(x, y, bg);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            _cache[cacheKey] = sprite;
            return sprite;
        }

        // ========== 工具 ==========

        /// <summary>圆角矩形SDF（返回0~1: 内部，>1: 外部）</summary>
        private static float RoundedRectSDF(int x, int y, int w, int h, int radius)
        {
            // 对称化坐标
            float px = Mathf.Abs(x - w / 2f);
            float py = Mathf.Abs(y - h / 2f);
            float hw = w / 2f - radius;
            float hh = h / 2f - radius;

            float dx = Mathf.Max(px - hw, 0);
            float dy = Mathf.Max(py - hh, 0);
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            return dist / radius;
        }

        private static string ColorToHex(Color c)
        {
            return $"{(int)(c.r*255):X2}{(int)(c.g*255):X2}{(int)(c.b*255):X2}{(int)(c.a*255):X2}";
        }
    }
}
