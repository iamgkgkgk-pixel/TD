// ============================================================
// 文件名：WaveManager.cs
// 功能描述：波次管理器 — 管理战斗波次流程
//          配置化波次生成、波次间倒计时、精英/Boss波次特殊处理
// 创建时间：2026-03-25
// 所属模块：Battle/Wave
// 对应交互：阶段三 #140-#141
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Map;
using Logger = AetheraSurvivors.Framework.Logger;


namespace AetheraSurvivors.Battle.Wave
{
    // ====================================================================
    // 波次配置数据
    // ====================================================================

    /// <summary>单批次怪物生成配置</summary>
    [Serializable]
    public class WaveGroup
    {
        /// <summary>怪物类型</summary>
        public EnemyType enemyType = EnemyType.Infantry;
        /// <summary>数量</summary>
        public int count = 5;
        /// <summary>生成间隔（秒）</summary>
        public float spawnInterval = 0.8f;
        /// <summary>HP倍率</summary>
        public float hpMultiplier = 1f;
        /// <summary>该组开始的延迟（秒，相对本波次开始）</summary>
        public float startDelay = 0f;
    }

    /// <summary>单波次配置</summary>
    [Serializable]
    public class WaveConfig
    {
        /// <summary>波次编号（从1开始）</summary>
        public int waveIndex = 1;
        /// <summary>怪物组列表</summary>
        public List<WaveGroup> groups = new List<WaveGroup>();
        /// <summary>本波次结束后的等待时间（秒）</summary>
        public float intervalAfterWave = 10f;
        /// <summary>是否为精英波（UI特殊提示）</summary>
        public bool isEliteWave = false;
        /// <summary>是否为Boss波</summary>
        public bool isBossWave = false;
        /// <summary>波次描述（预告文字）</summary>
        public string description = "";
    }

    /// <summary>关卡波次总配置</summary>
    [Serializable]
    public class LevelWaveData
    {
        /// <summary>关卡ID</summary>
        public string levelId;
        /// <summary>所有波次</summary>
        public List<WaveConfig> waves = new List<WaveConfig>();
        /// <summary>初始金币</summary>
        public int startGold = 200;
        /// <summary>基地生命值</summary>
        public int baseHP = 20;
    }

    // ====================================================================
    // 波次事件
    // ====================================================================

    /// <summary>波次开始事件</summary>
    public struct WaveStartEvent : IEvent
    {
        public int WaveIndex;
        public int TotalWaves;
        public bool IsElite;
        public bool IsBoss;
    }

    /// <summary>波次完成事件（该波所有怪物被消灭）</summary>
    public struct WaveCompleteEvent : IEvent
    {
        public int WaveIndex;
        public int TotalWaves;
    }

    /// <summary>所有波次完成事件</summary>
    public struct AllWavesClearedEvent : IEvent
    {
        public int TotalWaves;
    }

    /// <summary>波次倒计时事件</summary>
    public struct WaveCountdownEvent : IEvent
    {
        public float RemainingTime;
        public int NextWaveIndex;
    }

    // ====================================================================
    // WaveManager 核心类
    // ====================================================================

    /// <summary>
    /// 波次管理器
    /// 
    /// 职责：
    /// 1. 从配置加载波次数据
    /// 2. 按顺序执行波次（生成怪物组）
    /// 3. 波次间倒计时/手动跳过
    /// 4. 追踪当前波次内活跃怪物数
    /// 5. 全波次通关判定
    /// </summary>
    public class WaveManager : MonoSingleton<WaveManager>
    {
        // ========== 运行时数据 ==========

        /// <summary>当前关卡波次数据</summary>
        private LevelWaveData _levelData;

        /// <summary>当前波次索引（0-based）</summary>
        private int _currentWaveIndex = -1;

        /// <summary>当前波次内生成的怪物总数</summary>
        private int _currentWaveSpawnedCount = 0;

        /// <summary>当前波次内已消灭的怪物数</summary>
        private int _currentWaveKilledCount = 0;

        /// <summary>波次间倒计时</summary>
        private float _waveCountdown = 0f;

        /// <summary>是否正在执行波次</summary>
        private bool _isWaveActive = false;

        /// <summary>是否所有波次已完成</summary>
        private bool _allWavesCleared = false;

