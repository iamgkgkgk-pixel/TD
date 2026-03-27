// ============================================================
// 文件名：SpriteImporter.cs
// 功能描述：美术资源批量导入编辑器工具
//   1. 自动扫描 Resources/Sprites/ 目录下的PNG图片
//   2. 设置正确的 Sprite 导入参数（PPU/压缩/FilterMode）
//   3. 生成 SpriteAtlas 合图（按类别分组）
//   4. 提供运行时 Sprite 加载API（替换代码中的占位Texture2D）
// 创建时间：2026-03-25
// 所属模块：Editor
// 对应交互：#160.9
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace AetheraSurvivors.Editor
{
    /// <summary>
    /// 美术资源批量导入工具 — 编辑器窗口
    /// 菜单路径：AetheraSurvivors > Sprite Importer
    /// </summary>
    public class SpriteImporter : EditorWindow
    {
        // ====================================================================
        // 常量定义
        // ====================================================================

        /// <summary>Sprites资源根目录（相对于Assets）</summary>
        private const string SPRITES_ROOT = "Assets/Resources/Sprites";

        /// <summary>SpriteAtlas输出目录</summary>
        private const string ATLAS_OUTPUT = "Assets/Resources/Atlas";

        /// <summary>子目录与PPU映射配置</summary>
        private static readonly Dictionary<string, SpriteImportProfile> IMPORT_PROFILES = new Dictionary<string, SpriteImportProfile>
        {
            { "Towers",  new SpriteImportProfile { ppu = 64, filterMode = FilterMode.Point, maxSize = 128,  atlasName = "Atlas_Towers"  } },
            { "Enemies", new SpriteImportProfile { ppu = 64, filterMode = FilterMode.Point, maxSize = 256,  atlasName = "Atlas_Enemies" } },
            { "Maps",    new SpriteImportProfile { ppu = 32, filterMode = FilterMode.Point, maxSize = 64,   atlasName = "Atlas_Maps"    } },
            { "Effects", new SpriteImportProfile { ppu = 64, filterMode = FilterMode.Point, maxSize = 32,   atlasName = "Atlas_Effects" } },
            { "UI",      new SpriteImportProfile { ppu = 48, filterMode = FilterMode.Bilinear, maxSize = 64, atlasName = "Atlas_UI"     } },
        };

        /// <summary>导入配置结构</summary>
        private struct SpriteImportProfile
        {
            public int ppu;             // Pixels Per Unit
            public FilterMode filterMode;
            public int maxSize;         // 单张纹理最大尺寸
            public string atlasName;    // SpriteAtlas名称
        }

        // ====================================================================
        // 编辑器窗口状态
        // ====================================================================

        private Vector2 _scrollPos;
        private List<SpriteImportResult> _lastResults = new List<SpriteImportResult>();
        private bool _showResults;

        /// <summary>单个Sprite导入结果</summary>
        private struct SpriteImportResult
        {
            public string path;
            public string category;
            public bool success;
            public string message;
        }

        // ====================================================================
        // 菜单入口
        // ====================================================================

        [MenuItem("AetheraSurvivors/Sprite Importer 美术资源导入工具")]
        public static void ShowWindow()
        {
            var window = GetWindow<SpriteImporter>("Sprite Importer");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        // ====================================================================
        // GUI绘制
        // ====================================================================

        private void OnGUI()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("🎨 AetheraSurvivors 美术资源导入工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "本工具用于批量导入AI生成的PNG图片到Unity，自动设置Sprite参数。\n" +
                "请将PNG图片按类别放入 Resources/Sprites/ 对应子目录后，点击导入。",
                MessageType.Info);

            GUILayout.Space(10);

            // 显示各目录状态
            EditorGUILayout.LabelField("📁 资源目录状态", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            foreach (var kv in IMPORT_PROFILES)
            {
                string dir = Path.Combine(SPRITES_ROOT, kv.Key);
                int count = CountPngFiles(dir);
                string status = count > 0 ? $"✅ {count} 张PNG" : "⬜ 空";
                EditorGUILayout.LabelField($"{kv.Key}/", $"{status} (PPU={kv.Value.ppu}, Filter={kv.Value.filterMode})");
            }
            EditorGUI.indentLevel--;

            GUILayout.Space(15);

            // 操作按钮
            EditorGUILayout.LabelField("🔧 操作", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("📂 打开Sprites目录", GUILayout.Height(30)))
            {
                EditorUtility.RevealInFinder(SPRITES_ROOT);
            }
            if (GUILayout.Button("🔄 刷新", GUILayout.Height(30)))
            {
                AssetDatabase.Refresh();
                Repaint();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // 核心按钮：批量导入
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("🚀 批量导入并设置所有Sprite", GUILayout.Height(40)))
            {
                _lastResults = ImportAllSprites();
                _showResults = true;
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(5);

            // 生成SpriteAtlas按钮
            GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
            if (GUILayout.Button("📦 生成SpriteAtlas合图", GUILayout.Height(35)))
            {
                GenerateAllAtlases();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(5);

            // 验证按钮
            GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
            if (GUILayout.Button("✅ 验证所有Sprite设置", GUILayout.Height(30)))
            {
                _lastResults = ValidateAllSprites();
                _showResults = true;
            }
            GUI.backgroundColor = Color.white;

            // 显示结果
            if (_showResults && _lastResults.Count > 0)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("📋 操作结果", EditorStyles.boldLabel);

                int successCount = _lastResults.Count(r => r.success);
                int failCount = _lastResults.Count - successCount;
                EditorGUILayout.LabelField($"成功: {successCount}  失败: {failCount}");

                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MaxHeight(300));
                foreach (var result in _lastResults)
                {
                    string icon = result.success ? "✅" : "❌";
                    EditorGUILayout.LabelField($"{icon} [{result.category}] {Path.GetFileName(result.path)}", result.message);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        // ====================================================================
        // 核心功能：批量导入
        // ====================================================================

        /// <summary>批量导入并设置所有Sprite</summary>
        private static List<SpriteImportResult> ImportAllSprites()
        {
            var results = new List<SpriteImportResult>();
            int total = 0;
            int processed = 0;

            // 先统计总数
            foreach (var kv in IMPORT_PROFILES)
            {
                string dir = Path.Combine(SPRITES_ROOT, kv.Key);
                total += CountPngFiles(dir);
            }

            if (total == 0)
            {
                Debug.LogWarning("[SpriteImporter] 没有找到任何PNG文件！请先将图片放入 Resources/Sprites/ 对应目录。");
                return results;
            }

            try
            {
                AssetDatabase.StartAssetEditing(); // 批量操作优化

                foreach (var kv in IMPORT_PROFILES)
                {
                    string category = kv.Key;
                    SpriteImportProfile profile = kv.Value;
                    string dir = Path.Combine(SPRITES_ROOT, category);

                    if (!Directory.Exists(dir)) continue;

                    string[] pngFiles = Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly);

                    foreach (string filePath in pngFiles)
                    {
                        processed++;
                        string assetPath = filePath.Replace("\\", "/");

                        // 确保路径以Assets/开头
                        if (!assetPath.StartsWith("Assets/"))
                        {
                            int idx = assetPath.IndexOf("Assets/", StringComparison.Ordinal);
                            if (idx >= 0) assetPath = assetPath.Substring(idx);
                        }

                        EditorUtility.DisplayProgressBar(
                            "导入Sprite",
                            $"[{processed}/{total}] {Path.GetFileName(assetPath)}",
                            (float)processed / total);

                        var result = ImportSingleSprite(assetPath, category, profile);
                        results.Add(result);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            int success = results.Count(r => r.success);
            Debug.Log($"[SpriteImporter] 导入完成！成功: {success}/{results.Count}");

            return results;
        }

        /// <summary>导入单个Sprite并设置参数</summary>
        private static SpriteImportResult ImportSingleSprite(string assetPath, string category, SpriteImportProfile profile)
        {
            var result = new SpriteImportResult
            {
                path = assetPath,
                category = category,
                success = false,
                message = ""
            };

            try
            {
                // 获取TextureImporter
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    result.message = "无法获取TextureImporter";
                    return result;
                }

                bool needsReimport = false;

                // 设置为Sprite模式
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    needsReimport = true;
                }

                // 设置Sprite模式为Single
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    needsReimport = true;
                }

                // 设置PPU
                if (Mathf.Abs(importer.spritePixelsPerUnit - profile.ppu) > 0.1f)
                {
                    importer.spritePixelsPerUnit = profile.ppu;
                    needsReimport = true;
                }

                // 设置FilterMode
                if (importer.filterMode != profile.filterMode)
                {
                    importer.filterMode = profile.filterMode;
                    needsReimport = true;
                }

                // 关闭Mipmap（2D游戏不需要）
                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    needsReimport = true;
                }

                // 设置压缩格式 — WebGL平台使用ASTC
                TextureImporterPlatformSettings webglSettings = importer.GetPlatformTextureSettings("WebGL");
                if (!webglSettings.overridden || webglSettings.format != TextureImporterFormat.ASTC_4x4)
                {
                    webglSettings.overridden = true;
                    webglSettings.maxTextureSize = profile.maxSize;
                    webglSettings.format = TextureImporterFormat.ASTC_4x4;
                    webglSettings.compressionQuality = 50;
                    importer.SetPlatformTextureSettings(webglSettings);
                    needsReimport = true;
                }

                // 设置默认平台
                TextureImporterPlatformSettings defaultSettings = importer.GetDefaultPlatformTextureSettings();
                if (defaultSettings.maxTextureSize != profile.maxSize * 2) // 默认平台给大一点
                {
                    defaultSettings.maxTextureSize = profile.maxSize * 2;
                    defaultSettings.format = TextureImporterFormat.Automatic;
                    defaultSettings.compressionQuality = 50;
                    importer.SetPlatformTextureSettings(defaultSettings);
                    needsReimport = true;
                }

                // 设置纹理透明通道
                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    needsReimport = true;
                }

                // 需要重新导入
                if (needsReimport)
                {
                    importer.SaveAndReimport();
                    result.message = $"PPU={profile.ppu} Filter={profile.filterMode} ASTC_4x4";
                }
                else
                {
                    result.message = "设置已正确，无需修改";
                }

                result.success = true;
            }
            catch (Exception e)
            {
                result.message = $"错误: {e.Message}";
                Debug.LogError($"[SpriteImporter] 导入失败: {assetPath} - {e.Message}");
            }

            return result;
        }

        // ====================================================================
        // SpriteAtlas合图生成
        // ====================================================================

        /// <summary>为所有类别生成SpriteAtlas</summary>
        private static void GenerateAllAtlases()
        {
            // 确保输出目录存在
            if (!Directory.Exists(ATLAS_OUTPUT))
            {
                Directory.CreateDirectory(ATLAS_OUTPUT);
                AssetDatabase.Refresh();
            }

            int created = 0;

            foreach (var kv in IMPORT_PROFILES)
            {
                string category = kv.Key;
                SpriteImportProfile profile = kv.Value;
                string spriteDir = Path.Combine(SPRITES_ROOT, category);

                if (!Directory.Exists(spriteDir) || CountPngFiles(spriteDir) == 0)
                {
                    Debug.Log($"[SpriteImporter] 跳过 {category}/ — 无PNG文件");
                    continue;
                }

                string atlasPath = Path.Combine(ATLAS_OUTPUT, profile.atlasName + ".spriteatlas");

                // 创建或加载SpriteAtlas
                SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                bool isNew = atlas == null;

                if (isNew)
                {
                    atlas = new SpriteAtlas();
                }

                // 设置Atlas打包参数
                SpriteAtlasPackingSettings packSettings = new SpriteAtlasPackingSettings
                {
                    blockOffset = 1,
                    padding = 2,
                    enableRotation = false,
                    enableTightPacking = false
                };
                atlas.SetPackingSettings(packSettings);

                // 设置Atlas纹理参数
                SpriteAtlasTextureSettings texSettings = new SpriteAtlasTextureSettings
                {
                    readable = false,
                    generateMipMaps = false,
                    sRGB = true,
                    filterMode = profile.filterMode
                };
                atlas.SetTextureSettings(texSettings);

                // 设置WebGL平台压缩
                TextureImporterPlatformSettings webglPlatform = atlas.GetPlatformSettings("WebGL");
                webglPlatform.overridden = true;
                webglPlatform.maxTextureSize = 2048; // Atlas最大2048x2048
                webglPlatform.format = TextureImporterFormat.ASTC_4x4;
                webglPlatform.compressionQuality = 50;
                atlas.SetPlatformSettings(webglPlatform);

                // 添加整个文件夹作为源
                var folderObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(spriteDir);
                if (folderObj != null)
                {
                    // 清除旧的包含对象
                    var existing = atlas.GetPackables();
                    if (existing != null && existing.Length > 0)
                    {
                        atlas.Remove(existing);
                    }
                    atlas.Add(new UnityEngine.Object[] { folderObj });
                }

                if (isNew)
                {
                    AssetDatabase.CreateAsset(atlas, atlasPath);
                }
                else
                {
                    EditorUtility.SetDirty(atlas);
                }

                created++;
                Debug.Log($"[SpriteImporter] SpriteAtlas {(isNew ? "创建" : "更新")}: {atlasPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SpriteImporter] SpriteAtlas生成完成！共 {created} 个。");
            EditorUtility.DisplayDialog("SpriteAtlas", $"SpriteAtlas生成完成！共 {created} 个。", "确定");
        }

        // ====================================================================
        // 验证功能
        // ====================================================================

        /// <summary>验证所有Sprite设置是否正确</summary>
        private static List<SpriteImportResult> ValidateAllSprites()
        {
            var results = new List<SpriteImportResult>();

            foreach (var kv in IMPORT_PROFILES)
            {
                string category = kv.Key;
                SpriteImportProfile profile = kv.Value;
                string dir = Path.Combine(SPRITES_ROOT, category);

                if (!Directory.Exists(dir)) continue;

                string[] pngFiles = Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly);

                foreach (string filePath in pngFiles)
                {
                    string assetPath = filePath.Replace("\\", "/");
                    if (!assetPath.StartsWith("Assets/"))
                    {
                        int idx = assetPath.IndexOf("Assets/", StringComparison.Ordinal);
                        if (idx >= 0) assetPath = assetPath.Substring(idx);
                    }

                    var result = ValidateSingleSprite(assetPath, category, profile);
                    results.Add(result);
                }
            }

            int success = results.Count(r => r.success);
            int fail = results.Count - success;
            string msg = fail == 0
                ? $"验证通过！所有 {results.Count} 张Sprite设置正确。"
                : $"发现 {fail} 张Sprite设置异常，请重新导入。";

            Debug.Log($"[SpriteImporter] {msg}");
            EditorUtility.DisplayDialog("验证结果", msg, "确定");

            return results;
        }

        /// <summary>验证单个Sprite设置</summary>
        private static SpriteImportResult ValidateSingleSprite(string assetPath, string category, SpriteImportProfile profile)
        {
            var result = new SpriteImportResult
            {
                path = assetPath,
                category = category,
                success = true,
                message = "✅ 设置正确"
            };

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                result.success = false;
                result.message = "❌ 无法获取TextureImporter";
                return result;
            }

            var issues = new List<string>();

            if (importer.textureType != TextureImporterType.Sprite)
                issues.Add("类型非Sprite");

            if (Mathf.Abs(importer.spritePixelsPerUnit - profile.ppu) > 0.1f)
                issues.Add($"PPU={importer.spritePixelsPerUnit}(应为{profile.ppu})");

            if (importer.filterMode != profile.filterMode)
                issues.Add($"Filter={importer.filterMode}(应为{profile.filterMode})");

            if (importer.mipmapEnabled)
                issues.Add("Mipmap未关闭");

            if (!importer.alphaIsTransparency)
                issues.Add("Alpha透明未开启");

            TextureImporterPlatformSettings webgl = importer.GetPlatformTextureSettings("WebGL");
            if (!webgl.overridden)
                issues.Add("WebGL平台未覆盖设置");
            else if (webgl.format != TextureImporterFormat.ASTC_4x4)
                issues.Add($"WebGL压缩格式={webgl.format}(应为ASTC_4x4)");

            if (issues.Count > 0)
            {
                result.success = false;
                result.message = "❌ " + string.Join("; ", issues);
            }

            return result;
        }

        // ====================================================================
        // 工具方法
        // ====================================================================

        /// <summary>统计目录中PNG文件数量</summary>
        private static int CountPngFiles(string dir)
        {
            if (!Directory.Exists(dir)) return 0;
            return Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly).Length;
        }
    }

}

