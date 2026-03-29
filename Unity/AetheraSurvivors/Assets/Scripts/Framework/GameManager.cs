
// ============================================================
// 文件名：GameManager.cs
// 功能描述：游戏主入口 — 全局单例，管理游戏生命周期
//          负责串联所有管理器的初始化顺序
// 创建时间：2026-03-25
// 所属模块：Framework
// 对应交互：阶段二 #53
// ============================================================

using UnityEngine;
using AetheraSurvivors.Platform;

namespace AetheraSurvivors.Framework
{

    /// <summary>
    /// 游戏状态枚举
    /// </summary>
    public enum GameState
    {
        /// <summary>未初始化</summary>
        None = 0,

        /// <summary>正在初始化</summary>
        Initializing = 1,

        /// <summary>主界面</summary>
        MainMenu = 2,

        /// <summary>战斗中</summary>
        InBattle = 3,

        /// <summary>暂停</summary>
        Paused = 4,

        /// <summary>Loading中</summary>
        Loading = 5
    }

    /// <summary>
    /// 游戏主入口管理器 — 全局单例
    /// 
    /// 职责：
    /// 1. 游戏生命周期管理（初始化/暂停/恢复/退出）
    /// 2. 串联所有Manager的初始化顺序
    /// 3. 监听微信前后台切换
    /// 4. 游戏状态机
    /// 
    /// 初始化顺序（按架构文档第六章定义）：
    ///   1. Logger → 2. EventBus → 3. TimerManager → 4. ObjectPool
    ///   → 5. ResourceManager → 6. SaveManager → 7. AudioManager
    ///   → 8. UIManager → 9. SceneController
    ///   （平台层和数据层后续阶段接入）
    /// 
    /// 使用方式：
    ///   在BootScene中挂载GameManager到一个GameObject上
    ///   或通过 GameManager.Instance 自动创建
    /// </summary>
    public class GameManager : MonoSingleton<GameManager>
    {
        // ========== 私有字段 ==========

        /// <summary>当前游戏状态</summary>
        private GameState _currentState = GameState.None;

        /// <summary>暂停前的状态（恢复时使用）</summary>
        private GameState _stateBeforePause;

        /// <summary>初始化是否完成</summary>
        private bool _initCompleted;

        // ========== 公共属性 ==========

        /// <summary>当前游戏状态</summary>
        public GameState CurrentState => _currentState;

        /// <summary>游戏是否暂停</summary>
        public bool IsPaused => _currentState == GameState.Paused;

        /// <summary>是否在战斗中</summary>
        public bool IsInBattle => _currentState == GameState.InBattle;

        /// <summary>初始化是否完成</summary>
        public bool IsReady => _initCompleted;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            Debug.Log("========================================");
            Debug.Log("[GameManager] AetheraSurvivors 启动初始化");
            Debug.Log("========================================");

            ChangeState(GameState.Initializing);

            // ====== 按顺序初始化各管理器 ======

            // 1. EventBus（纯C#单例，手动初始化）
            try { EventBus.Instance.Initialize(); } catch (System.Exception e) { Debug.LogError($"[GameManager] EventBus初始化失败: {e.Message}"); }
            Debug.Log("[GameManager] ✅ 1/9 EventBus 初始化完成");

            // 2. TimerManager（MonoSingleton，访问Instance自动创建）
            try { TimerManager.Preload(); } catch (System.Exception e) { Debug.LogError($"[GameManager] TimerManager初始化失败: {e.Message}"); }
            Debug.Log("[GameManager] ✅ 2/9 TimerManager 初始化完成");

            // 3. ObjectPoolManager
            try { ObjectPoolManager.Preload(); } catch (System.Exception e) { Debug.LogError($"[GameManager] ObjectPoolManager初始化失败: {e.Message}"); }
            Debug.Log("[GameManager] ✅ 3/9 ObjectPoolManager 初始化完成");

            // 4. ResourceManager
            try { ResourceManager.Preload(); } catch (System.Exception e) { Debug.LogError($"[GameManager] ResourceManager初始化失败: {e.Message}"); }
            Debug.Log("[GameManager] ✅ 4/9 ResourceManager 初始化完成");

