// ============================================================
// 文件名：DamagePopup.cs
// 功能描述：伤害飘字系统 — 普通伤害/暴击/治疗/Miss
//          含动画和对象池回收
// 创建时间：2026-03-25
// 所属模块：Battle/Visual
// 对应交互：阶段三 #149
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Tower;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Visual

{
    /// <summary>飘字类型</summary>
    public enum PopupType
    {
        Normal,     // 普通伤害（白色）
        Critical,   // 暴击（红色放大）
        Heal,       // 治疗（绿色）
        Miss,       // 闪避（灰色"Miss"）
        Gold,       // 金币（金色）
        Poison      // 中毒（紫色）
    }

    /// <summary>
    /// 单个飘字实体
    /// </summary>
    public class DamagePopupItem : MonoBehaviour, IPoolable
    {
        // ========== 配置 ==========

        private TextMesh _textMesh;
        private float _lifetime;
        private float _timer;
        private float _floatSpeed = 1.5f;
        private float _fadeSpeed = 2f;
        private Vector3 _initialScale;
        private Color _color;
        private bool _isActive = false;

        // ========== 初始化 ==========

        /// <summary>
        /// 初始化飘字
        /// </summary>
        public void Setup(string text, Vector3 position, PopupType type)
        {
            EnsureTextMesh();

            transform.position = position;
            _textMesh.text = text;
            _timer = 0f;
            _isActive = true;

            // 根据类型设置外观
            switch (type)
            {
                case PopupType.Normal:
                    _color = Color.white;
                    _lifetime = 0.8f;
                    _floatSpeed = 1.5f;
                    _initialScale = Vector3.one * 0.15f;
                    break;

                case PopupType.Critical:
                    _color = new Color(1f, 0.2f, 0.1f);
                    _lifetime = 1.0f;
                    _floatSpeed = 2f;
                    _initialScale = Vector3.one * 0.25f; // 暴击更大
                    break;

                case PopupType.Heal:
                    _color = new Color(0.2f, 1f, 0.3f);
                    _lifetime = 0.8f;
                    _floatSpeed = 1.2f;
                    _initialScale = Vector3.one * 0.15f;
                    break;

                case PopupType.Miss:
                    _color = new Color(0.6f, 0.6f, 0.6f);
                    _lifetime = 0.6f;
                    _floatSpeed = 1f;
                    _initialScale = Vector3.one * 0.12f;
                    break;

                case PopupType.Gold:
                    _color = new Color(1f, 0.85f, 0.2f);
                    _lifetime = 1.0f;
                    _floatSpeed = 1.5f;
                    _initialScale = Vector3.one * 0.15f;
                    break;

                case PopupType.Poison:
                    _color = new Color(0.7f, 0.2f, 1f);
                    _lifetime = 0.6f;
                    _floatSpeed = 1f;
                    _initialScale = Vector3.one * 0.12f;
                    break;
            }

            _textMesh.color = _color;
            transform.localScale = _initialScale;

            // 添加随机水平偏移避免重叠
            float offsetX = Random.Range(-0.3f, 0.3f);
            transform.position += new Vector3(offsetX, 0, 0);
        }

        private void Update()
        {
            if (!_isActive) return;

            _timer += Time.deltaTime;

            // 向上漂浮
            transform.position += Vector3.up * _floatSpeed * Time.deltaTime;

            // 先放大后缩小（弹跳效果）
            float t = _timer / _lifetime;
            float scaleMultiplier;
            if (t < 0.2f)
            {
                // 放大阶段
                scaleMultiplier = 1f + (t / 0.2f) * 0.3f;
            }
            else
            {
                // 缩小+淡出阶段
                scaleMultiplier = 1.3f - (t - 0.2f) / 0.8f * 0.5f;
            }
            transform.localScale = _initialScale * Mathf.Max(scaleMultiplier, 0.1f);

            // 淡出
            if (t > 0.5f)
            {
                float alpha = 1f - (t - 0.5f) / 0.5f;
                var c = _color;
                c.a = Mathf.Max(alpha, 0f);
                _textMesh.color = c;
            }

            // 生命结束
            if (_timer >= _lifetime)
            {
                _isActive = false;
                ReturnToPool();
            }
        }

        private void EnsureTextMesh()
        {
            if (_textMesh == null)
            {
                _textMesh = GetComponent<TextMesh>();
                if (_textMesh == null)
                {
                    _textMesh = gameObject.AddComponent<TextMesh>();
                    _textMesh.alignment = TextAlignment.Center;
                    _textMesh.anchor = TextAnchor.MiddleCenter;
                    _textMesh.fontSize = 48;
                    _textMesh.characterSize = 0.1f;
                    _textMesh.fontStyle = FontStyle.Bold;

                    var mr = GetComponent<MeshRenderer>();
                    if (mr != null) mr.sortingOrder = 100;
                }
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
            _isActive = false;
            _timer = 0f;
        }

        public void OnDespawn()
        {
            _isActive = false;
        }
    }

    // ====================================================================
    // DamagePopupManager
    // ====================================================================

    /// <summary>
    /// 飘字管理器 — 监听伤害事件自动创建飘字
    /// </summary>
    public class DamagePopupManager : MonoSingleton<DamagePopupManager>
    {
        /// <summary>飘字预制体（运行时动态创建）</summary>
        private GameObject _popupPrefab;

        protected override void OnInit()
        {
            // 创建飘字预制体
            _popupPrefab = new GameObject("DamagePopup_Template");
            _popupPrefab.SetActive(false);
            _popupPrefab.transform.SetParent(transform);
            _popupPrefab.AddComponent<DamagePopupItem>();

            // 预热对象池
            if (ObjectPoolManager.HasInstance)
            {
                ObjectPoolManager.Instance.CreatePool(_popupPrefab, initialSize: 20, maxSize: 50);
            }

            // 订阅伤害事件
            EventBus.Instance.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged);
            EventBus.Instance.Subscribe<GoldChangedEvent>(OnGoldChanged);

            Logger.I("DamagePopup", "飘字系统初始化");
        }

        protected override void OnDispose()
        {
            EventBus.Instance.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
            EventBus.Instance.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
        }

        // ========== 公共方法 ==========

        /// <summary>
        /// 手动创建飘字
        /// </summary>
        public void ShowPopup(string text, Vector3 position, PopupType type)
        {
            GameObject obj;
            if (ObjectPoolManager.HasInstance)
            {
                obj = ObjectPoolManager.Instance.Get(_popupPrefab, position);
            }
            else
            {
                obj = Instantiate(_popupPrefab, position, Quaternion.identity);
                obj.SetActive(true);
            }

            var popup = obj.GetComponent<DamagePopupItem>();
            if (popup != null)
            {
                popup.Setup(text, position, type);
            }
        }

        // ========== 事件处理 ==========

        private void OnEnemyDamaged(EnemyDamagedEvent evt)
        {
            if (evt.IsDodged)
            {
                ShowPopup("Miss", evt.Position, PopupType.Miss);
            }
            else if (evt.IsCritical)
            {
                ShowPopup($"{evt.Damage:F0}!", evt.Position, PopupType.Critical);
            }
            else if (evt.DamageType == DamageType.True)
            {
                ShowPopup($"{evt.Damage:F0}", evt.Position, PopupType.Poison);
            }
            else
            {
                ShowPopup($"{evt.Damage:F0}", evt.Position, PopupType.Normal);
            }
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
            // 只对击杀金币显示飘字（其他来源不显示）
            if (evt.Delta > 0 && evt.Reason.Contains("击杀"))
            {
                // 从事件中获取位置（简化处理，后续可改进）
                // 暂时不显示金币飘字（需要位置信息）
            }
        }
    }
}
