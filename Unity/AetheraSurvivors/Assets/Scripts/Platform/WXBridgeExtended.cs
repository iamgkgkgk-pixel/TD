// ============================================================
// 文件名：WXBridgeExtended.cs
// 功能描述：微信小游戏SDK完整桥接层 — 封装所有常用微信API
//          这是所有微信功能（登录/支付/分享/广告等）的基础
// 创建时间：2026-03-25
// 所属模块：Platform
// 对应交互：阶段二 #62
// ============================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AetheraSurvivors.Platform
{
    /// <summary>
    /// 微信系统信息数据
    /// </summary>
    [Serializable]
    public class WXSystemInfo
    {
        public string brand;
        public string model;
        public string system;
        public string platform;
        public string SDKVersion;
        public int screenWidth;
        public int screenHeight;
        public int windowWidth;
        public int windowHeight;
        public float pixelRatio;
        public int benchmarkLevel;
    }

    /// <summary>
    /// 微信登录结果
    /// </summary>
    [Serializable]
    public class WXLoginResult
    {
        public string code;
        public string errMsg;
    }

    /// <summary>
    /// 微信用户信息
    /// </summary>
    [Serializable]
    public class WXUserInfo
    {
        public string nickName;
        public string avatarUrl;
        public int gender;
        public string country;
        public string province;
        public string city;
        public string language;
    }

    /// <summary>
    /// 微信启动参数（场景值+query参数）
    /// </summary>
    [Serializable]
    public class WXLaunchOptions
    {
        public int scene;
        public string query;
        public string referrerInfo;
    }

    /// <summary>
    /// 微信SDK完整桥接管理器 — 全局MonoSingleton
    /// 
    /// 职责：
    /// 1. 封装所有微信小游戏常用API（C#调用JS）
    /// 2. 统一处理异步回调（通过SendMessage机制）
    /// 3. 编辑器环境自动Mock（不依赖微信SDK）
    /// 4. 生命周期监听（onShow/onHide → EventBus事件）
    /// 
    /// 桥接机制：
    ///   C#(DllImport) → JS(WXBridgeExt.jslib) → wx.xxx API → JS回调 
    ///   → SendMessage → C#(OnXXXCallback)
    /// 
    /// 依赖：
    ///   - Framework.MonoSingleton（单例基类）
    ///   - Framework.EventBus（事件分发）
    ///   - Plugins/WebGL/WXBridgeExt.jslib（JS桥接文件）
    /// </summary>
    public class WXBridgeExtended : AetheraSurvivors.Framework.MonoSingleton<WXBridgeExtended>
    {
        // ========================================
        // JS 桥接函数声明（仅在 WebGL 平台生效）
        // ========================================

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WXEXT_Login(string callbackObj, string successMethod, string failMethod);

        [DllImport("__Internal")]
        private static extern void WXEXT_CheckSession(string callbackObj, string successMethod, string failMethod);

        [DllImport("__Internal")]
        private static extern void WXEXT_GetUserInfo(string callbackObj, string successMethod, string failMethod);

        [DllImport("__Internal")]
        private static extern string WXEXT_GetSystemInfoSync();

        [DllImport("__Internal")]
        private static extern void WXEXT_GetLaunchOptions(string callbackObj, string callbackMethod);

        [DllImport("__Internal")]
        private static extern void WXEXT_RegisterOnShow(string callbackObj, string callbackMethod);

        [DllImport("__Internal")]
        private static extern void WXEXT_RegisterOnHide(string callbackObj, string callbackMethod);

        [DllImport("__Internal")]
        private static extern void WXEXT_ShareAppMessage(string title, string imageUrl, string query);

        [DllImport("__Internal")]
        private static extern void WXEXT_ShowShareMenu();

        [DllImport("__Internal")]
        private static extern void WXEXT_ReportEvent(string eventName, string paramsJson);

        [DllImport("__Internal")]
        private static extern void WXEXT_SetClipboardData(string data);

        [DllImport("__Internal")]
        private static extern void WXEXT_Vibrate(bool isLong);

        [DllImport("__Internal")]
        private static extern void WXEXT_ShowToast(string title, string icon, int duration);

        [DllImport("__Internal")]
        private static extern void WXEXT_ShowModal(string title, string content, string callbackObj, string callbackMethod);

        [DllImport("__Internal")]
        private static extern void WXEXT_SetStorageSync(string key, string value);

        [DllImport("__Internal")]
        private static extern string WXEXT_GetStorageSync(string key);

        [DllImport("__Internal")]
        private static extern void WXEXT_RemoveStorageSync(string key);
#endif

        // ========================================
        // 回调委托
        // ========================================

        /// <summary>登录成功回调</summary>
        private Action<WXLoginResult> _loginSuccessCallback;
        /// <summary>登录失败回调</summary>
        private Action<string> _loginFailCallback;

        /// <summary>检查Session回调</summary>
        private Action<bool> _checkSessionCallback;

        /// <summary>获取用户信息回调</summary>
        private Action<WXUserInfo> _getUserInfoSuccessCallback;
        private Action<string> _getUserInfoFailCallback;

        /// <summary>模态对话框回调</summary>
        private Action<bool> _modalCallback;

        // ========================================
        // 缓存数据
        // ========================================

        /// <summary>缓存的系统信息</summary>
        private WXSystemInfo _cachedSystemInfo;

        /// <summary>缓存的启动参数</summary>
        private WXLaunchOptions _cachedLaunchOptions;

        /// <summary>是否在前台</summary>
        private bool _isForeground = true;

        // ========================================
        // 公共属性
        // ========================================

        /// <summary>缓存的系统信息</summary>
        public WXSystemInfo SystemInfo => _cachedSystemInfo;

        /// <summary>启动参数</summary>
        public WXLaunchOptions LaunchOptions => _cachedLaunchOptions;

        /// <summary>是否在前台运行</summary>
        public bool IsForeground => _isForeground;

        /// <summary>是否在微信环境中运行</summary>
        public bool IsWXEnvironment
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        // ========================================
        // 生命周期
        // ========================================

        protected override void OnInit()
        {
            Framework.Logger.I("WXBridge", "微信桥接层初始化");

            // 获取并缓存系统信息
            _cachedSystemInfo = GetSystemInfoSyncInternal();
            Framework.Logger.I("WXBridge", "系统信息: {0} {1}, 屏幕: {2}x{3}", 
                _cachedSystemInfo.brand, _cachedSystemInfo.model,
                _cachedSystemInfo.screenWidth, _cachedSystemInfo.screenHeight);

            // 注册前后台切换监听
            RegisterLifecycleListeners();

            Framework.Logger.I("WXBridge", "✅ 微信桥接层初始化完成");
        }

        protected override void OnDispose()
        {
            _loginSuccessCallback = null;
            _loginFailCallback = null;
            _checkSessionCallback = null;
            _getUserInfoSuccessCallback = null;
            _getUserInfoFailCallback = null;
            _modalCallback = null;
        }

        // ========================================
        // 公共方法：登录相关
        // ========================================

        /// <summary>
        /// 调用wx.login获取登录凭证code
        /// </summary>
        /// <param name="onSuccess">成功回调，返回包含code的结果</param>
        /// <param name="onFail">失败回调，返回错误信息</param>
        public void Login(Action<WXLoginResult> onSuccess, Action<string> onFail = null)
        {
            _loginSuccessCallback = onSuccess;
            _loginFailCallback = onFail;

#if UNITY_WEBGL && !UNITY_EDITOR
            WXEXT_Login(gameObject.name, "OnLoginSuccess", "OnLoginFail");
#else
            // 编辑器模拟
            Framework.Logger.D("WXBridge", "编辑器模式 - 模拟wx.login");
            OnLoginSuccess("{\"code\":\"MOCK_LOGIN_CODE_EDITOR\",\"errMsg\":\"login:ok\"}");
#endif
        }

        /// <summary>
        /// 检查登录态是否过期
        /// </summary>
        /// <param name="callback">回调，true=session有效，false=过期需重新登录</param>
        public void CheckSession(Action<bool> callback)
        {
            _checkSessionCallback = callback;

#if UNITY_WEBGL && !UNITY_EDITOR
            WXEXT_CheckSession(gameObject.name, "OnCheckSessionSuccess", "OnCheckSessionFail");
#else
            Framework.Logger.D("WXBridge", "编辑器模式 - 模拟wx.checkSession: 有效");
            callback?.Invoke(true);
#endif
        }

        /// <summary>
        /// 获取用户信息（需要用户授权）
        /// </summary>
        /// <param name="onSuccess">成功回调</param>
        /// <param name="onFail">失败回调</param>
        public void GetUserInfo(Action<WXUserInfo> onSuccess, Action<string> onFail = null)
        {
            _getUserInfoSuccessCallback = onSuccess;
            _getUserInfoFailCallback = onFail;

#if UNITY_WEBGL && !UNITY_EDITOR
            WXEXT_GetUserInfo(gameObject.name, "OnGetUserInfoSuccess", "OnGetUserInfoFail");
#else
            Framework.Logger.D("WXBridge", "编辑器模式 - 模拟wx.getUserInfo");
            OnGetUserInfoSuccess("{\"nickName\":\"测试玩家\",\"avatarUrl\":\"\",\"gender\":1,\"country\":\"CN\",\"province\":\"Beijing\",\"city\":\"Beijing\",\"language\":\"zh_CN\"}");
#endif
        }

        // ========================================
        // 公共方法：系统信息
        // ========================================

        /// <summary>
        /// 同步获取系统信息（使用缓存，首次调用时获取）
        /// </summary>
        public WXSystemInfo GetSystemInfoCached()
        {
            if (_cachedSystemInfo == null)
            {
                _cachedSystemInfo = GetSystemInfoSyncInternal();
            }
            return _cachedSystemInfo;
        }

        // ========================================
        // 公共方法：分享
        // ========================================

        /// <summary>
        /// 设置默认分享内容（右上角"分享"按钮触发时的内容）
        /// </summary>
        /// <param name="title">分享标题</param>
        /// <param name="imageUrl">分享封面图URL</param>
        /// <param name="query">携带的查询参数（如 "inviter=12345&channel=share"）</param>
        public void SetShareAppMessage(string title, string imageUrl = "", string query = "")
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WXEXT_ShareAppMessage(title, imageUrl, query);
#else
            Framework.Logger.D("WXBridge", "编辑器模式 - 设置分享: {0}", title);
#endif
        }

        /// <summary>
        /// 显示分享菜单（允许用户分享到群/朋友圈）
        /// </summary>
        public void ShowShareMenu()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WXEXT_ShowShareMenu();
#else
            Framework.Logger.D("WXBridge", "编辑器模式 - 显示分享菜单");
#endif
        }

        // ========================================
        // 公共方法：数据上报
        // ========================================

        /// <summary>
        /// 上报自定义事件到微信后台
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="paramsJson">参数JSON字符串</param>
        public void ReportEvent(string eventName, string paramsJson = "")
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WXEXT_ReportEvent(eventName, paramsJson);
#else
            Framework.Logger.D("WXBridge", "编辑器模式 - 上报事件: {0}", eventName);
