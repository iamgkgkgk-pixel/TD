
// ============================================================
// 文件名：ObjectPool.cs
// 功能描述：通用对象池系统
//          支持GameObject预制体池化和纯C#对象池化
//          微信小游戏GC敏感，高频创建/销毁的对象必须走对象池
// 创建时间：2026-03-25
// 所属模块：Framework
// 对应交互：阶段二 #46
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetheraSurvivors.Framework
{
    /// <summary>
    /// 可池化接口 — 需要使用对象池的组件应实现此接口
    /// </summary>
    public interface IPoolable
    {
        /// <summary>从池中取出时调用（替代Awake/OnEnable的初始化）</summary>
        void OnSpawn();

        /// <summary>回收到池中时调用（替代OnDestroy的清理）</summary>
        void OnDespawn();
    }

    /// <summary>
    /// GameObject对象池 — 管理单个预制体的对象池
    /// 负责指定预制体的创建、回收、预热
    /// </summary>
    public class GameObjectPool
    {
        // ========== 私有字段 ==========

        /// <summary>预制体引用</summary>
        private readonly GameObject _prefab;

        /// <summary>池中空闲的对象</summary>
        private readonly Stack<GameObject> _inactiveObjects;

        /// <summary>当前活跃（被取出）的对象</summary>
        private readonly HashSet<GameObject> _activeObjects;

        /// <summary>池的父节点（隐藏回收的对象）</summary>
        private readonly Transform _poolRoot;

        /// <summary>池的最大容量（0表示无限制）</summary>
        private readonly int _maxSize;

        // ========== 公共属性 ==========

        /// <summary>池中空闲对象数量</summary>
        public int InactiveCount => _inactiveObjects.Count;

        /// <summary>活跃对象数量</summary>
        public int ActiveCount => _activeObjects.Count;

        /// <summary>总对象数量</summary>
        public int TotalCount => InactiveCount + ActiveCount;

        /// <summary>预制体名称</summary>
        public string PrefabName => _prefab != null ? _prefab.name : "null";

        // ========== 构造函数 ==========

        /// <summary>
        /// 创建一个GameObject对象池
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="poolRoot">池的父节点</param>
        /// <param name="initialSize">初始预热数量</param>
        /// <param name="maxSize">最大容量（0=无限制）</param>
        public GameObjectPool(GameObject prefab, Transform poolRoot, int initialSize = 0, int maxSize = 0)
        {
            _prefab = prefab;
            _poolRoot = poolRoot;
            _maxSize = maxSize;
            _inactiveObjects = new Stack<GameObject>(Mathf.Max(initialSize, 8));
            _activeObjects = new HashSet<GameObject>();

            // 预热
            if (initialSize > 0)
            {
                PreWarm(initialSize);
            }
        }

        // ========== 公共方法 ==========

        /// <summary>
        /// 从池中获取一个对象
        /// 如果池为空，自动创建新对象
        /// </summary>
        /// <param name="position">初始位置</param>
        /// <param name="rotation">初始旋转</param>
        /// <param name="parent">父节点（null则放到池根节点）</param>
        /// <returns>激活的GameObject</returns>
        public GameObject Get(Vector3 position = default, Quaternion rotation = default, Transform parent = null)
        {
            GameObject obj;

            if (_inactiveObjects.Count > 0)
            {
                // 从池中取出
                obj = _inactiveObjects.Pop();

                // 检查是否被意外销毁
                if (obj == null)
                {
                    Debug.LogWarning($"[ObjectPool] 池中对象已被销毁，重新创建: {PrefabName}");
                    obj = CreateNewObject();
                }
            }
            else
            {
                // 池为空，创建新对象
                obj = CreateNewObject();
            }

            // 设置位置和激活
            var transform = obj.transform;
            transform.SetParent(parent);
            transform.position = position;
            transform.rotation = rotation;
            transform.localScale = Vector3.one;
            obj.SetActive(true);

            _activeObjects.Add(obj);

            // 通知可池化组件
            NotifySpawn(obj);

            return obj;
        }

        /// <summary>
        /// 回收一个对象到池中
        /// </summary>
        /// <param name="obj">要回收的对象</param>
        public void Return(GameObject obj)
        {
            if (obj == null) return;

            if (!_activeObjects.Remove(obj))
            {
                Debug.LogWarning($"[ObjectPool] 尝试回收不属于本池的对象: {obj.name}");
                return;
            }

            // 通知可池化组件
            NotifyDespawn(obj);

            // 超过最大容量则直接销毁
            if (_maxSize > 0 && _inactiveObjects.Count >= _maxSize)
            {
                UnityEngine.Object.Destroy(obj);
                return;
            }

            // 回收：隐藏并放回池中
            obj.SetActive(false);
            obj.transform.SetParent(_poolRoot);
            _inactiveObjects.Push(obj);
        }

        /// <summary>
        /// 回收所有活跃对象
        /// </summary>
        public void ReturnAll()
        {
            // 复制到临时列表避免迭代中修改集合
            var tempList = new List<GameObject>(_activeObjects);
            for (int i = 0; i < tempList.Count; i++)
            {
                Return(tempList[i]);
            }
        }

        /// <summary>
        /// 预热：预创建指定数量的对象放入池中
        /// 建议在Loading阶段调用，避免运行时创建卡顿
        /// </summary>
        /// <param name="count">预热数量</param>
        public void PreWarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_maxSize > 0 && TotalCount >= _maxSize) break;

                var obj = CreateNewObject();
                obj.SetActive(false);
                obj.transform.SetParent(_poolRoot);
                _inactiveObjects.Push(obj);
            }
        }

        /// <summary>
        /// 清空池（销毁所有对象）
        /// </summary>
        public void Clear()
        {
            // 销毁活跃对象
            foreach (var obj in _activeObjects)
            {
                if (obj != null)
                {
                    UnityEngine.Object.Destroy(obj);
                }
            }
            _activeObjects.Clear();

            // 销毁空闲对象
            while (_inactiveObjects.Count > 0)
            {
                var obj = _inactiveObjects.Pop();
                if (obj != null)
                {
                    UnityEngine.Object.Destroy(obj);
                }
            }
        }

        // ========== 私有方法 ==========

        /// <summary>创建新对象实例</summary>
        private GameObject CreateNewObject()
        {
            var obj = UnityEngine.Object.Instantiate(_prefab);
            obj.name = _prefab.name; // 移除"(Clone)"后缀，便于调试
            return obj;
        }

        /// <summary>通知所有IPoolable组件：被取出</summary>
        private void NotifySpawn(GameObject obj)
        {
            // 使用GetComponentsInChildren会产生GC，这里用缓存方式
            var poolables = obj.GetComponents<IPoolable>();
            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnSpawn();
            }
        }

        /// <summary>通知所有IPoolable组件：被回收</summary>
        private void NotifyDespawn(GameObject obj)
        {
            var poolables = obj.GetComponents<IPoolable>();
            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnDespawn();
            }
        }
    }

    // ====================================================================

    /// <summary>
    /// 对象池管理器 — 全局单例，管理所有GameObject对象池
    /// 
    /// 使用示例：
    ///   // 1. 注册预制体池（通常在Loading时）
    ///   ObjectPoolManager.Instance.CreatePool(enemyPrefab, initialSize: 20, maxSize: 100);
    ///   
    ///   // 2. 从池中获取
    ///   var enemy = ObjectPoolManager.Instance.Get(enemyPrefab, spawnPos, Quaternion.identity);
    ///   
    ///   // 3. 回收
    ///   ObjectPoolManager.Instance.Return(enemyPrefab, enemy);
    ///   
    ///   // 4. 或者使用扩展方法（更简洁）
    ///   enemy.ReturnToPool();  // 需要在对象上挂PoolableObject组件
    /// </summary>
    public class ObjectPoolManager : MonoSingleton<ObjectPoolManager>
    {
        // ========== 私有字段 ==========

        /// <summary>预制体InstanceID → 对象池 的映射</summary>
        private readonly Dictionary<int, GameObjectPool> _pools = new Dictionary<int, GameObjectPool>();

        /// <summary>已取出的对象 → 所属池的预制体ID 的反向映射（用于通过对象找回池）</summary>
        private readonly Dictionary<int, int> _objectToPoolMap = new Dictionary<int, int>();

        /// <summary>池的根节点（隐藏回收的对象）</summary>
        private Transform _poolRoot;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            // 创建池根节点
            _poolRoot = new GameObject("[ObjectPoolRoot]").transform;
            _poolRoot.SetParent(transform);
            _poolRoot.gameObject.SetActive(false); // 隐藏根节点下所有对象
        }

        protected override void OnDispose()
        {
            ClearAll();
        }

        // ========== 公共方法：池管理 ==========

        /// <summary>
        /// 创建/注册一个预制体对象池
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="initialSize">初始预热数量</param>
        /// <param name="maxSize">最大容量（0=无限制）</param>
        public void CreatePool(GameObject prefab, int initialSize = 0, int maxSize = 0)
        {
            if (prefab == null)
            {
                Debug.LogError("[ObjectPoolManager] CreatePool: prefab不能为null");
                return;
            }

            int prefabId = prefab.GetInstanceID();

            if (_pools.ContainsKey(prefabId))
            {
                Debug.LogWarning($"[ObjectPoolManager] 池已存在: {prefab.name}");
                return;
            }

            // 为每个池创建一个专属的父节点（方便Hierarchy中查看）
            var poolParent = new GameObject($"Pool_{prefab.name}").transform;
            poolParent.SetParent(_poolRoot);

            var pool = new GameObjectPool(prefab, poolParent, initialSize, maxSize);
            _pools[prefabId] = pool;
        }

        /// <summary>
        /// 从池中获取一个对象
        /// 如果池不存在，会自动创建池
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="position">位置</param>
        /// <param name="rotation">旋转</param>
        /// <param name="parent">父节点</param>
        /// <returns>池中取出的GameObject</returns>
        public GameObject Get(GameObject prefab, Vector3 position = default, Quaternion rotation = default, Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[ObjectPoolManager] Get: prefab不能为null");
                return null;
            }

            int prefabId = prefab.GetInstanceID();

            // 如果池不存在，自动创建
            if (!_pools.ContainsKey(prefabId))
            {
                CreatePool(prefab);
            }

            var obj = _pools[prefabId].Get(position, rotation, parent);

            // 记录反向映射
            if (obj != null)
            {
                _objectToPoolMap[obj.GetInstanceID()] = prefabId;
            }

            return obj;
        }

        /// <summary>
        /// 回收一个对象（通过预制体引用指定目标池）
        /// </summary>
        /// <param name="prefab">所属的预制体</param>
        /// <param name="obj">要回收的对象</param>
        public void Return(GameObject prefab, GameObject obj)
        {
            if (prefab == null || obj == null) return;

            int prefabId = prefab.GetInstanceID();

            if (_pools.TryGetValue(prefabId, out var pool))
            {
                pool.Return(obj);
                _objectToPoolMap.Remove(obj.GetInstanceID());
            }
            else
            {
                Debug.LogWarning($"[ObjectPoolManager] 找不到池: {prefab.name}，直接销毁对象");
                Destroy(obj);
            }
        }

        /// <summary>
        /// 回收一个对象（自动查找所属池）
        /// ⚠️ 略慢于指定prefab的版本，因为需要查找反向映射
        /// </summary>
        /// <param name="obj">要回收的对象</param>
        public void Return(GameObject obj)
        {
            if (obj == null) return;

            int objId = obj.GetInstanceID();

            if (_objectToPoolMap.TryGetValue(objId, out int prefabId))
            {
                if (_pools.TryGetValue(prefabId, out var pool))
                {
                    pool.Return(obj);
                }
                _objectToPoolMap.Remove(objId);
            }
            else
            {
                Debug.LogWarning($"[ObjectPoolManager] 对象 {obj.name} 不在任何池中，直接销毁");
                Destroy(obj);
            }
        }

        /// <summary>
        /// 回收指定预制体池的所有活跃对象
        /// </summary>
        /// <param name="prefab">预制体</param>
        public void ReturnAll(GameObject prefab)
        {
            if (prefab == null) return;

            int prefabId = prefab.GetInstanceID();

            if (_pools.TryGetValue(prefabId, out var pool))
            {
                pool.ReturnAll();
            }
        }

        /// <summary>
        /// 回收所有池的所有活跃对象
        /// 通常在场景切换时调用
        /// </summary>
        public void ReturnAll()
        {
            foreach (var pair in _pools)
            {
                pair.Value.ReturnAll();
            }
            _objectToPoolMap.Clear();
        }

        /// <summary>
        /// 销毁指定预制体的池
        /// </summary>
        /// <param name="prefab">预制体</param>
        public void DestroyPool(GameObject prefab)
        {
            if (prefab == null) return;

            int prefabId = prefab.GetInstanceID();

            if (_pools.TryGetValue(prefabId, out var pool))
            {
                pool.Clear();
                _pools.Remove(prefabId);
            }
        }

        /// <summary>
        /// 清除所有池
        /// </summary>
        public void ClearAll()
        {
            foreach (var pair in _pools)
            {
                pair.Value.Clear();
            }
            _pools.Clear();
            _objectToPoolMap.Clear();
        }

        // ========== 调试方法 ==========

        /// <summary>
        /// 获取所有池的调试信息
        /// </summary>
        /// <returns>池名称 → (活跃数, 空闲数, 总数) 的字典</returns>
        public Dictionary<string, (int active, int inactive, int total)> GetDebugInfo()
        {
            var info = new Dictionary<string, (int, int, int)>();
            foreach (var pair in _pools)
            {
                var pool = pair.Value;
                info[pool.PrefabName] = (pool.ActiveCount, pool.InactiveCount, pool.TotalCount);
            }
            return info;
        }
    }

    // ====================================================================

    /// <summary>
    /// 纯C#通用对象池（不依赖GameObject）
    /// 适用于：事件对象、临时数据结构、计算缓存等
    /// 
    /// 使用示例：
    ///   var listPool = new GenericPool&lt;List&lt;int&gt;&gt;(
    ///       createFunc: () => new List&lt;int&gt;(16),
    ///       onGet: list => list.Clear(),
    ///       onReturn: list => list.Clear()
    ///   );
    ///   
    ///   var list = listPool.Get();
    ///   // 使用list...
    ///   listPool.Return(list);
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    public class GenericPool<T> where T : class
    {
        // ========== 私有字段 ==========

        private readonly Stack<T> _pool;
        private readonly Func<T> _createFunc;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onReturn;
        private readonly int _maxSize;

        // ========== 公共属性 ==========

        /// <summary>池中空闲对象数量</summary>
        public int Count => _pool.Count;

        // ========== 构造函数 ==========

        /// <summary>
        /// 创建纯C#通用对象池
        /// </summary>
        /// <param name="createFunc">创建新对象的工厂方法</param>
        /// <param name="onGet">对象被取出时的回调</param>
        /// <param name="onReturn">对象被回收时的回调</param>
        /// <param name="initialSize">初始预热数量</param>
        /// <param name="maxSize">最大容量（0=无限制）</param>
        public GenericPool(Func<T> createFunc, Action<T> onGet = null, Action<T> onReturn = null,
                           int initialSize = 0, int maxSize = 0)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            _onGet = onGet;
            _onReturn = onReturn;
            _maxSize = maxSize;
            _pool = new Stack<T>(Mathf.Max(initialSize, 8));

            // 预热
            for (int i = 0; i < initialSize; i++)
            {
                _pool.Push(_createFunc());
            }
        }

        /// <summary>
        /// 从池中获取一个对象
        /// </summary>
        public T Get()
        {
            var obj = _pool.Count > 0 ? _pool.Pop() : _createFunc();
            _onGet?.Invoke(obj);
            return obj;
        }

        /// <summary>
        /// 回收一个对象到池中
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null) return;

            _onReturn?.Invoke(obj);

            if (_maxSize > 0 && _pool.Count >= _maxSize) return;

            _pool.Push(obj);
        }

        /// <summary>
        /// 清空池
        /// </summary>
        public void Clear()
        {
            _pool.Clear();
        }
    }

    // ====================================================================

    /// <summary>
    /// 可池化对象组件 — 挂载在需要池化的GameObject上
    /// 提供快捷的回收方法：gameObject.GetComponent&lt;PoolableObject&gt;().ReturnToPool()
    /// </summary>
    public class PoolableObject : MonoBehaviour
    {
        /// <summary>
        /// 将自身回收到对象池
        /// </summary>
        public void ReturnToPool()
        {
            if (ObjectPoolManager.HasInstance)
            {
                ObjectPoolManager.Instance.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 延迟回收（指定秒数后自动回收）
        /// </summary>
        /// <param name="delay">延迟秒数</param>
        public void ReturnToPoolDelayed(float delay)
        {
            Invoke(nameof(ReturnToPool), delay);
        }
    }
}
