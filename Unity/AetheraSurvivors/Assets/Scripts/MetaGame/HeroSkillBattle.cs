// ============================================================
// 文件名：HeroSkillBattle.cs
// 功能描述：英雄技能战斗对接 — 主动技能释放、被动技能自动生效、技能升级
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #252
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Data;
using AetheraSurvivors.Battle;
using AetheraSurvivors.Battle.Tower;
using AetheraSurvivors.Battle.Enemy;

namespace AetheraSurvivors.MetaGame
{
    // ========== 技能效果类型 ==========

    /// <summary>主动技能效果类型</summary>
    public enum ActiveSkillEffectType
    {
        BaseInvincible,     // 基地无敌（铁壁骑士）
        ArrowRain,          // 全屏箭雨（精灵射手）
        GlobalFreeze,       // 全屏冰冻（霜雪女巫）
        MeteorStrike,       // 陨石术AOE（炎魔法师）
        GoldRush,           // 金币翻倍（矮人矿工）
        DivineJudgment      // 全屏百分比伤害（天选者）
    }

    /// <summary>被动技能效果类型</summary>
    public enum PassiveSkillEffectType
    {
        BaseHPBonus,        // 基地生命加成
        TowerRangeBonus,    // 全塔射程加成
        IceTowerBonus,      // 冰塔效果加成
        FireRuneBonus,      // 火系词条加成
        GoldMineBonus,      // 金矿产出加成
        ExtraRuneChoice     // 词条多看1张
    }

    // ========== 技能配置 ==========

    /// <summary>
    /// 英雄技能完整配置（扩展HeroConfig中的技能数据）
    /// </summary>
    [Serializable]
    public class HeroSkillConfig
    {
        public string HeroId;

        // 主动技能
        public ActiveSkillEffectType ActiveEffect;
        public float BaseCooldown;          // 基础冷却时间（秒）
        public float CooldownPerLevel;      // 每级减少的冷却时间
        public float BaseDamage;            // 基础伤害/效果值
        public float DamagePerLevel;        // 每级增加的伤害
        public float Duration;              // 效果持续时间（秒）
        public float EffectRadius;          // 效果范围（世界单位）

        // 被动技能
        public PassiveSkillEffectType PassiveEffect;
        public float BasePassiveValue;      // 基础被动值
        public float PassiveValuePerLevel;  // 每级增加的被动值
        public float PassiveValuePerStar;   // 每星增加的被动值
    }

    // ========== 技能运行时状态 ==========

    /// <summary>
    /// 技能运行时状态（战斗中）
    /// </summary>
    public class SkillRuntimeState
    {
        public string HeroId;
        public float CooldownRemaining;     // 剩余冷却时间
        public float MaxCooldown;           // 最大冷却时间
        public bool IsReady;                // 是否可释放
        public bool IsActive;               // 效果是否正在生效
        public float ActiveDuration;        // 效果剩余持续时间
        public int SkillLevel;              // 技能等级
    }

    // ========== 技能事件 ==========

    /// <summary>主动技能释放事件</summary>
    public struct HeroSkillCastEvent : IEvent
    {
        public string HeroId;
        public ActiveSkillEffectType EffectType;
        public float Damage;
        public float Duration;
        public Vector3 TargetPosition;
    }

    /// <summary>主动技能冷却完成事件</summary>
    public struct HeroSkillReadyEvent : IEvent
    {
        public string HeroId;
    }

    // ========== 英雄技能战斗管理器 ==========

    /// <summary>
    /// 英雄技能战斗管理器
    /// 
    /// 职责：
    /// 1. 管理英雄主动技能的冷却和释放
    /// 2. 执行主动技能效果（对敌人造成伤害/控制/增益）
    /// 3. 在战斗开始时应用被动技能效果
    /// 4. 技能升级（消耗金币+材料）
    /// 5. 提供技能UI所需的状态数据
    /// </summary>
    public class HeroSkillBattleManager : MonoSingleton<HeroSkillBattleManager>
    {
        // ========== 技能配置表 ==========
        private static Dictionary<string, HeroSkillConfig> _skillConfigs;

        // ========== 运行时状态 ==========
        private SkillRuntimeState _activeSkillState;
        private string _currentHeroId;
        private bool _isBattleActive;

        // 被动效果缓存
        private float _passiveTowerRangeBonus;
        private float _passiveBaseHPBonus;
        private float _passiveIceTowerBonus;
        private float _passiveFireRuneBonus;
        private float _passiveGoldMineBonus;
        private int _passiveExtraRuneChoice;

