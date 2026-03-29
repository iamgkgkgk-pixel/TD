// ============================================================
// 文件名：BattleManager.cs
// 功能描述：战斗主控制器 — 串联完整战斗流程
//          加载关卡→准备→波次循环→词条选择→结算
// 创建时间：2026-03-25
// 所属模块：Battle
// 对应交互：阶段三 #146
// ============================================================

using System.Collections;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Map;
using AetheraSurvivors.Battle.Tower;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Wave;
using AetheraSurvivors.Battle.Rune;
using Logger = AetheraSurvivors.Framework.Logger;


namespace AetheraSurvivors.Battle
{
    // ====================================================================
    // 战斗状态枚举
    // ====================================================================

    /// <summary>战斗状态</summary>
    public enum BattleState
    {
        /// <summary>未开始</summary>
        None,
        /// <summary>加载关卡</summary>
        Loading,
        /// <summary>准备阶段（放塔）</summary>
        Preparing,
        /// <summary>战斗进行中</summary>
        Fighting,
        /// <summary>词条选择（波次间）</summary>
        RuneSelection,
        /// <summary>战斗胜利</summary>
        Victory,
        /// <summary>战斗失败</summary>
        Defeat,
        /// <summary>暂停</summary>
        Paused
    }

    // ====================================================================
    // 战斗事件
    // ====================================================================

    /// <summary>战斗状态变化事件</summary>
    public struct BattleStateChangedEvent : IEvent
    {
        public BattleState OldState;
        public BattleState NewState;
    }

    /// <summary>战斗结果事件</summary>
    public struct BattleResultEvent : IEvent
    {
        public bool IsVictory;
        public int WavesCleared;
        public int TotalWaves;
        public int GoldEarned;
        public int RunesSelected;
        public float Duration;
        public int BaseHPRemaining;
    }

    // ====================================================================
    // BattleManager 核心类
    // ====================================================================

    /// <summary>
    /// 战斗主控制器 — 管理战斗完整流程的状态机
    /// 
    /// 流程：
    /// Loading → Preparing → [Fighting ↔ RuneSelection]循环 → Victory/Defeat
    /// </summary>
    public class BattleManager : MonoSingleton<BattleManager>
    {
        // ========== 运行时数据 ==========

        /// <summary>当前战斗状态</summary>
        private BattleState _currentState = BattleState.None;

        /// <summary>战斗前的状态（暂停恢复用）</summary>
        private BattleState _stateBeforePause;

        /// <summary>战斗计时器</summary>
        private float _battleTimer = 0f;

        /// <summary>当前关卡ID</summary>
        private string _currentLevelId;

        /// <summary>当前关卡地图数据</summary>
        private LevelMapData _currentMapData;

        /// <summary>当前波次数据</summary>
        private LevelWaveData _currentWaveData;

        /// <summary>当前章节</summary>
        private int _currentChapter = 1;

        /// <summary>当前关卡</summary>
        private int _currentLevel = 1;

        /// <summary>本局击杀数</summary>
        private int _killCount = 0;

        /// <summary>本局建塔数</summary>
        private int _towerBuiltCount = 0;

        // ========== 公共属性 ==========

        /// <summary>当前战斗状态</summary>
        public BattleState CurrentState => _currentState;

        /// <summary>战斗时长（秒）</summary>
        public float BattleDuration => _battleTimer;

        /// <summary>是否正在战斗</summary>
        public bool IsInBattle => _currentState == BattleState.Fighting
                                || _currentState == BattleState.RuneSelection
                                || _currentState == BattleState.Preparing;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            // 订阅关键事件
            EventBus.Instance.Subscribe<BaseDestroyedEvent>(OnBaseDestroyed);
            EventBus.Instance.Subscribe<AllWavesClearedEvent>(OnAllWavesCleared);
            EventBus.Instance.Subscribe<WaveCompleteEvent>(OnWaveComplete);
            EventBus.Instance.Subscribe<EnemyDeathEvent>(OnEnemyKilled);
            EventBus.Instance.Subscribe<TowerUpgradedEvent>(OnTowerBuilt);

