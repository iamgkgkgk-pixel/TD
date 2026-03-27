
// ============================================================
// 文件名：EventBus.cs
// 功能描述：类型安全的事件总线系统
//          模块间解耦通信的核心基础设施
//          支持：类型安全、优先级排序、一次性监听、无GC分配
// 创建时间：2026-03-25
// 所属模块：Framework
// 对应交互：阶段二 #45
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetheraSurvivors.Framework
{
    /// <summary>
    /// 事件基类 — 所有事件必须继承此接口
    /// 使用struct定义事件以避免堆分配
    /// </summary>
    public interface IEvent
    {
    }

    /// <summary>
    /// 事件监听器包装（内部使用）
    /// </summary>
    internal class EventListenerWrapper : IComparable<EventListenerWrapper>
    {
        /// <summary>回调委托（未类型化，内部转换）</summary>
        public Delegate Callback;

        /// <summary>优先级（数值越小越先执行）</summary>
        public int Priority;

        /// <summary>是否为一次性监听（触发一次后自动移除）</summary>
        public bool IsOnce;

        /// <summary>按优先级排序</summary>
        public int CompareTo(EventListenerWrapper other)
        {
            return Priority.CompareTo(other.Priority);
        }
    }

    /// <summary>
    /// 事件总线 — 全局事件发布/订阅系统
    /// 
    /// 设计要点：
    /// 1. 类型安全：基于泛型，编译期检查事件类型
    /// 2. 优先级：监听器可指定执行优先级
    /// 3. 一次性监听：SubscribeOnce 触发一次后自动移除
    /// 4. 低GC：使用预分配List，避免频繁创建
    /// 5. 安全发布：发布过程中的订阅/取消订阅延迟到发布完成后执行
    /// 
    /// 使用示例：
    ///   // 定义事件（推荐使用struct减少GC）
    ///   public struct EnemyDeathEvent : IEvent
    ///   {
    ///       public int EnemyId;
    ///       public Vector3 Position;
    ///       public int GoldDrop;
    ///   }
    ///   
    ///   // 订阅事件
    ///   EventBus.Instance.Subscribe&lt;EnemyDeathEvent&gt;(OnEnemyDeath);
    ///   
    ///   // 发布事件
    ///   EventBus.Instance.Publish(new EnemyDeathEvent 
    ///   { 
    ///       EnemyId = 1, 
    ///       Position = transform.position, 
    ///       GoldDrop = 10 
    ///   });
    ///   
    ///   // 回调处理
    ///   private void OnEnemyDeath(EnemyDeathEvent evt)
    ///   {
    ///       Debug.Log($"怪物 {evt.EnemyId} 死亡，掉落 {evt.GoldDrop} 金币");
    ///   }
    ///   
    ///   // 取消订阅（OnDestroy时必须取消！）
    ///   EventBus.Instance.Unsubscribe&lt;EnemyDeathEvent&gt;(OnEnemyDeath);
    /// </summary>
    public class EventBus : Singleton<EventBus>
    {
        // ========== 常量 ==========

        /// <summary>每种事件类型的默认监听器列表容量</summary>
        private const int DefaultListenerCapacity = 8;

        /// <summary>默认优先级</summary>
        public const int PriorityDefault = 0;
        public const int PriorityHigh = -100;
        public const int PriorityLow = 100;

        // ========== 私有字段 ==========

        /// <summary>事件类型 → 监听器列表的映射</summary>
        private readonly Dictionary<Type, List<EventListenerWrapper>> _listenerMap
            = new Dictionary<Type, List<EventListenerWrapper>>();

        /// <summary>嵌套发布深度计数器（支持事件回调中再次发布事件）</summary>
        private int _publishDepth = 0;

        /// <summary>是否正在发布事件</summary>
        private bool _isPublishing => _publishDepth > 0;

        /// <summary>延迟执行的操作队列（发布过程中的订阅/取消请求）</summary>
        private readonly List<Action> _pendingActions = new List<Action>(16);


        // ========== 公共方法：订阅 ==========

        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <typeparam name="T">事件类型（必须实现IEvent）</typeparam>
        /// <param name="handler">回调方法</param>
        /// <param name="priority">优先级（默认0，数值越小越先执行）</param>
        public void Subscribe<T>(Action<T> handler, int priority = PriorityDefault) where T : struct, IEvent
        {
            if (handler == null)
            {
                Debug.LogError("[EventBus] Subscribe: handler不能为null");
                return;
            }

            // 如果正在发布中，延迟执行
            if (_isPublishing)
            {
                _pendingActions.Add(() => SubscribeInternal(handler, priority, false));
                return;
            }

            SubscribeInternal(handler, priority, false);
        }

        /// <summary>
        /// 订阅事件（一次性，触发后自动移除）
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handler">回调方法</param>
        /// <param name="priority">优先级</param>
        public void SubscribeOnce<T>(Action<T> handler, int priority = PriorityDefault) where T : struct, IEvent
        {
            if (handler == null)
            {
                Debug.LogError("[EventBus] SubscribeOnce: handler不能为null");
                return;
            }

            if (_isPublishing)
            {
                _pendingActions.Add(() => SubscribeInternal(handler, priority, true));
                return;
            }

            SubscribeInternal(handler, priority, true);
        }

        // ========== 公共方法：取消订阅 ==========

        /// <summary>
        /// 取消订阅事件
        /// ⚠️ 重要：在MonoBehaviour的OnDestroy中必须取消订阅，否则会内存泄漏
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handler">之前订阅时传入的同一个方法引用</param>
        public void Unsubscribe<T>(Action<T> handler) where T : struct, IEvent
        {
            if (handler == null) return;

            if (_isPublishing)
            {
                _pendingActions.Add(() => UnsubscribeInternal<T>(handler));
                return;
            }

            UnsubscribeInternal<T>(handler);
        }

        // ========== 公共方法：发布 ==========

        /// <summary>
        /// 发布事件（同步执行所有监听器回调）
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="evt">事件数据</param>
        public void Publish<T>(T evt) where T : struct, IEvent
        {
            var type = typeof(T);

            if (!_listenerMap.TryGetValue(type, out var listeners) || listeners.Count == 0)
            {
                return;
            }

            // 嵌套深度+1（支持事件回调中再次发布事件）
            _publishDepth++;

            // 每次Publish使用独立的本地快照（避免嵌套发布时共享快照被覆盖）
            var snapshot = new List<EventListenerWrapper>(listeners.Count);
            for (int i = 0; i < listeners.Count; i++)
            {
                snapshot.Add(listeners[i]);
            }

            // 按优先级顺序执行回调
            for (int i = 0; i < snapshot.Count; i++)
            {
                var wrapper = snapshot[i];

                try
                {
                    // 类型安全转换并调用
                    var typedCallback = wrapper.Callback as Action<T>;
                    typedCallback?.Invoke(evt);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EventBus] 处理事件 {type.Name} 时发生异常: {e}");
                }

                // 一次性监听器，触发后标记移除
                if (wrapper.IsOnce)
                {
                    _pendingActions.Add(() =>
                    {
                        if (listeners.Contains(wrapper))
                        {
                            listeners.Remove(wrapper);
                        }
                    });
                }
            }

            // 嵌套深度-1
            _publishDepth--;

            // 只在最外层Publish结束时才执行延迟操作
            if (_publishDepth == 0 && _pendingActions.Count > 0)
            {
                for (int i = 0; i < _pendingActions.Count; i++)
                {
                    _pendingActions[i].Invoke();
                }
                _pendingActions.Clear();
            }
        }


        // ========== 公共方法：管理 ==========

        /// <summary>
        /// 清除指定事件类型的所有监听器
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        public void ClearListeners<T>() where T : struct, IEvent
        {
            var type = typeof(T);
            if (_listenerMap.TryGetValue(type, out var listeners))
            {
                listeners.Clear();
            }
        }

        /// <summary>
        /// 清除所有事件的所有监听器
        /// 通常在场景切换或游戏退出时调用
        /// </summary>
        public void ClearAll()
        {
            _listenerMap.Clear();
            _pendingActions.Clear();
            _publishDepth = 0;
        }


        /// <summary>
        /// 获取指定事件类型的当前监听器数量（调试用）
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <returns>监听器数量</returns>
        public int GetListenerCount<T>() where T : struct, IEvent
        {
            var type = typeof(T);
            if (_listenerMap.TryGetValue(type, out var listeners))
            {
                return listeners.Count;
            }
            return 0;
        }

        /// <summary>
        /// 获取所有已注册事件类型的信息（调试用）
        /// </summary>
        /// <returns>事件类型名称 → 监听器数量 的字典</returns>
        public Dictionary<string, int> GetDebugInfo()
        {
            var info = new Dictionary<string, int>();
            foreach (var pair in _listenerMap)
            {
                info[pair.Key.Name] = pair.Value.Count;
            }
            return info;
        }

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            // 纯C#单例无需额外初始化
        }

        protected override void OnDispose()
        {
            ClearAll();
        }

        // ========== 内部方法 ==========

        /// <summary>
        /// 内部订阅实现
        /// </summary>
        private void SubscribeInternal<T>(Action<T> handler, int priority, bool isOnce) where T : struct, IEvent
        {
            var type = typeof(T);

            if (!_listenerMap.TryGetValue(type, out var listeners))
            {
                listeners = new List<EventListenerWrapper>(DefaultListenerCapacity);
                _listenerMap[type] = listeners;
            }

            // 检查是否重复订阅
            for (int i = 0; i < listeners.Count; i++)
            {
                if (listeners[i].Callback.Equals(handler))
                {
                    Debug.LogWarning($"[EventBus] 重复订阅事件 {type.Name}，已忽略");
                    return;
                }
            }

            var wrapper = new EventListenerWrapper
            {
                Callback = handler,
                Priority = priority,
                IsOnce = isOnce
            };

            // 插入排序，保持按优先级排列
            int insertIndex = listeners.Count;
            for (int i = 0; i < listeners.Count; i++)
            {
                if (priority < listeners[i].Priority)
                {
                    insertIndex = i;
                    break;
                }
            }

            listeners.Insert(insertIndex, wrapper);
        }

        /// <summary>
        /// 内部取消订阅实现
        /// </summary>
        private void UnsubscribeInternal<T>(Action<T> handler) where T : struct, IEvent
        {
            var type = typeof(T);

            if (!_listenerMap.TryGetValue(type, out var listeners))
            {
                return;
            }

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                if (listeners[i].Callback.Equals(handler))
                {
                    listeners.RemoveAt(i);
                    return;
                }
            }
        }
    }

    // ====================================================================
    // 以下是预定义的核心游戏事件
    // 其他模块的事件在各自模块中定义
    // ====================================================================

    #region 核心游戏事件定义

    /// <summary>
    /// 游戏状态变化事件
    /// </summary>
    public struct GameStateChangedEvent : IEvent
    {
        /// <summary>旧状态</summary>
        public int OldState;

        /// <summary>新状态</summary>
        public int NewState;
    }

    /// <summary>
    /// 场景加载完成事件
    /// </summary>
    public struct SceneLoadedEvent : IEvent
    {
        /// <summary>场景名称</summary>
        public string SceneName;
    }

    /// <summary>
    /// 应用前后台切换事件
    /// </summary>
    public struct AppFocusEvent : IEvent
    {
        /// <summary>true=回到前台, false=切到后台</summary>
        public bool IsForeground;
    }

    #endregion
}
