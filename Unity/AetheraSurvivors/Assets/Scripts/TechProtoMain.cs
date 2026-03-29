
using UnityEngine;
using UnityEngine.UI;
using AetheraSurvivors.Framework;
using AetheraSurvivors.MetaGame;


/// <summary>
/// 技术原型主入口 — 串联所有验证逻辑
/// 
/// 功能：
/// 1. 启动时自动调用 wx.getSystemInfo 和 wx.login
/// 2. 在屏幕上显示API返回结果
/// 3. 显示触控状态和性能数据
/// 4. 提供"输出报告"按钮，打印完整性能基线数据
/// 
/// 使用：
/// - 挂载到场景中的空GameObject（命名为 GameManager）
/// - 将UI Text组件拖拽赋值给 infoText
/// </summary>
public class TechProtoMain : MonoBehaviour
{
    [Header("UI引用")]
    [Tooltip("用于显示综合信息的Text组件")]
    public Text infoText;

    [Header("组件引用")]
    [Tooltip("性能监控组件（可自动查找）")]
    public PerformanceMonitor perfMonitor;

    [Tooltip("触控测试组件（可自动查找）")]
    public TouchTest touchTest;

    // 验证状态
    private bool loginVerified = false;       // wx.login 是否验证通过
    private bool systemInfoVerified = false;  // wx.getSystemInfo 是否验证通过
    private string loginResult = "等待中..."; // 登录结果
    private string systemInfoResult = "等待中..."; // 系统信息结果

    // 系统信息详细数据
    private string deviceBrand = "";
    private string deviceModel = "";
    private string deviceSystem = "";
    private string devicePlatform = "";
    private string sdkVersion = "";
    private int screenWidth = 0;
    private int screenHeight = 0;

    [Header("启动模式")]
    [Tooltip("true=启动战斗模式, false=启动大厅模式")]
    public bool startBattleMode = false;

    [Tooltip("true=启动技术验证模式（仅当startBattleMode=false时生效）")]
    public bool startTechVerifyMode = false;


    private void Start()
    {
        Debug.Log("========================================");
        Debug.Log("  AetheraSurvivors 技术原型 v0.1");
        Debug.Log("  验证项: 导出流程 / Touch / wx API / 性能");
        Debug.Log("========================================");

        // 自动查找组件
        if (perfMonitor == null) perfMonitor = GetComponent<PerformanceMonitor>();
        if (touchTest == null) touchTest = GetComponent<TouchTest>();

        // 启动模式分支
        if (startBattleMode)
        {
            StartBattleMode();
            return;
        }

        if (startTechVerifyMode)
        {
            // 技术验证模式：调用微信API
            Invoke("CallWXAPIs", 0.5f);
            return;
        }

        // 默认：大厅模式
        StartLobbyMode();

    }

    /// <summary>
    /// 启动大厅模式 — 初始化GameManager → MetaGameInitializer → 打开MainMenuUI
    /// </summary>
    private void StartLobbyMode()
    {
        Debug.Log("[TechProtoMain] 切换到大厅模式！");

        // 隐藏技术验证Canvas（场景中的Canvas）
        var canvas = FindObjectOfType<Canvas>();
        if (canvas != null && canvas.gameObject != null)
        {
            canvas.gameObject.SetActive(false);
        }

        // 禁用自身的技术验证相关组件
        if (perfMonitor != null) perfMonitor.enabled = false;
        if (touchTest != null) touchTest.enabled = false;
        this.enabled = false; // 停止TechProtoMain的Update

        // 1. 初始化GameManager（会自动初始化所有框架层Manager）
        Debug.Log("[TechProtoMain] 初始化 GameManager...");
        GameManager.Preload();

        // 2. 初始化PlayerDataManager（数据层）
        Debug.Log("[TechProtoMain] 初始化 PlayerDataManager...");
        AetheraSurvivors.Data.PlayerDataManager.Instance.Initialize();

        // 3. 初始化MetaGameInitializer（元游戏系统编排器）
        Debug.Log("[TechProtoMain] 初始化 MetaGameInitializer...");
        MetaGameInitializer.Preload();

        // 4. 打开主界面
        Debug.Log("[TechProtoMain] 打开主界面 MainMenuUI...");
        UIManager.Instance.Open<MainMenuUI>();

        // 5. 尝试弹出签到
        MetaGameInitializer.Instance.TryShowCheckIn();

        Debug.Log("[TechProtoMain] ✅ 大厅模式启动完成！");
    }

