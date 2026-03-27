
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 技术原型一键配置工具（Editor Only）
/// 
/// 功能：
/// 1. 自动创建/配置测试场景（Canvas + Text + Image + GameManager + 挂脚本）
/// 2. 自动配置 Player Settings（分辨率/Color Space/Stripping等）
/// 3. 自动切换 Build Target 到 WebGL
/// 
/// 使用方式：
/// Unity 菜单栏 → AetheraSurvivors → 一键配置技术原型
/// </summary>
public class TechProtoSetup : EditorWindow
{
    // ========================================
    // 菜单入口
    // ========================================

    [MenuItem("AetheraSurvivors/一键配置技术原型 (步骤四+五)", false, 1)]
    public static void SetupAll()
    {
        if (EditorUtility.DisplayDialog(
            "AetheraSurvivors 技术原型配置",
            "即将执行以下操作：\n\n" +
            "✅ 步骤四：创建测试场景（Canvas/Text/GameManager/挂脚本）\n" +
            "✅ 步骤五：配置 Player Settings + 切换到 WebGL\n\n" +
            "⚠️ 请先确保已导入 minigame-unity-sdk（步骤三）\n" +
            "   如果还没导入SDK也没关系，可以先配置场景，之后再导入SDK\n\n" +
            "是否继续？",
            "开始配置", "取消"))
        {
            SetupScene();
            SetupPlayerSettings();
            SwitchToWebGL();

            EditorUtility.DisplayDialog(
                "✅ 配置完成！",
                "技术原型已配置完成：\n\n" +
                "✅ 场景已创建并配置\n" +
                "✅ Player Settings 已设置\n" +
                "✅ Build Target 已切换到 WebGL\n\n" +
                "下一步：\n" +
                "1. 如果还没导入SDK → 导入 minigame-unity-sdk\n" +
                "2. 点击 Play 在 Editor 中先测试\n" +
                "3. 通过 SDK 菜单导出微信小游戏\n" +
                "4. 在微信开发者工具中打开验证",
                "知道了");
        }
    }

    [MenuItem("AetheraSurvivors/仅配置场景 (步骤四)", false, 2)]
    public static void SetupSceneOnly()
    {
        SetupScene();
        EditorUtility.DisplayDialog("✅ 场景配置完成",
            "测试场景已创建：\n" +
            "• GameManager（挂载了3个测试脚本）\n" +
            "• Canvas + InfoText + TouchIndicator\n" +
            "• EventSystem\n\n" +
            "点击 Play 可在 Editor 中测试基本功能",
            "知道了");
    }

    [MenuItem("AetheraSurvivors/仅配置 Build Settings (步骤五)", false, 3)]
    public static void SetupBuildOnly()
    {
        SetupPlayerSettings();
        SwitchToWebGL();
        EditorUtility.DisplayDialog("✅ Build Settings 配置完成",
            "已设置：\n" +
            "• 分辨率 750×1334（竖屏）\n" +
            "• Color Space: Gamma\n" +
            "• Managed Stripping Level: High\n" +
            "• Build Target: WebGL\n" +
            "• 目标帧率: 60fps",
            "知道了");
    }

    // ========================================
    // 步骤四：配置测试场景
    // ========================================

    private static void SetupScene()
    {
        Debug.Log("[TechProtoSetup] ========== 开始配置测试场景 ==========");

        // 1. 打开或创建场景
        var scene = EditorSceneManager.GetActiveScene();
        Debug.Log($"[TechProtoSetup] 当前场景: {scene.name} ({scene.path})");

        // 2. 检查并清理已有的测试对象（避免重复创建）
        CleanupExistingObjects();

        // 3. 创建 EventSystem（如果不存在）
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
            Debug.Log("[TechProtoSetup] ✅ 创建 EventSystem");
        }
        else
        {
            Debug.Log("[TechProtoSetup] ✅ EventSystem 已存在，跳过");
        }

        // 4. 创建 Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // 确保在最上层

        var canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(750, 1334);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        Debug.Log("[TechProtoSetup] ✅ 创建 Canvas (750×1334, Screen Space Overlay)");

        // 5. 创建 InfoText（综合信息显示）
        var infoTextGO = new GameObject("InfoText");
        infoTextGO.transform.SetParent(canvasGO.transform, false);
        
        var infoText = infoTextGO.AddComponent<Text>();
        infoText.text = "AetheraSurvivors 技术原型 加载中...";
        infoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (infoText.font == null)
        {
            infoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        infoText.fontSize = 20;
        infoText.color = Color.white;
        infoText.alignment = TextAnchor.UpperLeft;
        infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoText.verticalOverflow = VerticalWrapMode.Overflow;
        infoText.supportRichText = true;

        var infoRect = infoTextGO.GetComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0, 0);
        infoRect.anchorMax = new Vector2(1, 1);
        infoRect.offsetMin = new Vector2(20, 200);  // 左下边距
        infoRect.offsetMax = new Vector2(-20, -20);  // 右上边距
        Debug.Log("[TechProtoSetup] ✅ 创建 InfoText");

