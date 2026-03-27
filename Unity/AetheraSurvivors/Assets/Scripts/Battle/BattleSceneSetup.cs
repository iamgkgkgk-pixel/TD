// ============================================================
// 文件名：BattleSceneSetup.cs
// 功能描述：战斗场景入口 — 负责初始化所有战斗子系统并启动战斗
//          挂载到场景中的空GameObject即可自动运行完整一局
// 创建时间：2026-03-25
// 所属模块：Battle
// 对应交互：阶段三 G3-1（核心战斗可玩）
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Map;
using AetheraSurvivors.Battle.Tower;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Wave;
using AetheraSurvivors.Battle.Rune;
using AetheraSurvivors.Battle.Visual;
using AetheraSurvivors.Battle.Projectile;
using AetheraSurvivors.Battle.Polish;
using AetheraSurvivors.Battle.Performance;
using Logger = AetheraSurvivors.Framework.Logger;


namespace AetheraSurvivors.Battle
{
    /// <summary>
    /// 战斗场景入口 — 自动初始化所有子系统并启动战斗
    /// 
    /// 使用方式：
    ///   1. 在场景中创建空GameObject命名为 "BattleSetup"
    ///   2. 挂载此脚本
    ///   3. 运行场景即可看到完整战斗
    /// </summary>
    public class BattleSceneSetup : MonoBehaviour
    {
        [Header("自动启动")]
        [Tooltip("是否在Start时自动启动测试战斗")]
        [SerializeField] private bool _autoStartBattle = true;

        [Header("摄像机")]
        [Tooltip("主摄像机（留空则自动查找）")]
        [SerializeField] private Camera _mainCamera;

        private void Start()
        {
            Logger.I("BattleSetup", "═══ 战斗场景初始化开始 ═══");

            // 0. 确保主摄像机就绪
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            // 1. 预加载所有MonoSingleton（确保它们的GameObject在场景中创建）
            PreloadSingletons();

            // 2. 预加载纯C#单例
            RuneSystem.Preload();
            RuneSystem.Instance.Initialize();

            // 3. 创建战斗UI
            EnsureBattleUI();

            Logger.I("BattleSetup", "═══ 所有子系统初始化完成 ═══");

            // 4. 自动开始战斗
            if (_autoStartBattle)
            {
                Invoke(nameof(StartTestBattle), 0.2f); // 延迟一帧确保所有系统就绪
            }
        }

        /// <summary>
        /// 预加载所有MonoSingleton实例
        /// </summary>
        private void PreloadSingletons()
        {
            // 框架层（纯C#单例需要手动Initialize）
            EventBus.Preload();
            if (!EventBus.Instance.IsInitialized)
                EventBus.Instance.Initialize();
            ObjectPoolManager.Preload();


            // 地图系统
            GridSystem.Preload();
            MapRenderer.Preload();
            PathVisualizer.Preload();
            // Pathfinding 是纯C#单例，BattleManager加载关卡时会调用InitForMap
            Pathfinding.Preload();
            Pathfinding.Instance.Initialize();

            // 战斗核心
            BattleManager.Preload();
            BattleEconomyManager.Preload();
            BaseHealth.Preload();
            BattleInputHandler.Preload();

            // 塔系统
            TowerManager.Preload();

            // 怪物系统
            EnemySpawner.Preload();

            // 波次系统
            WaveManager.Preload();

            // 投射物系统
            ProjectileManager.Preload();

            // 命中特效系统
            HitEffectSystem.Preload();


            // 视觉反馈系统
            TowerAttackVFX.Preload();
            EnemyVisualManager.Preload();
            DamagePopupManager.Preload();
            BattleFieldVFX.Preload();
            ParticleVFXSystem.Preload();

            // 战斗手感 & 打磨系统
            BattleFeelSystem.Preload();
            BattleAudioFeedback.Preload();
            BattleBugFixer.Preload();
            BattleBalanceAdjuster.Preload();

            // 性能优化系统
            SpatialPartition.Preload();
            BattlePerformanceOptimizer.Preload();

            // 战斗摄像机系统（缩放/拖拽/边界限制）
            PreloadBattleCamera();

            // 画面增强系统（纹理质量修复 + 后处理效果 + 抗锯齿）
            PreloadVisualPolish();

            Logger.I("BattleSetup", "所有子系统预加载完成");




        }

