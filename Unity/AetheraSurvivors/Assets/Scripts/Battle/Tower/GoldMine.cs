// ============================================================
// 文件名：GoldMine.cs
// 功能描述：金矿 — 定时产出金币、可升级、有建造数量限制
// 创建时间：2026-03-25
// 所属模块：Battle/Tower
// 对应交互：阶段三 #126
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Tower

{
    /// <summary>
    /// 金矿产出金币事件
    /// </summary>
    public struct GoldMineProducedEvent : IEvent
    {
        public int TowerId;
        public int GoldAmount;
        public Vector3 Position;
    }

    /// <summary>
    /// 金矿 — 被动经济建筑
    /// 特性：
    /// - 不攻击怪物，定时产出金币
    /// - 建造数量有限制（每张地图最多3个）
    /// - Lv1：每10秒产出15金 → Lv2：每8秒产出20金 → Lv3：每6秒产出30金
    /// </summary>
    public class GoldMine : TowerBase
    {
        // ========== 配置 ==========

        /// <summary>最大建造数量</summary>
        public const int MaxGoldMines = 3;

        /// <summary>当前场上金矿数量（静态追踪）</summary>
        private static int _activeGoldMineCount = 0;

        /// <summary>各等级产出间隔（秒）</summary>
        private float ProduceInterval
        {
            get
            {
                switch (_currentLevel)
                {
                    case 1: return 10f;
                    case 2: return 8f;
                    default: return 6f;
                }
            }
        }

        /// <summary>各等级产出金币数</summary>
        private int ProduceAmount
        {
            get
            {
                switch (_currentLevel)
                {
                    case 1: return 15;
                    case 2: return 20;
                    default: return 30;
                }
            }
        }

        // ========== 运行时 ==========

        private float _produceTimer = 0f;

        // ========== 公共属性 ==========

        /// <summary>当前场上金矿数量</summary>
        public static int ActiveCount => _activeGoldMineCount;

        /// <summary>是否可以再建造金矿</summary>
        public static bool CanBuildMore => _activeGoldMineCount < MaxGoldMines;

        // ========== 生命周期 ==========

        public override void Initialize(TowerConfig config, Vector2Int gridPos)
        {
            base.Initialize(config, gridPos);
            _produceTimer = ProduceInterval;
            _activeGoldMineCount++;

            Logger.D("GoldMine", "金矿建造 ({0}/{1})", _activeGoldMineCount, MaxGoldMines);
        }

        protected override void Update()
        {
            if (!_isInitialized) return;

            // 金矿不攻击，只产出金币
            _produceTimer -= Time.deltaTime;
            if (_produceTimer <= 0f)
            {
                ProduceGold();
                _produceTimer = ProduceInterval;
            }
        }

        /// <summary>产出金币</summary>
        private void ProduceGold()
        {
            EventBus.Instance.Publish(new GoldMineProducedEvent
            {
                TowerId = InstanceId,
                GoldAmount = ProduceAmount,
                Position = transform.position
            });

            Logger.D("GoldMine", "金矿产出: +{0}金币", ProduceAmount);
        }

        public override int Sell()
        {
            _activeGoldMineCount = Mathf.Max(0, _activeGoldMineCount - 1);
            Logger.D("GoldMine", "金矿出售，剩余 {0}/{1}", _activeGoldMineCount, MaxGoldMines);
            return base.Sell();
        }

        public override void OnDespawn()
        {
            _activeGoldMineCount = Mathf.Max(0, _activeGoldMineCount - 1);
            base.OnDespawn();
        }

        /// <summary>重置静态计数器（战斗结束时调用）</summary>
        public static void ResetCount()
        {
            _activeGoldMineCount = 0;
        }
    }
}
