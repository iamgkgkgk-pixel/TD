// ============================================================
// 文件名：BattleBalanceAdjuster.cs
// 功能描述：战斗平衡性调整与Playtest支持系统
//          动态难度调整(DDA)、数值微调面板、战斗统计
//          支持MVP阶段的快速迭代平衡
// 创建时间：2026-03-25
// 所属模块：Battle/Polish
// 对应交互：阶段三 #221-245（多轮Playtest反馈迭代）
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Tower;
using AetheraSurvivors.Battle.Wave;
using AetheraSurvivors.Battle.Rune;
using Logger = AetheraSurvivors.Framework.Logger;


namespace AetheraSurvivors.Battle.Polish

{
    // ====================================================================
    // 战斗统计数据
    // ====================================================================

    /// <summary>
    /// 单局战斗统计（用于平衡性分析和Playtest）
    /// </summary>
    [Serializable]
    public class BattleStatistics
    {
        // ---- 基础统计 ----
        /// <summary>战斗时长（秒）</summary>
        public float Duration;
        /// <summary>是否胜利</summary>
        public bool IsVictory;
        /// <summary>完成的波次数</summary>
        public int WavesCompleted;
        /// <summary>总波次数</summary>
        public int TotalWaves;

        // ---- 击杀统计 ----
        /// <summary>总击杀数</summary>
        public int TotalKills;
        /// <summary>Boss击杀数</summary>
        public int BossKills;
        /// <summary>最高连击数</summary>
        public int MaxCombo;

        // ---- 经济统计 ----
        /// <summary>总收入</summary>
        public int TotalGoldEarned;
        /// <summary>总花费</summary>
        public int TotalGoldSpent;
        /// <summary>最终金币</summary>
        public int FinalGold;

        // ---- 塔统计 ----
        /// <summary>建造的塔总数</summary>
        public int TowersBuilt;
        /// <summary>升级次数</summary>
        public int TowerUpgrades;
        /// <summary>出售次数</summary>
        public int TowerSells;
        /// <summary>最终塔数量</summary>
        public int FinalTowerCount;
        /// <summary>各类塔建造数量</summary>
        public Dictionary<TowerType, int> TowerTypeCount = new Dictionary<TowerType, int>();

        // ---- 伤害统计 ----
        /// <summary>总输出伤害</summary>
        public float TotalDamageDealt;
        /// <summary>各塔类型输出伤害</summary>
        public Dictionary<TowerType, float> DamageByTowerType = new Dictionary<TowerType, float>();
        /// <summary>DPS峰值</summary>
        public float PeakDPS;

        // ---- 防御统计 ----
        /// <summary>基地受到的总伤害（泄漏怪物数量）</summary>
        public int BaseDamageTaken;
        /// <summary>最低基地血量</summary>
        public int LowestBaseHP;

        // ---- 词条统计 ----
        /// <summary>选择的词条数量</summary>
        public int RunesSelected;
        /// <summary>选择的词条列表</summary>
        public List<string> SelectedRuneNames = new List<string>();

        // ---- 难度评估 ----
        /// <summary>难度评分（0~10，基于各项指标综合计算）</summary>
        public float DifficultyScore;
        /// <summary>难度评级（"太简单"/"适中"/"太难"）</summary>
        public string DifficultyRating;
    }

    /// <summary>战斗统计完成事件</summary>
    public struct BattleStatsEvent : IEvent
    {
        public BattleStatistics Stats;
    }

    // ====================================================================
    // 动态难度调整（DDA）配置
    // ====================================================================

    /// <summary>DDA配置</summary>
    [Serializable]
    public class DDAConfig
    {
        /// <summary>是否启用DDA</summary>
        public bool enabled = true;

        /// <summary>HP调整范围（0.7~1.3，即最多±30%）</summary>
        public float minHPMultiplier = 0.7f;
        public float maxHPMultiplier = 1.3f;

        /// <summary>速度调整范围</summary>
        public float minSpeedMultiplier = 0.85f;
        public float maxSpeedMultiplier = 1.15f;

        /// <summary>金币调整范围</summary>
        public float minGoldMultiplier = 0.8f;
        public float maxGoldMultiplier = 1.3f;

        /// <summary>基地血量低于此值时降低难度</summary>
        public float lowHPThreshold = 0.3f;

        /// <summary>基地血量高于此值时提升难度</summary>
        public float highHPThreshold = 0.8f;
    }