            Logger.I("BattleManager", "战斗管理器初始化");
        }

        protected override void OnDispose()
        {
            EventBus.Instance.Unsubscribe<BaseDestroyedEvent>(OnBaseDestroyed);
            EventBus.Instance.Unsubscribe<AllWavesClearedEvent>(OnAllWavesCleared);
            EventBus.Instance.Unsubscribe<WaveCompleteEvent>(OnWaveComplete);
            EventBus.Instance.Unsubscribe<EnemyDeathEvent>(OnEnemyKilled);
            EventBus.Instance.Unsubscribe<TowerUpgradedEvent>(OnTowerBuilt);
        }

        private void Update()
        {
            if (_currentState == BattleState.Fighting || _currentState == BattleState.Preparing)
            {
                _battleTimer += Time.deltaTime;
            }
        }

        // ========== 核心方法：战斗流程 ==========

        /// <summary>
        /// 开始战斗（加载关卡并进入准备阶段）
        /// </summary>
        /// <param name="levelId">关卡ID</param>
        /// <param name="mapData">地图数据（null则使用默认测试数据）</param>
        /// <param name="waveData">波次数据（null则使用默认测试数据）</param>
        public void StartBattle(string levelId = "test", LevelMapData mapData = null, LevelWaveData waveData = null)
        {
            _currentLevelId = levelId;
            _battleTimer = 0f;
            _killCount = 0;
            _towerBuiltCount = 0;

            // 从PlayerData或GameManager.Pending读取当前章节/关卡
            _currentChapter = Framework.GameManager.PendingChapter;
            _currentLevel = Framework.GameManager.PendingLevel;

            if (AetheraSurvivors.Data.PlayerDataManager.HasInstance && _currentChapter <= 0)
            {
                var data = AetheraSurvivors.Data.PlayerDataManager.Instance.Data;
                _currentChapter = data.UnlockedChapter;
                _currentLevel = data.UnlockedLevel;
            }

            // 尝试从JSON配置加载关卡数据
            if (mapData == null || waveData == null)
            {
                var levelConfig = LoadLevelConfig(_currentChapter, _currentLevel);
                if (levelConfig != null)
                {
                    if (mapData == null) mapData = levelConfig.ToMapData();
                    if (waveData == null) waveData = levelConfig.ToWaveData();
                    _currentLevelId = levelConfig.levelId;
                    Logger.I("BattleManager", "从JSON配置加载关卡: {0}", _currentLevelId);
                }
            }

            ChangeState(BattleState.Loading);
            StartCoroutine(LoadBattleCoroutine(mapData, waveData));
        }

        /// <summary>
        /// 从 Resources/Configs/Levels/level_config.json 加载关卡配置
        /// </summary>
        private Data.LevelConfig LoadLevelConfig(int chapter, int level)
        {
            // 先检查缓存
            if (_cachedLevelTable == null)
            {
                var textAsset = Resources.Load<TextAsset>("Configs/Levels/level_config");
                if (textAsset != null)
                {
                    _cachedLevelTable = JsonUtility.FromJson<Data.LevelConfigTable>(textAsset.text);
                    Logger.I("BattleManager", "关卡配置表加载成功: {0}个关卡",
                        _cachedLevelTable?.levels?.Count ?? 0);
                }
                else
                {
                    Logger.W("BattleManager", "关卡配置表不存在: Configs/Levels/level_config，使用测试数据");
                    return null;
                }
            }

            return _cachedLevelTable?.Find(chapter, level);
        }

        /// <summary>关卡配置表缓存</summary>
        private static Data.LevelConfigTable _cachedLevelTable;

