// ============================================================
// 文件名：DamageCalculator.cs
// 功能描述：伤害计算公式与伤害数据结构
//          物理伤害（护甲减伤）、魔法伤害（魔抗减伤）、真实伤害、暴击、闪避
// 创建时间：2026-03-25
// 所属模块：Battle/Tower
// 对应交互：阶段三 #147
// ============================================================

using System;
using UnityEngine;
using AetheraSurvivors.Framework;

namespace AetheraSurvivors.Battle.Tower
{
    // ====================================================================
    // 伤害信息数据结构
    // ====================================================================

    /// <summary>
    /// 伤害信息 — 描述一次伤害的所有参数
    /// </summary>
    [Serializable]
    public struct DamageInfo
    {
        /// <summary>基础伤害值</summary>
        public float Damage;

        /// <summary>伤害类型（物理/魔法/真实）</summary>
        public DamageType DamageType;

        /// <summary>来源塔ID</summary>
        public int SourceTowerId;

        /// <summary>伤害来源位置</summary>
        public Vector3 SourcePosition;

        /// <summary>穿透目标数（1=不穿透）</summary>
        public int PierceCount;

        /// <summary>是否暴击</summary>
        public bool IsCritical;

        /// <summary>是否AOE命中（非直接命中）</summary>
        public bool IsAOEHit;

        /// <summary>附带的Buff类型（0=无）</summary>
        public int BuffId;

        /// <summary>附带的Buff持续时间</summary>
        public float BuffDuration;

        /// <summary>附带的Buff效果值</summary>
        public float BuffValue;
    }

    /// <summary>
    /// 伤害结果 — 经过减伤、暴击等计算后的最终伤害
    /// </summary>
    public struct DamageResult
    {
        /// <summary>最终伤害值（应用减伤/暴击后）</summary>
        public float FinalDamage;

        /// <summary>是否暴击</summary>
        public bool IsCritical;

        /// <summary>是否闪避</summary>
        public bool IsDodged;

        /// <summary>是否被护盾吸收</summary>
        public bool IsAbsorbed;

        /// <summary>护盾吸收的伤害量</summary>
        public float AbsorbedDamage;

        /// <summary>减伤比例（0~1）</summary>
        public float DamageReduction;
    }

    // ====================================================================
    // DamageCalculator 核心类
    // ====================================================================

    /// <summary>
    /// 伤害计算器 — 集中管理所有伤害计算公式
    /// 
    /// 公式：
    /// - 物理伤害 = 基础伤害 × (100 / (100 + 护甲))
    /// - 魔法伤害 = 基础伤害 × (100 / (100 + 魔抗))
    /// - 真实伤害 = 基础伤害（无减伤）
    /// - 暴击伤害 = 最终伤害 × 暴击倍率
    /// - 闪避：随机 < 闪避率 → 伤害 = 0
    /// </summary>
    public static class DamageCalculator
    {
        // ========== 配置常量 ==========

        /// <summary>护甲/魔抗减伤公式常数（越大→减伤曲线越平缓）</summary>
        private const float DefenseConstant = 100f;

        /// <summary>默认暴击倍率</summary>
        private const float DefaultCritMultiplier = 2.0f;

        /// <summary>最小伤害值（确保不为0）</summary>
        private const float MinDamage = 1f;

        /// <summary>最大减伤比例（防止无限堆甲）</summary>
        private const float MaxDamageReduction = 0.9f;

        // ========== 核心方法 ==========