    // ====================================================================
    // BattleBalanceAdjuster 核心类
    // ====================================================================

    /// <summary>
    /// 战斗平衡性调整与Playtest支持系统
    /// 
    /// 三大职责：
    /// 1. 战斗统计 — 记录详细的战斗数据用于分析
    /// 2. 动态难度 — 根据玩家表现微调怪物强度
    /// 3. 调试面板 — 提供实时数值调整能力
    /// 
    /// 为什么需要DDA？
    /// - 手游玩家水平差异大，固定难度会让大部分人觉得"太简单"或"太难"
    /// - DDA让每个玩家都能体验到"差一点就输了"的紧张感
    /// - 重点是**隐性调整**：玩家感觉不到难度在变化
    /// </summary>
    public class BattleBalanceAdjuster : MonoSingleton<BattleBalanceAdjuster>
    {
        // ========== 配置 ==========

        [Header("动态难度配置")]
        [SerializeField] private DDAConfig _ddaConfig = new DDAConfig();

        // ========== 运行时数据 ==========

        /// <summary>当前战斗统计</summary>
        private BattleStatistics _stats;

        /// <summary>DPS滑动窗口（用于计算峰值DPS）</summary>
        private readonly Queue<float> _dpsWindow = new Queue<float>();
        private float _dpsAccumulator = 0f;
        private float _dpsTimer = 0f;
        private const float DPSWindowSize = 1f; // 1秒窗口

        /// <summary>当前DDA倍率</summary>
        private float _currentHPMultiplier = 1f;
        private float _currentSpeedMultiplier = 1f;
        private float _currentGoldMultiplier = 1f;

        /// <summary>DDA平滑过渡速度</summary>
        private const float DDALerpSpeed = 0.5f;

        /// <summary>目标DDA倍率</summary>
        private float _targetHPMultiplier = 1f;
        private float _targetSpeedMultiplier = 1f;
        private float _targetGoldMultiplier = 1f;

        // ========== 公共属性 ==========

        /// <summary>当前战斗统计</summary>
        public BattleStatistics CurrentStats => _stats;

        /// <summary>DDA配置</summary>
        public DDAConfig DDA => _ddaConfig;

        /// <summary>当前DDA - HP倍率</summary>
        public float DDAHPMultiplier => _currentHPMultiplier;

        /// <summary>当前DDA - 速度倍率</summary>
        public float DDASpeedMultiplier => _currentSpeedMultiplier;

        /// <summary>当前DDA - 金币倍率</summary>
        public float DDAGoldMultiplier => _currentGoldMultiplier;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            _stats = new BattleStatistics();

            // 订阅事件
            EventBus.Instance.Subscribe<EnemyDeathEvent>(OnEnemyKilled);
            EventBus.Instance.Subscribe<EnemyReachedBaseEvent>(OnEnemyReachedBase);
            EventBus.Instance.Subscribe<TowerAttackEvent>(OnTowerAttack);
            EventBus.Instance.Subscribe<TowerUpgradedEvent>(OnTowerUpgraded);
            EventBus.Instance.Subscribe<TowerSoldEvent>(OnTowerSold);
            EventBus.Instance.Subscribe<WaveCompleteEvent>(OnWaveComplete);
            EventBus.Instance.Subscribe<BattleResultEvent>(OnBattleEnd);
            EventBus.Instance.Subscribe<ComboEvent>(OnCombo);
            EventBus.Instance.Subscribe<RuneSelectedEvent>(OnRuneSelected);


            Logger.I("BattleBalanceAdjuster", "平衡性调整系统初始化 DDA={0}", _ddaConfig.enabled);
        }

        protected override void OnDispose()
        {
            EventBus.Instance.Unsubscribe<EnemyDeathEvent>(OnEnemyKilled);
            EventBus.Instance.Unsubscribe<EnemyReachedBaseEvent>(OnEnemyReachedBase);
            EventBus.Instance.Unsubscribe<TowerAttackEvent>(OnTowerAttack);
            EventBus.Instance.Unsubscribe<TowerUpgradedEvent>(OnTowerUpgraded);
            EventBus.Instance.Unsubscribe<TowerSoldEvent>(OnTowerSold);
            EventBus.Instance.Unsubscribe<WaveCompleteEvent>(OnWaveComplete);
            EventBus.Instance.Unsubscribe<BattleResultEvent>(OnBattleEnd);
            EventBus.Instance.Unsubscribe<ComboEvent>(OnCombo);
            EventBus.Instance.Unsubscribe<RuneSelectedEvent>(OnRuneSelected);

        }

