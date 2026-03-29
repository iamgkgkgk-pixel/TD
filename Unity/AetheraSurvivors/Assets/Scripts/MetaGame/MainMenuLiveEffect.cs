// ============================================================
// 文件名：MainMenuLiveEffect.cs
// 功能描述：主界面叠加动效 — Shader负责图像形变，此处负责叠加粒子
//   仅保留Shader无法实现的效果：
//   1. 星辰闪烁（顶部天空，Image叠加）
//   2. 萤火虫（树丛飘出上升的光点）
//   3. 太阳额外光晕（叠加在Shader效果之上的呼吸光）
// 创建时间：2026-03-29 v3
// 所属模块：MetaGame
// ============================================================

using UnityEngine;
using UnityEngine.UI;

namespace AetheraSurvivors.MetaGame
{
    public class MainMenuLiveEffect : MonoBehaviour
    {
        // ========== 太阳光晕 ==========
        private Image _sunGlow;

        // ========== 星辰 ==========
        private Image[] _stars;
        private float[] _starPhases;
        private float[] _starSpeeds;
        private float[] _starBaseAlpha;
        private const int STAR_COUNT = 22;

        // ========== 萤火 ==========
        private RectTransform[] _fireflies;
        private Image[] _fireflyImages;
        private float[] _fireflyPhases;
        private Vector2[] _fireflyBase;
        private const int FIREFLY_COUNT = 16;

        public void Initialize(RectTransform parentRect)
        {
            CreateSunGlow(parentRect);
            CreateStars(parentRect);
            CreateFireflies(parentRect);
            Debug.Log("[MainMenuLiveEffect] v3 叠加层初始化完成（星辰+萤火+光晕）");
        }

        // ================================================================
        // 太阳叠加光晕（补充Shader的glow效果）
        // ================================================================

        private void CreateSunGlow(RectTransform parent)
        {
            var obj = new GameObject("SunGlowOverlay");
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.25f, 0.38f);
            rect.anchorMax = new Vector2(0.55f, 0.72f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _sunGlow = obj.AddComponent<Image>();
            _sunGlow.sprite = MakeRadialGradient(64, new Color(1f, 0.92f, 0.70f, 1f));
            _sunGlow.color = new Color(1f, 1f, 1f, 0.15f);
            _sunGlow.raycastTarget = false;
        }

        // ================================================================
        // 星辰闪烁
        // ================================================================