        // ========== 公共属性 ==========

        /// <summary>当前技能运行时状态</summary>
        public SkillRuntimeState CurrentSkillState => _activeSkillState;

        /// <summary>被动塔射程加成（百分比）</summary>
        public float PassiveTowerRangeBonus => _passiveTowerRangeBonus;

        /// <summary>被动基地HP加成</summary>
        public float PassiveBaseHPBonus => _passiveBaseHPBonus;

        /// <summary>被动冰塔效果加成（百分比）</summary>
        public float PassiveIceTowerBonus => _passiveIceTowerBonus;

        /// <summary>被动火系词条加成（百分比）</summary>
        public float PassiveFireRuneBonus => _passiveFireRuneBonus;

        /// <summary>被动金矿产出加成（百分比）</summary>
        public float PassiveGoldMineBonus => _passiveGoldMineBonus;

        /// <summary>被动额外词条选择数</summary>
        public int PassiveExtraRuneChoice => _passiveExtraRuneChoice;

        // ========== 初始化 ==========

        protected override void OnInit()
        {
            InitSkillConfigs();
            Debug.Log("[HeroSkillBattle] 初始化完成");
        }

        /// <summary>
        /// 初始化技能配置表
        /// </summary>
        private static void InitSkillConfigs()
        {
            if (_skillConfigs != null) return;
            _skillConfigs = new Dictionary<string, HeroSkillConfig>();

            // 铁壁骑士 — 无敌护盾
            _skillConfigs["hero_knight"] = new HeroSkillConfig
            {
                HeroId = "hero_knight",
                ActiveEffect = ActiveSkillEffectType.BaseInvincible,
                BaseCooldown = 60f, CooldownPerLevel = 0.5f,
                BaseDamage = 0f, DamagePerLevel = 0f,
                Duration = 3f, EffectRadius = 0f,
                PassiveEffect = PassiveSkillEffectType.BaseHPBonus,
                BasePassiveValue = 2f, PassiveValuePerLevel = 0.5f, PassiveValuePerStar = 1f
            };

            // 精灵射手 — 万箭齐发
            _skillConfigs["hero_archer"] = new HeroSkillConfig
            {
                HeroId = "hero_archer",
                ActiveEffect = ActiveSkillEffectType.ArrowRain,
                BaseCooldown = 60f, CooldownPerLevel = 0.5f,
                BaseDamage = 200f, DamagePerLevel = 8f,
                Duration = 5f, EffectRadius = 999f, // 全屏
                PassiveEffect = PassiveSkillEffectType.TowerRangeBonus,
                BasePassiveValue = 10f, PassiveValuePerLevel = 0.5f, PassiveValuePerStar = 2f
            };

            // 霜雪女巫 — 暴风雪
            _skillConfigs["hero_ice_witch"] = new HeroSkillConfig
            {
                HeroId = "hero_ice_witch",
                ActiveEffect = ActiveSkillEffectType.GlobalFreeze,
                BaseCooldown = 60f, CooldownPerLevel = 0.5f,
                BaseDamage = 150f, DamagePerLevel = 6f,
                Duration = 3f, EffectRadius = 999f,
                PassiveEffect = PassiveSkillEffectType.IceTowerBonus,
                BasePassiveValue = 15f, PassiveValuePerLevel = 0.8f, PassiveValuePerStar = 3f
            };

            // 炎魔法师 — 陨石术
            _skillConfigs["hero_fire_mage"] = new HeroSkillConfig
            {
                HeroId = "hero_fire_mage",
                ActiveEffect = ActiveSkillEffectType.MeteorStrike,
                BaseCooldown = 60f, CooldownPerLevel = 0.5f,
                BaseDamage = 350f, DamagePerLevel = 12f,
                Duration = 0.5f, EffectRadius = 3f,
                PassiveEffect = PassiveSkillEffectType.FireRuneBonus,
                BasePassiveValue = 20f, PassiveValuePerLevel = 1f, PassiveValuePerStar = 4f
            };

            // 矮人矿工 — 淘金热
            _skillConfigs["hero_dwarf_miner"] = new HeroSkillConfig
            {
                HeroId = "hero_dwarf_miner",
                ActiveEffect = ActiveSkillEffectType.GoldRush,
                BaseCooldown = 60f, CooldownPerLevel = 0.5f,
                BaseDamage = 0f, DamagePerLevel = 0f,
                Duration = 10f, EffectRadius = 0f,
                PassiveEffect = PassiveSkillEffectType.GoldMineBonus,
                BasePassiveValue = 20f, PassiveValuePerLevel = 1f, PassiveValuePerStar = 4f
            };

            // 天选者 — 神之裁决
            _skillConfigs["hero_chosen_one"] = new HeroSkillConfig
            {
                HeroId = "hero_chosen_one",
                ActiveEffect = ActiveSkillEffectType.DivineJudgment,
                BaseCooldown = 60f, CooldownPerLevel = 0.5f,
                BaseDamage = 20f, DamagePerLevel = 0.5f, // 百分比伤害
                Duration = 0.5f, EffectRadius = 999f,
                PassiveEffect = PassiveSkillEffectType.ExtraRuneChoice,
                BasePassiveValue = 1f, PassiveValuePerLevel = 0f, PassiveValuePerStar = 0f
            };
        }

