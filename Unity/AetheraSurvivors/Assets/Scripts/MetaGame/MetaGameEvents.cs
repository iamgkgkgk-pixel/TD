// ============================================================
// 文件名：MetaGameEvents.cs
// 功能描述：元游戏系统事件定义 — 所有外围系统间通信的事件
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #247-380
// ============================================================

using AetheraSurvivors.Framework;

namespace AetheraSurvivors.MetaGame
{
    // ========== 货币事件 ==========

    /// <summary>货币变化事件</summary>
    public struct CurrencyChangedEvent : IEvent
    {
        public string CurrencyType; // "gold", "diamonds", "stamina", "summon_ticket"
        public long OldAmount;
        public long NewAmount;
        public long Delta;
        public string Source; // 变化来源描述
    }

    // ========== 关卡事件 ==========

    /// <summary>关卡解锁事件</summary>
    public struct LevelUnlockedEvent : IEvent
    {
        public int Chapter;
        public int Level;
    }

    /// <summary>关卡通关事件</summary>
    public struct LevelCompletedEvent : IEvent
    {
        public int Chapter;
        public int Level;
        public int Stars;
        public float ClearTime;
        public int Difficulty; // 0=普通 1=困难 2=噩梦
    }

    // ========== 英雄事件 ==========

    /// <summary>英雄解锁事件</summary>
    public struct HeroUnlockedEvent : IEvent
    {
        public string HeroId;
    }

    /// <summary>英雄升级事件</summary>
    public struct HeroLevelUpEvent : IEvent
    {
        public string HeroId;
        public int OldLevel;
        public int NewLevel;
    }

    /// <summary>英雄升星事件</summary>
    public struct HeroStarUpEvent : IEvent
    {
        public string HeroId;
        public int OldStar;
        public int NewStar;
    }

    /// <summary>英雄出战变更事件</summary>
    public struct HeroActiveChangedEvent : IEvent
    {
        public string OldHeroId;
        public string NewHeroId;
    }

    // ========== 塔事件 ==========

    /// <summary>塔永久升级事件</summary>
    public struct TowerPermanentUpgradeEvent : IEvent
    {
        public string TowerId;
        public string UpgradeType; // "atk", "atkSpeed", "range"
        public int NewLevel;
    }

    // ========== 商城/支付事件 ==========

    /// <summary>商品购买事件</summary>
    public struct ShopPurchaseEvent : IEvent
    {
        public string ProductId;
        public string ProductName;
        public int Price;
        public string CurrencyType;
    }

    /// <summary>微信支付结果事件</summary>
    public struct WXPayResultEvent : IEvent
    {
        public string OrderId;
        public bool Success;
        public string ErrorMsg;
    }

    // ========== 战令事件 ==========

    /// <summary>战令经验获取事件</summary>
    public struct BattlePassExpGainEvent : IEvent
    {
        public long ExpGained;
        public int OldLevel;
        public int NewLevel;
    }

    /// <summary>战令奖励领取事件</summary>
    public struct BattlePassRewardClaimedEvent : IEvent
    {
        public int Level;
        public bool IsPremium;
    }

    // ========== 任务/成就事件 ==========

    /// <summary>任务进度更新事件</summary>
    public struct QuestProgressEvent : IEvent
    {
        public string QuestId;
        public int Progress;
        public int Target;
        public bool Completed;
    }

    /// <summary>任务奖励领取事件</summary>
    public struct QuestRewardClaimedEvent : IEvent
    {
        public string QuestId;
    }

    /// <summary>成就解锁事件</summary>
    public struct AchievementUnlockedEvent : IEvent
    {
        public string AchievementId;
        public string AchievementName;
    }

    // ========== 签到事件 ==========

    /// <summary>签到事件</summary>
    public struct CheckInEvent : IEvent
    {
        public int Day;
        public int ConsecutiveDays;
    }

    // ========== 抽卡事件 ==========

    /// <summary>抽卡结果事件</summary>
    public struct GachaResultEvent : IEvent
    {
        public int Count; // 1=单抽 10=十连
        public int SSRCount;
        public int SRCount;
        public int RCount;
        public bool HitPity; // 是否触发保底
    }

    // ========== 社交事件 ==========

    /// <summary>分享完成事件</summary>
    public struct ShareCompletedEvent : IEvent
    {
        public string ShareType;
        public bool Success;
    }

    // ========== 邮件事件 ==========

    /// <summary>新邮件事件</summary>
    public struct NewMailEvent : IEvent
    {
        public int UnreadCount;
    }

    /// <summary>邮件附件领取事件</summary>
    public struct MailAttachmentClaimedEvent : IEvent
    {
        public string MailId;
    }

    // ========== 红点事件 ==========

    /// <summary>红点状态变化事件</summary>
    public struct RedDotChangedEvent : IEvent
    {
        public string NodeId;
        public bool HasRedDot;
        public int Count;
    }

    // ========== 引导事件 ==========

    /// <summary>引导步骤完成事件</summary>
    public struct TutorialStepCompletedEvent : IEvent
    {
        public int StepIndex;
        public string StepId;
    }

    // ========== 体力事件 ==========

    /// <summary>体力变化事件</summary>
    public struct StaminaChangedEvent : IEvent
    {
        public int OldStamina;
        public int NewStamina;
        public int MaxStamina;
    }
}
