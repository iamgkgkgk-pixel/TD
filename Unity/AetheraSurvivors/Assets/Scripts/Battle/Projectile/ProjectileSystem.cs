// ============================================================
// 文件名：ProjectileSystem.cs
// 功能描述：投射物系统 — 直线弹、追踪弹、抛物线弹、激光
//          含命中检测、对象池回收
// 创建时间：2026-03-25
// 所属模块：Battle/Projectile
// 对应交互：阶段三 #128
// ============================================================

using System;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Tower;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Projectile

{
    /// <summary>投射物类型</summary>
    public enum ProjectileType
    {
        /// <summary>直线弹（沿发射方向直线飞行）</summary>
        Straight,
        /// <summary>追踪弹（持续追踪目标）</summary>
        Homing,
        /// <summary>抛物线弹（弧线飞行，适合炮塔）</summary>
        Parabolic,
        /// <summary>激光（即时命中，无飞行时间）</summary>
        Instant
    }

    /// <summary>
    /// 投射物基类 — 所有飞行物的核心逻辑
    /// </summary>
    public class ProjectileBase : MonoBehaviour, IPoolable
    {
        // ========== 配置 ==========

        [Header("投射物配置")]
        [SerializeField] protected ProjectileType _type = ProjectileType.Straight;
        [SerializeField] protected float _speed = 10f;
        [SerializeField] protected float _maxLifetime = 5f;

        // ========== 运行时数据 ==========

        /// <summary>伤害信息</summary>
        protected DamageInfo _damageInfo;

        /// <summary>目标Transform</summary>
        protected Transform _target;

        /// <summary>目标最后已知位置（目标死亡后使用）</summary>
        protected Vector3 _targetLastPos;

        /// <summary>发射位置</summary>
        protected Vector3 _startPos;

        /// <summary>生存计时器</summary>
        protected float _lifeTimer;

        /// <summary>是否已激活</summary>
        protected bool _isActive = false;

        /// <summary>飞行方向（直线弹用）</summary>
        protected Vector3 _direction;

        /// <summary>抛物线起始高度（抛物线弹用）</summary>
        protected float _arcHeight = 2f;

        /// <summary>抛物线飞行进度（0~1）</summary>
        protected float _arcProgress = 0f;

        // ========== 公共方法 ==========

        /// <summary>
        /// 发射投射物
        /// </summary>
        /// <param name="startPos">发射位置</param>
        /// <param name="target">目标</param>
        /// <param name="damageInfo">伤害信息</param>
        /// <param name="speed">飞行速度</param>
        public virtual void Launch(Vector3 startPos, Transform target, DamageInfo damageInfo, float speed = 0f)
        {
            _startPos = startPos;
            _target = target;
            _targetLastPos = target != null ? target.position : startPos + Vector3.right;
            _damageInfo = damageInfo;
            _speed = speed > 0 ? speed : _speed;
            _lifeTimer = _maxLifetime;
            _isActive = true;
            _arcProgress = 0f;

            transform.position = startPos;

            // 计算初始方向
            _direction = (_targetLastPos - startPos).normalized;

            // 旋转朝向目标
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        // ========== Unity生命周期 ==========

        protected virtual void Update()
        {
            if (!_isActive) return;

            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                OnMiss();
                return;
            }

            // 更新目标位置
            if (_target != null && _target.gameObject.activeInHierarchy)
            {
                _targetLastPos = _target.position;
            }

            // 根据类型移动
            switch (_type)
            {
                case ProjectileType.Straight:
                    MoveStraight();
                    break;
                case ProjectileType.Homing:
                    MoveHoming();
                    break;
                case ProjectileType.Parabolic:
                    MoveParabolic();
                    break;
                case ProjectileType.Instant:
                    // 即时命中，不移动
                    OnHit();
                    break;
            }
        }

        // ========== 移动逻辑 ==========

        /// <summary>直线飞行</summary>
        protected virtual void MoveStraight()
        {
            transform.position += _direction * _speed * Time.deltaTime;

            // 检测距离命中
            float distSqr = (transform.position - _targetLastPos).sqrMagnitude;
            if (distSqr < 0.15f * 0.15f)
            {
                OnHit();
            }
        }

        /// <summary>追踪飞行</summary>
        protected virtual void MoveHoming()
        {
            Vector3 toTarget = _targetLastPos - transform.position;
            _direction = toTarget.normalized;

            // 旋转朝向
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            transform.position += _direction * _speed * Time.deltaTime;

            // 检测命中
            if (toTarget.sqrMagnitude < 0.15f * 0.15f)
            {
                OnHit();
            }
        }

        /// <summary>抛物线飞行</summary>
        protected virtual void MoveParabolic()
        {
            float totalDist = Vector3.Distance(_startPos, _targetLastPos);
            float flightTime = totalDist / _speed;

            _arcProgress += Time.deltaTime / Mathf.Max(flightTime, 0.1f);
            _arcProgress = Mathf.Clamp01(_arcProgress);

            // 线性插值水平位置
            Vector3 flatPos = Vector3.Lerp(_startPos, _targetLastPos, _arcProgress);

            // 抛物线垂直偏移
            float arcOffset = _arcHeight * 4f * _arcProgress * (1f - _arcProgress);
            flatPos.y += arcOffset;

            // 旋转朝向飞行方向
            Vector3 moveDir = flatPos - transform.position;
            if (moveDir.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }

            transform.position = flatPos;

            // 到达终点
            if (_arcProgress >= 1f)
            {
                OnHit();
            }
        }

        // ========== 命中/未命中 ==========

        /// <summary>来源塔类型（用于命中特效）</summary>
        protected TowerType _sourceTowerType;

        /// <summary>设置来源塔类型</summary>
        public void SetSourceTowerType(TowerType type) { _sourceTowerType = type; }

        /// <summary>设置投射物飞行类型</summary>
        public void SetProjectileType(ProjectileType type) { _type = type; }

        /// <summary>命中目标</summary>

        protected virtual void OnHit()
        {
            _isActive = false;

            // 播放命中特效
            if (HitEffectSystem.HasInstance)
            {
                HitEffectSystem.Instance.PlayHitEffect(_targetLastPos, _sourceTowerType, _damageInfo.IsCritical);

            }

            // 对目标造伤
            if (_target != null && _target.gameObject.activeInHierarchy)
            {
                var enemy = _target.GetComponent<Enemy.EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    enemy.TakeDamage(_damageInfo);
                }
            }

            // 回收到对象池
            ReturnToPool();
        }


        /// <summary>未命中（超时/目标消失）</summary>
        protected virtual void OnMiss()
        {
            _isActive = false;
            ReturnToPool();
        }

        /// <summary>回收到对象池</summary>
        protected void ReturnToPool()
        {
            // 程序化创建的投射物直接销毁（包含TrailRenderer和子对象）
            Destroy(gameObject);
        }


        // ========== IPoolable ==========

        public virtual void OnSpawn()
        {
            _isActive = false;
            _target = null;
            _lifeTimer = _maxLifetime;
        }

        public virtual void OnDespawn()
        {
            _isActive = false;
            _target = null;
        }
    }

    // ====================================================================
    // 投射物工厂
    // ====================================================================

    /// <summary>
    /// 投射物管理器 — 创建和管理投射物
    /// </summary>
    public class ProjectileManager : MonoSingleton<ProjectileManager>
    {
        [Header("投射物预制体")]
        [SerializeField] private GameObject _arrowPrefab;
        [SerializeField] private GameObject _magicBoltPrefab;
        [SerializeField] private GameObject _cannonBallPrefab;
        [SerializeField] private GameObject _poisonBoltPrefab;

        /// <summary>默认投射物预制体（开发阶段）</summary>
        private GameObject _defaultProjectilePrefab;

        protected override void OnInit()
        {
            // 创建默认投射物预制体
            _defaultProjectilePrefab = new GameObject("DefaultProjectile_Template");
            _defaultProjectilePrefab.SetActive(false);
            _defaultProjectilePrefab.transform.SetParent(transform);
            _defaultProjectilePrefab.AddComponent<ProjectileBase>();
            var sr = _defaultProjectilePrefab.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;

            // 创建默认sprite — 优先加载真实美术资源
            Sprite arrowSprite = SpriteLoader.LoadProjectile("arrow");
            if (arrowSprite != null)
            {
                sr.sprite = arrowSprite;
                sr.color = Color.white;
            }
            else
            {
                // 无真实资源时使用占位（2x2白色像素）
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                tex.Apply();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 8f);
            }


            Logger.I("ProjectileManager", "投射物管理器初始化");
        }

        protected override void OnDispose()
        {
            Logger.I("ProjectileManager", "投射物管理器已销毁");
        }

        /// <summary>
        /// 发射投射物（带视觉效果）
        /// </summary>
        /// <param name="towerType">来源塔类型（决定投射物外观和命中特效）</param>
        public ProjectileBase Fire(ProjectileType type, Vector3 startPos, Transform target, DamageInfo damageInfo, float speed = 10f, TowerType towerType = TowerType.Archer)
        {
            // 创建投射物对象（不使用预制体，纯程序化生成）
            var obj = new GameObject($"Projectile_{towerType}");
            obj.transform.position = startPos;
            obj.SetActive(true);

            var projectile = obj.AddComponent<ProjectileBase>();
            projectile.SetSourceTowerType(towerType);
            projectile.SetProjectileType(type);

            // 应用程序化视觉效果

            ProjectileVisualFactory.ApplyVisual(obj, towerType, speed);

            projectile.Launch(startPos, target, damageInfo, speed);
            return projectile;
        }


        /// <summary>
        /// 即时命中（激光/闪电等无飞行时间的攻击）
        /// </summary>
        public void InstantHit(Transform target, DamageInfo damageInfo)
        {
            if (target == null) return;

            var enemy = target.GetComponent<Enemy.EnemyBase>();
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(damageInfo);
            }
        }

        private GameObject GetPrefab(ProjectileType type)
        {
            switch (type)
            {
                case ProjectileType.Straight:
                    return _arrowPrefab ?? _defaultProjectilePrefab;
                case ProjectileType.Homing:
                    return _magicBoltPrefab ?? _defaultProjectilePrefab;
                case ProjectileType.Parabolic:
                    return _cannonBallPrefab ?? _defaultProjectilePrefab;
                default:
                    return _defaultProjectilePrefab;
            }
        }
    }
}
