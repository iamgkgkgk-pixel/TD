// ============================================================
// 文件名：RedDotManager.cs
// 功能描述：红点系统 — 统一管理各入口的红点显示，支持多级红点
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #266
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>
    /// 红点节点
    /// </summary>
    public class RedDotNode
    {
        public string NodeId;
        public string ParentId;
        public bool HasRedDot;
        public int Count;
        public Func<bool> CheckFunc; // 自定义检查函数
        public List<string> ChildIds = new List<string>();
    }

    /// <summary>
    /// 红点管理器 — 纯C#单例
    /// 
    /// 支持树形红点结构：
    ///   root
    ///   ├── nav_quest（任务入口）
    ///   │   ├── quest_daily（每日任务）
    ///   │   └── quest_achievement（成就）
    ///   ├── nav_mail（邮件入口）
    ///   ├── hero（英雄入口）
    ///   │   ├── hero_levelup（可升级）
    ///   │   └── hero_starup（可升星）
    ///   ├── shop（商城入口）
    ///   ├── gacha（抽卡入口）
    ///   ├── battlepass（战令入口）
    ///   └── checkin（签到入口）
    /// </summary>
    public class RedDotManager : Singleton<RedDotManager>
    {
        private Dictionary<string, RedDotNode> _nodes = new Dictionary<string, RedDotNode>();

        protected override void OnInit()
        {
            // 注册红点节点树
            RegisterNode("root", null);

            // 导航栏
            RegisterNode("nav_quest", "root");
            RegisterNode("nav_mail", "root");

            // 任务子节点
            RegisterNode("quest_daily", "nav_quest");
            RegisterNode("quest_achievement", "nav_quest");

            // 英雄
            RegisterNode("hero", "root");
            RegisterNode("hero_levelup", "hero");
            RegisterNode("hero_starup", "hero");

            // 商城
            RegisterNode("shop", "root");
            RegisterNode("shop_first_pay", "shop");

            // 抽卡
            RegisterNode("gacha", "root");
            RegisterNode("gacha_free", "gacha");

            // 战令
            RegisterNode("battlepass", "root");
            RegisterNode("battlepass_reward", "battlepass");

            // 签到
            RegisterNode("checkin", "root");

            // 邮件
            RegisterNode("mail_unread", "nav_mail");

            Debug.Log("[RedDotManager] 初始化完成");
        }

        protected override void OnDispose()
        {
            _nodes.Clear();
        }

        // ========== 公共方法 ==========

        /// <summary>注册红点节点</summary>
        public void RegisterNode(string nodeId, string parentId, Func<bool> checkFunc = null)
        {
            if (_nodes.ContainsKey(nodeId)) return;

            var node = new RedDotNode
            {
                NodeId = nodeId,
                ParentId = parentId,
                HasRedDot = false,
                Count = 0,
                CheckFunc = checkFunc
            };

            _nodes[nodeId] = node;

            // 添加到父节点的子列表
            if (!string.IsNullOrEmpty(parentId) && _nodes.TryGetValue(parentId, out var parent))
            {
                parent.ChildIds.Add(nodeId);
            }
        }

        /// <summary>设置红点状态</summary>
        public void SetRedDot(string nodeId, bool hasRedDot, int count = 0)
        {
            if (!_nodes.TryGetValue(nodeId, out var node)) return;

            bool changed = node.HasRedDot != hasRedDot || node.Count != count;
            node.HasRedDot = hasRedDot;
            node.Count = count;

            if (changed)
            {
                // 发布变化事件
                if (EventBus.HasInstance)
                {
                    EventBus.Instance.Publish(new RedDotChangedEvent
                    {
                        NodeId = nodeId,
                        HasRedDot = hasRedDot,
                        Count = count
                    });
                }

                // 向上传播到父节点
                PropagateToParent(node.ParentId);
            }
        }

        /// <summary>查询红点状态</summary>
        public bool HasRedDot(string nodeId)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
                return node.HasRedDot;
            return false;
        }

        /// <summary>获取红点数量</summary>
        public int GetRedDotCount(string nodeId)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
                return node.Count;
            return 0;
        }

        /// <summary>刷新所有红点状态</summary>
        public void RefreshAll()
        {
            // 刷新签到
            bool canCheckIn = CheckInSystem.HasInstance && !CheckInSystem.Instance.HasCheckedInToday();
            SetRedDot("checkin", canCheckIn);

            // 刷新首充
            bool hasFirstPay = ShopSystem.HasInstance && !ShopSystem.Instance.IsFirstPayClaimed;
            SetRedDot("shop_first_pay", hasFirstPay);

            // 刷新战令可领取
            if (BattlePassSystem.HasInstance)
            {
                int bpLevel = BattlePassSystem.Instance.GetCurrentLevel();
                bool hasUnclaimedBP = false;
                for (int lv = 1; lv <= bpLevel; lv++)
                {
                    if (!BattlePassSystem.Instance.IsFreeRewardClaimed(lv))
                    {
                        hasUnclaimedBP = true;
                        break;
                    }
                }
                SetRedDot("battlepass_reward", hasUnclaimedBP);
            }

            // 刷新每日任务
            RefreshQuestRedDot();
        }

        private void RefreshQuestRedDot()
        {
            if (!DailyQuestSystem.HasInstance) return;

            var quests = DailyQuestSystem.Instance.GetDailyQuests();
            bool hasClaimable = false;
            // 简化检查：如果有已完成未领取的任务
            // 实际需要检查进度数据
            SetRedDot("quest_daily", hasClaimable);
        }

        // ========== 私有方法 ==========

        /// <summary>向上传播红点状态</summary>
        private void PropagateToParent(string parentId)
        {
            if (string.IsNullOrEmpty(parentId)) return;
            if (!_nodes.TryGetValue(parentId, out var parent)) return;

            // 父节点的红点 = 任意子节点有红点
            bool anyChild = false;
            int totalCount = 0;

            for (int i = 0; i < parent.ChildIds.Count; i++)
            {
                if (_nodes.TryGetValue(parent.ChildIds[i], out var child))
                {
                    if (child.HasRedDot)
                    {
                        anyChild = true;
                        totalCount += Mathf.Max(child.Count, 1);
                    }
                }
            }

            bool changed = parent.HasRedDot != anyChild || parent.Count != totalCount;
            parent.HasRedDot = anyChild;
            parent.Count = totalCount;

            if (changed)
            {
                if (EventBus.HasInstance)
                {
                    EventBus.Instance.Publish(new RedDotChangedEvent
                    {
                        NodeId = parentId,
                        HasRedDot = anyChild,
                        Count = totalCount
                    });
                }

                // 继续向上传播
                PropagateToParent(parent.ParentId);
            }
        }
    }
}
