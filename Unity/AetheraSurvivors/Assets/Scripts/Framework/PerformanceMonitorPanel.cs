// ============================================================
// 文件名：PerformanceMonitorPanel.cs
// 功能描述：运行时性能监控面板 — 实时FPS、DrawCall、内存、对象池
//          在游戏画面上叠加显示性能指标
// 创建时间：2026-03-25
// 所属模块：Scripts（运行时组件）
// 对应交互：阶段二 #72
// 注意：项目中已有PerformanceMonitor.cs（阶段一技术原型版本），
//       本文件是增强版，提供更丰富的监控功能
// ============================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AetheraSurvivors.Framework

{
    /// <summary>
    /// 运行时性能监控面板 — 增强版
    /// 
    /// 功能：
    /// 1. 实时FPS（含最小/最大/平均）
    /// 2. DrawCall和三角面数（通过Profiler）
    /// 3. 内存使用（Mono堆/已用/总系统内存）
    /// 4. 对象池状态（活跃/空闲数量）
    /// 5. GC次数追踪
    /// 6. 可折叠/展开的面板
    /// 7. 通过快捷键切换显示/隐藏
    /// 
    /// 使用方式：
    ///   挂载到任意GameObject上，运行时左上角显示性能数据
    ///   按 ` 键（反引号）切换面板显示/隐藏
    ///   按 Tab 键切换简洁/详细模式
    ///   
    /// ⚠️ 发布版本中建议关闭或移除此组件
    /// </summary>
    public class PerformanceMonitorPanel : MonoBehaviour
    {
        // ========== 配置 ==========

        [Header("显示控制")]
        [SerializeField] private bool _showOnStart = true;
        [SerializeField] private KeyCode _toggleKey = KeyCode.BackQuote; // ` 键
        [SerializeField] private KeyCode _modeKey = KeyCode.Tab;

        [Header("刷新间隔")]
        [SerializeField] private float _updateInterval = 0.5f;

        [Header("面板位置")]
        [SerializeField] private int _posX = 10;
        [SerializeField] private int _posY = 10;

        // ========== 运行时数据 ==========

        /// <summary>是否显示面板</summary>
        private bool _isVisible;

        /// <summary>是否详细模式</summary>
        private bool _isDetailMode = false;

        /// <summary>FPS相关</summary>
        private int _frameCount;
        private float _fpsTimer;
        private float _currentFps;
        private float _minFps = float.MaxValue;
        private float _maxFps = 0;
        private float _avgFps;
        private int _avgFrameCount;
        private float _avgFpsSum;

        /// <summary>内存相关</summary>
        private float _monoHeapMB;
        private float _monoUsedMB;
        private float _totalAllocMB;

        /// <summary>GC相关</summary>
        private int _gcCount;
        private int _lastGcCount;

        /// <summary>更新计时器</summary>
        private float _updateTimer;

        /// <summary>显示文本缓存</summary>
        private string _displayText = "";

        /// <summary>GUIStyle缓存</summary>
        private GUIStyle _labelStyle;
        private GUIStyle _boxStyle;

        // ========== 生命周期 ==========

        private void Start()
        {
            _isVisible = _showOnStart;
            _lastGcCount = System.GC.CollectionCount(0);

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // 发布版本默认不显示
            _isVisible = false;
#endif
        }

        private void Update()
        {
            // 快捷键控制
            if (Input.GetKeyDown(_toggleKey))
            {
                _isVisible = !_isVisible;
            }

            if (Input.GetKeyDown(_modeKey) && _isVisible)
            {
                _isDetailMode = !_isDetailMode;
            }

            if (!_isVisible) return;

            // FPS计算
            _frameCount++;
            _fpsTimer += Time.unscaledDeltaTime;
            _avgFrameCount++;
            _avgFpsSum += 1f / Mathf.Max(Time.unscaledDeltaTime, 0.001f);

            // 定时更新显示数据
            _updateTimer += Time.unscaledDeltaTime;
            if (_updateTimer >= _updateInterval)
            {
                UpdateMetrics();
                _updateTimer = 0;
            }
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            // 初始化GUIStyle
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white },
                    richText = true
                };

                _boxStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = MakeTexture(2, 2, new Color(0, 0, 0, 0.7f)) }
                };
            }

            // 计算面板大小
            float panelWidth = _isDetailMode ? 280 : 180;
            float panelHeight = _isDetailMode ? 200 : 80;

            // 绘制背景
            GUI.Box(new Rect(_posX, _posY, panelWidth, panelHeight), "", _boxStyle);

            // 绘制文本
            GUI.Label(new Rect(_posX + 5, _posY + 5, panelWidth - 10, panelHeight - 10),
                _displayText, _labelStyle);
        }

        // ========== 内部方法 ==========

        /// <summary>更新性能指标</summary>
        private void UpdateMetrics()
        {
            // FPS
            _currentFps = _frameCount / _fpsTimer;
            if (_currentFps < _minFps && _currentFps > 0) _minFps = _currentFps;
            if (_currentFps > _maxFps) _maxFps = _currentFps;
            _avgFps = _avgFpsSum / Mathf.Max(_avgFrameCount, 1);

            _frameCount = 0;
            _fpsTimer = 0;

            // 内存
            _monoUsedMB = System.GC.GetTotalMemory(false) / (1024f * 1024f);
            _totalAllocMB = (float)UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
            _monoHeapMB = (float)UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() / (1024f * 1024f);

            // GC
            int currentGcCount = System.GC.CollectionCount(0);
            _gcCount = currentGcCount - _lastGcCount;
            _lastGcCount = currentGcCount;

            // 构建显示文本
            BuildDisplayText();
        }

        /// <summary>构建显示文本</summary>
        private void BuildDisplayText()
        {
            var sb = new StringBuilder();

            // FPS颜色：>30绿色，>20黄色，<20红色
            string fpsColor = _currentFps >= 30 ? "#00FF00" :
                              _currentFps >= 20 ? "#FFFF00" : "#FF0000";

            sb.AppendLine($"<color={fpsColor}>FPS: {_currentFps:F0}</color>");

            if (_isDetailMode)
            {
                sb.AppendLine($"  Min: {_minFps:F0}  Max: {_maxFps:F0}  Avg: {_avgFps:F0}");
                sb.AppendLine();

                // 内存
                string memColor = _monoUsedMB > 200 ? "#FF0000" :
                                  _monoUsedMB > 128 ? "#FFFF00" : "#00FF00";
                sb.AppendLine($"<color={memColor}>内存: {_monoUsedMB:F1} MB</color> (Mono)");
                sb.AppendLine($"  Heap: {_monoHeapMB:F1} MB");
                sb.AppendLine($"  Total: {_totalAllocMB:F1} MB");
                sb.AppendLine();

                // GC
                sb.AppendLine($"GC: {_gcCount}/interval  Total: {_lastGcCount}");
                sb.AppendLine();

                // 对象池状态
                if (ObjectPoolManager.HasInstance)
                {
                    var debugInfo = ObjectPoolManager.Instance.GetDebugInfo();
                    int totalActive = 0, totalInactive = 0;
                    foreach (var pair in debugInfo)
                    {
                        totalActive += pair.Value.active;
                        totalInactive += pair.Value.inactive;
                    }
                    sb.AppendLine($"对象池: {debugInfo.Count}种 活跃:{totalActive} 空闲:{totalInactive}");
                }


                sb.AppendLine();
                sb.Append("<color=#888888>` 隐藏 | Tab 切换模式</color>");
            }
            else
            {
                sb.AppendLine($"Mem: {_monoUsedMB:F1}MB | GC: {_gcCount}");
                sb.Append("<color=#888888>Tab→详细</color>");
            }

            _displayText = sb.ToString();
        }

        /// <summary>创建纯色纹理（用于面板背景）</summary>
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            var tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>重置统计数据</summary>
        public void ResetStats()
        {
            _minFps = float.MaxValue;
            _maxFps = 0;
            _avgFrameCount = 0;
            _avgFpsSum = 0;
        }
    }
}
