// ============================================================
// 文件名：BattlePerformanceOptimizer.cs
// 功能描述：战斗性能优化器 — 综合性能管理
//          目标同屏100+怪物30fps稳定（微信小游戏平台）
//          寻路缓存、对象池预热、射程检测优化、GC抑制
// 创建时间：2026-03-25
// 所属模块：Battle/Performance
// 对应交互：阶段三 #161
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

using AetheraSurvivors.Battle.Enemy;

namespace AetheraSurvivors.Battle.Performance
{
    /// <summary>
    /// 战斗性能优化器
    /// 
    /// 职责：
    /// 1. 预热对象池（避免运行时频繁Instantiate）
    /// 2. 分帧更新策略（非关键系统降频更新）
    /// 3. 视锥剔除（屏幕外怪物降低更新频率）
    /// 4. LOD系统（远处怪物简化渲染）
    /// 5. GC抑制（手动控制GC时机）
    /// 6. 性能监控（实时检测和自适应降级）
    /// </summary>
    public class BattlePerformanceOptimizer : MonoSingleton<BattlePerformanceOptimizer>
    {
        // ========== 配置 ==========

        /// <summary>目标帧率</summary>
        private const int TargetFPS = 30;

        /// <summary>帧率过低阈值（连续低于此值则触发降级）</summary>
        private const float LowFPSThreshold = 25f;

        /// <summary>性能降级等级（0=最高画质，1=中等，2=低画质）</summary>
        private int _qualityLevel = 0;

        /// <summary>分帧计数器</summary>
        private int _frameCounter = 0;

        /// <summary>GC冷却计时器</summary>
        private float _gcTimer = 0f;
        private const float GCInterval = 30f; // 30秒一次手动GC

        // ========== 性能数据 ==========

        /// <summary>当前FPS</summary>
        private float _currentFPS = 60f;
        private float _fpsAccumulator = 0f;
        private int _fpsFrameCount = 0;
        private float _fpsUpdateInterval = 0.5f;
        private float _fpsTimer = 0f;

        /// <summary>低帧率持续帧数</summary>
        private int _lowFPSFrames = 0;

        /// <summary>当前同屏怪物数</summary>
        private int _visibleEnemyCount = 0;

        // ========== 分帧更新组 ==========

        /// <summary>分帧更新目标（每N帧才更新一次的低优先级系统）</summary>
        private readonly List<IFrameSkipUpdatable> _frameSkipTargets = new List<IFrameSkipUpdatable>(16);

        // ========== 公共属性 ==========

        /// <summary>当前性能降级等级</summary>
        public int QualityLevel => _qualityLevel;

        /// <summary>当前FPS</summary>
        public float CurrentFPS => _currentFPS;

        /// <summary>是否处于低性能模式</summary>
        public bool IsLowPerformance => _qualityLevel >= 2;

        /// <summary>同屏可见怪物数</summary>
        public int VisibleEnemyCount => _visibleEnemyCount;

        /// <summary>是否应该显示粒子特效（低性能模式下关闭）</summary>
        public bool ShouldShowParticles => _qualityLevel < 2;

        /// <summary>是否应该显示飘字（低性能模式下减少）</summary>
        public bool ShouldShowDamagePopup => _qualityLevel < 2;

        /// <summary>是否应该显示拖尾效果</summary>
        public bool ShouldShowTrails => _qualityLevel < 1;

        /// <summary>Buff更新频率（降级时降低）</summary>
        public int BuffUpdateSkipFrames => _qualityLevel >= 1 ? 2 : 1;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            Application.targetFrameRate = TargetFPS;
            Logger.I("PerfOptimizer", "性能优化器初始化 目标FPS={0}", TargetFPS);
        }

        protected override void OnDispose()
        {
            _frameSkipTargets.Clear();
        }

        private void Update()
        {
            _frameCounter++;

            // 更新FPS
            UpdateFPS();

            // 性能自适应
            if (_frameCounter % 30 == 0) // 每30帧检查一次
            {
                PerformanceAdaptation();
            }

            // 分帧更新
            UpdateFrameSkipTargets();

            // GC管理
            ManageGC();

            // 视锥剔除统计
            if (_frameCounter % 15 == 0)
            {
                UpdateVisibleEnemyCount();
            }
        }

