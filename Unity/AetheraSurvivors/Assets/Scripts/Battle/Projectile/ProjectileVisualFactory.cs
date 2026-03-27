// ============================================================
// 文件名：ProjectileVisualFactory.cs
// 功能描述：程序化投射物视觉工厂 — 纯代码生成所有投射物外观
//   箭矢（带尾迹）、魔法弹（发光球+粒子）、冰弹（冰晶）、
//   炮弹（带烟尾）、毒弹（毒液球）
//   不依赖任何外部美术素材
// 创建时间：2026-03-26
// 所属模块：Battle/Projectile
// ============================================================

using UnityEngine;
using AetheraSurvivors.Battle.Tower;

namespace AetheraSurvivors.Battle.Projectile
{
    /// <summary>
    /// 程序化投射物视觉工厂 — 为投射物添加纯代码生成的视觉效果
    /// 
    /// 每种塔类型的投射物有独特外观：
    /// - 箭塔：金色箭矢 + 拖尾线
    /// - 法塔：紫色魔法球 + 发光光环 + 粒子尾迹
    /// - 冰塔：蓝色冰晶 + 冰霜粒子
    /// - 炮塔：橙色炮弹 + 烟雾尾迹 + 旋转
    /// - 毒塔：绿色毒液球 + 滴落粒子
    /// </summary>
    public static class ProjectileVisualFactory
    {
        // ========== 缓存的纹理 ==========
        private static Texture2D _circleTexture;
        private static Texture2D _diamondTexture;
        private static Texture2D _softGlowTexture;
        private static Texture2D _arrowTexture;

        // ========== 公共接口 ==========

        /// <summary>
        /// 为投射物GameObject添加视觉效果
        /// </summary>
        public static void ApplyVisual(GameObject projectileObj, TowerType towerType, float speed)
        {
            switch (towerType)
            {
                case TowerType.Archer:
                    ApplyArrowVisual(projectileObj, speed);
                    break;
                case TowerType.Mage:
                    ApplyMagicBoltVisual(projectileObj, speed);
                    break;
                case TowerType.Ice:
                    ApplyIceBoltVisual(projectileObj, speed);
                    break;
                case TowerType.Cannon:
                    ApplyCannonBallVisual(projectileObj, speed);
                    break;
                case TowerType.Poison:
                    ApplyPoisonBoltVisual(projectileObj, speed);
                    break;
            }
        }

        // ========== 箭塔：金色箭矢 ==========

        private static void ApplyArrowVisual(GameObject obj, float speed)
        {
            // 主体：箭矢形状
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr == null) sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetArrowSprite();
            sr.color = new Color(1f, 0.9f, 0.4f, 1f);
            sr.sortingOrder = 12;
            obj.transform.localScale = new Vector3(0.4f, 0.15f, 1f);

            // 拖尾线
            var trail = obj.AddComponent<TrailRenderer>();
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startWidth = 0.06f;
            trail.endWidth = 0f;
            trail.time = 0.15f;
            trail.startColor = new Color(1f, 0.85f, 0.3f, 0.7f);
            trail.endColor = new Color(1f, 0.7f, 0.2f, 0f);
            trail.sortingOrder = 11;
            trail.minVertexDistance = 0.05f;

            // 添加动画组件
            var anim = obj.AddComponent<ProjectileAnimator>();
            anim.Init(ProjectileAnimType.Arrow);
        }

        // ========== 法塔：紫色魔法球 ==========

        private static void ApplyMagicBoltVisual(GameObject obj, float speed)
        {
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr == null) sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = new Color(0.7f, 0.3f, 1f, 1f);
            sr.sortingOrder = 12;
            obj.transform.localScale = new Vector3(0.2f, 0.2f, 1f);

