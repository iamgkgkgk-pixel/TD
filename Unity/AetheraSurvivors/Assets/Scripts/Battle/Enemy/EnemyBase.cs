// ============================================================
// 文件名：EnemyBase.cs
// 功能描述：怪物基类 — 沿路径移动、受伤、死亡、血条、Buff槽
//          配置驱动设计，不同怪物通过数据差异实现
// 创建时间：2026-03-25
// 所属模块：Battle/Enemy
// 对应交互：阶段三 #130
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

using AetheraSurvivors.Battle.Map;
using AetheraSurvivors.Battle.Tower;
using AetheraSurvivors.Battle.Rune;

namespace AetheraSurvivors.Battle.Enemy
{
    // ====================================================================
    // 枚举与配置
    // ====================================================================

    /// <summary>怪物类型</summary>
    public enum EnemyType
    {
        Infantry = 0,    // 步兵（基准）
        Assassin = 1,    // 刺客（高速低血）
        Knight = 2,      // 骑士（高甲慢速）
        Flyer = 3,       // 飞行单位（直线飞行）
        Healer = 10,     // 治疗者
        Slime = 11,      // 分裂史莱姆
        Rogue = 12,      // 隐身盗贼
        ShieldMage = 13, // 护盾法师
        BossDragon = 50, // Boss龙
        BossGiant = 51   // Boss巨人
    }

    /// <summary>怪物配置数据</summary>
    [Serializable]
    public class EnemyConfig
    {
        public EnemyType enemyType;
        public string displayName = "怪物";
        public float maxHP = 100f;
        public float moveSpeed = 2f;
        public float armor = 0f;
        public float magicResist = 0f;
        public float dodgeRate = 0f;
        public int goldDrop = 10;
        public bool isFlying = false;
        public bool isBoss = false;
        public float scale = 1f;
    }

    // ====================================================================
    // 怪物事件
    // ====================================================================

    /// <summary>怪物死亡事件</summary>
    public struct EnemyDeathEvent : IEvent
    {
        public int EnemyId;
        public EnemyType EnemyType;
        public Vector3 Position;
        public int GoldDrop;
        public bool IsBoss;
    }

    /// <summary>怪物到达基地事件</summary>
    public struct EnemyReachedBaseEvent : IEvent
    {
        public int EnemyId;
        public float RemainingHP;
    }

    /// <summary>怪物受伤事件（用于飘字）</summary>
    public struct EnemyDamagedEvent : IEvent
    {
        public int EnemyId;
        public Vector3 Position;
        public float Damage;
        public bool IsCritical;
        public bool IsDodged;
        public DamageType DamageType;
    }

    // ====================================================================
    // EnemyBase 核心基类
    // ====================================================================

    /// <summary>
    /// 怪物基类 — 所有怪物的核心逻辑
    /// 
    /// 设计原则：
    /// 1. 配置驱动：通过EnemyConfig数据驱动行为差异
    /// 2. 沿路径移动：从出生点沿预设路径走向基地
    /// 3. Buff系统：通过BuffContainer管理所有Buff/Debuff
    /// 4. 对象池友好：IPoolable接口
    /// </summary>
    public class EnemyBase : MonoBehaviour, IPoolable
    {
        // ========== 配置 ==========

        [SerializeField] protected EnemyConfig _config;

        // ========== 运行时数据 ==========

        private int _instanceId;
        private static int _nextInstanceId = 1;

        protected float _currentHP;
        protected float _maxHP;
        protected bool _isDead = false;
        protected bool _isInitialized = false;

        /// <summary>Buff容器</summary>
        protected BuffContainer _buffContainer;

        /// <summary>路径点列表（世界坐标）</summary>
        protected List<Vector3> _pathPoints;

        /// <summary>当前路径目标索引</summary>
        protected int _currentPathIndex = 0;

        /// <summary>路径行走进度（0~1）</summary>
        protected float _pathProgress = 0f;

        /// <summary>已走过的路径总距离</summary>
        protected float _distanceTraveled = 0f;

