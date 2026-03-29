
// ============================================================
// 文件名：SaveManager.cs
// 功能描述：本地数据存储系统 — JSON序列化、数据加密、版本迁移
//          适配微信小游戏wx.setStorageSync/wx.getStorageSync
// 创建时间：2026-03-25
// 所属模块：Framework
// 对应交互：阶段二 #50
// ============================================================

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AetheraSurvivors.Framework
{
    /// <summary>
    /// 存储提供者接口 — 平台抽象
    /// </summary>
    public interface IStorageProvider
    {
        void SetItem(string key, string value);
        string GetItem(string key);
        void RemoveItem(string key);
        bool HasItem(string key);
        void Clear();
    }

    /// <summary>
    /// 编辑器存储提供者 — 使用PlayerPrefs
    /// </summary>
    public class EditorStorageProvider : IStorageProvider
    {
        public void SetItem(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        public string GetItem(string key)
        {
            return PlayerPrefs.GetString(key, string.Empty);
        }

        public void RemoveItem(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        public bool HasItem(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public void Clear()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// 微信小游戏存储提供者 — 使用wx.setStorageSync/wx.getStorageSync
    /// 通过WXBridge桥接JS调用
    /// </summary>
    public class WXStorageProvider : IStorageProvider
    {
        // 微信存储API通过JS桥接层调用
        // 在Plugins/WebGL/WXBridge.jslib中实现具体的JS函数
        // 此处使用条件编译，WebGL平台才真正调用

        public void SetItem(string key, string value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // 调用微信SDK的存储接口
            // WX.StorageSetSync(key, value);
            // 暂用PlayerPrefs作为Fallback，后续接入微信SDK V2的存储API
            try
            {
                PlayerPrefs.SetString(key, value);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[WXStorage] SetItem失败: {key}, {e.Message}");
            }
#else
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
#endif
        }

        public string GetItem(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                return PlayerPrefs.GetString(key, string.Empty);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WXStorage] GetItem失败: {key}, {e.Message}");
                return string.Empty;
            }
#else
            return PlayerPrefs.GetString(key, string.Empty);
#endif
        }

        public void RemoveItem(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[WXStorage] RemoveItem失败: {key}, {e.Message}");
            }
#else
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
#endif
        }

        public bool HasItem(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public void Clear()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// 存档管理器 — 全局单例
    /// 
    /// 职责：
    /// 1. 统一的数据存取接口（自动序列化/反序列化）
    /// 2. 数据加密（AES-128）防止明文篡改
    /// 3. 数据版本管理和迁移
    /// 4. 平台适配（编辑器用PlayerPrefs，微信用wx.setStorageSync）
    /// 5. 存档签名校验（防篡改）
    /// 
    /// 使用示例：
    ///   // 保存数据
    ///   SaveManager.Instance.Save("player_data", playerData);
    ///   
    ///   // 读取数据
    ///   var data = SaveManager.Instance.Load&lt;PlayerData&gt;("player_data");
    ///   
    ///   // 删除数据
    ///   SaveManager.Instance.Delete("player_data");
    /// </summary>
    public class SaveManager : Singleton<SaveManager>
    {
        // ========== 常量 ==========

        /// <summary>当前存档版本号</summary>
        private const int CurrentSaveVersion = 1;

        /// <summary>存档版本号的存储Key</summary>
        private const string VersionKey = "__save_version__";

        /// <summary>加密密钥（运行时从设备ID派生，避免源码硬编码）</summary>
        private static byte[] _runtimeKey;
        private static byte[] _runtimeIV;

        private static byte[] GetEncryptKey()
        {
            if (_runtimeKey == null)
            {
                // WebGL平台deviceUniqueIdentifier每次会话不同，使用稳定标识符
                string deviceId;
#if UNITY_WEBGL && !UNITY_EDITOR
                // WebGL使用固定应用标识符（PlayerPrefs中存储随机生成的设备ID）
                deviceId = PlayerPrefs.GetString("__device_id__", "");
                if (string.IsNullOrEmpty(deviceId))
                {
                    deviceId = System.Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString("__device_id__", deviceId);
                    PlayerPrefs.Save();
                }
#else
                deviceId = SystemInfo.deviceUniqueIdentifier;
#endif
                string deviceSeed = deviceId + "AetheraSurvivor!";
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(deviceSeed));
                    _runtimeKey = new byte[16];
                    System.Array.Copy(hash, 0, _runtimeKey, 0, 16);
                }
            }
            return _runtimeKey;
        }

        private static byte[] GetEncryptIV()
        {
            if (_runtimeIV == null)
            {
                string deviceId;
#if UNITY_WEBGL && !UNITY_EDITOR
                deviceId = PlayerPrefs.GetString("__device_id__", "AetheraSurvivorsDefault");
#else
                deviceId = SystemInfo.deviceUniqueIdentifier;
#endif
                string ivSeed = "SurvivorsDefend!" + deviceId;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(ivSeed));
                    _runtimeIV = new byte[16];
                    System.Array.Copy(hash, 16, _runtimeIV, 0, 16);
                }
            }
            return _runtimeIV;
        }

        /// <summary>签名密钥</summary>
        private const string SignSecret = "AetheraSaveSign2026";

        // ========== 私有字段 ==========

        /// <summary>存储提供者（平台适配）</summary>
        private IStorageProvider _storage;

        /// <summary>是否启用加密（调试时可关闭）</summary>
        private bool _encryptionEnabled = true;

        /// <summary>内存缓存（避免频繁读写存储）</summary>
        private readonly Dictionary<string, string> _memoryCache = new Dictionary<string, string>(32);

        /// <summary>脏数据标记（哪些key需要写入存储）</summary>
        private readonly HashSet<string> _dirtyKeys = new HashSet<string>();

        // ========== 公共属性 ==========

        /// <summary>是否启用加密</summary>
        public bool EncryptionEnabled
        {
            get => _encryptionEnabled;
            set => _encryptionEnabled = value;
        }

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            // 根据平台选择存储提供者
#if UNITY_WEBGL && !UNITY_EDITOR
            _storage = new WXStorageProvider();
#else
            _storage = new EditorStorageProvider();
#endif

            // 检查存档版本，执行必要的迁移
            MigrateIfNeeded();
        }

        protected override void OnDispose()
        {
            // 退出前保存所有脏数据
            FlushAll();
            _memoryCache.Clear();
            _dirtyKeys.Clear();
        }

        // ========== 公共方法：存取 ==========

        /// <summary>
        /// 保存数据（序列化为JSON → 加密 → 签名 → 写入存储）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">存储键</param>
        /// <param name="data">要保存的数据对象</param>
        public void Save<T>(string key, T data)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[SaveManager] Save: key不能为空");
                return;
            }

            try
            {
                // 序列化为JSON
                string json = JsonUtility.ToJson(data);

                // 加密
                string stored = _encryptionEnabled ? Encrypt(json) : json;

                // 生成签名
                string signature = GenerateSignature(stored);

                // 存储格式：signature|data
                string fullData = signature + "|" + stored;

                // 写入内存缓存
                _memoryCache[key] = fullData;
                _dirtyKeys.Add(key);

                // 立即写入存储（关键数据不能丢）
                _storage.SetItem(key, fullData);
                _dirtyKeys.Remove(key);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 保存数据失败: {key}, {e.Message}");
            }
        }

        /// <summary>
        /// 读取数据（从存储读取 → 验签 → 解密 → 反序列化）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">存储键</param>
        /// <param name="defaultValue">默认值（读取失败时返回）</param>
        /// <returns>反序列化后的数据对象</returns>
        public T Load<T>(string key, T defaultValue = default)
        {
            if (string.IsNullOrEmpty(key)) return defaultValue;

            try
            {
                // 优先从内存缓存读取
                string fullData;
                if (!_memoryCache.TryGetValue(key, out fullData))
                {
                    fullData = _storage.GetItem(key);
                    if (string.IsNullOrEmpty(fullData)) return defaultValue;
                    _memoryCache[key] = fullData;
                }

                // 解析签名和数据
                int separatorIndex = fullData.IndexOf('|');
                if (separatorIndex < 0)
                {
                    // 旧版数据（无签名），直接尝试解密
                    string json = _encryptionEnabled ? Decrypt(fullData) : fullData;
                    if (json == null) return defaultValue;
                    return JsonUtility.FromJson<T>(json);
                }

                string signature = fullData.Substring(0, separatorIndex);
                string stored = fullData.Substring(separatorIndex + 1);

                // 验证签名
                string expectedSignature = GenerateSignature(stored);
                if (signature != expectedSignature)
                {
                    Debug.LogError($"[SaveManager] 数据签名验证失败（可能被篡改）: {key}");
                    return defaultValue;
                }

                // 解密
                string decrypted = _encryptionEnabled ? Decrypt(stored) : stored;
                if (decrypted == null) return defaultValue;

                // 反序列化
                return JsonUtility.FromJson<T>(decrypted);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 读取数据失败: {key}, {e.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// 删除指定key的数据
        /// </summary>
        public void Delete(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            _memoryCache.Remove(key);
            _dirtyKeys.Remove(key);
            _storage.RemoveItem(key);
        }

        /// <summary>
        /// 检查key是否存在
        /// </summary>
        public bool HasKey(string key)
        {
            return _memoryCache.ContainsKey(key) || _storage.HasItem(key);
        }

        /// <summary>
        /// 将所有脏数据写入存储
        /// </summary>
        public void FlushAll()
        {
            foreach (var key in _dirtyKeys)
            {
                if (_memoryCache.TryGetValue(key, out var data))
                {
                    _storage.SetItem(key, data);
                }
            }
            _dirtyKeys.Clear();
        }

        /// <summary>
        /// 清除所有存档数据（⚠️ 危险操作！）
        /// </summary>
        public void ClearAll()
        {
            _memoryCache.Clear();
            _dirtyKeys.Clear();
            _storage.Clear();
        }

        // ========== 版本迁移 ==========

        /// <summary>
        /// 检查存档版本并执行迁移
        /// </summary>
        private void MigrateIfNeeded()
        {
            string versionStr = _storage.GetItem(VersionKey);
            int savedVersion = 0;

            if (!string.IsNullOrEmpty(versionStr))
            {
                int.TryParse(versionStr, out savedVersion);
            }

            if (savedVersion < CurrentSaveVersion)
            {
                // 执行版本迁移
                for (int v = savedVersion; v < CurrentSaveVersion; v++)
                {
                    MigrateVersion(v, v + 1);
                }

                // 更新版本号
                _storage.SetItem(VersionKey, CurrentSaveVersion.ToString());
                Debug.Log($"[SaveManager] 存档从v{savedVersion}迁移到v{CurrentSaveVersion}");
            }
        }

        /// <summary>
        /// 单步版本迁移
        /// 子类可以重写此方法添加具体的迁移逻辑
        /// </summary>
        private void MigrateVersion(int from, int to)
        {
            Debug.Log($"[SaveManager] 执行存档迁移: v{from} → v{to}");

            // 版本迁移示例：
            // if (from == 0 && to == 1)
            // {
            //     // v0→v1：添加新字段，设置默认值
            //     var oldData = LoadRaw<OldPlayerData>("player_data");
            //     var newData = ConvertToV1(oldData);
            //     Save("player_data", newData);
            // }
        }

        // ========== 加密/解密 ==========

        /// <summary>AES加密</summary>
        private string Encrypt(string plainText)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = GetEncryptKey();
                    aes.IV = GetEncryptIV();
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    var encryptor = aes.CreateEncryptor();
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                    return Convert.ToBase64String(encryptedBytes);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 加密失败: {e.Message}");
                // 加密失败不返回明文，标记为加密失败数据
                return "ENC_FAIL:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
            }
        }

        /// <summary>AES解密</summary>
        private string Decrypt(string cipherText)
        {
            try
            {
                // 处理加密失败的标记数据
                if (cipherText.StartsWith("ENC_FAIL:"))
                {
                    string base64 = cipherText.Substring(9);
                    return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                }

                using (var aes = Aes.Create())
                {
                    aes.Key = GetEncryptKey();
                    aes.IV = GetEncryptIV();
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    var decryptor = aes.CreateDecryptor();
                    byte[] cipherBytes = Convert.FromBase64String(cipherText);
                    byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

                    return Encoding.UTF8.GetString(plainBytes);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 解密失败（数据可能已损坏）: {e.Message}");
                // 解密失败返回null而不是密文，防止将密文当作有效JSON解析导致数据损坏
                return null;
            }
        }

        /// <summary>生成数据签名（HMAC-SHA256）</summary>
        private string GenerateSignature(string data)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SignSecret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                // 取前8字节作为签名（32字符hex），减少存储开销
                var sb = new StringBuilder(16);
                for (int i = 0; i < 8; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
