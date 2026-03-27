// ============================================================
// 文件名：EnemyVisualFeedback.cs
// 功能描述：怪物视觉反馈系统
//          受击闪白、死亡动画（缩小+淡出）、Buff视觉指示器、
//          血条UI、Boss出场特效
// 创建时间：2026-03-25
// 所属模块：Battle/Visual
// 对应交互：阶段三 #151-#152
// ============================================================

using System.Collections;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Tower;
using Logger = AetheraSurvivors.Framework.Logger;
using System.Collections.Generic;

namespace AetheraSurvivors.Battle.Visual

{

    // ====================================================================
    // 怪物受击闪白组件
    // ====================================================================

    /// <summary>
    /// 怪物受击视觉反馈 — 挂载在怪物GameObject上
    /// 
    /// 功能：
    /// 1. 受击闪白：被攻击时Sprite变白0.1秒
    /// 2. 受击抖动：被攻击时微小抖动
    /// 3. 受击缩放弹跳：被击中时短暂压扁再弹回
    /// 4. 冰冻变色：被冰冻时变蓝
    /// 5. 中毒变色：中毒时变绿
    /// 6. 移动拖影：快速移动时产生残影
    /// 7. ★ 受击方向指示：被攻击时产生方向性冲击波
    /// 8. ★ 暴击特效：暴击时产生星星特效
    /// </summary>

    public class EnemyHitFlash : MonoBehaviour
    {
        // ========== 配置 ==========

        private static readonly Color HitFlashColor = Color.white;
        private static readonly Color FreezeColor = new Color(0.5f, 0.8f, 1f, 1f);
        private static readonly Color PoisonColor = new Color(0.6f, 1f, 0.5f, 1f);
        private const float FlashDuration = 0.1f;

        // ========== 运行时 ==========

        private SpriteRenderer _sr;
        private Color _originalColor;
        private Material _material;
        private bool _isFlashing = false;
        private float _flashTimer = 0f;

        // Buff染色状态
        private bool _isFrozenVisual = false;
        private bool _isPoisonedVisual = false;

        // 受击缩放弹跳（不直接修改transform.localScale，而是提供缩放因子）
        private float _hitBounceTimer = -1f;
        private const float HIT_BOUNCE_DURATION = 0.2f;

        /// <summary>当前受击缩放因子X（供EnemyVisualAnimator读取叠加）</summary>
        public float HitBounceScaleX { get; private set; } = 1f;
        /// <summary>当前受击缩放因子Y（供EnemyVisualAnimator读取叠加）</summary>
        public float HitBounceScaleY { get; private set; } = 1f;


        // 移动拖影
        private Vector3 _lastPos;
        private float _afterimageTimer;
        private const float AFTERIMAGE_INTERVAL = 0.06f;
        private const float AFTERIMAGE_SPEED_THRESHOLD = 1.5f;

        // ★ 受击方向冲击波
        private List<HitDirectionIndicator> _activeIndicators = new List<HitDirectionIndicator>(4);

