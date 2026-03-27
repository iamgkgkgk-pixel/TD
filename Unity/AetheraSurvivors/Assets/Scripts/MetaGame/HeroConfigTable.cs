// ============================================================
// 文件名：HeroConfigTable.cs
// 功能描述：英雄配置表 — 定义所有英雄的静态数据
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #251-252
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>英雄稀有度</summary>
    public enum HeroRarity { R = 1, SR = 2, SSR = 3 }

    /// <summary>英雄定位</summary>
    public enum HeroRole { Defense, Attack, Control, Burst, Economy, AllRound }

    /// <summary>
    /// 英雄静态配置数据
    /// </summary>
    public class HeroConfig
    {
        public string Id;
        public string Name;
        public string Icon; // emoji图标
        public HeroRarity Rarity;
        public HeroRole Role;
        public string ActiveSkillName;
        public string ActiveSkillDesc;
        public float ActiveSkillCD; // 秒
        public string PassiveSkillName;
        public string PassiveSkillDesc;
        public string ObtainMethod;
        public int MaxLevel;
        public int MaxStar;

        // 基础属性（1级0星）
        public float BaseHP;
        public float BaseSkillDamage;

        // 升级成长
        public float HPPerLevel;
        public float SkillDamagePerLevel;

        // 升星加成（每星百分比）
        public float StarBonusPercent;
    }

    /// <summary>
    /// 英雄配置表 — 静态数据管理
    /// </summary>
    public static class HeroConfigTable
    {
        private static Dictionary<string, HeroConfig> _configs;
        private static List<HeroConfig> _allHeroes;

        /// <summary>获取所有英雄配置</summary>
        public static List<HeroConfig> GetAllHeroes()
        {
            EnsureInit();
            return _allHeroes;
        }

        /// <summary>根据ID获取英雄配置</summary>
        public static HeroConfig GetHero(string heroId)
        {
            EnsureInit();
            _configs.TryGetValue(heroId, out var config);
            return config;
        }

        /// <summary>获取指定稀有度的英雄列表</summary>
        public static List<HeroConfig> GetHeroesByRarity(HeroRarity rarity)
        {
            EnsureInit();
            var result = new List<HeroConfig>();
            for (int i = 0; i < _allHeroes.Count; i++)
            {
                if (_allHeroes[i].Rarity == rarity)
                    result.Add(_allHeroes[i]);
            }
            return result;
        }

        private static void EnsureInit()
        {
            if (_configs != null) return;

            _configs = new Dictionary<string, HeroConfig>();
            _allHeroes = new List<HeroConfig>();

            // ===== 首版6英雄（来自GDD 6.2节） =====

            Register(new HeroConfig
            {
Id = "hero_knight", Name = "铁壁骑士", Icon = "[剑]",

                Rarity = HeroRarity.R, Role = HeroRole.Defense,
                ActiveSkillName = "无敌护盾", ActiveSkillDesc = "基地3秒无敌",
                ActiveSkillCD = 60f,
                PassiveSkillName = "坚韧意志", PassiveSkillDesc = "基地+2生命",
                ObtainMethod = "第2关教学赠送",
                MaxLevel = 60, MaxStar = 6,
                BaseHP = 500, BaseSkillDamage = 0,
                HPPerLevel = 15, SkillDamagePerLevel = 0,
                StarBonusPercent = 12f
            });

            Register(new HeroConfig
            {
Id = "hero_archer", Name = "精灵射手", Icon = "[弓]",

                Rarity = HeroRarity.R, Role = HeroRole.Attack,
                ActiveSkillName = "万箭齐发", ActiveSkillDesc = "全屏随机箭雨5秒",
                ActiveSkillCD = 60f,
                PassiveSkillName = "鹰眼", PassiveSkillDesc = "全塔射程+10%",
                ObtainMethod = "第5关赠送",
                MaxLevel = 60, MaxStar = 6,
                BaseHP = 350, BaseSkillDamage = 200,
                HPPerLevel = 10, SkillDamagePerLevel = 8,
                StarBonusPercent = 12f
            });

            Register(new HeroConfig
            {
Id = "hero_ice_witch", Name = "霜雪女巫", Icon = "[冰]",

                Rarity = HeroRarity.SR, Role = HeroRole.Control,
                ActiveSkillName = "暴风雪", ActiveSkillDesc = "全屏冰冻所有敌人3秒",
                ActiveSkillCD = 60f,
                PassiveSkillName = "寒冰之力", PassiveSkillDesc = "冰塔效果+15%",
                ObtainMethod = "抽卡获取",
                MaxLevel = 60, MaxStar = 6,
                BaseHP = 400, BaseSkillDamage = 150,
                HPPerLevel = 12, SkillDamagePerLevel = 6,
                StarBonusPercent = 15f
            });

            Register(new HeroConfig
            {
Id = "hero_fire_mage", Name = "炎魔法师", Icon = "[火]",

                Rarity = HeroRarity.SR, Role = HeroRole.Burst,
                ActiveSkillName = "陨石术", ActiveSkillDesc = "对目标区域造成大量AOE伤害",
                ActiveSkillCD = 60f,
                PassiveSkillName = "火焰亲和", PassiveSkillDesc = "火系词条效果+20%",
                ObtainMethod = "抽卡获取",
                MaxLevel = 60, MaxStar = 6,
                BaseHP = 380, BaseSkillDamage = 350,
                HPPerLevel = 11, SkillDamagePerLevel = 12,
                StarBonusPercent = 15f
            });

            Register(new HeroConfig
            {
Id = "hero_dwarf_miner", Name = "矮人矿工", Icon = "[$]",

                Rarity = HeroRarity.SR, Role = HeroRole.Economy,
                ActiveSkillName = "淘金热", ActiveSkillDesc = "10秒内击杀金币翻倍",
                ActiveSkillCD = 60f,
                PassiveSkillName = "矿脉感知", PassiveSkillDesc = "金矿产出+20%",
                ObtainMethod = "抽卡获取",
                MaxLevel = 60, MaxStar = 6,
                BaseHP = 420, BaseSkillDamage = 100,
                HPPerLevel = 13, SkillDamagePerLevel = 4,
                StarBonusPercent = 15f
            });

            Register(new HeroConfig
            {
Id = "hero_chosen_one", Name = "天选者", Icon = "★",

                Rarity = HeroRarity.SSR, Role = HeroRole.AllRound,
                ActiveSkillName = "神之裁决", ActiveSkillDesc = "对全屏敌人造成当前血量20%伤害",
                ActiveSkillCD = 60f,
                PassiveSkillName = "命运垂青", PassiveSkillDesc = "词条选择时多看1张（4选1）",
                ObtainMethod = "抽卡获取（保底50次）",
                MaxLevel = 60, MaxStar = 6,
                BaseHP = 500, BaseSkillDamage = 300,
                HPPerLevel = 15, SkillDamagePerLevel = 10,
                StarBonusPercent = 18f
            });
        }

        private static void Register(HeroConfig config)
        {
            _configs[config.Id] = config;
            _allHeroes.Add(config);
        }
    }
}
