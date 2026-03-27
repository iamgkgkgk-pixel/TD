// ============================================================
// 文件名：WXLogin.cs
// 功能描述：微信登录流程管理器 — 完整登录链路
//          静默登录→检查session→获取用户信息→创建/登录游戏账号
// 创建时间：2026-03-25
// 所属模块：Platform
// 对应交互：阶段二 #65
// ============================================================

using System;
using UnityEngine;

namespace AetheraSurvivors.Platform
{
    /// <summary>
    /// 登录状态枚举
    /// </summary>
    public enum LoginState
    {
        /// <summary>未登录</summary>
        NotLoggedIn = 0,

        /// <summary>正在登录</summary>
        LoggingIn = 1,

        /// <summary>已登录</summary>
        LoggedIn = 2,

        /// <summary>登录失败</summary>
        LoginFailed = 3
    }

    /// <summary>
    /// 登录请求数据（发送到游戏服务端）
    /// </summary>
    [Serializable]
    public class GameLoginRequest
    {
        /// <summary>微信登录凭证</summary>
        public string wx_code;
        /// <summary>设备型号</summary>
        public string device_model;
        /// <summary>系统版本</summary>
        public string system;
        /// <summary>客户端版本</summary>
        public string client_version;
    }

    /// <summary>
    /// 登录响应数据（游戏服务端返回）
    /// </summary>
    [Serializable]
    public class GameLoginResponse
    {
        /// <summary>是否成功</summary>
        public bool success;
        /// <summary>游戏用户ID</summary>
        public string user_id;
        /// <summary>访问令牌</summary>
        public string access_token;
        /// <summary>令牌过期时间（Unix秒）</summary>
        public long token_expire;
        /// <summary>是否新用户</summary>
        public bool is_new_user;
        /// <summary>错误信息</summary>
        public string error;
    }

    /// <summary>
    /// 微信登录流程管理器 — 全局单例
    /// 
    /// 完整登录流程：
    /// 1. 检查本地缓存的Token是否有效 → 有效则跳过登录
    /// 2. 检查wx.checkSession → Session有效则用缓存的code
    /// 3. 调用wx.login获取新code
    /// 4. 将code发送到游戏服务端换取Token
    /// 5. 缓存Token到本地
    /// 6. 设置HttpClient的Authorization头
    /// 
    /// 错误处理：
    /// - wx.login失败 → 重试最多3次
    /// - 服务端返回错误 → 重试最多2次
    /// - 全部失败 → 回调通知UI显示"登录失败"
    /// 
    /// 使用示例：
    ///   WXLogin.Instance.StartLogin(
    ///       onSuccess: (userId) => Debug.Log("登录成功: " + userId),
    ///       onFail: (error) => ShowLoginFailDialog(error)
    ///   );
    /// </summary>
    public class WXLogin : Framework.Singleton<WXLogin>
    {
        // ========== 常量 ==========

        /// <summary>游戏服务端登录接口路径</summary>
        private const string LoginApiPath = "/api/auth/login";

        /// <summary>wx.login最大重试次数</summary>
        private const int MaxWXLoginRetries = 3;

        /// <summary>服务端登录最大重试次数</summary>
        private const int MaxServerLoginRetries = 2;

        /// <summary>Token缓存Key</summary>
        private const string TokenCacheKey = "cached_access_token";

        /// <summary>Token过期时间缓存Key</summary>
        private const string TokenExpireKey = "cached_token_expire";

        /// <summary>用户ID缓存Key</summary>
        private const string UserIdCacheKey = "cached_user_id";

        // ========== 私有字段 ==========

        /// <summary>当前登录状态</summary>
        private LoginState _state = LoginState.NotLoggedIn;

        /// <summary>当前用户ID</summary>
        private string _userId;

        /// <summary>当前访问令牌</summary>
        private string _accessToken;

        /// <summary>令牌过期时间（Unix秒）</summary>
        private long _tokenExpire;

        /// <summary>是否为新用户</summary>
        private bool _isNewUser;

