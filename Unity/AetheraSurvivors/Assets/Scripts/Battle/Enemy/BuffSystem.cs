// ============================================================
// 文件名：BuffSystem.cs
// 功能描述：Buff/Debuff系统 — 减速、中毒、冰冻、灼烧、护甲降低等
//          支持叠加/覆盖/互斥规则
// 创建时间：2026-03-25
// 所属模块：Battle/Enemy
// 对应交互：阶段三 #131
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;

namespace AetheraSurvivors.Battle.Enemy
{
    /// <summary>Buff类型ID常量</summary>
    public static class BuffSystem
    {
        // ===== 减益Buff =====
        public const int BUFF_SLOW = 1;         // 减速
        public const int BUFF_POISON = 2;        // 中毒（DOT）
        public const int BUFF_FREEZE = 3;        // 冰冻（完全停止）
        public const int BUFF_BURN = 4;          // 灼烧（DOT+减防）
        public const int BUFF_ARMOR_REDUCE = 5;  // 护甲降低
        public const int BUFF_STUN = 6;          // 眩晕

        // ===== 增益Buff =====
        public const int BUFF_SPEED_UP = 10;     // 加速
        public const int BUFF_HEAL = 11;         // 持续治疗
        public const int BUFF_SHIELD = 12;       // 护盾
        public const int BUFF_ARMOR_UP = 13;     // 护甲提升

        /// <summary>获取Buff名称</summary>
        public static string GetBuffName(int buffId)
        {
            switch (buffId)
            {
                case BUFF_SLOW: return "减速";
                case BUFF_POISON: return "中毒";
                case BUFF_FREEZE: return "冰冻";
                case BUFF_BURN: return "灼烧";
                case BUFF_ARMOR_REDUCE: return "护甲降低";
                case BUFF_STUN: return "眩晕";
                case BUFF_SPEED_UP: return "加速";
                case BUFF_HEAL: return "治疗";
                case BUFF_SHIELD: return "护盾";
                case BUFF_ARMOR_UP: return "护甲提升";
                default: return "未知Buff";
            }
        }
    }

    /// <summary>Buff叠加规则</summary>
    public enum BuffStackRule
    {
        /// <summary>刷新持续时间（取最大值）</summary>
        Refresh,
        /// <summary>叠加效果值（可多层）</summary>
        Stack,
        /// <summary>取最强效果（覆盖弱的）</summary>
        Strongest,
        /// <summary>不可叠加（先到先得）</summary>
        NoStack
    }

    /// <summary>
    /// Buff实例数据
    /// </summary>
    public class BuffInstance
    {
        /// <summary>Buff类型ID</summary>
        public int BuffId;

        /// <summary>效果值（减速百分比/DOT伤害/护甲值等）</summary>
        public float Value;

        /// <summary>剩余持续时间</summary>
        public float RemainingTime;

        /// <summary>最大持续时间</summary>
        public float MaxDuration;

        /// <summary>叠加层数</summary>
        public int StackCount = 1;

        /// <summary>DOT Tick计时器</summary>
        public float TickTimer;

        /// <summary>DOT Tick间隔</summary>
        public float TickInterval = 1f;

        /// <summary>来源ID（哪个塔施加的）</summary>
        public int SourceId;

        /// <summary>是否已过期</summary>
        public bool IsExpired => RemainingTime <= 0f;
    }

    /// <summary>
    /// Buff容器 — 挂在EnemyBase上管理该怪物的所有Buff
    /// </summary>
    public class BuffContainer
    {
        // ========== 配置 ==========

        /// <summary>每种Buff的最大叠加层数</summary>
        private const int MaxStackCount = 5;

        /// <summary>DOT Tick间隔</summary>
        private const float DefaultTickInterval = 0.5f;

        // ========== 运行时数据 ==========

        /// <summary>所有活跃的Buff列表</summary>
        private readonly List<BuffInstance> _activeBuffs = new List<BuffInstance>(8);

        /// <summary>待移除的Buff索引</summary>
        private readonly List<int> _removeIndices = new List<int>(4);

        /// <summary>Buff拥有者</summary>
        private readonly EnemyBase _owner;

        // ========== 缓存的Buff效果总值 ==========

        /// <summary>总减速比例（0~1）</summary>
        public float TotalSlowPercent { get; private set; }