            // 5. SaveManager（纯C#单例）
            try { SaveManager.Instance.Initialize(); } catch (System.Exception e) { Debug.LogError($"[GameManager] SaveManager初始化失败: {e.Message}"); }
            Debug.Log("[GameManager] ✅ 5/9 SaveManager 初始化完成");

            // 6. AudioManager
            try { AudioManager.Preload(); } catch (System.Exception e) { Debug.LogError($"[GameManager] AudioManager初始化失败: {e.Message}"); }
            Debug.Log("[GameManager] ✅ 6/9 AudioManager 初始化完成");

            // 7. UIManager
            try { UIManager.Preload(); } catch (System.Exception e) { Debug.LogError($"[GameManager] UIManager初始化失败: {e.Message}"); }
            Debug.Log("[GameManager] ✅ 7/9 UIManager 初始化完成");

            // 8. SceneController
            try { SceneController.Preload(); } catch (System.Exception e) { Debug.LogError($"[GameManager] SceneController初始化失败: {e.Message}"); }
            Debug.Log("[GameManager] ✅ 8/14 SceneController 初始化完成");

            // ====== 平台层初始化（#62-#66） ======

            // 9. CrashReporter — 最先初始化，尽早捕获异常
            try { CrashReporter.Preload(); } catch (System.Exception e) { Debug.LogError($"[GameManager] CrashReporter初始化失败: {e.Message}"); }
            Debug.Log("[GameManager] ✅ 9/14 CrashReporter 初始化完成");

            // 10. HttpClient — 网络基础层
            try { HttpClient.Preload(); } catch (System.Exception e) { Debug.LogError($"[GameManager] HttpClient初始化失败: {e.Message}"); }
            Debug.Log("[GameManager] ✅ 10/14 HttpClient 初始化完成");

            // 11. WXBridgeExtended — 微信SDK桥接层
            try { WXBridgeExtended.Preload(); } catch (System.Exception e) { Debug.LogError($"[GameManager] WXBridgeExtended初始化失败: {e.Message}"); }
            Debug.Log("[GameManager] ✅ 11/14 WXBridgeExtended 初始化完成");

            // 12. AnalyticsManager — 数据埋点
            try { AnalyticsManager.Instance.Initialize(); } catch (System.Exception e) { Debug.LogError($"[GameManager] AnalyticsManager初始化失败: {e.Message}"); }
            Debug.Log("[GameManager] ✅ 12/14 AnalyticsManager 初始化完成");

            // 13. TimeSync — 服务器时间同步
            try { TimeSync.Instance.Initialize(); } catch (System.Exception e) { Debug.LogError($"[GameManager] TimeSync初始化失败: {e.Message}"); }
            Debug.Log("[GameManager] ✅ 13/14 TimeSync 初始化完成");

            // 14. WXLogin — 微信登录（仅初始化，不自动登录）
            try { WXLogin.Instance.Initialize(); } catch (System.Exception e) { Debug.LogError($"[GameManager] WXLogin初始化失败: {e.Message}"); }
            Debug.Log("[GameManager] ✅ 14/14 WXLogin 初始化完成");

            // 预加载微信系统字体（异步，不阻塞）
            Battle.BattleUI.PreloadWXFont();

            // ====== 初始化完成 ======

            _initCompleted = true;

            // 上报应用启动事件
            AnalyticsManager.Instance.TrackAppLaunch();

            // 启动时间同步（异步，不阻塞）
            TimeSync.Instance.SyncIfNeeded();

            Debug.Log("========================================");
            Debug.Log("[GameManager] ✅ 全部14个Manager初始化完成！");
            Debug.Log($"[GameManager] 耗时: {Time.realtimeSinceStartup:F2}秒");
            Debug.Log("========================================");


            // 切换到主界面状态
            ChangeState(GameState.MainMenu);

            // 发布初始化完成事件
            EventBus.Instance.Publish(new GameStateChangedEvent
            {
                OldState = (int)GameState.Initializing,
                NewState = (int)GameState.MainMenu
            });
        }