        // 6. 创建 FpsText（性能数据，放在左上角）
        var fpsTextGO = new GameObject("FpsText");
        fpsTextGO.transform.SetParent(canvasGO.transform, false);
        
        var fpsText = fpsTextGO.AddComponent<Text>();
        fpsText.text = "FPS: --";
        fpsText.font = infoText.font;
        fpsText.fontSize = 18;
        fpsText.color = Color.green;
        fpsText.alignment = TextAnchor.UpperLeft;
        fpsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        fpsText.verticalOverflow = VerticalWrapMode.Overflow;
        fpsText.supportRichText = true;

        var fpsRect = fpsTextGO.GetComponent<RectTransform>();
        fpsRect.anchorMin = new Vector2(0, 1);
        fpsRect.anchorMax = new Vector2(1, 1);
        fpsRect.pivot = new Vector2(0.5f, 1);
        fpsRect.offsetMin = new Vector2(10, -100);
        fpsRect.offsetMax = new Vector2(-10, -5);
        Debug.Log("[TechProtoSetup] ✅ 创建 FpsText");

        // 7. 创建背景面板（半透明黑色，让文字更易读）
        var bgPanelGO = new GameObject("BackgroundPanel");
        bgPanelGO.transform.SetParent(canvasGO.transform, false);
        bgPanelGO.transform.SetAsFirstSibling(); // 放到最底层

