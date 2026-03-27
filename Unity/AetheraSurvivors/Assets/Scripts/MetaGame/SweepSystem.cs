// ============================================================
// 文件名：SweepSystem.cs
// 功能描述：扫荡系统 — 3星通关后快速扫荡，消耗体力直接发放奖励
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #250
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Data;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>
    /// 扫荡结果数据
    /// </summary>
    [Serializable]
    public class SweepResult
    {
        public int Chapter;
        public int Level;
        public int StaminaCost;
        public List<SweepRewardItem> Rewards;
        public long ExpGained;
        public int BattlePassExpGained;
    }

    /// <summary>
    /// 扫荡奖励条目
    /// </summary>
    [Serializable]
    public class SweepRewardItem
    {
        public string RewardType; // "gold", "diamonds", "exp_book", "hero_fragment", "tower_fragment"
        public string RewardId;   // 具体物品ID（如英雄碎片的英雄ID）
        public int Amount;
    }

    /// <summary>
    /// 扫荡完成事件
    /// </summary>
    public struct SweepCompletedEvent : IEvent
    {
        public int Chapter;
        public int Level;
        public int SweepCount;
        public int TotalGold;
        public int TotalDiamonds;
    }

    /// <summary>
    /// 扫荡系统管理器
    /// 
    /// 规则：
    /// 1. 只有3星通关的关卡才能扫荡
    /// 2. 每次扫荡消耗与正常战斗相同的体力
    /// 3. 奖励基于关卡配置的掉落表（含随机浮动）
    /// 4. 支持批量扫荡（一次扫荡多次）
    /// 5. 扫荡结果计入每日任务进度
    /// </summary>
    public class SweepSystem : Singleton<SweepSystem>
    {
        // ========== 常量 ==========

        /// <summary>单次扫荡最大次数</summary>
        public const int MaxSweepCount = 10;

        /// <summary>基础体力消耗（随章节递增）</summary>
        private const int BaseStaminaCost = 6;

        // ========== 关卡奖励配置 ==========

        /// <summary>
        /// 获取关卡的体力消耗
        /// </summary>
        public int GetStaminaCost(int chapter, int level)
        {
            return BaseStaminaCost + (chapter - 1);
        }

        /// <summary>
        /// 检查关卡是否可以扫荡
        /// </summary>
        public bool CanSweep(int chapter, int level)
        {
            return GetLevelStars(chapter, level) >= 3;
        }

        /// <summary>
        /// 获取最大可扫荡次数（受体力限制）
        /// </summary>
        public int GetMaxSweepTimes(int chapter, int level)
        {
            if (!CanSweep(chapter, level)) return 0;
            if (!PlayerDataManager.HasInstance) return 0;

            int staminaCost = GetStaminaCost(chapter, level);
            int currentStamina = PlayerDataManager.Instance.Data.Stamina;

            // 先恢复体力
            if (StaminaSystem.HasInstance)
                StaminaSystem.Instance.RecoverStamina();

            currentStamina = PlayerDataManager.Instance.Data.Stamina;
            int maxTimes = staminaCost > 0 ? currentStamina / staminaCost : 0;
            return Mathf.Min(maxTimes, MaxSweepCount);
        }

        /// <summary>
        /// 执行单次扫荡
        /// </summary>
        public SweepResult DoSweep(int chapter, int level)
        {
            if (!CanSweep(chapter, level))
            {
                Debug.LogWarning($"[Sweep] 关卡 {chapter}-{level} 未达到3星，无法扫荡");
                return null;
            }

            int staminaCost = GetStaminaCost(chapter, level);

            // 消耗体力
            if (StaminaSystem.HasInstance)
            {
                if (!StaminaSystem.Instance.ConsumeStamina(staminaCost))
                {
                    Debug.LogWarning("[Sweep] 体力不足");
                    return null;
                }
            }

            // 生成奖励
            var rewards = GenerateRewards(chapter, level);

            // 发放奖励
            int totalGold = 0;
            int totalDiamonds = 0;
            foreach (var reward in rewards)
            {
                DeliverReward(reward);
                if (reward.RewardType == "gold") totalGold += reward.Amount;
                if (reward.RewardType == "diamonds") totalDiamonds += reward.Amount;
            }

            // 战令经验
            int bpExp = 10 + chapter;
            if (BattlePassSystem.HasInstance)
                BattlePassSystem.Instance.AddExp(bpExp);

            // 更新每日任务进度
            if (DailyQuestSystem.HasInstance)
            {
                DailyQuestSystem.Instance.UpdateProgress(QuestType.ClearLevel, 1);
                DailyQuestSystem.Instance.UpdateProgress(QuestType.KillEnemy, 20 + chapter * 5);
            }

            var result = new SweepResult
            {
                Chapter = chapter,
                Level = level,
                StaminaCost = staminaCost,
                Rewards = rewards,
                ExpGained = 50 + chapter * 10 + level * 5,
                BattlePassExpGained = bpExp
            };

            Debug.Log($"[Sweep] 扫荡完成: {chapter}-{level}, 金币+{totalGold}, 钻石+{totalDiamonds}");
            return result;
        }

        /// <summary>
        /// 批量扫荡
        /// </summary>
        public List<SweepResult> DoBatchSweep(int chapter, int level, int count)
        {
            count = Mathf.Clamp(count, 1, MaxSweepCount);
            var results = new List<SweepResult>();

            int totalGold = 0;
            int totalDiamonds = 0;

            for (int i = 0; i < count; i++)
            {
                var result = DoSweep(chapter, level);
                if (result == null) break; // 体力不足，停止

                results.Add(result);

                foreach (var reward in result.Rewards)
                {
                    if (reward.RewardType == "gold") totalGold += reward.Amount;
                    if (reward.RewardType == "diamonds") totalDiamonds += reward.Amount;
                }
            }

            // 发布扫荡完成事件
            if (results.Count > 0 && EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new SweepCompletedEvent
                {
                    Chapter = chapter,
                    Level = level,
                    SweepCount = results.Count,
                    TotalGold = totalGold,
                    TotalDiamonds = totalDiamonds
                });
            }

            if (PlayerDataManager.HasInstance)
            {
                PlayerDataManager.Instance.MarkDirty();
                PlayerDataManager.Instance.Save();
            }

            Debug.Log($"[Sweep] 批量扫荡完成: {chapter}-{level} ×{results.Count}");
            return results;
        }

        // ========== 奖励生成 ==========

        /// <summary>
        /// 根据关卡配置生成随机奖励
        /// </summary>
        private List<SweepRewardItem> GenerateRewards(int chapter, int level)
        {
            var rewards = new List<SweepRewardItem>();

            // 基础金币奖励（随章节递增，含±20%随机浮动）
            int baseGold = 100 + chapter * 30 + level * 10;
            int goldVariance = Mathf.RoundToInt(baseGold * 0.2f);
            int finalGold = baseGold + UnityEngine.Random.Range(-goldVariance, goldVariance + 1);
            rewards.Add(new SweepRewardItem
            {
                RewardType = "gold",
                RewardId = "",
                Amount = Mathf.Max(finalGold, 50)
            });

            // 经验书奖励（概率掉落）
            if (UnityEngine.Random.value < 0.6f)
            {
                int expBookAmount = 1 + chapter / 5;
                rewards.Add(new SweepRewardItem
                {
                    RewardType = "exp_book",
                    RewardId = "exp_book_small",
                    Amount = expBookAmount
                });
            }

            // 钻石奖励（低概率）
            float diamondChance = 0.05f + chapter * 0.005f; // 5%~20%
            if (UnityEngine.Random.value < diamondChance)
            {
                rewards.Add(new SweepRewardItem
                {
                    RewardType = "diamonds",
                    RewardId = "",
                    Amount = UnityEngine.Random.Range(1, 5 + chapter / 3)
                });
            }

            // 英雄碎片（困难章节概率掉落）
            if (chapter >= 5 && UnityEngine.Random.value < 0.08f)
            {
                // 随机选择一个英雄
                var heroes = HeroConfigTable.GetAllHeroes();
                if (heroes.Count > 0)
                {
                    var hero = heroes[UnityEngine.Random.Range(0, heroes.Count)];
                    rewards.Add(new SweepRewardItem
                    {
                        RewardType = "hero_fragment",
                        RewardId = hero.Id,
                        Amount = UnityEngine.Random.Range(1, 3)
                    });
                }
            }

            // 塔碎片/升级材料（中等概率）
            if (UnityEngine.Random.value < 0.3f)
            {
                string[] towerIds = { "Archer", "Mage", "Ice", "Cannon", "Poison", "GoldMine" };
                string towerId = towerIds[UnityEngine.Random.Range(0, towerIds.Length)];
                rewards.Add(new SweepRewardItem
                {
                    RewardType = "tower_fragment",
                    RewardId = towerId,
                    Amount = UnityEngine.Random.Range(1, 4)
                });
            }

            return rewards;
        }

        /// <summary>
        /// 发放单个奖励
        /// </summary>
        private void DeliverReward(SweepRewardItem reward)
        {
            if (!PlayerDataManager.HasInstance) return;

            switch (reward.RewardType)
            {
                case "gold":
                    PlayerDataManager.Instance.AddGold(reward.Amount);
                    break;

                case "diamonds":
                    PlayerDataManager.Instance.AddDiamonds(reward.Amount);
                    break;

                case "exp_book":
                    // 经验书存入背包（简化：直接转化为金币价值）
                    PlayerDataManager.Instance.AddGold(reward.Amount * 50);
                    break;

                case "hero_fragment":
                    if (HeroSystem.HasInstance && !string.IsNullOrEmpty(reward.RewardId))
                    {
                        HeroSystem.Instance.AddFragments(reward.RewardId, reward.Amount);
                    }
                    break;

                case "tower_fragment":
                    // 塔碎片存入图鉴系统
                    if (TowerCollectionSystem.HasInstance && !string.IsNullOrEmpty(reward.RewardId))
                    {
                        TowerCollectionSystem.Instance.AddFragments(reward.RewardId, reward.Amount);
                    }
                    break;
            }
        }

        // ========== 工具方法 ==========

        /// <summary>
        /// 获取关卡星级
        /// </summary>
        private int GetLevelStars(int chapter, int level)
        {
            if (!PlayerDataManager.HasInstance) return 0;
            var data = PlayerDataManager.Instance.Data;
            if (data.LevelStars == null) return 0;

            for (int i = 0; i < data.LevelStars.Count; i++)
            {
                var ls = data.LevelStars[i];
                if (ls.Chapter == chapter && ls.Level == level)
                    return ls.Stars;
            }
            return 0;
        }

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            Debug.Log("[SweepSystem] 初始化完成");
        }

        protected override void OnDispose() { }
    }
}
