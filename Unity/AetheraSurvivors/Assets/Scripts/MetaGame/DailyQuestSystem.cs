// ============================================================
// 文件名：DailyQuestSystem.cs
// 功能描述：每日任务、成就、签到、体力系统
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #258-260
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Data;

namespace AetheraSurvivors.MetaGame
{
    // ========== 任务配置 ==========

    /// <summary>任务类型</summary>
    public enum QuestType
    {
        ClearLevel,      // 通关关卡
        KillEnemy,       // 击杀怪物
        BuildTower,      // 建造塔
        UpgradeHero,     // 升级英雄
        SpendGold,       // 消耗金币
        Login,           // 登录
        WatchAd,         // 观看广告
        ShareGame        // 分享游戏
    }

    /// <summary>任务配置</summary>
    [Serializable]
    public class QuestConfig
    {
        public string QuestId;
        public string Name;
        public string Description;
        public QuestType Type;
        public int Target;
        public string RewardType; // "gold", "diamonds", "stamina", "exp_book"
        public int RewardAmount;
        public int ActivityPoints; // 完成获得的活跃度
    }

    // ========== 成就配置 ==========

    /// <summary>成就配置</summary>
    [Serializable]
    public class AchievementConfig
    {
        public string AchievementId;
        public string Name;
        public string Description;
        public string Category; // "battle", "hero", "collection", "social"
        public QuestType TrackType;
        public int Target;
        public string RewardType;
        public int RewardAmount;
    }

    // ========== 每日任务系统 ==========

    /// <summary>
    /// 每日任务系统管理器
    /// </summary>
    public class DailyQuestSystem : Singleton<DailyQuestSystem>
    {
        private List<QuestConfig> _dailyQuests;
        private int[] _activityRewardThresholds = { 20, 40, 60, 80, 100 };
        private int[] _activityRewardAmounts = { 100, 200, 300, 500, 1000 }; // 金币奖励

        protected override void OnInit()
        {
            InitDailyQuests();
            CheckDailyReset();
            Debug.Log("[DailyQuestSystem] 初始化完成");
        }

        protected override void OnDispose() { }

        /// <summary>获取每日任务列表</summary>
        public List<QuestConfig> GetDailyQuests() => _dailyQuests;

        /// <summary>更新任务进度</summary>
        public void UpdateProgress(QuestType type, int amount = 1)
        {
            if (!PlayerDataManager.HasInstance) return;
            var questData = EnsureQuestData();

            for (int i = 0; i < _dailyQuests.Count; i++)
            {
                var config = _dailyQuests[i];
                if (config.Type != type) continue;

                var progress = GetOrCreateProgress(questData, config.QuestId);
                if (progress.Completed) continue;

                progress.Progress = Mathf.Min(progress.Progress + amount, config.Target);

                if (progress.Progress >= config.Target && !progress.Completed)
                {
                    progress.Completed = true;

                    if (EventBus.HasInstance)
                    {
                        EventBus.Instance.Publish(new QuestProgressEvent
                        {
                            QuestId = config.QuestId,
                            Progress = progress.Progress,
                            Target = config.Target,
                            Completed = true
                        });
                    }
                }
            }

            PlayerDataManager.Instance.MarkDirty();
        }

        /// <summary>领取任务奖励</summary>
        public bool ClaimReward(string questId)
        {
            if (!PlayerDataManager.HasInstance) return false;
            var questData = EnsureQuestData();

            var progress = FindProgress(questData, questId);
            if (progress == null || !progress.Completed || progress.Claimed) return false;

            var config = GetQuestConfig(questId);
            if (config == null) return false;

            // 发放奖励
            DeliverReward(config.RewardType, config.RewardAmount);

            // 增加活跃度
            questData.ActivityPoints += config.ActivityPoints;

            progress.Claimed = true;
            PlayerDataManager.Instance.MarkDirty();
            PlayerDataManager.Instance.Save();

            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new QuestRewardClaimedEvent { QuestId = questId });
            }

