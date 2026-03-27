
// ============================================================
// 文件名：AnalyticsManager.cs
// 功能描述：数据埋点框架 — 统一埋点接口、事件队列、批量上报
//          适配微信小游戏wx.reportEvent
// 创建时间：2026-03-25
// 所属模块：Platform
// 对应交互：阶段二 #60
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetheraSurvivors.Platform
{
    /// <summary>
    /// 埋点事件数据
    /// </summary>
    [Serializable]
    public class AnalyticsEvent
    {
        /// <summary>事件名称</summary>
        public string EventName;

        /// <summary>事件参数（KV对）</summary>
        public Dictionary<string, string> Params;

        /// <summary>事件时间戳（Unix毫秒）</summary>
        public long Timestamp;

        public AnalyticsEvent(string eventName)
        {
            EventName = eventName;
            Params = new Dictionary<string, string>();
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>添加参数（链式调用）</summary>
        public AnalyticsEvent Set(string key, string value)
        {
            Params[key] = value;
            return this;
        }

        /// <summary>添加int参数</summary>
        public AnalyticsEvent Set(string key, int value)
        {
            Params[key] = value.ToString();
            return this;
        }

        /// <summary>添加long参数</summary>
        public AnalyticsEvent Set(string key, long value)
        {
            Params[key] = value.ToString();
            return this;
        }

        /// <summary>添加float参数</summary>
        public AnalyticsEvent Set(string key, float value)
        {
            Params[key] = value.ToString("F2");
            return this;
        }

        /// <summary>添加bool参数</summary>
        public AnalyticsEvent Set(string key, bool value)
        {
            Params[key] = value ? "1" : "0";
            return this;
        }
    }

    /// <summary>
    /// 数据埋点管理器 — 全局单例
    /// 
    /// 职责：
    /// 1. 统一的埋点接口（Track方法）
    /// 2. 事件队列 + 批量上报
    /// 3. 适配微信小游戏wx.reportEvent
    /// 4. 编辑器环境输出到Console
    /// 
    /// 使用示例：
    ///   // 简单事件
    ///   AnalyticsManager.Instance.Track("app_launch");
    ///   
    ///   // 带参数的事件
    ///   AnalyticsManager.Instance.Track("level_start", 
    ///       new AnalyticsEvent("level_start")
    ///           .Set("chapter", 1)
    ///           .Set("level", 3)
    ///           .Set("hero_id", "hero_001")
    ///   );
    ///   
    ///   // 使用预定义事件
    ///   AnalyticsManager.Instance.TrackLevelStart(1, 3, "hero_001");
    /// </summary>
    public class AnalyticsManager : AetheraSurvivors.Framework.Singleton<AnalyticsManager>
    {
        // ========== 常量 ==========

        /// <summary>批量上报阈值（累积到此数量自动上报）</summary>
        private const int BatchSize = 10;

        /// <summary>批量上报间隔（秒）</summary>
        private const float BatchInterval = 30f;

        // ========== 私有字段 ==========

        /// <summary>事件队列（等待上报）</summary>
        private readonly List<AnalyticsEvent> _eventQueue = new List<AnalyticsEvent>(BatchSize);

        /// <summary>上次上报时间</summary>
        private float _lastFlushTime;

        /// <summary>是否启用埋点（可全局关闭）</summary>
        private bool _enabled = true;

        /// <summary>用户ID（登录后设置）</summary>
        private string _userId;

        /// <summary>公共参数（每个事件都会附带）</summary>
        private readonly Dictionary<string, string> _commonParams = new Dictionary<string, string>();

        // ========== 公共属性 ==========

        /// <summary>是否启用</summary>
        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            _lastFlushTime = Time.realtimeSinceStartup;

            // 设置公共参数
            _commonParams["app_version"] = Application.version;
            _commonParams["platform"] = Application.platform.ToString();
            _commonParams["device_model"] = SystemInfo.deviceModel;
            _commonParams["os"] = SystemInfo.operatingSystem;
            _commonParams["screen"] = $"{Screen.width}x{Screen.height}";

            Debug.Log("[AnalyticsManager] 埋点系统初始化完成");
        }

        protected override void OnDispose()
        {
            // 退出前上报所有未发送的事件
            Flush();
        }

        // ========== 公共方法：配置 ==========

        /// <summary>
        /// 设置用户ID（登录成功后调用）
        /// </summary>
        public void SetUserId(string userId)
        {
            _userId = userId;
            _commonParams["user_id"] = userId;
        }

        /// <summary>
        /// 设置公共参数（每个事件都会附带）
        /// </summary>
        public void SetCommonParam(string key, string value)
        {
            _commonParams[key] = value;
        }

        // ========== 公共方法：通用埋点 ==========

        /// <summary>
        /// 上报事件（无参数）
        /// </summary>
        public void Track(string eventName)
        {
            if (!_enabled || string.IsNullOrEmpty(eventName)) return;

            var evt = new AnalyticsEvent(eventName);
            EnqueueEvent(evt);
        }

        /// <summary>
        /// 上报事件（带参数）
        /// </summary>
        public void Track(string eventName, AnalyticsEvent evt)
        {
            if (!_enabled || evt == null) return;

            evt.EventName = eventName;
            EnqueueEvent(evt);
        }

        /// <summary>
        /// 立即上报所有队列中的事件
        /// </summary>
        public void Flush()
        {
            if (_eventQueue.Count == 0) return;

            // 批量上报
            for (int i = 0; i < _eventQueue.Count; i++)
            {
                ReportEvent(_eventQueue[i]);
            }

            Debug.Log($"[AnalyticsManager] 批量上报 {_eventQueue.Count} 个事件");
            _eventQueue.Clear();
            _lastFlushTime = Time.realtimeSinceStartup;
        }

        // ========== 预定义事件方法（便捷API） ==========

        /// <summary>应用启动</summary>
        public void TrackAppLaunch()
        {
            Track("app_launch", new AnalyticsEvent("app_launch")
                .Set("launch_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }

        /// <summary>应用切到后台</summary>
        public void TrackAppHide()
        {
            Track("app_hide");
        }

        /// <summary>应用回到前台</summary>
        public void TrackAppShow()
        {
            Track("app_show");
        }

        /// <summary>页面浏览</summary>
        public void TrackPageView(string pageName)
        {
            Track("page_view", new AnalyticsEvent("page_view")
                .Set("page", pageName));
        }

        /// <summary>按钮点击</summary>
        public void TrackButtonClick(string buttonName, string page = "")
        {
            Track("click_button", new AnalyticsEvent("click_button")
                .Set("button", buttonName)
                .Set("page", page));
        }

        /// <summary>关卡开始</summary>
        public void TrackLevelStart(int chapter, int level, string heroId)
        {
            Track("level_start", new AnalyticsEvent("level_start")
                .Set("chapter", chapter)
                .Set("level", level)
                .Set("hero_id", heroId));
        }

        /// <summary>关卡通关</summary>
        public void TrackLevelComplete(int chapter, int level, int stars, float duration, int killCount)
        {
            Track("level_complete", new AnalyticsEvent("level_complete")
                .Set("chapter", chapter)
                .Set("level", level)
                .Set("stars", stars)
                .Set("duration", duration)
                .Set("kill_count", killCount));
        }

        /// <summary>关卡失败</summary>
        public void TrackLevelFail(int chapter, int level, int waveReached, float duration)
        {
            Track("level_fail", new AnalyticsEvent("level_fail")
                .Set("chapter", chapter)
                .Set("level", level)
                .Set("wave_reached", waveReached)
                .Set("duration", duration));
        }

        /// <summary>付费事件</summary>
        public void TrackPurchase(string productId, float price, string currency)
        {
            Track("purchase", new AnalyticsEvent("purchase")
                .Set("product_id", productId)
                .Set("price", price)
                .Set("currency", currency));
        }

        /// <summary>广告观看</summary>
        public void TrackAdWatch(string adType, string adUnitId, bool completed)
        {
            Track("ad_watch", new AnalyticsEvent("ad_watch")
                .Set("ad_type", adType)
                .Set("ad_unit_id", adUnitId)
                .Set("completed", completed));
        }

        /// <summary>分享事件</summary>
        public void TrackShare(string shareType, string shareScene)
        {
            Track("share", new AnalyticsEvent("share")
                .Set("share_type", shareType)
                .Set("share_scene", shareScene));
        }

        /// <summary>词条选择</summary>
        public void TrackRuneSelect(string runeId, int waveNum, string rarity)
        {
            Track("rune_select", new AnalyticsEvent("rune_select")
                .Set("rune_id", runeId)
                .Set("wave_num", waveNum)
                .Set("rarity", rarity));
        }

        // ========== 私有方法 ==========

        /// <summary>将事件加入队列</summary>
        private void EnqueueEvent(AnalyticsEvent evt)
        {
            // 附加公共参数
            foreach (var pair in _commonParams)
            {
                if (!evt.Params.ContainsKey(pair.Key))
                {
                    evt.Params[pair.Key] = pair.Value;
                }
            }

            _eventQueue.Add(evt);

            // 编辑器中立即输出
#if UNITY_EDITOR
            DebugPrintEvent(evt);
#endif

            // 达到批量阈值或超时则自动上报
            if (_eventQueue.Count >= BatchSize ||
                (Time.realtimeSinceStartup - _lastFlushTime) >= BatchInterval)
            {
                Flush();
            }
        }

        /// <summary>上报单个事件（实际发送）</summary>
        private void ReportEvent(AnalyticsEvent evt)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // 微信小游戏：使用 wx.reportEvent 上报
            // 后续通过WXBridge.ReportEvent调用
            // WXBridge.ReportEvent(evt.EventName, JsonUtility.ToJson(evt.Params));
            
            // 暂时使用Debug输出，待WXBridge扩展后替换
            Debug.Log($"[Analytics-WX] {evt.EventName}");
#else
            // 编辑器：输出到Console即可
            // 后续可替换为自建上报服务器
#endif
        }

        /// <summary>编辑器中打印事件详情</summary>
        private void DebugPrintEvent(AnalyticsEvent evt)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"[Analytics] 📊 {evt.EventName}");

            if (evt.Params.Count > 0)
            {
                sb.Append(" {");
                bool first = true;
                foreach (var pair in evt.Params)
                {
                    // 跳过公共参数，减少输出噪音
                    if (_commonParams.ContainsKey(pair.Key)) continue;

                    if (!first) sb.Append(", ");
                    sb.Append($"{pair.Key}={pair.Value}");
                    first = false;
                }
                sb.Append("}");
            }

            Debug.Log(sb.ToString());
        }
    }
}
