// ============================================================
// 文件名：BattleBugFixer.cs
// 功能描述：战斗Bug修复与安全防护系统
//          寻路异常修复、伤害溢出防护、对象池泄漏检测
//          边界条件处理、状态恢复机制
// 创建时间：2026-03-25
// 所属模块：Battle/Polish
// 对应交互：阶段三 #196-220（战斗Bug修复）
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Tower;
using AetheraSurvivors.Battle.Map;
using AetheraSurvivors.Battle.Wave;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Polish

{
    // ====================================================================
    // Bug修复事件
    // ====================================================================

    /// <summary>Bug检测事件（用于统计和上报）</summary>
    public struct BugDetectedEvent : IEvent
    {
        public string BugType;
        public string Description;
        public string AutoFixApplied;
    }

    // ====================================================================
    // BattleBugFixer 核心类
    // ====================================================================

    /// <summary>
    /// 战斗Bug修复与安全防护系统
    /// 
    /// 设计理念：
    /// 1. 防御性编程 — 在战斗运行中主动检测异常并自动修复
    /// 2. 不依赖玩家上报 — 自动发现并静默修复常见问题
    /// 3. 统计上报 — 检测到的Bug自动上报（后续接入AnalyticsManager）
    /// 
    /// 覆盖的Bug类别：
    /// - 寻路异常：怪物卡墙/停滞/路径丢失
    /// - 伤害异常：NaN/无限大/负数伤害
    /// - 对象池泄漏：未正确回收的对象
    /// - 状态异常：死亡后仍被攻击/重复死亡
    /// - 经济异常：负金币/溢出
    /// - 波次异常：计数不一致/僵死
    /// </summary>
    public class BattleBugFixer : MonoSingleton<BattleBugFixer>
    {
        // ========== 配置 ==========

        /// <summary>检测间隔（秒）</summary>
        private const float CheckInterval = 2.0f;

        /// <summary>怪物停滞检测阈值（秒）— 超过此时间未移动视为卡住</summary>
        private const float StuckThreshold = 3.0f;

        /// <summary>最大允许伤害值（防溢出）</summary>
        private const float MaxDamageValue = 99999f;

        /// <summary>最大允许血量值</summary>
        private const float MaxHPValue = 9999999f;

        /// <summary>对象池泄漏检测阈值（场上不应超过此数量的同类对象）</summary>
        private const int MaxPoolObjectsWarning = 200;

        /// <summary>波次僵死检测阈值（秒）— 超过此时间波次仍未结束</summary>
        private const float WaveDeadlockThreshold = 60f;


        // ========== 运行时数据 ==========

        /// <summary>检测计时器</summary>
        private float _checkTimer = 0f;

        /// <summary>怪物上一帧位置缓存（用于停滞检测）</summary>
        private readonly Dictionary<int, Vector3> _lastEnemyPositions = new Dictionary<int, Vector3>();

        /// <summary>怪物停滞计时器</summary>
        private readonly Dictionary<int, float> _stuckTimers = new Dictionary<int, float>();

        /// <summary>修复统计</summary>
        private int _totalFixesApplied = 0;
        private int _stuckEnemiesFixed = 0;
        private int _damageOverflowFixed = 0;
        private int _poolLeaksFixed = 0;
        private int _waveDeadlocksFixed = 0;

        /// <summary>当前波次开始时间</summary>
        private float _currentWaveStartTime = 0f;

        /// <summary>是否启用自动修复</summary>
        private bool _autoFixEnabled = true;

        // ========== 公共属性 ==========

        /// <summary>是否启用自动修复</summary>
        public bool AutoFixEnabled { get => _autoFixEnabled; set => _autoFixEnabled = value; }

        /// <summary>修复统计信息</summary>
        public string FixStats => $"总修复:{_totalFixesApplied} 卡死:{_stuckEnemiesFixed} " +
                                  $"溢出:{_damageOverflowFixed} 泄漏:{_poolLeaksFixed} 僵死:{_waveDeadlocksFixed}";

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            EventBus.Instance.Subscribe<WaveStartEvent>(OnWaveStart);
            EventBus.Instance.Subscribe<WaveCompleteEvent>(OnWaveComplete);

            Logger.I("BattleBugFixer", "战斗Bug修复系统初始化");
        }

        protected override void OnDispose()
        {
            EventBus.Instance.Unsubscribe<WaveStartEvent>(OnWaveStart);
            EventBus.Instance.Unsubscribe<WaveCompleteEvent>(OnWaveComplete);

            _lastEnemyPositions.Clear();
            _stuckTimers.Clear();

            if (_totalFixesApplied > 0)
            {
                Logger.I("BattleBugFixer", "本局修复统计: {0}", FixStats);
            }
        }

        private void Update()
        {
            if (!_autoFixEnabled) return;

            _checkTimer += Time.deltaTime;
            if (_checkTimer >= CheckInterval)
            {
                _checkTimer = 0f;
                RunAllChecks();
            }
        }

        // ====================================================================
        // 总检测入口
        // ====================================================================

        /// <summary>运行所有检测</summary>
        private void RunAllChecks()
        {
            if (BattleManager.HasInstance &&
                BattleManager.Instance.CurrentState != BattleState.Fighting)
            {
                return; // 只在战斗中检测
            }

            CheckStuckEnemies();
            CheckObjectPoolLeaks();
            CheckWaveDeadlock();
            CheckEconomyAnomaly();
        }

        // ====================================================================
        // 1. 怪物卡死/停滞检测
        // ====================================================================

        /// <summary>检测并修复卡住的怪物</summary>
        private void CheckStuckEnemies()
        {
            if (!EnemySpawner.HasInstance) return;

            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            var toRemove = new List<int>();

            foreach (var enemyObj in enemies)
            {
                if (enemyObj == null || !enemyObj.activeInHierarchy) continue;

                var enemyBase = enemyObj.GetComponent<EnemyBase>();
                if (enemyBase == null || enemyBase.IsDead) continue;
                if (enemyBase.IsStunned) continue; // 被冰冻/眩晕的不算卡死

                int id = enemyBase.InstanceId;
                Vector3 currentPos = enemyObj.transform.position;

                if (_lastEnemyPositions.TryGetValue(id, out Vector3 lastPos))
                {
                    float dist = Vector3.Distance(currentPos, lastPos);

                    if (dist < 0.01f)
                    {
                        // 几乎没动
                        if (!_stuckTimers.ContainsKey(id))
                            _stuckTimers[id] = 0f;

                        _stuckTimers[id] += CheckInterval;

                        if (_stuckTimers[id] >= StuckThreshold)
                        {
                            // 确认卡死，强制处理
                            FixStuckEnemy(enemyBase);
                            toRemove.Add(id);
                        }
                    }
                    else
                    {
                        // 有移动，重置计时器
                        _stuckTimers.Remove(id);
                    }
                }

                _lastEnemyPositions[id] = currentPos;
            }

            // 清理已处理的数据
            foreach (int id in toRemove)
            {
                _lastEnemyPositions.Remove(id);
                _stuckTimers.Remove(id);
            }
        }

        /// <summary>修复卡住的怪物（强制向下一个路径点移动）</summary>
        private void FixStuckEnemy(EnemyBase enemy)
        {
            _stuckEnemiesFixed++;
            _totalFixesApplied++;

            // 方案：强制将怪物传送到下一个路径点
            // 如果是路径丢失，直接视为到达基地
            Logger.W("BattleBugFixer", "修复卡死怪物: {0} ID={1} @{2}",
                enemy.Config?.displayName, enemy.InstanceId, enemy.transform.position);

            // 让怪物受到一次大伤害直接死亡（最安全的处理方式）
            enemy.TakeDamage(new DamageInfo
            {
                Damage = enemy.MaxHP * 2f,
                DamageType = DamageType.True,
                SourceTowerId = -1 // 系统击杀
            });

            ReportBug("EnemyStuck",
                $"怪物{enemy.Config?.displayName}在{enemy.transform.position}位置卡死",
                "强制击杀");
        }

        // ====================================================================
        // 2. 伤害值安全检查（供外部调用）
        // ====================================================================

        /// <summary>
        /// 安全化伤害值（防止NaN/Infinity/负数/溢出）
        /// </summary>
        /// <param name="damage">原始伤害值</param>
        /// <returns>安全的伤害值</returns>
        public static float SanitizeDamage(float damage)
        {
            // NaN / Infinity
            if (float.IsNaN(damage) || float.IsInfinity(damage))
            {
                Logger.W("BattleBugFixer", "检测到异常伤害值: {0}，重置为1", damage);
                if (HasInstance) Instance._damageOverflowFixed++;
                return 1f;
            }

            // 负数
            if (damage < 0f)
            {
                Logger.W("BattleBugFixer", "检测到负伤害: {0}，重置为0", damage);
                if (HasInstance) Instance._damageOverflowFixed++;
                return 0f;
            }

            // 溢出
            if (damage > MaxDamageValue)
            {
                Logger.W("BattleBugFixer", "检测到伤害溢出: {0}，限制为{1}", damage, MaxDamageValue);
                if (HasInstance) Instance._damageOverflowFixed++;
                return MaxDamageValue;
            }

            return damage;
        }

        /// <summary>
        /// 安全化血量值
        /// </summary>
        public static float SanitizeHP(float hp)
        {
            if (float.IsNaN(hp) || float.IsInfinity(hp)) return 0f;
            if (hp < 0f) return 0f;
            if (hp > MaxHPValue) return MaxHPValue;
            return hp;
        }

        /// <summary>
        /// 安全化百分比值（限定在0~1之间）
        /// </summary>
        public static float SanitizePercent(float percent)
        {
            if (float.IsNaN(percent) || float.IsInfinity(percent)) return 0f;
            return Mathf.Clamp01(percent);
        }

        // ====================================================================
        // 3. 对象池泄漏检测
        // ====================================================================

        /// <summary>检测对象池泄漏</summary>
        private void CheckObjectPoolLeaks()
        {
            // 检查场上活跃的Enemy数量
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            if (enemies.Length > MaxPoolObjectsWarning)
            {
                _poolLeaksFixed++;
                _totalFixesApplied++;

                Logger.E("BattleBugFixer", "⚠️ 检测到可能的对象池泄漏! 场上怪物数={0}（阈值{1}）",
                    enemies.Length, MaxPoolObjectsWarning);

                ReportBug("PoolLeak",
                    $"场上怪物数{enemies.Length}超过阈值{MaxPoolObjectsWarning}",
                    "触发警告，暂不自动清理");

                // 清理已死亡但未回收的对象
                int cleaned = 0;
                foreach (var enemyObj in enemies)
                {
                    if (enemyObj == null) continue;
                    var eb = enemyObj.GetComponent<EnemyBase>();
                    if (eb != null && eb.IsDead && enemyObj.activeInHierarchy)
                    {
                        enemyObj.SetActive(false);
                        if (ObjectPoolManager.HasInstance)
                        {
                            ObjectPoolManager.Instance.Return(enemyObj);
                        }
                        cleaned++;
                    }
                }

                if (cleaned > 0)
                {
                    Logger.W("BattleBugFixer", "清理了{0}个已死亡但未回收的怪物对象", cleaned);
                }
            }
        }

        // ====================================================================
        // 4. 波次僵死检测
        // ====================================================================

        /// <summary>检测波次是否僵死（超时未结束）</summary>
        private void CheckWaveDeadlock()
        {
            if (!WaveManager.HasInstance || !WaveManager.Instance.IsWaveActive) return;

            float elapsed = Time.time - _currentWaveStartTime;
            if (elapsed > WaveDeadlockThreshold)
            {
                _waveDeadlocksFixed++;
                _totalFixesApplied++;

                Logger.E("BattleBugFixer", "⚠️ 检测到波次僵死! 波次已运行{0:F0}秒", elapsed);

                // 统计场上残余怪物
                var enemies = GameObject.FindGameObjectsWithTag("Enemy");
                int alive = 0;
                foreach (var e in enemies)
                {
                    if (e != null && e.activeInHierarchy)
                    {
                        var eb = e.GetComponent<EnemyBase>();
                        if (eb != null && !eb.IsDead) alive++;
                    }
                }

                Logger.W("BattleBugFixer", "场上残留{0}个活跃怪物，尝试强制击杀", alive);

                // 强制击杀所有残留怪物
                foreach (var e in enemies)
                {
                    if (e == null || !e.activeInHierarchy) continue;
                    var eb = e.GetComponent<EnemyBase>();
                    if (eb != null && !eb.IsDead)
                    {
                        eb.TakeDamage(new DamageInfo
                        {
                            Damage = eb.MaxHP * 10f,
                            DamageType = DamageType.True,
                            SourceTowerId = -1
                        });
                    }
                }

                ReportBug("WaveDeadlock",
                    $"波次运行{elapsed:F0}秒未结束，残留{alive}个怪物",
                    "强制击杀所有残留怪物");
            }
        }

        // ====================================================================
        // 5. 经济系统异常检测
        // ====================================================================

        /// <summary>检测经济异常</summary>
        private void CheckEconomyAnomaly()
        {
            if (!BattleEconomyManager.HasInstance) return;

            int gold = BattleEconomyManager.Instance.CurrentGold;

            // 负金币
            if (gold < 0)
            {
                Logger.E("BattleBugFixer", "⚠️ 检测到负金币: {0}，重置为0", gold);
                BattleEconomyManager.Instance.SetGold(0);


                _totalFixesApplied++;

                ReportBug("NegativeGold",
                    $"金币为负数: {gold}",
                    "重置为0");
            }

            // 金币溢出（超过合理范围）
            if (gold > 99999)
            {
                Logger.W("BattleBugFixer", "⚠️ 金币异常偏高: {0}", gold);
                // 不强制修复，只上报
                ReportBug("GoldOverflow",
                    $"金币异常偏高: {gold}",
                    "仅上报不修复");
            }
        }

        // ====================================================================
        // 6. 重复死亡防护（供EnemyBase调用）
        // ====================================================================

        /// <summary>
        /// 检查怪物是否可以受伤（防止死亡后仍被攻击）
        /// </summary>
        public static bool CanEnemyTakeDamage(EnemyBase enemy)
        {
            if (enemy == null) return false;
            if (enemy.IsDead) return false;
            if (!enemy.gameObject.activeInHierarchy) return false;
            if (enemy.CurrentHP <= 0f) return false;
            return true;
        }

        // ====================================================================
        // 事件处理
        // ====================================================================

        private void OnWaveStart(WaveStartEvent evt)
        {
            _currentWaveStartTime = Time.time;
        }

        private void OnWaveComplete(WaveCompleteEvent evt)
        {
            // 波次结束时清理停滞数据
            _lastEnemyPositions.Clear();
            _stuckTimers.Clear();
        }

        // ====================================================================
        // Bug上报
        // ====================================================================

        /// <summary>上报Bug</summary>
        private void ReportBug(string bugType, string description, string autoFix)
        {
            EventBus.Instance.Publish(new BugDetectedEvent
            {
                BugType = bugType,
                Description = description,
                AutoFixApplied = autoFix
            });

            // 后续接入AnalyticsManager上报
            // if (AnalyticsManager.HasInstance)
            //     AnalyticsManager.Instance.TrackEvent("bug_detected", bugType, description);
        }

        // ====================================================================
        // 调试
        // ====================================================================

        public string GetDebugInfo()
        {
            return $"BugFixer: 自动修复={_autoFixEnabled} {FixStats}";
        }
    }
}