        private void Update()
        {
            // 更新DPS统计
            UpdateDPSTracking(Time.deltaTime);

            // 更新DDA
            if (_ddaConfig.enabled)
            {
                UpdateDDA(Time.deltaTime);
            }
        }

        // ====================================================================
        // 1. DPS统计
        // ====================================================================

        /// <summary>记录一次伤害</summary>
        public void RecordDamage(float damage, TowerType towerType)
        {
            _stats.TotalDamageDealt += damage;
            _dpsAccumulator += damage;

            if (!_stats.DamageByTowerType.ContainsKey(towerType))
                _stats.DamageByTowerType[towerType] = 0f;
            _stats.DamageByTowerType[towerType] += damage;
        }

        /// <summary>更新DPS追踪</summary>
        private void UpdateDPSTracking(float deltaTime)
        {
            _dpsTimer += deltaTime;
            if (_dpsTimer >= DPSWindowSize)
            {
                _dpsTimer = 0f;
                float currentDPS = _dpsAccumulator / DPSWindowSize;
                _dpsAccumulator = 0f;

                _dpsWindow.Enqueue(currentDPS);
                if (_dpsWindow.Count > 10)
                    _dpsWindow.Dequeue();

                if (currentDPS > _stats.PeakDPS)
                    _stats.PeakDPS = currentDPS;
            }
        }

        /// <summary>获取当前实时DPS</summary>
        public float GetCurrentDPS()
        {
            if (_dpsWindow.Count == 0) return 0f;
            float sum = 0f;
            foreach (float dps in _dpsWindow) sum += dps;
            return sum / _dpsWindow.Count;
        }

        // ====================================================================
        // 2. 动态难度调整（DDA）
        // ====================================================================

        /// <summary>更新DDA倍率</summary>
        private void UpdateDDA(float deltaTime)
        {
            if (!BaseHealth.HasInstance) return;

            float hpPercent = (float)BaseHealth.Instance.CurrentHP / Mathf.Max(BaseHealth.Instance.MaxHP, 1);

            // 根据基地血量比例调整目标倍率
            if (hpPercent < _ddaConfig.lowHPThreshold)
            {
                // 低血量 → 降低难度（怪物变弱/变慢，多给金币）
                float urgency = 1f - (hpPercent / _ddaConfig.lowHPThreshold);
                _targetHPMultiplier = Mathf.Lerp(1f, _ddaConfig.minHPMultiplier, urgency);
                _targetSpeedMultiplier = Mathf.Lerp(1f, _ddaConfig.minSpeedMultiplier, urgency);
                _targetGoldMultiplier = Mathf.Lerp(1f, _ddaConfig.maxGoldMultiplier, urgency);
            }
            else if (hpPercent > _ddaConfig.highHPThreshold)
            {
                // 高血量 → 提升难度（怪物变强/变快，少给金币）
                float comfort = (hpPercent - _ddaConfig.highHPThreshold) / (1f - _ddaConfig.highHPThreshold);
                _targetHPMultiplier = Mathf.Lerp(1f, _ddaConfig.maxHPMultiplier, comfort);
                _targetSpeedMultiplier = Mathf.Lerp(1f, _ddaConfig.maxSpeedMultiplier, comfort);
                _targetGoldMultiplier = Mathf.Lerp(1f, _ddaConfig.minGoldMultiplier, comfort);
            }
            else
            {
                // 血量适中 → 逐渐回归标准
                _targetHPMultiplier = 1f;
                _targetSpeedMultiplier = 1f;
                _targetGoldMultiplier = 1f;
            }

            // 平滑过渡（避免突然变化被玩家察觉）
            _currentHPMultiplier = Mathf.Lerp(_currentHPMultiplier, _targetHPMultiplier, DDALerpSpeed * deltaTime);
            _currentSpeedMultiplier = Mathf.Lerp(_currentSpeedMultiplier, _targetSpeedMultiplier, DDALerpSpeed * deltaTime);
            _currentGoldMultiplier = Mathf.Lerp(_currentGoldMultiplier, _targetGoldMultiplier, DDALerpSpeed * deltaTime);
        }

