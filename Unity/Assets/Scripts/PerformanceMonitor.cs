
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Profiling;

/// <summary>
/// 性能监控组件 — 实时显示FPS、内存占用、DrawCall等性能数据
/// 
/// 用于 #36 技术原型的性能基线采集
/// 在屏幕左上角持续显示性能指标
/// </summary>
public class PerformanceMonitor : MonoBehaviour
{
    [Header("UI引用")]
    [Tooltip("用于显示FPS等性能数据的Text组件")]
    public Text fpsText;

    [Header("配置")]
    [Tooltip("数据更新间隔（秒）")]
    public float updateInterval = 0.5f;

    // FPS计算相关
    private float fpsAccumulator = 0f;   // FPS累加器
    private int fpsFrameCount = 0;       // 帧计数
    private float fpsTimer = 0f;         // 计时器
    private float currentFPS = 0f;       // 当前FPS
    private float minFPS = float.MaxValue;   // 最低FPS
    private float maxFPS = 0f;           // 最高FPS
    private float avgFPS = 0f;           // 平均FPS
    private int totalFrames = 0;         // 总帧数
    private float totalFPS = 0f;         // FPS总和（用于计算平均）

    // 内存数据
    private long totalMemoryMB = 0;      // 总内存(MB)
    private long usedMemoryMB = 0;       // 已用内存(MB)
    private long monoUsedMB = 0;         // Mono堆已用(MB)
    private long monoHeapMB = 0;         // Mono堆大小(MB)

    // 性能数据记录（用于最终报告）
    private float testStartTime;
    private float testDuration = 0f;

    /// <summary>
    /// 获取性能摘要字符串（供主入口使用）
    /// </summary>
    public string GetPerformanceSummary()
    {
        return $"FPS: {currentFPS:F1} (最低:{minFPS:F1} / 最高:{maxFPS:F1} / 平均:{avgFPS:F1})\n" +
               $"内存: 总保留{totalMemoryMB}MB | Mono已用{monoUsedMB}MB/{monoHeapMB}MB\n" +
               $"运行时长: {testDuration:F1}秒 | 总帧数: {totalFrames}";
    }

    private void Start()
    {
        testStartTime = Time.realtimeSinceStartup;
        Debug.Log("[PerformanceMonitor] 性能监控已启动");

        // 设置目标帧率
        Application.targetFrameRate = 60;
        
        // 输出基础信息
        Debug.Log($"[PerformanceMonitor] 设备信息:");
        Debug.Log($"  GPU: {SystemInfo.graphicsDeviceName}");
        Debug.Log($"  GPU内存: {SystemInfo.graphicsMemorySize}MB");
        Debug.Log($"  系统内存: {SystemInfo.systemMemorySize}MB");
        Debug.Log($"  屏幕分辨率: {Screen.width}x{Screen.height} @ {Screen.dpi}dpi");
        Debug.Log($"  目标帧率: {Application.targetFrameRate}");
    }

    private void Update()
    {
        // 累加帧时间
        fpsAccumulator += Time.unscaledDeltaTime;
        fpsFrameCount++;
        totalFrames++;
        fpsTimer += Time.unscaledDeltaTime;

        // 按间隔更新显示
        if (fpsTimer >= updateInterval)
        {
            // 计算当前FPS
            currentFPS = fpsFrameCount / fpsAccumulator;
            
            // 更新最大最小值（忽略前10帧的启动波动）
            if (totalFrames > 10)
            {
                if (currentFPS < minFPS) minFPS = currentFPS;
                if (currentFPS > maxFPS) maxFPS = currentFPS;
            }
            
            // 计算平均FPS
            totalFPS += currentFPS;
            int sampleCount = Mathf.Max(1, totalFrames / Mathf.Max(1, Mathf.RoundToInt(updateInterval * 60)));
            avgFPS = totalFPS / Mathf.Max(1, sampleCount);

            // 采集内存数据
            totalMemoryMB = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
            usedMemoryMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            monoUsedMB = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);
            monoHeapMB = Profiler.GetMonoHeapSizeLong() / (1024 * 1024);

            // 运行时长
            testDuration = Time.realtimeSinceStartup - testStartTime;

            // 更新UI显示
            UpdateDisplay();

            // 重置计数器
            fpsAccumulator = 0f;
            fpsFrameCount = 0;
            fpsTimer = 0f;
        }
    }

    /// <summary>
    /// 更新屏幕显示
    /// </summary>
    private void UpdateDisplay()
    {
        if (fpsText == null) return;

        // 根据FPS设置颜色
        string fpsColor;
        if (currentFPS >= 55)
            fpsColor = "#00FF00"; // 绿色 - 优秀
        else if (currentFPS >= 30)
            fpsColor = "#FFFF00"; // 黄色 - 可接受
        else
            fpsColor = "#FF0000"; // 红色 - 需优化

        fpsText.text = 
            $"<color={fpsColor}>FPS: {currentFPS:F1}</color>" +
            $" (低:{minFPS:F1} / 高:{maxFPS:F1} / 均:{avgFPS:F1})\n" +
            $"内存: {usedMemoryMB}MB / {totalMemoryMB}MB | Mono: {monoUsedMB}MB\n" +
            $"帧数: {totalFrames} | 时长: {testDuration:F0}秒";
    }

    /// <summary>
    /// 输出性能报告到控制台（可在测试结束时手动调用）
    /// </summary>
    public void PrintPerformanceReport()
    {
        testDuration = Time.realtimeSinceStartup - testStartTime;
        
        Debug.Log("========================================");
        Debug.Log("  AetheraSurvivors 性能基线报告");
        Debug.Log("========================================");
        Debug.Log($"  测试时长: {testDuration:F1}秒");
        Debug.Log($"  总帧数: {totalFrames}");
        Debug.Log($"  平均FPS: {avgFPS:F1}");
        Debug.Log($"  最低FPS: {minFPS:F1}");
        Debug.Log($"  最高FPS: {maxFPS:F1}");
        Debug.Log($"  当前FPS: {currentFPS:F1}");
        Debug.Log("  ---");
        Debug.Log($"  总保留内存: {totalMemoryMB}MB");
        Debug.Log($"  已分配内存: {usedMemoryMB}MB");
        Debug.Log($"  Mono堆: {monoUsedMB}MB / {monoHeapMB}MB");
        Debug.Log("  ---");
        Debug.Log($"  GPU: {SystemInfo.graphicsDeviceName}");
        Debug.Log($"  分辨率: {Screen.width}x{Screen.height}");
        Debug.Log("========================================");
        
        // 判定结果
        if (avgFPS >= 55 && totalMemoryMB < 80)
        {
            Debug.Log("  ✅ 结论: 性能基线优秀，可以继续开发");
        }
        else if (avgFPS >= 30 && totalMemoryMB < 150)
        {
            Debug.Log("  ⚠️ 结论: 性能基线可接受，但需要注意优化");
        }
        else
        {
            Debug.Log("  ❌ 结论: 性能基线不达标，需要排查原因");
        }
        Debug.Log("========================================");
    }
}