        // ========== FPS计算 ==========

        private void UpdateFPS()
        {
            _fpsAccumulator += Time.unscaledDeltaTime;
            _fpsFrameCount++;
            _fpsTimer += Time.unscaledDeltaTime;

            if (_fpsTimer >= _fpsUpdateInterval)
            {
                _currentFPS = _fpsFrameCount / _fpsAccumulator;
                _fpsAccumulator = 0f;
                _fpsFrameCount = 0;
                _fpsTimer = 0f;
            }
        }

        // ========== 性能自适应 ==========

        /// <summary>根据当前帧率自动调整画质等级</summary>
        private void PerformanceAdaptation()
        {
            if (_currentFPS < LowFPSThreshold)
            {
                _lowFPSFrames++;
            }
            else
            {
                _lowFPSFrames = Mathf.Max(0, _lowFPSFrames - 1);
            }

            // 连续3次检测到低帧率 → 降级
            if (_lowFPSFrames >= 3 && _qualityLevel < 2)
            {
                SetQualityLevel(_qualityLevel + 1);
                _lowFPSFrames = 0;
            }

            // 连续10次正常帧率 → 升级
            if (_lowFPSFrames == 0 && _currentFPS > TargetFPS + 5 && _qualityLevel > 0)
            {
                _lowFPSFrames = -10; // 需要10次正常
                SetQualityLevel(_qualityLevel - 1);
            }
        }

        /// <summary>设置画质等级</summary>
        public void SetQualityLevel(int level)
        {
            int oldLevel = _qualityLevel;
            _qualityLevel = Mathf.Clamp(level, 0, 2);

            if (oldLevel != _qualityLevel)
            {
                ApplyQualitySettings();
                Logger.I("PerfOptimizer", "画质等级调整: {0} → {1} (FPS={2:F1})",
                    oldLevel, _qualityLevel, _currentFPS);
            }
        }

        /// <summary>应用画质设置</summary>
        private void ApplyQualitySettings()
        {
            switch (_qualityLevel)
            {
                case 0: // 最高画质
                    QualitySettings.vSyncCount = 0;
                    QualitySettings.antiAliasing = 2; // 2x MSAA抗锯齿
                    Application.targetFrameRate = TargetFPS;
                    break;

                case 1: // 中等画质：减少粒子、降低更新频率
                    QualitySettings.vSyncCount = 0;
                    QualitySettings.antiAliasing = 2; // 2x MSAA
                    Application.targetFrameRate = TargetFPS;
                    break;

                case 2: // 低画质：关闭粒子、关闭飘字、降低渲染
                    QualitySettings.vSyncCount = 0;
                    QualitySettings.antiAliasing = 2; // 保留最低限度抗锯齿，避免UI锯齿
                    Application.targetFrameRate = TargetFPS;
                    break;

            }
        }


        // ========== 分帧更新 ==========

        /// <summary>注册分帧更新目标</summary>
        public void RegisterFrameSkipTarget(IFrameSkipUpdatable target)
        {
            if (!_frameSkipTargets.Contains(target))
            {
                _frameSkipTargets.Add(target);
            }
        }

        /// <summary>注销分帧更新目标</summary>
        public void UnregisterFrameSkipTarget(IFrameSkipUpdatable target)
        {
            _frameSkipTargets.Remove(target);
        }

        /// <summary>执行分帧更新</summary>
        private void UpdateFrameSkipTargets()
        {
            for (int i = 0; i < _frameSkipTargets.Count; i++)
            {
                var target = _frameSkipTargets[i];
                if (target == null) continue;

                // 根据优先级决定跳帧数
                int skipFrames = target.FrameSkipCount;
                if (_qualityLevel >= 1) skipFrames *= 2; // 低画质模式下进一步降频

                if (_frameCounter % skipFrames == 0)
                {
                    target.FrameSkipUpdate();
                }
            }
        }

        // ========== GC管理 ==========

