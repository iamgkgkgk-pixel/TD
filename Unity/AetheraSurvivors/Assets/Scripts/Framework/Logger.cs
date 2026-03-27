
// ============================================================
// 文件名：Logger.cs
// 功能描述：日志系统 — 分级别、条件编译、支持远程上报
//          发布版自动关闭Debug级别日志
// 创建时间：2026-03-25
// 所属模块：Framework
// 对应交互：阶段二 #59
// ============================================================

using System;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AetheraSurvivors.Framework
{
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        /// <summary>调试信息（仅开发环境）</summary>
        Debug = 0,

        /// <summary>一般信息</summary>
        Info = 1,

        /// <summary>警告</summary>
        Warning = 2,

        /// <summary>错误</summary>
        Error = 3,

        /// <summary>不输出任何日志</summary>
        None = 99
    }

    /// <summary>
    /// 日志系统 — 全局静态工具
    /// 
    /// 特性：
    /// 1. 分级别输出（Debug/Info/Warning/Error）
    /// 2. 发布版通过条件编译自动关闭Debug级别
    /// 3. 模块标签（方便过滤）
    /// 4. 预留远程上报接口
    /// 
    /// 使用示例：
    ///   Logger.D("TowerManager", "放置了一座箭塔");
    ///   Logger.I("BattleManager", "波次开始: {0}", waveNum);
    ///   Logger.W("SaveManager", "存档文件损坏，使用默认数据");
    ///   Logger.E("Network", "请求超时: {0}", url);
    /// </summary>
    public static class Logger
    {
        // ========== 配置 ==========

        /// <summary>当前最低输出级别</summary>
        private static LogLevel _minLevel =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogLevel.Debug;
#else
            LogLevel.Info;  // 发布版不输出Debug
#endif

        /// <summary>是否启用模块标签</summary>
        private static bool _enableTag = true;

        /// <summary>远程上报回调（Error级别自动上报）</summary>
        private static Action<string, string> _remoteReportCallback;

        // ========== 公共方法：配置 ==========

        /// <summary>设置最低日志级别</summary>
        public static void SetMinLevel(LogLevel level)
        {
            _minLevel = level;
        }

        /// <summary>设置远程上报回调</summary>
        public static void SetRemoteReporter(Action<string, string> callback)
        {
            _remoteReportCallback = callback;
        }

        // ========== 公共方法：日志输出 ==========

        /// <summary>
        /// Debug级别日志（仅开发环境输出）
        /// 使用[Conditional("UNITY_EDITOR")]确保发布版完全剥离
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void D(string tag, string message)
        {
            if (_minLevel > LogLevel.Debug) return;
            Debug.Log(FormatMessage("D", tag, message));
        }

        /// <summary>
        /// Debug级别日志（带格式化参数）
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void D(string tag, string format, params object[] args)
        {
            if (_minLevel > LogLevel.Debug) return;
            Debug.Log(FormatMessage("D", tag, string.Format(format, args)));
        }

        /// <summary>
        /// Info级别日志
        /// </summary>
        public static void I(string tag, string message)
        {
            if (_minLevel > LogLevel.Info) return;
            Debug.Log(FormatMessage("I", tag, message));
        }

        /// <summary>
        /// Info级别日志（带格式化参数）
        /// </summary>
        public static void I(string tag, string format, params object[] args)
        {
            if (_minLevel > LogLevel.Info) return;
            Debug.Log(FormatMessage("I", tag, string.Format(format, args)));
        }

        /// <summary>
        /// Warning级别日志
        /// </summary>
        public static void W(string tag, string message)
        {
            if (_minLevel > LogLevel.Warning) return;
            Debug.LogWarning(FormatMessage("W", tag, message));
        }

        /// <summary>
        /// Warning级别日志（带格式化参数）
        /// </summary>
        public static void W(string tag, string format, params object[] args)
        {
            if (_minLevel > LogLevel.Warning) return;
            Debug.LogWarning(FormatMessage("W", tag, string.Format(format, args)));
        }

        /// <summary>
        /// Error级别日志（会自动上报到远程）
        /// </summary>
        public static void E(string tag, string message)
        {
            if (_minLevel > LogLevel.Error) return;

            string formatted = FormatMessage("E", tag, message);
            Debug.LogError(formatted);

            // 错误自动上报
            _remoteReportCallback?.Invoke(tag, message);
        }

        /// <summary>
        /// Error级别日志（带格式化参数）
        /// </summary>
        public static void E(string tag, string format, params object[] args)
        {
            if (_minLevel > LogLevel.Error) return;

            string message = string.Format(format, args);
            string formatted = FormatMessage("E", tag, message);
            Debug.LogError(formatted);

            _remoteReportCallback?.Invoke(tag, message);
        }

        /// <summary>
        /// Error级别日志（带异常信息）
        /// </summary>
        public static void E(string tag, string message, Exception exception)
        {
            if (_minLevel > LogLevel.Error) return;

            string fullMessage = $"{message}\n{exception}";
            string formatted = FormatMessage("E", tag, fullMessage);
            Debug.LogError(formatted);

            _remoteReportCallback?.Invoke(tag, fullMessage);
        }

        // ========== 私有方法 ==========

        /// <summary>格式化日志消息</summary>
        private static string FormatMessage(string level, string tag, string message)
        {
            if (_enableTag && !string.IsNullOrEmpty(tag))
            {
                return $"[{level}][{tag}] {message}";
            }
            return $"[{level}] {message}";
        }
    }
}
