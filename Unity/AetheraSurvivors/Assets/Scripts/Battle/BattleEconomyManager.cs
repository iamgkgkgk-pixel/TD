// ============================================================
// 文件名：BattleEconomyManager.cs
// 功能描述：局内经济系统 — 金币管理
//          击杀获金、波次奖励、金矿产出、放塔消耗、升级消耗
// 创建时间：2026-03-25
// 所属模块：Battle
// 对应交互：阶段三 #143
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Tower;
using AetheraSurvivors.Battle.Wave;

namespace AetheraSurvivors.Battle
{
    /// <summary>金币变化事件</summary>
    public struct GoldChangedEvent : IEvent
    {
        public int CurrentGold;
        public int Delta;
        public string Reason;
    }

    /// <summary>
    /// 局内经济管理器 — 管理战斗中的金币收支
    /// </summary>
    public class BattleEconomyManager : MonoSingleton<BattleEconomyManager>
    {
        // ========== 运行时数据 ==========

        /// <summary>当前金币</summary>
        private int _currentGold = 0;

        /// <summary>本局总收入</summary>
        private int _totalIncome = 0;

        /// <summary>本局总支出</summary>
        private int _totalExpense = 0;

        // ========== 配置常量 ==========

        /// <summary>每波次完成奖励基础金币</summary>
        private const int WaveCompleteBonusBase = 20;

        /// <summary>精英波次额外奖励</summary>
        private const int EliteWaveBonus = 30;

        /// <summary>Boss波次额外奖励</summary>
        private const int BossWaveBonus = 50;

        // ========== 公共属性 ==========

        /// <summary>当前金币</summary>
        public int CurrentGold => _currentGold;

        /// <summary>本局总收入</summary>
        public int TotalIncome => _totalIncome;

        /// <summary>本局总支出</summary>
        public int TotalExpense => _totalExpense;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            // 订阅事件
            EventBus.Instance.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
            EventBus.Instance.Subscribe<WaveCompleteEvent>(OnWaveComplete);
            EventBus.Instance.Subscribe<GoldMineProducedEvent>(OnGoldMineProduced);

            Logger.I("BattleEconomy", "局内经济系统初始化");
        }

        protected override void OnDispose()
        {
            EventBus.Instance.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
            EventBus.Instance.Unsubscribe<WaveCompleteEvent>(OnWaveComplete);
            EventBus.Instance.Unsubscribe<GoldMineProducedEvent>(OnGoldMineProduced);
        }

        // ========== 核心方法 ==========

        /// <summary>
        /// 初始化经济（关卡开始时）
        /// </summary>
        public void InitEconomy(int startGold)
        {
            _currentGold = startGold;
            _totalIncome = startGold;
            _totalExpense = 0;

            PublishGoldChanged(0, "初始金币");
            Logger.I("BattleEconomy", "经济初始化: 起始金币={0}", startGold);
        }

        /// <summary>是否可以负担费用</summary>
        public bool CanAfford(int cost)
        {
            return _currentGold >= cost;
        }

        /// <summary>增加金币</summary>
        public void AddGold(int amount, string reason = "")
        {
            if (amount <= 0) return;

            _currentGold += amount;
            _totalIncome += amount;

            PublishGoldChanged(amount, reason);
        }

        /// <summary>消耗金币</summary>
        public bool SpendGold(int amount, string reason = "")
        {
            if (amount <= 0) return true;
            if (_currentGold < amount)
            {
                Logger.D("BattleEconomy", "金币不足: 需要{0}, 当前{1}", amount, _currentGold);
                return false;
            }

            _currentGold -= amount;
            _totalExpense += amount;

            PublishGoldChanged(-amount, reason);
            return true;
        }

        /// <summary>直接设置金币数量（仅供BugFixer异常修复使用）</summary>
        public void SetGold(int amount)
        {
            int delta = amount - _currentGold;
            _currentGold = Mathf.Max(0, amount);
            PublishGoldChanged(delta, "系统修正");
        }

        /// <summary>重置经济数据</summary>
        public void Reset()
        {
            _currentGold = 0;
            _totalIncome = 0;
            _totalExpense = 0;
        }


        // ========== 事件处理 ==========

        /// <summary>怪物被击杀 → 获得金币</summary>
        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            if (evt.GoldDrop > 0)
            {
                AddGold(evt.GoldDrop, $"击杀{evt.EnemyType}");
            }
        }

        /// <summary>波次完成 → 波次奖励</summary>
        private void OnWaveComplete(WaveCompleteEvent evt)
        {
            int bonus = WaveCompleteBonusBase + evt.WaveIndex * 5;
            AddGold(bonus, $"第{evt.WaveIndex}波完成奖励");
        }

        /// <summary>金矿产出</summary>
        private void OnGoldMineProduced(GoldMineProducedEvent evt)
        {
            AddGold(evt.GoldAmount, "金矿产出");
        }

        // ========== 内部方法 ==========

        private void PublishGoldChanged(int delta, string reason)
        {
            EventBus.Instance.Publish(new GoldChangedEvent
            {
                CurrentGold = _currentGold,
                Delta = delta,
                Reason = reason
            });
        }

        /// <summary>获取经济统计信息</summary>
        public string GetDebugInfo()
        {
            return $"金币:{_currentGold} 总收入:{_totalIncome} 总支出:{_totalExpense}";
        }
    }
}