        /// <summary>
        /// 玩家点击"开始波次"
        /// </summary>
        public void PlayerStartWave()
        {
            if (_currentState == BattleState.Preparing || _currentState == BattleState.RuneSelection)
            {
                ChangeState(BattleState.Fighting);

                if (WaveManager.HasInstance)
                {
                    WaveManager.Instance.StartNextWave();
                }
            }
        }

        /// <summary>
        /// 选择词条后继续战斗
        /// </summary>
        public void OnRuneSelected(int runeId)
        {
            if (_currentState != BattleState.RuneSelection) return;

            RuneSystem.Instance.SelectRune(runeId);

            // 返回准备状态（让玩家可以放塔后再点"开始"按钮）
            // 或直接进入战斗并自动开始下一波
            ChangeState(BattleState.Fighting);

            // 直接开始下一波（WaveManager 的协程已 yield break，需重新调用 StartNextWave）
            if (WaveManager.HasInstance)
            {
                WaveManager.Instance.StartNextWave();
            }

            Logger.I("BattleManager", "词条选择完成，自动开始下一波");
        }


        /// <summary>
        /// 暂停
        /// </summary>
        public void PauseBattle()
        {
            if (_currentState == BattleState.Paused) return;

            _stateBeforePause = _currentState;
            ChangeState(BattleState.Paused);
            Time.timeScale = 0f;
        }

        /// <summary>
        /// 恢复
        /// </summary>
        public void ResumeBattle()
        {
            if (_currentState != BattleState.Paused) return;

            ChangeState(_stateBeforePause);
            Time.timeScale = BattleInputHandler.HasInstance ? BattleInputHandler.Instance.GameSpeed : 1f;
        }

        /// <summary>
        /// 退出战斗（返回主界面）
        /// </summary>
        public void ExitBattle()
        {
            CleanupBattle();
            ChangeState(BattleState.None);
            Time.timeScale = 1f;
        }

        /// <summary>
        /// 重新开始当前关卡
        /// </summary>
        public void RestartBattle()
        {
            CleanupBattle();
            StartBattle(_currentLevelId, _currentMapData, _currentWaveData);
        }

        // ========== 加载流程 ==========

        private IEnumerator LoadBattleCoroutine(LevelMapData mapData, LevelWaveData waveData)
        {
            Logger.I("BattleManager", "开始加载关卡: {0}", _currentLevelId);

            // 1. 加载地图
            _currentMapData = mapData ?? CreateTestMapData();
            if (GridSystem.HasInstance)
            {
                GridSystem.Instance.LoadMap(_currentMapData);
            }

            yield return null;

            // 2. 初始化寻路
            if (Pathfinding.HasInstance)
            {
                Pathfinding.Instance.InitForMap(_currentMapData.width, _currentMapData.height);
            }

            // 3. 渲染地图
            if (MapRenderer.HasInstance)
            {
                MapRenderer.Instance.RenderMap();
            }

            // 4. 显示路径
            if (PathVisualizer.HasInstance)
            {
                PathVisualizer.Instance.ShowMainPathFromGrid();
            }

            yield return null;

            // 5. 加载波次数据
            _currentWaveData = waveData ?? WaveManager.CreateTestWaveData();
            if (WaveManager.HasInstance)
            {
                WaveManager.Instance.LoadWaveData(_currentWaveData);
            }

            // 6. 初始化经济
            if (BattleEconomyManager.HasInstance)
            {
                BattleEconomyManager.Instance.InitEconomy(_currentWaveData.startGold);
            }

            // 7. 初始化基地血量
            if (BaseHealth.HasInstance)
            {
                BaseHealth.Instance.InitHealth(_currentWaveData.baseHP);
            }

            // 8. 重置词条系统
            RuneSystem.Instance.ResetForNewRun();

            // 9. 重置金矿计数
            GoldMine.ResetCount();

            // 10. 播放战斗BGM
            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.PlayBGM("Audio/BGM/bgm_battle", 1.0f);
            }

            Logger.I("BattleManager", "✅ 关卡加载完成，进入准备阶段");