#endif
        }

        // ========================================
        // 公共方法：存储（直接桥接微信存储API）
        // ========================================

        /// <summary>
        /// 同步设置存储（wx.setStorageSync）
        /// </summary>
        public void SetStorage(string key, string value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WXEXT_SetStorageSync(key, value);
#else
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
#endif
        }

        /// <summary>
        /// 同步获取存储（wx.getStorageSync）
        /// </summary>
        public string GetStorage(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WXEXT_GetStorageSync(key);
#else
            return PlayerPrefs.GetString(key, string.Empty);
#endif
        }

        /// <summary>
        /// 同步删除存储（wx.removeStorageSync）
        /// </summary>
        public void RemoveStorage(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WXEXT_RemoveStorageSync(key);
#else
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
#endif
        }

        // ========================================
        // 公共方法：UI交互
        // ========================================

        /// <summary>
        /// 显示Toast提示
        /// </summary>
        /// <param name="title">提示文字</param>
        /// <param name="icon">图标类型：success/error/loading/none</param>
        /// <param name="duration">持续毫秒数</param>
        public void ShowToast(string title, string icon = "none", int duration = 1500)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WXEXT_ShowToast(title, icon, duration);
#else
            Framework.Logger.D("WXBridge", "Toast: {0} (icon={1})", title, icon);
