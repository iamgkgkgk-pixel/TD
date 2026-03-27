
// ============================================================
// 文件名：ResourceManager.cs
// 功能描述：资源管理器 — 统一的资源加载/缓存/卸载系统
//          支持同步/异步加载、引用计数、自动卸载
//          适配微信小游戏：不使用StreamingAssets，走Resources加载
// 创建时间：2026-03-25
// 所属模块：Framework
// 对应交互：阶段二 #47
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AetheraSurvivors.Framework
{
    /// <summary>
    /// 资源管理器 — 全局单例
    /// 
    /// 职责：
    /// 1. 统一管理Resources资源的加载和卸载
    /// 2. 引用计数，避免重复加载和提前卸载
    /// 3. 支持同步/异步加载
    /// 4. 自动清理不再使用的资源
    /// 
    /// 微信小游戏适配：
    /// - 不能使用StreamingAssets，所有资源走Resources目录
    /// - 内存敏感（256MB限制），引用计数+自动卸载
    /// - 未来大资源走CDN+微信文件系统缓存
    /// 
    /// 使用示例：
    ///   // 同步加载
    ///   var prefab = ResourceManager.Instance.Load&lt;GameObject&gt;("Prefabs/Towers/ArcherTower");
    ///   
    ///   // 异步加载
    ///   ResourceManager.Instance.LoadAsync&lt;Sprite&gt;("Sprites/UI/icon_gold", sprite => {
    ///       myImage.sprite = sprite;
    ///   });
    ///   
    ///   // 释放（引用计数-1）
    ///   ResourceManager.Instance.Release("Prefabs/Towers/ArcherTower");
    ///   
    ///   // 卸载所有无引用的资源
    ///   ResourceManager.Instance.UnloadUnused();
    /// </summary>
    public class ResourceManager : MonoSingleton<ResourceManager>
    {
        // ========== 内部类 ==========

        /// <summary>资源缓存条目</summary>
        private class ResourceEntry
        {
            /// <summary>资源引用</summary>
            public UnityEngine.Object Asset;

            /// <summary>引用计数</summary>
            public int RefCount;

            /// <summary>最后访问时间</summary>
            public float LastAccessTime;

            /// <summary>资源类型名（调试用）</summary>
            public string TypeName;
        }

        // ========== 常量 ==========

        /// <summary>自动卸载检查间隔（秒）</summary>
        private const float AutoUnloadInterval = 60f;

        /// <summary>资源空闲超时（秒）：超过此时间且引用计数为0则自动卸载</summary>
        private const float IdleTimeout = 120f;

        // ========== 私有字段 ==========

        /// <summary>资源路径 → 缓存条目</summary>
        private readonly Dictionary<string, ResourceEntry> _cache = new Dictionary<string, ResourceEntry>(64);

        /// <summary>自动卸载计时器</summary>
        private float _autoUnloadTimer;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            _autoUnloadTimer = 0f;
        }

        protected override void OnDispose()
        {
            ClearAll();
        }

        private void Update()
        {
            // 定时检查自动卸载
            _autoUnloadTimer += Time.deltaTime;
            if (_autoUnloadTimer >= AutoUnloadInterval)
            {
                _autoUnloadTimer = 0f;
                AutoUnloadIdle();
            }
        }

        // ========== 公共方法：同步加载 ==========

        /// <summary>
        /// 同步加载资源（从Resources目录）
        /// 如果已缓存则直接返回，引用计数+1
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">Resources下的相对路径（不含后缀）</param>
        /// <returns>加载的资源，失败返回null</returns>
        public T Load<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[ResourceManager] Load: path不能为空");
                return null;
            }

            // 检查缓存
            if (_cache.TryGetValue(path, out var entry))
            {
                entry.RefCount++;
                entry.LastAccessTime = Time.realtimeSinceStartup;
                return entry.Asset as T;
            }

            // 从Resources加载
            var asset = Resources.Load<T>(path);

            if (asset == null)
            {
                Debug.LogWarning($"[ResourceManager] 资源加载失败: {path}");
                return null;
            }

            // 加入缓存
            _cache[path] = new ResourceEntry
            {
                Asset = asset,
                RefCount = 1,
                LastAccessTime = Time.realtimeSinceStartup,
                TypeName = typeof(T).Name
            };

            return asset;
        }

        /// <summary>
        /// 同步加载并实例化预制体
        /// </summary>
        /// <param name="path">Resources下的预制体路径</param>
        /// <param name="parent">父节点</param>
        /// <returns>实例化的GameObject</returns>
        public GameObject LoadAndInstantiate(string path, Transform parent = null)
        {
            var prefab = Load<GameObject>(path);
            if (prefab == null) return null;

            var go = Instantiate(prefab, parent);
            go.name = prefab.name; // 移除(Clone)后缀
            return go;
        }

        // ========== 公共方法：异步加载 ==========

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">Resources下的相对路径</param>
        /// <param name="onComplete">加载完成回调</param>
        /// <param name="onProgress">加载进度回调（0-1）</param>
        public void LoadAsync<T>(string path, Action<T> onComplete, Action<float> onProgress = null)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[ResourceManager] LoadAsync: path不能为空");
                onComplete?.Invoke(null);
                return;
            }

            // 检查缓存
            if (_cache.TryGetValue(path, out var entry))
            {
                entry.RefCount++;
                entry.LastAccessTime = Time.realtimeSinceStartup;
                onProgress?.Invoke(1f);
                onComplete?.Invoke(entry.Asset as T);
                return;
            }

            // 启动异步加载协程
            StartCoroutine(LoadAsyncCoroutine(path, onComplete, onProgress));
        }

        /// <summary>
        /// 批量异步加载资源
        /// </summary>
        /// <param name="paths">资源路径列表</param>
        /// <param name="onAllComplete">全部加载完成回调</param>
        /// <param name="onProgress">总体进度回调（0-1）</param>
        public void LoadBatchAsync(List<string> paths, Action onAllComplete, Action<float> onProgress = null)
        {
            if (paths == null || paths.Count == 0)
            {
                onProgress?.Invoke(1f);
                onAllComplete?.Invoke();
                return;
            }

            StartCoroutine(LoadBatchCoroutine(paths, onAllComplete, onProgress));
        }

        // ========== 公共方法：释放 ==========

        /// <summary>
        /// 释放资源引用（引用计数-1）
        /// 当引用计数归零时资源不会立即卸载，会在下次自动清理时卸载
        /// </summary>
        /// <param name="path">资源路径</param>
        public void Release(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (_cache.TryGetValue(path, out var entry))
            {
                entry.RefCount = Mathf.Max(0, entry.RefCount - 1);
            }
        }

        /// <summary>
        /// 立即卸载所有引用计数为0的资源
        /// 建议在场景切换时调用
        /// </summary>
        public void UnloadUnused()
        {
            var toRemove = new List<string>();

            foreach (var pair in _cache)
            {
                if (pair.Value.RefCount <= 0)
                {
                    toRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                _cache.Remove(toRemove[i]);
            }

            if (toRemove.Count > 0)
            {
                Resources.UnloadUnusedAssets();
                Debug.Log($"[ResourceManager] 卸载了 {toRemove.Count} 个无引用资源");
            }
        }

        /// <summary>
        /// 强制清除所有缓存（通常只在游戏退出时调用）
        /// </summary>
        public void ClearAll()
        {
            _cache.Clear();
            Resources.UnloadUnusedAssets();
        }

        // ========== 公共方法：查询 ==========

        /// <summary>
        /// 检查资源是否已缓存
        /// </summary>
        public bool IsCached(string path)
        {
            return _cache.ContainsKey(path);
        }

        /// <summary>
        /// 获取资源的当前引用计数
        /// </summary>
        public int GetRefCount(string path)
        {
            if (_cache.TryGetValue(path, out var entry))
            {
                return entry.RefCount;
            }
            return 0;
        }

        /// <summary>
        /// 获取当前缓存的资源总数（调试用）
        /// </summary>
        public int CachedCount => _cache.Count;

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public Dictionary<string, (string type, int refCount)> GetDebugInfo()
        {
            var info = new Dictionary<string, (string, int)>();
            foreach (var pair in _cache)
            {
                info[pair.Key] = (pair.Value.TypeName, pair.Value.RefCount);
            }
            return info;
        }

        // ========== 私有方法 ==========

        /// <summary>异步加载协程</summary>
        private IEnumerator LoadAsyncCoroutine<T>(string path, Action<T> onComplete, Action<float> onProgress)
            where T : UnityEngine.Object
        {
            var request = Resources.LoadAsync<T>(path);

            while (!request.isDone)
            {
                onProgress?.Invoke(request.progress);
                yield return null;
            }

            onProgress?.Invoke(1f);

            var asset = request.asset as T;
            if (asset == null)
            {
                Debug.LogWarning($"[ResourceManager] 异步加载失败: {path}");
                onComplete?.Invoke(null);
                yield break;
            }

            // 加入缓存（异步完成时再次检查，防止同时发起重复请求）
            if (!_cache.ContainsKey(path))
            {
                _cache[path] = new ResourceEntry
                {
                    Asset = asset,
                    RefCount = 1,
                    LastAccessTime = Time.realtimeSinceStartup,
                    TypeName = typeof(T).Name
                };
            }
            else
            {
                _cache[path].RefCount++;
                _cache[path].LastAccessTime = Time.realtimeSinceStartup;
            }

            onComplete?.Invoke(asset);
        }

        /// <summary>批量异步加载协程</summary>
        private IEnumerator LoadBatchCoroutine(List<string> paths, Action onAllComplete, Action<float> onProgress)
        {
            int total = paths.Count;
            int completed = 0;

            for (int i = 0; i < total; i++)
            {
                var path = paths[i];

                // 已缓存的直接跳过
                if (_cache.ContainsKey(path))
                {
                    _cache[path].RefCount++;
                    completed++;
                    onProgress?.Invoke((float)completed / total);
                    continue;
                }

                var request = Resources.LoadAsync(path);
                yield return request;

                if (request.asset != null)
                {
                    _cache[path] = new ResourceEntry
                    {
                        Asset = request.asset,
                        RefCount = 1,
                        LastAccessTime = Time.realtimeSinceStartup,
                        TypeName = request.asset.GetType().Name
                    };
                }

                completed++;
                onProgress?.Invoke((float)completed / total);
            }

            onAllComplete?.Invoke();
        }

        /// <summary>
        /// 自动卸载空闲资源
        /// 引用计数为0且超过IdleTimeout的资源会被卸载
        /// </summary>
        private void AutoUnloadIdle()
        {
            float now = Time.realtimeSinceStartup;
            var toRemove = new List<string>();

            foreach (var pair in _cache)
            {
                if (pair.Value.RefCount <= 0 && (now - pair.Value.LastAccessTime) > IdleTimeout)
                {
                    toRemove.Add(pair.Key);
                }
            }

            if (toRemove.Count > 0)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    _cache.Remove(toRemove[i]);
                }

                Resources.UnloadUnusedAssets();
                Debug.Log($"[ResourceManager] 自动卸载了 {toRemove.Count} 个空闲资源");
            }
        }
    }
}