        /// <summary>总护甲修改量</summary>
        public float TotalArmorModifier { get; private set; }

        /// <summary>总魔抗修改量</summary>
        public float TotalMagicResistModifier { get; private set; }

        /// <summary>是否被冰冻/眩晕（完全停止）</summary>
        public bool IsStunned { get; private set; }

        /// <summary>护盾值</summary>
        public float ShieldAmount { get; private set; }

        /// <summary>活跃Buff数量</summary>
        public int ActiveBuffCount => _activeBuffs.Count;

        // ========== 构造函数 ==========

        public BuffContainer(EnemyBase owner)
        {
            _owner = owner;
        }

        // ========== 核心方法 ==========

        /// <summary>
        /// 添加/刷新Buff
        /// </summary>
        public void AddBuff(int buffId, float value, float duration, int sourceId = 0)
        {
            var rule = GetStackRule(buffId);
            var existing = FindBuff(buffId);

            switch (rule)
            {
                case BuffStackRule.Refresh:
                    if (existing != null)
                    {
                        // 刷新持续时间，取最大效果
                        existing.RemainingTime = Mathf.Max(existing.RemainingTime, duration);
                        existing.Value = Mathf.Max(existing.Value, value);
                    }
                    else
                    {
                        CreateBuff(buffId, value, duration, sourceId);
                    }
                    break;

                case BuffStackRule.Stack:
                    if (existing != null && existing.StackCount < MaxStackCount)
                    {
                        existing.StackCount++;
                        existing.Value += value;
                        existing.RemainingTime = Mathf.Max(existing.RemainingTime, duration);
                    }
                    else if (existing == null)
                    {
                        CreateBuff(buffId, value, duration, sourceId);
                    }
                    break;

                case BuffStackRule.Strongest:
                    if (existing != null)
                    {
                        if (value > existing.Value)
                        {
                            existing.Value = value;
                            existing.RemainingTime = duration;
                        }
                    }
                    else
                    {
                        CreateBuff(buffId, value, duration, sourceId);
                    }
                    break;

                case BuffStackRule.NoStack:
                    if (existing == null)
                    {
                        CreateBuff(buffId, value, duration, sourceId);
                    }
                    break;
            }

            RecalculateModifiers();
        }

        /// <summary>
        /// 移除指定类型的Buff
        /// </summary>
        public void RemoveBuff(int buffId)
        {
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                if (_activeBuffs[i].BuffId == buffId)
                {
                    _activeBuffs.RemoveAt(i);
                }
            }
            RecalculateModifiers();
        }

        /// <summary>
        /// 清除所有Buff
        /// </summary>
        public void ClearAll()
        {
            _activeBuffs.Clear();
            RecalculateModifiers();
        }