        /// <summary>
        /// 计算最终伤害
        /// </summary>
        /// <param name="damageInfo">伤害信息</param>
        /// <param name="targetArmor">目标护甲值</param>
        /// <param name="targetMagicResist">目标魔抗值</param>
        /// <param name="targetDodgeRate">目标闪避率（0~1）</param>
        /// <param name="critRate">攻击方暴击率（0~1）</param>
        /// <param name="critMultiplier">暴击倍率（默认2.0）</param>
        /// <param name="shieldAmount">目标当前护盾值</param>
        /// <returns>伤害结果</returns>
        public static DamageResult Calculate(
            DamageInfo damageInfo,
            float targetArmor = 0f,
            float targetMagicResist = 0f,
            float targetDodgeRate = 0f,
            float critRate = 0f,
            float critMultiplier = DefaultCritMultiplier,
            float shieldAmount = 0f)
        {
            var result = new DamageResult();

            // 1. 闪避判定
            if (targetDodgeRate > 0f && UnityEngine.Random.value < targetDodgeRate)
            {
                result.IsDodged = true;
                result.FinalDamage = 0f;
                return result;
            }

            float baseDamage = damageInfo.Damage;

            // 2. 减伤计算（根据伤害类型）
            float reduction = 0f;
            switch (damageInfo.DamageType)
            {
                case DamageType.Physical:
                    reduction = CalculateArmorReduction(targetArmor);
                    break;
                case DamageType.Magical:
                    reduction = CalculateArmorReduction(targetMagicResist);
                    break;
                case DamageType.True:
                    reduction = 0f; // 真实伤害无减伤
                    break;
            }

            result.DamageReduction = reduction;
            float afterReduction = baseDamage * (1f - reduction);

            // 3. 暴击判定
            bool isCrit = damageInfo.IsCritical || (critRate > 0f && UnityEngine.Random.value < critRate);
            if (isCrit)
            {
                afterReduction *= critMultiplier;
                result.IsCritical = true;
            }

            // 4. 护盾吸收
            if (shieldAmount > 0f)
            {
                if (shieldAmount >= afterReduction)
                {
                    result.AbsorbedDamage = afterReduction;
                    result.IsAbsorbed = true;
                    result.FinalDamage = 0f;
                    return result;
                }
                else
                {
                    result.AbsorbedDamage = shieldAmount;
                    afterReduction -= shieldAmount;
                }
            }

            // 5. 最终伤害（确保最小伤害）
            result.FinalDamage = Mathf.Max(afterReduction, MinDamage);

            return result;
        }

        /// <summary>
        /// 计算护甲/魔抗减伤比例
        /// 公式：reduction = defense / (defense + constant)
        /// 护甲100时减伤50%，护甲200时减伤66.7%，上限90%
        /// </summary>
        /// <param name="defense">护甲/魔抗值</param>
        /// <returns>减伤比例（0~MaxDamageReduction）</returns>
        public static float CalculateArmorReduction(float defense)
        {
            if (defense <= 0f) return 0f;
            float reduction = defense / (defense + DefenseConstant);
            return Mathf.Min(reduction, MaxDamageReduction);
        }

        /// <summary>
        /// 计算穿甲后的等效护甲
        /// </summary>
        /// <param name="armor">原始护甲</param>
        /// <param name="armorPenetration">穿甲值（固定减少）</param>
        /// <param name="armorPenPercent">穿甲百分比（0~1）</param>
        /// <returns>等效护甲值</returns>
        public static float CalculateEffectiveArmor(float armor, float armorPenetration = 0f, float armorPenPercent = 0f)
        {
            // 先减百分比穿甲，再减固定穿甲
            float effective = armor * (1f - armorPenPercent);
            effective -= armorPenetration;
            return Mathf.Max(effective, 0f);
        }

        /// <summary>
        /// 快速计算伤害（不含暴击/闪避/护盾，用于UI显示预估DPS）
        /// </summary>
        public static float QuickCalculate(float baseDamage, DamageType type, float armor, float magicResist)
        {
            float defense = type == DamageType.Physical ? armor :
                           type == DamageType.Magical ? magicResist : 0f;
            float reduction = CalculateArmorReduction(defense);
            return Mathf.Max(baseDamage * (1f - reduction), MinDamage);
        }

        /// <summary>
        /// 计算DPS预估值
        /// </summary>
        /// <param name="damage">单发伤害</param>
        /// <param name="attackInterval">攻击间隔（秒）</param>
        /// <param name="critRate">暴击率</param>
        /// <param name="critMultiplier">暴击倍率</param>
        /// <returns>预估DPS</returns>
        public static float EstimateDPS(float damage, float attackInterval, float critRate = 0f, float critMultiplier = DefaultCritMultiplier)
        {
            if (attackInterval <= 0f) return 0f;
            float avgDamage = damage * (1f + critRate * (critMultiplier - 1f));
            return avgDamage / attackInterval;
        }
    }
}
