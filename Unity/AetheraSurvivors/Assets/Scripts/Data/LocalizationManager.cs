
// ============================================================
// 文件名：LocalizationManager.cs
// 功能描述：多语言系统 — 支持中/英文切换、Key-Value管理、动态参数替换
// 创建时间：2026-03-25
// 所属模块：Data
// 对应交互：阶段二 #58
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetheraSurvivors.Data
{
    /// <summary>
    /// 多语言管理器 — 全局单例
    /// 
    /// 使用示例：
    ///   // 获取文本
    ///   string text = LocalizationManager.Instance.Get("ui_confirm");
    ///   
    ///   // 带参数替换
    ///   string text = LocalizationManager.Instance.Get("wave_info", currentWave, totalWave);
    ///   // 配置中："wave_info": "第 {0} 波 / 共 {1} 波"
    ///   // 结果："第 3 波 / 共 10 波"
    ///   
    ///   // 切换语言
    ///   LocalizationManager.Instance.SetLanguage("en");
    /// </summary>
    public class LocalizationManager : AetheraSurvivors.Framework.Singleton<LocalizationManager>
    {
        // ========== 常量 ==========

        /// <summary>默认语言</summary>
        private const string DefaultLanguage = "zh-CN";

        /// <summary>多语言配置文件路径前缀</summary>
        private const string LangPath = "Configs/Lang/";

        // ========== 私有字段 ==========

        /// <summary>当前语言</summary>
        private string _currentLanguage;

        /// <summary>Key → 翻译文本映射</summary>
        private readonly Dictionary<string, string> _translations = new Dictionary<string, string>(256);

        /// <summary>语言切换事件</summary>
        public event Action<string> OnLanguageChanged;

        // ========== 公共属性 ==========

        /// <summary>当前语言代码</summary>
        public string CurrentLanguage => _currentLanguage;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            // 加载默认语言
            SetLanguage(DefaultLanguage);
        }

        protected override void OnDispose()
        {
            _translations.Clear();
            OnLanguageChanged = null;
        }

        // ========== 公共方法 ==========

        /// <summary>
        /// 切换语言
        /// </summary>
        /// <param name="languageCode">语言代码（如"zh-CN"、"en"）</param>
        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode)) return;

            _currentLanguage = languageCode;
            _translations.Clear();

            // 加载语言文件
            string path = LangPath + languageCode;
            var textAsset = Resources.Load<TextAsset>(path);

            if (textAsset != null)
            {
                // 解析JSON格式的语言文件
                ParseLanguageFile(textAsset.text);
                Debug.Log($"[Localization] 语言切换为: {languageCode}, 加载 {_translations.Count} 条文本");
            }
            else
            {
                Debug.LogWarning($"[Localization] 语言文件不存在: {path}，使用空文本");
            }

            // 通知所有监听者
            OnLanguageChanged?.Invoke(languageCode);
        }

        /// <summary>
        /// 获取翻译文本
        /// </summary>
        /// <param name="key">文本Key</param>
        /// <returns>翻译后的文本，未找到则返回key本身</returns>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (_translations.TryGetValue(key, out var text))
            {
                return text;
            }

            // 未找到翻译，返回key本身（便于识别缺失的翻译）
            Debug.LogWarning($"[Localization] 翻译缺失: {key}");
            return key;
        }

        /// <summary>
        /// 获取翻译文本（带参数替换）
        /// 配置中使用 {0}, {1}, {2}... 作为占位符
        /// </summary>
        /// <param name="key">文本Key</param>
        /// <param name="args">替换参数</param>
        /// <returns>替换后的文本</returns>
        public string Get(string key, params object[] args)
        {
            string template = Get(key);

            if (args == null || args.Length == 0) return template;

            try
            {
                return string.Format(template, args);
            }
            catch (FormatException e)
            {
                Debug.LogError($"[Localization] 文本格式化失败: {key}, {e.Message}");
                return template;
            }
        }

        /// <summary>
        /// 检查是否有指定key的翻译
        /// </summary>
        public bool HasKey(string key)
        {
            return _translations.ContainsKey(key);
        }

        /// <summary>
        /// 手动添加翻译条目（用于运行时热更新）
        /// </summary>
        public void AddTranslation(string key, string value)
        {
            _translations[key] = value;
        }

        /// <summary>
        /// 批量添加翻译（用于服务端下发）
        /// </summary>
        public void AddTranslations(Dictionary<string, string> translations)
        {
            foreach (var pair in translations)
            {
                _translations[pair.Key] = pair.Value;
            }
        }

        // ========== 私有方法 ==========

        /// <summary>
        /// 解析语言文件
        /// 支持简单的JSON格式：{"key1":"value1","key2":"value2"}
        /// 也支持嵌套分组格式
        /// </summary>
        private void ParseLanguageFile(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                // 使用简易JSON解析（避免引入重型库）
                // 语言文件格式约定为扁平的 key:value JSON
                var wrapper = JsonUtility.FromJson<LanguageFileWrapper>(json);
                if (wrapper != null && wrapper.entries != null)
                {
                    for (int i = 0; i < wrapper.entries.Count; i++)
                    {
                        var entry = wrapper.entries[i];
                        if (!string.IsNullOrEmpty(entry.key))
                        {
                            _translations[entry.key] = entry.value ?? string.Empty;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Localization] 语言文件解析失败: {e.Message}");
            }
        }

        /// <summary>语言文件的JSON包装结构</summary>
        [Serializable]
        private class LanguageFileWrapper
        {
            public List<LanguageEntry> entries;
        }

        /// <summary>语言条目</summary>
        [Serializable]
        private class LanguageEntry
        {
            public string key;
            public string value;
        }
    }
}
