// ============================================================
// 文件名：TimeSync.cs
// 功能描述：服务器时间同步模块 — 防止客户端本地时间作弊
//          通过HTTP请求服务端获取时间戳，维护本地偏移量
// 创建时间：2026-03-25
// 所属模块：Platform
// 对应交互：阶段二 #64
// ============================================================

using System;
using UnityEngine;

namespace AetheraSurvivors.Platform
{
    /// <summary>
    /// 时间同步响应数据
    /// </summary>
    [Serializable]
    public class TimeSyncResponse
    {
        /// <summary>服务端时间戳（Unix秒）</summary>
        public long server_time;
    }

    /// <summary>
    /// 服务器时间同步管理器 — 全局单例
    /// 
    /// 职责：
    /// 1. 定期与服务端同步时间
    /// 2. 维护本地与服务端的时间偏移量
    /// 3. 提供可信的当前时间（防止客户端改时间作弊）
    /// 4. 用于限时活动倒计时、每日刷新判断等
    /// 
    /// 工作原理：
    ///   1. 客户端发请求记录本地时间T1
    ///   2. 服务端返回服务端时间Ts
    ///   3. 客户端收到响应记录本地时间T2
    ///   4. 网络延迟 ≈ (T2 - T1) / 2
    ///   5. 偏移量 = Ts - (T1 + T2) / 2
    ///   6. 服务端当前时间 ≈ 本地时间 + 偏移量
    /// 
    /// 使用示例：
    ///   // 获取服务端当前时间
    ///   long serverNow = TimeSync.Instance.ServerTimeSeconds;
    ///   
    ///   // 获取服务端DateTime
    ///   DateTime serverDateTime = TimeSync.Instance.ServerDateTime;
    ///   
    ///   // 检查每日是否刷新（以服务端时间为准）
    ///   bool isNewDay = TimeSync.Instance.IsNewDay(lastLoginTimestamp);
    /// </summary>
    public class TimeSync : Framework.Singleton<TimeSync>
    {
        // ========== 常量 ==========

        /// <summary>时间同步API路径</summary>
        private const string TimeSyncPath = "/api/time";

        /// <summary>自动同步间隔（秒）</summary>
        private const float AutoSyncInterval = 300f; // 5分钟

        /// <summary>最大允许的网络延迟（秒），超过则认为同步不可靠</summary>
        private const float MaxAllowedLatency = 5f;

        /// <summary>每日刷新时间（小时，UTC+8凌晨5点 = UTC 21点）</summary>
        private const int DailyResetHourUTC = 21;

        // ========== 私有字段 ==========

        /// <summary>本地与服务端的时间偏移（秒）</summary>
        private double _offsetSeconds = 0;

        /// <summary>是否已成功同步过</summary>
        private bool _isSynced = false;

        /// <summary>上次同步的本地时间</summary>
        private float _lastSyncRealtime = 0;

        /// <summary>同步次数</summary>
        private int _syncCount = 0;

        /// <summary>最后一次网络延迟（秒）</summary>
        private float _lastLatency = 0;

        /// <summary>是否正在同步中</summary>
        private bool _isSyncing = false;

        // ========== 公共属性 ==========

        /// <summary>是否已成功同步</summary>
        public bool IsSynced => _isSynced;

        /// <summary>
        /// 服务端当前时间（Unix秒）
        /// 如果未同步，返回本地时间（不可信）
        /// </summary>
        public long ServerTimeSeconds
        {
            get
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return (long)(now + _offsetSeconds);
            }
        }

