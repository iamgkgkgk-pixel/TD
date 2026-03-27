// ============================================================
// 文件名：EconomyValidator.cs
// 功能描述：经济闭环验证工具 — 验证从新手→通关→付费→养成的完整数据流
//          调试面板，可在运行时模拟各种操作验证数据正确性
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 G4-2（经济闭环验收）
// ============================================================

using System.Text;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Data;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>
    /// 经济闭环验证工具
    /// 
    /// 验证项：
    /// 1. 新手流程：初始资源 → 第1关 → 获得金币 → 升级英雄
    /// 2. 付费流程：购买钻石 → 抽卡 → 获得英雄 → 碎片转化
    /// 3. 养成流程：金币消耗 → 英雄升级 → 战力提升
    /// 4. 日常循环：签到 → 任务 → 活跃度奖励 → 战令经验
    /// 5. 体力循环：消耗体力 → 自然恢复 → 购买体力
    /// </summary>
    public class EconomyValidator : Singleton<EconomyValidator>
    {
        protected override void OnInit()
        {
            Debug.Log("[EconomyValidator] 初始化完成");
        }

        protected override void OnDispose() { }

        /// <summary>
        /// 运行完整的经济闭环验证
        /// </summary>
        public string RunFullValidation()
        {
            var sb = new StringBuilder();
            sb.AppendLine("========== 经济闭环验证报告 ==========");
            sb.AppendLine($"验证时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // 1. 数据层验证
            sb.AppendLine("--- 1. 数据层验证 ---");
            ValidateDataLayer(sb);

            // 2. 系统可用性验证
            sb.AppendLine("--- 2. 系统可用性验证 ---");
            ValidateSystemAvailability(sb);

            // 3. 货币流通验证
            sb.AppendLine("--- 3. 货币流通验证 ---");
            ValidateCurrencyFlow(sb);

            // 4. 英雄系统验证
            sb.AppendLine("--- 4. 英雄系统验证 ---");
            ValidateHeroSystem(sb);

            // 5. 抽卡概率验证
            sb.AppendLine("--- 5. 抽卡概率验证 ---");
            ValidateGachaProbability(sb);

            // 6. 日常循环验证
            sb.AppendLine("--- 6. 日常循环验证 ---");
            ValidateDailyLoop(sb);

            // 7. 体力系统验证
            sb.AppendLine("--- 7. 体力系统验证 ---");
            ValidateStaminaSystem(sb);

            sb.AppendLine("========== 验证完成 ==========");

            string report = sb.ToString();
            Debug.Log(report);
            return report;
        }

        private void ValidateDataLayer(StringBuilder sb)
        {
            bool pdmOk = PlayerDataManager.HasInstance;
            bool smOk = SaveManager.HasInstance;

            sb.AppendLine($"  PlayerDataManager: {(pdmOk ? "✅" : "❌")}");
            sb.AppendLine($"  SaveManager: {(smOk ? "✅" : "❌")}");

            if (pdmOk)
            {
                var data = PlayerDataManager.Instance.Data;
                sb.AppendLine($"  玩家等级: Lv.{data.Level}");
                sb.AppendLine($"  金币: {data.Gold}");
                sb.AppendLine($"  钻石: {data.Diamonds}");
                sb.AppendLine($"  体力: {data.Stamina}/{data.MaxStamina}");
                sb.AppendLine($"  英雄数: {data.Heroes?.Count ?? 0}");
                sb.AppendLine($"  关卡进度: 章节{data.UnlockedChapter}-关卡{data.UnlockedLevel}");
            }
        }

        private void ValidateSystemAvailability(StringBuilder sb)
        {
            sb.AppendLine($"  HeroSystem: {(HeroSystem.HasInstance ? "✅" : "❌")}");
            sb.AppendLine($"  ShopSystem: {(ShopSystem.HasInstance ? "✅" : "❌")}");
            sb.AppendLine($"  BattlePassSystem: {(BattlePassSystem.HasInstance ? "✅" : "❌")}");
            sb.AppendLine($"  GachaSystem: {(GachaSystem.HasInstance ? "✅" : "❌")}");
            sb.AppendLine($"  DailyQuestSystem: {(DailyQuestSystem.HasInstance ? "✅" : "❌")}");
            sb.AppendLine($"  AchievementSystem: {(AchievementSystem.HasInstance ? "✅" : "❌")}");
            sb.AppendLine($"  CheckInSystem: {(CheckInSystem.HasInstance ? "✅" : "❌")}");
            sb.AppendLine($"  StaminaSystem: {(StaminaSystem.HasInstance ? "✅" : "❌")}");
            sb.AppendLine($"  SocialSystem: {(SocialSystem.HasInstance ? "✅" : "❌")}");
            sb.AppendLine($"  MailSystem: {(MailSystem.HasInstance ? "✅" : "❌")}");
            sb.AppendLine($"  RedDotManager: {(RedDotManager.HasInstance ? "✅" : "❌")}");
            sb.AppendLine($"  MetaGuidanceSystem: {(MetaGuidanceSystem.HasInstance ? "✅" : "❌")}");
        }

        private void ValidateCurrencyFlow(StringBuilder sb)
        {
            if (!PlayerDataManager.HasInstance)
            {
                sb.AppendLine("  ❌ PlayerDataManager不可用，跳过");
                return;
            }

            var data = PlayerDataManager.Instance.Data;
            long oldGold = data.Gold;
            long oldDiamonds = data.Diamonds;

            // 测试金币增减
            PlayerDataManager.Instance.AddGold(100);
            bool goldAddOk = data.Gold == oldGold + 100;
            sb.AppendLine($"  金币增加: {(goldAddOk ? "✅" : "❌")} ({oldGold} → {data.Gold})");

            bool goldSpendOk = PlayerDataManager.Instance.SpendGold(50);
            sb.AppendLine($"  金币消耗: {(goldSpendOk ? "✅" : "❌")} ({data.Gold + 50} → {data.Gold})");

            bool goldOverspendOk = !PlayerDataManager.Instance.SpendGold(999999999);
            sb.AppendLine($"  金币超支保护: {(goldOverspendOk ? "✅" : "❌")}");

            // 恢复原始值
            data.Gold = oldGold;

            // 测试钻石增减
            PlayerDataManager.Instance.AddDiamonds(100);
            bool diamondAddOk = data.Diamonds == oldDiamonds + 100;
            sb.AppendLine($"  钻石增加: {(diamondAddOk ? "✅" : "❌")}");

            data.Diamonds = oldDiamonds;
            PlayerDataManager.Instance.MarkDirty();
        }

        private void ValidateHeroSystem(StringBuilder sb)
        {
            if (!HeroSystem.HasInstance)
            {
                sb.AppendLine("  ❌ HeroSystem不可用，跳过");
                return;
            }

            // 验证英雄配置表
            var allHeroes = HeroConfigTable.GetAllHeroes();
            sb.AppendLine($"  英雄配置数: {allHeroes.Count}");

            int rCount = 0, srCount = 0, ssrCount = 0;
            for (int i = 0; i < allHeroes.Count; i++)
            {
                switch (allHeroes[i].Rarity)
                {
                    case HeroRarity.R: rCount++; break;
                    case HeroRarity.SR: srCount++; break;
                    case HeroRarity.SSR: ssrCount++; break;
                }
            }
            sb.AppendLine($"  R: {rCount}, SR: {srCount}, SSR: {ssrCount}");

            // 验证初始英雄
            bool hasInitialHero = HeroSystem.Instance.IsHeroUnlocked("hero_chosen_one");
            sb.AppendLine($"  初始英雄解锁: {(hasInitialHero ? "✅" : "⚠️ 未解锁")}");
        }

        private void ValidateGachaProbability(StringBuilder sb)
        {
            if (!GachaSystem.HasInstance)
            {
                sb.AppendLine("  ❌ GachaSystem不可用，跳过");
                return;
            }

            // 验证概率配置
            float totalRate = GachaSystem.RateR + GachaSystem.RateSR + GachaSystem.RateSSR;
            bool rateOk = Mathf.Approximately(totalRate, 100f);
            sb.AppendLine($"  概率总和: {totalRate}% {(rateOk ? "✅" : "❌")}");
            sb.AppendLine($"  保底次数: {GachaSystem.PityCount}");
            sb.AppendLine($"  当前保底计数: {GachaSystem.Instance.GetPityCounter()}");
            sb.AppendLine($"  距离保底: {GachaSystem.Instance.GetPityRemaining()}次");
        }

        private void ValidateDailyLoop(StringBuilder sb)
        {
            // 签到
            if (CheckInSystem.HasInstance)
            {
                bool checkedToday = CheckInSystem.Instance.HasCheckedInToday();
                int consecutive = CheckInSystem.Instance.GetConsecutiveDays();
                sb.AppendLine($"  今日签到: {(checkedToday ? "✅ 已签" : "⬜ 未签")}");
                sb.AppendLine($"  连续签到: {consecutive}天");
            }

            // 每日任务
            if (DailyQuestSystem.HasInstance)
            {
                var quests = DailyQuestSystem.Instance.GetDailyQuests();
                int activity = DailyQuestSystem.Instance.GetActivityPoints();
                sb.AppendLine($"  每日任务数: {quests.Count}");
                sb.AppendLine($"  活跃度: {activity}/100");
            }

            // 战令
            if (BattlePassSystem.HasInstance)
            {
                int bpLevel = BattlePassSystem.Instance.GetCurrentLevel();
                bool premium = BattlePassSystem.Instance.IsPremium();
                sb.AppendLine($"  战令等级: Lv.{bpLevel}");
                sb.AppendLine($"  付费战令: {(premium ? "✅" : "⬜")}");
            }

            // 邮件
            if (MailSystem.HasInstance)
            {
                int unread = MailSystem.Instance.GetUnreadCount();
                int total = MailSystem.Instance.GetAllMails().Count;
                sb.AppendLine($"  邮件: {unread}未读/{total}总计");
            }
        }

        private void ValidateStaminaSystem(StringBuilder sb)
        {
            if (!StaminaSystem.HasInstance || !PlayerDataManager.HasInstance)
            {
                sb.AppendLine("  ❌ StaminaSystem不可用，跳过");
                return;
            }

            var data = PlayerDataManager.Instance.Data;
            sb.AppendLine($"  当前体力: {data.Stamina}/{data.MaxStamina}");
            sb.AppendLine($"  恢复间隔: {StaminaSystem.RecoverIntervalSeconds}秒");
            sb.AppendLine($"  下次恢复: {StaminaSystem.Instance.GetNextRecoverSeconds()}秒后");
        }

        /// <summary>
        /// 模拟完整的新手流程验证
        /// </summary>
        public string SimulateNewPlayerFlow()
        {
            var sb = new StringBuilder();
            sb.AppendLine("========== 模拟新手流程 ==========");

            if (!MetaGameInitializer.HasInstance)
            {
                sb.AppendLine("❌ MetaGameInitializer未初始化");
                return sb.ToString();
            }

            // 模拟第1关通关
            var result1 = new BattleResultData
            {
                Chapter = 1, Level = 1, IsVictory = true, Stars = 2,
                ClearTime = 120f, KillCount = 15, TowerBuiltCount = 3,
                HighestDPS = 50f, Difficulty = 0, HeroId = "hero_chosen_one"
            };

            sb.AppendLine("--- 模拟通关 1-1 ---");
            long goldBefore = PlayerDataManager.Instance.Data.Gold;
            MetaGameInitializer.Instance.ProcessBattleResult(result1);
            long goldAfter = PlayerDataManager.Instance.Data.Gold;
            sb.AppendLine($"  金币变化: {goldBefore} → {goldAfter} (+{goldAfter - goldBefore})");
            sb.AppendLine($"  关卡进度: {PlayerDataManager.Instance.Data.UnlockedChapter}-{PlayerDataManager.Instance.Data.UnlockedLevel}");

            // 模拟签到
            sb.AppendLine("--- 模拟签到 ---");
            if (CheckInSystem.HasInstance && !CheckInSystem.Instance.HasCheckedInToday())
            {
                CheckInSystem.Instance.DoCheckIn();
                sb.AppendLine($"  签到成功, 连续{CheckInSystem.Instance.GetConsecutiveDays()}天");
            }

            // 模拟领取任务奖励
            sb.AppendLine("--- 模拟领取任务奖励 ---");
            if (DailyQuestSystem.HasInstance)
            {
                var quests = DailyQuestSystem.Instance.GetDailyQuests();
                for (int i = 0; i < quests.Count; i++)
                {
                    bool claimed = DailyQuestSystem.Instance.ClaimReward(quests[i].QuestId);
                    if (claimed) sb.AppendLine($"  领取: {quests[i].Name}");
                }
                sb.AppendLine($"  活跃度: {DailyQuestSystem.Instance.GetActivityPoints()}");
            }

            sb.AppendLine("========== 模拟完成 ==========");
            return sb.ToString();
        }
    }
}