        // ★ 暴击星星特效
        private float _critStarTimer = -1f;
        private const float CRIT_STAR_DURATION = 0.4f;
        private GameObject[] _critStarObjs;
        private const int CRIT_STAR_COUNT = 3;


        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null)
            {
                _originalColor = _sr.color;
                _material = _sr.material;
            }
            _lastPos = transform.position;
        }


        private void Update()
        {
            // 受击闪白
            if (_isFlashing)
            {
                _flashTimer -= Time.deltaTime;
                // 闪白过程中做颜色渐变（白→原色），更自然
                float flashT = _flashTimer / FlashDuration;
                if (_sr != null && flashT > 0)
                {
                    _sr.color = Color.Lerp(GetTargetColor(), HitFlashColor, flashT);
                }
                if (_flashTimer <= 0f)
                {
                    _isFlashing = false;
                    RestoreColor();
                }
            }

            // 受击缩放弹跳
            UpdateHitBounce();

            // 移动拖影
            UpdateAfterimage();

            // ★ 暴击星星
            UpdateCritStars();

            // ★ 清理已完成的方向指示器
            CleanupIndicators();
        }


        /// <summary>触发受击闪白 + 缩放弹跳</summary>
        public void TriggerHitFlash()
        {
            if (_sr == null) return;

            _isFlashing = true;
            _flashTimer = FlashDuration;
            _sr.color = HitFlashColor;

            // 同时触发缩放弹跳
            _hitBounceTimer = HIT_BOUNCE_DURATION;
        }

        /// <summary>触发受击抖动</summary>
        public void TriggerHitShake(float intensity = 0.06f)
        {
            StartCoroutine(ShakeCoroutine(intensity, 0.12f));
        }

        /// <summary>★ 触发受击方向冲击波（从伤害来源方向产生视觉指示）</summary>
        public void TriggerHitDirection(Vector3 hitSourcePos)
        {
            if (_sr == null) return;

            Vector3 dir = (transform.position - hitSourcePos).normalized;
            var indicator = HitDirectionIndicator.Create(transform.position, dir, _sr.sortingOrder + 3);
            if (indicator != null)
                _activeIndicators.Add(indicator);
        }

        /// <summary>★ 触发暴击星星特效</summary>
        public void TriggerCritEffect()
        {
            _critStarTimer = CRIT_STAR_DURATION;
            SpawnCritStars();
        }

        private void SpawnCritStars()
        {
            // 清理旧的
            if (_critStarObjs != null)
            {
                for (int i = 0; i < _critStarObjs.Length; i++)
                    if (_critStarObjs[i] != null) Destroy(_critStarObjs[i]);
            }

            _critStarObjs = new GameObject[CRIT_STAR_COUNT];
            for (int i = 0; i < CRIT_STAR_COUNT; i++)
            {
                var starObj = new GameObject($"CritStar_{i}");
                starObj.transform.position = transform.position + new Vector3(
                    Random.Range(-0.2f, 0.2f),
                    Random.Range(0.1f, 0.4f),
                    0f
                );

                var sr = starObj.AddComponent<SpriteRenderer>();
                sr.sprite = CreateStarSprite();
                sr.sortingOrder = (_sr != null ? _sr.sortingOrder : 8) + 5;
                sr.color = new Color(1f, 0.95f, 0.3f, 1f); // 金色星星

                float s = Random.Range(0.08f, 0.15f);
                starObj.transform.localScale = new Vector3(s, s, 1f);
                starObj.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

                _critStarObjs[i] = starObj;
            }
        }

        private void UpdateCritStars()
        {
            if (_critStarTimer <= 0f || _critStarObjs == null) return;

            _critStarTimer -= Time.deltaTime;
            float t = 1f - (_critStarTimer / CRIT_STAR_DURATION);

            for (int i = 0; i < _critStarObjs.Length; i++)
            {
                if (_critStarObjs[i] == null) continue;

                // 向上漂浮 + 淡出 + 旋转
                _critStarObjs[i].transform.position += Vector3.up * Time.deltaTime * 0.8f;
                _critStarObjs[i].transform.Rotate(0, 0, 180f * Time.deltaTime);

                var sr = _critStarObjs[i].GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = 1f - t;
                    sr.color = c;
                }

                // 缩小
                float scale = (1f - t * 0.5f) * _critStarObjs[i].transform.localScale.x;
                _critStarObjs[i].transform.localScale = new Vector3(
                    Mathf.Max(scale, 0.01f),
                    Mathf.Max(scale, 0.01f),
                    1f
                );
            }

            if (_critStarTimer <= 0f)
            {
                for (int i = 0; i < _critStarObjs.Length; i++)
                    if (_critStarObjs[i] != null) Destroy(_critStarObjs[i]);
                _critStarObjs = null;
            }
        }

        private void CleanupIndicators()
        {
            for (int i = _activeIndicators.Count - 1; i >= 0; i--)
            {
                if (_activeIndicators[i] == null || _activeIndicators[i].IsFinished)
                    _activeIndicators.RemoveAt(i);
            }
        }

        private static Sprite _starSprite;
        private static Sprite CreateStarSprite()
        {
            if (_starSprite != null) return _starSprite;

            int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);
                    float starRadius = 0.5f + 0.5f * Mathf.Cos(angle * 4f);
                    starRadius = Mathf.Lerp(0.3f, 1f, starRadius);
                    float alpha = dist < starRadius ? Mathf.Clamp01(1f - dist / starRadius) : 0f;
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha * alpha));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _starSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _starSprite;
        }


        /// <summary>设置冰冻视觉</summary>
        public void SetFrozenVisual(bool frozen)
        {
            _isFrozenVisual = frozen;
            if (!_isFlashing) RestoreColor();
        }

        /// <summary>设置中毒视觉</summary>
        public void SetPoisonedVisual(bool poisoned)
        {
            _isPoisonedVisual = poisoned;
            if (!_isFlashing) RestoreColor();
        }

        private Color GetTargetColor()
        {
            if (_isFrozenVisual) return FreezeColor;
            if (_isPoisonedVisual) return PoisonColor;
            return _originalColor;
        }

        private void RestoreColor()
        {
            if (_sr == null) return;
            _sr.color = GetTargetColor();
        }

        // ========== 受击缩放弹跳 ==========
        // 注意：不直接修改transform.localScale，而是计算缩放因子
        // 由EnemyVisualAnimator在设置localScale时统一叠加，避免两个系统互相覆盖导致缩小bug

        private void UpdateHitBounce()
        {
            if (_hitBounceTimer <= 0f)
            {
                HitBounceScaleX = 1f;
                HitBounceScaleY = 1f;
                return;
            }

            _hitBounceTimer -= Time.deltaTime;
            float t = 1f - (_hitBounceTimer / HIT_BOUNCE_DURATION);

            if (t >= 1f)
            {
                _hitBounceTimer = -1f;
                HitBounceScaleX = 1f;
                HitBounceScaleY = 1f;
                return;
            }

            // 先压扁（squash）再拉伸（stretch）再恢复
            float squashStretch;
            if (t < 0.25f)
            {
                // 快速压扁
                squashStretch = 1f - Mathf.Sin(t / 0.25f * Mathf.PI * 0.5f) * 0.2f;
            }
            else if (t < 0.5f)
            {
                // 拉伸回弹
                float bt = (t - 0.25f) / 0.25f;
                squashStretch = 0.8f + Mathf.Sin(bt * Mathf.PI * 0.5f) * 0.25f;
            }
            else
            {
                // 衰减回正
                float bt = (t - 0.5f) / 0.5f;
                squashStretch = 1.05f - bt * 0.05f;
            }

            float invSquash = 2f - squashStretch; // 保持面积不变
            HitBounceScaleX = invSquash;
            HitBounceScaleY = squashStretch;
        }


        // ========== 移动拖影 ==========

        private void UpdateAfterimage()
        {
            if (_sr == null || _sr.sprite == null) return;

            float speed = (transform.position - _lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.001f);
            _lastPos = transform.position;

            if (speed < AFTERIMAGE_SPEED_THRESHOLD) return;

            _afterimageTimer -= Time.deltaTime;
            if (_afterimageTimer > 0f) return;
            _afterimageTimer = AFTERIMAGE_INTERVAL;

            // 生成残影
            var ghostObj = new GameObject("Afterimage");
            ghostObj.transform.position = transform.position;
            ghostObj.transform.localScale = transform.localScale;
            ghostObj.transform.rotation = transform.rotation;

            var ghostSr = ghostObj.AddComponent<SpriteRenderer>();
            ghostSr.sprite = _sr.sprite;
            ghostSr.color = new Color(_sr.color.r, _sr.color.g, _sr.color.b, 0.35f);
            ghostSr.sortingOrder = _sr.sortingOrder - 1;

            // 残影淡出组件
            var fade = ghostObj.AddComponent<AfterimageAutoFade>();
            fade.Init(0.2f);
        }

        private IEnumerator ShakeCoroutine(float intensity, float duration)
        {
            Vector3 originalPos = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                // 衰减抖动强度
                float decay = 1f - (elapsed / duration);
                float x = Random.Range(-intensity, intensity) * decay;
                float y = Random.Range(-intensity, intensity) * decay;
                transform.localPosition = originalPos + new Vector3(x, y, 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localPosition = originalPos;
        }
    }

    // ====================================================================
    // ★ 受击方向冲击波组件
    // ====================================================================

    /// <summary>受击方向指示器 — 从伤害来源方向产生的小型冲击波</summary>
    public class HitDirectionIndicator : MonoBehaviour
    {
        private float _timer;
        private float _duration = 0.25f;
        private Vector3 _direction;
        private SpriteRenderer _sr;
        private float _startAlpha;

        public bool IsFinished => _timer <= 0f;

        public static HitDirectionIndicator Create(Vector3 position, Vector3 direction, int sortingOrder)
        {
            var obj = new GameObject("HitDirIndicator");
            obj.transform.position = position + direction * 0.15f;

            // 旋转指向伤害方向
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            obj.transform.rotation = Quaternion.Euler(0, 0, angle);
            obj.transform.localScale = new Vector3(0.25f, 0.08f, 1f);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateIndicatorSprite();
            sr.sortingOrder = sortingOrder;
            sr.color = new Color(1f, 0.9f, 0.7f, 0.7f);

            var indicator = obj.AddComponent<HitDirectionIndicator>();
            indicator._direction = direction;
            indicator._timer = indicator._duration;
            indicator._sr = sr;
            indicator._startAlpha = 0.7f;

            return indicator;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            float t = _timer / _duration;

            // 向外扩散
            transform.position += _direction * Time.deltaTime * 1.5f;

            // 淡出 + 拉伸
            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = _startAlpha * t;
                _sr.color = c;
            }

            float scaleX = 0.25f + (1f - t) * 0.15f;
            float scaleY = 0.08f * t;
            transform.localScale = new Vector3(scaleX, Mathf.Max(scaleY, 0.01f), 1f);
        }

        private static Sprite _indicatorSprite;
        private static Sprite CreateIndicatorSprite()
        {
            if (_indicatorSprite != null) return _indicatorSprite;

            int w = 16, h = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 横向渐变（左亮右暗）+ 纵向柔边
                    float hFade = 1f - (float)x / w;
                    float vCenter = Mathf.Abs(y - h * 0.5f) / (h * 0.5f);
                    float vFade = Mathf.Clamp01(1f - vCenter * vCenter);
                    float alpha = hFade * vFade;
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _indicatorSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), 16);
            return _indicatorSprite;
        }
    }

    // ====================================================================
    // 残影自动淡出组件
    // ====================================================================


    /// <summary>残影自动淡出并销毁</summary>
    public class AfterimageAutoFade : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _duration;
        private float _timer;
        private float _startAlpha;

        public void Init(float duration)
        {
            _duration = duration;
            _timer = duration;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _startAlpha = _sr.color.a;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (_sr != null)
            {
                float t = _timer / _duration;
                Color c = _sr.color;
                c.a = _startAlpha * t;
                _sr.color = c;
                // 残影轻微缩小
                transform.localScale *= (1f - Time.deltaTime * 0.5f);
            }
        }
    }


    // ====================================================================
    // 怪物死亡动画组件
    // ====================================================================

    /// <summary>
    /// 怪物死亡视觉效果 — 缩小+淡出+粒子
    /// </summary>
    public class EnemyDeathEffect : MonoBehaviour
    {
        /// <summary>
        /// 播放死亡效果
        /// </summary>
        /// <param name="position">死亡位置</param>
        /// <param name="isBoss">是否Boss（Boss有更大的特效）</param>
        /// <param name="enemyType">怪物类型</param>
        public static void Play(Vector3 position, bool isBoss, EnemyType enemyType)
        {
            // 创建死亡特效对象
            var obj = new GameObject("DeathEffect");
            obj.transform.position = position;
            var effect = obj.AddComponent<EnemyDeathEffect>();
            effect.StartCoroutine(effect.DeathAnimation(isBoss, enemyType));

            // 生成碎片飞散
            SpawnDebris(position, isBoss, enemyType);

            // Boss死亡时触发屏幕震动
            if (isBoss)
            {
                TriggerScreenShake(0.15f, 0.4f);
            }
        }

        /// <summary>生成碎片飞散效果</summary>
        private static void SpawnDebris(Vector3 position, bool isBoss, EnemyType enemyType)
        {
            int debrisCount = isBoss ? 10 : 5;
            Color debrisColor = GetDeathColor(enemyType);

            for (int i = 0; i < debrisCount; i++)
            {
                var debrisObj = new GameObject($"Debris_{i}");
                debrisObj.transform.position = position;

                var sr = debrisObj.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDeathSprite();
                sr.sortingOrder = 10;

                float colorVariation = Random.Range(-0.15f, 0.15f);
                sr.color = new Color(
                    Mathf.Clamp01(debrisColor.r + colorVariation),
                    Mathf.Clamp01(debrisColor.g + colorVariation),
                    Mathf.Clamp01(debrisColor.b + colorVariation),
                    0.9f
                );

                float s = Random.Range(0.08f, 0.2f) * (isBoss ? 1.5f : 1f);
                debrisObj.transform.localScale = new Vector3(s, s, 1f);

                var debris = debrisObj.AddComponent<DebrisParticle>();
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float speed = Random.Range(2f, 5f) * (isBoss ? 1.5f : 1f);
                debris.Init(
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * speed,
                    Random.Range(0.3f, 0.6f),
                    Random.Range(-360f, 360f)
                );
            }
        }

        /// <summary>触发屏幕震动</summary>
        private static void TriggerScreenShake(float intensity, float duration)
        {
            var cam = Camera.main;
            if (cam == null) return;

            var shaker = cam.GetComponent<CameraShake>();
            if (shaker == null)
                shaker = cam.gameObject.AddComponent<CameraShake>();
            shaker.Shake(intensity, duration);
        }

        private IEnumerator DeathAnimation(bool isBoss, EnemyType enemyType)
        {
            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 9;

            Color deathColor = GetDeathColor(enemyType);
            sr.color = deathColor;

            float duration = isBoss ? 1.0f : 0.5f;
            float maxScale = isBoss ? 2.5f : 1.2f;
            float elapsed = 0f;

            sr.sprite = CreateDeathSprite();

            // 同时创建扩散冲击波环
            if (isBoss)
            {
                var ringObj = new GameObject("DeathRing");
                ringObj.transform.position = transform.position;
                ringObj.AddComponent<DeathShockwaveRing>().Init(deathColor, 3f, 0.5f);
            }

            while (elapsed < duration)
            {
                float t = elapsed / duration;

                // 更丰富的缩放曲线：快速膨胀→短暂停留→缩小消失
                float scale;
                if (t < 0.15f)
                {
                    scale = Mathf.Lerp(0.3f, maxScale, t / 0.15f);
                }
                else if (t < 0.3f)
                {
                    // 短暂停留并轻微脉动
                    scale = maxScale * (1f + Mathf.Sin((t - 0.15f) / 0.15f * Mathf.PI) * 0.1f);
                }
                else
                {
                    scale = Mathf.Lerp(maxScale, 0f, (t - 0.3f) / 0.7f);
                }
                transform.localScale = Vector3.one * scale;

                // 颜色渐变：从亮色到暗色
                float alpha = (1f - t) * 0.7f;
                float brightness = 1f + (1f - t) * 0.3f; // 初始更亮
                sr.color = new Color(
                    deathColor.r * brightness,
                    deathColor.g * brightness,
                    deathColor.b * brightness,
                    alpha
                );

                elapsed += Time.deltaTime;
                yield return null;
            }

            Destroy(gameObject);
        }


        private static Color GetDeathColor(EnemyType type)

        {
            switch (type)
            {
                case EnemyType.BossDragon: return new Color(1f, 0.3f, 0.1f);    // 火红
                case EnemyType.BossGiant: return new Color(0.6f, 0.5f, 0.3f);    // 岩石色
                case EnemyType.Slime: return new Color(0.3f, 1f, 0.3f);           // 绿色
                case EnemyType.Rogue: return new Color(0.5f, 0.3f, 0.7f);         // 暗紫
                default: return new Color(0.8f, 0.8f, 0.8f);                       // 默认灰白
            }
        }

        private static Sprite _deathSprite;
        private static Sprite CreateDeathSprite()

        {
            if (_deathSprite != null) return _deathSprite;

            int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(1f - dist / center);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _deathSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _deathSprite;
        }
    }

    // ====================================================================
    // 碎片粒子组件
    // ====================================================================

    /// <summary>碎片粒子 — 带重力和旋转的飞散碎片</summary>
    public class DebrisParticle : MonoBehaviour
    {
        private Vector3 _velocity;
        private float _lifetime;
        private float _timer;
        private float _rotSpeed;
        private SpriteRenderer _sr;

        public void Init(Vector3 velocity, float lifetime, float rotSpeed)
        {
            _velocity = velocity;
            _lifetime = lifetime;
            _timer = lifetime;
            _rotSpeed = rotSpeed;
            _sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // 带重力的运动
            _velocity.y -= 8f * Time.deltaTime; // 重力
            _velocity *= 0.97f; // 空气阻力
            transform.position += _velocity * Time.deltaTime;
            transform.Rotate(0, 0, _rotSpeed * Time.deltaTime);

            // 淡出 + 缩小
            float t = _timer / _lifetime;
            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = t * 0.9f;
                _sr.color = c;
            }
            transform.localScale *= (1f - Time.deltaTime * 1.5f);
        }
    }

    // ====================================================================
    // 死亡冲击波环
    // ====================================================================

    /// <summary>死亡冲击波环 — Boss死亡时的扩散环</summary>
    public class DeathShockwaveRing : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _maxScale;
        private float _duration;
        private float _timer;
        private Color _baseColor;

        public void Init(Color color, float maxScale, float duration)
        {
            _maxScale = maxScale;
            _duration = duration;
            _timer = 0f;
            _baseColor = color;

            // 创建环形纹理
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float a = Mathf.Abs(d - 0.8f) < 0.15f ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.15f) : 0f;
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _sr.color = color;
            _sr.sortingOrder = 14;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = _timer / _duration;
            if (t >= 1f) { Destroy(gameObject); return; }

            float easeT = 1f - (1f - t) * (1f - t);
            float scale = Mathf.Lerp(0.3f, _maxScale, easeT);
            transform.localScale = Vector3.one * scale;

            Color c = _baseColor;
            c.a = _baseColor.a * (1f - t) * 0.7f;
            _sr.color = c;
        }
    }

    // ====================================================================
    // 屏幕震动组件
    // ====================================================================

    /// <summary>相机震动组件</summary>
    public class CameraShake : MonoBehaviour
    {
        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeTimer;
        private Vector3 _originalPos;
        private bool _isShaking;

        public void Shake(float intensity, float duration)
        {
            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeTimer = duration;
            if (!_isShaking)
            {
                _originalPos = transform.localPosition;
                _isShaking = true;
            }
        }

        private void LateUpdate()
        {
            if (!_isShaking) return;

            _shakeTimer -= Time.deltaTime;
            if (_shakeTimer <= 0f)
            {
                _isShaking = false;
                transform.localPosition = _originalPos;
                return;
            }

            float decay = _shakeTimer / _shakeDuration;
            float x = Random.Range(-_shakeIntensity, _shakeIntensity) * decay;
            float y = Random.Range(-_shakeIntensity, _shakeIntensity) * decay;
            transform.localPosition = _originalPos + new Vector3(x, y, 0f);
        }
    }

    // ====================================================================
    // 怪物血条UI组件
    // ====================================================================

    /// <summary>
    /// 怪物头顶血条 — 世界空间内的简单血条
    /// 增强：延迟伤害条（白色条延迟跟随红色条）
    /// </summary>
    public class EnemyHealthBar : MonoBehaviour
    {
        private SpriteRenderer _bgBar;
        private SpriteRenderer _hpBar;
        private SpriteRenderer _delayBar; // 延迟伤害条
        private SpriteRenderer _shieldBar;

        private EnemyBase _enemy;

        private static readonly Color HealthColor = new Color(0.2f, 0.9f, 0.2f);
        private static readonly Color LowHealthColor = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color DelayBarColor = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color ShieldColor = new Color(0.5f, 0.8f, 1f, 0.8f);
        private static readonly Color BgColor = new Color(0.15f, 0.15f, 0.15f, 0.75f);

        private const float BarWidth = 0.6f;
        private const float BarHeight = 0.06f;
        private const float BarOffsetY = 0.5f;

        // 延迟条追踪
        private float _displayedHP = 1f;
        private float _delayedHP = 1f;
        private const float DELAY_SPEED = 2f; // 延迟条追赶速度

        /// <summary>初始化血条</summary>
        public void Initialize(EnemyBase enemy)
        {
            _enemy = enemy;

            // 背景条
            _bgBar = CreateBarSprite("HealthBarBG", BgColor, 20);
            _bgBar.transform.localScale = new Vector3(BarWidth + 0.02f, BarHeight + 0.02f, 1f);
            _bgBar.transform.localPosition = new Vector3(0, BarOffsetY, 0);

            // 延迟伤害条（白色，在血量条后面）
            _delayBar = CreateBarSprite("HealthBarDelay", DelayBarColor, 21);
            _delayBar.transform.localPosition = new Vector3(0, BarOffsetY, 0);

            // 血量条
            _hpBar = CreateBarSprite("HealthBarHP", HealthColor, 22);
            _hpBar.transform.localPosition = new Vector3(0, BarOffsetY, 0);

            // 护盾条
            _shieldBar = CreateBarSprite("HealthBarShield", ShieldColor, 23);
            _shieldBar.transform.localPosition = new Vector3(0, BarOffsetY + BarHeight + 0.02f, 0);
            _shieldBar.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_enemy == null || _enemy.IsDead)
            {
                gameObject.SetActive(false);
                return;
            }

            // 当前血量百分比
            float hpPercent = _enemy.HPPercent;
            _displayedHP = hpPercent;

            // 延迟条平滑追赶
            if (_delayedHP > _displayedHP)
            {
                _delayedHP -= DELAY_SPEED * Time.deltaTime;
                _delayedHP = Mathf.Max(_delayedHP, _displayedHP);
            }
            else
            {
                _delayedHP = _displayedHP;
            }

            // 更新血量条
            _hpBar.transform.localScale = new Vector3(BarWidth * hpPercent, BarHeight, 1f);
            float hpOffset = -BarWidth * (1f - hpPercent) / 2f;
            _hpBar.transform.localPosition = new Vector3(hpOffset, BarOffsetY, 0);

            // 更新延迟条
            _delayBar.transform.localScale = new Vector3(BarWidth * _delayedHP, BarHeight, 1f);
            float delayOffset = -BarWidth * (1f - _delayedHP) / 2f;
            _delayBar.transform.localPosition = new Vector3(delayOffset, BarOffsetY, 0);

            // 血量颜色（低血量时渐变到红色）
            if (hpPercent > 0.5f)
                _hpBar.color = HealthColor;
            else if (hpPercent > 0.25f)
                _hpBar.color = Color.Lerp(LowHealthColor, HealthColor, (hpPercent - 0.25f) / 0.25f);
            else
                _hpBar.color = LowHealthColor;

            // 护盾条
            float shield = _enemy.Buffs?.ShieldAmount ?? 0f;
            if (shield > 0f)
            {
                _shieldBar.gameObject.SetActive(true);
                float shieldPercent = Mathf.Clamp01(shield / _enemy.MaxHP);
                _shieldBar.transform.localScale = new Vector3(BarWidth * shieldPercent, BarHeight * 0.5f, 1f);
            }
            else
            {
                _shieldBar.gameObject.SetActive(false);
            }
        }


        private SpriteRenderer CreateBarSprite(string name, Color color, int sortingOrder)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform);
            obj.transform.localPosition = Vector3.zero;

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = CreatePixelSprite();
            sr.color = color;
            sr.sortingOrder = sortingOrder;

            return sr;
        }

        private static Sprite _barSprite;
        /// <summary>
        /// 创建带圆角和柔边的血条Sprite（消除锯齿）
        /// 使用较大的纹理 + Bilinear过滤，边缘自然平滑
        /// </summary>
        private Sprite CreatePixelSprite()
        {
            if (_barSprite != null) return _barSprite;

            // 使用32x32的纹理，PPU=32 → 世界尺寸1x1，与原有scale逻辑兼容
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

            float cornerRadius = 4f; // 圆角半径（像素）
            float edgeSoftness = 1.5f; // 边缘柔化宽度（像素）

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 计算到最近圆角中心的距离
                    float dx = 0f, dy = 0f;

                    // 水平方向：只在左右两端的圆角区域内计算
                    if (x < cornerRadius)
                        dx = cornerRadius - x;
                    else if (x > size - 1 - cornerRadius)
                        dx = x - (size - 1 - cornerRadius);

                    // 垂直方向：只在上下两端的圆角区域内计算
                    if (y < cornerRadius)
                        dy = cornerRadius - y;
                    else if (y > size - 1 - cornerRadius)
                        dy = y - (size - 1 - cornerRadius);

                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // 在圆角区域外用柔边过渡
                    float alpha = 1f - Mathf.Clamp01((dist - cornerRadius) / edgeSoftness);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear; // Bilinear过滤，边缘平滑
            tex.wrapMode = TextureWrapMode.Clamp;

            _barSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _barSprite;
        }


    }

    // ====================================================================
    // 视觉反馈总管理器
    // ====================================================================

    /// <summary>
    /// 怪物视觉反馈管理器 — 监听事件统一管理所有怪物视觉效果
    /// </summary>
    public class EnemyVisualManager : MonoSingleton<EnemyVisualManager>
    {
        protected override void OnInit()
        {
            EventBus.Instance.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged);
            EventBus.Instance.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
            EventBus.Instance.Subscribe<EnemySpawnedEvent>(OnEnemySpawned);

            Logger.I("EnemyVisualMgr", "怪物视觉反馈管理器初始化");
        }

        protected override void OnDispose()
        {
            EventBus.Instance.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
            EventBus.Instance.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
            EventBus.Instance.Unsubscribe<EnemySpawnedEvent>(OnEnemySpawned);
        }

        private void OnEnemySpawned(EnemySpawnedEvent evt)
        {
            // 找到刚生成的怪物，挂载视觉组件
            if (EnemySpawner.HasInstance)
            {
                var enemies = EnemySpawner.Instance.ActiveEnemies;
                for (int i = enemies.Count - 1; i >= 0; i--)
                {
                    if (enemies[i] != null && enemies[i].InstanceId == evt.EnemyId)
                    {
                        EnsureVisualComponents(enemies[i]);
                        break;
                    }
                }
            }
        }

        private void OnEnemyDamaged(EnemyDamagedEvent evt)
        {
            // 查找对应的怪物并触发闪白
            if (EnemySpawner.HasInstance)
            {
                var enemies = EnemySpawner.Instance.ActiveEnemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i] != null && enemies[i].InstanceId == evt.EnemyId)
                    {
                        var flash = enemies[i].GetComponent<EnemyHitFlash>();
                        if (flash != null)
                        {
                            flash.TriggerHitFlash();

                            // ★ 受击方向冲击波
                            flash.TriggerHitDirection(evt.Position - Vector3.up * 0.5f);

                            if (evt.IsCritical)
                            {
                                flash.TriggerHitShake(0.1f);
                                // ★ 暴击星星特效
                                flash.TriggerCritEffect();
                            }
                        }

                        // ★ 触发受击方向倾斜（通过EnemyVisualAnimator）
                        var animator = enemies[i].GetComponent<EnemyVisualAnimator>();
                        if (animator != null)
                        {
                            Vector3 hitDir = enemies[i].transform.position - (evt.Position - Vector3.up * 0.5f);
                            animator.TriggerHitTilt(hitDir);
                        }

                        // 伤害飘字已由 DamagePopupManager 统一处理，此处不重复

                        break;

                    }
                }
            }
        }



        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            // 播放死亡效果
            EnemyDeathEffect.Play(evt.Position, evt.IsBoss, evt.EnemyType);
        }

        /// <summary>确保怪物有视觉组件</summary>
        private void EnsureVisualComponents(EnemyBase enemy)
        {
            if (enemy.GetComponent<EnemyHitFlash>() == null)
            {
                enemy.gameObject.AddComponent<EnemyHitFlash>();
            }

            if (enemy.GetComponent<EnemyHealthBar>() == null)
            {
                var healthBar = enemy.gameObject.AddComponent<EnemyHealthBar>();
                healthBar.Initialize(enemy);
            }

            // ★ Boss出场特效
            if (enemy.IsBoss)
            {
                BossEntranceEffect.Play(enemy.transform.position, enemy.Type);
            }
        }
    }

    // ====================================================================
    // ★ Boss出场特效
    // ====================================================================

    /// <summary>Boss出场特效 — 震撼的入场动画</summary>
    public class BossEntranceEffect : MonoBehaviour
    {
        public static void Play(Vector3 position, EnemyType bossType)
        {
            var obj = new GameObject("BossEntrance");
            obj.transform.position = position;
            var effect = obj.AddComponent<BossEntranceEffect>();
            effect.StartCoroutine(effect.EntranceAnimation(bossType));

            // 屏幕震动
            var cam = Camera.main;
            if (cam != null)
            {
                var shaker = cam.GetComponent<CameraShake>();
                if (shaker == null)
                    shaker = cam.gameObject.AddComponent<CameraShake>();
                shaker.Shake(0.12f, 0.6f);
            }
        }

        private IEnumerator EntranceAnimation(EnemyType bossType)
        {
            Color bossColor = bossType == EnemyType.BossDragon
                ? new Color(1f, 0.3f, 0.05f, 0.6f)
                : new Color(0.7f, 0.5f, 0.2f, 0.6f);

            // 创建光柱
            var pillarObj = new GameObject("BossPillar");
            pillarObj.transform.position = transform.position;
            var pillarSr = pillarObj.AddComponent<SpriteRenderer>();
            pillarSr.sprite = CreatePillarSprite();
            pillarSr.sortingOrder = 15;
            pillarSr.color = bossColor;
            pillarObj.transform.localScale = new Vector3(0.8f, 4f, 1f);

            // 创建地面冲击波
            var waveObj = new GameObject("BossWave");
            waveObj.transform.position = transform.position;
            var waveSr = waveObj.AddComponent<SpriteRenderer>();
            waveSr.sprite = CreateWaveSprite();
            waveSr.sortingOrder = 14;
            waveSr.color = bossColor;
            waveObj.transform.localScale = Vector3.one * 0.5f;

            float duration = 0.8f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;

                // 光柱淡出 + 缩窄
                float pillarAlpha = bossColor.a * (1f - t);
                float pillarWidth = 0.8f * (1f - t * 0.7f);
                pillarObj.transform.localScale = new Vector3(pillarWidth, 4f, 1f);
                pillarSr.color = new Color(bossColor.r, bossColor.g, bossColor.b, pillarAlpha);

                // 冲击波扩散
                float waveScale = 0.5f + t * 3f;
                waveObj.transform.localScale = new Vector3(waveScale, waveScale * 0.4f, 1f);
                waveSr.color = new Color(bossColor.r, bossColor.g, bossColor.b, bossColor.a * (1f - t) * 0.5f);

                elapsed += Time.deltaTime;
                yield return null;
            }

            Destroy(pillarObj);
            Destroy(waveObj);
            Destroy(gameObject);
        }

        private static Sprite _pillarSprite;
        private static Sprite CreatePillarSprite()
        {
            if (_pillarSprite != null) return _pillarSprite;

            int w = 16, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float centerX = w / 2f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float hDist = Mathf.Abs(x - centerX) / centerX;
                    float hFade = Mathf.Clamp01(1f - hDist * hDist);
                    float vFade = (float)y / h; // 底部亮，顶部暗
                    float alpha = hFade * (1f - vFade * vFade) * 0.8f;
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _pillarSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 16);
            return _pillarSprite;
        }

        private static Sprite _waveSprite;
        private static Sprite CreateWaveSprite()
        {
            if (_waveSprite != null) return _waveSprite;

            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    // 环形：在d=0.7~0.9区间有亮度
                    float ring = Mathf.Abs(d - 0.8f) < 0.15f ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.15f) : 0f;
                    // 内部填充
                    float fill = d < 0.7f ? Mathf.Clamp01(1f - d / 0.7f) * 0.3f : 0f;
                    float alpha = Mathf.Max(ring, fill);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _waveSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _waveSprite;
        }
    }
}

