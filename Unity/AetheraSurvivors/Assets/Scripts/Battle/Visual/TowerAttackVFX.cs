// ============================================================
// 文件名：TowerAttackVFX.cs
// 功能描述：塔攻击视觉效果系统
//          塔开火闪光、弹道轨迹、命中闪光、射程脉冲
// 创建时间：2026-03-25
// 所属模块：Battle/Visual
// 对应交互：阶段三 #150
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Tower;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Visual

{
    /// <summary>
    /// 塔攻击视觉效果管理器
    /// 
    /// 监听TowerAttackEvent，根据塔类型播放对应的攻击特效：
    /// - 箭塔：箭矢飞行轨迹
    /// - 法塔：魔法弹闪光
    /// - 冰塔：冰晶粒子
    /// - 炮塔：开火火光 + 爆炸
    /// - 毒塔：毒雾喷射
    /// </summary>
    public class TowerAttackVFX : MonoSingleton<TowerAttackVFX>
    {
        // ========== 预制体（运行时动态创建） ==========

        private GameObject _muzzleFlashPrefab;   // 开火闪光
        private GameObject _impactFlashPrefab;   // 命中闪光
        private GameObject _trailPrefab;         // 弹道拖尾

        // ========== 配色方案 ==========

        /// <summary>各塔类型对应的特效颜色</summary>
        private static readonly Color[] TowerColors = new Color[]
        {
            new Color(1f, 0.95f, 0.7f),     // Archer - 暖黄色
            new Color(0.5f, 0.3f, 1f),       // Mage - 紫蓝色
            new Color(0.4f, 0.85f, 1f),      // Ice - 冰蓝色
            new Color(1f, 0.5f, 0.1f),       // Cannon - 橙红色
            new Color(0.3f, 0.9f, 0.2f),     // Poison - 毒绿色
            new Color(1f, 0.85f, 0.2f),      // GoldMine - 金色
        };

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            CreateVFXPrefabs();
            EventBus.Instance.Subscribe<TowerAttackEvent>(OnTowerAttack);
            Logger.I("TowerAttackVFX", "塔攻击视觉系统初始化");
        }

        protected override void OnDispose()
        {
            EventBus.Instance.Unsubscribe<TowerAttackEvent>(OnTowerAttack);
        }

        // ========== 事件处理 ==========

        private void OnTowerAttack(TowerAttackEvent evt)
        {
            Color color = GetTowerColor(evt.TowerType);

            // 1. 开火闪光（保留 — 在塔位置的闪光效果）
            SpawnMuzzleFlash(evt.TowerPos, color);

            // 2. 命中闪光已由HitEffectSystem在投射物命中时自动处理
            //    不再需要延迟模拟弹道飞行时间
        }


        // ========== 特效生成 ==========

        /// <summary>生成开火闪光</summary>
        private void SpawnMuzzleFlash(Vector3 position, Color color)
        {
            var obj = GetVFXObject(_muzzleFlashPrefab, position);
            if (obj == null) return;

            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = color;
            }

            // 随机旋转
            obj.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            // 闪光动画（快速缩放后消失）
            var anim = obj.GetComponent<VFXAutoReturn>();
            if (anim != null)
            {
                anim.Play(0.15f, 1.5f);
            }
        }

        /// <summary>延迟命中效果</summary>
        private System.Collections.IEnumerator DelayedImpact(Vector3 pos, Color color, TowerType type, float delay)
        {
            yield return new WaitForSeconds(delay);
            SpawnImpactFlash(pos, color, type);
        }

        /// <summary>生成命中闪光</summary>
        private void SpawnImpactFlash(Vector3 position, Color color, TowerType type)
        {
            var obj = GetVFXObject(_impactFlashPrefab, position);
            if (obj == null) return;

            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = color;
            }

            float duration = 0.2f;
            float scale = 0.8f;

            // 根据塔类型调整命中效果大小
            switch (type)
            {
                case TowerType.Cannon:
                    scale = 1.5f;      // 炮塔爆炸更大
                    duration = 0.35f;
                    break;
                case TowerType.Mage:
                    scale = 1.2f;      // 法塔魔法命中
                    duration = 0.25f;
                    break;
                case TowerType.Ice:
                    scale = 1.0f;      // 冰塔冰晶
                    duration = 0.3f;
                    break;
            }

            obj.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            var anim = obj.GetComponent<VFXAutoReturn>();
            if (anim != null)
            {
                anim.Play(duration, scale);
            }
        }

        // ========== 公共方法 ==========

        /// <summary>手动播放命中特效（ProjectileSystem使用）</summary>
        public void PlayImpactAt(Vector3 position, TowerType type)
        {
            Color color = GetTowerColor(type);
            SpawnImpactFlash(position, color, type);
        }

        /// <summary>播放AOE爆炸特效</summary>
        public void PlayAOEExplosion(Vector3 position, float radius, Color color)
        {
            var obj = GetVFXObject(_impactFlashPrefab, position);
            if (obj == null) return;

            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(color.r, color.g, color.b, 0.4f);
            }

            var anim = obj.GetComponent<VFXAutoReturn>();
            if (anim != null)
            {
                anim.Play(0.4f, radius * 2f);
            }
        }

        // ========== 工具方法 ==========

        private Color GetTowerColor(TowerType type)
        {
            int idx = (int)type;
            return idx >= 0 && idx < TowerColors.Length ? TowerColors[idx] : Color.white;
        }

        private GameObject GetVFXObject(GameObject prefab, Vector3 position)
        {
            if (ObjectPoolManager.HasInstance)
            {
                return ObjectPoolManager.Instance.Get(prefab, position);
            }

            var obj = Instantiate(prefab, position, Quaternion.identity);
            obj.SetActive(true);
            return obj;
        }

        // ========== 预制体创建 ==========

        private void CreateVFXPrefabs()
        {
            // 开火闪光预制体
            _muzzleFlashPrefab = CreateVFXTemplate("MuzzleFlash_Template", 0.3f, 12);
            // 命中闪光预制体
            _impactFlashPrefab = CreateVFXTemplate("ImpactFlash_Template", 0.4f, 10);
            // 拖尾预制体（保留扩展用）
            _trailPrefab = CreateVFXTemplate("Trail_Template", 0.2f, 8);

            // 预热对象池
            if (ObjectPoolManager.HasInstance)
            {
                ObjectPoolManager.Instance.CreatePool(_muzzleFlashPrefab, initialSize: 10, maxSize: 30);
                ObjectPoolManager.Instance.CreatePool(_impactFlashPrefab, initialSize: 10, maxSize: 30);
            }
        }

        private GameObject CreateVFXTemplate(string name, float size, int sortingOrder)
        {
            var obj = new GameObject(name);
            obj.SetActive(false);
            obj.transform.SetParent(transform);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.sortingOrder = sortingOrder;
            sr.color = Color.white;

            obj.AddComponent<VFXAutoReturn>();

            return obj;
        }

        /// <summary>创建一个简单的圆形Sprite（运行时生成，无需外部资源）</summary>
        private static Sprite _cachedCircleSprite;
        private Sprite CreateCircleSprite()
        {
            if (_cachedCircleSprite != null) return _cachedCircleSprite;

            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float radius = center - 1;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(1f - (dist - radius + 2f) / 2f);
                    // 中心更亮，边缘渐变
                    float brightness = Mathf.Clamp01(1f - dist / radius);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * brightness));
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;

            _cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedCircleSprite;
        }
    }

    // ====================================================================
    // VFX自动回收组件
    // ====================================================================

    /// <summary>
    /// VFX自动回收 — 播放缩放+淡出动画后自动回收到对象池
    /// </summary>
    public class VFXAutoReturn : MonoBehaviour, IPoolable
    {
        private SpriteRenderer _sr;
        private float _duration;
        private float _timer;
        private float _maxScale;
        private bool _isPlaying = false;
        private Color _originalColor;

        public void Play(float duration, float maxScale = 1f)
        {
            _sr = GetComponent<SpriteRenderer>();
            _duration = duration;
            _maxScale = maxScale;
            _timer = 0f;
            _isPlaying = true;
            _originalColor = _sr != null ? _sr.color : Color.white;
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            if (!_isPlaying) return;

            _timer += Time.deltaTime;
            float t = _timer / _duration;

            if (t >= 1f)
            {
                _isPlaying = false;
                ReturnToPool();
                return;
            }

            // 快速放大 + 缓慢缩小
            float scaleT;
            if (t < 0.3f)
            {
                // 快速膨胀
                scaleT = Mathf.SmoothStep(0f, 1f, t / 0.3f);
            }
            else
            {
                // 缓慢缩小
                scaleT = Mathf.Lerp(1f, 0.3f, (t - 0.3f) / 0.7f);
            }
            transform.localScale = Vector3.one * _maxScale * scaleT;

            // 淡出
            if (_sr != null && t > 0.4f)
            {
                float alpha = Mathf.Lerp(_originalColor.a, 0f, (t - 0.4f) / 0.6f);
                _sr.color = new Color(_originalColor.r, _originalColor.g, _originalColor.b, alpha);
            }
        }

        private void ReturnToPool()
        {
            if (ObjectPoolManager.HasInstance)
            {
                ObjectPoolManager.Instance.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void OnSpawn()
        {
            _isPlaying = false;
            _timer = 0f;
        }

        public void OnDespawn()
        {
            _isPlaying = false;
        }
    }
}
