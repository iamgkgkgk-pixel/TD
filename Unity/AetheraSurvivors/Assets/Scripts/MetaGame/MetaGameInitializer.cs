// ============================================================
// 文件名：MetaGameInitializer.cs
// 功能描述：元游戏系统初始化编排器 & 联调桥接
//          负责所有外围系统的初始化顺序、数据流通、经济闭环
//          战斗结算→外围系统联动（经验/金币/任务/战令/成就）
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #311-340（联调）
// ============================================================

using System;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Data;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>
    /// 元游戏系统初始化编排器
    /// 
    /// 职责：
    /// 1. 按正确顺序初始化所有外围系统
    /// 2. 注册系统间的事件联动
    /// 3. 战斗结算后的数据分发
    /// 4. 经济闭环验证
    /// 5. 新玩家首次进入的初始化
    /// 
    /// 初始化顺序：
    ///   PlayerDataManager → SaveManager → 各业务系统 → RedDotManager → UI
    /// </summary>
    public class MetaGameInitializer : MonoSingleton<MetaGameInitializer>
    {
        // ========== 初始化状态 ==========
        private bool _systemsReady = false;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            Debug.Log("[MetaGameInitializer] ========== 开始初始化元游戏系统 ==========");

            InitializeSystems();
            RegisterEventBridges();
            InitializeNewPlayer();

            _systemsReady = true;
            Debug.Log("[MetaGameInitializer] ========== 元游戏系统初始化完成 ==========");
        }

        protected override void OnDispose()
        {
            UnregisterEventBridges();
        }

        // ========== 公共方法 ==========

        /// <summary>系统是否就绪</summary>
        public bool IsReady => _systemsReady;

        /// <summary>
        /// 处理战斗结算 — 核心联调入口
        /// 战斗结束后调用此方法，分发奖励到各系统
        /// </summary>
        public void ProcessBattleResult(BattleResultData result)
        {
            if (!_systemsReady)
            {
                Debug.LogError("[MetaGameInitializer] 系统未就绪，无法处理战斗结算");
                return;
            }

            Debug.Log($"[MetaGameInitializer] 处理战斗结算: 章节{result.Chapter}-关卡{result.Level}, " +
                      $"胜利={result.IsVictory}, 星级={result.Stars}, 击杀={result.KillCount}");

            // 1. 更新关卡进度
            ProcessLevelProgress(result);

            // 2. 发放金币奖励
            ProcessGoldReward(result);

            // 3. 更新统计数据
            ProcessStatistics(result);

            // 4. 更新每日任务进度
            ProcessDailyQuests(result);

            // 5. 增加战令经验
            ProcessBattlePassExp(result);

            // 6. 检查成就
            ProcessAchievements(result);

            // 7. 增加玩家经验
            ProcessPlayerExp(result);

            // 8. 刷新红点
            RefreshAllRedDots();

            // 9. 保存数据
            if (PlayerDataManager.HasInstance)
            {
                PlayerDataManager.Instance.Save();
            }

            Debug.Log("[MetaGameInitializer] 战斗结算处理完成");
        }

        /// <summary>刷新所有红点状态</summary>
        public void RefreshAllRedDots()
        {
            if (RedDotManager.HasInstance)
            {
                RedDotManager.Instance.RefreshAll();
            }
        }

        /// <summary>触发签到弹窗（主界面打开时调用）</summary>
        public void TryShowCheckIn()
        {
            if (CheckInSystem.HasInstance && !CheckInSystem.Instance.HasCheckedInToday())
            {
                UIManager.Instance.Open<CheckInPanel>();
            }
        }

        /// <summary>触发新手引导（主界面打开时调用）</summary>
        public void TryTriggerGuide(GuideTrigger trigger)
        {
            if (MetaGuidanceSystem.HasInstance)
            {
                MetaGuidanceSystem.Instance.TryTrigger(trigger);
            }
        }

        // ========== 系统初始化 ==========

        private void InitializeSystems()
        {
            // 第一层：数据层（已由GameManager初始化）
            // PlayerDataManager, SaveManager 应该已经就绪

            // 第二层：核心业务系统
            Debug.Log("[MetaGameInitializer] 初始化英雄系统...");
            HeroSystem.Instance.Initialize();

            Debug.Log("[MetaGameInitializer] 初始化商城系统...");
            ShopSystem.Instance.Initialize();

            Debug.Log("[MetaGameInitializer] 初始化战令系统...");
            BattlePassSystem.Instance.Initialize();

            Debug.Log("[MetaGameInitializer] 初始化抽卡系统...");
            GachaSystem.Instance.Initialize();

            Debug.Log("[MetaGameInitializer] 初始化每日任务系统...");
            DailyQuestSystem.Instance.Initialize();

            Debug.Log("[MetaGameInitializer] 初始化成就系统...");
            AchievementSystem.Instance.Initialize();

            Debug.Log("[MetaGameInitializer] 初始化签到系统...");
            CheckInSystem.Instance.Initialize();

            Debug.Log("[MetaGameInitializer] 初始化体力系统...");
            StaminaSystem.Instance.Initialize();

            // 测试模式：确保体力无限
            if (Data.PlayerDataManager.HasInstance)
            {
                var data = Data.PlayerDataManager.Instance.Data;
                if (data.Stamina < 1000)
                {
                    data.Stamina = 99999;
                    data.MaxStamina = 99999;
                    Data.PlayerDataManager.Instance.MarkDirty();
                    Debug.Log("[MetaGameInitializer] 测试模式：体力已设为无限");
                }
            }

            // 第三层：社交和通知系统
            Debug.Log("[MetaGameInitializer] 初始化社交系统...");
            SocialSystem.Instance.Initialize();

            Debug.Log("[MetaGameInitializer] 初始化邮件系统...");
            MailSystem.Instance.Initialize();

            // 第四层：红点系统（依赖上面所有系统）
            Debug.Log("[MetaGameInitializer] 初始化红点系统...");
            RedDotManager.Instance.Initialize();

            // 第五层：引导系统
            Debug.Log("[MetaGameInitializer] 初始化引导系统...");
            MetaGuidanceSystem.Instance.Initialize();

            // 初始刷新红点
            RedDotManager.Instance.RefreshAll();
        }

        // ========== 事件桥接注册 ==========

        private void RegisterEventBridges()
        {
            if (!EventBus.HasInstance) return;

            // 货币变化 → 刷新红点
            EventBus.Instance.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);

            // 英雄解锁 → 成就检查 + 邮件通知
            EventBus.Instance.Subscribe<HeroUnlockedEvent>(OnHeroUnlocked);

            // 英雄升级 → 成就检查 + 任务进度
            EventBus.Instance.Subscribe<HeroLevelUpEvent>(OnHeroLevelUp);

            // 关卡通关 → 多系统联动
            EventBus.Instance.Subscribe<LevelCompletedEvent>(OnLevelCompleted);

            // 抽卡结果 → 战令经验
            EventBus.Instance.Subscribe<GachaResultEvent>(OnGachaResult);

            // 商品购买 → 战令经验 + 任务进度
            EventBus.Instance.Subscribe<ShopPurchaseEvent>(OnShopPurchase);

            Debug.Log("[MetaGameInitializer] 事件桥接注册完成");
        }

        private void UnregisterEventBridges()
        {
            if (!EventBus.HasInstance) return;

            EventBus.Instance.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Instance.Unsubscribe<HeroUnlockedEvent>(OnHeroUnlocked);
            EventBus.Instance.Unsubscribe<HeroLevelUpEvent>(OnHeroLevelUp);
            EventBus.Instance.Unsubscribe<LevelCompletedEvent>(OnLevelCompleted);
            EventBus.Instance.Unsubscribe<GachaResultEvent>(OnGachaResult);
            EventBus.Instance.Unsubscribe<ShopPurchaseEvent>(OnShopPurchase);
        }

        // ========== 事件回调 ==========

        private void OnCurrencyChanged(CurrencyChangedEvent evt)
        {
            // 消耗金币 → 更新任务进度
            if (evt.CurrencyType == "gold" && evt.Delta < 0)
            {
                if (DailyQuestSystem.HasInstance)
                    DailyQuestSystem.Instance.UpdateProgress(QuestType.SpendGold, (int)(-evt.Delta));
            }

            // 刷新红点
            RefreshAllRedDots();
        }

        private void OnHeroUnlocked(HeroUnlockedEvent evt)
        {
            // 发送邮件通知
            if (MailSystem.HasInstance)
            {
                var heroConfig = HeroConfigTable.GetHero(evt.HeroId);
                string heroName = heroConfig?.Name ?? evt.HeroId;
                MailSystem.Instance.SendSystemMail(
                    $"🎉 新英雄解锁！",
                    $"恭喜你获得了新英雄「{heroName}」！\n快去英雄面板查看吧！");
            }

            // 触发引导
            TryTriggerGuide(GuideTrigger.FirstGetHero);
        }

        private void OnHeroLevelUp(HeroLevelUpEvent evt)
        {
            // 更新任务进度
            if (DailyQuestSystem.HasInstance)
                DailyQuestSystem.Instance.UpdateProgress(QuestType.UpgradeHero, 1);

            // 检查成就
            if (AchievementSystem.HasInstance)
            {
                if (evt.NewLevel >= 10)
                    AchievementSystem.Instance.TryUnlock("hero_lv10");
                if (evt.NewLevel >= 30)
                    AchievementSystem.Instance.TryUnlock("hero_lv30");
            }

            // 增加战令经验
            if (BattlePassSystem.HasInstance)
                BattlePassSystem.Instance.AddExp(10);

            // 触发引导
            TryTriggerGuide(GuideTrigger.FirstUpgradeHero);
        }

        private void OnLevelCompleted(LevelCompletedEvent evt)
        {
            // 这个事件由战斗系统发出，这里做额外的联动
            // 主要的结算逻辑在ProcessBattleResult中

            // 触发引导
            if (evt.Chapter == 1 && evt.Level == 1)
                TryTriggerGuide(GuideTrigger.FirstClearLevel1);
            if (evt.Chapter == 1 && evt.Level == 3)
                TryTriggerGuide(GuideTrigger.FirstClearLevel3);
        }

        private void OnGachaResult(GachaResultEvent evt)
        {
            // 抽卡增加战令经验
            if (BattlePassSystem.HasInstance)
            {
                int expGain = evt.Count * 5; // 每抽5经验
                BattlePassSystem.Instance.AddExp(expGain);
            }
        }

        private void OnShopPurchase(ShopPurchaseEvent evt)
        {
            // 购买增加战令经验
            if (BattlePassSystem.HasInstance)
            {
                BattlePassSystem.Instance.AddExp(20);
            }
        }

        // ========== 战斗结算处理 ==========

        private void ProcessLevelProgress(BattleResultData result)
        {
            if (!PlayerDataManager.HasInstance || !result.IsVictory) return;
            var data = PlayerDataManager.Instance.Data;

            // 更新星级
            bool found = false;
            if (data.LevelStars == null) data.LevelStars = new System.Collections.Generic.List<LevelStarData>();

            for (int i = 0; i < data.LevelStars.Count; i++)
            {
                if (data.LevelStars[i].Chapter == result.Chapter && data.LevelStars[i].Level == result.Level)
                {
                    if (result.Stars > data.LevelStars[i].Stars)
                        data.LevelStars[i].Stars = result.Stars;
                    if (result.ClearTime < data.LevelStars[i].BestTime || data.LevelStars[i].BestTime <= 0)
                        data.LevelStars[i].BestTime = result.ClearTime;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                data.LevelStars.Add(new LevelStarData
                {
                    Chapter = result.Chapter,
                    Level = result.Level,
                    Stars = result.Stars,
                    BestTime = result.ClearTime
                });
            }

            // 解锁下一关
            int nextLevel = result.Level + 1;
            int nextChapter = result.Chapter;
            if (nextLevel > 5) // 每章5关
            {
                nextLevel = 1;
                nextChapter++;
            }

            if (nextChapter > data.UnlockedChapter ||
                (nextChapter == data.UnlockedChapter && nextLevel > data.UnlockedLevel))
            {
                data.UnlockedChapter = nextChapter;
                data.UnlockedLevel = nextLevel;

                if (EventBus.HasInstance)
                {
                    EventBus.Instance.Publish(new LevelUnlockedEvent
                    {
                        Chapter = nextChapter,
                        Level = nextLevel
                    });
                }
            }

            PlayerDataManager.Instance.MarkDirty();
        }

        private void ProcessGoldReward(BattleResultData result)
        {
            if (!PlayerDataManager.HasInstance) return;

            if (!result.IsVictory)
            {
                // 败局只给少量击杀金币（30%），不给基础金币和星级奖励
                int failGold = Mathf.RoundToInt(result.KillCount * 2 * 0.3f);
                if (failGold > 0) PlayerDataManager.Instance.AddGold(failGold);
                Debug.Log($"[MetaGameInitializer] 败局金币: 击杀{failGold}");
                return;
            }

            // 胜利：基础金币 + 击杀奖励 + 星级奖励
            int baseGold = 100 + result.Chapter * 20 + result.Level * 10;
            int killGold = result.KillCount * 2;
            int starBonus = result.Stars * 50;
            int totalGold = baseGold + killGold + starBonus;

            PlayerDataManager.Instance.AddGold(totalGold);

            // 胜利额外奖励钻石
            if (result.Stars >= 3)
            {
                PlayerDataManager.Instance.AddDiamonds(5);
            }

            Debug.Log($"[MetaGameInitializer] 金币奖励: 基础{baseGold} + 击杀{killGold} + 星级{starBonus} = {totalGold}");
        }

        private void ProcessStatistics(BattleResultData result)
        {
            if (!PlayerDataManager.HasInstance) return;
            var data = PlayerDataManager.Instance.Data;

            if (result.IsVictory) data.TotalWins++;
            else data.TotalLosses++;

            data.TotalKills += result.KillCount;

            if (result.HighestDPS > data.HighestDPS)
                data.HighestDPS = result.HighestDPS;

            PlayerDataManager.Instance.MarkDirty();

            // 上报排行榜
            if (SocialSystem.HasInstance)
            {
                int score = data.TotalWins * 100 + data.Level * 10;
                SocialSystem.Instance.ReportScore(score);
            }
        }

        private void ProcessDailyQuests(BattleResultData result)
        {
            if (!DailyQuestSystem.HasInstance) return;

            // 通关次数
            if (result.IsVictory)
                DailyQuestSystem.Instance.UpdateProgress(QuestType.ClearLevel, 1);

            // 击杀数
            DailyQuestSystem.Instance.UpdateProgress(QuestType.KillEnemy, result.KillCount);

            // 建塔数
            DailyQuestSystem.Instance.UpdateProgress(QuestType.BuildTower, result.TowerBuiltCount);
        }

        private void ProcessBattlePassExp(BattleResultData result)
        {
            if (!BattlePassSystem.HasInstance) return;

            // 战斗经验 = 基础 + 星级加成
            int baseExp = 20;
            int starExp = result.Stars * 5;
            int totalExp = baseExp + starExp;

            BattlePassSystem.Instance.AddExp(totalExp);
        }

        private void ProcessAchievements(BattleResultData result)
        {
            if (!AchievementSystem.HasInstance || !PlayerDataManager.HasInstance) return;
            var data = PlayerDataManager.Instance.Data;

            // 通关成就
            if (result.IsVictory)
            {
                if (data.TotalWins >= 1) AchievementSystem.Instance.TryUnlock("first_clear");
                if (data.TotalWins >= 10) AchievementSystem.Instance.TryUnlock("clear_10");
                if (data.TotalWins >= 50) AchievementSystem.Instance.TryUnlock("clear_50");
            }

            // 击杀成就
            if (data.TotalKills >= 1000) AchievementSystem.Instance.TryUnlock("kill_1000");
            if (data.TotalKills >= 10000) AchievementSystem.Instance.TryUnlock("kill_10000");

            // 建塔成就（累计）
            // 需要额外的累计建塔统计字段，这里简化处理
            if (result.TowerBuiltCount >= 100) AchievementSystem.Instance.TryUnlock("build_100");
        }

        private void ProcessPlayerExp(BattleResultData result)
        {
            if (!PlayerDataManager.HasInstance) return;
            var data = PlayerDataManager.Instance.Data;

            // 玩家经验 = 基础 + 通关加成
            long expGain = 50 + (result.IsVictory ? 100 : 0) + result.Stars * 20;
            data.Experience += expGain;

            // 升级检查
            long expNeeded = GetExpForLevel(data.Level);
            while (data.Experience >= expNeeded && data.Level < 100)
            {
                data.Experience -= expNeeded;
                data.Level++;
                expNeeded = GetExpForLevel(data.Level);

                Debug.Log($"[MetaGameInitializer] 玩家升级! Lv.{data.Level}");
            }

            PlayerDataManager.Instance.MarkDirty();
        }

        private long GetExpForLevel(int level)
        {
            // 经验曲线：100 + level * 50
            return 100 + level * 50;
        }

        // ========== 新玩家初始化 ==========

        private void InitializeNewPlayer()
        {
            if (!PlayerDataManager.HasInstance) return;
            var data = PlayerDataManager.Instance.Data;

            // 检查是否是新玩家（没有英雄）
            if (data.Heroes == null || data.Heroes.Count == 0)
            {
                Debug.Log("[MetaGameInitializer] 检测到新玩家，执行初始化...");

                // 赠送初始英雄
                if (HeroSystem.HasInstance)
                {
                    HeroSystem.Instance.UnlockHero("hero_chosen_one");
                    data.ActiveHeroId = "hero_chosen_one";
                }

                // 赠送初始资源
                data.Gold = 500;
                data.Diamonds = 100;
                data.Stamina = 60;
                data.MaxStamina = 60;

                PlayerDataManager.Instance.MarkDirty();
                PlayerDataManager.Instance.Save();

                // 发送欢迎邮件
                if (MailSystem.HasInstance)
                {
                    MailSystem.Instance.SendSystemMail(
                        "🎉 欢迎新指挥官！",
                        "欢迎加入以太守护者！\n\n" +
                        "这里有一份新手礼包送给你：\n" +
                        "💎 100钻石\n" +
                        "🪙 500金币\n" +
                        "🦸 初始英雄「天选之人」\n\n" +
                        "快去开始你的第一场战斗吧！",
                        "diamonds", 50);
                }

                Debug.Log("[MetaGameInitializer] 新玩家初始化完成");
            }
        }
    }

    // ========== 战斗结算数据 ==========

    /// <summary>
    /// 战斗结算数据（由战斗系统填充，传递给元游戏系统）
    /// </summary>
    [Serializable]
    public class BattleResultData
    {
        /// <summary>章节号</summary>
        public int Chapter;

        /// <summary>关卡号</summary>
        public int Level;

        /// <summary>是否胜利</summary>
        public bool IsVictory;

        /// <summary>获得星级（0-3）</summary>
        public int Stars;

        /// <summary>通关时间（秒）</summary>
        public float ClearTime;

        /// <summary>击杀怪物数</summary>
        public int KillCount;

        /// <summary>建造塔数量</summary>
        public int TowerBuiltCount;

        /// <summary>最高DPS</summary>
        public float HighestDPS;

        /// <summary>难度（0=普通 1=困难 2=噩梦）</summary>
        public int Difficulty;

        /// <summary>使用的英雄ID</summary>
        public string HeroId;
    }
}