        // ========== 战斗生命周期 ==========

        /// <summary>
        /// 战斗开始时调用 — 初始化技能状态并应用被动效果
        /// </summary>
        public void OnBattleStart()
        {
            _isBattleActive = true;

            // 获取当前出战英雄
            if (!PlayerDataManager.HasInstance) return;
            _currentHeroId = PlayerDataManager.Instance.Data.ActiveHeroId;

            if (string.IsNullOrEmpty(_currentHeroId))
            {
                Debug.LogWarning("[HeroSkillBattle] 没有出战英雄");
                return;
            }

            var config = GetSkillConfig(_currentHeroId);
            if (config == null) return;

            var heroData = HeroSystem.HasInstance ? HeroSystem.Instance.GetHeroData(_currentHeroId) : null;
            int heroLevel = heroData?.Level ?? 1;
            int heroStar = heroData?.Star ?? 0;

            // 初始化主动技能状态
            float maxCD = Mathf.Max(config.BaseCooldown - config.CooldownPerLevel * (heroLevel - 1), 15f);
            _activeSkillState = new SkillRuntimeState
            {
                HeroId = _currentHeroId,
                CooldownRemaining = maxCD * 0.5f, // 战斗开始时半CD
                MaxCooldown = maxCD,
                IsReady = false,
                IsActive = false,
                ActiveDuration = 0f,
                SkillLevel = heroLevel
            };

            // 计算并应用被动效果
            ApplyPassiveEffects(config, heroLevel, heroStar);

            Debug.Log($"[HeroSkillBattle] 战斗开始: 英雄={_currentHeroId}, 技能CD={maxCD:F1}s");
        }

        /// <summary>
        /// 战斗结束时调用 — 清理状态
        /// </summary>
        public void OnBattleEnd()
        {
            _isBattleActive = false;
            _activeSkillState = null;
            ClearPassiveEffects();
            Debug.Log("[HeroSkillBattle] 战斗结束，技能状态已清理");
        }

        /// <summary>
        /// 每帧更新（由BattleManager调用）
        /// </summary>
        public void UpdateSkills(float deltaTime)
        {
            if (!_isBattleActive || _activeSkillState == null) return;

            // 更新冷却
            if (!_activeSkillState.IsReady && !_activeSkillState.IsActive)
            {
                _activeSkillState.CooldownRemaining -= deltaTime;
                if (_activeSkillState.CooldownRemaining <= 0f)
                {
                    _activeSkillState.CooldownRemaining = 0f;
                    _activeSkillState.IsReady = true;

                    // 发布技能就绪事件
                    if (EventBus.HasInstance)
                    {
                        EventBus.Instance.Publish(new HeroSkillReadyEvent
                        {
                            HeroId = _currentHeroId
                        });
                    }
                }
            }

            // 更新效果持续时间
            if (_activeSkillState.IsActive)
            {
                _activeSkillState.ActiveDuration -= deltaTime;
                if (_activeSkillState.ActiveDuration <= 0f)
                {
                    _activeSkillState.IsActive = false;
                    OnSkillEffectEnd();
                }
            }
        }

        // ========== 主动技能释放 ==========

