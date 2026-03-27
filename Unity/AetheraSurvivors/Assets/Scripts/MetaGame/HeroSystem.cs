// ============================================================
// 文件名：HeroSystem.cs
// 功能描述：英雄系统 — 英雄解锁、升级、升星、技能、出战选择
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #251-252
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Data;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>
    /// 英雄系统管理器 — 纯C#单例
    /// 
    /// 职责：
    /// 1. 英雄解锁/获取
    /// 2. 英雄升级（消耗经验书+金币）
    /// 3. 英雄升星（消耗碎片）
    /// 4. 技能升级
    /// 5. 出战英雄选择
    /// 6. 计算英雄最终属性
    /// </summary>
    public class HeroSystem : Singleton<HeroSystem>
    {
        // ========== 升级消耗表 ==========
        // 等级 → 所需经验书数量
        private static readonly int[] LevelUpExpCost = {
            0, 5, 8, 12, 16, 20, 25, 30, 36, 42,  // 1-10
            50, 58, 67, 76, 86, 96, 108, 120, 133, 146, // 11-20
            160, 175, 190, 206, 223, 240, 258, 277, 296, 316, // 21-30
            337, 358, 380, 403, 426, 450, 475, 500, 526, 553, // 31-40
            580, 608, 637, 666, 696, 727, 758, 790, 823, 856, // 41-50
            890, 925, 960, 996, 1033, 1070, 1108, 1147, 1186, 1226 // 51-60
        };

        // 等级 → 所需金币
        private static readonly int[] LevelUpGoldCost = {
            0, 100, 150, 200, 260, 320, 390, 460, 540, 620,
            710, 800, 900, 1000, 1110, 1220, 1340, 1460, 1590, 1720,
            1860, 2000, 2150, 2300, 2460, 2620, 2790, 2960, 3140, 3320,
            3510, 3700, 3900, 4100, 4310, 4520, 4740, 4960, 5190, 5420,
            5660, 5900, 6150, 6400, 6660, 6920, 7190, 7460, 7740, 8020,
            8310, 8600, 8900, 9200, 9510, 9820, 10140, 10460, 10790, 11120
        };

        // 升星 → 所需碎片数
        private static readonly int[] StarUpFragmentCost = { 0, 10, 30, 60, 100, 150, 200 };

        // ========== 公共方法 ==========

        /// <summary>解锁英雄</summary>
        public bool UnlockHero(string heroId)
        {
            var config = HeroConfigTable.GetHero(heroId);
            if (config == null)
            {
                Debug.LogError($"[HeroSystem] 英雄不存在: {heroId}");
                return false;
            }

            if (!PlayerDataManager.HasInstance) return false;
            var data = PlayerDataManager.Instance.Data;

            // 检查是否已解锁
            if (IsHeroUnlocked(heroId))
            {
                Debug.LogWarning($"[HeroSystem] 英雄已解锁: {heroId}");
                return false;
            }

            // 添加英雄数据
            var heroData = new HeroSaveData
            {
                HeroId = heroId,
                Level = 1,
                Star = 0,
                Exp = 0,
                Fragments = 0
            };
            data.Heroes.Add(heroData);

            // 如果没有出战英雄，自动设为出战
            if (string.IsNullOrEmpty(data.ActiveHeroId))
            {
                data.ActiveHeroId = heroId;
            }

            PlayerDataManager.Instance.MarkDirty();
            PlayerDataManager.Instance.Save();

            // 发布事件
            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new HeroUnlockedEvent { HeroId = heroId });
            }

            Debug.Log($"[HeroSystem] 英雄解锁: {config.Name}");
            return true;
        }

        /// <summary>英雄升级</summary>
        public bool LevelUpHero(string heroId)
        {
            var heroData = GetHeroData(heroId);
            if (heroData == null) return false;

            var config = HeroConfigTable.GetHero(heroId);
            if (config == null) return false;

            if (heroData.Level >= config.MaxLevel)
            {
                Debug.LogWarning($"[HeroSystem] 英雄已满级: {heroId}");
                return false;
            }

            int nextLevel = heroData.Level; // 数组索引=当前等级
            if (nextLevel >= LevelUpExpCost.Length) return false;

            int expCost = LevelUpExpCost[nextLevel];
            int goldCost = LevelUpGoldCost[nextLevel];

            // 检查资源（简化：直接检查金币）
            if (!PlayerDataManager.Instance.SpendGold(goldCost))
            {
                Debug.LogWarning("[HeroSystem] 金币不足");
                return false;
            }

            int oldLevel = heroData.Level;
            heroData.Level++;

            PlayerDataManager.Instance.MarkDirty();
            PlayerDataManager.Instance.Save();

            // 发布事件
            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new HeroLevelUpEvent
                {
                    HeroId = heroId,
                    OldLevel = oldLevel,
                    NewLevel = heroData.Level
                });
            }

            Debug.Log($"[HeroSystem] 英雄升级: {config.Name} Lv.{oldLevel} → Lv.{heroData.Level}");
            return true;
        }

        /// <summary>英雄升星</summary>
        public bool StarUpHero(string heroId)
        {
            var heroData = GetHeroData(heroId);
            if (heroData == null) return false;

            var config = HeroConfigTable.GetHero(heroId);
            if (config == null) return false;

            if (heroData.Star >= config.MaxStar)
            {
                Debug.LogWarning($"[HeroSystem] 英雄已满星: {heroId}");
                return false;
            }

            int nextStar = heroData.Star + 1;
            if (nextStar >= StarUpFragmentCost.Length) return false;

            int fragmentCost = StarUpFragmentCost[nextStar];
            if (heroData.Fragments < fragmentCost)
            {
                Debug.LogWarning($"[HeroSystem] 碎片不足: 需要{fragmentCost}, 拥有{heroData.Fragments}");
                return false;
            }

            heroData.Fragments -= fragmentCost;
            int oldStar = heroData.Star;
            heroData.Star++;

            PlayerDataManager.Instance.MarkDirty();
            PlayerDataManager.Instance.Save();

            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new HeroStarUpEvent
                {
                    HeroId = heroId,
                    OldStar = oldStar,
                    NewStar = heroData.Star
                });
            }

            Debug.Log($"[HeroSystem] 英雄升星: {config.Name} {oldStar}★ → {heroData.Star}★");
            return true;
        }

        /// <summary>设置出战英雄</summary>
        public bool SetActiveHero(string heroId)
        {
            if (!IsHeroUnlocked(heroId))
            {
                Debug.LogWarning($"[HeroSystem] 英雄未解锁: {heroId}");
                return false;
            }

            if (!PlayerDataManager.HasInstance) return false;
            var data = PlayerDataManager.Instance.Data;

            string oldHeroId = data.ActiveHeroId;
            data.ActiveHeroId = heroId;

            PlayerDataManager.Instance.MarkDirty();
            PlayerDataManager.Instance.Save();

            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new HeroActiveChangedEvent
                {
                    OldHeroId = oldHeroId,
                    NewHeroId = heroId
                });
            }

            return true;
        }

        /// <summary>添加英雄碎片</summary>
        public void AddFragments(string heroId, int amount)
        {
            var heroData = GetHeroData(heroId);
            if (heroData == null)
            {
                // 英雄未解锁，先解锁
                UnlockHero(heroId);
                heroData = GetHeroData(heroId);
                if (heroData == null) return;
            }

            heroData.Fragments += amount;
            PlayerDataManager.Instance.MarkDirty();
        }

        // ========== 属性计算 ==========

        /// <summary>计算英雄最终HP</summary>
        public float GetHeroHP(string heroId)
        {
            var heroData = GetHeroData(heroId);
            var config = HeroConfigTable.GetHero(heroId);
            if (heroData == null || config == null) return 0;

            float baseHP = config.BaseHP + config.HPPerLevel * (heroData.Level - 1);
            float starBonus = 1f + heroData.Star * config.StarBonusPercent / 100f;
            return baseHP * starBonus;
        }

        /// <summary>计算英雄技能伤害</summary>
        public float GetHeroSkillDamage(string heroId)
        {
            var heroData = GetHeroData(heroId);
            var config = HeroConfigTable.GetHero(heroId);
            if (heroData == null || config == null) return 0;

            float baseDmg = config.BaseSkillDamage + config.SkillDamagePerLevel * (heroData.Level - 1);
            float starBonus = 1f + heroData.Star * config.StarBonusPercent / 100f;
            return baseDmg * starBonus;
        }

        /// <summary>计算英雄战力</summary>
        public int GetHeroPower(string heroId)
        {
            float hp = GetHeroHP(heroId);
            float dmg = GetHeroSkillDamage(heroId);
            return Mathf.RoundToInt(hp + dmg * 2f);
        }

        // ========== 查询方法 ==========

        /// <summary>英雄是否已解锁</summary>
        public bool IsHeroUnlocked(string heroId)
        {
            return GetHeroData(heroId) != null;
        }

        /// <summary>获取英雄存档数据</summary>
        public HeroSaveData GetHeroData(string heroId)
        {
            if (!PlayerDataManager.HasInstance) return null;
            var heroes = PlayerDataManager.Instance.Data.Heroes;
            if (heroes == null) return null;

            for (int i = 0; i < heroes.Count; i++)
            {
                if (heroes[i].HeroId == heroId)
                    return heroes[i];
            }
            return null;
        }

        /// <summary>获取所有已解锁英雄</summary>
        public List<HeroSaveData> GetUnlockedHeroes()
        {
            if (!PlayerDataManager.HasInstance) return new List<HeroSaveData>();
            return PlayerDataManager.Instance.Data.Heroes ?? new List<HeroSaveData>();
        }

        /// <summary>获取升级所需金币</summary>
        public int GetLevelUpGoldCost(string heroId)
        {
            var heroData = GetHeroData(heroId);
            if (heroData == null) return 0;
            int idx = heroData.Level;
            return idx < LevelUpGoldCost.Length ? LevelUpGoldCost[idx] : 99999;
        }

        /// <summary>获取升星所需碎片</summary>
        public int GetStarUpFragmentCost(string heroId)
        {
            var heroData = GetHeroData(heroId);
            if (heroData == null) return 0;
            int nextStar = heroData.Star + 1;
            return nextStar < StarUpFragmentCost.Length ? StarUpFragmentCost[nextStar] : 99999;
        }

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            Debug.Log("[HeroSystem] 初始化完成");
        }

        protected override void OnDispose()
        {
        }
    }
}