        /// <summary>重置DDA（新关卡开始）</summary>
        public void ResetDDA()
        {
            _currentHPMultiplier = 1f;
            _currentSpeedMultiplier = 1f;
            _currentGoldMultiplier = 1f;
            _targetHPMultiplier = 1f;
            _targetSpeedMultiplier = 1f;
            _targetGoldMultiplier = 1f;
        }

        // ====================================================================
        // 3. 难度评估
        // ====================================================================

        /// <summary>
        /// 计算难度评分（战斗结束时调用）
        /// 0分=极易（碾压），5分=适中，10分=极难（惨败）
        /// </summary>
        public float CalculateDifficultyScore()
        {
            float score = 5f; // 基准

            if (!BaseHealth.HasInstance) return score;

            float hpPercent = _stats.IsVictory
                ? (float)_stats.LowestBaseHP / Mathf.Max(BaseHealth.Instance.MaxHP, 1)
                : 0f;

            // 1. 基地血量影响（权重40%）
            // 满血通关=太简单(-2)，1滴血通关=完美紧张(+1)，失败=太难(+2)
            if (_stats.IsVictory)
            {
                float hpFactor = (1f - hpPercent) * 3f - 1f; // 满血=-1, 半血=0.5, 1血≈2
                score += hpFactor * 0.4f;
            }
            else
            {
                float waveProgress = _stats.TotalWaves > 0
                    ? (float)_stats.WavesCompleted / _stats.TotalWaves
                    : 0f;
                score += (2f + (1f - waveProgress) * 3f) * 0.4f; // 失败基础+2，越早失败越难
            }

            // 2. 泄漏怪物数量影响（权重30%）
            float leakRate = _stats.TotalKills > 0
                ? (float)_stats.BaseDamageTaken / (_stats.TotalKills + _stats.BaseDamageTaken)
                : 0f;
            score += (leakRate * 6f - 1f) * 0.3f;

            // 3. 经济压力影响（权重20%）
            // 最终金币越少说明经济越紧张
            float goldPressure = _stats.FinalGold < 50 ? 1f :
                                _stats.FinalGold < 200 ? 0.5f : 0f;
            score += (goldPressure * 2f - 0.5f) * 0.2f;

            // 4. 战斗时长影响（权重10%）
            float expectedDuration = _stats.TotalWaves * 30f; // 假设每波30秒
            float durationRatio = expectedDuration > 0 ? _stats.Duration / expectedDuration : 1f;
            score += (durationRatio - 1f) * 0.1f;

            score = Mathf.Clamp(score, 0f, 10f);
            _stats.DifficultyScore = score;
            _stats.DifficultyRating = GetDifficultyRating(score);

            return score;
        }

        /// <summary>获取难度评级文字</summary>
        public static string GetDifficultyRating(float score)
        {
            if (score < 2f) return "太简单 — 需要增加怪物强度或减少资源";
            if (score < 3.5f) return "偏容易 — 可以微调提升";
            if (score < 6.5f) return "✅ 适中 — 体验良好";
            if (score < 8f) return "偏困难 — 可以微调降低";
            return "太难 — 需要降低怪物强度或增加资源";
        }

        // ====================================================================
        // 4. 实时数值调整（调试用）
        // ====================================================================

        /// <summary>
        /// 全局伤害倍率调整（调试用，叠加在词条之上）
        /// </summary>
        [Header("调试用全局倍率")]
        [SerializeField] [Range(0.1f, 5f)] private float _globalDamageMultiplier = 1f;
        [SerializeField] [Range(0.1f, 5f)] private float _globalHPMultiplier = 1f;
        [SerializeField] [Range(0.1f, 3f)] private float _globalSpeedMultiplier = 1f;
        [SerializeField] [Range(0.1f, 3f)] private float _globalGoldMultiplier = 1f;

        /// <summary>全局伤害倍率</summary>
        public float GlobalDamageMultiplier => _globalDamageMultiplier;

        /// <summary>全局HP倍率（DDA + 调试叠加）</summary>
        public float GlobalHPMultiplier => _globalHPMultiplier * _currentHPMultiplier;

        /// <summary>全局速度倍率（DDA + 调试叠加）</summary>
        public float GlobalSpeedMultiplier => _globalSpeedMultiplier * _currentSpeedMultiplier;