        /// <summary>
        /// 释放主动技能
        /// </summary>
        /// <param name="targetPosition">目标位置（部分技能需要指定位置）</param>
        /// <returns>是否成功释放</returns>
        public bool CastActiveSkill(Vector3 targetPosition = default)
        {
            if (!_isBattleActive || _activeSkillState == null) return false;

            if (!_activeSkillState.IsReady)
            {
                Debug.LogWarning($"[HeroSkillBattle] 技能冷却中: {_activeSkillState.CooldownRemaining:F1}s");
                return false;
            }

            var config = GetSkillConfig(_currentHeroId);
            if (config == null) return false;

            var heroData = HeroSystem.HasInstance ? HeroSystem.Instance.GetHeroData(_currentHeroId) : null;
            int heroLevel = heroData?.Level ?? 1;
            int heroStar = heroData?.Star ?? 0;

            // 计算技能伤害
            float damage = config.BaseDamage + config.DamagePerLevel * (heroLevel - 1);
            float starBonus = 1f + heroStar * 0.1f;
            damage *= starBonus;

            // 执行技能效果
            ExecuteActiveSkill(config.ActiveEffect, damage, config.Duration, config.EffectRadius, targetPosition);

            // 进入冷却
            _activeSkillState.IsReady = false;
            _activeSkillState.CooldownRemaining = _activeSkillState.MaxCooldown;
            _activeSkillState.IsActive = config.Duration > 0.5f;
            _activeSkillState.ActiveDuration = config.Duration;

            // 发布事件
            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new HeroSkillCastEvent
                {
                    HeroId = _currentHeroId,
                    EffectType = config.ActiveEffect,
                    Damage = damage,
                    Duration = config.Duration,
                    TargetPosition = targetPosition
                });
            }