#endif
        }

        /// <summary>
        /// 显示模态对话框
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="content">内容</param>
        /// <param name="callback">用户点击回调（true=确认，false=取消）</param>
        public void ShowModal(string title, string content, Action<bool> callback = null)
        {
            _modalCallback = callback;

#if UNITY_WEBGL && !UNITY_EDITOR
            WXEXT_ShowModal(title, content, gameObject.name, "OnModalCallback");
#else
            Framework.Logger.D("WXBridge", "Modal: {0} - {1}", title, content);
            // 编辑器默认模拟点击确认
            callback?.Invoke(true);
#endif
        }

        /// <summary>
        /// 设置剪贴板内容
        /// </summary>
        public void SetClipboard(string data)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WXEXT_SetClipboardData(data);
#else
            GUIUtility.systemCopyBuffer = data;
            Framework.Logger.D("WXBridge", "剪贴板: {0}", data);
#endif
        }

        /// <summary>
        /// 触发震动反馈
        /// </summary>
        /// <param name="isLong">true=长震动(400ms), false=短震动(15ms)</param>
        public void Vibrate(bool isLong = false)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WXEXT_Vibrate(isLong);
#else
            Framework.Logger.D("WXBridge", "震动: {0}", isLong ? "长" : "短");
#endif
        }

        // ========================================
        // JS回调接收方法（由SendMessage调用）
        // ========================================

        /// <summary>登录成功回调</summary>
        public void OnLoginSuccess(string json)
        {
            Framework.Logger.I("WXBridge", "登录成功: {0}", json);
            try
            {
                var result = JsonUtility.FromJson<WXLoginResult>(json);
                _loginSuccessCallback?.Invoke(result);
            }
            catch (Exception e)
            {
                Framework.Logger.E("WXBridge", "解析登录结果失败", e);
                _loginFailCallback?.Invoke("解析登录结果失败: " + e.Message);
            }
            finally
            {
                _loginSuccessCallback = null;
                _loginFailCallback = null;
            }
        }

        /// <summary>登录失败回调</summary>
        public void OnLoginFail(string errorJson)
        {
            Framework.Logger.W("WXBridge", "登录失败: {0}", errorJson);
            _loginFailCallback?.Invoke(errorJson);
            _loginSuccessCallback = null;
            _loginFailCallback = null;
        }

        /// <summary>检查Session成功回调</summary>
        public void OnCheckSessionSuccess(string json)
        {
            _checkSessionCallback?.Invoke(true);
            _checkSessionCallback = null;
        }

        /// <summary>检查Session失败回调（Session已过期）</summary>
        public void OnCheckSessionFail(string json)
        {
            _checkSessionCallback?.Invoke(false);
            _checkSessionCallback = null;
        }

        /// <summary>获取用户信息成功回调</summary>
        public void OnGetUserInfoSuccess(string json)
        {
            Framework.Logger.I("WXBridge", "获取用户信息成功");
            try
            {
                var info = JsonUtility.FromJson<WXUserInfo>(json);
                _getUserInfoSuccessCallback?.Invoke(info);
            }
            catch (Exception e)
            {
                Framework.Logger.E("WXBridge", "解析用户信息失败", e);
                _getUserInfoFailCallback?.Invoke("解析用户信息失败: " + e.Message);
            }
            finally
            {
                _getUserInfoSuccessCallback = null;
                _getUserInfoFailCallback = null;
            }
        }

        /// <summary>获取用户信息失败回调</summary>
        public void OnGetUserInfoFail(string errorJson)
        {
            Framework.Logger.W("WXBridge", "获取用户信息失败: {0}", errorJson);
            _getUserInfoFailCallback?.Invoke(errorJson);
            _getUserInfoSuccessCallback = null;
            _getUserInfoFailCallback = null;
        }

        /// <summary>模态对话框回调</summary>
        public void OnModalCallback(string result)
        {
            // result: "confirm" 或 "cancel"
            bool isConfirm = result == "confirm";
            _modalCallback?.Invoke(isConfirm);
            _modalCallback = null;
        }

        /// <summary>应用进入前台回调</summary>
        public void OnAppShow(string json)
        {
            _isForeground = true;
            Framework.Logger.I("WXBridge", "应用回到前台");

            // 解析启动参数（onShow也会携带场景值和query）
            try
            {
                if (!string.IsNullOrEmpty(json))
                {
                    _cachedLaunchOptions = JsonUtility.FromJson<WXLaunchOptions>(json);
                }
            }
            catch (Exception e)
            {
                Framework.Logger.W("WXBridge", "解析onShow参数失败: {0}", e.Message);
            }

            // 发布前后台切换事件
            if (Framework.EventBus.HasInstance)
            {
                Framework.EventBus.Instance.Publish(new WXAppShowEvent
                {
                    Scene = _cachedLaunchOptions?.scene ?? 0,
                    Query = _cachedLaunchOptions?.query ?? ""
                });
            }
        }

        /// <summary>应用进入后台回调</summary>
        public void OnAppHide(string json)
        {
            _isForeground = false;
            Framework.Logger.I("WXBridge", "应用进入后台");

            // 发布后台事件
            if (Framework.EventBus.HasInstance)
            {
                Framework.EventBus.Instance.Publish(new WXAppHideEvent());
            }
        }

        // ========================================
        // 内部方法
        // ========================================

        /// <summary>注册前后台切换监听</summary>
        private void RegisterLifecycleListeners()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WXEXT_RegisterOnShow(gameObject.name, "OnAppShow");
            WXEXT_RegisterOnHide(gameObject.name, "OnAppHide");
            Framework.Logger.I("WXBridge", "已注册onShow/onHide监听");
