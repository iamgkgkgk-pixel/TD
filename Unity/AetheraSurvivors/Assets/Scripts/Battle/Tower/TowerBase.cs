// ============================================================
// 文件名：TowerBase.cs
// 功能描述：塔防塔的基类 — 所有塔类型的核心逻辑
//          放置、升级（3级）、出售、攻击目标选择、射程、冷却
// 创建时间：2026-03-25
// 所属模块：Battle/Tower
// 对应交互：阶段三 #120
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Map;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Performance;
using AetheraSurvivors.Battle.Rune;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Tower

{
    // ====================================================================
    // 枚举与配置定义
    // ====================================================================

    /// <summary>塔的类型枚举</summary>
    public enum TowerType
    {
        Archer = 0,     // 箭塔
        Mage = 1,       // 法塔
        Ice = 2,        // 冰塔
        Cannon = 3,     // 炮塔
        Poison = 4,     // 毒塔
        GoldMine = 5    // 金矿
    }

    /// <summary>攻击目标选择策略</summary>
    public enum TargetStrategy
    {
        /// <summary>最近的目标</summary>
        Nearest,
        /// <summary>最靠前的目标（离基地最近/走得最远）</summary>
        First,
        /// <summary>血量最少的目标</summary>
        LowestHP,
        /// <summary>血量最多的目标（最强）</summary>
        Strongest,
        /// <summary>最后进入射程的目标</summary>
        Last
    }

    /// <summary>
    /// 塔的等级数据（每级属性不同）
    /// </summary>
    [Serializable]
    public class TowerLevelData
    {
        /// <summary>攻击力</summary>
        public float damage = 10f;
        /// <summary>攻击间隔（秒）</summary>
        public float attackInterval = 1f;
        /// <summary>射程（世界单位）</summary>
        public float range = 3f;
        /// <summary>升级所需金币</summary>
        public int upgradeCost = 50;
        /// <summary>出售返还金币</summary>
        public int sellPrice = 25;
        /// <summary>特殊能力描述</summary>
        public string specialAbility = "";
    }

    /// <summary>
    /// 塔的配置数据（驱动塔的所有行为）
    /// </summary>
    [Serializable]
    public class TowerConfig
    {
        /// <summary>塔类型</summary>
        public TowerType towerType;
        /// <summary>塔名称</summary>
        public string displayName = "塔";
        /// <summary>描述</summary>
        public string description = "";
        /// <summary>建造费用</summary>
        public int buildCost = 100;
        /// <summary>最大等级</summary>
        public int maxLevel = 3;
        /// <summary>各等级数据</summary>
        public TowerLevelData[] levelData = new TowerLevelData[3];
        /// <summary>是否可攻击（金矿不攻击）</summary>
        public bool canAttack = true;
        /// <summary>攻击类型标签（物理/魔法/真实）</summary>
        public DamageType damageType = DamageType.Physical;
        /// <summary>是否AOE</summary>
        public bool isAOE = false;
        /// <summary>AOE半径（仅isAOE=true时有效）</summary>
        public float aoeRadius = 0f;
    }

    /// <summary>伤害类型</summary>
    public enum DamageType
    {
        Physical,   // 物理伤害（护甲减伤）
        Magical,    // 魔法伤害（魔抗减伤）
        True        // 真实伤害（无减伤）
    }

    // ====================================================================
    // 塔的事件定义
    // ====================================================================

    /// <summary>塔升级事件</summary>
    public struct TowerUpgradedEvent : IEvent
    {
        public int TowerId;
        public int NewLevel;
        public Vector2Int GridPos;
    }

    /// <summary>塔出售事件</summary>
    public struct TowerSoldEvent : IEvent
    {
        public int TowerId;
        public int RefundGold;
        public Vector2Int GridPos;
    }

    /// <summary>塔攻击事件（用于触发视觉/音效）</summary>
    public struct TowerAttackEvent : IEvent
    {
        public int TowerId;
        public TowerType TowerType;
        public Vector3 TowerPos;
        public Vector3 TargetPos;
        public float Damage;
    }

    // ====================================================================
    // TowerBase 核心基类
    // ====================================================================

    /// <summary>
    /// 塔的基类 — 所有塔类型的核心逻辑
    /// 
    /// 设计原则：
    /// 1. 配置驱动：所有数值通过TowerConfig配置，子类只需重写特殊逻辑
    /// 2. 组件化思维：攻击/特效/动画等通过虚方法扩展
    /// 3. 对象池友好：实现IPoolable接口
    /// </summary>
    public class TowerBase : MonoBehaviour, IPoolable
    {
        // ========== 序列化字段 ==========

        [Header("塔配置")]
        [SerializeField] protected TowerConfig _config;

        [Header("组件引用")]
        [SerializeField] protected Transform _rotateRoot;    // 旋转部分（炮管/弓）
        [SerializeField] protected Transform _firePoint;     // 发射点
        [SerializeField] protected SpriteRenderer _baseSprite;  // 底座Sprite
        [SerializeField] protected SpriteRenderer _topSprite;   // 顶部Sprite（炮管）

        // ========== 运行时数据 ==========

        /// <summary>唯一实例ID</summary>
        private int _instanceId;
        private static int _nextInstanceId = 1;

        /// <summary>当前等级（1-3）</summary>
        protected int _currentLevel = 1;

        /// <summary>所在网格坐标</summary>
        protected Vector2Int _gridPos;

        /// <summary>攻击冷却计时器</summary>
        protected float _attackTimer = 0f;

        /// <summary>当前锁定的目标</summary>
        protected Transform _currentTarget;

        /// <summary>目标选择策略</summary>
        protected TargetStrategy _targetStrategy = TargetStrategy.First;

        /// <summary>是否已初始化</summary>
        protected bool _isInitialized = false;

        /// <summary>射程可视化对象</summary>
        private GameObject _rangeIndicator;
        private bool _showingRange = false;

        // ========== 公共属性 ==========

        /// <summary>实例ID</summary>
        public int InstanceId => _instanceId;

        /// <summary>塔类型</summary>
        public TowerType Type => _config?.towerType ?? TowerType.Archer;

        /// <summary>当前等级</summary>
        public int CurrentLevel => _currentLevel;

        /// <summary>最大等级</summary>
        public int MaxLevel => _config?.maxLevel ?? 3;

        /// <summary>是否满级</summary>
        public bool IsMaxLevel => _currentLevel >= MaxLevel;

        /// <summary>所在网格坐标</summary>
        public Vector2Int GridPos => _gridPos;

        /// <summary>塔配置</summary>
        public TowerConfig Config => _config;

        /// <summary>当前等级数据</summary>
        public TowerLevelData CurrentLevelData
        {
            get
            {
                if (_config?.levelData == null || _currentLevel < 1) return new TowerLevelData();
                int idx = Mathf.Clamp(_currentLevel - 1, 0, _config.levelData.Length - 1);
                return _config.levelData[idx];
            }
        }

        /// <summary>当前攻击力（含词条加成）</summary>
        public float Damage
        {
            get
            {
                float baseDmg = CurrentLevelData.damage;
                if (RuneSystem.HasInstance)
                {
                    baseDmg *= (1f + RuneSystem.Instance.DamageBonus);
                    // 物理/魔法专精加成
                    if (_config != null)
                    {
                        if (_config.damageType == DamageType.Physical)
                            baseDmg *= (1f + RuneSystem.Instance.PhysicalDamageBonus);
                        else if (_config.damageType == DamageType.Magical)
                            baseDmg *= (1f + RuneSystem.Instance.MagicalDamageBonus);
                    }
                }
                return baseDmg;
            }
        }

        /// <summary>当前攻击间隔（含词条加成）</summary>
        public float AttackInterval
        {
            get
            {
                float baseInterval = CurrentLevelData.attackInterval;
                if (RuneSystem.HasInstance && RuneSystem.Instance.AttackSpeedBonus > 0f)
                {
                    baseInterval /= (1f + RuneSystem.Instance.AttackSpeedBonus);
                }
                return Mathf.Max(baseInterval, 0.1f); // 最小攻击间隔0.1秒
            }
        }

        /// <summary>当前射程</summary>
        public float Range => CurrentLevelData.range;

        /// <summary>升级费用（-1表示已满级）</summary>
        public int UpgradeCost => IsMaxLevel ? -1 : CurrentLevelData.upgradeCost;

        /// <summary>出售返还</summary>
        public int SellPrice
        {
            get
            {
                // 出售返还 = 建造费 + 已花升级费的60%
                int totalInvested = _config?.buildCost ?? 0;
                for (int i = 0; i < _currentLevel - 1; i++)
                {
                    if (_config?.levelData != null && i < _config.levelData.Length)
                        totalInvested += _config.levelData[i].upgradeCost;
                }
                return Mathf.RoundToInt(totalInvested * 0.6f);
            }
        }

        /// <summary>目标选择策略</summary>
        public TargetStrategy Strategy
        {
            get => _targetStrategy;
            set => _targetStrategy = value;
        }

        /// <summary>当前目标</summary>
        public Transform CurrentTarget => _currentTarget;

        // ========== 初始化 ==========

        /// <summary>
        /// 初始化塔（放置时调用）
        /// </summary>
        /// <param name="config">塔配置</param>
        /// <param name="gridPos">网格坐标</param>
        public virtual void Initialize(TowerConfig config, Vector2Int gridPos)
        {
            _config = config;
            _gridPos = gridPos;
            _currentLevel = 1;
            _attackTimer = 0f;
            _currentTarget = null;
            _instanceId = _nextInstanceId++;
            _isInitialized = true;

            // 设置世界位置
            if (GridSystem.HasInstance)
            {
                transform.position = GridSystem.Instance.GridToWorld(gridPos);
            }

            OnLevelChanged();

            Logger.D("TowerBase", "塔初始化: {0} Lv{1} @({2},{3}) ID={4}",
                _config.displayName, _currentLevel, gridPos.x, gridPos.y, _instanceId);
        }

        // ========== Unity生命周期 ==========

        protected virtual void Update()
        {
            if (!_isInitialized) return;

            // 攻击冷却
            if (_attackTimer > 0f)
            {
                _attackTimer -= Time.deltaTime;
            }

            // 如果可以攻击
            if (_config != null && _config.canAttack)
            {
                // 检查当前目标是否仍然有效
                if (!IsTargetValid(_currentTarget))
                {
                    _currentTarget = null;
                }

                // 寻找目标
                if (_currentTarget == null)
                {
                    _currentTarget = FindTarget();
                }

                // 旋转朝向目标
                if (_currentTarget != null)
                {
                    RotateTowards(_currentTarget.position);

                    // 攻击
                    if (_attackTimer <= 0f)
                    {
                        PerformAttack(_currentTarget);
                        _attackTimer = AttackInterval;
                    }
                }
            }
        }

        // ========== 核心方法：升级 ==========

        /// <summary>
        /// 升级塔
        /// </summary>
        /// <returns>升级是否成功</returns>
        public virtual bool Upgrade()
        {
            if (IsMaxLevel)
            {
                Logger.W("TowerBase", "升级失败：{0} 已满级", _config?.displayName);
                return false;
            }

            _currentLevel++;
            OnLevelChanged();

            // 发布事件
            EventBus.Instance.Publish(new TowerUpgradedEvent
            {
                TowerId = _instanceId,
                NewLevel = _currentLevel,
                GridPos = _gridPos
            });

            Logger.D("TowerBase", "塔升级: {0} → Lv{1}", _config?.displayName, _currentLevel);
            return true;
        }

        /// <summary>
        /// 出售塔
        /// </summary>
        /// <returns>返还的金币数</returns>
        public virtual int Sell()
        {
            int refund = SellPrice;

            // 从网格系统移除
            if (GridSystem.HasInstance)
            {
                GridSystem.Instance.RemoveTower(_gridPos);
            }

            // 发布事件
            EventBus.Instance.Publish(new TowerSoldEvent
            {
                TowerId = _instanceId,
                RefundGold = refund,
                GridPos = _gridPos
            });

            Logger.D("TowerBase", "塔出售: {0} 返还{1}金币", _config?.displayName, refund);

            // 回收到对象池
            HideRangeIndicator();
            _isInitialized = false;

            if (ObjectPoolManager.HasInstance)
            {
                ObjectPoolManager.Instance.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }

            return refund;
        }

        // ========== 核心方法：攻击 ==========

        /// <summary>
        /// 执行攻击（子类可重写实现不同攻击方式）
        /// </summary>
        /// <param name="target">攻击目标</param>
        protected virtual void PerformAttack(Transform target)
        {
            if (target == null) return;

            // 发布攻击事件
            EventBus.Instance.Publish(new TowerAttackEvent
            {
                TowerId = _instanceId,
                TowerType = Type,
                TowerPos = GetFirePoint(),
                TargetPos = target.position,
                Damage = Damage
            });

            // 基类实现：直接对目标造成伤害（子类应重写为发射投射物等）
            OnAttack(target);
        }

        /// <summary>
        /// 攻击回调（子类重写实现具体攻击逻辑：发射投射物/直接伤害/AOE等）
        /// </summary>
        protected virtual void OnAttack(Transform target)
        {
            // 子类实现
        }

        /// <summary>
        /// 获取发射点位置
        /// </summary>
        protected Vector3 GetFirePoint()
        {
            return _firePoint != null ? _firePoint.position : transform.position;
        }

        // ========== 核心方法：目标选择 ==========

        /// <summary>
        /// 查找攻击目标（根据策略选择）
        /// </summary>
        /// <returns>目标Transform（null=无可攻击目标）</returns>
        protected virtual Transform FindTarget()
        {
            // 获取射程内所有怪物
            var enemiesInRange = GetEnemiesInRange();
            if (enemiesInRange == null || enemiesInRange.Count == 0) return null;

            switch (_targetStrategy)
            {
                case TargetStrategy.Nearest:
                    return FindNearestTarget(enemiesInRange);
                case TargetStrategy.First:
                    return FindFirstTarget(enemiesInRange);
                case TargetStrategy.LowestHP:
                    return FindLowestHPTarget(enemiesInRange);
                case TargetStrategy.Strongest:
                    return FindStrongestTarget(enemiesInRange);
                case TargetStrategy.Last:
                    return FindLastTarget(enemiesInRange);
                default:
                    return FindFirstTarget(enemiesInRange);
            }
        }

        /// <summary>获取射程内的所有怪物（优先使用空间分区，性能安全）</summary>
        protected virtual List<Transform> GetEnemiesInRange()
        {
            var results = new List<Transform>();

            // 计算词条加成后的实际射程
            float effectiveRange = Range;
            if (RuneSystem.HasInstance && RuneSystem.Instance.RangeBonus > 0f)
            {
                effectiveRange *= (1f + RuneSystem.Instance.RangeBonus);
            }
            float rangeSqr = effectiveRange * effectiveRange;

            // 优先使用空间分区系统（O(K) vs O(N)）
            if (SpatialPartition.HasInstance)
            {
                var enemies = SpatialPartition.Instance.QueryRadius(transform.position, effectiveRange);
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i] != null && !enemies[i].IsDead)
                    {
                        results.Add(enemies[i].transform);
                    }
                }
                return results;
            }

            // 回退：使用EnemySpawner的活跃列表（避免FindGameObjectsWithTag）
            if (EnemySpawner.HasInstance)
            {
                var activeEnemies = EnemySpawner.Instance.ActiveEnemies;
                for (int i = 0; i < activeEnemies.Count; i++)
                {
                    var enemy = activeEnemies[i];
                    if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

                    float distSqr = (enemy.transform.position - transform.position).sqrMagnitude;
                    if (distSqr <= rangeSqr)
                    {
                        results.Add(enemy.transform);
                    }
                }
            }

            return results;
        }

        /// <summary>最近目标</summary>
        private Transform FindNearestTarget(List<Transform> enemies)
        {
            Transform nearest = null;
            float minDistSqr = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                float distSqr = (enemies[i].position - transform.position).sqrMagnitude;
                if (distSqr < minDistSqr)
                {
                    minDistSqr = distSqr;
                    nearest = enemies[i];
                }
            }
            return nearest;
        }

        /// <summary>最靠前目标（走得最远的）</summary>
        private Transform FindFirstTarget(List<Transform> enemies)
        {
            // 通过EnemyBase的PathProgress判断（最大的=走得最远）
            Transform first = null;
            float maxProgress = -1f;

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemyComp = enemies[i].GetComponent<Enemy.EnemyBase>();
                if (enemyComp != null)
                {
                    float progress = enemyComp.PathProgress;
                    if (progress > maxProgress)
                    {
                        maxProgress = progress;
                        first = enemies[i];
                    }
                }
                else
                {
                    // 没有EnemyBase组件，用距离基地的距离代替
                    if (first == null) first = enemies[i];
                }
            }
            return first ?? (enemies.Count > 0 ? enemies[0] : null);
        }

        /// <summary>血量最少目标</summary>
        private Transform FindLowestHPTarget(List<Transform> enemies)
        {
            Transform lowest = null;
            float minHP = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemyComp = enemies[i].GetComponent<Enemy.EnemyBase>();
                if (enemyComp != null)
                {
                    if (enemyComp.CurrentHP < minHP)
                    {
                        minHP = enemyComp.CurrentHP;
                        lowest = enemies[i];
                    }
                }
            }
            return lowest ?? (enemies.Count > 0 ? enemies[0] : null);
        }

        /// <summary>血量最多目标</summary>
        private Transform FindStrongestTarget(List<Transform> enemies)
        {
            Transform strongest = null;
            float maxHP = -1f;

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemyComp = enemies[i].GetComponent<Enemy.EnemyBase>();
                if (enemyComp != null)
                {
                    if (enemyComp.CurrentHP > maxHP)
                    {
                        maxHP = enemyComp.CurrentHP;
                        strongest = enemies[i];
                    }
                }
            }
            return strongest ?? (enemies.Count > 0 ? enemies[0] : null);
        }

        /// <summary>最后进入射程的目标</summary>
        private Transform FindLastTarget(List<Transform> enemies)
        {
            Transform last = null;
            float minProgress = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemyComp = enemies[i].GetComponent<Enemy.EnemyBase>();
                if (enemyComp != null)
                {
                    float progress = enemyComp.PathProgress;
                    if (progress < minProgress)
                    {
                        minProgress = progress;
                        last = enemies[i];
                    }
                }
            }
            return last ?? (enemies.Count > 0 ? enemies[0] : null);
        }

        /// <summary>检查目标是否仍然有效（含词条射程加成）</summary>
        protected bool IsTargetValid(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;

            // 检查目标是否已死亡
            var enemyComp = target.GetComponent<Enemy.EnemyBase>();
            if (enemyComp != null && enemyComp.IsDead) return false;

            // 检查是否仍在射程内（与GetEnemiesInRange使用相同的effectiveRange）
            float effectiveRange = Range;
            if (RuneSystem.HasInstance && RuneSystem.Instance.RangeBonus > 0f)
            {
                effectiveRange *= (1f + RuneSystem.Instance.RangeBonus);
            }
            float distSqr = (target.position - transform.position).sqrMagnitude;
            return distSqr <= effectiveRange * effectiveRange;
        }

        // ========== 旋转瞄准 ==========

        /// <summary>旋转塔顶朝向目标</summary>
        protected virtual void RotateTowards(Vector3 targetPos)
        {
            if (_rotateRoot == null) return;

            Vector3 dir = targetPos - transform.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _rotateRoot.rotation = Quaternion.Euler(0, 0, angle);
        }

        // ========== 射程可视化 ==========

        /// <summary>显示射程指示器</summary>
        public void ShowRangeIndicator()
        {
            if (_showingRange) return;
            _showingRange = true;

            if (_rangeIndicator == null)
            {
                _rangeIndicator = new GameObject("RangeIndicator");
                _rangeIndicator.transform.SetParent(transform);
                _rangeIndicator.transform.localPosition = Vector3.zero;

                var lr = _rangeIndicator.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startWidth = 0.05f;
                lr.endWidth = 0.05f;
                lr.startColor = new Color(1f, 1f, 0f, 0.5f);
                lr.endColor = new Color(1f, 1f, 0f, 0.5f);
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.sortingOrder = 15;

                // 绘制圆形
                int segments = 36;
                lr.positionCount = segments;
                float angle = 0f;
                for (int i = 0; i < segments; i++)
                {
                    float x = Mathf.Cos(angle * Mathf.Deg2Rad) * Range + transform.position.x;
                    float y = Mathf.Sin(angle * Mathf.Deg2Rad) * Range + transform.position.y;
                    lr.SetPosition(i, new Vector3(x, y, -0.1f));
                    angle += 360f / segments;
                }
            }

            _rangeIndicator.SetActive(true);
        }

        /// <summary>隐藏射程指示器</summary>
        public void HideRangeIndicator()
        {
            _showingRange = false;
            if (_rangeIndicator != null)
            {
                _rangeIndicator.SetActive(false);
            }
        }

        // ========== 等级变化回调 ==========

        /// <summary>等级变化时的回调（更新视觉和射程指示器）</summary>
        protected virtual void OnLevelChanged()
        {
            // 尝试加载新等级的真实Sprite
            UpdateSpriteForLevel();

            // 通知视觉增强组件更新（描边、厚度层、等级星星等）+ 升级闪光
            var visualEffect = GetComponent<TowerVisualEffect>();
            if (visualEffect != null)
            {
                visualEffect.UpdateGlowColor();
                visualEffect.TriggerUpgradeFlash();
            }


            // 刷新射程指示器
            if (_showingRange)
            {
                HideRangeIndicator();
                ShowRangeIndicator();
            }
        }


        /// <summary>根据当前等级更新塔的Sprite</summary>
        protected void UpdateSpriteForLevel()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || _config == null) return;

            Sprite newSprite = SpriteLoader.LoadTower((int)_config.towerType, _currentLevel);
            if (newSprite != null)
            {
                sr.sprite = newSprite;
                sr.color = Color.white;
            }
        }


        // ========== IPoolable ==========

        public virtual void OnSpawn()
        {
            _isInitialized = false;
            _currentTarget = null;
            _attackTimer = 0f;
            _currentLevel = 1;
        }

        public virtual void OnDespawn()
        {
            _isInitialized = false;
            _currentTarget = null;
            HideRangeIndicator();
        }

        // ========== 调试 ==========

        public virtual string GetDebugInfo()
        {
            return $"{_config?.displayName ?? "未知"} Lv{_currentLevel} ATK:{Damage} SPD:{AttackInterval:F1}s RNG:{Range}";
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_config?.levelData == null || _currentLevel < 1) return;

            // 绘制射程圆
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, Range);

            // 绘制AOE范围
            if (_config.isAOE && _config.aoeRadius > 0)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                Gizmos.DrawWireSphere(transform.position, _config.aoeRadius);
            }
        }
#endif
    }
}