        private void CreateStars(RectTransform parent)
        {
            _stars = new Image[STAR_COUNT];
            _starPhases = new float[STAR_COUNT];
            _starSpeeds = new float[STAR_COUNT];
            _starBaseAlpha = new float[STAR_COUNT];

            var starSprite = MakeRadialGradient(12, Color.white);

            for (int i = 0; i < STAR_COUNT; i++)
            {
                var obj = new GameObject($"Star_{i}");
                obj.transform.SetParent(parent, false);
                var rect = obj.AddComponent<RectTransform>();

                float px, py;
                if (i < 8)       { px = Random.Range(0.02f, 0.28f); py = Random.Range(0.72f, 0.97f); }
                else if (i < 15) { px = Random.Range(0.83f, 0.98f); py = Random.Range(0.78f, 0.97f); }
                else             { px = Random.Range(0.30f, 0.58f); py = Random.Range(0.86f, 0.97f); }

                float size = Random.Range(0.006f, 0.014f);
                rect.anchorMin = new Vector2(px - size * 0.5f, py - size * 0.9f);
                rect.anchorMax = new Vector2(px + size * 0.5f, py + size * 0.9f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var img = obj.AddComponent<Image>();
                img.sprite = starSprite;
                img.raycastTarget = false;

                float temp = Random.Range(0f, 1f);
                img.color = temp < 0.3f ? new Color(0.80f, 0.90f, 1f, 1f) :
                            temp < 0.7f ? Color.white :
                                          new Color(1f, 0.93f, 0.75f, 1f);

                _stars[i] = img;
                _starPhases[i] = Random.Range(0f, Mathf.PI * 2f);
                _starSpeeds[i] = Random.Range(1.5f, 5f);
                _starBaseAlpha[i] = Random.Range(0.4f, 1f);
            }
        }

        // ================================================================
        // 萤火虫
        // ================================================================

        private void CreateFireflies(RectTransform parent)
        {
            _fireflies = new RectTransform[FIREFLY_COUNT];
            _fireflyImages = new Image[FIREFLY_COUNT];
            _fireflyPhases = new float[FIREFLY_COUNT];
            _fireflyBase = new Vector2[FIREFLY_COUNT];

            var ffSprite = MakeRadialGradient(10, new Color(1f, 0.93f, 0.60f, 1f));

            for (int i = 0; i < FIREFLY_COUNT; i++)
            {
                var obj = new GameObject($"FF_{i}");
                obj.transform.SetParent(parent, false);
                var rect = obj.AddComponent<RectTransform>();

                float px, py;
                int zone = i % 3;
                if (zone == 0)      { px = Random.Range(0.04f, 0.20f); py = Random.Range(0.10f, 0.35f); }
                else if (zone == 1) { px = Random.Range(0.82f, 0.96f); py = Random.Range(0.06f, 0.26f); }
                else                { px = Random.Range(0.58f, 0.72f); py = Random.Range(0.20f, 0.44f); }

                float s = Random.Range(0.005f, 0.010f);
                rect.anchorMin = new Vector2(px - s, py - s * 1.78f);
                rect.anchorMax = new Vector2(px + s, py + s * 1.78f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var img = obj.AddComponent<Image>();
                img.sprite = ffSprite;
                img.color = new Color(1f, 0.92f, 0.55f, 0.4f);
                img.raycastTarget = false;

                _fireflies[i] = rect;
                _fireflyImages[i] = img;
                _fireflyPhases[i] = Random.Range(0f, Mathf.PI * 2f);
                _fireflyBase[i] = new Vector2(px, py);
            }
        }

        // ================================================================
        // Update
        // ================================================================

        private void Update()
        {
            float t = Time.unscaledTime;

            // 太阳光晕呼吸
            if (_sunGlow != null)
            {
                float b = (Mathf.Sin(t * 0.35f) + 1f) * 0.5f;
                float scale = 0.92f + b * 0.16f;
                _sunGlow.rectTransform.localScale = new Vector3(scale, scale, 1f);
                _sunGlow.color = new Color(1f, 1f, 1f, 0.10f + b * 0.12f);
            }

            // 星辰闪烁
            if (_stars != null)
            {
                for (int i = 0; i < STAR_COUNT; i++)
                {
                    if (_stars[i] == null) continue;
                    float raw = Mathf.Sin(t * _starSpeeds[i] + _starPhases[i]);
                    float flicker = Mathf.Pow((raw + 1f) * 0.5f, 3f);
                    float alpha = _starBaseAlpha[i] * (0.12f + flicker * 0.88f);
                    var c = _stars[i].color;
                    _stars[i].color = new Color(c.r, c.g, c.b, alpha);
                }
            }

            // 萤火虫
            if (_fireflies != null)
            {
                for (int i = 0; i < FIREFLY_COUNT; i++)
                {
                    if (_fireflies[i] == null) continue;

                    float phase = t * 0.12f + _fireflyPhases[i] * 4f;
                    float yRise = (phase % 1f) * 0.28f;
                    float xWave = Mathf.Sin(t * 0.6f + _fireflyPhases[i]) * 0.02f;

                    float bx = _fireflyBase[i].x;
                    float by = _fireflyBase[i].y;
                    float newY = by + yRise;
                    if (newY > by + 0.28f) newY -= 0.28f;

                    float s = 0.007f;
                    _fireflies[i].anchorMin = new Vector2(bx + xWave - s, newY - s * 1.78f);
                    _fireflies[i].anchorMax = new Vector2(bx + xWave + s, newY + s * 1.78f);

                    if (_fireflyImages[i] != null)
                    {
                        float fl = (Mathf.Sin(t * 2.5f + _fireflyPhases[i] * 7f) + 1f) * 0.5f;
                        float fadeUp = 1f - Mathf.Clamp01((newY - (by + 0.20f)) / 0.08f);
                        _fireflyImages[i].color = new Color(1f, 0.92f, 0.55f, (0.15f + fl * 0.55f) * fadeUp);
                    }
                }
            }
        }

        // ================================================================
        // 工具
        // ================================================================

        private Sprite MakeRadialGradient(int size, Color center)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                    float a = Mathf.Clamp01(1f - d * d);
                    tex.SetPixel(x, y, new Color(center.r, center.g, center.b, a * center.a));
                }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