        /// <summary>是否暂停</summary>
        private bool _isPaused = false;

        /// <summary>生成协程是否已完毕（所有组都完成生成）</summary>
        private bool _spawnComplete = false;

        /// <summary>备用检测计时器</summary>
        private float _fallbackCheckTimer = 0f;

        /// <summary>备用检测间隔（秒）</summary>
        private const float FallbackCheckInterval = 1.0f;

        // ========== 公共属性 ==========

        /// <summary>当前波次编号（1-based，显示用）</summary>
        public int CurrentWaveNumber => _currentWaveIndex + 1;

        /// <summary>总波次数</summary>
        public int TotalWaves => _levelData?.waves?.Count ?? 0;

        /// <summary>是否正在执行波次</summary>
        public bool IsWaveActive => _isWaveActive;

        /// <summary>是否全部完成</summary>
        public bool AllWavesCleared => _allWavesCleared;

        /// <summary>波次间倒计时</summary>
        public float WaveCountdown => _waveCountdown;


        /// <summary>当前波次配置</summary>
        public WaveConfig CurrentWaveConfig
        {
            get
            {
                if (_levelData?.waves == null || _currentWaveIndex < 0 || _currentWaveIndex >= _levelData.waves.Count)
                    return null;
                return _levelData.waves[_currentWaveIndex];
            }
        }

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            EventBus.Instance.Subscribe<EnemyDeathEvent>(OnEnemyDied);
            EventBus.Instance.Subscribe<EnemyReachedBaseEvent>(OnEnemyReachedBase);

            Logger.I("WaveManager", "波次管理器初始化 订阅EnemyDeathEvent和EnemyReachedBaseEvent");
        }

        /// <summary>
        /// 备用检查：每秒检测一次，如果波次活跃但场上已无怪物且生成已完毕，
        /// 则强制补齐计数器（防止事件丢失导致波次卡死）
        /// </summary>
        private void Update()
        {
            if (!_isWaveActive || !_spawnComplete) return;

            _fallbackCheckTimer += Time.deltaTime;
            if (_fallbackCheckTimer < FallbackCheckInterval) return;
            _fallbackCheckTimer = 0f;

            // 如果计数器已满足，不需要备用检查
            if (_currentWaveKilledCount >= _currentWaveSpawnedCount) return;

            // 检查场上是否还有活跃怪物（会先清理null引用）
            int activeCount = 0;
            if (EnemySpawner.HasInstance)
            {
                activeCount = EnemySpawner.Instance.ActiveEnemyCount;
            }

            // 诊断日志：每次备用检测都输出
            Logger.I("WaveManager", "备用检测: killed={0}/{1} 活跃怪物={2} EventBus订阅数(Death={3}, Reached={4})",
                _currentWaveKilledCount, _currentWaveSpawnedCount, activeCount,
                EventBus.Instance.GetListenerCount<EnemyDeathEvent>(),
                EventBus.Instance.GetListenerCount<EnemyReachedBaseEvent>());

            if (activeCount == 0 && _currentWaveKilledCount < _currentWaveSpawnedCount)
            {
                // 场上无怪物但计数不足 — 说明有事件丢失！强制补齐计数器
                int missing = _currentWaveSpawnedCount - _currentWaveKilledCount;
                Logger.E("WaveManager", "⚠️ 检测到事件丢失! killed={0}/{1} 活跃怪物={2}，强制补齐{3}个计数",
                    _currentWaveKilledCount, _currentWaveSpawnedCount, activeCount, missing);
                _currentWaveKilledCount = _currentWaveSpawnedCount;
            }
        }



        protected override void OnDispose()
        {
            EventBus.Instance.Unsubscribe<EnemyDeathEvent>(OnEnemyDied);
            EventBus.Instance.Unsubscribe<EnemyReachedBaseEvent>(OnEnemyReachedBase);

            StopAllCoroutines();
            Logger.I("WaveManager", "波次管理器已销毁");
        }

        // ========== 核心方法 ==========