        /// <summary>路径总长度</summary>
        protected float _totalPathLength = 0f;

        /// <summary>死亡回调列表</summary>
        private List<Action<EnemyBase>> _deathCallbacks;

        /// <summary>SpriteRenderer引用</summary>
        protected SpriteRenderer _spriteRenderer;

        /// <summary>视觉动画组件引用</summary>
        protected EnemyVisualAnimator _visualAnimator;


        // ========== 公共属性 ==========

        public int InstanceId => _instanceId;
        public EnemyType Type => _config?.enemyType ?? EnemyType.Infantry;
        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public float HPPercent => _maxHP > 0 ? _currentHP / _maxHP : 0f;
        public bool IsDead => _isDead;
        public float MoveSpeed => GetEffectiveMoveSpeed();
        public float Armor => GetEffectiveArmor();
        public float MagicResist => GetEffectiveMagicResist();
        public float PathProgress => _pathProgress;
        public float DistanceTraveled => _distanceTraveled;
        public bool IsFlying => _config?.isFlying ?? false;
        public bool IsBoss => _config?.isBoss ?? false;
        public EnemyConfig Config => _config;
        public BuffContainer Buffs => _buffContainer;

        /// <summary>是否被冰冻/眩晕</summary>
        public bool IsStunned => _buffContainer != null && _buffContainer.IsStunned;

        // ========== 初始化 ==========

        /// <summary>
        /// 初始化怪物
        /// </summary>
        public virtual void Initialize(EnemyConfig config, List<Vector3> pathPoints)
        {
            _config = config;
            _maxHP = config.maxHP;
            _currentHP = _maxHP;
            _isDead = false;
            _isInitialized = true;
            _instanceId = _nextInstanceId++;
            _currentPathIndex = 0;
            _distanceTraveled = 0f;
            _pathProgress = 0f;

            // 初始化Buff容器
            _buffContainer = new BuffContainer(this);

            // 设置路径
            _pathPoints = pathPoints != null ? new List<Vector3>(pathPoints) : new List<Vector3>();
            CalculateTotalPathLength();

            // 设置起始位置
            if (_pathPoints.Count > 0)
            {
                transform.position = _pathPoints[0];
                _currentPathIndex = 1;
            }

            // 设置缩放
            transform.localScale = Vector3.one * config.scale;

            // 设置Tag
            gameObject.tag = "Enemy";

            // 获取SpriteRenderer
            _spriteRenderer = GetComponent<SpriteRenderer>();

            // 获取视觉动画组件（可选，由EnemySpawner挂载）
            _visualAnimator = GetComponent<EnemyVisualAnimator>();

            // 清除死亡回调

            _deathCallbacks?.Clear();

            Logger.D("EnemyBase", "怪物初始化: {0} HP={1} SPD={2} ID={3}",
                config.displayName, _maxHP, config.moveSpeed, _instanceId);
        }

        // ========== Unity生命周期 ==========

        protected virtual void Update()
        {
            if (!_isInitialized || _isDead) return;

            // 更新Buff
            float dotDamage = _buffContainer.Update(Time.deltaTime);
            if (dotDamage > 0f)
            {
                TakeDotDamage(dotDamage);
            }

            // 移动
            if (!IsStunned)
            {
                MoveAlongPath(Time.deltaTime);
            }
        }

        // ========== 核心方法：移动 ==========

