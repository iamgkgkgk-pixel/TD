// ============================================================
// 文件名：HttpClient.cs
// 功能描述：网络通信基础层 — GET/POST请求、JSON序列化、超时重试
//          适配微信小游戏的wx.request（非XMLHttpRequest）
// 创建时间：2026-03-25
// 所属模块：Platform
// 对应交互：阶段二 #63
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AetheraSurvivors.Platform
{
    /// <summary>
    /// HTTP请求方法
    /// </summary>
    public enum HttpMethod
    {
        GET,
        POST,
        PUT,
        DELETE
    }

    /// <summary>
    /// HTTP响应数据
    /// </summary>
    public class HttpResponse
    {
        /// <summary>HTTP状态码</summary>
        public long StatusCode;

        /// <summary>响应体文本</summary>
        public string Body;

        /// <summary>是否成功（状态码200-299）</summary>
        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;

        /// <summary>错误信息（失败时有值）</summary>
        public string Error;

        /// <summary>响应头</summary>
        public Dictionary<string, string> Headers;

        /// <summary>将响应体解析为指定类型</summary>
        public T ParseBody<T>()
        {
            if (string.IsNullOrEmpty(Body)) return default;
            return JsonUtility.FromJson<T>(Body);
        }
    }

    /// <summary>
    /// HTTP请求配置
    /// </summary>
    public class HttpRequest
    {
        /// <summary>请求URL</summary>
        public string Url;

        /// <summary>请求方法</summary>
        public HttpMethod Method = HttpMethod.GET;

        /// <summary>请求头</summary>
        public Dictionary<string, string> Headers;

        /// <summary>请求体（POST/PUT时使用）</summary>
        public string Body;

        /// <summary>超时时间（秒）</summary>
        public int Timeout = 10;

        /// <summary>最大重试次数</summary>
        public int MaxRetries = 2;

        /// <summary>重试间隔（秒）</summary>
        public float RetryInterval = 1f;

        /// <summary>成功回调</summary>
        public Action<HttpResponse> OnSuccess;

        /// <summary>失败回调</summary>
        public Action<HttpResponse> OnError;

        /// <summary>完成回调（无论成功失败）</summary>
        public Action<HttpResponse> OnComplete;

        public HttpRequest(string url)
        {
            Url = url;
            Headers = new Dictionary<string, string>();
        }

        /// <summary>设置JSON请求体</summary>
        public HttpRequest SetJsonBody(object data)
        {
            Body = JsonUtility.ToJson(data);
            Headers["Content-Type"] = "application/json";
            return this;
        }

        /// <summary>设置请求头</summary>
        public HttpRequest SetHeader(string key, string value)
        {
            Headers[key] = value;
            return this;
        }
    }

    /// <summary>
    /// HTTP客户端 — 网络通信管理器（MonoSingleton，需要协程）
    /// 
    /// 特性：
    /// 1. 支持GET/POST/PUT/DELETE请求
    /// 2. 自动JSON序列化/反序列化
    /// 3. 超时控制和自动重试
    /// 4. 统一错误处理
    /// 5. 编辑器使用UnityWebRequest
    /// 6. 微信平台后续可替换为wx.request桥接
    /// 7. 公共请求头（Token/签名自动附加）
    /// 
    /// 使用示例：
    ///   // GET请求
    ///   HttpClient.Instance.Get("https://api.example.com/data",
    ///       onSuccess: resp => Debug.Log(resp.Body),
    ///       onError: resp => Debug.LogError(resp.Error)
    ///   );
    ///   
    ///   // POST请求
    ///   HttpClient.Instance.Post("https://api.example.com/login",
    ///       new LoginRequest { username = "test" },
    ///       onSuccess: resp => {
    ///           var result = resp.ParseBody&lt;LoginResponse&gt;();
    ///       }
    ///   );
    /// </summary>
    public class HttpClient : AetheraSurvivors.Framework.MonoSingleton<HttpClient>
    {
        // ========== 常量 ==========

        /// <summary>默认超时时间（秒）</summary>
        private const int DefaultTimeout = 10;

        /// <summary>默认最大重试次数</summary>
        private const int DefaultMaxRetries = 2;

        // ========== 私有字段 ==========

        /// <summary>公共请求头（每个请求都会附带）</summary>
        private readonly Dictionary<string, string> _commonHeaders = new Dictionary<string, string>();

        /// <summary>服务端基础URL</summary>
        private string _baseUrl = "";

        /// <summary>当前活跃请求数</summary>
        private int _activeRequests = 0;

        // ========== 公共属性 ==========

        /// <summary>当前活跃请求数</summary>
        public int ActiveRequests => _activeRequests;

        /// <summary>是否有正在进行的请求</summary>
        public bool IsBusy => _activeRequests > 0;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            // 设置默认公共请求头
            _commonHeaders["Accept"] = "application/json";
            _commonHeaders["X-Client-Version"] = Application.version;
            _commonHeaders["X-Platform"] = Application.platform.ToString();

            Framework.Logger.I("HttpClient", "✅ HTTP客户端初始化完成");
        }

        protected override void OnDispose()
        {
            StopAllCoroutines();
            _commonHeaders.Clear();
        }

        // ========== 公共方法：配置 ==========

        /// <summary>
        /// 设置服务端基础URL（所有请求的URL前缀）
        /// </summary>
        /// <param name="baseUrl">如 "https://api.example.com/v1"</param>
        public void SetBaseUrl(string baseUrl)
        {
            _baseUrl = baseUrl?.TrimEnd('/') ?? "";
            Framework.Logger.I("HttpClient", "BaseURL: {0}", _baseUrl);
        }

        /// <summary>
        /// 设置公共请求头（如Authorization Token）
        /// </summary>
        public void SetCommonHeader(string key, string value)
        {
            _commonHeaders[key] = value;
        }

        /// <summary>
        /// 移除公共请求头
        /// </summary>
        public void RemoveCommonHeader(string key)
        {
            _commonHeaders.Remove(key);
        }

        /// <summary>
        /// 设置认证Token（自动添加Authorization头）
        /// </summary>
        public void SetAuthToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _commonHeaders.Remove("Authorization");
            }
            else
            {
                _commonHeaders["Authorization"] = "Bearer " + token;
            }
        }

        // ========== 公共方法：便捷请求 ==========

        /// <summary>
        /// 发送GET请求
        /// </summary>
        /// <param name="path">请求路径（会自动拼接BaseURL）</param>
        /// <param name="onSuccess">成功回调</param>
        /// <param name="onError">失败回调</param>
        /// <param name="timeout">超时秒数</param>
        public void Get(string path, Action<HttpResponse> onSuccess = null,
            Action<HttpResponse> onError = null, int timeout = DefaultTimeout)
        {
            var request = new HttpRequest(BuildUrl(path))
            {
                Method = HttpMethod.GET,
                Timeout = timeout,
                OnSuccess = onSuccess,
                OnError = onError
            };
            Send(request);
        }

        /// <summary>
        /// 发送POST请求（自动JSON序列化）
        /// </summary>
        /// <param name="path">请求路径</param>
        /// <param name="data">请求体数据对象（会自动序列化为JSON）</param>
        /// <param name="onSuccess">成功回调</param>
        /// <param name="onError">失败回调</param>
        /// <param name="timeout">超时秒数</param>
        public void Post(string path, object data, Action<HttpResponse> onSuccess = null,
            Action<HttpResponse> onError = null, int timeout = DefaultTimeout)
        {
            var request = new HttpRequest(BuildUrl(path))
            {
                Method = HttpMethod.POST,
                Timeout = timeout,
                OnSuccess = onSuccess,
                OnError = onError
            };
            request.SetJsonBody(data);
            Send(request);
        }

        /// <summary>
        /// 发送POST请求（原始字符串Body）
        /// </summary>
        public void PostRaw(string path, string body, Action<HttpResponse> onSuccess = null,
            Action<HttpResponse> onError = null, int timeout = DefaultTimeout)
        {
            var request = new HttpRequest(BuildUrl(path))
            {
                Method = HttpMethod.POST,
                Body = body,
                Timeout = timeout,
                OnSuccess = onSuccess,
                OnError = onError
            };
            request.Headers["Content-Type"] = "application/json";
            Send(request);
        }

        /// <summary>
        /// 发送自定义请求
        /// </summary>
        public void Send(HttpRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Url))
            {
                Framework.Logger.E("HttpClient", "请求参数无效");
                return;
            }

            StartCoroutine(SendRequestCoroutine(request, 0));
        }

        // ========== 协程：请求发送 ==========

        /// <summary>
        /// 发送请求的协程（含重试逻辑）
        /// </summary>
        private IEnumerator SendRequestCoroutine(HttpRequest request, int retryCount)
        {
            _activeRequests++;

            Framework.Logger.D("HttpClient", "{0} {1} (retry={2})",
                request.Method, request.Url, retryCount);

            // 创建UnityWebRequest
            UnityWebRequest webRequest = CreateWebRequest(request);

            // 附加公共请求头
            foreach (var header in _commonHeaders)
            {
                webRequest.SetRequestHeader(header.Key, header.Value);
            }

            // 附加自定义请求头
            if (request.Headers != null)
            {
                foreach (var header in request.Headers)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }
            }

            // 设置超时
            webRequest.timeout = request.Timeout;

            // 发送请求
            yield return webRequest.SendWebRequest();

            // 构造响应
            var response = new HttpResponse
            {
                StatusCode = webRequest.responseCode,
                Body = webRequest.downloadHandler?.text ?? "",
                Headers = new Dictionary<string, string>()
            };

            // 解析响应头
            var responseHeaders = webRequest.GetResponseHeaders();
            if (responseHeaders != null)
            {
                foreach (var header in responseHeaders)
                {
                    response.Headers[header.Key] = header.Value;
                }
            }

            // 判断结果
            bool isNetworkError = webRequest.result == UnityWebRequest.Result.ConnectionError;
            bool isHttpError = webRequest.result == UnityWebRequest.Result.ProtocolError;
            bool isTimeout = webRequest.result == UnityWebRequest.Result.ConnectionError &&
                             webRequest.error?.Contains("timeout") == true;

            if (isNetworkError || isTimeout)
            {
                response.Error = webRequest.error;

                // 网络错误/超时 → 重试
                if (retryCount < request.MaxRetries)
                {
                    Framework.Logger.W("HttpClient", "请求失败，{0}秒后重试 ({1}/{2}): {3}",
                        request.RetryInterval, retryCount + 1, request.MaxRetries, request.Url);

                    _activeRequests--;
                    webRequest.Dispose();

                    yield return new WaitForSecondsRealtime(request.RetryInterval);
                    StartCoroutine(SendRequestCoroutine(request, retryCount + 1));
                    yield break;
                }

                Framework.Logger.E("HttpClient", "请求最终失败（已达最大重试次数）: {0}, Error: {1}",
                    request.Url, response.Error);
                request.OnError?.Invoke(response);
            }
            else if (isHttpError)
            {
                response.Error = $"HTTP {response.StatusCode}: {webRequest.error}";
                Framework.Logger.W("HttpClient", "HTTP错误: {0} → {1}", request.Url, response.Error);
                request.OnError?.Invoke(response);
            }
            else
            {
                // 成功
                Framework.Logger.D("HttpClient", "请求成功: {0} → {1}B",
                    request.Url, response.Body?.Length ?? 0);
                request.OnSuccess?.Invoke(response);
            }

            // 完成回调（无论成功失败）
            request.OnComplete?.Invoke(response);

            _activeRequests--;
            webRequest.Dispose();
        }

        // ========== 内部方法 ==========

        /// <summary>根据请求配置创建UnityWebRequest</summary>
        private UnityWebRequest CreateWebRequest(HttpRequest request)
        {
            UnityWebRequest webRequest;

            switch (request.Method)
            {
                case HttpMethod.GET:
                    webRequest = UnityWebRequest.Get(request.Url);
                    break;

                case HttpMethod.POST:
                    webRequest = new UnityWebRequest(request.Url, "POST");
                    if (!string.IsNullOrEmpty(request.Body))
                    {
                        byte[] bodyRaw = Encoding.UTF8.GetBytes(request.Body);
                        webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    }
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    break;

                case HttpMethod.PUT:
                    webRequest = new UnityWebRequest(request.Url, "PUT");
                    if (!string.IsNullOrEmpty(request.Body))
                    {
                        byte[] bodyRaw = Encoding.UTF8.GetBytes(request.Body);
                        webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    }
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    break;

                case HttpMethod.DELETE:
                    webRequest = UnityWebRequest.Delete(request.Url);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    break;

                default:
                    webRequest = UnityWebRequest.Get(request.Url);
                    break;
            }

            return webRequest;
        }

        /// <summary>拼接完整URL</summary>
        private string BuildUrl(string path)
        {
            if (string.IsNullOrEmpty(path)) return _baseUrl;

            // 如果path已经是完整URL，直接返回
            if (path.StartsWith("http://") || path.StartsWith("https://"))
            {
                return path;
            }

            // 拼接BaseURL
            if (string.IsNullOrEmpty(_baseUrl))
            {
                return path;
            }

            return _baseUrl + "/" + path.TrimStart('/');
        }
    }
}