        var bgImage = bgPanelGO.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.7f);

        var bgRect = bgPanelGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Debug.Log("[TechProtoSetup] ✅ 创建 BackgroundPanel");

        // 8. 创建 TouchIndicator（触控指示器）
        var touchIndicatorGO = new GameObject("TouchIndicator");
        touchIndicatorGO.transform.SetParent(canvasGO.transform, false);

        var touchImage = touchIndicatorGO.AddComponent<Image>();
        touchImage.color = new Color(1, 0, 0, 0.5f); // 半透明红色

        var touchRect = touchIndicatorGO.GetComponent<RectTransform>();
        touchRect.sizeDelta = new Vector2(80, 80);
        touchRect.anchoredPosition = Vector2.zero;

        touchIndicatorGO.SetActive(false); // 默认隐藏
        Debug.Log("[TechProtoSetup] ✅ 创建 TouchIndicator (80×80 半透明红)");

        // 9. 创建 GameManager 并挂载脚本
        var gameManagerGO = new GameObject("GameManager");

        // 挂载 TechProtoMain
        var techProto = gameManagerGO.AddComponent<TechProtoMain>();
        techProto.infoText = infoText;
        Debug.Log("[TechProtoSetup] ✅ 挂载 TechProtoMain + 绑定 infoText");

        // 挂载 PerformanceMonitor
        var perfMonitor = gameManagerGO.AddComponent<PerformanceMonitor>();
        perfMonitor.fpsText = fpsText;
        Debug.Log("[TechProtoSetup] ✅ 挂载 PerformanceMonitor + 绑定 fpsText");

        // 挂载 TouchTest
        var touchTest = gameManagerGO.AddComponent<TouchTest>();
        touchTest.touchIndicator = touchRect;
        Debug.Log("[TechProtoSetup] ✅ 挂载 TouchTest + 绑定 touchIndicator");

        // 绑定 TechProtoMain 的组件引用
        techProto.perfMonitor = perfMonitor;
        techProto.touchTest = touchTest;
        Debug.Log("[TechProtoSetup] ✅ TechProtoMain 组件引用已绑定");

        // 10. 设置摄像机背景色
        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f); // 深蓝灰色
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            Debug.Log("[TechProtoSetup] ✅ 摄像机背景色已设置");
        }

        // 11. 标记场景为脏（需要保存）
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[TechProtoSetup] ========== 场景配置完成！ ==========");
        Debug.Log("[TechProtoSetup] 场景层级结构:");
        Debug.Log("[TechProtoSetup]   ├── Main Camera");
        Debug.Log("[TechProtoSetup]   ├── EventSystem");
        Debug.Log("[TechProtoSetup]   ├── Canvas");
        Debug.Log("[TechProtoSetup]   │   ├── BackgroundPanel (半透明黑)");
        Debug.Log("[TechProtoSetup]   │   ├── InfoText (综合信息)");
        Debug.Log("[TechProtoSetup]   │   ├── FpsText (性能数据)");
        Debug.Log("[TechProtoSetup]   │   └── TouchIndicator (触控指示)");
        Debug.Log("[TechProtoSetup]   └── GameManager");
        Debug.Log("[TechProtoSetup]       ├── TechProtoMain");
        Debug.Log("[TechProtoSetup]       ├── PerformanceMonitor");
        Debug.Log("[TechProtoSetup]       └── TouchTest");
    }

    /// <summary>
    /// 清理已有的测试对象（避免重复创建）
    /// </summary>
    private static void CleanupExistingObjects()
    {
        string[] objectNames = { "GameManager", "Canvas", "TouchIndicator" };
        foreach (var name in objectNames)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                // 只清理我们创建的对象（检查是否有我们的组件）
                if (name == "GameManager" && existing.GetComponent<TechProtoMain>() != null)
                {
                    Object.DestroyImmediate(existing);
                    Debug.Log($"[TechProtoSetup] 清理已有的 {name}");
                }
                else if (name == "Canvas" && existing.transform.Find("InfoText") != null)
                {
                    Object.DestroyImmediate(existing);
                    Debug.Log($"[TechProtoSetup] 清理已有的 {name}");
                }
            }
        }
    }

    // ========================================
    // 步骤五：配置 Player Settings
    // ========================================

    private static void SetupPlayerSettings()
    {
        Debug.Log("[TechProtoSetup] ========== 开始配置 Player Settings ==========");

        // 基本信息
        PlayerSettings.productName = "AetheraSurvivors";
        PlayerSettings.companyName = "AetheraSurvivorsStudio";
        Debug.Log("[TechProtoSetup] ✅ Product Name: AetheraSurvivors");

        // 分辨率设置（竖屏）
        PlayerSettings.defaultScreenWidth = 750;
        PlayerSettings.defaultScreenHeight = 1334;
        PlayerSettings.defaultIsNativeResolution = false;
        Debug.Log("[TechProtoSetup] ✅ 默认分辨率: 750×1334 (竖屏)");

        // Color Space（WebGL推荐Gamma）
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        Debug.Log("[TechProtoSetup] ✅ Color Space: Gamma");

        // API Compatibility Level
        PlayerSettings.SetApiCompatibilityLevel(
            BuildTargetGroup.WebGL,
            ApiCompatibilityLevel.NET_Standard_2_0);
        Debug.Log("[TechProtoSetup] ✅ API Compatibility: .NET Standard 2.0");

        // Managed Stripping Level（高度裁剪，减小包体）
        PlayerSettings.SetManagedStrippingLevel(
            BuildTargetGroup.WebGL,
            ManagedStrippingLevel.High);
        Debug.Log("[TechProtoSetup] ✅ Managed Stripping Level: High");

        // WebGL 特定设置
#if UNITY_2021_2_OR_NEWER
        // 压缩格式（Brotli最优）
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        Debug.Log("[TechProtoSetup] ✅ WebGL Compression: Brotli");

        // 数据缓存
        PlayerSettings.WebGL.dataCaching = true;
        Debug.Log("[TechProtoSetup] ✅ WebGL Data Caching: Enabled");

        // 去掉异常支持（减小包体）
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        Debug.Log("[TechProtoSetup] ✅ WebGL Exception Support: None (减小包体)");
#endif

        // 目标帧率
        Application.targetFrameRate = 60;
        Debug.Log("[TechProtoSetup] ✅ Target Frame Rate: 60");

        Debug.Log("[TechProtoSetup] ========== Player Settings 配置完成 ==========");
    }

    // ========================================
    // 切换到 WebGL 平台
    // ========================================

    private static void SwitchToWebGL()
    {
        Debug.Log("[TechProtoSetup] ========== 切换 Build Target ==========");

        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
        {
            Debug.Log("[TechProtoSetup] ✅ 当前已经是 WebGL 平台，无需切换");
            return;
        }

        Debug.Log("[TechProtoSetup] 当前平台: " + EditorUserBuildSettings.activeBuildTarget);
        Debug.Log("[TechProtoSetup] 正在切换到 WebGL... (首次切换可能需要几分钟)");

        bool success = EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.WebGL, BuildTarget.WebGL);

        if (success)
        {
            Debug.Log("[TechProtoSetup] ✅ 已成功切换到 WebGL 平台！");
        }
        else
        {
            Debug.LogError("[TechProtoSetup] ❌ 切换 WebGL 失败！请检查是否已安装 WebGL Build Support 模块");
            Debug.LogError("[TechProtoSetup] 解决方法: Unity Hub → Installs → 对应版本 → Add Modules → 勾选 WebGL Build Support");
            
            EditorUtility.DisplayDialog(
                "❌ WebGL 切换失败",
                "请确认已安装 WebGL Build Support 模块：\n\n" +
                "Unity Hub → Installs → 你的Unity版本 → Add Modules → 勾选 WebGL Build Support\n\n" +
                "安装后重启 Unity 再试。\n\n" +
                "场景和 Player Settings 已配置成功，只需切换平台即可。",
                "知道了");
        }
    }

    // ========================================
    // 辅助工具菜单
    // ========================================

    [MenuItem("AetheraSurvivors/检查配置状态", false, 20)]
    public static void CheckStatus()
    {
        string status = "═══ AetheraSurvivors 技术原型配置状态 ═══\n\n";

        // 检查 Build Target
        bool isWebGL = EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL;
        status += $"Build Target: {EditorUserBuildSettings.activeBuildTarget} {(isWebGL ? "✅" : "❌ 需要切换到WebGL")}\n";

        // 检查 Color Space
        bool isGamma = PlayerSettings.colorSpace == ColorSpace.Gamma;
        status += $"Color Space: {PlayerSettings.colorSpace} {(isGamma ? "✅" : "⚠️ WebGL推荐Gamma")}\n";

        // 检查场景中的对象
        var gameManager = GameObject.Find("GameManager");
        bool hasGameManager = gameManager != null && gameManager.GetComponent<TechProtoMain>() != null;
        status += $"GameManager: {(hasGameManager ? "✅ 已创建并挂载脚本" : "❌ 未创建")}\n";

        var canvas = GameObject.Find("Canvas");
        bool hasCanvas = canvas != null;
        status += $"Canvas: {(hasCanvas ? "✅ 已创建" : "❌ 未创建")}\n";

        // 检查 EventSystem
        bool hasEventSystem = Object.FindObjectOfType<EventSystem>() != null;
        status += $"EventSystem: {(hasEventSystem ? "✅ 存在" : "❌ 缺失")}\n";

        // 检查脚本文件
        bool hasWXBridge = System.IO.File.Exists(
            Application.dataPath + "/Scripts/WXBridge.cs");
        bool hasTouchTest = System.IO.File.Exists(
            Application.dataPath + "/Scripts/TouchTest.cs");
        bool hasPerfMon = System.IO.File.Exists(
            Application.dataPath + "/Scripts/PerformanceMonitor.cs");
        bool hasTechProto = System.IO.File.Exists(
            Application.dataPath + "/Scripts/TechProtoMain.cs");
        bool hasJslib = System.IO.File.Exists(
            Application.dataPath + "/Plugins/WebGL/WXBridge.jslib");

        status += $"\n脚本文件:\n";
        status += $"  WXBridge.cs: {(hasWXBridge ? "✅" : "❌")}\n";
        status += $"  TouchTest.cs: {(hasTouchTest ? "✅" : "❌")}\n";
        status += $"  PerformanceMonitor.cs: {(hasPerfMon ? "✅" : "❌")}\n";
        status += $"  TechProtoMain.cs: {(hasTechProto ? "✅" : "❌")}\n";
        status += $"  WXBridge.jslib: {(hasJslib ? "✅" : "❌")}\n";

        // 检查 minigame-unity-sdk
        bool hasSDK = System.IO.Directory.Exists(
            Application.dataPath + "/WX-WASM-SDK") ||
            System.IO.Directory.Exists(
            Application.dataPath + "/WX-WASM-SDK-V2");
        status += $"\n微信SDK: {(hasSDK ? "✅ 已检测到" : "⚠️ 未检测到（需要手动导入）")}\n";

        // 总结
        int total = 0;
        int passed = 0;
        total++; if (isWebGL) passed++;
        total++; if (hasGameManager) passed++;
        total++; if (hasCanvas) passed++;
        total++; if (hasEventSystem) passed++;
        total++; if (hasWXBridge && hasTouchTest && hasPerfMon && hasTechProto) passed++;
        total++; if (hasJslib) passed++;
        total++; if (hasSDK) passed++;

        status += $"\n═══ 总计: {passed}/{total} 项通过 ═══";

        Debug.Log(status);
        EditorUtility.DisplayDialog("配置状态检查", status, "知道了");
    }

    [MenuItem("AetheraSurvivors/输出性能报告", false, 21)]
    public static void PrintReport()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("提示", "请先点击 Play 运行游戏，再使用此功能。", "知道了");
            return;
        }

        var perfMon = Object.FindObjectOfType<PerformanceMonitor>();
        if (perfMon != null)
        {
            perfMon.PrintPerformanceReport();
            EditorUtility.DisplayDialog("提示", "性能报告已输出到 Console 窗口。", "知道了");
        }
        else
        {
            EditorUtility.DisplayDialog("错误", "未找到 PerformanceMonitor 组件。", "知道了");
        }
    }
}