        /// <summary>
        /// 加载关卡波次数据
        /// </summary>
        public void LoadWaveData(LevelWaveData data)
        {
            _levelData = data;
            _currentWaveIndex = -1;
            _allWavesCleared = false;
            _isWaveActive = false;

            Logger.I("WaveManager", "波次数据加载: {0}波 起始金币={1} 基地HP={2}",
                data.waves.Count, data.startGold, data.baseHP);
        }

        /// <summary>
        /// 开始下一波
        /// </summary>
        public void StartNextWave()
        {
            if (_allWavesCleared)
            {
                Logger.W("WaveManager", "StartNextWave被调用但所有波次已完成");
                return;
            }
            if (_isWaveActive)
            {
                Logger.W("WaveManager", "StartNextWave被调用但当前波次仍在进行中");
                return;
            }

            _currentWaveIndex++;

            if (_currentWaveIndex >= TotalWaves)
            {
                // 所有波次完成
                _allWavesCleared = true;
                EventBus.Instance.Publish(new AllWavesClearedEvent { TotalWaves = TotalWaves });
                Logger.I("WaveManager", "所有波次已完成！");
                return;
            }

            Logger.I("WaveManager", ">>> 启动第{0}波", _currentWaveIndex + 1);
            StartCoroutine(ExecuteWaveCoroutine());
        }


        /// <summary>
        /// 跳过波次间倒计时（手动开始下一波）
        /// </summary>
        public void SkipCountdown()
        {
            _waveCountdown = 0f;
        }

        /// <summary>
        /// 暂停/恢复
        /// </summary>
        public void SetPaused(bool paused)
        {
            _isPaused = paused;
        }

        /// <summary>
        /// 重置（重新开始关卡时）
        /// </summary>
        public void Reset()
        {
            StopAllCoroutines();
            _currentWaveIndex = -1;
            _allWavesCleared = false;
            _isWaveActive = false;
            _waveCountdown = 0f;
        }

        // ========== 波次执行 ==========