    /// <summary>
    /// 启动战斗模式 — 隐藏技术验证UI，初始化数据层后创建BattleSceneSetup启动战斗
    /// </summary>
    private void StartBattleMode()
    {
        Debug.Log("[TechProtoMain] 切换到战斗模式！");

        // 隐藏技术验证Canvas（场景中的Canvas）
        var canvas = FindObjectOfType<Canvas>();
        if (canvas != null && canvas.gameObject != null)
        {
            canvas.gameObject.SetActive(false);
        }

        // 禁用自身的技术验证相关组件
        if (perfMonitor != null) perfMonitor.enabled = false;
        if (touchTest != null) touchTest.enabled = false;
        this.enabled = false; // 停止TechProtoMain的Update

        // 确保数据层初始化（体力检查依赖PlayerDataManager）
        GameManager.Preload();
        AetheraSurvivors.Data.PlayerDataManager.Instance.Initialize();

        // 体力检查（测试模式：跳过体力消耗）
        if (AetheraSurvivors.Data.PlayerDataManager.HasInstance)
        {
            var data = AetheraSurvivors.Data.PlayerDataManager.Instance.Data;
            // 测试模式：确保体力充足
            if (data.Stamina < 100) data.Stamina = 99999;
            Debug.Log($"[TechProtoMain] 测试模式：体力={data.Stamina}（无限）");
        }

        // 创建战斗场景入口
        var battleSetup = new GameObject("[BattleSceneSetup]");
        battleSetup.AddComponent<AetheraSurvivors.Battle.BattleSceneSetup>();
    }


    /// <summary>
    /// 调用微信API进行验证
    /// </summary>
    private void CallWXAPIs()
    {
        Debug.Log("[TechProtoMain] 开始调用微信API...");

        // 验证1: wx.getSystemInfo
        try
        {
            WXBridge.GetSystemInfo(gameObject.name, "OnGetSystemInfoCallback");
            Debug.Log("[TechProtoMain] wx.getSystemInfo 调用已发起");
        }
        catch (System.Exception e)
        {
            systemInfoResult = $"调用失败: {e.Message}";
            Debug.LogError($"[TechProtoMain] wx.getSystemInfo 调用失败: {e}");
        }

        // 验证2: wx.login
        try
        {
            WXBridge.Login(gameObject.name, "OnLoginCallback");
            Debug.Log("[TechProtoMain] wx.login 调用已发起");
        }
        catch (System.Exception e)
        {
            loginResult = $"调用失败: {e.Message}";
            Debug.LogError($"[TechProtoMain] wx.login 调用失败: {e}");
        }
    }

    /// <summary>
    /// wx.getSystemInfo 回调
    /// </summary>
    /// <param name="jsonData">微信返回的系统信息JSON</param>
    public void OnGetSystemInfoCallback(string jsonData)
    {
        Debug.Log($"[TechProtoMain] wx.getSystemInfo 返回: {jsonData}");

        try
        {
            // 简单解析JSON（不依赖第三方库）
            deviceBrand = ExtractJsonString(jsonData, "brand");
            deviceModel = ExtractJsonString(jsonData, "model");
            deviceSystem = ExtractJsonString(jsonData, "system");
            devicePlatform = ExtractJsonString(jsonData, "platform");
            sdkVersion = ExtractJsonString(jsonData, "SDKVersion");
            
            string widthStr = ExtractJsonString(jsonData, "screenWidth");
            string heightStr = ExtractJsonString(jsonData, "screenHeight");
            int.TryParse(widthStr, out screenWidth);
            int.TryParse(heightStr, out screenHeight);

            systemInfoVerified = true;
systemInfoResult = "[OK] 成功";


            Debug.Log($"[TechProtoMain] 系统信息解析成功:");
            Debug.Log($"  品牌: {deviceBrand}");
            Debug.Log($"  型号: {deviceModel}");
            Debug.Log($"  系统: {deviceSystem}");
            Debug.Log($"  平台: {devicePlatform}");
            Debug.Log($"  SDK: {sdkVersion}");
            Debug.Log($"  分辨率: {screenWidth}x{screenHeight}");
        }
        catch (System.Exception e)
        {
            systemInfoResult = $"解析失败: {e.Message}";
            Debug.LogError($"[TechProtoMain] 系统信息解析失败: {e}");
        }
    }

