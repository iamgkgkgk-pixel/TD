// ============================================================
// 文件名：CrashReporter.cs
// 功能描述：崩溃/异常上报系统 — 捕获未处理异常并上报
//          收集设备信息和堆栈，适配微信小游戏异常捕获
// 创建时间：2026-03-25
// 所属模块：Platform
// 对应交互：阶段二 #66
// ============================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AetheraSurvivors.Platform
{
    /// <summary>
    /// 异常报告数据
    /// </summary>
    [Serializable]
    public class CrashReport
    {
        /// <summary>异常类型</summary>
        public string exception_type;
        /// <summary>异常消息</summary>
        public string message;
        /// <summary>堆栈信息</summary>
        public string stacktrace;
        /// <summary>日志类型（Exception/Error/Assert）</summary>
        public string log_type;
        /// <summary>设备型号</summary>
        public string device_model;
        /// <summary>操作系统</summary>
        public string os;
        /// <summary>屏幕分辨率</summary>
        public string screen;
        /// <summary>内存使用（MB）</summary>
        public int memory_mb;
        /// <summary>FPS</summary>
        public int fps;
        /// <summary>客户端版本</summary>
        public string client_version;
        /// <summary>用户ID</summary>
        public string user_id;
        /// <summary>当前场景</summary>
        public string scene;
        /// <summary>时间戳（Unix秒）</summary>
        public long timestamp;
        /// <summary>附加信息</summary>
        public string extra;
    }

    /// <summary>
    /// 崩溃/异常上报管理器 — 全局MonoSingleton
    /// 
    /// 职责：
    /// 1. 注册Unity全局异常监听（Application.logMessageReceived）
    /// 2. 捕获未处理异常和Error级别日志
    /// 3. 收集设备信息、内存使用、FPS等上下文
    /// 4. 去重过滤（相同异常不重复上报）
    /// 5. 队列批量上报到远程服务器
    /// 6. 本地缓存未成功上报的异常（离线后恢复上报）
    /// 
    /// 使用方式：
    ///   在GameManager初始化时最先初始化CrashReporter
    ///   CrashReporter.Preload();
    ///   
    ///   // 手动上报
    ///   CrashReporter.Instance.ReportException(new Exception("test"));
    ///   
    ///   // 附加自定义信息到下次崩溃报告
    ///   CrashReporter.Instance.SetExtraInfo("battle_state", "wave_3");
    /// </summary>
    public class CrashReporter : Framework.MonoSingleton<CrashReporter>
    {
        // ========== 常量 ==========

        /// <summary>异常上报API路径</summary>
        private const string ReportApiPath = "/api/crash/report";

        /// <summary>队列大小限制</summary>
        private const int MaxQueueSize = 20;

        /// <summary>上报间隔（秒）</summary>
        private const float ReportInterval = 10f;

        /// <summary>相同异常去重时间窗口（秒）</summary>
        private const float DeduplicateWindow = 60f;

        /// <summary>本地缓存Key</summary>
        private const string LocalCacheKey = "crash_reports_cache";

        // ========== 私有字段 ==========

        /// <summary>待上报队列</summary>
        private readonly List<CrashReport> _reportQueue = new List<CrashReport>();

        /// <summary>已上报异常的指纹缓存（用于去重）</summary>
        private readonly Dictionary<string, float> _recentReports = new Dictionary<string, float>();

        /// <summary>附加信息</summary>
        private readonly Dictionary<string, string> _extraInfo = new Dictionary<string, string>();

        /// <summary>上次上报时间</summary>
        private float _lastReportTime;

        /// <summary>是否已注册监听</summary>
        private bool _isListening = false;

        /// <summary>FPS计算相关</summary>
        private int _frameCount;
        private float _fpsTimer;
        private int _currentFps;

        /// <summary>是否启用（可关闭以减少性能开销）</summary>
        private bool _enabled = true;

        // ========== 公共属性 ==========

        /// <summary>是否启用异常上报</summary>
        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>待上报队列大小</summary>
        public int PendingCount => _reportQueue.Count;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            RegisterListeners();
            _lastReportTime = Time.realtimeSinceStartup;

            // 尝试恢复未成功上报的异常
            RestoreCachedReports();

            Framework.Logger.I("CrashReporter", "✅ 崩溃上报系统初始化完成");
        }

        protected override void OnDispose()
        {
            UnregisterListeners();

            // 缓存未上报的异常到本地
            CacheReportsLocally();
        }

        private void Update()
        {
            if (!_enabled) return;

            // FPS计算
            _frameCount++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 1f)
            {
                _currentFps = _frameCount;
                _frameCount = 0;
                _fpsTimer -= 1f;
            }

            // 定时上报
            if (_reportQueue.Count > 0 &&
                (Time.realtimeSinceStartup - _lastReportTime) >= ReportInterval)
            {
                FlushReports();
            }

            // 清理过期的去重缓存
            CleanExpiredDeduplicates();
        }

        // ========== 公共方法 ==========

        /// <summary>
        /// 手动上报一个异常
        /// </summary>
        public void ReportException(Exception exception, string context = "")
        {
            if (!_enabled || exception == null) return;

            var report = CreateReport(
                exception.GetType().Name,
                exception.Message,
                exception.StackTrace ?? "",
                "Exception"
            );

            if (!string.IsNullOrEmpty(context))
            {
                report.extra += $"\nContext: {context}";
            }

            EnqueueReport(report);
        }

        /// <summary>
        /// 设置附加信息（会包含在下一次崩溃报告中）
        /// 用于记录当前游戏状态，帮助定位问题
        /// </summary>
        /// <param name="key">信息键名</param>
        /// <param name="value">信息值</param>
        public void SetExtraInfo(string key, string value)
        {
            _extraInfo[key] = value;
        }

        /// <summary>
        /// 移除附加信息
        /// </summary>
        public void RemoveExtraInfo(string key)
        {
            _extraInfo.Remove(key);
        }

        /// <summary>
        /// 立即上报所有队列中的异常
        /// </summary>
        public void FlushReports()
        {
            if (_reportQueue.Count == 0) return;

            Framework.Logger.D("CrashReporter", "上报 {0} 个异常", _reportQueue.Count);

            // 逐条上报
            for (int i = 0; i < _reportQueue.Count; i++)
            {
                SendReport(_reportQueue[i]);
            }

            _reportQueue.Clear();
            _lastReportTime = Time.realtimeSinceStartup;
        }

        // ========== 内部方法：监听注册 ==========

        /// <summary>注册全局异常监听</summary>
        private void RegisterListeners()
        {
            if (_isListening) return;

            Application.logMessageReceived += OnLogMessageReceived;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            _isListening = true;
            Framework.Logger.D("CrashReporter", "已注册全局异常监听");
        }

        /// <summary>取消全局异常监听</summary>
        private void UnregisterListeners()
        {
            if (!_isListening) return;

            Application.logMessageReceived -= OnLogMessageReceived;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;

            _isListening = false;
        }

        // ========== 内部方法：异常处理 ==========

        /// <summary>Unity日志回调（捕获Exception和Error）</summary>
        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (!_enabled) return;

            // 只捕获Exception和Error
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
                return;

            // 忽略CrashReporter自身产生的日志，避免死循环
            if (condition.Contains("[CrashReporter]")) return;

            var report = CreateReport(
                type.ToString(),
                condition,
                stackTrace,
                type.ToString()
            );

            EnqueueReport(report);
        }

        /// <summary>.NET未处理异常回调</summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (!_enabled) return;

            var exception = args.ExceptionObject as Exception;
            if (exception != null)
            {
                var report = CreateReport(
                    exception.GetType().Name,
                    exception.Message,
                    exception.StackTrace ?? "",
                    "UnhandledException"
                );

                EnqueueReport(report);

                // 未处理异常立即上报
                FlushReports();
            }
        }

        // ========== 内部方法：报告创建 ==========

        /// <summary>创建异常报告</summary>
        private CrashReport CreateReport(string exceptionType, string message,
            string stacktrace, string logType)
        {
            // 收集附加信息
            var sb = new StringBuilder();
            foreach (var pair in _extraInfo)
            {
                sb.Append($"{pair.Key}={pair.Value};");
            }

            return new CrashReport
            {
                exception_type = exceptionType,
                message = TruncateString(message, 500),
                stacktrace = TruncateString(stacktrace, 2000),
                log_type = logType,
                device_model = SystemInfo.deviceModel,
                os = SystemInfo.operatingSystem,
                screen = $"{Screen.width}x{Screen.height}",
                memory_mb = (int)(SystemInfo.systemMemorySize > 0 ? SystemInfo.systemMemorySize : 0),
                fps = _currentFps,
                client_version = Application.version,
                user_id = WXLogin.HasInstance && WXLogin.Instance.IsLoggedIn
                    ? WXLogin.Instance.UserId : "unknown",
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                extra = sb.ToString()
            };
        }

        /// <summary>加入上报队列（含去重）</summary>
        private void EnqueueReport(CrashReport report)
        {
            // 生成异常指纹（用于去重）
            string fingerprint = $"{report.exception_type}:{report.message}".GetHashCode().ToString();

            // 检查是否在去重窗口内已上报过
            if (_recentReports.TryGetValue(fingerprint, out float lastTime))
            {
                if (Time.realtimeSinceStartup - lastTime < DeduplicateWindow)
                {
                    return; // 跳过重复异常
                }
            }

            _recentReports[fingerprint] = Time.realtimeSinceStartup;

            // 队列满时丢弃最旧的
            if (_reportQueue.Count >= MaxQueueSize)
            {
                _reportQueue.RemoveAt(0);
            }

            _reportQueue.Add(report);
        }

        // ========== 内部方法：上报 ==========

        /// <summary>发送单条异常报告</summary>
        private void SendReport(CrashReport report)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // 微信平台：使用wx.reportEvent上报（如果WXBridge可用）
            if (WXBridgeExtended.HasInstance)
            {
                WXBridgeExtended.Instance.ReportEvent("crash_report",
                    JsonUtility.ToJson(report));
            }