        /// <summary>
        /// 沿路径移动
        /// </summary>
        protected virtual void MoveAlongPath(float deltaTime)
        {
            if (_pathPoints == null || _currentPathIndex >= _pathPoints.Count) return;

            Vector3 targetPos = _pathPoints[_currentPathIndex];
            Vector3 currentPos = transform.position;
            float speed = GetEffectiveMoveSpeed();
            float moveDistance = speed * deltaTime;

            Vector3 direction = targetPos - currentPos;
            float distToTarget = direction.magnitude;

            if (distToTarget <= moveDistance)
            {
                // 到达当前路径点
                transform.position = targetPos;
                _distanceTraveled += distToTarget;
                _currentPathIndex++;

                // 检查是否到达终点（基地）
                if (_currentPathIndex >= _pathPoints.Count)
                {
                    OnReachedBase();
                    return;
                }

                // 移动剩余距离
                float remaining = moveDistance - distToTarget;
                if (remaining > 0f && _currentPathIndex < _pathPoints.Count)
                {
                    Vector3 nextDir = (_pathPoints[_currentPathIndex] - transform.position).normalized;
                    transform.position += nextDir * remaining;
                    _distanceTraveled += remaining;
                }
            }
            else
            {
                // 向目标移动
                transform.position += direction.normalized * moveDistance;
                _distanceTraveled += moveDistance;
            }

            // 更新路径进度
            _pathProgress = _totalPathLength > 0 ? _distanceTraveled / _totalPathLength : 0f;

            // 朝向移动方向
            if (direction.sqrMagnitude > 0.001f)
            {
                UpdateFacing(direction);
            }
        }

        /// <summary>更新朝向</summary>
        protected virtual void UpdateFacing(Vector3 direction)
        {
            if (_spriteRenderer != null)
            {
                // 所有模式统一使用flipX处理左右朝向（侧面视角精灵图）
                bool flipX = direction.x < 0;
                _spriteRenderer.flipX = flipX;

                // 同步腿部朝向（腿部分离动画）
                if (_visualAnimator != null)
                {
                    _visualAnimator.SyncLegsFlip(flipX);
                }
            }
        }



        /// <summary>到达基地</summary>
        protected virtual void OnReachedBase()
        {
            EventBus.Instance.Publish(new EnemyReachedBaseEvent
            {
                EnemyId = _instanceId,
                RemainingHP = _currentHP
            });

            Logger.D("EnemyBase", "怪物到达基地: {0} 剩余HP={1}", _config?.displayName, _currentHP);

            // 回收
            Die(false);
        }

        // ========== 核心方法：受伤 ==========

        /// <summary>
        /// 受到伤害（外部调用入口）
        /// </summary>
        public virtual void TakeDamage(DamageInfo damageInfo)
        {
            if (_isDead) return;

            // 使用DamageCalculator计算最终伤害（传入词条加成）
            float shield = _buffContainer?.ShieldAmount ?? 0f;
            float runeCritRate = RuneSystem.HasInstance ? RuneSystem.Instance.CritRateBonus : 0f;
            float runeCritDmg = RuneSystem.HasInstance ? RuneSystem.Instance.CritDamageBonus : 0f;
            var result = DamageCalculator.Calculate(
                damageInfo,
                GetEffectiveArmor(),
                GetEffectiveMagicResist(),
                _config?.dodgeRate ?? 0f,
                critRate: runeCritRate,
                critMultiplier: 2.0f + runeCritDmg,
                shieldAmount: shield
            );

            // 发布受伤事件（用于飘字等视觉反馈）
            EventBus.Instance.Publish(new EnemyDamagedEvent
            {
                EnemyId = _instanceId,
                Position = transform.position + Vector3.up * 0.5f,
                Damage = result.FinalDamage,
                IsCritical = result.IsCritical,
                IsDodged = result.IsDodged,
                DamageType = damageInfo.DamageType
            });

            if (result.IsDodged) return;

            // 消耗护盾
            if (result.AbsorbedDamage > 0f && _buffContainer != null)
            {
                _buffContainer.ConsumeShield(result.AbsorbedDamage);
            }

            // 扣血
            if (result.FinalDamage > 0f)
            {
                _currentHP -= result.FinalDamage;
            }

            // 斩杀线检查（RuneSystem词条效果）
            if (_currentHP > 0f && RuneSystem.HasInstance && RuneSystem.Instance.ExecuteThreshold > 0f)
            {
                if (HPPercent <= RuneSystem.Instance.ExecuteThreshold)
                {
                    _currentHP = 0f;
                }
            }

            // 应用附带Buff
            if (damageInfo.BuffId > 0 && damageInfo.BuffDuration > 0f)
            {
                ApplyBuff(damageInfo.BuffId, damageInfo.BuffValue, damageInfo.BuffDuration);
            }

            // 受击回调
            OnDamaged(result);

            // 死亡检查
            if (_currentHP <= 0f)
            {
                _currentHP = 0f;
                Die(true);
            }
        }

