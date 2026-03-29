// ============================================================
// 文件名：RuneSystem.cs
// 功能描述：Roguelike词条系统运行时逻辑
//          波次结束后弹出3选1、词条效果实时应用、词条叠加计算
// 创建时间：2026-03-25
// 所属模块：Battle/Rune
// 对应交互：阶段三 #145
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Rune

{
    // ====================================================================
    // 词条数据
    // ====================================================================

    /// <summary>词条稀有度</summary>
    public enum RuneRarity
    {
        Common = 0,     // 普通（白）
        Rare = 1,       // 稀有（蓝）
        Epic = 2,       // 史诗（紫）
        Legendary = 3   // 传说（橙）
    }

    /// <summary>词条类别</summary>
    public enum RuneCategory
    {
        Attack,     // 攻击类
        Defense,    // 防御类
        Economy,    // 经济类
        Special     // 特殊类
    }

    /// <summary>词条效果类型</summary>
    public enum RuneEffectType
    {
        // 攻击类
        DamageUp,           // 所有塔伤害+X%
        AttackSpeedUp,      // 所有塔攻速+X%
        RangeUp,            // 所有塔射程+X%
        CritRateUp,         // 暴击率+X%
        CritDamageUp,       // 暴击伤害+X%
        PhysicalDamageUp,   // 物理伤害+X%
        MagicalDamageUp,    // 魔法伤害+X%
        PierceUp,           // 穿透+1

        // 防御类
        BaseHPUp,           // 基地生命+X
        SlowEffectUp,       // 减速效果+X%
        ArmorReductionUp,   // 护甲削减+X

        // 经济类
        GoldDropUp,         // 击杀金币+X%
        WaveBonusUp,        // 波次奖励+X%
        BuildCostDown,      // 建造费用-X%
        UpgradeCostDown,    // 升级费用-X%

        // 特殊类
        SplashDamage,       // 攻击附带溅射
        ChainLightning,     // 攻击附带连锁闪电
        LifeSteal,          // 塔吸血（击杀回复基地1点HP）
        ExecuteThreshold,   // 斩杀线（血量低于X%直接处决）
    }

    /// <summary>词条配置</summary>
    [Serializable]
    public class RuneConfig
    {
        public int runeId;
        public string displayName;
        public string description;
        public RuneRarity rarity;
        public RuneCategory category;
        public RuneEffectType effectType;
        public float effectValue;
        public int maxStack = 3;
        public Sprite icon;
    }

    /// <summary>已选择的词条实例</summary>
    public class SelectedRune
    {
        public RuneConfig Config;
        public int StackCount = 1;
        public float TotalValue => Config.effectValue * StackCount;
    }

    // ====================================================================
    // 词条事件
    // ====================================================================

    /// <summary>词条选择界面弹出事件</summary>
    public struct RuneSelectionEvent : IEvent
    {
        public RuneConfig[] Options; // 3个选项
    }

    /// <summary>词条被选择事件</summary>
    public struct RuneSelectedEvent : IEvent
    {
        public int RuneId;
        public string RuneName;
    }

    // ====================================================================
    // RuneSystem 核心类
    // ====================================================================

    /// <summary>
    /// Roguelike词条系统
    /// 
    /// 职责：
    /// 1. 管理词条池
    /// 2. 波次结束后随机抽取3个词条供选择
    /// 3. 维护本局已选词条列表和效果计算
    /// 4. 提供全局效果修改器供其他系统查询
    /// </summary>
    public class RuneSystem : Singleton<RuneSystem>
    {
        // ========== 数据 ==========

        /// <summary>全部词条池</summary>
        private readonly List<RuneConfig> _runePool = new List<RuneConfig>();

        /// <summary>本局已选词条</summary>
        private readonly Dictionary<int, SelectedRune> _selectedRunes = new Dictionary<int, SelectedRune>();

        /// <summary>本局选择历史（有序）</summary>
        private readonly List<SelectedRune> _selectionHistory = new List<SelectedRune>();

        /// <summary>稀有度权重</summary>
        private readonly Dictionary<RuneRarity, float> _rarityWeights = new Dictionary<RuneRarity, float>
        {
            { RuneRarity.Common, 60f },
            { RuneRarity.Rare, 25f },
            { RuneRarity.Epic, 12f },
            { RuneRarity.Legendary, 3f }
        };

        // ========== 缓存的效果修改器 ==========

        /// <summary>所有塔伤害加成（百分比，0.1=10%）</summary>
        public float DamageBonus { get; private set; }
        /// <summary>所有塔攻速加成</summary>
        public float AttackSpeedBonus { get; private set; }
        /// <summary>所有塔射程加成</summary>
        public float RangeBonus { get; private set; }
        /// <summary>暴击率加成</summary>
        public float CritRateBonus { get; private set; }
        /// <summary>暴击伤害倍率加成</summary>
        public float CritDamageBonus { get; private set; }
        /// <summary>物理伤害加成</summary>
        public float PhysicalDamageBonus { get; private set; }
        /// <summary>魔法伤害加成</summary>
        public float MagicalDamageBonus { get; private set; }
        /// <summary>击杀金币加成</summary>
        public float GoldDropBonus { get; private set; }
        /// <summary>建造费用折扣</summary>
        public float BuildCostDiscount { get; private set; }
        /// <summary>斩杀线（血量百分比）</summary>
        public float ExecuteThreshold { get; private set; }
        /// <summary>是否有吸血效果</summary>
        public bool HasLifeSteal { get; private set; }

        /// <summary>已选词条数</summary>
        public int SelectedCount => _selectionHistory.Count;

        /// <summary>已选词条列表</summary>
        public IReadOnlyList<SelectedRune> SelectionHistory => _selectionHistory;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            InitDefaultRunePool();
            Logger.I("RuneSystem", "词条系统初始化，词条池数量={0}", _runePool.Count);
        }

        protected override void OnDispose()
        {
            _selectedRunes.Clear();
            _selectionHistory.Clear();
        }

        // ========== 核心方法 ==========

        /// <summary>
        /// 重置本局词条（新关卡开始时）
        /// </summary>
        public void ResetForNewRun()
        {
            _selectedRunes.Clear();
            _selectionHistory.Clear();
            RecalculateModifiers();
        }

        /// <summary>
        /// 生成3个词条选项（波次结束后调用）
        /// </summary>
        public RuneConfig[] GenerateOptions(int count = 3)
        {
            var options = new RuneConfig[count];
            var usedIds = new HashSet<int>();

            for (int i = 0; i < count; i++)
            {
                var rune = PickRandomRune(usedIds);
                options[i] = rune;
                if (rune != null) usedIds.Add(rune.runeId);
            }

            // 发布选择事件
            EventBus.Instance.Publish(new RuneSelectionEvent { Options = options });

            return options;
        }

        /// <summary>
        /// 选择一个词条
        /// </summary>
        public void SelectRune(int runeId)
        {
            var config = _runePool.Find(r => r.runeId == runeId);
            if (config == null)
            {
                Logger.W("RuneSystem", "未找到词条: ID={0}", runeId);
                return;
            }

            if (_selectedRunes.TryGetValue(runeId, out var existing))
            {
                if (existing.StackCount < config.maxStack)
                {
                    existing.StackCount++;
                }
                else
                {
                    Logger.W("RuneSystem", "词条{0}已达最大叠加数", config.displayName);
                    return;
                }
            }
            else
            {
                var selected = new SelectedRune { Config = config, StackCount = 1 };
                _selectedRunes[runeId] = selected;
                _selectionHistory.Add(selected);
            }

            RecalculateModifiers();

            // 实时应用特殊效果
            ApplyImmediateEffects(config);

            EventBus.Instance.Publish(new RuneSelectedEvent
            {
                RuneId = runeId,
                RuneName = config.displayName
            });

            Logger.I("RuneSystem", "选择词条: {0} (x{1})",
                config.displayName,
                _selectedRunes[runeId].StackCount);
        }

        // ========== 即时效果应用 ==========

        /// <summary>应用需要立即生效的词条效果</summary>
        private void ApplyImmediateEffects(RuneConfig config)
        {
            switch (config.effectType)
            {
                case RuneEffectType.BaseHPUp:
                    // 立即增加基地最大血量和当前血量
                    if (AetheraSurvivors.Battle.BaseHealth.HasInstance)
                    {
                        int hpAdd = Mathf.RoundToInt(config.effectValue);
                        AetheraSurvivors.Battle.BaseHealth.Instance.IncreaseMaxHP(hpAdd);
                    }
                    break;
            }
        }

        // ========== 效果计算 ==========

        /// <summary>重新计算所有效果修改器</summary>
        private void RecalculateModifiers()
        {
            // 重置
            DamageBonus = 0f;
            AttackSpeedBonus = 0f;
            RangeBonus = 0f;
            CritRateBonus = 0f;
            CritDamageBonus = 0f;
            PhysicalDamageBonus = 0f;
            MagicalDamageBonus = 0f;
            GoldDropBonus = 0f;
            BuildCostDiscount = 0f;
            ExecuteThreshold = 0f;
            HasLifeSteal = false;

            foreach (var pair in _selectedRunes)
            {
                var rune = pair.Value;
                float val = rune.TotalValue;

                switch (rune.Config.effectType)
                {
                    case RuneEffectType.DamageUp: DamageBonus += val; break;
                    case RuneEffectType.AttackSpeedUp: AttackSpeedBonus += val; break;
                    case RuneEffectType.RangeUp: RangeBonus += val; break;
                    case RuneEffectType.CritRateUp: CritRateBonus += val; break;
                    case RuneEffectType.CritDamageUp: CritDamageBonus += val; break;
                    case RuneEffectType.PhysicalDamageUp: PhysicalDamageBonus += val; break;
                    case RuneEffectType.MagicalDamageUp: MagicalDamageBonus += val; break;
                    case RuneEffectType.GoldDropUp: GoldDropBonus += val; break;
                    case RuneEffectType.BuildCostDown: BuildCostDiscount += val; break;
                    case RuneEffectType.ExecuteThreshold: ExecuteThreshold += val; break;
                    case RuneEffectType.LifeSteal: HasLifeSteal = true; break;
                }
            }
        }

        // ========== 随机抽取 ==========

        /// <summary>按权重随机抽取一个词条</summary>
        private RuneConfig PickRandomRune(HashSet<int> excludeIds)
        {
            // 构建有效候选列表
            var candidates = new List<(RuneConfig rune, float weight)>();
            float totalWeight = 0f;

            for (int i = 0; i < _runePool.Count; i++)
            {
                var rune = _runePool[i];

                // 排除已选过的
                if (excludeIds.Contains(rune.runeId)) continue;

                // 检查是否已达最大叠加
                if (_selectedRunes.TryGetValue(rune.runeId, out var selected))
                {
                    if (selected.StackCount >= rune.maxStack) continue;
                }

                float weight = _rarityWeights[rune.rarity];
                candidates.Add((rune, weight));
                totalWeight += weight;
            }

            if (candidates.Count == 0 || totalWeight <= 0f) return _runePool[0]; // 兜底

            // 加权随机
            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float accumulated = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                accumulated += candidates[i].weight;
                if (roll <= accumulated)
                {
                    return candidates[i].rune;
                }
            }

            return candidates[candidates.Count - 1].rune;
        }

        // ========== 默认词条池 ==========

        private void InitDefaultRunePool()
        {
            int id = 1;
            // 攻击类
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "力量祝福", description = "所有塔伤害+10%", rarity = RuneRarity.Common, category = RuneCategory.Attack, effectType = RuneEffectType.DamageUp, effectValue = 0.1f, maxStack = 5 });
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "迅捷之风", description = "所有塔攻速+10%", rarity = RuneRarity.Common, category = RuneCategory.Attack, effectType = RuneEffectType.AttackSpeedUp, effectValue = 0.1f, maxStack = 5 });
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "鹰眼", description = "所有塔射程+15%", rarity = RuneRarity.Rare, category = RuneCategory.Attack, effectType = RuneEffectType.RangeUp, effectValue = 0.15f, maxStack = 3 });
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "致命一击", description = "暴击率+8%", rarity = RuneRarity.Rare, category = RuneCategory.Attack, effectType = RuneEffectType.CritRateUp, effectValue = 0.08f, maxStack = 5 });
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "暴击强化", description = "暴击伤害+30%", rarity = RuneRarity.Rare, category = RuneCategory.Attack, effectType = RuneEffectType.CritDamageUp, effectValue = 0.3f, maxStack = 3 });
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "物理精通", description = "物理伤害+20%", rarity = RuneRarity.Rare, category = RuneCategory.Attack, effectType = RuneEffectType.PhysicalDamageUp, effectValue = 0.2f, maxStack = 3 });
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "魔法精通", description = "魔法伤害+20%", rarity = RuneRarity.Rare, category = RuneCategory.Attack, effectType = RuneEffectType.MagicalDamageUp, effectValue = 0.2f, maxStack = 3 });

            // 防御类
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "坚固堡垒", description = "基地生命+3", rarity = RuneRarity.Common, category = RuneCategory.Defense, effectType = RuneEffectType.BaseHPUp, effectValue = 3f, maxStack = 3 });
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "寒冰之触", description = "减速效果+15%", rarity = RuneRarity.Rare, category = RuneCategory.Defense, effectType = RuneEffectType.SlowEffectUp, effectValue = 0.15f, maxStack = 3 });

            // 经济类
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "贪婪", description = "击杀金币+15%", rarity = RuneRarity.Common, category = RuneCategory.Economy, effectType = RuneEffectType.GoldDropUp, effectValue = 0.15f, maxStack = 3 });
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "精打细算", description = "建造费用-10%", rarity = RuneRarity.Rare, category = RuneCategory.Economy, effectType = RuneEffectType.BuildCostDown, effectValue = 0.1f, maxStack = 3 });

            // 特殊类
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "嗜血本能", description = "击杀恢复基地1点生命", rarity = RuneRarity.Epic, category = RuneCategory.Special, effectType = RuneEffectType.LifeSteal, effectValue = 1f, maxStack = 1 });
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "斩杀令", description = "血量低于10%的怪物直接处决", rarity = RuneRarity.Legendary, category = RuneCategory.Special, effectType = RuneEffectType.ExecuteThreshold, effectValue = 0.1f, maxStack = 3 });
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "大力神击", description = "所有塔伤害+25%", rarity = RuneRarity.Epic, category = RuneCategory.Attack, effectType = RuneEffectType.DamageUp, effectValue = 0.25f, maxStack = 2 });
            _runePool.Add(new RuneConfig { runeId = id++, displayName = "狂风之怒", description = "所有塔攻速+20%", rarity = RuneRarity.Epic, category = RuneCategory.Attack, effectType = RuneEffectType.AttackSpeedUp, effectValue = 0.2f, maxStack = 2 });
        }
    }
}