        /// <summary>wx.login当前重试次数</summary>
        private int _wxLoginRetryCount;

        /// <summary>服务端登录当前重试次数</summary>
        private int _serverLoginRetryCount;

        /// <summary>登录成功回调</summary>
        private Action<string> _onLoginSuccess;

        /// <summary>登录失败回调</summary>
        private Action<string> _onLoginFail;

        // ========== 公共属性 ==========

        /// <summary>当前登录状态</summary>
        public LoginState State => _state;

        /// <summary>是否已登录</summary>
        public bool IsLoggedIn => _state == LoginState.LoggedIn;

        /// <summary>当前用户ID</summary>
        public string UserId => _userId;

        /// <summary>当前访问令牌</summary>
        public string AccessToken => _accessToken;

        /// <summary>是否为新用户（本次登录注册的）</summary>
        public bool IsNewUser => _isNewUser;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            Framework.Logger.I("WXLogin", "登录管理器初始化");

            // 尝试从本地缓存恢复Token
            TryRestoreCachedToken();
        }

        protected override void OnDispose()
        {
            _onLoginSuccess = null;
            _onLoginFail = null;
        }

        // ========== 公共方法：登录 ==========

        /// <summary>
        /// 启动登录流程
        /// 如果已有有效Token，直接返回成功
        /// </summary>
        /// <param name="onSuccess">成功回调，参数为userId</param>
        /// <param name="onFail">失败回调，参数为错误信息</param>
        /// <param name="forceRelogin">是否强制重新登录（忽略缓存Token）</param>
        public void StartLogin(Action<string> onSuccess = null, Action<string> onFail = null,
            bool forceRelogin = false)
        {
            _onLoginSuccess = onSuccess;
            _onLoginFail = onFail;

            // 如果已经在登录中，忽略
            if (_state == LoginState.LoggingIn)
            {
                Framework.Logger.W("WXLogin", "登录进行中，忽略重复请求");
                return;
            }

            // 如果已登录且Token有效且不强制重登
            if (!forceRelogin && IsTokenValid())
            {
                Framework.Logger.I("WXLogin", "Token有效，跳过登录流程");
                _state = LoginState.LoggedIn;
                OnLoginFlowSuccess();
                return;
            }

            Framework.Logger.I("WXLogin", "开始登录流程...");
            _state = LoginState.LoggingIn;
            _wxLoginRetryCount = 0;
            _serverLoginRetryCount = 0;

            // Step1：检查Session
            CheckSessionAndLogin();
        }

        /// <summary>
        /// 登出（清除本地Token和状态）
        /// </summary>
        public void Logout()
        {
            Framework.Logger.I("WXLogin", "用户登出");

            _state = LoginState.NotLoggedIn;
            _userId = null;
            _accessToken = null;
            _tokenExpire = 0;
            _isNewUser = false;

            // 清除缓存
            ClearCachedToken();

            // 清除HttpClient的Token
            if (HttpClient.HasInstance)
            {
                HttpClient.Instance.SetAuthToken(null);
            }

            // 清除埋点用户ID
            if (AnalyticsManager.HasInstance)
            {
                AnalyticsManager.Instance.SetUserId("");
            }
        }

        // ========== 内部方法：登录流程 ==========

        /// <summary>Step1: 检查微信Session</summary>
        private void CheckSessionAndLogin()
        {
            if (!WXBridgeExtended.HasInstance)
            {
                Framework.Logger.W("WXLogin", "WXBridge不可用，直接进行wx.login");
                DoWXLogin();
                return;
            }

            WXBridgeExtended.Instance.CheckSession((isValid) =>
            {
                if (isValid)
                {
                    Framework.Logger.D("WXLogin", "Session有效");
                }
                else
                {
                    Framework.Logger.D("WXLogin", "Session已过期，需要重新登录");
                }

                // 无论Session是否有效，都需要获取新code
                // （服务端需要用code换session_key）
                DoWXLogin();
            });
        }