        private IEnumerator ExecuteWaveCoroutine()
        {
            if (_levelData == null || _levelData.waves == null || _currentWaveIndex < 0 || _currentWaveIndex >= _levelData.waves.Count)
            {
                Logger.E("WaveManager", "ExecuteWaveCoroutine: 波次数据异常! index={0} totalWaves={1}",
                    _currentWaveIndex, _levelData?.waves?.Count ?? 0);
                yield break;
            }

            var waveConfig = _levelData.waves[_currentWaveIndex];
            _isWaveActive = true;
            _currentWaveSpawnedCount = 0;
            _currentWaveKilledCount = 0;

            Logger.I("WaveManager", "★★★ 第{0}/{1}波协程启动 _isWaveActive={2} {3}{4}",
                _currentWaveIndex + 1, TotalWaves, _isWaveActive,
                waveConfig.isEliteWave ? "[精英]" : "",
                waveConfig.isBossWave ? "[BOSS]" : "");

            // 发布波次开始事件
            EventBus.Instance.Publish(new WaveStartEvent
            {
                WaveIndex = _currentWaveIndex + 1,
                TotalWaves = TotalWaves,
                IsElite = waveConfig.isEliteWave,
                IsBoss = waveConfig.isBossWave
            });

            // 获取路径
            var pathPoints = GetEnemyPath();
            Logger.D("WaveManager", "路径点数量: {0}", pathPoints?.Count ?? 0);

            // 计算本波次总怪物数
            int totalInWave = 0;
            foreach (var group in waveConfig.groups)
            {
                totalInWave += group.count;
            }

            if (totalInWave <= 0)
            {
                Logger.E("WaveManager", "波次{0}怪物总数为0! groups.Count={1}",
                    _currentWaveIndex + 1, waveConfig.groups.Count);
                _isWaveActive = false;
                yield break;
            }

            _currentWaveSpawnedCount = totalInWave;

            Logger.I("WaveManager", "第{0}波预计生成{1}个怪物，启动{2}个生成协程",
                _currentWaveIndex + 1, totalInWave, waveConfig.groups.Count);

            // 重置生成完毕标记和备用检测计时器
            _spawnComplete = false;
            _fallbackCheckTimer = 0f;

            // 记录需要等待完成的生成协程数
            int groupsRemaining = waveConfig.groups.Count;

            // 为每个组启动生成协程
            foreach (var group in waveConfig.groups)
            {
                StartCoroutine(SpawnGroupCoroutineTracked(group, pathPoints, () =>
                {
                    groupsRemaining--;
                    if (groupsRemaining <= 0)
                    {
                        _spawnComplete = true;
                        Logger.I("WaveManager", "第{0}波所有怪物生成完毕，启用备用检测", _currentWaveIndex + 1);
                    }
                }));
            }


            // 等待所有怪物被消灭（增加超时保护）
            float waitStartTime = Time.time;
            float maxWaitTime = 180f; // 最长等待3分钟
            yield return new WaitUntil(() =>
            {
                bool countReached = _currentWaveKilledCount >= _currentWaveSpawnedCount;
                bool timeout = (Time.time - waitStartTime) > maxWaitTime;
                if (timeout && !countReached)
                {
                    Logger.E("WaveManager", "波次{0}等待超时! killed={1}/{2}",
                        _currentWaveIndex + 1, _currentWaveKilledCount, _currentWaveSpawnedCount);
                }
                return countReached || timeout;
            });


            // 波次完成
            _isWaveActive = false;

            Logger.I("WaveManager", "第{0}波完成 killed={1}/{2}",
                _currentWaveIndex + 1, _currentWaveKilledCount, _currentWaveSpawnedCount);

            EventBus.Instance.Publish(new WaveCompleteEvent
            {
                WaveIndex = _currentWaveIndex + 1,
                TotalWaves = TotalWaves
            });

            // 检查是否还有下一波
            if (_currentWaveIndex + 1 < TotalWaves)
            {
                // 检查是否进入了词条选择状态（由BattleManager在OnWaveComplete事件的同步回调中设置）
                // WaveCompleteEvent是同步发布的，此时BattleManager已经处理完毕
                if (BattleManager.HasInstance && BattleManager.Instance.CurrentState == BattleState.RuneSelection)
                {
                    // 词条选择中，等待玩家选择完毕后BattleManager会调用PlayerStartWave触发下一波
                    Logger.I("WaveManager", "词条选择中，等待玩家操作后开始下一波");
                    yield break;
                }

                // 波次间倒计时
                _waveCountdown = waveConfig.intervalAfterWave;
                Logger.I("WaveManager", "▶ 波次间倒计时开始: {0}秒 等待后自动开始第{1}波",
                    _waveCountdown, _currentWaveIndex + 2);

                while (_waveCountdown > 0f)
                {
                    if (!_isPaused)
                    {
                        _waveCountdown -= Time.deltaTime;
                        EventBus.Instance.Publish(new WaveCountdownEvent
                        {
                            RemainingTime = _waveCountdown,
                            NextWaveIndex = _currentWaveIndex + 2
                        });
                    }
                    yield return null;
                }

                Logger.I("WaveManager", "▶ 倒计时结束，自动开始下一波");


                // 自动开始下一波
                StartNextWave();
            }
            else
            {
                // 全部波次完成
                _allWavesCleared = true;
                EventBus.Instance.Publish(new AllWavesClearedEvent { TotalWaves = TotalWaves });
                Logger.I("WaveManager", "✅ 所有{0}波次已完成！", TotalWaves);
            }

        }

        /// <summary>带完成回调的生成协程</summary>
        private IEnumerator SpawnGroupCoroutineTracked(WaveGroup group, List<Vector3> pathPoints, System.Action onComplete)
        {
            // 组的开始延迟
            if (group.startDelay > 0f)
            {
                yield return new WaitForSeconds(group.startDelay);
            }

            Logger.I("WaveManager", "开始生成: {0}x{1} 间隔={2}s",
                group.count, group.enemyType, group.spawnInterval);

            for (int i = 0; i < group.count; i++)
            {
                while (_isPaused) yield return null;

                var enemy = EnemySpawner.Instance.SpawnEnemy(group.enemyType, pathPoints, group.hpMultiplier);
                if (enemy == null)
                {
                    Logger.E("WaveManager", "怪物生成失败! type={0} index={1}/{2}",
                        group.enemyType, i + 1, group.count);
                }

                if (group.spawnInterval > 0f && i < group.count - 1)
                {
                    yield return new WaitForSeconds(group.spawnInterval);
                }
            }

            Logger.I("WaveManager", "生成完毕: {0}x{1}", group.count, group.enemyType);
            onComplete?.Invoke();
        }



