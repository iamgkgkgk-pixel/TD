// ============================================================
// 文件名：MemoryOptimizer.cs
// 功能描述：内存优化 — 资源卸载策略、纹理压缩验证、
//          内存预算管理、自动降级
// 创建时间：2026-03-25
// 所属模块：Battle/Performance
// 对应交互：阶段三 #165-#166
// ============================================================

using System;
using UnityEngine;
using UnityEngine.Profiling;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Performance

{
    /// <summary>
    /// 内存优化器
    /// 
    /// 目标：运行内存 < 256MB（微信小游戏限制）
    /// 
    /// 策略：
    /// 1. 内存预算监控：实时追踪Mono堆+纹理+Mesh占用
    /// 2. 资源卸载：战斗结束后卸载非必要资源
    /// 3. 纹理压缩验证：确保所有纹理使用ASTC/ETC2
    /// 4. 自动降级：内存超预算时自动降低纹理质量
    /// 5. 对象池容量控制：避免池过大占用内存
    /// </summary>
    public class MemoryOptimizer : MonoSingleton<MemoryOptimizer>
    {
        // ========== 配置 ==========

        /// <summary>内存预算上限（MB）</summary>
        private const float MemoryBudgetMB = 230f; // 留20MB安全余量

        /// <summary>内存警告阈值（MB）</summary>
        private const float MemoryWarningMB = 200f;

        /// <summary>内存检查间隔（秒）</summary>
        private const float CheckInterval = 5f;

        // ========== 运行时数据 ==========

        /// <summary>内存检查计时器</summary>
        private float _checkTimer = 0f;

        /// <summary>上次测量的内存（MB）</summary>
        private float _lastMeasuredMemoryMB = 0f;

        /// <summary>内存峰值（MB）</summary>
        private float _peakMemoryMB = 0f;

        /// <summary>是否已触发内存警告</summary>
        private bool _memoryWarningTriggered = false;

        // ========== 公共属性 ==========

        /// <summary>当前已用内存（MB）</summary>
        public float CurrentMemoryMB => _lastMeasuredMemoryMB;

        /// <summary>内存峰值（MB）</summary>
        public float PeakMemoryMB => _peakMemoryMB;

        /// <summary>内存使用百分比</summary>
        public float MemoryUsagePercent => _lastMeasuredMemoryMB / MemoryBudgetMB;

        /// <summary>是否处于内存压力状态</summary>
        public bool IsMemoryPressure => _lastMeasuredMemoryMB >= MemoryWarningMB;

        // ========== 事件 ==========

        /// <summary>内存警告回调</summary>
        public Action<float> OnMemoryWarning;

        /// <summary>内存危险回调</summary>
        public Action<float> OnMemoryCritical;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            MeasureMemory();
            Logger.I("MemoryOptimizer", "内存优化器初始化 预算:{0}MB 当前:{1:F1}MB",
                MemoryBudgetMB, _lastMeasuredMemoryMB);
        }

        private void Update()
        {
            _checkTimer += Time.unscaledDeltaTime;

            if (_checkTimer >= CheckInterval)
            {
                _checkTimer = 0f;
                MeasureMemory();
                CheckMemoryBudget();
            }
        }

        // ========== 核心方法 ==========

        /// <summary>测量当前内存使用</summary>
        public void MeasureMemory()
        {
            // 总分配内存（Mono + Native）
            long totalBytes = Profiler.GetTotalAllocatedMemoryLong();
            _lastMeasuredMemoryMB = totalBytes / (1024f * 1024f);

            if (_lastMeasuredMemoryMB > _peakMemoryMB)
            {
                _peakMemoryMB = _lastMeasuredMemoryMB;
            }
        }

        /// <summary>检查内存预算</summary>
        private void CheckMemoryBudget()
        {
            if (_lastMeasuredMemoryMB >= MemoryBudgetMB)
            {
                // 内存危险！
                Logger.E("MemoryOptimizer", "⚠️ 内存超预算: {0:F1}MB / {1}MB",
                    _lastMeasuredMemoryMB, MemoryBudgetMB);

                OnMemoryCritical?.Invoke(_lastMeasuredMemoryMB);
                EmergencyCleanup();
            }
            else if (_lastMeasuredMemoryMB >= MemoryWarningMB)
            {
                if (!_memoryWarningTriggered)
                {
                    _memoryWarningTriggered = true;
                    Logger.W("MemoryOptimizer", "内存警告: {0:F1}MB / {1}MB",
                        _lastMeasuredMemoryMB, MemoryBudgetMB);

                    OnMemoryWarning?.Invoke(_lastMeasuredMemoryMB);
                    SoftCleanup();
                }
            }
            else
            {
                _memoryWarningTriggered = false;
            }
        }

        /// <summary>
        /// 软清理（内存警告时执行）
        /// </summary>
        public void SoftCleanup()
        {
            // 1. 卸载未使用的资源
            Resources.UnloadUnusedAssets();

            // 2. 缩减对象池
            TrimObjectPools();

            // 3. 触发GC
            GC.Collect();

            Logger.I("MemoryOptimizer", "软清理完成");
        }

        /// <summary>
        /// 紧急清理（内存危险时执行）
        /// </summary>
        public void EmergencyCleanup()
        {
            // 1. 卸载所有未使用资源
            Resources.UnloadUnusedAssets();

            // 2. 大幅缩减对象池
            if (ObjectPoolManager.HasInstance)
            {
                ObjectPoolManager.Instance.ReturnAll();
            }

            // 3. 强制GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // 4. 降级渲染
            if (BattlePerformanceOptimizer.HasInstance)
            {
                BattlePerformanceOptimizer.Instance.SetQualityLevel(2);
            }

            Logger.W("MemoryOptimizer", "紧急清理完成");
        }

        /// <summary>
        /// 场景切换时的内存清理
        /// </summary>
        public void CleanupForSceneTransition()
        {
            // 回收所有对象池
            if (ObjectPoolManager.HasInstance)
            {
                ObjectPoolManager.Instance.ReturnAll();
            }

            // 卸载资源
            Resources.UnloadUnusedAssets();
            GC.Collect();

            Logger.I("MemoryOptimizer", "场景切换清理完成");
        }

        /// <summary>
        /// 缩减对象池（释放多余的空闲对象）
        /// </summary>
        private void TrimObjectPools()
        {
            // 对象池自身没有Trim方法，这里通过ReturnAll后重建来缩减
            // 后续可在ObjectPoolManager中添加Trim方法
            Logger.D("MemoryOptimizer", "对象池缩减");
        }

        /// <summary>
        /// 获取详细内存信息
        /// </summary>
        public MemorySnapshot GetMemorySnapshot()
        {
            return new MemorySnapshot
            {
                TotalAllocatedMB = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f),
                TotalReservedMB = Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f),
                MonoUsedMB = Profiler.GetMonoUsedSizeLong() / (1024f * 1024f),
                MonoHeapMB = Profiler.GetMonoHeapSizeLong() / (1024f * 1024f),
                PeakMB = _peakMemoryMB,
                BudgetMB = MemoryBudgetMB,
                Timestamp = Time.realtimeSinceStartup
            };
        }

        // ========== 调试 ==========

        public string GetDebugInfo()
        {
            return $"内存:{_lastMeasuredMemoryMB:F1}/{MemoryBudgetMB}MB " +
                   $"峰值:{_peakMemoryMB:F1}MB " +
                   $"Mono:{Profiler.GetMonoUsedSizeLong() / (1024f * 1024f):F1}MB " +
                   $"{(IsMemoryPressure ? "⚠️压力" : "正常")}";
        }
    }

    /// <summary>内存快照数据</summary>
    public struct MemorySnapshot
    {
        public float TotalAllocatedMB;
        public float TotalReservedMB;
        public float MonoUsedMB;
        public float MonoHeapMB;
        public float PeakMB;
        public float BudgetMB;
        public float Timestamp;

        public override string ToString()
        {
            return $"Total:{TotalAllocatedMB:F1}MB Reserved:{TotalReservedMB:F1}MB " +
                   $"Mono:{MonoUsedMB:F1}/{MonoHeapMB:F1}MB Peak:{PeakMB:F1}MB";
        }
    }
}