        /// <summary>Step2: 调用wx.login获取code</summary>
        private void DoWXLogin()
        {
            if (!WXBridgeExtended.HasInstance)
            {
                Framework.Logger.W("WXLogin", "WXBridge不可用，使用Mock登录");
                DoServerLogin("MOCK_CODE_EDITOR");
                return;
            }

            WXBridgeExtended.Instance.Login(
                onSuccess: (result) =>
                {
                    if (!string.IsNullOrEmpty(result.code))
                    {
                        Framework.Logger.I("WXLogin", "wx.login成功, code长度={0}", result.code.Length);
                        DoServerLogin(result.code);
                    }
                    else
                    {
                        HandleWXLoginFail("wx.login返回空code: " + result.errMsg);
                    }
                },
                onFail: (error) =>
                {
                    HandleWXLoginFail(error);
                }
            );
        }

        /// <summary>处理wx.login失败（含重试）</summary>
        private void HandleWXLoginFail(string error)
        {
            _wxLoginRetryCount++;

            if (_wxLoginRetryCount < MaxWXLoginRetries)
            {
                Framework.Logger.W("WXLogin", "wx.login失败，重试 ({0}/{1}): {2}",
                    _wxLoginRetryCount, MaxWXLoginRetries, error);
                DoWXLogin();
            }
            else
            {
                Framework.Logger.E("WXLogin", "wx.login最终失败: {0}", error);
                OnLoginFlowFail("微信登录失败，请检查网络后重试");
            }
        }

        /// <summary>Step3: 将code发送到游戏服务端换取Token</summary>
        private void DoServerLogin(string wxCode)
        {
            // 检查HttpClient是否可用
            if (!HttpClient.HasInstance)
            {
                Framework.Logger.W("WXLogin", "HttpClient不可用，使用Mock登录结果");
                HandleMockLogin();
                return;
            }

            var systemInfo = WXBridgeExtended.HasInstance
                ? WXBridgeExtended.Instance.SystemInfo
                : new WXSystemInfo { model = "Editor", system = "Unknown" };

            var loginReq = new GameLoginRequest
            {
                wx_code = wxCode,
                device_model = systemInfo?.model ?? "Unknown",
                system = systemInfo?.system ?? "Unknown",
                client_version = Application.version
            };

            HttpClient.Instance.Post(LoginApiPath, loginReq,
                onSuccess: (response) =>
                {
                    try
                    {
                        var loginResp = response.ParseBody<GameLoginResponse>();

                        if (loginResp.success)
                        {
                            // 登录成功
                            _userId = loginResp.user_id;
                            _accessToken = loginResp.access_token;
                            _tokenExpire = loginResp.token_expire;
                            _isNewUser = loginResp.is_new_user;

                            // 缓存Token
                            CacheToken();

                            // 设置HttpClient的Token
                            HttpClient.Instance.SetAuthToken(_accessToken);

                            Framework.Logger.I("WXLogin", "✅ 服务端登录成功: userId={0}, isNew={1}",
                                _userId, _isNewUser);

                            OnLoginFlowSuccess();
                        }
                        else
                        {
                            HandleServerLoginFail(loginResp.error ?? "服务端返回失败");
                        }
                    }
                    catch (Exception e)
                    {
                        HandleServerLoginFail("解析登录响应失败: " + e.Message);
                    }
                },
                onError: (response) =>
                {
                    HandleServerLoginFail(response.Error ?? "网络请求失败");
                }
            );
        }

        /// <summary>处理服务端登录失败（含重试）</summary>
        private void HandleServerLoginFail(string error)
        {
            _serverLoginRetryCount++;

            if (_serverLoginRetryCount < MaxServerLoginRetries)
            {
                Framework.Logger.W("WXLogin", "服务端登录失败，重新获取code重试 ({0}/{1}): {2}",
                    _serverLoginRetryCount, MaxServerLoginRetries, error);
                _wxLoginRetryCount = 0; // 重置wx.login重试计数
                DoWXLogin(); // 重新从wx.login开始
            }
            else
            {
                Framework.Logger.E("WXLogin", "服务端登录最终失败: {0}", error);
                OnLoginFlowFail("服务器连接失败，请稍后重试");
            }
        }