        /// <summary>DOT伤害（跳过减伤计算，已在施加时计算过）</summary>
        protected void TakeDotDamage(float damage)
        {
            if (_isDead) return;

            _currentHP -= damage;

            EventBus.Instance.Publish(new EnemyDamagedEvent
            {
                EnemyId = _instanceId,
                Position = transform.position + Vector3.up * 0.5f,
                Damage = damage,
                IsCritical = false,
                IsDodged = false,
                DamageType = DamageType.True // DOT视为真实伤害
            });

            if (_currentHP <= 0f)
            {
                _currentHP = 0f;
                Die(true);
            }
        }

        /// <summary>受伤回调（子类可重写添加特殊效果）</summary>
        protected virtual void OnDamaged(DamageResult result)
        {
            // 触发受击视觉动画（闪白+抖动）
            if (_visualAnimator != null)
            {
                _visualAnimator.PlayHitEffect();
            }
        }


        // ========== 核心方法：死亡 ==========

        /// <summary>
        /// 怪物死亡
        /// </summary>
        /// <param name="killedByPlayer">是否被玩家击杀（false=到达基地）</param>
        protected virtual void Die(bool killedByPlayer)
        {
            if (_isDead) return;
            _isDead = true;

            // 触发死亡回调
            if (_deathCallbacks != null)
            {
                for (int i = 0; i < _deathCallbacks.Count; i++)
                {
                    _deathCallbacks[i]?.Invoke(this);
                }
                _deathCallbacks.Clear();
            }

            if (killedByPlayer)
            {
                // 计算金币掉落（含词条加成）
                int goldDrop = _config?.goldDrop ?? 0;
                if (RuneSystem.HasInstance && RuneSystem.Instance.GoldDropBonus > 0f)
                {
                    goldDrop = Mathf.RoundToInt(goldDrop * (1f + RuneSystem.Instance.GoldDropBonus));
                }

                // 发布死亡事件（掉落金币等）
                EventBus.Instance.Publish(new EnemyDeathEvent
                {
                    EnemyId = _instanceId,
                    EnemyType = Type,
                    Position = transform.position,
                    GoldDrop = goldDrop,
                    IsBoss = _config?.isBoss ?? false
                });

                // 吸血词条：击杀恢复基地1点生命
                if (RuneSystem.HasInstance && RuneSystem.Instance.HasLifeSteal)
                {
                    if (BaseHealth.HasInstance && !BaseHealth.Instance.IsDestroyed)
                    {
                        int currentHP = BaseHealth.Instance.CurrentHP;
                        int maxHP = BaseHealth.Instance.MaxHP;
                        if (currentHP < maxHP)
                        {
                            // 通过初始化重新设置不合适，直接在BaseHealth上加方法
                            // 简单方式：发布恢复事件或直接调用
                            BaseHealth.Instance.Heal(1);
                        }
                    }
                }
            }

            // 清除Buff
            _buffContainer?.ClearAll();

            // 死亡动画/效果后回收
            OnDeathEffect();
        }