#else
            Framework.Logger.D("WXBridge", "编辑器模式 - 跳过onShow/onHide注册");
#endif
        }

        /// <summary>同步获取系统信息</summary>
        private WXSystemInfo GetSystemInfoSyncInternal()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                string json = WXEXT_GetSystemInfoSync();
                return JsonUtility.FromJson<WXSystemInfo>(json);
            }
            catch (Exception e)
            {
                Framework.Logger.E("WXBridge", "获取系统信息失败", e);
                return CreateMockSystemInfo();
            }
#else
            return CreateMockSystemInfo();
#endif
        }

        /// <summary>创建模拟的系统信息（编辑器用）</summary>
        private WXSystemInfo CreateMockSystemInfo()
        {
            return new WXSystemInfo
            {
                brand = "Editor",
                model = "Unity Editor",
                system = UnityEngine.SystemInfo.operatingSystem,
                platform = "devtools",
                SDKVersion = "mock",
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                windowWidth = Screen.width,
                windowHeight = Screen.height,
                pixelRatio = 1f,
                benchmarkLevel = 30
            };
        }
    }

    // ====================================================================
    // 微信平台相关事件定义
    // ====================================================================

    /// <summary>
    /// 微信应用回到前台事件
    /// </summary>
    public struct WXAppShowEvent : Framework.IEvent
    {
        /// <summary>场景值（用于追踪启动来源）</summary>
        public int Scene;
        /// <summary>查询参数字符串</summary>
        public string Query;
    }

    /// <summary>
    /// 微信应用进入后台事件
    /// </summary>
    public struct WXAppHideEvent : Framework.IEvent
    {
    }

    /// <summary>
    /// 微信登录完成事件
    /// </summary>
    public struct WXLoginCompleteEvent : Framework.IEvent
    {
        /// <summary>是否成功</summary>
        public bool Success;
        /// <summary>用户ID（成功时有值）</summary>
        public string UserId;
        /// <summary>错误信息（失败时有值）</summary>
        public string Error;
    }
}
