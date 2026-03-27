
// ============================================================
// 文件名：TimerManager.cs
// 功能描述：定时器系统 — 延迟执行、重复执行、暂停/恢复
//          不依赖MonoBehaviour的Invoke/协程，统一管理
// 创建时间：2026-03-25
// 所属模块：Framework
// 对应交互：阶段二 #52
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetheraSurvivors.Framework
{
    /// <summary>
    /// 定时器数据
    /// </summary>
    public class Timer
    {
        /// <summary>唯一ID</summary>
        public int Id { get; internal set; }

        /// <summary>延迟时间（秒）</summary>
        public float Delay { get; internal set; }

        /// <summary>重复间隔（0=不重复）</summary>
        public float Interval { get; internal set; }

        /// <summary>重复次数（-1=无限）</summary>
        public int RepeatCount { get; internal set; }

        /// <summary>已执行次数</summary>
        public int ExecutedCount { get; internal set; }

        /// <summary>是否暂停</summary>
        public bool IsPaused { get; internal set; }

        /// <summary>是否已取消</summary>
        public bool IsCancelled { get; internal set; }

        /// <summary>是否使用不受TimeScale影响的时间</summary>
        public bool UseUnscaledTime { get; internal set; }

        /// <summary>回调</summary>
        internal Action Callback;

        /// <summary>剩余时间</summary>
        internal float RemainingTime;

        /// <summary>是否是首次延迟阶段</summary>
        internal bool IsInDelay;

        /// <summary>暂停定时器</summary>
        public void Pause() => IsPaused = true;

        /// <summary>恢复定时器</summary>
        public void Resume() => IsPaused = false;

        /// <summary>取消定时器</summary>
        public void Cancel() => IsCancelled = true;
    }

    /// <summary>
    /// 定时器管理器 — 全局单例
    /// 
    /// 职责：
    /// 1. 延迟执行（DelayCall）
    /// 2. 重复执行（RepeatCall）
    /// 3. 暂停/恢复/取消
    /// 4. 不依赖MonoBehaviour的Invoke/协程
    /// 
    /// 使用示例：
    ///   // 延迟2秒执行
    ///   var timer = TimerManager.Instance.DelayCall(2f, () => Debug.Log("2秒后"));
    ///   
    ///   // 每1秒执行一次，共5次
    ///   var timer = TimerManager.Instance.RepeatCall(1f, () => Debug.Log("tick"), 5);
    ///   
    ///   // 每0.5秒无限执行
    ///   var timer = TimerManager.Instance.RepeatCall(0.5f, () => Debug.Log("loop"), -1);
    ///   
    ///   // 暂停/恢复/取消
    ///   timer.Pause();
    ///   timer.Resume();
    ///   timer.Cancel();
    ///   
    ///   // 通过ID取消
    ///   TimerManager.Instance.CancelTimer(timerId);
    /// </summary>
    public class TimerManager : MonoSingleton<TimerManager>
    {
        // ========== 私有字段 ==========

        /// <summary>所有活跃的定时器</summary>
        private readonly List<Timer> _activeTimers = new List<Timer>(32);

        /// <summary>待添加的定时器（避免在遍历中修改列表）</summary>
        private readonly List<Timer> _pendingAdd = new List<Timer>(8);

        /// <summary>自增ID</summary>
        private int _nextId = 1;

        /// <summary>是否正在更新中</summary>
        private bool _isUpdating;

        // ========== 公共方法 ==========

        /// <summary>
        /// 延迟执行（执行一次）
        /// </summary>
        /// <param name="delay">延迟秒数</param>
        /// <param name="callback">回调</param>
        /// <param name="useUnscaledTime">是否使用不受TimeScale影响的时间</param>
        /// <returns>定时器对象（可用于暂停/取消）</returns>
        public Timer DelayCall(float delay, Action callback, bool useUnscaledTime = false)
        {
            return CreateTimer(delay, 0f, 1, callback, useUnscaledTime);
        }

        /// <summary>
        /// 重复执行
        /// </summary>
        /// <param name="interval">执行间隔（秒）</param>
        /// <param name="callback">回调</param>
        /// <param name="repeatCount">重复次数（-1=无限）</param>
        /// <param name="initialDelay">首次延迟（0=立即开始第一个间隔）</param>
        /// <param name="useUnscaledTime">是否使用不受TimeScale影响的时间</param>
        /// <returns>定时器对象</returns>
        public Timer RepeatCall(float interval, Action callback, int repeatCount = -1,
                                float initialDelay = 0f, bool useUnscaledTime = false)
        {
            var timer = CreateTimer(initialDelay, interval, repeatCount, callback, useUnscaledTime);
            return timer;
        }

        /// <summary>
        /// 通过ID取消定时器
        /// </summary>
        public void CancelTimer(int timerId)
        {
            for (int i = 0; i < _activeTimers.Count; i++)
            {
                if (_activeTimers[i].Id == timerId)
                {
                    _activeTimers[i].IsCancelled = true;
                    return;
                }
            }

            // 也检查待添加列表
            for (int i = 0; i < _pendingAdd.Count; i++)
            {
                if (_pendingAdd[i].Id == timerId)
                {
                    _pendingAdd[i].IsCancelled = true;
                    return;
                }
            }
        }

        /// <summary>
        /// 取消所有定时器
        /// </summary>
        public void CancelAll()
        {
            for (int i = 0; i < _activeTimers.Count; i++)
            {
                _activeTimers[i].IsCancelled = true;
            }

            for (int i = 0; i < _pendingAdd.Count; i++)
            {
                _pendingAdd[i].IsCancelled = true;
            }
        }

        /// <summary>
        /// 暂停所有定时器
        /// </summary>
        public void PauseAll()
        {
            for (int i = 0; i < _activeTimers.Count; i++)
            {
                _activeTimers[i].IsPaused = true;
            }
        }

        /// <summary>
        /// 恢复所有定时器
        /// </summary>
        public void ResumeAll()
        {
            for (int i = 0; i < _activeTimers.Count; i++)
            {
                _activeTimers[i].IsPaused = false;
            }
        }

        /// <summary>
        /// 获取当前活跃定时器数量
        /// </summary>
        public int ActiveCount => _activeTimers.Count;

        // ========== Unity生命周期 ==========

        private void Update()
        {
            // 添加待加入的定时器
            if (_pendingAdd.Count > 0)
            {
                _activeTimers.AddRange(_pendingAdd);
                _pendingAdd.Clear();
            }

            if (_activeTimers.Count == 0) return;

            _isUpdating = true;
            float deltaTime = Time.deltaTime;
            float unscaledDeltaTime = Time.unscaledDeltaTime;

            for (int i = _activeTimers.Count - 1; i >= 0; i--)
            {
                var timer = _activeTimers[i];

                // 已取消，移除
                if (timer.IsCancelled)
                {
                    _activeTimers.RemoveAt(i);
                    continue;
                }

                // 已暂停，跳过
                if (timer.IsPaused) continue;

                // 计算时间
                float dt = timer.UseUnscaledTime ? unscaledDeltaTime : deltaTime;
                timer.RemainingTime -= dt;

                if (timer.RemainingTime <= 0f)
                {
                    // 触发回调
                    try
                    {
                        timer.Callback?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[TimerManager] 定时器回调异常(ID={timer.Id}): {e}");
                    }

                    timer.ExecutedCount++;

                    // 判断是否继续
                    if (timer.RepeatCount > 0 && timer.ExecutedCount >= timer.RepeatCount)
                    {
                        // 达到最大次数，移除
                        _activeTimers.RemoveAt(i);
                    }
                    else if (timer.RepeatCount == -1 || timer.ExecutedCount < timer.RepeatCount)
                    {
                        // 继续重复
                        timer.RemainingTime = timer.Interval;
                        timer.IsInDelay = false;
                    }
                    else
                    {
                        _activeTimers.RemoveAt(i);
                    }
                }
            }

            _isUpdating = false;
        }

        // ========== 生命周期 ==========

        protected override void OnDispose()
        {
            CancelAll();
            _activeTimers.Clear();
            _pendingAdd.Clear();
        }

        // ========== 私有方法 ==========

        /// <summary>创建定时器</summary>
        private Timer CreateTimer(float delay, float interval, int repeatCount, Action callback, bool useUnscaledTime)
        {
            var timer = new Timer
            {
                Id = _nextId++,
                Delay = delay,
                Interval = interval,
                RepeatCount = repeatCount,
                ExecutedCount = 0,
                IsPaused = false,
                IsCancelled = false,
                UseUnscaledTime = useUnscaledTime,
                Callback = callback,
                RemainingTime = delay > 0f ? delay : interval,
                IsInDelay = delay > 0f
            };

            // 如果正在更新中，延迟添加
            if (_isUpdating)
            {
                _pendingAdd.Add(timer);
            }
            else
            {
                _activeTimers.Add(timer);
            }

            return timer;
        }
    }
}
