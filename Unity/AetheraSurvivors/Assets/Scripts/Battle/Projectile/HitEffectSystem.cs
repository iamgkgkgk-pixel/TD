// ============================================================
// 文件名：HitEffectSystem.cs
// 功能描述：命中特效系统 — 纯程序化实现所有命中/爆炸/冰冻特效
//   箭矢命中闪光、魔法爆炸、冰冻扩散、炮弹爆炸、毒雾扩散
//   不依赖任何外部美术素材或粒子系统
// 创建时间：2026-03-26
// 所属模块：Battle/Projectile
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Tower;
using AetheraSurvivors.Battle.Visual;


namespace AetheraSurvivors.Battle.Projectile
{
    /// <summary>
    /// 命中特效系统 — 在投射物命中时播放视觉效果
    /// 所有特效纯程序化生成，无需外部素材
    /// </summary>
    public class HitEffectSystem : MonoSingleton<HitEffectSystem>
    {
        // ========== 缓存纹理 ==========
        private static Texture2D _particleTex;
        private static Texture2D _ringTex;
        private static Sprite _particleSprite;
        private static Sprite _ringSprite;

        protected override void OnInit()
        {
            CreateTextures();
        }

        protected override void OnDispose() { }

        private void CreateTextures()
        {
            // 粒子纹理（柔和圆形）
            int pSize = 16;
            _particleTex = new Texture2D(pSize, pSize, TextureFormat.RGBA32, false);
            float pc = pSize / 2f;
            for (int y = 0; y < pSize; y++)
                for (int x = 0; x < pSize; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(pc, pc)) / pc;
                    float a = Mathf.Clamp01(1f - d * d);
                    _particleTex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            _particleTex.Apply();
            _particleTex.filterMode = FilterMode.Bilinear;
            _particleSprite = Sprite.Create(_particleTex, new Rect(0, 0, pSize, pSize), new Vector2(0.5f, 0.5f), pSize);

            // 环形纹理
            int rSize = 32;
            _ringTex = new Texture2D(rSize, rSize, TextureFormat.RGBA32, false);
            float rc = rSize / 2f;
            for (int y = 0; y < rSize; y++)
                for (int x = 0; x < rSize; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(rc, rc)) / rc;
                    float a = Mathf.Abs(d - 0.75f) < 0.12f ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.75f) / 0.12f) : 0f;
                    _ringTex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            _ringTex.Apply();
            _ringTex.filterMode = FilterMode.Bilinear;
            _ringSprite = Sprite.Create(_ringTex, new Rect(0, 0, rSize, rSize), new Vector2(0.5f, 0.5f), rSize);
        }

        // ========== 公共接口 ==========

        /// <summary>
        /// 播放命中特效
        /// </summary>
        public void PlayHitEffect(Vector3 position, TowerType towerType, bool isCritical = false)
        {
            switch (towerType)
            {
                case TowerType.Archer:
                    SpawnArrowHit(position, isCritical);
                    break;
                case TowerType.Mage:
                    SpawnMagicExplosion(position, isCritical);
                    break;
                case TowerType.Ice:
                    SpawnIceFreeze(position, isCritical);
                    break;
                case TowerType.Cannon:
                    SpawnCannonExplosion(position, isCritical);
                    break;
                case TowerType.Poison:
                    SpawnPoisonCloud(position, isCritical);
                    break;
            }

            // 暴击时额外的全局效果
            if (isCritical)
            {
                SpawnCriticalFlash(position);
            }
        }


        /// <summary>播放AOE范围指示特效</summary>
        public void PlayAOEIndicator(Vector3 position, float radius, Color color, float duration = 0.5f)
        {
            var obj = new GameObject("AOE_Indicator");
            obj.transform.position = position;
            var effect = obj.AddComponent<AOERingEffect>();
            effect.Init(_ringSprite, radius, color, duration);
        }

        // ========== 箭矢命中：小型闪光 + 碎片 + 火花 ==========

        private void SpawnArrowHit(Vector3 pos, bool isCritical)
        {
            int count = isCritical ? 8 : 5;
            float size = isCritical ? 0.16f : 0.12f;
            float speed = isCritical ? 3f : 2f;

            var obj = new GameObject("ArrowHit");
            obj.transform.position = pos;
            var effect = obj.AddComponent<ParticleBurstEffect>();
            effect.Init(_particleSprite, count, new Color(1f, 0.9f, 0.4f), size, speed, 0.25f);

            // 添加小型闪光
            var flashObj = new GameObject("ArrowFlash");
            flashObj.transform.position = pos;
            var flash = flashObj.AddComponent<FlashEffect>();
            flash.Init(_particleSprite, new Color(1f, 0.95f, 0.7f, 0.9f), isCritical ? 0.4f : 0.25f, 0.1f);
        }


        // ========== 魔法爆炸：紫色扩散环 + 粒子 + 次级环 ==========

        private void SpawnMagicExplosion(Vector3 pos, bool isCritical)
        {
            float scale = isCritical ? 1.4f : 1f;

            // 主扩散环
            var ringObj = new GameObject("MagicRing");
            ringObj.transform.position = pos;
            var ring = ringObj.AddComponent<ExpandingRingEffect>();
            ring.Init(_ringSprite, new Color(0.7f, 0.3f, 1f, 0.8f), 0.3f * scale, 1.5f * scale, 0.4f);

            // 次级扩散环（延迟）
            var ring2Obj = new GameObject("MagicRing2");
            ring2Obj.transform.position = pos;
            var ring2 = ring2Obj.AddComponent<ExpandingRingEffect>();
            ring2.Init(_ringSprite, new Color(0.5f, 0.2f, 0.9f, 0.5f), 0.15f * scale, 1.8f * scale, 0.55f);

            // 粒子爆发
            int burstCount = isCritical ? 12 : 8;
            var burstObj = new GameObject("MagicBurst");
            burstObj.transform.position = pos;
            var burst = burstObj.AddComponent<ParticleBurstEffect>();
            burst.Init(_particleSprite, burstCount, new Color(0.6f, 0.2f, 1f), 0.2f * scale, 3.5f * scale, 0.4f);

            // 中心闪光
            var flashObj = new GameObject("MagicFlash");
            flashObj.transform.position = pos;
            var flash = flashObj.AddComponent<FlashEffect>();
            flash.Init(_particleSprite, new Color(0.8f, 0.5f, 1f, 1f), 0.7f * scale, 0.18f);
        }


        // ========== 冰冻：蓝色冰晶扩散 + 冰霜粒子 + 冰裂纹 ==========

        private void SpawnIceFreeze(Vector3 pos, bool isCritical)
        {
            float scale = isCritical ? 1.3f : 1f;

            // 冰冻环
            var ringObj = new GameObject("IceRing");
            ringObj.transform.position = pos;
            var ring = ringObj.AddComponent<ExpandingRingEffect>();
            ring.Init(_ringSprite, new Color(0.4f, 0.8f, 1f, 0.7f), 0.2f * scale, 1.2f * scale, 0.5f);

            // 冰晶粒子
            int burstCount = isCritical ? 10 : 6;
            var burstObj = new GameObject("IceBurst");
            burstObj.transform.position = pos;
            var burst = burstObj.AddComponent<ParticleBurstEffect>();
            burst.Init(_particleSprite, burstCount, new Color(0.5f, 0.9f, 1f), 0.15f * scale, 2.5f * scale, 0.45f);

            // 冰霜闪光
            var flashObj = new GameObject("IceFlash");
            flashObj.transform.position = pos;
            var flash = flashObj.AddComponent<FlashEffect>();
            flash.Init(_particleSprite, new Color(0.7f, 0.95f, 1f, 0.8f), 0.5f * scale, 0.15f);
        }


        // ========== 炮弹爆炸：大型火焰爆炸 + 冲击波 + 屏幕震动 ==========

        private void SpawnCannonExplosion(Vector3 pos, bool isCritical)
        {
            float scale = isCritical ? 1.5f : 1f;

            // 大型冲击波
            var ringObj = new GameObject("ExplosionRing");
            ringObj.transform.position = pos;
            var ring = ringObj.AddComponent<ExpandingRingEffect>();
            ring.Init(_ringSprite, new Color(1f, 0.6f, 0.1f, 0.9f), 0.3f * scale, 2.5f * scale, 0.35f);

            // 次级冲击波
            var ring2Obj = new GameObject("ExplosionRing2");
            ring2Obj.transform.position = pos;
            var ring2 = ring2Obj.AddComponent<ExpandingRingEffect>();
            ring2.Init(_ringSprite, new Color(1f, 0.8f, 0.3f, 0.6f), 0.5f * scale, 3f * scale, 0.45f);

            // 火焰粒子
            int burstCount = isCritical ? 18 : 12;
            var burstObj = new GameObject("ExplosionBurst");
            burstObj.transform.position = pos;
            var burst = burstObj.AddComponent<ParticleBurstEffect>();
            burst.Init(_particleSprite, burstCount, new Color(1f, 0.5f, 0.1f), 0.28f * scale, 4.5f * scale, 0.45f);

            // 中心大闪光
            var flashObj = new GameObject("ExplosionFlash");
            flashObj.transform.position = pos;
            var flash = flashObj.AddComponent<FlashEffect>();
            flash.Init(_particleSprite, new Color(1f, 0.9f, 0.5f, 1f), 1.2f * scale, 0.22f);

            // 烟雾粒子（灰色，慢速上升）
            var smokeObj = new GameObject("ExplosionSmoke");
            smokeObj.transform.position = pos;
            var smoke = smokeObj.AddComponent<SmokeRiseEffect>();
            smoke.Init(_particleSprite, isCritical ? 10 : 6, new Color(0.4f, 0.38f, 0.35f, 0.5f), 0.7f);

            // 炮弹爆炸触发屏幕震动
            TriggerScreenShake(isCritical ? 0.1f : 0.05f, isCritical ? 0.25f : 0.15f);
        }


        // ========== 毒雾：绿色毒云扩散 + 毒液飞溅 ==========

        private void SpawnPoisonCloud(Vector3 pos, bool isCritical)
        {
            float scale = isCritical ? 1.3f : 1f;

            // 毒雾扩散
            var cloudObj = new GameObject("PoisonCloud");
            cloudObj.transform.position = pos;
            var cloud = cloudObj.AddComponent<PoisonCloudEffect>();
            cloud.Init(_particleSprite, new Color(0.2f, 0.85f, 0.2f, 0.5f), 1.2f, 0.9f * scale);

            // 毒液飞溅
            int splashCount = isCritical ? 8 : 5;
            var burstObj = new GameObject("PoisonSplash");
            burstObj.transform.position = pos;
            var burst = burstObj.AddComponent<ParticleBurstEffect>();
            burst.Init(_particleSprite, splashCount, new Color(0.1f, 0.7f, 0.1f), 0.14f * scale, 2f * scale, 0.35f);

            // 毒雾闪光
            var flashObj = new GameObject("PoisonFlash");
            flashObj.transform.position = pos;
            var flash = flashObj.AddComponent<FlashEffect>();
            flash.Init(_particleSprite, new Color(0.3f, 1f, 0.3f, 0.6f), 0.4f * scale, 0.12f);
        }

        // ========== 暴击额外闪光 ==========

        private void SpawnCriticalFlash(Vector3 pos)
        {
            // 暴击时的额外白色闪光
            var flashObj = new GameObject("CritFlash");
            flashObj.transform.position = pos;
            var flash = flashObj.AddComponent<FlashEffect>();
            flash.Init(_particleSprite, new Color(1f, 1f, 1f, 0.8f), 0.8f, 0.1f);
        }

        // ========== 屏幕震动 ==========

        private void TriggerScreenShake(float intensity, float duration)
        {
            var cam = Camera.main;
            if (cam == null) return;

            // 复用EnemyVisualFeedback中的CameraShake组件
            var shaker = cam.GetComponent<AetheraSurvivors.Battle.Visual.CameraShake>();
            if (shaker == null)
                shaker = cam.gameObject.AddComponent<AetheraSurvivors.Battle.Visual.CameraShake>();
            shaker.Shake(intensity, duration);
        }

    }

    // ====================================================================
    // 特效组件
    // ====================================================================

    /// <summary>粒子爆发特效</summary>
    public class ParticleBurstEffect : MonoBehaviour
    {
        private SpriteRenderer[] _particles;
        private Vector3[] _velocities;
        private float[] _lifetimes;
        private float _maxLife;
        private float _timer;

        public void Init(Sprite sprite, int count, Color color, float size, float speed, float lifetime)
        {
            _maxLife = lifetime;
            _timer = lifetime;
            _particles = new SpriteRenderer[count];
            _velocities = new Vector3[count];
            _lifetimes = new float[count];

            for (int i = 0; i < count; i++)
            {
                var pObj = new GameObject($"P_{i}");
                pObj.transform.SetParent(transform);
                pObj.transform.localPosition = Vector3.zero;
                pObj.transform.localScale = Vector3.one * size * Random.Range(0.6f, 1.4f);

                var sr = pObj.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(
                    color.r + Random.Range(-0.1f, 0.1f),
                    color.g + Random.Range(-0.1f, 0.1f),
                    color.b + Random.Range(-0.1f, 0.1f),
                    color.a
                );
                sr.sortingOrder = 15;
                _particles[i] = sr;

                // 随机方向
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float spd = speed * Random.Range(0.5f, 1.2f);
                _velocities[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * spd;
                _lifetimes[i] = lifetime * Random.Range(0.7f, 1f);
            }
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0)
            {
                Destroy(gameObject);
                return;
            }

            for (int i = 0; i < _particles.Length; i++)
            {
                if (_particles[i] == null) continue;

                _particles[i].transform.localPosition += _velocities[i] * Time.deltaTime;
                _velocities[i] *= 0.93f; // 减速

                float t = Mathf.Clamp01(_timer / _maxLife);
                Color c = _particles[i].color;
                c.a = t;
                _particles[i].color = c;
                _particles[i].transform.localScale *= 0.98f;
            }
        }
    }

    /// <summary>扩散环特效</summary>
    public class ExpandingRingEffect : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _startScale;
        private float _endScale;
        private float _duration;
        private float _timer;
        private Color _baseColor;

        public void Init(Sprite sprite, Color color, float startScale, float endScale, float duration)
        {
            _startScale = startScale;
            _endScale = endScale;
            _duration = duration;
            _timer = 0f;
            _baseColor = color;

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = sprite;
            _sr.color = color;
            _sr.sortingOrder = 14;
            transform.localScale = Vector3.one * startScale;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = _timer / _duration;

            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // 缓出扩散
            float easeT = 1f - (1f - t) * (1f - t);
            float scale = Mathf.Lerp(_startScale, _endScale, easeT);
            transform.localScale = Vector3.one * scale;

            // 淡出
            Color c = _baseColor;
            c.a = _baseColor.a * (1f - t);
            _sr.color = c;
        }
    }

    /// <summary>闪光特效</summary>
    public class FlashEffect : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _duration;
        private float _timer;
        private float _maxScale;
        private Color _baseColor;

        public void Init(Sprite sprite, Color color, float maxScale, float duration)
        {
            _maxScale = maxScale;
            _duration = duration;
            _timer = 0f;
            _baseColor = color;

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = sprite;
            _sr.color = color;
            _sr.sortingOrder = 16;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = _timer / _duration;

            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // 快速扩大后缩小
            float scale;
            if (t < 0.3f)
            {
                scale = Mathf.Lerp(0, _maxScale, t / 0.3f);
            }
            else
            {
                scale = Mathf.Lerp(_maxScale, 0, (t - 0.3f) / 0.7f);
            }
            transform.localScale = Vector3.one * scale;

            Color c = _baseColor;
            c.a = 1f - t;
            _sr.color = c;
        }
    }

    /// <summary>烟雾上升特效</summary>
    public class SmokeRiseEffect : MonoBehaviour
    {
        private SpriteRenderer[] _smokes;
        private Vector3[] _velocities;
        private float _timer;
        private float _duration;

        public void Init(Sprite sprite, int count, Color color, float duration)
        {
            _duration = duration;
            _timer = duration;
            _smokes = new SpriteRenderer[count];
            _velocities = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                var obj = new GameObject($"Smoke_{i}");
                obj.transform.SetParent(transform);
                obj.transform.localPosition = new Vector3(
                    Random.Range(-0.2f, 0.2f),
                    Random.Range(-0.1f, 0.1f), 0);
                obj.transform.localScale = Vector3.one * Random.Range(0.2f, 0.4f);

                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(color.r, color.g, color.b, color.a * Random.Range(0.5f, 1f));
                sr.sortingOrder = 13;
                _smokes[i] = sr;

                _velocities[i] = new Vector3(
                    Random.Range(-0.3f, 0.3f),
                    Random.Range(0.5f, 1.5f), 0);
            }
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0)
            {
                Destroy(gameObject);
                return;
            }

            float t = _timer / _duration;
            for (int i = 0; i < _smokes.Length; i++)
            {
                if (_smokes[i] == null) continue;
                _smokes[i].transform.localPosition += _velocities[i] * Time.deltaTime;
                _smokes[i].transform.localScale *= 1.01f; // 慢慢变大
                Color c = _smokes[i].color;
                c.a = t * 0.4f;
                _smokes[i].color = c;
            }
        }
    }

    /// <summary>毒云特效</summary>
    public class PoisonCloudEffect : MonoBehaviour
    {
        private SpriteRenderer[] _clouds;
        private Vector3[] _driftVelocities;
        private float _timer;
        private float _duration;

        public void Init(Sprite sprite, Color color, float duration, float radius)
        {
            _duration = duration;
            _timer = duration;
            int count = 5;
            _clouds = new SpriteRenderer[count];
            _driftVelocities = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                var obj = new GameObject($"Cloud_{i}");
                obj.transform.SetParent(transform);
                float angle = (float)i / count * 360f * Mathf.Deg2Rad;
                float r = Random.Range(0f, radius * 0.3f);
                obj.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0);
                obj.transform.localScale = Vector3.one * Random.Range(0.3f, 0.6f);

                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(color.r, color.g, color.b, color.a * Random.Range(0.5f, 1f));
                sr.sortingOrder = 13;
                _clouds[i] = sr;

                // 缓慢向外扩散
                _driftVelocities[i] = new Vector3(
                    Mathf.Cos(angle) * 0.5f + Random.Range(-0.2f, 0.2f),
                    Mathf.Sin(angle) * 0.5f + Random.Range(-0.2f, 0.2f), 0);
            }
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0)
            {
                Destroy(gameObject);
                return;
            }

            float t = _timer / _duration;
            for (int i = 0; i < _clouds.Length; i++)
            {
                if (_clouds[i] == null) continue;
                _clouds[i].transform.localPosition += _driftVelocities[i] * Time.deltaTime;
                _clouds[i].transform.localScale *= 1.005f;
                Color c = _clouds[i].color;
                c.a = t * 0.4f;
                _clouds[i].color = c;
            }
        }
    }

    /// <summary>AOE范围环特效</summary>
    public class AOERingEffect : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _duration;
        private float _timer;
        private Color _baseColor;
        private float _targetScale;

        public void Init(Sprite sprite, float radius, Color color, float duration)
        {
            _duration = duration;
            _timer = 0f;
            _baseColor = color;
            _targetScale = radius * 2f; // 直径

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = sprite;
            _sr.color = color;
            _sr.sortingOrder = 14;
            transform.localScale = Vector3.one * _targetScale;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = _timer / _duration;

            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // 脉动
            float pulse = 1f + Mathf.Sin(t * Mathf.PI * 4f) * 0.05f;
            transform.localScale = Vector3.one * _targetScale * pulse;

            Color c = _baseColor;
            c.a = _baseColor.a * (1f - t * t);
            _sr.color = c;
        }
    }
}
