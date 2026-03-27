
// ============================================================
// 文件名：FSM.cs
// 功能描述：有限状态机 — 通用泛型状态机
//          支持Enter/Update/Exit回调、状态切换
// 创建时间：2026-03-25
// 所属模块：Framework
// 对应交互：阶段二 #54
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetheraSurvivors.Framework
{
    /// <summary>
    /// 状态基类
    /// </summary>
    /// <typeparam name="T">拥有者类型（挂载状态机的对象）</typeparam>
    public abstract class FsmState<T>
    {
        /// <summary>状态机引用</summary>
        public FSM<T> Machine { get; internal set; }

        /// <summary>拥有者引用</summary>
        public T Owner => Machine.Owner;

        /// <summary>
        /// 进入状态时调用
        /// </summary>
        /// <param name="prevState">上一个状态（可能为null）</param>
        public virtual void OnEnter(FsmState<T> prevState) { }

        /// <summary>
        /// 每帧更新（需要手动调用FSM.Update）
        /// </summary>
        /// <param name="deltaTime">帧间隔时间</param>
        public virtual void OnUpdate(float deltaTime) { }

        /// <summary>
        /// 固定物理更新
        /// </summary>
        /// <param name="fixedDeltaTime">固定间隔时间</param>
        public virtual void OnFixedUpdate(float fixedDeltaTime) { }

        /// <summary>
        /// 退出状态时调用
        /// </summary>
        /// <param name="nextState">下一个状态</param>
        public virtual void OnExit(FsmState<T> nextState) { }
    }

    /// <summary>
    /// 有限状态机 — 泛型实现
    /// 
    /// 使用示例：
    ///   // 1. 定义状态枚举
    ///   public enum EnemyState { Idle, Walk, Attack, Death }
    ///   
    ///   // 2. 定义具体状态类
    ///   public class EnemyIdleState : FsmState&lt;EnemyBase&gt;
    ///   {
    ///       public override void OnEnter(FsmState&lt;EnemyBase&gt; prev)
    ///       {
    ///           // 播放待机动画
    ///       }
    ///       
    ///       public override void OnUpdate(float dt)
    ///       {
    ///           // 检测到路径后切换到Walk
    ///           if (Owner.HasPath)
    ///               Machine.ChangeState&lt;EnemyWalkState&gt;();
    ///       }
    ///   }
    ///   
    ///   // 3. 创建和使用状态机
    ///   var fsm = new FSM&lt;EnemyBase&gt;(this);
    ///   fsm.AddState&lt;EnemyIdleState&gt;();
    ///   fsm.AddState&lt;EnemyWalkState&gt;();
    ///   fsm.AddState&lt;EnemyAttackState&gt;();
    ///   fsm.AddState&lt;EnemyDeathState&gt;();
    ///   fsm.Start&lt;EnemyIdleState&gt;();
    ///   
    ///   // 4. 在Update中驱动
    ///   fsm.Update(Time.deltaTime);
    /// </summary>
    /// <typeparam name="T">拥有者类型</typeparam>
    public class FSM<T>
    {
        // ========== 私有字段 ==========

        /// <summary>状态类型 → 状态实例映射</summary>
        private readonly Dictionary<Type, FsmState<T>> _states = new Dictionary<Type, FsmState<T>>();

        /// <summary>当前状态</summary>
        private FsmState<T> _currentState;

        /// <summary>上一个状态</summary>
        private FsmState<T> _previousState;

        /// <summary>是否正在切换状态（防止切换中再次切换）</summary>
        private bool _isTransitioning;

        // ========== 公共属性 ==========

        /// <summary>拥有者</summary>
        public T Owner { get; private set; }

        /// <summary>当前状态</summary>
        public FsmState<T> CurrentState => _currentState;

        /// <summary>上一个状态</summary>
        public FsmState<T> PreviousState => _previousState;

        /// <summary>当前状态类型</summary>
        public Type CurrentStateType => _currentState?.GetType();

        /// <summary>状态机是否已启动</summary>
        public bool IsRunning => _currentState != null;

        // ========== 构造函数 ==========

        /// <summary>
        /// 创建状态机
        /// </summary>
        /// <param name="owner">拥有者对象</param>
        public FSM(T owner)
        {
            Owner = owner;
        }

        // ========== 公共方法：状态管理 ==========

        /// <summary>
        /// 添加状态
        /// </summary>
        /// <typeparam name="TState">状态类型</typeparam>
        /// <returns>状态机自身（链式调用）</returns>
        public FSM<T> AddState<TState>() where TState : FsmState<T>, new()
        {
            var stateType = typeof(TState);

            if (_states.ContainsKey(stateType))
            {
                Debug.LogWarning($"[FSM] 状态已存在: {stateType.Name}");
                return this;
            }

            var state = new TState();
            state.Machine = this;
            _states[stateType] = state;

            return this;
        }

        /// <summary>
        /// 添加状态（传入已创建的实例）
        /// </summary>
        public FSM<T> AddState<TState>(TState state) where TState : FsmState<T>
        {
            if (state == null)
            {
                Debug.LogError("[FSM] AddState: state不能为null");
                return this;
            }

            var stateType = typeof(TState);
            state.Machine = this;
            _states[stateType] = state;

            return this;
        }

        /// <summary>
        /// 启动状态机（设置初始状态）
        /// </summary>
        /// <typeparam name="TState">初始状态类型</typeparam>
        public void Start<TState>() where TState : FsmState<T>
        {
            var stateType = typeof(TState);

            if (!_states.TryGetValue(stateType, out var state))
            {
                Debug.LogError($"[FSM] 启动失败，状态未注册: {stateType.Name}");
                return;
            }

            _currentState = state;
            _currentState.OnEnter(null);
        }

        /// <summary>
        /// 切换状态
        /// </summary>
        /// <typeparam name="TState">目标状态类型</typeparam>
        public void ChangeState<TState>() where TState : FsmState<T>
        {
            ChangeState(typeof(TState));
        }

        /// <summary>
        /// 切换状态（非泛型版本）
        /// </summary>
        public void ChangeState(Type stateType)
        {
            if (_isTransitioning)
            {
                Debug.LogWarning($"[FSM] 正在切换状态中，忽略切换请求: {stateType.Name}");
                return;
            }

            if (!_states.TryGetValue(stateType, out var nextState))
            {
                Debug.LogError($"[FSM] 状态未注册: {stateType.Name}");
                return;
            }

            // 相同状态不切换
            if (_currentState != null && _currentState.GetType() == stateType)
            {
                return;
            }

            _isTransitioning = true;

            // 退出当前状态
            _previousState = _currentState;
            _currentState?.OnExit(nextState);

            // 进入新状态
            _currentState = nextState;
            _currentState.OnEnter(_previousState);

            _isTransitioning = false;
        }

        /// <summary>
        /// 回退到上一个状态
        /// </summary>
        public void RevertToPrevious()
        {
            if (_previousState != null)
            {
                ChangeState(_previousState.GetType());
            }
        }

        // ========== 公共方法：更新 ==========

        /// <summary>
        /// 每帧更新（需要外部调用）
        /// </summary>
        public void Update(float deltaTime)
        {
            _currentState?.OnUpdate(deltaTime);
        }

        /// <summary>
        /// 固定物理更新
        /// </summary>
        public void FixedUpdate(float fixedDeltaTime)
        {
            _currentState?.OnFixedUpdate(fixedDeltaTime);
        }

        // ========== 公共方法：查询 ==========

        /// <summary>
        /// 检查当前是否处于指定状态
        /// </summary>
        public bool IsInState<TState>() where TState : FsmState<T>
        {
            return _currentState != null && _currentState.GetType() == typeof(TState);
        }

        /// <summary>
        /// 获取指定类型的状态实例
        /// </summary>
        public TState GetState<TState>() where TState : FsmState<T>
        {
            if (_states.TryGetValue(typeof(TState), out var state))
            {
                return state as TState;
            }
            return null;
        }

        /// <summary>
        /// 清理所有状态
        /// </summary>
        public void Clear()
        {
            _currentState?.OnExit(null);
            _currentState = null;
            _previousState = null;
            _states.Clear();
        }
    }
}