        /// <summary>
        /// 服务端当前时间（Unix毫秒）
        /// </summary>
        public long ServerTimeMilliseconds
        {
            get
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                return (long)(now + _offsetSeconds * 1000);
            }
        }

        /// <summary>
        /// 服务端当前DateTime（UTC）
        /// </summary>
        public DateTime ServerDateTimeUtc
        {
            get
            {
                return DateTimeOffset.FromUnixTimeSeconds(ServerTimeSeconds).UtcDateTime;
            }
        }

        /// <summary>
        /// 服务端当前DateTime（北京时间 UTC+8）
        /// </summary>
        public DateTime ServerDateTimeLocal
        {
            get
            {
                return ServerDateTimeUtc.AddHours(8);
            }
        }

        /// <summary>上次网络延迟（秒）</summary>
        public float LastLatency => _lastLatency;

        /// <summary>时间偏移量（秒）</summary>
        public double OffsetSeconds => _offsetSeconds;

        /// <summary>同步次数</summary>
        public int SyncCount => _syncCount;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            Framework.Logger.I("TimeSync", "时间同步模块初始化");
        }

        protected override void OnDispose()
        {
            _isSynced = false;
            _isSyncing = false;
        }

        // ========== 公共方法：同步 ==========

        /// <summary>
        /// 手动触发时间同步
        /// </summary>
        /// <param name="onComplete">同步完成回调（true=成功, false=失败）</param>
        public void Sync(Action<bool> onComplete = null)
        {
            if (_isSyncing)
            {
                Framework.Logger.W("TimeSync", "正在同步中，忽略重复请求");
                return;
            }

            _isSyncing = true;

            // 记录发送时刻的本地时间
            float sendTime = Time.realtimeSinceStartup;
            long sendTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // 检查HttpClient是否可用
            if (!HttpClient.HasInstance)
            {
                Framework.Logger.W("TimeSync", "HttpClient不可用，使用本地时间");
                _isSyncing = false;
                onComplete?.Invoke(false);
                return;
            }

            HttpClient.Instance.Get(TimeSyncPath,
                onSuccess: (response) =>
                {
                    _isSyncing = false;

                    // 记录收到响应的本地时间
                    float receiveTime = Time.realtimeSinceStartup;
                    long receiveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    // 计算网络延迟
                    float latency = receiveTime - sendTime;
                    _lastLatency = latency;

                    // 延迟过大，同步不可靠
                    if (latency > MaxAllowedLatency)
                    {
                        Framework.Logger.W("TimeSync", "网络延迟过大({0:F1}s)，同步结果不可靠", latency);
                        onComplete?.Invoke(false);
                        return;
                    }

                    try
                    {
                        // 解析服务端时间
                        var syncResp = response.ParseBody<TimeSyncResponse>();
                        long serverTime = syncResp.server_time;

                        // 计算偏移量：服务端时间 - 本地中间时间
                        double localMidTime = (sendTimestamp + receiveTimestamp) / 2.0;
                        _offsetSeconds = serverTime - localMidTime;

                        _isSynced = true;
                        _lastSyncRealtime = receiveTime;
                        _syncCount++;

                        Framework.Logger.I("TimeSync", "✅ 时间同步成功: 偏移={0:F1}s, 延迟={1:F1}ms, 次数={2}",
                            _offsetSeconds, latency * 1000, _syncCount);

                        onComplete?.Invoke(true);
                    }
                    catch (Exception e)
                    {
                        Framework.Logger.E("TimeSync", "解析服务端时间失败", e);
                        onComplete?.Invoke(false);
                    }
                },
                onError: (response) =>
                {
                    _isSyncing = false;
                    Framework.Logger.W("TimeSync", "时间同步请求失败: {0}", response.Error);
                    onComplete?.Invoke(false);
                },
                timeout: 5 // 时间同步请求超时较短
            );
        }

        /// <summary>
        /// 检查是否需要重新同步（超过同步间隔）
        /// </summary>
        public bool NeedsResync()
        {
            if (!_isSynced) return true;
            return (Time.realtimeSinceStartup - _lastSyncRealtime) > AutoSyncInterval;
        }

        /// <summary>
        /// 如果需要则自动同步
        /// </summary>
        public void SyncIfNeeded(Action<bool> onComplete = null)
        {
            if (NeedsResync())
            {
                Sync(onComplete);
            }
            else
            {
                onComplete?.Invoke(true);
            }
        }

        // ========== 公共方法：时间工具 ==========

        /// <summary>
        /// 判断是否跨天（以服务端时间为准）
        /// 每日刷新时间：北京时间凌晨5:00
        /// </summary>
        /// <param name="lastTimestampSeconds">上次记录的时间戳（Unix秒）</param>
        /// <returns>true=已跨天（需要刷新每日任务等）</returns>
        public bool IsNewDay(long lastTimestampSeconds)
        {
            if (lastTimestampSeconds <= 0) return true;

            // 将时间戳转为UTC DateTime
            var lastTime = DateTimeOffset.FromUnixTimeSeconds(lastTimestampSeconds).UtcDateTime;
            var nowTime = ServerDateTimeUtc;

            // 计算"游戏日"（以UTC 21:00/北京05:00为分界）
            var lastGameDay = GetGameDay(lastTime);
            var nowGameDay = GetGameDay(nowTime);

            return nowGameDay > lastGameDay;
        }

        /// <summary>
        /// 获取距离每日刷新的剩余秒数
        /// </summary>
        public int GetSecondsToNextDailyReset()
        {
            var now = ServerDateTimeUtc;
            var nextReset = now.Date.AddHours(DailyResetHourUTC);

            if (now >= nextReset)
            {
                nextReset = nextReset.AddDays(1);
            }

            return (int)(nextReset - now).TotalSeconds;
        }

        /// <summary>
        /// 获取距离指定时间戳的剩余秒数（用于倒计时显示）
        /// </summary>
        /// <param name="targetTimestampSeconds">目标时间戳（Unix秒）</param>
        /// <returns>剩余秒数（如果已过去则返回0）</returns>
        public int GetRemainingSeconds(long targetTimestampSeconds)
        {
            long remaining = targetTimestampSeconds - ServerTimeSeconds;
            return remaining > 0 ? (int)remaining : 0;
        }

        /// <summary>
        /// 将Unix时间戳格式化为显示字符串
        /// </summary>
        /// <param name="timestampSeconds">Unix时间戳（秒）</param>
        /// <param name="format">格式字符串</param>
        public string FormatTimestamp(long timestampSeconds, string format = "yyyy-MM-dd HH:mm:ss")
        {
            var dt = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds).UtcDateTime.AddHours(8);
            return dt.ToString(format);
        }

        /// <summary>
        /// 将秒数格式化为 "HH:MM:SS" 倒计时字符串
        /// </summary>
        public string FormatCountdown(int totalSeconds)
        {
            if (totalSeconds <= 0) return "00:00:00";

            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;

            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        }

        // ========== 内部方法 ==========

        /// <summary>
        /// 获取"游戏日"编号
        /// 游戏日分界线：UTC 21:00 (= 北京 05:00)
        /// </summary>
        private int GetGameDay(DateTime utcTime)
        {
            // 将时间回退DailyResetHourUTC小时，使得分界线变成0:00
            var adjusted = utcTime.AddHours(-DailyResetHourUTC);
            return adjusted.Year * 10000 + adjusted.Month * 100 + adjusted.Day;
        }
    }
}