            Debug.Log($"[HeroSkillBattle] 技能释放: {config.ActiveEffect}, 伤害={damage:F0}, 持续={config.Duration:F1}s");
            return true;
        }

        /// <summary>
        /// 获取技能冷却进度（0~1，1=就绪）
        /// </summary>
        public float GetCooldownProgress()
        {
            if (_activeSkillState == null) return 0f;
            if (_activeSkillState.IsReady) return 1f;
            if (_activeSkillState.MaxCooldown <= 0f) return 1f;
            return 1f - (_activeSkillState.CooldownRemaining / _activeSkillState.MaxCooldown);
        }

        /// <summary>
        /// 获取技能冷却剩余秒数
        /// </summary>
        public float GetCooldownRemaining()
        {
            return _activeSkillState?.CooldownRemaining ?? 0f;
        }

        /// <summary>
        /// 技能是否就绪
        /// </summary>
        public bool IsSkillReady()
        {
            return _activeSkillState?.IsReady ?? false;
        }

        // ========== 技能效果执行 ==========

        /// <summary>
        /// 执行主动技能效果
        /// </summary>
        private void ExecuteActiveSkill(ActiveSkillEffectType effectType, float damage,
            float duration, float radius, Vector3 targetPos)
        {
            switch (effectType)
            {
                case ActiveSkillEffectType.BaseInvincible:
                    ExecuteBaseInvincible(duration);
                    break;

                case ActiveSkillEffectType.ArrowRain:
                    ExecuteArrowRain(damage, duration);
                    break;

                case ActiveSkillEffectType.GlobalFreeze:
                    ExecuteGlobalFreeze(damage, duration);
                    break;

                case ActiveSkillEffectType.MeteorStrike:
                    ExecuteMeteorStrike(damage, radius, targetPos);
                    break;

                case ActiveSkillEffectType.GoldRush:
                    ExecuteGoldRush(duration);
                    break;

                case ActiveSkillEffectType.DivineJudgment:
                    ExecuteDivineJudgment(damage);
                    break;
            }
        }

        /// <summary>基地无敌（铁壁骑士）</summary>
        private void ExecuteBaseInvincible(float duration)
        {
            Debug.Log($"[Skill] 无敌护盾: 基地{duration}秒无敌");
            // 通知BattleManager基地进入无敌状态
            // BattleManager通过事件监听来处理
        }

        /// <summary>全屏箭雨（精灵射手）</summary>
        private void ExecuteArrowRain(float damagePerSecond, float duration)
        {
            Debug.Log($"[Skill] 万箭齐发: 全屏每秒{damagePerSecond:F0}伤害, 持续{duration:F1}s");

            // 查找所有存活敌人并造成伤害
            var enemies = FindAllAliveEnemies();
            float totalDamage = damagePerSecond * 0.5f; // 首次立即造成半秒伤害
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null)
                {
                    enemies[i].TakeDamage(new DamageInfo
                    {
                        Damage = totalDamage,
                        DamageType = DamageType.Physical,
                        SourcePosition = Vector3.zero,
                        IsAOEHit = true
                    });
                }
            }
        }


        /// <summary>全屏冰冻（霜雪女巫）</summary>
        private void ExecuteGlobalFreeze(float damage, float duration)
        {
            Debug.Log($"[Skill] 暴风雪: 全屏冰冻{duration:F1}s, 伤害{damage:F0}");

            var enemies = FindAllAliveEnemies();
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null)
                {
                    enemies[i].TakeDamage(new DamageInfo
                    {
                        Damage = damage * 0.3f,
                        DamageType = DamageType.Magical,
                        SourcePosition = Vector3.zero,
                        IsAOEHit = true
                    });
                    enemies[i].ApplyBuff(BuffSystem.BUFF_FREEZE, 0f, duration);
                }
            }
        }


        /// <summary>陨石术AOE（炎魔法师）</summary>
        private void ExecuteMeteorStrike(float damage, float radius, Vector3 targetPos)
        {
            Debug.Log($"[Skill] 陨石术: 位置{targetPos}, 范围{radius:F1}, 伤害{damage:F0}");

            var enemies = FindAllAliveEnemies();
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null)
                {
                    float dist = Vector3.Distance(enemies[i].transform.position, targetPos);
                    if (dist <= radius)
                    {
                        // 距离衰减：中心100%，边缘50%
                        float falloff = 1f - (dist / radius) * 0.5f;
                        enemies[i].TakeDamage(new DamageInfo
                        {
                            Damage = damage * falloff,
                            DamageType = DamageType.Magical,
                            SourcePosition = targetPos,
                            IsAOEHit = true
                        });
                    }
                }
            }
        }


        /// <summary>金币翻倍（矮人矿工）</summary>
        private void ExecuteGoldRush(float duration)
        {
            Debug.Log($"[Skill] 淘金热: {duration:F1}秒内击杀金币翻倍");
            // 通过事件通知经济系统进入金币翻倍模式
            // BattleManager/EconomySystem监听此事件
        }

        /// <summary>全屏百分比伤害（天选者）</summary>
        private void ExecuteDivineJudgment(float percentDamage)
        {
            Debug.Log($"[Skill] 神之裁决: 全屏{percentDamage:F1}%当前血量伤害");

            var enemies = FindAllAliveEnemies();
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null)
                {
                    float currentHP = enemies[i].CurrentHP;
                    float damage = currentHP * (percentDamage / 100f);
                    enemies[i].TakeDamage(new DamageInfo
                    {
                        Damage = damage,
                        DamageType = DamageType.True,
                        SourcePosition = Vector3.zero,
                        IsAOEHit = true
                    });
                }
            }
        }


        // ========== 工具方法：获取所有存活敌人 ==========

        /// <summary>
        /// 查找场景中所有存活的敌人
        /// </summary>
        private EnemyBase[] FindAllAliveEnemies()
        {
            return FindObjectsOfType<EnemyBase>();
        }

        /// <summary>技能效果结束回调</summary>
        private void OnSkillEffectEnd()

        {
            var config = GetSkillConfig(_currentHeroId);
            if (config == null) return;

            Debug.Log($"[HeroSkillBattle] 技能效果结束: {config.ActiveEffect}");

            // 特殊处理：箭雨持续伤害结束、金币翻倍结束等
            switch (config.ActiveEffect)
            {
                case ActiveSkillEffectType.GoldRush:
                    Debug.Log("[Skill] 淘金热结束，金币恢复正常");
                    break;
                case ActiveSkillEffectType.BaseInvincible:
                    Debug.Log("[Skill] 无敌护盾结束");
                    break;
            }
        }

        // ========== 被动技能 ==========

        /// <summary>
        /// 应用被动技能效果
        /// </summary>
        private void ApplyPassiveEffects(HeroSkillConfig config, int heroLevel, int heroStar)
        {
            ClearPassiveEffects();

            float value = config.BasePassiveValue
                        + config.PassiveValuePerLevel * (heroLevel - 1)
                        + config.PassiveValuePerStar * heroStar;

            switch (config.PassiveEffect)
            {
                case PassiveSkillEffectType.BaseHPBonus:
                    _passiveBaseHPBonus = value;
                    Debug.Log($"[Passive] 基地生命+{value:F0}");
                    break;

                case PassiveSkillEffectType.TowerRangeBonus:
                    _passiveTowerRangeBonus = value / 100f; // 转为百分比
                    Debug.Log($"[Passive] 全塔射程+{value:F1}%");
                    break;

                case PassiveSkillEffectType.IceTowerBonus:
                    _passiveIceTowerBonus = value / 100f;
                    Debug.Log($"[Passive] 冰塔效果+{value:F1}%");
                    break;

                case PassiveSkillEffectType.FireRuneBonus:
                    _passiveFireRuneBonus = value / 100f;
                    Debug.Log($"[Passive] 火系词条+{value:F1}%");
                    break;

                case PassiveSkillEffectType.GoldMineBonus:
                    _passiveGoldMineBonus = value / 100f;
                    Debug.Log($"[Passive] 金矿产出+{value:F1}%");
                    break;

                case PassiveSkillEffectType.ExtraRuneChoice:
                    _passiveExtraRuneChoice = Mathf.RoundToInt(value);
                    Debug.Log($"[Passive] 词条选择+{_passiveExtraRuneChoice}张");
                    break;
            }
        }

        /// <summary>
        /// 清除被动效果
        /// </summary>
        private void ClearPassiveEffects()
        {
            _passiveTowerRangeBonus = 0f;
            _passiveBaseHPBonus = 0f;
            _passiveIceTowerBonus = 0f;
            _passiveFireRuneBonus = 0f;
            _passiveGoldMineBonus = 0f;
            _passiveExtraRuneChoice = 0;
        }

        // ========== 技能升级 ==========

        /// <summary>技能升级消耗金币表（技能等级=英雄等级，此处为额外技能强化）</summary>
        private static readonly int[] SkillUpgradeCost = {
            500, 800, 1200, 1600, 2000, 2500, 3000, 3600, 4200, 5000
        };

        /// <summary>
        /// 获取技能升级所需金币
        /// </summary>
        public int GetSkillUpgradeCost(string heroId)
        {
            var heroData = HeroSystem.HasInstance ? HeroSystem.Instance.GetHeroData(heroId) : null;
            if (heroData == null) return 99999;

            // 技能等级跟随英雄等级，此处返回英雄升级费用
            return HeroSystem.Instance.GetLevelUpGoldCost(heroId);
        }

        // ========== 查询方法 ==========

        /// <summary>
        /// 获取技能配置
        /// </summary>
        public static HeroSkillConfig GetSkillConfig(string heroId)
        {
            InitSkillConfigs();
            _skillConfigs.TryGetValue(heroId, out var config);
            return config;
        }

        /// <summary>
        /// 获取技能描述文本（含当前等级数值）
        /// </summary>
        public string GetActiveSkillDescription(string heroId)
        {
            var config = GetSkillConfig(heroId);
            if (config == null) return "无技能";

            var heroConfig = HeroConfigTable.GetHero(heroId);
            var heroData = HeroSystem.HasInstance ? HeroSystem.Instance.GetHeroData(heroId) : null;
            int level = heroData?.Level ?? 1;
            int star = heroData?.Star ?? 0;

            float damage = config.BaseDamage + config.DamagePerLevel * (level - 1);
            float starBonus = 1f + star * 0.1f;
            damage *= starBonus;

            float cd = Mathf.Max(config.BaseCooldown - config.CooldownPerLevel * (level - 1), 15f);

            string desc = heroConfig?.ActiveSkillDesc ?? "未知技能";
            return $"{desc}\n伤害: {damage:F0} | CD: {cd:F0}s | 持续: {config.Duration:F1}s";
        }

        /// <summary>
        /// 获取被动技能描述文本
        /// </summary>
        public string GetPassiveSkillDescription(string heroId)
        {
            var config = GetSkillConfig(heroId);
            if (config == null) return "无被动";

            var heroConfig = HeroConfigTable.GetHero(heroId);
            var heroData = HeroSystem.HasInstance ? HeroSystem.Instance.GetHeroData(heroId) : null;
            int level = heroData?.Level ?? 1;
            int star = heroData?.Star ?? 0;

            float value = config.BasePassiveValue
                        + config.PassiveValuePerLevel * (level - 1)
                        + config.PassiveValuePerStar * star;

            string desc = heroConfig?.PassiveSkillDesc ?? "未知被动";
            return $"{desc}\n当前效果: {value:F1}";
        }
    }
}