    /// <summary>
    /// wx.login 回调
    /// </summary>
    /// <param name="jsonData">微信返回的登录信息JSON</param>
    public void OnLoginCallback(string jsonData)
    {
        Debug.Log($"[TechProtoMain] wx.login 返回: {jsonData}");

        try
        {
            string code = ExtractJsonString(jsonData, "code");
            string errMsg = ExtractJsonString(jsonData, "errMsg");

            if (!string.IsNullOrEmpty(code))
            {
                loginVerified = true;
loginResult = $"[OK] 成功 (code: {code.Substring(0, Mathf.Min(10, code.Length))}...)";

                Debug.Log($"[TechProtoMain] 登录成功! code={code}");
            }
            else
            {
                loginResult = $"❌ 失败 ({errMsg})";
                Debug.LogWarning($"[TechProtoMain] 登录失败: {errMsg}");
            }
        }
        catch (System.Exception e)
        {
            loginResult = $"解析失败: {e.Message}";
            Debug.LogError($"[TechProtoMain] 登录回调解析失败: {e}");
        }
    }

    private void Update()
    {
        UpdateInfoDisplay();
    }

    /// <summary>
    /// 更新屏幕信息显示
    /// </summary>
    private void UpdateInfoDisplay()
    {
        if (infoText == null) return;

        string info = "<b>═══ AetheraSurvivors 技术原型 ═══</b>\n\n";

        // 验证①: 导出状态（如果能看到这个界面，说明导出成功）
info += "<color=#00FF00>[OK] 验证①: 导出流程成功（画面可见）</color>\n\n";


        // 验证②: Touch状态
        if (touchTest != null)
        {
            info += "<b>验证②: Touch输入</b>\n";
            info += touchTest.GetTouchSummary() + "\n";
            info += "<color=#AAAAAA>（请点击/拖拽屏幕进行测试）</color>\n\n";
        }

        // 验证③: wx API状态
        info += "<b>验证③: 微信API</b>\n";
        info += $"  wx.login: {loginResult}\n";
        info += $"  wx.getSystemInfo: {systemInfoResult}\n";
        if (systemInfoVerified)
        {
            info += $"  设备: {deviceBrand} {deviceModel}\n";
            info += $"  系统: {deviceSystem} | 平台: {devicePlatform}\n";
            info += $"  SDK: {sdkVersion} | 屏幕: {screenWidth}x{screenHeight}\n";
        }
        info += "\n";

        // 验证④: 性能数据
        if (perfMonitor != null)
        {
            info += "<b>验证④: 性能基线</b>\n";
            info += perfMonitor.GetPerformanceSummary() + "\n\n";
        }

        // 总结状态
        int passCount = 1; // 导出成功默认通过
        if (loginVerified) passCount++;
        if (systemInfoVerified) passCount++;
        // Touch 需要用户手动验证
        info += $"<b>验证进度: {passCount}/4 项自动通过</b>\n";
        info += "<color=#AAAAAA>Touch需手动验证 | 长按5秒输出性能报告</color>";

        infoText.text = info;
    }

    // ========================================
    // 长按5秒输出报告
    // ========================================
    
    private float longPressTimer = 0f;
    private bool reportPrinted = false;

    private void LateUpdate()
    {
        // 检测长按（任意输入持续5秒）
        if (Input.GetMouseButton(0) || Input.touchCount > 0)
        {
            longPressTimer += Time.deltaTime;
            if (longPressTimer >= 5f && !reportPrinted)
            {
                reportPrinted = true;
                if (perfMonitor != null)
                {
                    perfMonitor.PrintPerformanceReport();
                }
                Debug.Log("[TechProtoMain] 性能报告已输出到控制台");
            }
        }
        else
        {
            longPressTimer = 0f;
            reportPrinted = false;
        }
    }

    // ========================================
    // JSON简易解析工具（避免依赖第三方库）
    // ========================================

    /// <summary>
    /// 从JSON字符串中提取指定key的值（简易实现，仅支持简单键值对）
    /// </summary>
    private string ExtractJsonString(string json, string key)
    {
        // 查找 "key": 或 "key":
        string searchKey = "\"" + key + "\"";
        int keyIndex = json.IndexOf(searchKey);
        if (keyIndex < 0) return "";

        // 跳到冒号后面
        int colonIndex = json.IndexOf(':', keyIndex + searchKey.Length);
        if (colonIndex < 0) return "";

        // 跳过空白
        int valueStart = colonIndex + 1;
        while (valueStart < json.Length && json[valueStart] == ' ') valueStart++;

        if (valueStart >= json.Length) return "";

        // 判断值类型
        if (json[valueStart] == '"')
        {
            // 字符串值
            int strStart = valueStart + 1;
            int strEnd = json.IndexOf('"', strStart);
            if (strEnd < 0) return "";
            return json.Substring(strStart, strEnd - strStart);
        }
        else
        {
            // 数值或布尔值
            int valEnd = valueStart;
            while (valEnd < json.Length && json[valEnd] != ',' && json[valEnd] != '}' && json[valEnd] != ' ')
            {
                valEnd++;
            }
            return json.Substring(valueStart, valEnd - valueStart);
        }
    }
}