        /// <summary>
        /// 预加载战斗摄像机 — 确保BattleCamera挂载到主摄像机上
        /// 而不是创建新的空GameObject，避免相机控制权冲突
        /// </summary>
        private void PreloadBattleCamera()
        {
            // 检查场景中是否已有BattleCamera
            if (BattleCamera.HasInstance) return;

            var existingCamera = FindObjectOfType<BattleCamera>();
            if (existingCamera != null) return;

            // 将BattleCamera挂载到主摄像机对象上（而非创建新GameObject）
            if (_mainCamera != null)
            {
                _mainCamera.gameObject.AddComponent<BattleCamera>();
                Logger.I("BattleSetup", "BattleCamera已挂载到主摄像机: {0}", _mainCamera.gameObject.name);
            }
            else
            {
                // 兜底：走默认Preload流程（会创建新GameObject）
                BattleCamera.Preload();
                Logger.W("BattleSetup", "主摄像机为空，BattleCamera走默认Preload");
            }
        }

        /// <summary>
        /// 预加载画面增强系统 — 挂载到主摄像机上以支持OnRenderImage后处理
        /// </summary>
        private void PreloadVisualPolish()
        {
            if (VisualPolishSystem.HasInstance) return;


            var existing = FindObjectOfType<VisualPolishSystem>();
            if (existing != null) return;

            // 将VisualPolishSystem挂载到主摄像机上（OnRenderImage需要在Camera所在的GameObject上）
            if (_mainCamera != null)
            {
                _mainCamera.gameObject.AddComponent<VisualPolishSystem>();
                Logger.I("BattleSetup", "VisualPolishSystem已挂载到主摄像机");
            }
            else
            {
                VisualPolishSystem.Preload();
                Logger.W("BattleSetup", "主摄像机为空，VisualPolishSystem走默认Preload");
            }
        }


        /// <summary>
        /// 确保战斗UI存在
        /// </summary>


        private void EnsureBattleUI()
        {
            if (FindObjectOfType<BattleUI>() == null)
            {
                var uiObj = new GameObject("[BattleUI]");
                uiObj.AddComponent<BattleUI>();
                DontDestroyOnLoad(uiObj);
                Logger.I("BattleSetup", "BattleUI已自动创建");
            }
        }

        /// <summary>
        /// 启动测试战斗
        /// </summary>
        private void StartTestBattle()
        {
            Logger.I("BattleSetup", "正在启动测试战斗...");
            BattleManager.Instance.StartBattle("test");

            // 延迟调整摄像机适配地图
            Invoke(nameof(FitCameraToMap), 0.3f);
        }

        /// <summary>
        /// 调整摄像机以适配地图大小
        /// 优先委托给BattleCamera处理，避免两套系统争夺相机控制权
        /// </summary>
        private void FitCameraToMap()
        {
            // 优先使用BattleCamera的FitToMap（它会正确设置_targetPosition和_targetOrthoSize）
            if (BattleCamera.HasInstance)
            {
                BattleCamera.Instance.FitToMap();
                Logger.I("BattleSetup", "摄像机适配已委托给BattleCamera");
                return;
            }

            // 兜底：直接操作主摄像机（BattleCamera不存在时）
            if (_mainCamera == null || !GridSystem.HasInstance || !GridSystem.Instance.IsMapLoaded) return;

            var grid = GridSystem.Instance;
            var bounds = grid.GetMapBounds();

            // 强制设为正交模式（2D塔防游戏必须使用正交摄像机）
            if (!_mainCamera.orthographic)
            {
                _mainCamera.orthographic = true;
                Logger.I("BattleSetup", "摄像机已强制切换为正交模式");
            }

            // 将摄像机移到地图中心
            var camPos = bounds.center;
            camPos.z = _mainCamera.transform.position.z;
            _mainCamera.transform.position = camPos;

            // 调整正交大小以完整显示地图（加上一些边距）
            float mapHeight = bounds.size.y;
            float mapWidth = bounds.size.x;
            float screenAspect = (float)Screen.width / Screen.height;
            float targetOrthoSize = Mathf.Max(mapHeight * 0.55f, mapWidth * 0.55f / screenAspect);
            _mainCamera.orthographicSize = targetOrthoSize;

            Logger.I("BattleSetup", "摄像机已适配地图(兜底): 中心=({0:F1},{1:F1}) 正交大小={2:F1}",
                camPos.x, camPos.y, _mainCamera.orthographicSize);
        }

    }
}
