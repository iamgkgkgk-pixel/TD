// ============================================================
// 文件名：BattlePassSystem.cs
// 功能描述：战令/赛季通行证系统 — 免费/付费双轨奖励、经验获取
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #257
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Data;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>
    /// 战令奖励配置
    /// </summary>
    [Serializable]
    public class BattlePassReward
    {
        public int Level;
        public string FreeRewardType;  // "gold", "diamonds", "summon_ticket", "hero_fragment", "exp_book"
        public int FreeRewardAmount;
        public string PremiumRewardType;
        public int PremiumRewardAmount;
        public string PremiumRewardHeroId; // 如果是英雄碎片
    }

    /// <summary>
    /// 战令系统管理器
    /// 
    /// 规则：
    /// - 60级双轨奖励
    /// - 每级需要100经验
    /// - 赛季30天重置
    /// - 付费战令68元
    /// </summary>
    public class BattlePassSystem : Singleton<BattlePassSystem>
    {
        // ========== 常量 ==========
        public const int MaxLevel = 60;
        public const long ExpPerLevel = 100;
        public const int SeasonDurationDays = 30;
        public const int PremiumPrice = 68; // RMB

        // ========== 奖励表 ==========
        private List<BattlePassReward> _rewards;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            InitRewards();
            CheckSeasonReset();
            Debug.Log("[BattlePassSystem] 初始化完成");
        }

        protected override void OnDispose() { }

        // ========== 公共方法 ==========

        /// <summary>获取当前战令等级</summary>
        public int GetCurrentLevel()
        {
            if (!PlayerDataManager.HasInstance) return 0;
            return PlayerDataManager.Instance.Data.BattlePassData?.Level ?? 0;
        }

        /// <summary>获取当前经验</summary>
        public long GetCurrentExp()
        {
            if (!PlayerDataManager.HasInstance) return 0;
            return PlayerDataManager.Instance.Data.BattlePassData?.Exp ?? 0;
        }

        /// <summary>是否购买了付费战令</summary>
        public bool IsPremium()
        {
            if (!PlayerDataManager.HasInstance) return false;
            return PlayerDataManager.Instance.Data.BattlePassData?.IsPremium ?? false;
        }

        /// <summary>增加战令经验</summary>
        public void AddExp(long amount)
        {
            if (!PlayerDataManager.HasInstance || amount <= 0) return;

            var bpData = EnsureBPData();
            int oldLevel = bpData.Level;
            bpData.Exp += amount;

            // 升级检查
            while (bpData.Exp >= ExpPerLevel && bpData.Level < MaxLevel)
            {
                bpData.Exp -= ExpPerLevel;
                bpData.Level++;
            }

            // 满级后经验不再累积
            if (bpData.Level >= MaxLevel)
            {
                bpData.Exp = 0;
            }

            PlayerDataManager.Instance.MarkDirty();
            PlayerDataManager.Instance.Save();

            if (EventBus.HasInstance && bpData.Level != oldLevel)
            {
                EventBus.Instance.Publish(new BattlePassExpGainEvent
                {
                    ExpGained = amount,
                    OldLevel = oldLevel,
                    NewLevel = bpData.Level
                });
            }
        }

        /// <summary>购买付费战令</summary>
        public bool PurchasePremium()
        {
            if (!PlayerDataManager.HasInstance) return false;
            var bpData = EnsureBPData();

            if (bpData.IsPremium)
            {
                Debug.LogWarning("[BattlePass] 已购买付费战令");
                return false;
            }

            // 模拟RMB支付
            bpData.IsPremium = true;
            PlayerDataManager.Instance.MarkDirty();
            PlayerDataManager.Instance.Save();

            Debug.Log("[BattlePass] 购买付费战令成功");
            return true;
        }

        /// <summary>领取免费轨奖励</summary>
        public bool ClaimFreeReward(int level)
        {
            if (!PlayerDataManager.HasInstance) return false;
            var bpData = EnsureBPData();

            if (bpData.Level < level)
            {
                Debug.LogWarning($"[BattlePass] 等级不足: 需要{level}, 当前{bpData.Level}");
                return false;
            }

            if (bpData.ClaimedFree == null) bpData.ClaimedFree = new List<int>();
            if (bpData.ClaimedFree.Contains(level))
            {
                Debug.LogWarning($"[BattlePass] 免费奖励已领取: Lv.{level}");
                return false;
            }

            var reward = GetReward(level);
            if (reward == null) return false;

            // 发放奖励
            DeliverReward(reward.FreeRewardType, reward.FreeRewardAmount, null);
            bpData.ClaimedFree.Add(level);

            PlayerDataManager.Instance.MarkDirty();
            PlayerDataManager.Instance.Save();

            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new BattlePassRewardClaimedEvent
                {
                    Level = level, IsPremium = false
                });
            }

            return true;
        }

        /// <summary>领取付费轨奖励</summary>
        public bool ClaimPremiumReward(int level)
        {
            if (!PlayerDataManager.HasInstance) return false;
            var bpData = EnsureBPData();

            if (!bpData.IsPremium)
            {
                Debug.LogWarning("[BattlePass] 未购买付费战令");
                return false;
            }

            if (bpData.Level < level) return false;

            if (bpData.ClaimedPremium == null) bpData.ClaimedPremium = new List<int>();
            if (bpData.ClaimedPremium.Contains(level)) return false;

            var reward = GetReward(level);
            if (reward == null) return false;

            DeliverReward(reward.PremiumRewardType, reward.PremiumRewardAmount, reward.PremiumRewardHeroId);
            bpData.ClaimedPremium.Add(level);

            PlayerDataManager.Instance.MarkDirty();
            PlayerDataManager.Instance.Save();

            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new BattlePassRewardClaimedEvent
                {
                    Level = level, IsPremium = true
                });
            }

            return true;
        }

        /// <summary>一键领取所有可领取的奖励</summary>
        public int ClaimAllAvailable()
        {
            int claimed = 0;
            var bpData = EnsureBPData();

            for (int lv = 1; lv <= bpData.Level && lv <= MaxLevel; lv++)
            {
                if (bpData.ClaimedFree == null || !bpData.ClaimedFree.Contains(lv))
                {
                    if (ClaimFreeReward(lv)) claimed++;
                }
                if (bpData.IsPremium && (bpData.ClaimedPremium == null || !bpData.ClaimedPremium.Contains(lv)))
                {
                    if (ClaimPremiumReward(lv)) claimed++;
                }
            }

            return claimed;
        }

        /// <summary>获取奖励配置</summary>
        public BattlePassReward GetReward(int level)
        {
            if (_rewards == null) InitRewards();
            for (int i = 0; i < _rewards.Count; i++)
            {
                if (_rewards[i].Level == level) return _rewards[i];
            }
            return null;
        }

        /// <summary>获取所有奖励配置</summary>
        public List<BattlePassReward> GetAllRewards() => _rewards;

        private void DeliverReward(string rewardType, int amount, string heroId)
        {
            if (!PlayerDataManager.HasInstance) return;

            switch (rewardType)
            {
                case "gold":
                    PlayerDataManager.Instance.AddGold(amount);
                    break;
                case "diamonds":
                    PlayerDataManager.Instance.AddDiamonds(amount);
                    break;
                case "hero_fragment":
                    if (!string.IsNullOrEmpty(heroId))
                        HeroSystem.Instance.AddFragments(heroId, amount);
                    break;
                case "stamina":
                    var sdata = PlayerDataManager.Instance.Data;
                    sdata.Stamina = Mathf.Min(sdata.Stamina + amount, sdata.MaxStamina * 2);
                    PlayerDataManager.Instance.MarkDirty();
                    break;
                case "summon_ticket":
                    var tdata = PlayerDataManager.Instance.Data;
                    tdata.SummonTickets += amount;
                    PlayerDataManager.Instance.MarkDirty();
                    break;
            }
        }

        /// <summary>检查免费奖励是否已领取</summary>
        public bool IsFreeRewardClaimed(int level)
        {
            var bpData = EnsureBPData();
            return bpData.ClaimedFree != null && bpData.ClaimedFree.Contains(level);
        }

        /// <summary>检查付费奖励是否已领取</summary>
        public bool IsPremiumRewardClaimed(int level)
        {
            var bpData = EnsureBPData();
            return bpData.ClaimedPremium != null && bpData.ClaimedPremium.Contains(level);
        }

        // ========== 私有方法 ==========

        private BattlePassSaveData EnsureBPData()
        {
            var data = PlayerDataManager.Instance.Data;
            if (data.BattlePassData == null)
            {
                data.BattlePassData = new BattlePassSaveData
                {
                    SeasonId = 1, Level = 0, Exp = 0,
                    IsPremium = false,
                    ClaimedFree = new List<int>(),
                    ClaimedPremium = new List<int>(),
                    SeasonStartTimestamp = 0
                };
            }
            return data.BattlePassData;
        }

        private void CheckSeasonReset()
        {
            if (!PlayerDataManager.HasInstance) return;
            var bpData = EnsureBPData();

            // 计算赛季结束时间
            if (bpData.SeasonStartTimestamp <= 0)
            {
                // 首次初始化赛季起始时间
                bpData.SeasonStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                PlayerDataManager.Instance.MarkDirty();
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long seasonEnd = bpData.SeasonStartTimestamp + SeasonDurationDays * 86400L;

            if (now >= seasonEnd)
            {
                // 赛季过期，重置战令
                Debug.Log("[BattlePass] 赛季过期，重置战令数据");
                bpData.SeasonId++;
                bpData.Level = 0;
                bpData.Exp = 0;
                bpData.IsPremium = false;
                bpData.ClaimedFree = new List<int>();
                bpData.ClaimedPremium = new List<int>();
                bpData.SeasonStartTimestamp = now;
                PlayerDataManager.Instance.MarkDirty();
                PlayerDataManager.Instance.Save();
            }
        }

        /// <summary>获取赛季剩余天数</summary>
        public int GetRemainingDays()
        {
            if (!PlayerDataManager.HasInstance) return SeasonDurationDays;
            var bpData = EnsureBPData();

            if (bpData.SeasonStartTimestamp <= 0) return SeasonDurationDays;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long seasonEnd = bpData.SeasonStartTimestamp + SeasonDurationDays * 86400L;
            long remainingSeconds = seasonEnd - now;

            return Mathf.Max(0, (int)(remainingSeconds / 86400L));
        }

        private void InitRewards()
        {
            _rewards = new List<BattlePassReward>();

            // 生成60级奖励（简化版，每5级一个大奖）
            for (int lv = 1; lv <= MaxLevel; lv++)
            {
                var reward = new BattlePassReward { Level = lv };

                if (lv % 10 == 0) // 每10级大奖
                {
                    reward.FreeRewardType = "diamonds";
                    reward.FreeRewardAmount = 100;
                    reward.PremiumRewardType = "hero_fragment";
                    reward.PremiumRewardAmount = 10;
                    reward.PremiumRewardHeroId = "hero_chosen_one";
                }
                else if (lv % 5 == 0) // 每5级中奖
                {
                    reward.FreeRewardType = "diamonds";
                    reward.FreeRewardAmount = 50;
                    reward.PremiumRewardType = "diamonds";
                    reward.PremiumRewardAmount = 100;
                }
                else if (lv % 2 == 0) // 偶数级
                {
                    reward.FreeRewardType = "gold";
                    reward.FreeRewardAmount = 500 + lv * 50;
                    reward.PremiumRewardType = "gold";
                    reward.PremiumRewardAmount = 1000 + lv * 100;
                }
                else // 奇数级
                {
                    reward.FreeRewardType = "gold";
                    reward.FreeRewardAmount = 300 + lv * 30;
                    reward.PremiumRewardType = "diamonds";
                    reward.PremiumRewardAmount = 20;
                }

                _rewards.Add(reward);
            }
        }
    }
}