        /// <summary>
        /// 清除所有减益Buff
        /// </summary>
        public void ClearDebuffs()
        {
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                if (_activeBuffs[i].BuffId < 10) // 小于10的是减益
                {
                    _activeBuffs.RemoveAt(i);
                }
            }
            RecalculateModifiers();
        }

        /// <summary>
        /// 每帧更新所有Buff（由EnemyBase调用）
        /// </summary>
        /// <param name="deltaTime">帧间隔</param>
        /// <returns>本帧的DOT总伤害</returns>
        public float Update(float deltaTime)
        {
            float totalDotDamage = 0f;
            _removeIndices.Clear();

            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                var buff = _activeBuffs[i];
                buff.RemainingTime -= deltaTime;

                // DOT伤害处理
                if (buff.BuffId == BuffSystem.BUFF_POISON || buff.BuffId == BuffSystem.BUFF_BURN)
                {
                    buff.TickTimer -= deltaTime;
                    if (buff.TickTimer <= 0f)
                    {
                        buff.TickTimer = buff.TickInterval;
                        totalDotDamage += buff.Value * buff.StackCount;
                    }
                }

                // 持续治疗
                if (buff.BuffId == BuffSystem.BUFF_HEAL)
                {
                    buff.TickTimer -= deltaTime;
                    if (buff.TickTimer <= 0f)
                    {
                        buff.TickTimer = buff.TickInterval;
                        _owner?.Heal(buff.Value);
                    }
                }

                // 过期标记
                if (buff.IsExpired)
                {
                    _removeIndices.Add(i);
                }
            }

            // 移除过期Buff（从后往前删除避免索引错乱）
            for (int i = _removeIndices.Count - 1; i >= 0; i--)
            {
                _activeBuffs.RemoveAt(_removeIndices[i]);
            }

            if (_removeIndices.Count > 0)
            {
                RecalculateModifiers();
            }

            return totalDotDamage;
        }

        /// <summary>
        /// 消耗护盾
        /// </summary>
        /// <param name="damage">伤害值</param>
        /// <returns>被护盾吸收的伤害量</returns>
        public float ConsumeShield(float damage)
        {
            if (ShieldAmount <= 0f) return 0f;

            float absorbed = Mathf.Min(damage, ShieldAmount);

            // 从护盾Buff中扣除
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].BuffId == BuffSystem.BUFF_SHIELD)
                {
                    _activeBuffs[i].Value -= absorbed;
                    if (_activeBuffs[i].Value <= 0f)
                    {
                        _activeBuffs.RemoveAt(i);
                    }
                    break;
                }
            }

            RecalculateModifiers();
            return absorbed;
        }

        /// <summary>检查是否有指定Buff</summary>
        public bool HasBuff(int buffId)
        {
            return FindBuff(buffId) != null;
        }

        /// <summary>获取活跃Buff列表（只读）</summary>
        public IReadOnlyList<BuffInstance> GetActiveBuffs()
        {
            return _activeBuffs;
        }

        // ========== 内部方法 ==========

        private BuffInstance FindBuff(int buffId)
        {
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].BuffId == buffId) return _activeBuffs[i];
            }
            return null;
        }

        private void CreateBuff(int buffId, float value, float duration, int sourceId)
        {
            _activeBuffs.Add(new BuffInstance
            {
                BuffId = buffId,
                Value = value,
                RemainingTime = duration,
                MaxDuration = duration,
                StackCount = 1,
                TickTimer = DefaultTickInterval,
                TickInterval = DefaultTickInterval,
                SourceId = sourceId
            });
        }

        /// <summary>获取Buff的叠加规则</summary>
        private BuffStackRule GetStackRule(int buffId)
        {
            switch (buffId)
            {
                case BuffSystem.BUFF_SLOW: return BuffStackRule.Strongest;
                case BuffSystem.BUFF_POISON: return BuffStackRule.Stack;
                case BuffSystem.BUFF_FREEZE: return BuffStackRule.Refresh;
                case BuffSystem.BUFF_BURN: return BuffStackRule.Stack;
                case BuffSystem.BUFF_ARMOR_REDUCE: return BuffStackRule.Strongest;
                case BuffSystem.BUFF_STUN: return BuffStackRule.Refresh;
                case BuffSystem.BUFF_SPEED_UP: return BuffStackRule.Strongest;
                case BuffSystem.BUFF_HEAL: return BuffStackRule.Refresh;
                case BuffSystem.BUFF_SHIELD: return BuffStackRule.Strongest;
                case BuffSystem.BUFF_ARMOR_UP: return BuffStackRule.Strongest;
                default: return BuffStackRule.Refresh;
            }
        }

        /// <summary>重新计算所有Buff修改器</summary>
        private void RecalculateModifiers()
        {
            TotalSlowPercent = 0f;
            TotalArmorModifier = 0f;
            TotalMagicResistModifier = 0f;
            IsStunned = false;
            ShieldAmount = 0f;

            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                var buff = _activeBuffs[i];
                switch (buff.BuffId)
                {
                    case BuffSystem.BUFF_SLOW:
                        TotalSlowPercent = Mathf.Max(TotalSlowPercent, buff.Value);
                        break;
                    case BuffSystem.BUFF_FREEZE:
                    case BuffSystem.BUFF_STUN:
                        IsStunned = true;
                        break;
                    case BuffSystem.BUFF_ARMOR_REDUCE:
                        TotalArmorModifier -= buff.Value;
                        break;
                    case BuffSystem.BUFF_ARMOR_UP:
                        TotalArmorModifier += buff.Value;
                        break;
                    case BuffSystem.BUFF_SPEED_UP:
                        TotalSlowPercent -= buff.Value; // 负的减速=加速
                        break;
                    case BuffSystem.BUFF_SHIELD:
                        ShieldAmount += buff.Value;
                        break;
                }
            }

            // 减速上限80%
            TotalSlowPercent = Mathf.Clamp(TotalSlowPercent, -0.5f, 0.8f);
        }
    }
}