        // ========== 事件处理 ==========

        private void OnEnemyDied(EnemyDeathEvent evt)
        {
            _currentWaveKilledCount++;
            Logger.I("WaveManager", "怪物死亡 killed={0}/{1} waveActive={2}",
                _currentWaveKilledCount, _currentWaveSpawnedCount, _isWaveActive);
        }

        private void OnEnemyReachedBase(EnemyReachedBaseEvent evt)
        {
            _currentWaveKilledCount++;
            Logger.I("WaveManager", "怪物到达基地 killed={0}/{1} waveActive={2}",
                _currentWaveKilledCount, _currentWaveSpawnedCount, _isWaveActive);
        }



        // ========== 工具方法 ==========

        /// <summary>获取怪物行进路径（世界坐标）</summary>
        private List<Vector3> GetEnemyPath()
        {
            if (GridSystem.HasInstance && GridSystem.Instance.IsMapLoaded)
            {
                return GridSystem.Instance.GetPathWorldPositions();
            }

            // 默认直线路径（开发阶段）
            return new List<Vector3>
            {
                new Vector3(-5, 0, 0),
                new Vector3(5, 0, 0)
            };
        }

        /// <summary>创建默认测试波次（第1章5关）</summary>
        public static LevelWaveData CreateTestWaveData()
        {
            var data = new LevelWaveData
            {
                levelId = "chapter1_level1",
                startGold = 400,
                baseHP = 50,
                waves = new List<WaveConfig>
                {
                    // 第1波：3个步兵（轻松热身）
                    new WaveConfig
                    {
                        waveIndex = 1, description = "步兵来袭",
                        intervalAfterWave = 15f,

                        groups = new List<WaveGroup>
                        {
                            new WaveGroup { enemyType = EnemyType.Infantry, count = 3, spawnInterval = 1.5f }
                        }
                    },
                    // 第2波：4个步兵 + 1个刺客
                    new WaveConfig
                    {
                        waveIndex = 2, description = "刺客出没",
                        intervalAfterWave = 15f,

                        groups = new List<WaveGroup>
                        {
                            new WaveGroup { enemyType = EnemyType.Infantry, count = 4, spawnInterval = 1.2f },
                            new WaveGroup { enemyType = EnemyType.Assassin, count = 1, spawnInterval = 0.8f, startDelay = 3f }
                        }
                    },
                    // 第3波：2个骑士 + 3个步兵
                    new WaveConfig
                    {
                        waveIndex = 3, description = "重甲骑士",
                        intervalAfterWave = 15f,

                        groups = new List<WaveGroup>
                        {
                            new WaveGroup { enemyType = EnemyType.Knight, count = 2, spawnInterval = 1.5f },
                            new WaveGroup { enemyType = EnemyType.Infantry, count = 3, spawnInterval = 1f, startDelay = 2f }
                        }
                    },
                    // 第4波（精英）：混合小队
                    new WaveConfig
                    {
                        waveIndex = 4, description = "精英部队！",
                        isEliteWave = true,
                        intervalAfterWave = 20f,
                        groups = new List<WaveGroup>
                        {
                            new WaveGroup { enemyType = EnemyType.Knight, count = 2, spawnInterval = 1.2f, hpMultiplier = 1.2f },
                            new WaveGroup { enemyType = EnemyType.Assassin, count = 2, spawnInterval = 0.8f, startDelay = 2f },
                            new WaveGroup { enemyType = EnemyType.Healer, count = 1, spawnInterval = 0f, startDelay = 4f }
                        }
                    },
                    // 第5波（Boss）：Boss + 少量护卫
                    new WaveConfig
                    {
waveIndex = 5, description = "!! BOSS来袭！",

                        isBossWave = true,
                        intervalAfterWave = 0f,
                        groups = new List<WaveGroup>
                        {
                            new WaveGroup { enemyType = EnemyType.Infantry, count = 3, spawnInterval = 0.8f },
                            new WaveGroup { enemyType = EnemyType.BossDragon, count = 1, spawnInterval = 0f, startDelay = 3f }
                        }
                    }
                }
            };


            return data;
        }
    }
}