        /// <summary>死亡效果（子类可重写）</summary>
        protected virtual void OnDeathEffect()
        {
            // 从空间分区注销
            if (Performance.SpatialPartition.HasInstance)
            {
                Performance.SpatialPartition.Instance.Unregister(this);
            }

            // 有视觉动画组件时播放死亡动画，完成后再销毁
            if (_visualAnimator != null)
            {
                _isInitialized = false; // 停止逻辑更新
                _visualAnimator.PlayDeathAnimation(() =>
                {
                    gameObject.SetActive(false);
                    Destroy(gameObject);
                });
            }
            else
            {
                // 无动画组件时直接销毁
                _isInitialized = false;
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }



        // ========== 核心方法：治疗 ==========

        /// <summary>
        /// 治疗
        /// </summary>
        public virtual void Heal(float amount)
        {
            if (_isDead || amount <= 0f) return;
            _currentHP = Mathf.Min(_currentHP + amount, _maxHP);
        }

        // ========== Buff相关 ==========

        /// <summary>应用Buff</summary>
        public void ApplyBuff(int buffId, float value, float duration)
        {
            _buffContainer?.AddBuff(buffId, value, duration);
        }

        /// <summary>击退（沿路径反方向回退）</summary>
        public virtual void ApplyKnockback(float distance)
        {
            if (_isDead || _pathPoints == null || _currentPathIndex < 2) return;

            // 回退一定距离
            _distanceTraveled = Mathf.Max(0f, _distanceTraveled - distance);
            _pathProgress = _totalPathLength > 0 ? _distanceTraveled / _totalPathLength : 0f;

            // 简单实现：回退到上一个路径点
            if (_currentPathIndex > 1)
            {
                _currentPathIndex--;
                transform.position = _pathPoints[_currentPathIndex];
            }
        }

        /// <summary>注册死亡回调</summary>
        public void RegisterDeathCallback(Action<EnemyBase> callback)
        {
            if (_deathCallbacks == null)
                _deathCallbacks = new List<Action<EnemyBase>>(2);
            _deathCallbacks.Add(callback);
        }

        // ========== 属性计算 ==========

        /// <summary>获取有效移动速度（考虑Buff）</summary>
        protected float GetEffectiveMoveSpeed()
        {
            float baseSpeed = _config?.moveSpeed ?? 2f;
            float slowPercent = _buffContainer?.TotalSlowPercent ?? 0f;
            return baseSpeed * (1f - slowPercent);
        }

        /// <summary>获取有效护甲（考虑Buff）</summary>
        protected float GetEffectiveArmor()
        {
            float baseArmor = _config?.armor ?? 0f;
            float modifier = _buffContainer?.TotalArmorModifier ?? 0f;
            return Mathf.Max(baseArmor + modifier, 0f);
        }

        /// <summary>获取有效魔抗（考虑Buff）</summary>
        protected float GetEffectiveMagicResist()
        {
            float baseMR = _config?.magicResist ?? 0f;
            return Mathf.Max(baseMR, 0f);
        }

        // ========== 路径工具 ==========

        /// <summary>计算路径总长度</summary>
        private void CalculateTotalPathLength()
        {
            _totalPathLength = 0f;
            if (_pathPoints == null || _pathPoints.Count < 2) return;

            for (int i = 0; i < _pathPoints.Count - 1; i++)
            {
                _totalPathLength += Vector3.Distance(_pathPoints[i], _pathPoints[i + 1]);
            }
        }

        // ========== IPoolable ==========

        public virtual void OnSpawn()
        {
            _isDead = false;
            _isInitialized = false;
            _currentHP = 0;
            _currentPathIndex = 0;
            _distanceTraveled = 0;
            _pathProgress = 0;
            _deathCallbacks?.Clear();
        }

        public virtual void OnDespawn()
        {
            _isDead = true;
            _isInitialized = false;
            _buffContainer?.ClearAll();
            _deathCallbacks?.Clear();
        }

        // ========== 调试 ==========

        public string GetDebugInfo()
        {
            return $"{_config?.displayName ?? "?"} HP:{_currentHP:F0}/{_maxHP:F0} " +
                   $"SPD:{MoveSpeed:F1} ARM:{Armor:F0} Progress:{_pathProgress:P0} " +
                   $"Buffs:{_buffContainer?.ActiveBuffCount ?? 0}";
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 绘制路径
            if (_pathPoints != null && _pathPoints.Count > 1)
            {
                Gizmos.color = Color.cyan;
                for (int i = _currentPathIndex - 1; i < _pathPoints.Count - 1; i++)
                {
                    if (i < 0) continue;
                    Gizmos.DrawLine(_pathPoints[i], _pathPoints[i + 1]);
                }
            }
        }
#endif
    }
}