        /// <summary>全局金币倍率（DDA + 调试叠加）</summary>
        public float GlobalGoldMultiplier => _globalGoldMultiplier * _currentGoldMultiplier;

        // ====================================================================
        // 事件处理
        // ====================================================================

        private void OnEnemyKilled(EnemyDeathEvent evt)
        {
            _stats.TotalKills++;
            if (evt.IsBoss) _stats.BossKills++;
            _stats.TotalGoldEarned += evt.GoldDrop;
        }

        private void OnEnemyReachedBase(EnemyReachedBaseEvent evt)
        {
            _stats.BaseDamageTaken++;

            if (BaseHealth.HasInstance)
            {
                int hp = BaseHealth.Instance.CurrentHP;
                if (hp < _stats.LowestBaseHP || _stats.LowestBaseHP == 0)
                    _stats.LowestBaseHP = hp;
            }
        }

        private void OnTowerAttack(TowerAttackEvent evt)
        {
            RecordDamage(evt.Damage, evt.TowerType);
        }

        private void OnTowerUpgraded(TowerUpgradedEvent evt)
        {
            _stats.TowerUpgrades++;
        }

        private void OnTowerSold(TowerSoldEvent evt)
        {
            _stats.TowerSells++;
        }

        private void OnWaveComplete(WaveCompleteEvent evt)
        {
            _stats.WavesCompleted = evt.WaveIndex;
            _stats.TotalWaves = evt.TotalWaves;
        }

        private void OnCombo(ComboEvent evt)
        {
            if (evt.ComboCount > _stats.MaxCombo)
                _stats.MaxCombo = evt.ComboCount;
        }

        private void OnRuneSelected(RuneSelectedEvent evt)

        {
            _stats.RunesSelected++;
            _stats.SelectedRuneNames.Add(evt.RuneName);
        }

        private void OnBattleEnd(BattleResultEvent evt)
        {
            _stats.IsVictory = evt.IsVictory;
            _stats.Duration = evt.Duration;
            _stats.FinalGold = BattleEconomyManager.HasInstance ? BattleEconomyManager.Instance.CurrentGold : 0;
            _stats.FinalTowerCount = TowerManager.HasInstance ? TowerManager.Instance.ActiveTowerCount : 0;

            // 计算难度评分
            CalculateDifficultyScore();

            // 发布统计事件
            EventBus.Instance.Publish(new BattleStatsEvent { Stats = _stats });

            // 输出战斗报告
            Logger.I("BattleBalanceAdjuster", "========== 战斗报告 ==========");
            Logger.I("BattleBalanceAdjuster", "结果: {0} | 时长: {1:F1}秒", evt.IsVictory ? "✅胜利" : "❌失败", evt.Duration);
            Logger.I("BattleBalanceAdjuster", "波次: {0}/{1} | 击杀: {2} | Boss: {3}",
                _stats.WavesCompleted, _stats.TotalWaves, _stats.TotalKills, _stats.BossKills);
            Logger.I("BattleBalanceAdjuster", "最高连击: {0} | 总伤害: {1:F0} | 峰值DPS: {2:F0}",
                _stats.MaxCombo, _stats.TotalDamageDealt, _stats.PeakDPS);
            Logger.I("BattleBalanceAdjuster", "经济: 收入{0} 花费{1} 剩余{2}",
                _stats.TotalGoldEarned, _stats.TotalGoldSpent, _stats.FinalGold);
            Logger.I("BattleBalanceAdjuster", "难度评分: {0:F1}/10 — {1}",
                _stats.DifficultyScore, _stats.DifficultyRating);
            Logger.I("BattleBalanceAdjuster", "============================");
        }

        // ====================================================================
        // 重置
        // ====================================================================

        /// <summary>重置统计（新战斗开始时）</summary>
        public void ResetStats()
        {
            _stats = new BattleStatistics();
            _dpsWindow.Clear();
            _dpsAccumulator = 0f;
            _dpsTimer = 0f;
            ResetDDA();
        }

        // ====================================================================
        // 调试
        // ====================================================================

        public string GetDebugInfo()
        {
            return $"击杀:{_stats.TotalKills} DPS:{GetCurrentDPS():F0} " +
                   $"DDA: HP×{_currentHPMultiplier:F2} SPD×{_currentSpeedMultiplier:F2} " +
                   $"GOLD×{_currentGoldMultiplier:F2}";
        }
    }
}
