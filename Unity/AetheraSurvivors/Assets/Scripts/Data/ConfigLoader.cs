
// ============================================================
// 文件名：ConfigLoader.cs
// 功能描述：配置表加载系统 — 从JSON加载配置、自动反序列化
//          支持热重载（编辑器中）
// 创建时间：2026-03-25
// 所属模块：Data
// 对应交互：阶段二 #55
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetheraSurvivors.Data
{
    /// <summary>
    /// 配置表加载器 — 全局单例
    /// 
    /// 职责：
    /// 1. 从Resources/Configs/目录加载JSON配置文件
    /// 2. 反序列化为强类型C#对象
    /// 3. 缓存已加载的配置
    /// 4. 支持编辑器热重载
    /// 
    /// 使用示例：
    ///   // 加载单个配置
    ///   var towerCfg = ConfigLoader.Instance.Load&lt;TowerConfigTable&gt;("Towers/tower_config");
    ///   
    ///   // 获取已缓存的配置
    ///   var enemyCfg = ConfigLoader.Instance.Get&lt;EnemyConfigTable&gt;();
    ///   
    ///   // 加载所有配置（启动时）
    ///   ConfigLoader.Instance.LoadAll();
    /// </summary>
    public class ConfigLoader : AetheraSurvivors.Framework.Singleton<ConfigLoader>
    {
        // ========== 常量 ==========

        /// <summary>配置文件路径前缀</summary>
        private const string ConfigPath = "Configs/";

        // ========== 私有字段 ==========

        /// <summary>配置类型 → 配置对象缓存</summary>
        private readonly Dictionary<Type, object> _configCache = new Dictionary<Type, object>();

        /// <summary>配置路径 → JSON原文缓存（用于热重载检测）</summary>
        private readonly Dictionary<string, string> _jsonCache = new Dictionary<string, string>();

        // ========== 公共方法 ==========

        /// <summary>
        /// 加载配置文件并反序列化
        /// </summary>
        /// <typeparam name="T">配置数据类型</typeparam>
        /// <param name="path">Configs/下的相对路径（不含后缀）</param>
        /// <returns>配置对象</returns>
        public T Load<T>(string path) where T : class
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[ConfigLoader] Load: path不能为空");
                return null;
            }

            string fullPath = ConfigPath + path;

            // 加载JSON文件
            var textAsset = Resources.Load<TextAsset>(fullPath);
            if (textAsset == null)
            {
                Debug.LogError($"[ConfigLoader] 配置文件不存在: {fullPath}");
                return null;
            }

            string json = textAsset.text;
            _jsonCache[path] = json;

            try
            {
                // 反序列化
                T config = JsonUtility.FromJson<T>(json);

                if (config == null)
                {
                    Debug.LogError($"[ConfigLoader] 反序列化失败: {fullPath}");
                    return null;
                }

                // 缓存
                _configCache[typeof(T)] = config;

                Debug.Log($"[ConfigLoader] 加载配置成功: {fullPath}");
                return config;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigLoader] 反序列化异常: {fullPath}, {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从JSON字符串加载配置（用于服务端热更下发）
        /// </summary>
        /// <typeparam name="T">配置数据类型</typeparam>
        /// <param name="json">JSON字符串</param>
        /// <returns>配置对象</returns>
        public T LoadFromJson<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[ConfigLoader] LoadFromJson: json不能为空");
                return null;
            }

            try
            {
                T config = JsonUtility.FromJson<T>(json);
                if (config != null)
                {
                    _configCache[typeof(T)] = config;
                }
                return config;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigLoader] 从JSON加载失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取已缓存的配置（不重新加载）
        /// </summary>
        /// <typeparam name="T">配置数据类型</typeparam>
        /// <returns>配置对象，未加载则返回null</returns>
        public T Get<T>() where T : class
        {
            if (_configCache.TryGetValue(typeof(T), out var config))
            {
                return config as T;
            }

            Debug.LogWarning($"[ConfigLoader] 配置未加载: {typeof(T).Name}，请先调用Load");
            return null;
        }

        /// <summary>
        /// 加载所有核心配置（启动时调用）
        /// 此方法会在后续阶段逐步补充加载的配置类型
        /// </summary>
        public void LoadAll()
        {
            Debug.Log("[ConfigLoader] 开始加载所有配置...");

            // TODO: 后续阶段添加具体配置加载
            // Load<TowerConfigTable>("Towers/tower_config");
            // Load<EnemyConfigTable>("Enemies/enemy_config");
            // Load<RuneConfigTable>("Runes/rune_config");
            // Load<WaveConfigTable>("Waves/wave_config");
            // Load<LevelConfigTable>("Levels/level_config");

            Debug.Log($"[ConfigLoader] 所有配置加载完成，共 {_configCache.Count} 个");
        }

        /// <summary>
        /// 重新加载指定配置（热重载）
        /// </summary>
        /// <typeparam name="T">配置数据类型</typeparam>
        /// <param name="path">配置路径</param>
        /// <returns>重新加载的配置对象</returns>
        public T Reload<T>(string path) where T : class
        {
            // 清除缓存
            _configCache.Remove(typeof(T));
            _jsonCache.Remove(path);

            // 重新加载
            return Load<T>(path);
        }

        /// <summary>
        /// 检查配置是否已加载
        /// </summary>
        public bool IsLoaded<T>() where T : class
        {
            return _configCache.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 获取所有已加载配置的类型名列表（调试用）
        /// </summary>
        public List<string> GetLoadedConfigNames()
        {
            var names = new List<string>();
            foreach (var pair in _configCache)
            {
                names.Add(pair.Key.Name);
            }
            return names;
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearAll()
        {
            _configCache.Clear();
            _jsonCache.Clear();
        }

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            // 可在此处调用LoadAll()预加载所有配置
        }

        protected override void OnDispose()
        {
            ClearAll();
        }
    }
}
