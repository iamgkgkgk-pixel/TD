
// ============================================================
// 文件名：Singleton.cs
// 功能描述：单例基类，提供MonoBehaviour单例和纯C#单例两种实现
//          MonoBehaviour单例：适用于需要挂载到GameObject的管理器
//          纯C#单例：适用于不需要Unity生命周期的纯逻辑管理器
// 创建时间：2026-03-25
// 所属模块：Framework
// 对应交互：阶段二 #44
// ============================================================

using System;
using UnityEngine;

namespace AetheraSurvivors.Framework
{
    /// <summary>
    /// MonoBehaviour单例基类
    /// 适用于需要挂载到GameObject、使用Unity生命周期的管理器
    /// 特性：
    /// 1. 自动创建GameObject（如果场景中不存在）
    /// 2. DontDestroyOnLoad（跨场景持久）
    /// 3. 防止重复实例
    /// 4. 支持子类重写OnInit/OnDispose进行初始化和清理
    /// 使用示例：
    ///   public class GameManager : MonoSingleton&lt;GameManager&gt; { }
    ///   GameManager.Instance.DoSomething();
    /// </summary>
    /// <typeparam name="T">子类类型</typeparam>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        // ========== 私有字段 ==========

        /// <summary>单例实例</summary>
        private static T _instance;

        /// <summary>锁对象，防止多线程竞争（虽然WebGL单线程，但编辑器中可能多线程）</summary>
        private static readonly object _lock = new object();

        /// <summary>应用是否正在退出（防止OnDestroy后又创建新实例）</summary>
        private static bool _isApplicationQuitting = false;

        /// <summary>是否已完成初始化</summary>
        private bool _isInitialized = false;

        // ========== 公共属性 ==========

        /// <summary>
        /// 获取单例实例
        /// 如果实例不存在且应用未退出，会自动创建一个新的GameObject并挂载组件
        /// </summary>
        public static T Instance
        {
            get
            {
                // 应用退出时不再创建新实例，避免"Some objects were not cleaned up"警告
                if (_isApplicationQuitting)
                {
                    Debug.LogWarning($"[MonoSingleton] 应用正在退出，不再创建 {typeof(T).Name} 实例");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // 先在场景中查找是否已有实例
                        _instance = FindObjectOfType<T>();

                        if (_instance == null)
                        {
                            // 场景中没有，自动创建
                            var go = new GameObject($"[{typeof(T).Name}]");
                            _instance = go.AddComponent<T>();
                            DontDestroyOnLoad(go);
                        }
                    }

                    return _instance;
                }
            }
        }

        /// <summary>
        /// 检查单例是否已创建（不会触发自动创建）
        /// 用于在不确定是否需要实例的场景下安全检查
        /// </summary>
        public static bool HasInstance => _instance != null;

        /// <summary>是否已完成初始化</summary>
        public bool IsInitialized => _isInitialized;

        // ========== Unity生命周期 ==========

        protected virtual void Awake()
        {
            // 防止重复实例：如果已有实例且不是自己，销毁自己
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[MonoSingleton] {typeof(T).Name} 已存在实例，销毁重复对象: {gameObject.name}");
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;
            DontDestroyOnLoad(gameObject);

            // 执行子类初始化
            if (!_isInitialized)
            {
                OnInit();
                _isInitialized = true;
            }
        }

        protected virtual void OnDestroy()
        {
            // 只有当销毁的是当前单例实例时才清理
            if (_instance == this)
            {
                OnDispose();
                _instance = null;
                _isInitialized = false;
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _isApplicationQuitting = true;
        }

        // ========== 可重写的生命周期方法 ==========

        /// <summary>
        /// 初始化回调（在Awake中调用，只执行一次）
        /// 子类重写此方法进行初始化工作，替代Awake
        /// </summary>
        protected virtual void OnInit()
        {
        }

        /// <summary>
        /// 清理回调（在OnDestroy中调用）
        /// 子类重写此方法进行资源清理、事件取消订阅等
        /// </summary>
        protected virtual void OnDispose()
        {
        }

        // ========== 公共方法 ==========

        /// <summary>
        /// 手动预创建单例实例
        /// 在GameManager中可用于控制初始化顺序
        /// </summary>
        public static void Preload()
        {
            // 访问Instance属性会自动创建
            var _ = Instance;
        }

        /// <summary>
        /// 重置应用退出标记（仅编辑器中需要，Play模式退出后重新进入）
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _isApplicationQuitting = false;
            _instance = null;
        }
    }

    // ====================================================================

    /// <summary>
    /// 纯C#单例基类（不依赖MonoBehaviour）
    /// 适用于不需要Unity生命周期回调的纯逻辑管理器
    /// 特性：
    /// 1. 懒加载（首次访问时创建）
    /// 2. 线程安全
    /// 3. 支持手动初始化和销毁
    /// 使用示例：
    ///   public class ConfigLoader : Singleton&lt;ConfigLoader&gt; { }
    ///   ConfigLoader.Instance.LoadAll();
    /// </summary>
    /// <typeparam name="T">子类类型，必须有无参构造函数</typeparam>
    public abstract class Singleton<T> where T : Singleton<T>, new()
    {
        // ========== 私有字段 ==========

        /// <summary>单例实例</summary>
        private static T _instance;

        /// <summary>锁对象</summary>
        private static readonly object _lock = new object();

        /// <summary>是否已初始化</summary>
        private bool _isInitialized = false;

        // ========== 公共属性 ==========

        /// <summary>
        /// 获取单例实例（线程安全，懒加载）
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new T();
                        }
                    }
                }

                return _instance;
            }
        }

        /// <summary>
        /// 检查单例是否已创建（不会触发自动创建）
        /// </summary>
        public static bool HasInstance => _instance != null;

        /// <summary>是否已初始化</summary>
        public bool IsInitialized => _isInitialized;

        // ========== 公共方法 ==========

        /// <summary>
        /// 初始化单例（手动调用，用于有依赖顺序的初始化场景）
        /// 可多次安全调用，只有首次会执行OnInit
        /// </summary>
        public void Initialize()
        {
            if (!_isInitialized)
            {
                OnInit();
                _isInitialized = true;
            }
        }

        /// <summary>
        /// 销毁单例（手动调用，释放资源）
        /// </summary>
        public void Dispose()
        {
            if (_isInitialized)
            {
                OnDispose();
                _isInitialized = false;
            }

            if (_instance == (T)this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 手动预创建单例实例
        /// </summary>
        public static void Preload()
        {
            var _ = Instance;
        }

        // ========== 可重写的生命周期方法 ==========

        /// <summary>
        /// 初始化回调
        /// 子类重写此方法进行初始化工作
        /// </summary>
        protected virtual void OnInit()
        {
        }

        /// <summary>
        /// 清理回调
        /// 子类重写此方法进行资源释放
        /// </summary>
        protected virtual void OnDispose()
        {
        }
    }
}
