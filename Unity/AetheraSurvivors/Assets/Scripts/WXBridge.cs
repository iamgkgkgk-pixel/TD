
using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// 微信API桥接层 — 封装微信小游戏平台的JS API调用
/// 用于验证 wx.login / wx.getSystemInfo 等API是否可正常调用
/// 
/// 使用方式：
/// 1. 本脚本通过 DllImport 调用 .jslib 中定义的JS函数
/// 2. 在 Unity Editor 中运行时会使用模拟数据（不调用真实API）
/// 3. 导出到微信小游戏后会调用真实的 wx API
/// </summary>
public static class WXBridge
{
    // ========================================
    // JS 桥接函数声明（仅在 WebGL 平台生效）
    // ========================================

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WX_Login(string callbackObj, string callbackMethod);

    [DllImport("__Internal")]
    private static extern void WX_GetSystemInfo(string callbackObj, string callbackMethod);

    [DllImport("__Internal")]
    private static extern string WX_GetSystemInfoSync();
#endif

    // ========================================
    // 公开API
    // ========================================

    /// <summary>
    /// 调用 wx.login 获取登录凭证code
    /// </summary>
    /// <param name="callbackObj">接收回调的GameObject名称</param>
    /// <param name="callbackMethod">回调方法名</param>
    public static void Login(string callbackObj, string callbackMethod)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WX_Login(callbackObj, callbackMethod);
#else
        // 编辑器模式下模拟返回
        Debug.Log("[WXBridge] 编辑器模式 - 模拟 wx.login");
        var go = GameObject.Find(callbackObj);
        if (go != null)
        {
            go.SendMessage(callbackMethod, "{\"code\":\"MOCK_CODE_FOR_EDITOR\",\"errMsg\":\"login:ok\"}");
        }
#endif
    }

    /// <summary>
    /// 调用 wx.getSystemInfo 获取系统信息（异步）
    /// </summary>
    /// <param name="callbackObj">接收回调的GameObject名称</param>
    /// <param name="callbackMethod">回调方法名</param>
    public static void GetSystemInfo(string callbackObj, string callbackMethod)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WX_GetSystemInfo(callbackObj, callbackMethod);
#else
        // 编辑器模式下模拟返回
        Debug.Log("[WXBridge] 编辑器模式 - 模拟 wx.getSystemInfo");
        var go = GameObject.Find(callbackObj);
        if (go != null)
        {
            string mockData = "{" +
                "\"brand\":\"Editor\"," +
                "\"model\":\"Unity Editor\"," +
                "\"system\":\"" + SystemInfo.operatingSystem + "\"," +
                "\"platform\":\"devtools\"," +
                "\"SDKVersion\":\"mock\"," +
                "\"screenWidth\":" + Screen.width + "," +
                "\"screenHeight\":" + Screen.height + "," +
                "\"windowWidth\":" + Screen.width + "," +
                "\"windowHeight\":" + Screen.height + "," +
                "\"pixelRatio\":" + Screen.dpi / 160f + "," +
                "\"benchmarkLevel\":30" +
                "}";
            go.SendMessage(callbackMethod, mockData);
        }
#endif
    }

    /// <summary>
    /// 同步获取系统信息（返回JSON字符串）
    /// </summary>
    /// <returns>系统信息JSON字符串</returns>
    public static string GetSystemInfoSync()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return WX_GetSystemInfoSync();
#else
        Debug.Log("[WXBridge] 编辑器模式 - 模拟 wx.getSystemInfoSync");
        return "{\"brand\":\"Editor\",\"model\":\"Unity Editor\",\"platform\":\"devtools\"}";
#endif
    }
}