            return true;
        }

        /// <summary>领取活跃度奖励</summary>
        public bool ClaimActivityReward(int threshold)
        {
            if (!PlayerDataManager.HasInstance) return false;
            var questData = EnsureQuestData();

            if (questData.ActivityPoints < threshold) return false;
            if (questData.ClaimedActivityRewards == null)
                questData.ClaimedActivityRewards = new List<int>();
            if (questData.ClaimedActivityRewards.Contains(threshold)) return false;

            // 找到对应奖励
            int rewardIdx = Array.IndexOf(_activityRewardThresholds, threshold);
            if (rewardIdx < 0) return false;

            PlayerDataManager.Instance.AddGold(_activityRewardAmounts[rewardIdx]);
            questData.ClaimedActivityRewards.Add(threshold);
            PlayerDataManager.Instance.MarkDirty();
            PlayerDataManager.Instance.Save();

            return true;
        }

        /// <summary>获取当前活跃度</summary>
        public int GetActivityPoints()
        {
            if (!PlayerDataManager.HasInstance) return 0;
            return EnsureQuestData().ActivityPoints;
        }

        /// <summary>获取活跃度奖励阈值</summary>
        public int[] GetActivityThresholds() => _activityRewardThresholds;

        private void InitDailyQuests()
        {
            _dailyQuests = new List<QuestConfig>
            {
                new QuestConfig
                {
                    QuestId = "daily_login", Name = "每日登录",
                    Description = "登录游戏", Type = QuestType.Login,
                    Target = 1, RewardType = "gold", RewardAmount = 200, ActivityPoints = 20
                },
                new QuestConfig
                {
                    QuestId = "daily_clear_3", Name = "通关3次",
                    Description = "通关任意关卡3次", Type = QuestType.ClearLevel,
                    Target = 3, RewardType = "diamonds", RewardAmount = 30, ActivityPoints = 20
                },
                new QuestConfig
                {
                    QuestId = "daily_kill_100", Name = "击杀100怪物",
                    Description = "击杀100个怪物", Type = QuestType.KillEnemy,
                    Target = 100, RewardType = "gold", RewardAmount = 500, ActivityPoints = 20
                },
                new QuestConfig
                {
                    QuestId = "daily_build_10", Name = "建造10座塔",
                    Description = "建造10座防御塔", Type = QuestType.BuildTower,
                    Target = 10, RewardType = "gold", RewardAmount = 300, ActivityPoints = 20
                },
                new QuestConfig
                {
                    QuestId = "daily_share", Name = "分享游戏",
                    Description = "分享游戏给好友", Type = QuestType.ShareGame,
                    Target = 1, RewardType = "diamonds", RewardAmount = 20, ActivityPoints = 20
                }
            };
        }

        private void CheckDailyReset()
        {
            if (!PlayerDataManager.HasInstance) return;
            var questData = EnsureQuestData();

            int today = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            if (questData.LastRefreshDate != today)
            {
                // 重置每日任务
                questData.LastRefreshDate = today;
                questData.Quests = new List<QuestProgress>();
                questData.ActivityPoints = 0;
                questData.ClaimedActivityRewards = new List<int>();
                PlayerDataManager.Instance.MarkDirty();
                PlayerDataManager.Instance.Save();

                // 自动完成登录任务
                UpdateProgress(QuestType.Login, 1);
            }
        }

        private DailyQuestSaveData EnsureQuestData()
        {
            var data = PlayerDataManager.Instance.Data;
            if (data.DailyQuestData == null)
            {
                data.DailyQuestData = new DailyQuestSaveData
                {
                    LastRefreshDate = 0,
                    Quests = new List<QuestProgress>(),
                    ActivityPoints = 0,
                    ClaimedActivityRewards = new List<int>()
                };
            }
            return data.DailyQuestData;
        }

        private QuestProgress GetOrCreateProgress(DailyQuestSaveData questData, string questId)
        {
            if (questData.Quests == null) questData.Quests = new List<QuestProgress>();

            for (int i = 0; i < questData.Quests.Count; i++)
            {
                if (questData.Quests[i].QuestId == questId)
                    return questData.Quests[i];
            }

            var newProgress = new QuestProgress
            {
                QuestId = questId, Progress = 0, Completed = false, Claimed = false
            };
            questData.Quests.Add(newProgress);
            return newProgress;
        }

        private QuestProgress FindProgress(DailyQuestSaveData questData, string questId)
        {
            if (questData.Quests == null) return null;
            for (int i = 0; i < questData.Quests.Count; i++)
            {
                if (questData.Quests[i].QuestId == questId)
                    return questData.Quests[i];
            }
            return null;
        }

        private QuestConfig GetQuestConfig(string questId)
        {
            for (int i = 0; i < _dailyQuests.Count; i++)
            {
                if (_dailyQuests[i].QuestId == questId)
                    return _dailyQuests[i];
            }
            return null;
        }

        private void DeliverReward(string rewardType, int amount)
        {
            if (!PlayerDataManager.HasInstance) return;
            switch (rewardType)
            {
                case "gold": PlayerDataManager.Instance.AddGold(amount); break;
                case "diamonds": PlayerDataManager.Instance.AddDiamonds(amount); break;
                case "stamina":
                    var data = PlayerDataManager.Instance.Data;
                    data.Stamina = Mathf.Min(data.Stamina + amount, data.MaxStamina * 2);
                    PlayerDataManager.Instance.MarkDirty();
                    break;
            }
        }
    }

    // ========== 成就系统 ==========

    /// <summary>
    /// 成就系统管理器
    /// </summary>
    public class AchievementSystem : Singleton<AchievementSystem>
    {
        private List<AchievementConfig> _achievements;
        private HashSet<string> _unlockedAchievements;

        protected override void OnInit()
        {
            _unlockedAchievements = new HashSet<string>();
            InitAchievements();
            LoadState();
            Debug.Log("[AchievementSystem] 初始化完成");
        }

        protected override void OnDispose()
        {
            SaveState();
        }

        /// <summary>获取所有成就</summary>
        public List<AchievementConfig> GetAllAchievements() => _achievements;

        /// <summary>检查成就是否已解锁</summary>
        public bool IsUnlocked(string achievementId) => _unlockedAchievements.Contains(achievementId);

        /// <summary>尝试解锁成就</summary>
        public bool TryUnlock(string achievementId)
        {
            if (_unlockedAchievements.Contains(achievementId)) return false;

            _unlockedAchievements.Add(achievementId);
            SaveState();

            var config = GetConfig(achievementId);
            if (config != null)
            {
                // 发放奖励
                if (!string.IsNullOrEmpty(config.RewardType))
                {
                    switch (config.RewardType)
                    {
                        case "gold": PlayerDataManager.Instance.AddGold(config.RewardAmount); break;
                        case "diamonds": PlayerDataManager.Instance.AddDiamonds(config.RewardAmount); break;
                    }
                }

                if (EventBus.HasInstance)
                {
                    EventBus.Instance.Publish(new AchievementUnlockedEvent
                    {
                        AchievementId = achievementId,
                        AchievementName = config.Name
                    });
                }
            }

            return true;
        }

        private AchievementConfig GetConfig(string id)
        {
            for (int i = 0; i < _achievements.Count; i++)
            {
                if (_achievements[i].AchievementId == id) return _achievements[i];
            }
            return null;
        }

        private void InitAchievements()
        {
            _achievements = new List<AchievementConfig>
            {
                new AchievementConfig { AchievementId = "first_clear", Name = "初次通关", Description = "通关第1关", Category = "battle", TrackType = QuestType.ClearLevel, Target = 1, RewardType = "diamonds", RewardAmount = 50 },
                new AchievementConfig { AchievementId = "clear_10", Name = "征战十场", Description = "通关10个关卡", Category = "battle", TrackType = QuestType.ClearLevel, Target = 10, RewardType = "diamonds", RewardAmount = 100 },
                new AchievementConfig { AchievementId = "clear_50", Name = "百战老兵", Description = "通关50个关卡", Category = "battle", TrackType = QuestType.ClearLevel, Target = 50, RewardType = "diamonds", RewardAmount = 300 },
                new AchievementConfig { AchievementId = "kill_1000", Name = "千人斩", Description = "累计击杀1000怪物", Category = "battle", TrackType = QuestType.KillEnemy, Target = 1000, RewardType = "gold", RewardAmount = 5000 },
                new AchievementConfig { AchievementId = "kill_10000", Name = "万夫不当", Description = "累计击杀10000怪物", Category = "battle", TrackType = QuestType.KillEnemy, Target = 10000, RewardType = "diamonds", RewardAmount = 500 },
                new AchievementConfig { AchievementId = "hero_lv10", Name = "初露锋芒", Description = "英雄升到10级", Category = "hero", TrackType = QuestType.UpgradeHero, Target = 10, RewardType = "gold", RewardAmount = 2000 },
                new AchievementConfig { AchievementId = "hero_lv30", Name = "身经百战", Description = "英雄升到30级", Category = "hero", TrackType = QuestType.UpgradeHero, Target = 30, RewardType = "diamonds", RewardAmount = 200 },
                new AchievementConfig { AchievementId = "build_100", Name = "建筑大师", Description = "累计建造100座塔", Category = "battle", TrackType = QuestType.BuildTower, Target = 100, RewardType = "gold", RewardAmount = 3000 },
            };
        }

        private void LoadState()
        {
            if (SaveManager.HasInstance)
            {
                var state = SaveManager.Instance.Load<AchievementSaveState>("achievement_state");
                if (state?.UnlockedIds != null)
                {
                    for (int i = 0; i < state.UnlockedIds.Count; i++)
                        _unlockedAchievements.Add(state.UnlockedIds[i]);
                }
            }
        }

        private void SaveState()
        {
            if (!SaveManager.HasInstance) return;
            var state = new AchievementSaveState
            {
                UnlockedIds = new List<string>(_unlockedAchievements)
            };
            SaveManager.Instance.Save("achievement_state", state);
        }
    }

    [Serializable]
    public class AchievementSaveState
    {
        public List<string> UnlockedIds;
    }

    // ========== 签到系统 ==========

    /// <summary>
    /// 签到系统管理器
    /// </summary>
    public class CheckInSystem : Singleton<CheckInSystem>
    {
        // 7天签到奖励
        private static readonly string[] RewardTypes = { "gold", "gold", "diamonds", "gold", "gold", "diamonds", "hero_fragment" };
        private static readonly int[] RewardAmounts = { 200, 300, 30, 400, 500, 50, 10 };

        protected override void OnInit()
        {
            Debug.Log("[CheckInSystem] 初始化完成");
        }

        protected override void OnDispose() { }

        /// <summary>执行签到</summary>
        public bool DoCheckIn()
        {
            if (!PlayerDataManager.HasInstance) return false;
            var data = PlayerDataManager.Instance.Data;
            if (data.CheckInData == null)
                data.CheckInData = new CheckInSaveData();

            int today = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            if (data.CheckInData.LastCheckInDate == today)
            {
                Debug.LogWarning("[CheckIn] 今日已签到");
                return false;
            }

            // 检查连续签到
            int yesterday = int.Parse(DateTime.Now.AddDays(-1).ToString("yyyyMMdd"));
            if (data.CheckInData.LastCheckInDate == yesterday)
            {
                data.CheckInData.ConsecutiveDays++;
            }
            else
            {
                data.CheckInData.ConsecutiveDays = 1;
            }

            data.CheckInData.LastCheckInDate = today;

            // 发放奖励（循环7天）
            int dayIndex = (data.CheckInData.ConsecutiveDays - 1) % 7;
            string rewardType = RewardTypes[dayIndex];
            int rewardAmount = RewardAmounts[dayIndex];

            switch (rewardType)
            {
                case "gold": PlayerDataManager.Instance.AddGold(rewardAmount); break;
                case "diamonds": PlayerDataManager.Instance.AddDiamonds(rewardAmount); break;
                case "hero_fragment":
                    HeroSystem.Instance.AddFragments("hero_chosen_one", rewardAmount);
                    break;
            }

            PlayerDataManager.Instance.MarkDirty();
            PlayerDataManager.Instance.Save();

            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new CheckInEvent
                {
                    Day = data.CheckInData.ConsecutiveDays,
                    ConsecutiveDays = data.CheckInData.ConsecutiveDays
                });
            }

            Debug.Log($"[CheckIn] 签到成功: 第{data.CheckInData.ConsecutiveDays}天, 奖励: {rewardType}×{rewardAmount}");
            return true;
        }

        /// <summary>今日是否已签到</summary>
        public bool HasCheckedInToday()
        {
            if (!PlayerDataManager.HasInstance) return false;
            var checkInData = PlayerDataManager.Instance.Data.CheckInData;
            if (checkInData == null) return false;
            int today = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            return checkInData.LastCheckInDate == today;
        }

        /// <summary>获取连续签到天数</summary>
        public int GetConsecutiveDays()
        {
            if (!PlayerDataManager.HasInstance) return 0;
            return PlayerDataManager.Instance.Data.CheckInData?.ConsecutiveDays ?? 0;
        }

        /// <summary>获取签到奖励信息</summary>
        public (string type, int amount) GetRewardForDay(int day)
        {
            int idx = (day - 1) % 7;
            return (RewardTypes[idx], RewardAmounts[idx]);
        }
    }

    // ========== 体力系统 ==========

    /// <summary>
    /// 体力系统管理器
    /// </summary>
    public class StaminaSystem : Singleton<StaminaSystem>
    {
        /// <summary>体力恢复间隔（秒）</summary>
        public const int RecoverIntervalSeconds = 300; // 5分钟恢复1点

        protected override void OnInit()
        {
            Debug.Log("[StaminaSystem] 初始化完成");
        }

        protected override void OnDispose() { }

        /// <summary>消耗体力</summary>
        public bool ConsumeStamina(int amount)
        {
            if (!PlayerDataManager.HasInstance) return false;
            var data = PlayerDataManager.Instance.Data;

            // 先计算恢复
            RecoverStamina();

            if (data.Stamina < amount)
            {
                Debug.LogWarning($"[Stamina] 体力不足: 需要{amount}, 拥有{data.Stamina}");
                return false;
            }

            int oldStamina = data.Stamina;
            data.Stamina -= amount;

            // 如果体力从满变为不满，记录恢复起始时间
            if (data.Stamina < data.MaxStamina && data.StaminaRecoverTime == 0)
            {
                data.StaminaRecoverTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            PlayerDataManager.Instance.MarkDirty();
            PlayerDataManager.Instance.Save();

            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new StaminaChangedEvent
                {
                    OldStamina = oldStamina,
                    NewStamina = data.Stamina,
                    MaxStamina = data.MaxStamina
                });
            }

            return true;
        }

        /// <summary>增加体力（可超过上限）</summary>
        public void AddStamina(int amount)
        {
            if (!PlayerDataManager.HasInstance || amount <= 0) return;
            var data = PlayerDataManager.Instance.Data;

            int oldStamina = data.Stamina;
            data.Stamina = Mathf.Min(data.Stamina + amount, data.MaxStamina * 2);

            PlayerDataManager.Instance.MarkDirty();

            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new StaminaChangedEvent
                {
                    OldStamina = oldStamina,
                    NewStamina = data.Stamina,
                    MaxStamina = data.MaxStamina
                });
            }
        }

        /// <summary>计算并恢复体力</summary>
        public void RecoverStamina()
        {
            if (!PlayerDataManager.HasInstance) return;
            var data = PlayerDataManager.Instance.Data;

            if (data.Stamina >= data.MaxStamina)
            {
                data.StaminaRecoverTime = 0;
                return;
            }

            if (data.StaminaRecoverTime <= 0)
            {
                data.StaminaRecoverTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long elapsed = now - data.StaminaRecoverTime;
            int recovered = (int)(elapsed / RecoverIntervalSeconds);

            if (recovered > 0)
            {
                int oldStamina = data.Stamina;
                data.Stamina = Mathf.Min(data.Stamina + recovered, data.MaxStamina);
                data.StaminaRecoverTime = now - (elapsed % RecoverIntervalSeconds);

                if (data.Stamina >= data.MaxStamina)
                {
                    data.StaminaRecoverTime = 0;
                }

                PlayerDataManager.Instance.MarkDirty();
            }
        }

        /// <summary>获取下次恢复剩余秒数</summary>
        public int GetNextRecoverSeconds()
        {
            if (!PlayerDataManager.HasInstance) return 0;
            var data = PlayerDataManager.Instance.Data;

            if (data.Stamina >= data.MaxStamina) return 0;
            if (data.StaminaRecoverTime <= 0) return RecoverIntervalSeconds;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long elapsed = now - data.StaminaRecoverTime;
            int remaining = RecoverIntervalSeconds - (int)(elapsed % RecoverIntervalSeconds);
            return remaining;
        }
    }
}
