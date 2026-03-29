// ============================================================
// 文件名：SocialSystem.cs
// 功能描述：社交系统 — 排行榜、分享、好友互助、邀请奖励
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #263-265
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Data;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>排行榜条目</summary>
    [Serializable]
    public class RankEntry
    {
        public string PlayerId;
        public string Nickname;
        public string AvatarUrl;
        public int Score;
        public int Rank;
    }

    /// <summary>
    /// 社交系统管理器
    /// 
    /// 功能：
    /// 1. 微信排行榜（好友排行）
    /// 2. 分享系统（自定义分享卡片+防刷）
    /// 3. 好友互助（送体力）
    /// 4. 邀请奖励
    /// </summary>
    public class SocialSystem : Singleton<SocialSystem>
    {
        // ========== 分享防刷 ==========
        private int _dailyShareCount;
        private int _lastShareDate;
        private const int MaxDailyShares = 5; // 每日最多领取5次分享奖励

        // ========== 好友体力防刷 ==========
        private int _dailyFriendStaminaClaims;
        private int _lastFriendStaminaDate;
        private const int MaxDailyFriendStamina = 10; // 每日最多领取10次好友体力

        // ========== 排行榜缓存 ==========
        private List<RankEntry> _friendRankCache;
        private long _rankCacheTime;
        private const long RankCacheExpiry = 300; // 5分钟缓存

        protected override void OnInit()
        {
            LoadShareState();
            Debug.Log("[SocialSystem] 初始化完成");
        }

        protected override void OnDispose()
        {
            SaveShareState();
        }

        // ========== 排行榜 ==========

        /// <summary>获取好友排行榜</summary>
        public List<RankEntry> GetFriendRank()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (_friendRankCache != null && now - _rankCacheTime < RankCacheExpiry)
            {
                return _friendRankCache;
            }

            // 模拟排行榜数据（实际应从微信开放数据域获取）
            _friendRankCache = GenerateMockRank();
            _rankCacheTime = now;
            return _friendRankCache;
        }

        /// <summary>上报分数到排行榜</summary>
        public void ReportScore(int score)
        {
            Debug.Log($"[Social] 上报排行榜分数: {score}");
            // TODO: 调用微信开放数据域API上报
        }

        // ========== 分享系统 ==========

        /// <summary>分享游戏</summary>
        public bool ShareGame(string shareType = "normal")
        {
            Debug.Log($"[Social] 分享游戏: type={shareType}");

            // TODO: 调用微信分享API
            // wx.shareAppMessage({ title, imageUrl, query })

            // 模拟分享成功
            bool success = true;

            if (success)
            {
                // 检查是否可以领取分享奖励（防刷）
                CheckShareDateReset();

                if (_dailyShareCount < MaxDailyShares)
                {
                    _dailyShareCount++;
                    SaveShareState();

                    // 发放分享奖励
                    if (PlayerDataManager.HasInstance)
                    {
                        PlayerDataManager.Instance.AddDiamonds(10);
                    }

                    // 更新每日任务
                    if (DailyQuestSystem.HasInstance)
                    {
                        DailyQuestSystem.Instance.UpdateProgress(QuestType.ShareGame, 1);
                    }

                    Debug.Log($"[Social] 分享奖励已发放 ({_dailyShareCount}/{MaxDailyShares})");
                }
                else
                {
                    Debug.Log("[Social] 今日分享奖励已达上限");
                }

                if (EventBus.HasInstance)
                {
                    EventBus.Instance.Publish(new ShareCompletedEvent
                    {
                        ShareType = shareType,
                        Success = true
                    });
                }
            }

            return success;
        }

        /// <summary>获取今日剩余分享奖励次数</summary>
        public int GetRemainingShareRewards()
        {
            CheckShareDateReset();
            return Mathf.Max(0, MaxDailyShares - _dailyShareCount);
        }

        // ========== 好友互助 ==========

        /// <summary>送好友体力</summary>
        public bool SendStaminaToFriend(string friendId)
        {
            Debug.Log($"[Social] 送体力给好友: {friendId}");
            // TODO: 通过服务端发送
            return true;
        }

        /// <summary>领取好友送的体力（每日最多10次）</summary>
        public bool ClaimFriendStamina()
        {
            // 防刷：检查每日次数
            CheckFriendStaminaDateReset();
            if (_dailyFriendStaminaClaims >= MaxDailyFriendStamina)
            {
                Debug.Log("[Social] 今日好友体力领取已达上限");
                return false;
            }

            if (StaminaSystem.HasInstance)
            {
                StaminaSystem.Instance.AddStamina(5);
                _dailyFriendStaminaClaims++;
                SaveShareState();
                Debug.Log($"[Social] 领取好友体力: +5 ({_dailyFriendStaminaClaims}/{MaxDailyFriendStamina})");
                return true;
            }
            return false;
        }

        /// <summary>获取今日剩余好友体力领取次数</summary>
        public int GetRemainingFriendStamina()
        {
            CheckFriendStaminaDateReset();
            return Mathf.Max(0, MaxDailyFriendStamina - _dailyFriendStaminaClaims);
        }

        // ========== 邀请奖励 ==========

        /// <summary>生成邀请链接</summary>
        public string GenerateInviteLink()
        {
            string playerId = PlayerDataManager.HasInstance ? PlayerDataManager.Instance.Data.PlayerId : "unknown";
            return $"invite_{playerId}";
        }

        /// <summary>处理邀请奖励</summary>
        public void ProcessInviteReward(string inviterId)
        {
            // 被邀请者奖励
            if (PlayerDataManager.HasInstance)
            {
                PlayerDataManager.Instance.AddDiamonds(100);
            }

            Debug.Log($"[Social] 邀请奖励已发放, 邀请人: {inviterId}");
        }

        // ========== 私有方法 ==========

        private void CheckShareDateReset()
        {
            int today = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            if (_lastShareDate != today)
            {
                _lastShareDate = today;
                _dailyShareCount = 0;
            }
        }

        private void CheckFriendStaminaDateReset()
        {
            int today = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            if (_lastFriendStaminaDate != today)
            {
                _lastFriendStaminaDate = today;
                _dailyFriendStaminaClaims = 0;
            }
        }

        private List<RankEntry> GenerateMockRank()
        {
            var rank = new List<RankEntry>();

            // 添加自己
            if (PlayerDataManager.HasInstance)
            {
                var data = PlayerDataManager.Instance.Data;
                rank.Add(new RankEntry
                {
                    PlayerId = data.PlayerId,
                    Nickname = data.Nickname,
                    Score = data.TotalWins * 100 + data.Level * 10,
                    Rank = 0
                });
            }

            // 模拟好友数据
            string[] mockNames = { "小明", "大壮", "阿花", "老王", "小李", "张三", "李四", "王五" };
            for (int i = 0; i < mockNames.Length; i++)
            {
                rank.Add(new RankEntry
                {
                    PlayerId = $"mock_{i}",
                    Nickname = mockNames[i],
                    Score = UnityEngine.Random.Range(50, 5000),
                    Rank = 0
                });
            }

            // 按分数排序
            rank.Sort((a, b) => b.Score.CompareTo(a.Score));

            // 设置排名
            for (int i = 0; i < rank.Count; i++)
            {
                rank[i].Rank = i + 1;
            }

            return rank;
        }

        private void LoadShareState()
        {
            if (SaveManager.HasInstance)
            {
                var state = SaveManager.Instance.Load<ShareSaveState>("share_state");
                if (state != null)
                {
                    _dailyShareCount = state.DailyShareCount;
                    _lastShareDate = state.LastShareDate;
                    _dailyFriendStaminaClaims = state.DailyFriendStaminaClaims;
                    _lastFriendStaminaDate = state.LastFriendStaminaDate;
                }
            }
        }

        private void SaveShareState()
        {
            if (SaveManager.HasInstance)
            {
                SaveManager.Instance.Save("share_state", new ShareSaveState
                {
                    DailyShareCount = _dailyShareCount,
                    LastShareDate = _lastShareDate,
                    DailyFriendStaminaClaims = _dailyFriendStaminaClaims,
                    LastFriendStaminaDate = _lastFriendStaminaDate
                });
            }
        }
    }

    [Serializable]
    public class ShareSaveState
    {
        public int DailyShareCount;
        public int LastShareDate;
        public int DailyFriendStaminaClaims;
        public int LastFriendStaminaDate;
    }
}
