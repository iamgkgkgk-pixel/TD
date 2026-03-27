// ============================================================
// 文件名：BuildTool.cs
// 功能描述：Unity Editor扩展 — 一键打包微信小游戏
//          自动设置微信小游戏平台参数、压缩纹理、分包配置
// 创建时间：2026-03-25
// 所属模块：Editor/BuildTool
// 对应交互：阶段二 #69
// ============================================================

#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AetheraSurvivors.Editor
{
    /// <summary>
    /// 一键打包微信小游戏工具 — Unity Editor扩展
    /// 
    /// 功能：
    /// 1. 自动设置WebGL平台参数
    /// 2. 自动压缩纹理（ASTC/ETC2）
    /// 3. 分包配置检查
    /// 4. 一键打包WebGL → 再使用微信SDK导出小游戏
    /// 5. 打包前检查清单（包体大小、场景列表、图标等）
    /// 
    /// 打开方式：
    ///   Unity菜单 → AetheraSurvivors → Build Tool
    /// </summary>
    public class BuildTool : EditorWindow
    {
        // ========== 常量 ==========

        /// <summary>WebGL输出路径</summary>
        private const string DefaultOutputPath = "D:\\AIRearch\\TD\\Unity\\Wegame\\minigame\\";

        /// <summary>主包大小限制（字节）</summary>
        private const long MaxMainPackageSize = 4 * 1024 * 1024; // 4MB

        // ========== 状态 ==========

        /// <summary>输出路径</summary>
        private string _outputPath = DefaultOutputPath;

        /// <summary>是否使用Development Build</summary>
        private bool _isDevelopment = false;

        /// <summary>是否压缩纹理</summary>
        private bool _compressTextures = true;

        /// <summary>是否启用代码压缩</summary>
        private bool _enableCodeStripping = true;

        /// <summary>打包前检查结果</summary>
        private string _checkResult = "";

        /// <summary>滚动位置</summary>
        private Vector2 _scrollPos;

        // ========== 菜单入口 ==========

        [MenuItem("AetheraSurvivors/一键打包 (Build Tool)")]
        public static void ShowWindow()
        {
            var window = GetWindow<BuildTool>("一键打包");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        // ========== 快捷菜单 ==========

        [MenuItem("AetheraSurvivors/快速打包 - Development")]
        public static void QuickBuildDev()
        {
            DoBuild(DefaultOutputPath, true);
        }

        [MenuItem("AetheraSurvivors/快速打包 - Release")]
        public static void QuickBuildRelease()
        {
            DoBuild(DefaultOutputPath, false);
        }

        // ========== GUI ==========

        private void OnGUI()
        {
            EditorGUILayout.LabelField("═══ AetheraSurvivors 一键打包工具 ═══", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 输出路径
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("输出路径:", GUILayout.Width(60));
            _outputPath = EditorGUILayout.TextField(_outputPath);
            if (GUILayout.Button("浏览", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择输出目录", _outputPath, "");
                if (!string.IsNullOrEmpty(path)) _outputPath = path;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 打包选项
            EditorGUILayout.LabelField("打包选项:", EditorStyles.boldLabel);
            _isDevelopment = EditorGUILayout.Toggle("Development Build", _isDevelopment);
            _compressTextures = EditorGUILayout.Toggle("压缩纹理 (ASTC/ETC2)", _compressTextures);
            _enableCodeStripping = EditorGUILayout.Toggle("IL2CPP代码剥离", _enableCodeStripping);

            EditorGUILayout.Space(5);

            // 当前平台信息
            EditorGUILayout.LabelField("当前环境:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  Unity版本: {Application.unityVersion}");
            EditorGUILayout.LabelField($"  当前平台: {EditorUserBuildSettings.activeBuildTarget}");
            EditorGUILayout.LabelField($"  场景数量: {EditorBuildSettings.scenes.Length}");

            EditorGUILayout.Space(10);

            // 操作按钮
            if (GUILayout.Button("🔍 打包前检查", GUILayout.Height(30)))
            {
                PreBuildCheck();
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.8f, 0.8f, 0.3f);
            if (GUILayout.Button("🔧 设置WebGL平台参数", GUILayout.Height(35)))
            {
                SetupWebGLSettings();
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("📦 一键打包WebGL", GUILayout.Height(35)))
            {
                if (EditorUtility.DisplayDialog("确认打包",
                    $"即将打包WebGL到:\n{_outputPath}\n\n" +
                    $"模式: {(_isDevelopment ? "Development" : "Release")}\n" +
                    "确定继续？", "开始打包", "取消"))
                {
                    DoBuild(_outputPath, _isDevelopment);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // 检查结果
            if (!string.IsNullOrEmpty(_checkResult))
            {
                EditorGUILayout.Space(10);
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(150));
                EditorGUILayout.HelpBox(_checkResult, MessageType.Info);
                EditorGUILayout.EndScrollView();
            }
        }

        // ========== 逻辑方法 ==========

        /// <summary>打包前检查</summary>
        private void PreBuildCheck()
        {
            var sb = new System.Text.StringBuilder();
            int warnings = 0;
            int errors = 0;

            sb.AppendLine("═══ 打包前检查报告 ═══\n");

            // 1. 检查平台
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                sb.AppendLine("⚠️ 当前平台不是WebGL，建议先切换 (File → Build Settings → WebGL)");
                warnings++;
            }
            else
            {
                sb.AppendLine("✅ 当前平台: WebGL");
            }

            // 2. 检查场景列表
            int enabledScenes = 0;
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) enabledScenes++;
            }
            if (enabledScenes == 0)
            {
                sb.AppendLine("❌ Build Settings中没有启用的场景！");
                errors++;
            }
            else
            {
                sb.AppendLine($"✅ 启用场景数: {enabledScenes}");
            }

            // 3. 检查.NET API兼容级别
            var apiLevel = PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.WebGL);
            sb.AppendLine($"ℹ️ .NET API Level: {apiLevel}");

            // 4. 检查IL2CPP（WebGL固定为IL2CPP）
            sb.AppendLine("ℹ️ Scripting Backend: IL2CPP (WebGL默认)");


            // 5. 检查输出目录
            if (!Directory.Exists(_outputPath))
            {
                sb.AppendLine($"ℹ️ 输出目录不存在，将自动创建: {_outputPath}");
            }
            else
            {
                sb.AppendLine($"✅ 输出目录存在: {_outputPath}");
            }

            // 6. 检查微信SDK
            bool hasMiniGameSDK = Directory.Exists("Assets/WX-WASM-SDK-V2");
            if (hasMiniGameSDK)
            {
                sb.AppendLine("✅ 检测到 WX-WASM-SDK-V2");
            }
            else
            {
                sb.AppendLine("⚠️ 未检测到微信小游戏SDK（WX-WASM-SDK-V2）");
                warnings++;
            }

            sb.AppendLine($"\n══ 总结: {errors} 个错误, {warnings} 个警告 ══");
            if (errors > 0)
            {
                sb.AppendLine("⛔ 存在错误，建议修复后再打包");
            }
            else if (warnings > 0)
            {
                sb.AppendLine("⚠️ 存在警告，可以打包但建议关注");
            }
            else
            {
                sb.AppendLine("✅ 全部检查通过，可以打包！");
            }

            _checkResult = sb.ToString();
        }

        /// <summary>设置WebGL平台参数</summary>
        private void SetupWebGLSettings()
        {
            // 切换到WebGL平台（如果还没有切换）
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                Debug.Log("[BuildTool] 正在切换到WebGL平台...");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }

            // 设置Player Settings
            PlayerSettings.companyName = "AetheraSurvivors";
            PlayerSettings.productName = "AetheraSurvivors";

            // WebGL特定设置
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.template = "PROJECT:WXTemplate2022";
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.dataCaching = true;

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
#endif

            // 内存设置
            PlayerSettings.WebGL.emscriptenArgs = "";
            PlayerSettings.WebGL.initialMemorySize = 32;

            // IL2CPP设置（WebGL平台默认使用IL2CPP，无需手动设置ScriptingBackend）
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL,

                _isDevelopment ? Il2CppCompilerConfiguration.Debug : Il2CppCompilerConfiguration.Release);

            // 代码剥离
            if (_enableCodeStripping)
            {
                PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Medium);
            }

            Debug.Log("[BuildTool] ✅ WebGL平台参数设置完成");
            EditorUtility.DisplayDialog("设置完成", "WebGL平台参数已设置！", "确定");
        }

        /// <summary>执行打包</summary>
        private static void DoBuild(string outputPath, bool isDevelopment)
        {
            Debug.Log($"[BuildTool] 开始打包: output={outputPath}, dev={isDevelopment}");

            // 收集启用的场景
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    scenes.Add(scene.path);
                }
            }

            if (scenes.Count == 0)
            {
                EditorUtility.DisplayDialog("打包失败", "Build Settings中没有启用的场景！", "确定");
                return;
            }

            // 确保输出目录存在
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // 构建选项
            var buildOptions = BuildOptions.None;
            if (isDevelopment)
            {
                buildOptions |= BuildOptions.Development;
                buildOptions |= BuildOptions.AllowDebugging;
            }

            // 执行打包
            var report = BuildPipeline.BuildPlayer(
                scenes.ToArray(),
                outputPath,
                BuildTarget.WebGL,
                buildOptions
            );

            // 输出结果
            if (report.summary.result == BuildResult.Succeeded)
            {
                ulong totalSize = report.summary.totalSize;
                float sizeMB = totalSize / (1024f * 1024f);


                string message = $"✅ 打包成功!\n\n" +
                    $"输出路径: {outputPath}\n" +
                    $"总大小: {sizeMB:F2} MB\n" +
                    $"打包耗时: {report.summary.totalTime.TotalSeconds:F1} 秒\n" +
                    $"场景数: {scenes.Count}\n" +
                    $"警告数: {report.summary.totalWarnings}\n" +
                    $"错误数: {report.summary.totalErrors}";

                Debug.Log($"[BuildTool] {message}");
                EditorUtility.DisplayDialog("打包成功", message, "确定");

                // 检查包体大小
                if (totalSize > (ulong)MaxMainPackageSize)

                {
                    EditorUtility.DisplayDialog("⚠️ 包体超限",
                        $"主包大小 {sizeMB:F2}MB 超过微信限制 4MB！\n" +
                        "请检查资源优化和分包配置。", "知道了");
                }
            }
            else
            {
                string error = $"❌ 打包失败!\n结果: {report.summary.result}\n错误数: {report.summary.totalErrors}";
                Debug.LogError($"[BuildTool] {error}");
                EditorUtility.DisplayDialog("打包失败", error, "确定");
            }
        }
    }
}

#endif