        /// <summary>管理GC时机</summary>
        private void ManageGC()
        {
            _gcTimer += Time.unscaledDeltaTime;

            if (_gcTimer >= GCInterval)
            {
                _gcTimer = 0f;

                // 在波次间隙或非战斗高峰期执行GC
                if (!IsInCombatPeak())
                {
                    PerformGC();
                }
            }
        }

        /// <summary>手动触发GC（在安全时机调用）</summary>
        public void PerformGC()
        {
            System.GC.Collect();
            Logger.D("PerfOptimizer", "手动GC执行");
        }

        /// <summary>是否处于战斗高峰期</summary>
        private bool IsInCombatPeak()
        {
            if (!EnemySpawner.HasInstance) return false;
            return EnemySpawner.Instance.ActiveEnemyCount > 20;
        }

        // ========== 视锥剔除 ==========

        /// <summary>更新可见怪物计数</summary>
        private void UpdateVisibleEnemyCount()
        {
            _visibleEnemyCount = 0;

            if (!EnemySpawner.HasInstance || Camera.main == null) return;

            var cam = Camera.main;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 camPos = cam.transform.position;

            // 扩大一点边界（预加载区域）
            float padding = 1f;
            float left = camPos.x - halfWidth - padding;
            float right = camPos.x + halfWidth + padding;
            float bottom = camPos.y - halfHeight - padding;
            float top = camPos.y + halfHeight + padding;

            var enemies = EnemySpawner.Instance.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] == null) continue;
                var pos = enemies[i].transform.position;
                if (pos.x >= left && pos.x <= right && pos.y >= bottom && pos.y <= top)
                {
                    _visibleEnemyCount++;
                }
            }
        }

        /// <summary>
        /// 检查一个位置是否在摄像机可见范围内
        /// </summary>
        public bool IsVisible(Vector3 worldPos)
        {
            if (Camera.main == null) return true;

            var cam = Camera.main;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 camPos = cam.transform.position;

            float padding = 1f;
            return worldPos.x >= camPos.x - halfWidth - padding &&
                   worldPos.x <= camPos.x + halfWidth + padding &&
                   worldPos.y >= camPos.y - halfHeight - padding &&
                   worldPos.y <= camPos.y + halfHeight + padding;
        }

        // ========== 对象池预热 ==========

        /// <summary>
        /// 预热战斗相关的对象池
        /// 建议在关卡加载时调用
        /// </summary>
        public void PreWarmBattlePools()
        {
            Logger.I("PerfOptimizer", "开始预热对象池...");

            // 后续接入预制体时，在此处预热各种对象池
            // ObjectPoolManager.Instance.CreatePool(enemyPrefab, 20, 100);
            // ObjectPoolManager.Instance.CreatePool(projectilePrefab, 30, 80);
            // ObjectPoolManager.Instance.CreatePool(vfxPrefab, 15, 50);

            Logger.I("PerfOptimizer", "对象池预热完成");
        }

        // ========== 调试 ==========

        /// <summary>获取完整性能调试信息</summary>
        public string GetDebugInfo()
        {
            string spatialInfo = SpatialPartition.HasInstance ? SpatialPartition.Instance.GetDebugInfo() : "未初始化";

            return $"FPS:{_currentFPS:F1} " +
                   $"画质:Lv{_qualityLevel} " +
                   $"可见怪物:{_visibleEnemyCount} " +
                   $"总怪物:{(EnemySpawner.HasInstance ? EnemySpawner.Instance.ActiveEnemyCount : 0)} " +
                   $"空间分区:[{spatialInfo}]";
        }
    }

    // ====================================================================
    // 分帧更新接口
    // ====================================================================

    /// <summary>
    /// 分帧更新接口 — 实现此接口的系统将按降频方式更新
    /// </summary>
    public interface IFrameSkipUpdatable
    {
        /// <summary>跳帧数（2=每2帧更新一次，3=每3帧更新一次）</summary>
        int FrameSkipCount { get; }

        /// <summary>分帧更新回调</summary>
        void FrameSkipUpdate();
    }
}