        /// <summary>Mock登录（服务端不可用时的编辑器模拟）</summary>
        private void HandleMockLogin()
        {
            Framework.Logger.I("WXLogin", "使用Mock登录（编辑器模式）");
            _userId = "mock_user_001";
            _accessToken = "mock_token_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _tokenExpire = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 86400; // 24小时
            _isNewUser = false;

            CacheToken();
            OnLoginFlowSuccess();
        }

        // ========== 内部方法：登录流程完成 ==========

        /// <summary>登录流程成功</summary>
        private void OnLoginFlowSuccess()
        {
            _state = LoginState.LoggedIn;

            // 设置埋点用户ID
            if (AnalyticsManager.HasInstance)
            {
                AnalyticsManager.Instance.SetUserId(_userId);
            }

            // 发布登录事件
            if (Framework.EventBus.HasInstance)
            {
                Framework.EventBus.Instance.Publish(new WXLoginCompleteEvent
                {
                    Success = true,
                    UserId = _userId,
                    Error = ""
                });
            }

            _onLoginSuccess?.Invoke(_userId);
            _onLoginSuccess = null;
            _onLoginFail = null;
        }

        /// <summary>登录流程失败</summary>
        private void OnLoginFlowFail(string error)
        {
            _state = LoginState.LoginFailed;

            // 发布登录失败事件
            if (Framework.EventBus.HasInstance)
            {
                Framework.EventBus.Instance.Publish(new WXLoginCompleteEvent
                {
                    Success = false,
                    UserId = "",
                    Error = error
                });
            }

            _onLoginFail?.Invoke(error);
            _onLoginSuccess = null;
            _onLoginFail = null;
        }

        // ========== 内部方法：Token管理 ==========

        /// <summary>检查Token是否仍然有效</summary>
        private bool IsTokenValid()
        {
            if (string.IsNullOrEmpty(_accessToken)) return false;
            if (_tokenExpire <= 0) return false;

            // 预留60秒缓冲，避免刚好到期时请求失败
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return _tokenExpire > (now + 60);
        }

        /// <summary>缓存Token到本地</summary>
        private void CacheToken()
        {
            if (Framework.SaveManager.HasInstance)
            {
                // 使用SaveManager存储（会自动加密）
                Framework.SaveManager.Instance.Save(TokenCacheKey, 
                    new TokenCache { token = _accessToken, expire = _tokenExpire, userId = _userId });
            }
        }

        /// <summary>尝试从本地缓存恢复Token</summary>
        private void TryRestoreCachedToken()
        {
            if (!Framework.SaveManager.HasInstance) return;

            try
            {
                var cache = Framework.SaveManager.Instance.Load<TokenCache>(TokenCacheKey);
                if (cache != null && !string.IsNullOrEmpty(cache.token))
                {
                    _accessToken = cache.token;
                    _tokenExpire = cache.expire;
                    _userId = cache.userId;

                    if (IsTokenValid())
                    {
                        Framework.Logger.I("WXLogin", "从缓存恢复Token: userId={0}", _userId);
                    }
                    else
                    {
                        Framework.Logger.D("WXLogin", "缓存Token已过期");
                        _accessToken = null;
                        _tokenExpire = 0;
                    }
                }
            }
            catch (Exception e)
            {
                Framework.Logger.W("WXLogin", "恢复缓存Token失败: {0}", e.Message);
            }
        }

        /// <summary>清除缓存的Token</summary>
        private void ClearCachedToken()
        {
            if (Framework.SaveManager.HasInstance)
            {
                Framework.SaveManager.Instance.Delete(TokenCacheKey);
            }
        }

        /// <summary>Token缓存数据结构</summary>
        [Serializable]
        private class TokenCache
        {
            public string token;
            public long expire;
            public string userId;
        }
    }
}