        protected override void OnDispose()
        {
            // 按初始化相反的顺序销毁

            // 平台层（按初始化相反顺序）
            if (WXLogin.HasInstance)
                WXLogin.Instance.Dispose();

            if (TimeSync.HasInstance)
                TimeSync.Instance.Dispose();

            if (AnalyticsManager.HasInstance)
            {
                AnalyticsManager.Instance.Flush(); // 退出前上报
                AnalyticsManager.Instance.Dispose();
            }

            // 框架层
            if (SaveManager.HasInstance)
            {
                SaveManager.Instance.FlushAll(); // 退出前保存
                SaveManager.Instance.Dispose();
            }

            if (EventBus.HasInstance)
                EventBus.Instance.Dispose();

            Debug.Log("[GameManager] 所有管理器已销毁");

        }

        // ========== 公共方法：状态管理 ==========

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public void Pause()
        {
            if (_currentState == GameState.Paused) return;

            _stateBeforePause = _currentState;
            ChangeState(GameState.Paused);

            Time.timeScale = 0f;

            // 暂停音频
            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.PauseBGM();
            }

            // 暂停定时器
            if (TimerManager.HasInstance)
            {
                TimerManager.Instance.PauseAll();
            }

            Debug.Log("[GameManager] 游戏已暂停");
        }

        /// <summary>
        /// 恢复游戏
        /// </summary>
        public void Resume()
        {
            if (_currentState != GameState.Paused) return;

            ChangeState(_stateBeforePause);

            Time.timeScale = 1f;

            // 恢复音频
            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.ResumeBGM();
            }

            // 恢复定时器
            if (TimerManager.HasInstance)
            {
                TimerManager.Instance.ResumeAll();
            }

            Debug.Log("[GameManager] 游戏已恢复");
        }

        /// <summary>待进入的章节号（EnterBattle前设置）</summary>
        public static int PendingChapter = 1;
        /// <summary>待进入的关卡号（EnterBattle前设置）</summary>
        public static int PendingLevel = 1;

        /// <summary>
        /// 进入战斗 — 关闭大厅UI，创建BattleSceneSetup启动完整战斗流程
        /// </summary>
        public void EnterBattle(int chapter = 0, int level = 0)
        {
            if (chapter > 0) PendingChapter = chapter;
            if (level > 0) PendingLevel = level;

            Debug.Log($"[GameManager] 从大厅进入战斗: 第{PendingChapter}章 关卡{PendingLevel}");

            ChangeState(GameState.InBattle);

            // 1. 关闭所有大厅UI面板
            if (UIManager.HasInstance)
            {
                UIManager.Instance.CloseAll();
            }

            // 2. 创建BattleSceneSetup（它会自动初始化所有战斗子系统并启动战斗）
            var existing = FindObjectOfType<AetheraSurvivors.Battle.BattleSceneSetup>();
            if (existing == null)
            {
                var battleSetup = new GameObject("[BattleSceneSetup]");
                battleSetup.AddComponent<AetheraSurvivors.Battle.BattleSceneSetup>();
                Debug.Log("[GameManager] ✅ BattleSceneSetup 已创建，战斗即将启动");
            }
            else
            {
                Debug.Log("[GameManager] BattleSceneSetup 已存在，跳过创建");
            }
        }


        /// <summary>
        /// 退出战斗，返回主界面
        /// </summary>
        public void ExitBattle()
        {
            Debug.Log("[GameManager] 退出战斗，返回主界面...");

            // 确保时间恢复正常（战斗中可能被暂停）
            Time.timeScale = 1f;

            // 1. 调用BattleManager清理战斗数据

            if (AetheraSurvivors.Battle.BattleManager.HasInstance)
            {
                AetheraSurvivors.Battle.BattleManager.Instance.ExitBattle();
            }

            // 2. 销毁BattleSceneSetup（它创建了所有战斗子系统）
            var battleSetup = FindObjectOfType<AetheraSurvivors.Battle.BattleSceneSetup>();
            if (battleSetup != null)
            {
                Destroy(battleSetup.gameObject);
                Debug.Log("[GameManager] BattleSceneSetup 已销毁");
            }

            // 3. 销毁BattleUI
            var battleUI = FindObjectOfType<AetheraSurvivors.Battle.BattleUI>();
            if (battleUI != null)
            {
                Destroy(battleUI.gameObject);
                Debug.Log("[GameManager] BattleUI 已销毁");
            }

            // 4. 销毁所有战斗MonoSingleton实例（它们由BattleSceneSetup创建）
            DestroyBattleSingletons();

            // 5. 切换状态到主界面
            ChangeState(GameState.MainMenu);

            // 6. 重新打开大厅UI
            if (UIManager.HasInstance)
            {
                UIManager.Instance.Open<AetheraSurvivors.MetaGame.MainMenuUI>();
                Debug.Log("[GameManager] MainMenuUI 已重新打开");
            }

            Debug.Log("[GameManager] ✅ 已返回主界面");
        }

        /// <summary>
        /// 销毁战斗相关的MonoSingleton实例
        /// </summary>
        private void DestroyBattleSingletons()
        {
            // 地图系统
            if (AetheraSurvivors.Battle.Map.GridSystem.HasInstance)
                Destroy(AetheraSurvivors.Battle.Map.GridSystem.Instance.gameObject);
            if (AetheraSurvivors.Battle.Map.MapRenderer.HasInstance)
                Destroy(AetheraSurvivors.Battle.Map.MapRenderer.Instance.gameObject);
            if (AetheraSurvivors.Battle.Map.PathVisualizer.HasInstance)
                Destroy(AetheraSurvivors.Battle.Map.PathVisualizer.Instance.gameObject);

            // 战斗核心
            if (AetheraSurvivors.Battle.BattleManager.HasInstance)
                Destroy(AetheraSurvivors.Battle.BattleManager.Instance.gameObject);
            if (AetheraSurvivors.Battle.BattleEconomyManager.HasInstance)
                Destroy(AetheraSurvivors.Battle.BattleEconomyManager.Instance.gameObject);
            if (AetheraSurvivors.Battle.BaseHealth.HasInstance)
                Destroy(AetheraSurvivors.Battle.BaseHealth.Instance.gameObject);
            if (AetheraSurvivors.Battle.BattleInputHandler.HasInstance)
                Destroy(AetheraSurvivors.Battle.BattleInputHandler.Instance.gameObject);

            // 塔 & 怪物 & 波次
            if (AetheraSurvivors.Battle.Tower.TowerManager.HasInstance)
                Destroy(AetheraSurvivors.Battle.Tower.TowerManager.Instance.gameObject);
            if (AetheraSurvivors.Battle.Enemy.EnemySpawner.HasInstance)
                Destroy(AetheraSurvivors.Battle.Enemy.EnemySpawner.Instance.gameObject);
            if (AetheraSurvivors.Battle.Wave.WaveManager.HasInstance)
                Destroy(AetheraSurvivors.Battle.Wave.WaveManager.Instance.gameObject);

            // 投射物 & 特效
            if (AetheraSurvivors.Battle.Projectile.ProjectileManager.HasInstance)
                Destroy(AetheraSurvivors.Battle.Projectile.ProjectileManager.Instance.gameObject);
            if (AetheraSurvivors.Battle.Projectile.HitEffectSystem.HasInstance)
                Destroy(AetheraSurvivors.Battle.Projectile.HitEffectSystem.Instance.gameObject);

            if (AetheraSurvivors.Battle.Visual.TowerAttackVFX.HasInstance)
                Destroy(AetheraSurvivors.Battle.Visual.TowerAttackVFX.Instance.gameObject);
            if (AetheraSurvivors.Battle.Visual.EnemyVisualManager.HasInstance)
                Destroy(AetheraSurvivors.Battle.Visual.EnemyVisualManager.Instance.gameObject);
            if (AetheraSurvivors.Battle.Visual.DamagePopupManager.HasInstance)
                Destroy(AetheraSurvivors.Battle.Visual.DamagePopupManager.Instance.gameObject);
            if (AetheraSurvivors.Battle.Visual.BattleFieldVFX.HasInstance)
                Destroy(AetheraSurvivors.Battle.Visual.BattleFieldVFX.Instance.gameObject);
            if (AetheraSurvivors.Battle.Visual.ParticleVFXSystem.HasInstance)
                Destroy(AetheraSurvivors.Battle.Visual.ParticleVFXSystem.Instance.gameObject);

            // 打磨 & 性能系统
            if (AetheraSurvivors.Battle.Polish.BattleFeelSystem.HasInstance)
                Destroy(AetheraSurvivors.Battle.Polish.BattleFeelSystem.Instance.gameObject);
            if (AetheraSurvivors.Battle.Polish.BattleAudioFeedback.HasInstance)
                Destroy(AetheraSurvivors.Battle.Polish.BattleAudioFeedback.Instance.gameObject);
            if (AetheraSurvivors.Battle.Polish.BattleBugFixer.HasInstance)
                Destroy(AetheraSurvivors.Battle.Polish.BattleBugFixer.Instance.gameObject);
            if (AetheraSurvivors.Battle.Polish.BattleBalanceAdjuster.HasInstance)
                Destroy(AetheraSurvivors.Battle.Polish.BattleBalanceAdjuster.Instance.gameObject);
            if (AetheraSurvivors.Battle.Performance.SpatialPartition.HasInstance)
                Destroy(AetheraSurvivors.Battle.Performance.SpatialPartition.Instance.gameObject);
            if (AetheraSurvivors.Battle.Performance.BattlePerformanceOptimizer.HasInstance)
                Destroy(AetheraSurvivors.Battle.Performance.BattlePerformanceOptimizer.Instance.gameObject);

            // 摄像机上的组件（不能销毁gameObject，只移除组件）
            if (AetheraSurvivors.Battle.BattleCamera.HasInstance)
                Destroy(AetheraSurvivors.Battle.BattleCamera.Instance);
            if (AetheraSurvivors.Battle.Visual.VisualPolishSystem.HasInstance)
                Destroy(AetheraSurvivors.Battle.Visual.VisualPolishSystem.Instance);

            // 纯C#单例重置
            if (AetheraSurvivors.Battle.Rune.RuneSystem.HasInstance)
                AetheraSurvivors.Battle.Rune.RuneSystem.Instance.Dispose();
            if (AetheraSurvivors.Battle.Map.Pathfinding.HasInstance)
                AetheraSurvivors.Battle.Map.Pathfinding.Instance.Dispose();


            Debug.Log("[GameManager] 战斗MonoSingleton实例已全部销毁");

        }


        /// <summary>
        /// 退出游戏
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("[GameManager] 正在退出游戏...");

            // 保存所有数据
            if (SaveManager.HasInstance)
            {
                SaveManager.Instance.FlushAll();
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ========== 公共方法：状态切换 ==========

        /// <summary>
        /// 切换游戏状态
        /// </summary>
        public void ChangeState(GameState newState)
        {
            if (_currentState == newState) return;

            var oldState = _currentState;
            _currentState = newState;

            Debug.Log($"[GameManager] 状态切换: {oldState} → {newState}");

            // 发布状态变化事件
            if (EventBus.HasInstance && EventBus.Instance.IsInitialized)
            {
                EventBus.Instance.Publish(new GameStateChangedEvent
                {
                    OldState = (int)oldState,
                    NewState = (int)newState
                });
            }
        }

        // ========== 前后台切换 ==========

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!_initCompleted) return;

            Debug.Log($"[GameManager] 应用焦点变化: {(hasFocus ? "获得焦点" : "失去焦点")}");

            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new AppFocusEvent { IsForeground = hasFocus });
            }

            if (!hasFocus)
            {
                // 切到后台：保存数据 + 上报埋点
                if (SaveManager.HasInstance)
                {
                    SaveManager.Instance.FlushAll();
                }
                if (AnalyticsManager.HasInstance)
                {
                    AnalyticsManager.Instance.TrackAppHide();
                    AnalyticsManager.Instance.Flush();
                }
            }
            else
            {
                // 回到前台：上报事件 + 同步时间
                if (AnalyticsManager.HasInstance)
                {
                    AnalyticsManager.Instance.TrackAppShow();
                }
                if (TimeSync.HasInstance)
                {
                    TimeSync.Instance.SyncIfNeeded();
                }
            }

        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!_initCompleted) return;

            // 微信小游戏的onShow/onHide通过此回调触发
            if (pauseStatus)
            {
                // 切到后台
                Debug.Log("[GameManager] 应用进入后台");
                if (SaveManager.HasInstance)
                {
                    SaveManager.Instance.FlushAll();
                }
            }
            else
            {
                // 回到前台
                Debug.Log("[GameManager] 应用回到前台");
            }
        }
    }
}
