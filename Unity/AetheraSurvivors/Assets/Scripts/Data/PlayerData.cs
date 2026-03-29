
// ============================================================
// 文件名：PlayerData.cs
// 功能描述：玩家数据模型 — 包含所有需要持久化的数据字段
//          使用SaveManager进行序列化/反序列化
// 创建时间：2026-03-25
// 所属模块：Data
// 对应交互：阶段二 #56
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetheraSurvivors.Data
{
    /// <summary>
    /// 玩家核心数据 — 序列化到存档
    /// 所有需要持久化的数据都在此类中定义
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        // ========== 基础信息 ==========

        /// <summary>玩家唯一ID（微信openid或自生成）</summary>
        public string PlayerId;

        /// <summary>昵称</summary>
        public string Nickname;

        /// <summary>头像URL</summary>
        public string AvatarUrl;

        /// <summary>玩家等级</summary>
        public int Level;

        /// <summary>玩家经验值</summary>
        public long Experience;

        /// <summary>账号创建时间（Unix时间戳）</summary>
        public long CreateTime;

        /// <summary>最后登录时间</summary>
        public long LastLoginTime;

        /// <summary>总游戏时长（秒）</summary>
        public long TotalPlayTime;

        // ========== 货币 ==========

        /// <summary>钻石（付费货币）</summary>
        public long Diamonds;

        /// <summary>金币（通用货币）</summary>
        public long Gold;

        /// <summary>体力</summary>
        public int Stamina;

        /// <summary>最大体力</summary>
        public int MaxStamina;

        /// <summary>体力恢复时间（Unix时间戳）</summary>
        public long StaminaRecoverTime;

        /// <summary>召唤券数量</summary>
        public int SummonTickets;

        /// <summary>上次月卡/周卡奖励领取日期（yyyyMMdd格式）</summary>
        public string LastCardClaimDate;

        // ========== 关卡进度 ==========

        /// <summary>已解锁的最大章节</summary>
        public int UnlockedChapter;

        /// <summary>已解锁的最大关卡</summary>
        public int UnlockedLevel;

        /// <summary>关卡星级数据（格式：chapter_level → stars）</summary>
        public List<LevelStarData> LevelStars;

        // ========== 英雄 ==========

        /// <summary>已解锁的英雄列表</summary>
        public List<HeroSaveData> Heroes;

        /// <summary>当前出战英雄ID</summary>
        public string ActiveHeroId;

        // ========== 塔 ==========

        /// <summary>塔的局外永久升级数据</summary>
        public List<TowerUpgradeData> TowerUpgrades;

        // ========== 系统数据 ==========

        /// <summary>新手引导进度</summary>
        public int TutorialStep;

        /// <summary>是否完成新手引导</summary>
        public bool TutorialCompleted;

        /// <summary>签到数据</summary>
        public CheckInSaveData CheckInData;

        /// <summary>每日任务数据</summary>
        public DailyQuestSaveData DailyQuestData;

        /// <summary>战令数据</summary>
        public BattlePassSaveData BattlePassData;

        // ========== 设置 ==========

        /// <summary>BGM音量（0-1）</summary>
        public float BgmVolume;

        /// <summary>SFX音量（0-1）</summary>
        public float SfxVolume;

        /// <summary>BGM是否静音</summary>
        public bool BgmMuted;

        /// <summary>SFX是否静音</summary>
        public bool SfxMuted;

        /// <summary>语言设置</summary>
        public string Language;

        // ========== 统计 ==========

        /// <summary>总通关次数</summary>
        public int TotalWins;

        /// <summary>总失败次数</summary>
        public int TotalLosses;

        /// <summary>总击杀怪物数</summary>
        public long TotalKills;

        /// <summary>最高单局DPS</summary>
        public float HighestDPS;

        // ========== 构造函数 ==========

        /// <summary>
        /// 创建默认的新玩家数据
        /// </summary>
        public static PlayerData CreateDefault()
        {
            return new PlayerData
            {
                PlayerId = string.Empty,
                Nickname = "指挥官",
                AvatarUrl = string.Empty,
                Level = 1,
                Experience = 0,
                CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                LastLoginTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                TotalPlayTime = 0,

                Diamonds = 0,
                Gold = 500,       // 新手初始金币
                Stamina = 99999,  // 测试模式：无限体力
                MaxStamina = 99999,
                StaminaRecoverTime = 0,
                SummonTickets = 0,
                LastCardClaimDate = "",

                UnlockedChapter = 1,
                UnlockedLevel = 1,
                LevelStars = new List<LevelStarData>(),

                Heroes = new List<HeroSaveData>(),
                ActiveHeroId = string.Empty,

                TowerUpgrades = new List<TowerUpgradeData>(),

                TutorialStep = 0,
                TutorialCompleted = false,
                CheckInData = new CheckInSaveData(),
                DailyQuestData = new DailyQuestSaveData(),
                BattlePassData = new BattlePassSaveData(),

                BgmVolume = 1f,
                SfxVolume = 1f,
                BgmMuted = false,
                SfxMuted = false,
                Language = "zh-CN",

                TotalWins = 0,
                TotalLosses = 0,
                TotalKills = 0,
                HighestDPS = 0f
            };
        }
    }

    // ========== 子数据结构 ==========

    /// <summary>
    /// 关卡星级数据
    /// </summary>
    [Serializable]
    public class LevelStarData
    {
        /// <summary>章节号</summary>
        public int Chapter;

        /// <summary>关卡号</summary>
        public int Level;

        /// <summary>获得的星数（0-3）</summary>
        public int Stars;

        /// <summary>最佳通关时间（秒）</summary>
        public float BestTime;
    }

    /// <summary>
    /// 英雄存档数据
    /// </summary>
    [Serializable]
    public class HeroSaveData
    {
        /// <summary>英雄ID</summary>
        public string HeroId;

        /// <summary>英雄等级</summary>
        public int Level;

        /// <summary>星级</summary>
        public int Star;

        /// <summary>经验值</summary>
        public long Exp;

        /// <summary>碎片数量</summary>
        public int Fragments;
    }

    /// <summary>
    /// 塔升级数据
    /// </summary>
    [Serializable]
    public class TowerUpgradeData
    {
        /// <summary>塔类型ID</summary>
        public string TowerId;

        /// <summary>攻击力永久升级等级</summary>
        public int AtkUpgradeLevel;

        /// <summary>攻速永久升级等级</summary>
        public int AtkSpeedUpgradeLevel;

        /// <summary>射程永久升级等级</summary>
        public int RangeUpgradeLevel;
    }

    /// <summary>
    /// 签到存档数据
    /// </summary>
    [Serializable]
    public class CheckInSaveData
    {
        /// <summary>连续签到天数</summary>
        public int ConsecutiveDays;

        /// <summary>上次签到日期（yyyyMMdd格式）</summary>
        public int LastCheckInDate;

        /// <summary>本月签到记录（位掩码，每bit表示一天）</summary>
        public int MonthlyBitmask;
    }

    /// <summary>
    /// 每日任务存档数据
    /// </summary>
    [Serializable]
    public class DailyQuestSaveData
    {
        /// <summary>上次刷新日期（yyyyMMdd格式）</summary>
        public int LastRefreshDate;

        /// <summary>各任务进度</summary>
        public List<QuestProgress> Quests;

        /// <summary>活跃度</summary>
        public int ActivityPoints;

        /// <summary>已领取的活跃度奖励档位</summary>
        public List<int> ClaimedActivityRewards;
    }

    /// <summary>
    /// 任务进度
    /// </summary>
    [Serializable]
    public class QuestProgress
    {
        /// <summary>任务ID</summary>
        public string QuestId;

        /// <summary>当前进度</summary>
        public int Progress;

        /// <summary>是否已完成</summary>
        public bool Completed;

        /// <summary>是否已领奖</summary>
        public bool Claimed;
    }

    /// <summary>
    /// 战令存档数据
    /// </summary>
    [Serializable]
    public class BattlePassSaveData
    {
        /// <summary>赛季ID</summary>
        public int SeasonId;

        /// <summary>当前等级</summary>
        public int Level;

        /// <summary>当前经验</summary>
        public long Exp;

        /// <summary>是否购买了付费战令</summary>
        public bool IsPremium;

        /// <summary>免费轨已领取的等级列表</summary>
        public List<int> ClaimedFree;

        /// <summary>付费轨已领取的等级列表</summary>
        public List<int> ClaimedPremium;

        /// <summary>赛季开始时间（Unix时间戳）</summary>
        public long SeasonStartTimestamp;
    }

    // ====================================================================

    /// <summary>
    /// 玩家数据管理器 — 运行时玩家数据的访问入口
    /// 提供便捷的读写方法，自动触发存档
    /// </summary>
    public class PlayerDataManager : AetheraSurvivors.Framework.Singleton<PlayerDataManager>
    {
        // ========== 常量 ==========

        /// <summary>存档Key</summary>
        private const string SaveKey = "player_data";

        // ========== 私有字段 ==========

        /// <summary>玩家数据实例</summary>
        private PlayerData _data;

        /// <summary>数据是否被修改（脏标记）</summary>
        private bool _isDirty;

        // ========== 公共属性 ==========

        /// <summary>玩家数据（只读访问，修改请用专用方法）</summary>
        public PlayerData Data => _data;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            // 从存档加载
            _data = AetheraSurvivors.Framework.SaveManager.Instance.Load<PlayerData>(SaveKey);

            if (_data == null)
            {
                // 新玩家，创建默认数据
                _data = PlayerData.CreateDefault();
                Save();
                Debug.Log("[PlayerDataManager] 创建新玩家数据");
            }
            else
            {
                Debug.Log($"[PlayerDataManager] 加载玩家数据: Lv.{_data.Level}, 金币:{_data.Gold}");
            }
        }

        protected override void OnDispose()
        {
            Save();
        }

        // ========== 公共方法：货币操作 ==========

        /// <summary>
        /// 增加金币
        /// </summary>
        public void AddGold(long amount)
        {
            if (amount <= 0) return;
            _data.Gold += amount;
            MarkDirty();
        }

        /// <summary>
        /// 消耗金币（返回是否足够）
        /// </summary>
        public bool SpendGold(long amount)
        {
            if (amount <= 0 || _data.Gold < amount) return false;
            _data.Gold -= amount;
            MarkDirty();
            return true;
        }

        /// <summary>
        /// 增加钻石
        /// </summary>
        public void AddDiamonds(long amount)
        {
            if (amount <= 0) return;
            _data.Diamonds += amount;
            MarkDirty();
        }

        /// <summary>
        /// 消耗钻石（返回是否足够）
        /// </summary>
        public bool SpendDiamonds(long amount)
        {
            if (amount <= 0 || _data.Diamonds < amount) return false;
            _data.Diamonds -= amount;
            MarkDirty();
            return true;
        }

        // ========== 公共方法：存档 ==========

        /// <summary>
        /// 保存到存档
        /// </summary>
        public void Save()
        {
            if (AetheraSurvivors.Framework.SaveManager.HasInstance)
            {
                AetheraSurvivors.Framework.SaveManager.Instance.Save(SaveKey, _data);
            }
            _isDirty = false;
        }

        /// <summary>
        /// 标记数据已修改（延迟保存）
        /// </summary>
        public void MarkDirty()
        {
            _isDirty = true;
        }

        /// <summary>
        /// 如果数据有修改，执行保存
        /// </summary>
        public void SaveIfDirty()
        {
            if (_isDirty)
            {
                Save();
            }
        }
    }
}