            // 外发光
            var glowObj = new GameObject("MagicGlow");
            glowObj.transform.SetParent(obj.transform);
            glowObj.transform.localPosition = Vector3.zero;
            glowObj.transform.localScale = new Vector3(2.5f, 2.5f, 1f);
            var glowSr = glowObj.AddComponent<SpriteRenderer>();
            glowSr.sprite = GetSoftGlowSprite();
            glowSr.color = new Color(0.6f, 0.2f, 1f, 0.4f);
            glowSr.sortingOrder = 11;

            // 拖尾
            var trail = obj.AddComponent<TrailRenderer>();
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startWidth = 0.12f;
            trail.endWidth = 0f;
            trail.time = 0.25f;
            trail.startColor = new Color(0.7f, 0.3f, 1f, 0.6f);
            trail.endColor = new Color(0.5f, 0.1f, 0.8f, 0f);
            trail.sortingOrder = 11;
            trail.minVertexDistance = 0.05f;

            var anim = obj.AddComponent<ProjectileAnimator>();
            anim.Init(ProjectileAnimType.MagicBolt);
        }

        // ========== 冰塔：蓝色冰晶 ==========

        private static void ApplyIceBoltVisual(GameObject obj, float speed)
        {
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr == null) sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetDiamondSprite();
            sr.color = new Color(0.5f, 0.85f, 1f, 1f);
            sr.sortingOrder = 12;
            obj.transform.localScale = new Vector3(0.18f, 0.25f, 1f);

            // 冰霜光环
            var glowObj = new GameObject("IceGlow");
            glowObj.transform.SetParent(obj.transform);
            glowObj.transform.localPosition = Vector3.zero;
            glowObj.transform.localScale = new Vector3(2f, 2f, 1f);
            var glowSr = glowObj.AddComponent<SpriteRenderer>();
            glowSr.sprite = GetSoftGlowSprite();
            glowSr.color = new Color(0.4f, 0.7f, 1f, 0.3f);
            glowSr.sortingOrder = 11;

            // 冰霜拖尾
            var trail = obj.AddComponent<TrailRenderer>();
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startWidth = 0.08f;
            trail.endWidth = 0f;
            trail.time = 0.2f;
            trail.startColor = new Color(0.6f, 0.9f, 1f, 0.5f);
            trail.endColor = new Color(0.4f, 0.7f, 1f, 0f);
            trail.sortingOrder = 11;
            trail.minVertexDistance = 0.05f;

            var anim = obj.AddComponent<ProjectileAnimator>();
            anim.Init(ProjectileAnimType.IceBolt);
        }

        // ========== 炮塔：橙色炮弹 ==========

        private static void ApplyCannonBallVisual(GameObject obj, float speed)
        {
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr == null) sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = new Color(0.3f, 0.25f, 0.2f, 1f);
            sr.sortingOrder = 12;
            obj.transform.localScale = new Vector3(0.22f, 0.22f, 1f);

            // 火焰光环
            var glowObj = new GameObject("FireGlow");
            glowObj.transform.SetParent(obj.transform);
            glowObj.transform.localPosition = Vector3.zero;
            glowObj.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
            var glowSr = glowObj.AddComponent<SpriteRenderer>();
            glowSr.sprite = GetSoftGlowSprite();
            glowSr.color = new Color(1f, 0.5f, 0.1f, 0.5f);
            glowSr.sortingOrder = 11;

            // 烟雾拖尾
            var trail = obj.AddComponent<TrailRenderer>();
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startWidth = 0.15f;
            trail.endWidth = 0.05f;
            trail.time = 0.4f;
            trail.startColor = new Color(0.4f, 0.35f, 0.3f, 0.5f);
            trail.endColor = new Color(0.3f, 0.3f, 0.3f, 0f);
            trail.sortingOrder = 10;
            trail.minVertexDistance = 0.05f;

            var anim = obj.AddComponent<ProjectileAnimator>();
            anim.Init(ProjectileAnimType.CannonBall);
        }

        // ========== 毒塔：绿色毒液球 ==========

        private static void ApplyPoisonBoltVisual(GameObject obj, float speed)
        {
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr == null) sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = new Color(0.2f, 0.9f, 0.2f, 1f);
            sr.sortingOrder = 12;
            obj.transform.localScale = new Vector3(0.15f, 0.15f, 1f);

            // 毒雾光环
            var glowObj = new GameObject("PoisonGlow");
            glowObj.transform.SetParent(obj.transform);
            glowObj.transform.localPosition = Vector3.zero;
            glowObj.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
            var glowSr = glowObj.AddComponent<SpriteRenderer>();
            glowSr.sprite = GetSoftGlowSprite();
            glowSr.color = new Color(0.1f, 0.8f, 0.1f, 0.3f);
            glowSr.sortingOrder = 11;

            // 毒液拖尾
            var trail = obj.AddComponent<TrailRenderer>();
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startWidth = 0.1f;
            trail.endWidth = 0f;
            trail.time = 0.3f;
            trail.startColor = new Color(0.2f, 0.9f, 0.2f, 0.5f);
            trail.endColor = new Color(0.1f, 0.6f, 0.1f, 0f);
            trail.sortingOrder = 11;
            trail.minVertexDistance = 0.05f;

            var anim = obj.AddComponent<ProjectileAnimator>();
            anim.Init(ProjectileAnimType.PoisonBolt);
        }

        // ========== 纹理生成 ==========

        /// <summary>获取圆形纹理（缓存）</summary>
        private static Sprite GetCircleSprite()
        {
            if (_circleTexture == null)
            {
                int size = 32;
                _circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float center = size / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                        float alpha = dist < 0.85f ? 1f : Mathf.Clamp01((1f - dist) / 0.15f);
                        float brightness = 1f - dist * 0.3f; // 中心更亮
                        _circleTexture.SetPixel(x, y, new Color(brightness, brightness, brightness, alpha));
                    }
                }
                _circleTexture.Apply();
                _circleTexture.filterMode = FilterMode.Bilinear;
            }
            return Sprite.Create(_circleTexture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
        }

        /// <summary>获取菱形纹理（冰晶用）</summary>
        private static Sprite GetDiamondSprite()
        {
            if (_diamondTexture == null)
            {
                int size = 32;
                _diamondTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float center = size / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // 菱形距离
                        float dx = Mathf.Abs(x - center) / center;
                        float dy = Mathf.Abs(y - center) / center;
                        float dist = dx + dy;
                        float alpha = dist < 0.85f ? 1f : Mathf.Clamp01((1f - dist) / 0.15f);
                        float brightness = 1f - dist * 0.2f;
                        _diamondTexture.SetPixel(x, y, new Color(brightness, brightness, brightness, alpha));
                    }
                }
                _diamondTexture.Apply();
                _diamondTexture.filterMode = FilterMode.Bilinear;
            }
            return Sprite.Create(_diamondTexture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
        }

        /// <summary>获取柔和发光纹理</summary>
        private static Sprite GetSoftGlowSprite()
        {
            if (_softGlowTexture == null)
            {
                int size = 32;
                _softGlowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float center = size / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                        float alpha = Mathf.Clamp01(1f - dist * dist); // 二次衰减
                        _softGlowTexture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                    }
                }
                _softGlowTexture.Apply();
                _softGlowTexture.filterMode = FilterMode.Bilinear;
            }
            return Sprite.Create(_softGlowTexture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
        }

        /// <summary>获取箭矢纹理</summary>
        private static Sprite GetArrowSprite()
        {
            if (_arrowTexture == null)
            {
                int w = 32, h = 12;
                _arrowTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                float cx = w / 2f, cy = h / 2f;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float nx = (float)x / w;
                        float ny = Mathf.Abs(y - cy) / cy;

                        // 箭矢形状：前端尖，后端宽
                        float maxWidth = nx < 0.7f ? nx * 1.4f : (1f - nx) * 3.3f;
                        float alpha = ny < maxWidth ? 1f : 0f;

                        // 箭头部分更亮
                        float brightness = nx > 0.6f ? 1.2f : 0.9f;
                        _arrowTexture.SetPixel(x, y, new Color(brightness, brightness, brightness, alpha));
                    }
                }
                _arrowTexture.Apply();
                _arrowTexture.filterMode = FilterMode.Bilinear;
            }
            return Sprite.Create(_arrowTexture, new Rect(0, 0, 32, 12), new Vector2(0.5f, 0.5f), 32);
        }
    }

    // ====================================================================
    // 投射物动画类型
    // ====================================================================

    public enum ProjectileAnimType
    {
        Arrow,
        MagicBolt,
        IceBolt,
        CannonBall,
        PoisonBolt
    }

    // ====================================================================
    // 投射物动画组件
    // ====================================================================

    /// <summary>
    /// 投射物飞行动画 — 旋转、缩放脉动、发光闪烁
    /// </summary>
    public class ProjectileAnimator : MonoBehaviour
    {
        private ProjectileAnimType _type;
        private float _timer;
        private Transform _glowChild;
        private SpriteRenderer _glowSr;
        private SpriteRenderer _mainSr;
        private Vector3 _baseScale;

        public void Init(ProjectileAnimType type)
        {
            _type = type;
            _timer = 0f;
            _mainSr = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;

            // 查找发光子对象
            if (transform.childCount > 0)
            {
                _glowChild = transform.GetChild(0);
                _glowSr = _glowChild.GetComponent<SpriteRenderer>();
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            switch (_type)
            {
                case ProjectileAnimType.Arrow:
                    // 箭矢：轻微上下抖动
                    break;

                case ProjectileAnimType.MagicBolt:
                    // 魔法球：脉动 + 发光闪烁
                    float pulse = 1f + Mathf.Sin(_timer * 12f) * 0.15f;
                    transform.localScale = _baseScale * pulse;
                    if (_glowSr != null)
                    {
                        Color c = _glowSr.color;
                        c.a = 0.3f + Mathf.Sin(_timer * 8f) * 0.15f;
                        _glowSr.color = c;
                        float gs = 2.5f + Mathf.Sin(_timer * 10f) * 0.5f;
                        _glowChild.localScale = new Vector3(gs, gs, 1f);
                    }
                    break;

                case ProjectileAnimType.IceBolt:
                    // 冰晶：旋转 + 闪烁
                    transform.Rotate(0, 0, 360f * Time.deltaTime);
                    if (_mainSr != null)
                    {
                        float iceB = 0.85f + Mathf.Sin(_timer * 15f) * 0.15f;
                        _mainSr.color = new Color(0.5f * iceB, 0.85f * iceB, 1f * iceB, 1f);
                    }
                    break;

                case ProjectileAnimType.CannonBall:
                    // 炮弹：旋转 + 火焰闪烁
                    transform.Rotate(0, 0, -200f * Time.deltaTime);
                    if (_glowSr != null)
                    {
                        float fireFlicker = 0.4f + Mathf.Sin(_timer * 20f) * 0.15f;
                        _glowSr.color = new Color(1f, 0.5f, 0.1f, fireFlicker);
                    }
                    break;

                case ProjectileAnimType.PoisonBolt:
                    // 毒液球：脉动 + 颜色变化
                    float poisonPulse = 1f + Mathf.Sin(_timer * 8f) * 0.1f;
                    transform.localScale = _baseScale * poisonPulse;
                    if (_mainSr != null)
                    {
                        float g = 0.8f + Mathf.Sin(_timer * 6f) * 0.2f;
                        _mainSr.color = new Color(0.2f, g, 0.2f, 1f);
                    }
                    break;
            }
        }

        private void OnDisable()
        {
            // 重置状态（对象池回收时）
            transform.localScale = _baseScale;
            _timer = 0f;
        }
    }
}