#endif

            // 同时尝试通过HTTP上报到自建服务器
            if (HttpClient.HasInstance && !HttpClient.Instance.IsBusy)
            {
                HttpClient.Instance.Post(ReportApiPath, report,
                    onSuccess: (resp) =>
                    {
                        Framework.Logger.D("CrashReporter", "异常上报成功");
                    },
                    onError: (resp) =>
                    {
                        // 上报失败静默处理，不再递归报错
                    },
                    timeout: 5
                );
            }
        }

        // ========== 内部方法：本地缓存 ==========

        /// <summary>缓存未上报的异常到本地</summary>
        private void CacheReportsLocally()
        {
            if (_reportQueue.Count == 0) return;

            try
            {
                // 简单JSON数组序列化
                var wrapper = new CrashReportListWrapper { reports = _reportQueue.ToArray() };
                string json = JsonUtility.ToJson(wrapper);
                PlayerPrefs.SetString(LocalCacheKey, json);
                PlayerPrefs.Save();

                Framework.Logger.D("CrashReporter", "缓存了 {0} 条未上报的异常", _reportQueue.Count);
            }
            catch (Exception)
            {
                // 缓存失败不报错
            }
        }

        /// <summary>恢复本地缓存的异常</summary>
        private void RestoreCachedReports()
        {
            try
            {
                string json = PlayerPrefs.GetString(LocalCacheKey, "");
                if (string.IsNullOrEmpty(json)) return;

                var wrapper = JsonUtility.FromJson<CrashReportListWrapper>(json);
                if (wrapper?.reports != null && wrapper.reports.Length > 0)
                {
                    _reportQueue.AddRange(wrapper.reports);
                    Framework.Logger.D("CrashReporter", "恢复了 {0} 条缓存的异常", wrapper.reports.Length);
                }

                // 清除缓存
                PlayerPrefs.DeleteKey(LocalCacheKey);
                PlayerPrefs.Save();
            }
            catch (Exception)
            {
                // 恢复失败不报错
                PlayerPrefs.DeleteKey(LocalCacheKey);
            }
        }

        // ========== 内部方法：工具 ==========

        /// <summary>截断过长的字符串</summary>
        private string TruncateString(string str, int maxLength)
        {
            if (string.IsNullOrEmpty(str) || str.Length <= maxLength) return str;
            return str.Substring(0, maxLength) + "...(truncated)";
        }

        /// <summary>清理过期的去重缓存</summary>
        private void CleanExpiredDeduplicates()
        {
            // 每30秒清理一次
            if (Time.frameCount % 900 != 0) return;

            var keysToRemove = new List<string>();
            foreach (var pair in _recentReports)
            {
                if (Time.realtimeSinceStartup - pair.Value > DeduplicateWindow * 2)
                {
                    keysToRemove.Add(pair.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _recentReports.Remove(key);
            }
        }

        /// <summary>报告列表包装器（用于JSON序列化）</summary>
        [Serializable]
        private class CrashReportListWrapper
        {
            public CrashReport[] reports;
        }
    }
}