            // 进入准备阶段
            ChangeState(BattleState.Preparing);
        }

        // ========== 事件处理 ==========

        /// <summary>基地被摧毁 → 战斗失败</summary>
        private void OnBaseDestroyed(BaseDestroyedEvent evt)
        {
            ChangeState(BattleState.Defeat);
            OnBattleEnd(false);
        }

        /// <summary>所有波次清除 → 战斗胜利</summary>
        private void OnAllWavesCleared(AllWavesClearedEvent evt)
        {
            // 等待所有怪物被清除
            StartCoroutine(CheckVictoryCoroutine());
        }

        private IEnumerator CheckVictoryCoroutine()
        {
            // 等到场上没有怪物
            while (EnemySpawner.HasInstance && EnemySpawner.Instance.ActiveEnemyCount > 0)
            {
                yield return new WaitForSeconds(0.5f);
            }

            if (!BaseHealth.HasInstance || !BaseHealth.Instance.IsDestroyed)
            {
                ChangeState(BattleState.Victory);
                OnBattleEnd(true);
            }
        }

        /// <summary>波次完成 → 弹出词条选择</summary>
        private void OnWaveComplete(WaveCompleteEvent evt)
        {
            // 每隔2波提供一次词条选择
            if (evt.WaveIndex % 2 == 0)
            {
                ChangeState(BattleState.RuneSelection);
                RuneSystem.Instance.GenerateOptions(3);
            }
        }

        /// <summary>怪物被击杀 → 统计击杀数</summary>
        private void OnEnemyKilled(EnemyDeathEvent evt)
        {
            _killCount++;
        }

        /// <summary>塔被建造/升级 → 统计建塔数</summary>
        private void OnTowerBuilt(TowerUpgradedEvent evt)
        {
            if (evt.NewLevel == 1) _towerBuiltCount++;
        }

        // ========== 战斗结束 ==========

        private void OnBattleEnd(bool isVictory)
        {
            Time.timeScale = 1f;

            // 停止战斗BGM
            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.StopBGM(0.5f);
            }

            int wavesCleared = WaveManager.HasInstance ? WaveManager.Instance.CurrentWaveNumber : 0;
            int totalWaves = WaveManager.HasInstance ? WaveManager.Instance.TotalWaves : 0;

            EventBus.Instance.Publish(new BattleResultEvent
            {
                IsVictory = isVictory,
                WavesCleared = wavesCleared,
                TotalWaves = totalWaves,
                GoldEarned = BattleEconomyManager.HasInstance ? BattleEconomyManager.Instance.TotalIncome : 0,
                RunesSelected = RuneSystem.Instance.SelectedCount,
                Duration = _battleTimer,
                BaseHPRemaining = BaseHealth.HasInstance ? BaseHealth.Instance.CurrentHP : 0
            });

            // 调用元游戏系统处理战斗结算（解锁下一关、发放奖励等）
            if (MetaGame.MetaGameInitializer.HasInstance)
            {
                int stars = 0;
                if (isVictory)
                {
                    // 根据基地剩余血量计算星级
                    float hpPercent = BaseHealth.HasInstance ? BaseHealth.Instance.HPPercent : 0f;
                    if (hpPercent >= 0.8f) stars = 3;
                    else if (hpPercent >= 0.4f) stars = 2;
                    else stars = 1;
                }

                MetaGame.MetaGameInitializer.Instance.ProcessBattleResult(new MetaGame.BattleResultData
                {
                    Chapter = _currentChapter,
                    Level = _currentLevel,
                    IsVictory = isVictory,
                    Stars = stars,
                    ClearTime = _battleTimer,
                    KillCount = _killCount,
                    TowerBuiltCount = _towerBuiltCount,
                    HighestDPS = 0f,
                    Difficulty = 0,
                    HeroId = ""
                });
            }

            Logger.I("BattleManager", "战斗结束: {0}, 波次{1}/{2}, 用时{3:F1}秒",
                isVictory ? "✅胜利" : "❌失败", wavesCleared, totalWaves, _battleTimer);
        }

        // ========== 状态切换 ==========

        private void ChangeState(BattleState newState)
        {
            var oldState = _currentState;
            _currentState = newState;

            EventBus.Instance.Publish(new BattleStateChangedEvent
            {
                OldState = oldState,
                NewState = newState
            });

            Logger.D("BattleManager", "状态切换: {0} → {1}", oldState, newState);
        }

        // ========== 清理 ==========

        private void CleanupBattle()
        {
            StopAllCoroutines();

            if (TowerManager.HasInstance) TowerManager.Instance.ClearAllTowers();
            if (EnemySpawner.HasInstance) EnemySpawner.Instance.ClearAllEnemies();
            if (WaveManager.HasInstance) WaveManager.Instance.Reset();
            if (GridSystem.HasInstance) GridSystem.Instance.UnloadMap();
            if (PathVisualizer.HasInstance) PathVisualizer.Instance.ClearAll();
            if (BattleEconomyManager.HasInstance) BattleEconomyManager.Instance.Reset();
        }

        // ========== 测试数据 ==========

        /// <summary>创建测试地图数据</summary>
        private LevelMapData CreateTestMapData()
        {
            int w = 12, h = 8;
            var gridData = new int[w * h];

            // 填充空地
            for (int i = 0; i < gridData.Length; i++) gridData[i] = 0;

            // 创建路径（从左到右的Z形路径）
            var pathPoints = new System.Collections.Generic.List<Vector2Int>();

            // 行1：y=1 从 x=0 到 x=10
            for (int x = 0; x <= 10; x++)
            {
                gridData[1 * w + x] = (int)GridCellType.Path;
                pathPoints.Add(new Vector2Int(x, 1));
            }
            // 连接：x=10 从 y=1 到 y=4
            for (int y = 2; y <= 4; y++)
            {
                gridData[y * w + 10] = (int)GridCellType.Path;
                pathPoints.Add(new Vector2Int(10, y));
            }
            // 行2：y=4 从 x=10 到 x=1
            for (int x = 9; x >= 1; x--)
            {
                gridData[4 * w + x] = (int)GridCellType.Path;
                pathPoints.Add(new Vector2Int(x, 4));
            }
            // 连接：x=1 从 y=4 到 y=6
            for (int y = 5; y <= 6; y++)
            {
                gridData[y * w + 1] = (int)GridCellType.Path;
                pathPoints.Add(new Vector2Int(1, y));
            }
            // 行3：y=6 从 x=1 到 x=11
            for (int x = 2; x <= 11; x++)
            {
                gridData[6 * w + x] = (int)GridCellType.Path;
                pathPoints.Add(new Vector2Int(x, 6));
            }

            // 出生点和基地
            gridData[1 * w + 0] = (int)GridCellType.SpawnPoint;
            gridData[6 * w + 11] = (int)GridCellType.BasePoint;

            // 塔位（路径两侧）
            int[] towerX = { 2, 4, 6, 8, 3, 5, 7, 9, 3, 5, 7, 9 };
            int[] towerY = { 0, 0, 0, 0, 2, 2, 2, 2, 5, 5, 5, 5 };
            for (int i = 0; i < towerX.Length; i++)
            {
                int idx = towerY[i] * w + towerX[i];
                if (idx >= 0 && idx < gridData.Length && gridData[idx] == 0)
                {
                    gridData[idx] = (int)GridCellType.TowerSlot;
                }
            }

            return new LevelMapData
            {
                levelId = "test_level",
                chapter = 1,
                levelIndex = 1,
                width = w,
                height = h,
                gridData = gridData,
                pathPoints = pathPoints,
                spawnPoint = new Vector2Int(0, 1),
                basePoint = new Vector2Int(11, 6),
                description = "测试关卡"
            };
        }
    }
}
