// ============================================================
// 文件名：ConfigExporter.cs
// 功能描述：Unity Editor扩展 — 配置表导出工具
//          支持从CSV导出为JSON配置文件
// 创建时间：2026-03-25
// 所属模块：Editor/ConfigExporter
// 对应交互：阶段二 #68
// ============================================================

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AetheraSurvivors.Editor
{
    /// <summary>
    /// 配置表导出工具 — Unity Editor扩展
    /// 
    /// 功能：
    /// 1. 批量导入CSV配置表并转换为JSON格式
    /// 2. 支持自定义导出路径
    /// 3. 导出后自动刷新AssetDatabase
    /// 4. CSV解析支持引号内逗号
    /// 
    /// 打开方式：
    ///   Unity菜单 → AetheraSurvivors → Config Exporter
    /// </summary>
    public class ConfigExporter : EditorWindow
    {
        // ========== 常量 ==========

        /// <summary>CSV源文件目录</summary>
        private const string DefaultCsvPath = "Assets/Resources/Configs/";

        /// <summary>JSON导出目录</summary>
        private const string DefaultJsonPath = "Assets/Resources/Configs/";

        // ========== 状态 ==========

        /// <summary>CSV源文件路径</summary>
        private string _csvSourcePath = DefaultCsvPath;

        /// <summary>JSON导出路径</summary>
        private string _jsonOutputPath = DefaultJsonPath;

        /// <summary>扫描到的CSV文件列表</summary>
        private List<string> _csvFiles = new List<string>();

        /// <summary>选中导出的文件</summary>
        private Dictionary<string, bool> _selectedFiles = new Dictionary<string, bool>();

        /// <summary>滚动位置</summary>
        private Vector2 _scrollPos;

        /// <summary>上次导出日志</summary>
        private string _lastLog = "";

        // ========== 菜单入口 ==========

        [MenuItem("AetheraSurvivors/配置表导出工具 (Config Exporter)")]
        public static void ShowWindow()
        {
            var window = GetWindow<ConfigExporter>("配置表导出");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        // ========== GUI ==========

        private void OnGUI()
        {
            EditorGUILayout.LabelField("═══ CSV → JSON 配置表导出工具 ═══", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 路径配置
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("CSV源目录:", GUILayout.Width(80));
            _csvSourcePath = EditorGUILayout.TextField(_csvSourcePath);
            if (GUILayout.Button("浏览", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择CSV目录", _csvSourcePath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    _csvSourcePath = "Assets" + path.Replace(Application.dataPath, "");
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("JSON输出:", GUILayout.Width(80));
            _jsonOutputPath = EditorGUILayout.TextField(_jsonOutputPath);
            if (GUILayout.Button("浏览", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择输出目录", _jsonOutputPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    _jsonOutputPath = "Assets" + path.Replace(Application.dataPath, "");
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 扫描按钮
            if (GUILayout.Button("🔍 扫描CSV文件", GUILayout.Height(25)))
            {
                ScanCSVFiles();
            }

            EditorGUILayout.Space(5);

            // 文件列表
            if (_csvFiles.Count > 0)
            {
                EditorGUILayout.LabelField($"找到 {_csvFiles.Count} 个CSV文件：", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("全选", GUILayout.Width(60)))
                {
                    foreach (var file in _csvFiles) _selectedFiles[file] = true;
                }
                if (GUILayout.Button("全不选", GUILayout.Width(60)))
                {
                    foreach (var file in _csvFiles) _selectedFiles[file] = false;
                }
                EditorGUILayout.EndHorizontal();

                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(200));
                foreach (var file in _csvFiles)
                {
                    if (!_selectedFiles.ContainsKey(file)) _selectedFiles[file] = true;

                    EditorGUILayout.BeginHorizontal();
                    _selectedFiles[file] = EditorGUILayout.Toggle(_selectedFiles[file], GUILayout.Width(20));
                    EditorGUILayout.LabelField(Path.GetFileName(file));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(5);

                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button("📦 导出选中的CSV为JSON", GUILayout.Height(35)))
                {
                    ExportSelected();
                }
                GUI.backgroundColor = Color.white;
            }

            // 日志显示
            if (!string.IsNullOrEmpty(_lastLog))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(_lastLog, MessageType.Info);
            }
        }

        // ========== 逻辑方法 ==========

        /// <summary>扫描CSV文件</summary>
        private void ScanCSVFiles()
        {
            _csvFiles.Clear();
            _selectedFiles.Clear();

            string fullPath = Path.Combine(Application.dataPath,
                _csvSourcePath.Replace("Assets/", ""));

            if (!Directory.Exists(fullPath))
            {
                _lastLog = "❌ 目录不存在: " + fullPath;
                return;
            }

            var files = Directory.GetFiles(fullPath, "*.csv", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                _csvFiles.Add(file);
            }

            _lastLog = $"✅ 扫描完成，找到 {_csvFiles.Count} 个CSV文件";
        }

        /// <summary>导出选中的CSV</summary>
        private void ExportSelected()
        {
            int exported = 0;
            int failed = 0;
            var logBuilder = new StringBuilder();

            foreach (var file in _csvFiles)
            {
                if (!_selectedFiles.ContainsKey(file) || !_selectedFiles[file]) continue;

                try
                {
                    string json = ConvertCSVToJSON(file);
                    string outputDir = Path.Combine(Application.dataPath,
                        _jsonOutputPath.Replace("Assets/", ""));

                    if (!Directory.Exists(outputDir))
                    {
                        Directory.CreateDirectory(outputDir);
                    }

                    string outputFile = Path.Combine(outputDir,
                        Path.GetFileNameWithoutExtension(file) + ".json");
                    File.WriteAllText(outputFile, json, Encoding.UTF8);

                    exported++;
                    logBuilder.AppendLine($"  ✅ {Path.GetFileName(file)} → JSON");
                }
                catch (Exception e)
                {
                    failed++;
                    logBuilder.AppendLine($"  ❌ {Path.GetFileName(file)}: {e.Message}");
                }
            }

            AssetDatabase.Refresh();

            _lastLog = $"导出完成: 成功 {exported}，失败 {failed}\n{logBuilder}";
        }

        /// <summary>
        /// 将CSV文件转换为JSON字符串
        /// CSV格式约定：
        ///   第1行：字段名（表头）
        ///   第2行起：数据行
        ///   支持引号包裹（处理含逗号的值）
        /// </summary>
        private string ConvertCSVToJSON(string csvPath)
        {
            var lines = File.ReadAllLines(csvPath, Encoding.UTF8);

            if (lines.Length < 2)
            {
                throw new Exception("CSV文件至少需要2行（表头+数据）");
            }

            // 解析表头
            var headers = ParseCSVLine(lines[0]);

            // 解析数据行
            var jsonBuilder = new StringBuilder();
            jsonBuilder.AppendLine("[");

            bool isFirst = true;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue; // 跳过空行
                if (line.StartsWith("//") || line.StartsWith("#")) continue; // 跳过注释行

                var values = ParseCSVLine(line);

                if (!isFirst) jsonBuilder.AppendLine(",");
                isFirst = false;

                jsonBuilder.Append("  {");
                for (int j = 0; j < headers.Length && j < values.Length; j++)
                {
                    if (j > 0) jsonBuilder.Append(",");
                    string header = headers[j].Trim();
                    string value = values[j].Trim();

                    // 尝试判断数值类型
                    if (int.TryParse(value, out int intVal))
                    {
                        jsonBuilder.Append($"\"{header}\":{intVal}");
                    }
                    else if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float floatVal))
                    {
                        jsonBuilder.Append($"\"{header}\":{floatVal.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    }
                    else if (value.ToLower() == "true" || value.ToLower() == "false")
                    {
                        jsonBuilder.Append($"\"{header}\":{value.ToLower()}");
                    }
                    else
                    {
                        // 转义JSON特殊字符
                        value = value.Replace("\\", "\\\\")
                                     .Replace("\"", "\\\"")
                                     .Replace("\n", "\\n")
                                     .Replace("\r", "\\r")
                                     .Replace("\t", "\\t");
                        jsonBuilder.Append($"\"{header}\":\"{value}\"");
                    }
                }
                jsonBuilder.Append("}");
            }

            jsonBuilder.AppendLine();
            jsonBuilder.Append("]");

            return jsonBuilder.ToString();
        }

        /// <summary>解析CSV行（支持引号内逗号）</summary>
        private string[] ParseCSVLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }
    }
}

#endif
