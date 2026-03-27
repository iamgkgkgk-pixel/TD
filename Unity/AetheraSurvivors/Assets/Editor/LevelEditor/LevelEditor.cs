// ============================================================
// 文件名：LevelEditor.cs
// 功能描述：Unity Editor扩展 — 关卡编辑器
//          可视化编辑地图网格、放塔点、怪物路径、波次配置
// 创建时间：2026-03-25
// 所属模块：Editor/LevelEditor
// 对应交互：阶段二 #67
// ============================================================

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AetheraSurvivors.Editor
{
    /// <summary>
    /// 地图网格单元类型
    /// </summary>
    public enum CellType
    {
        /// <summary>空地（不可放塔不可行走）</summary>
        Empty = 0,
        /// <summary>怪物路径</summary>
        Path = 1,
        /// <summary>可放塔位置</summary>
        TowerSlot = 2,
        /// <summary>障碍物</summary>
        Obstacle = 3,
        /// <summary>怪物出生点</summary>
        SpawnPoint = 4,
        /// <summary>玩家基地（终点）</summary>
        BasePoint = 5
    }

    /// <summary>
    /// 关卡编辑数据
    /// </summary>
    [Serializable]
    public class LevelEditorData
    {
        /// <summary>关卡ID</summary>
        public string levelId;
        /// <summary>所属章节</summary>
        public int chapter;
        /// <summary>章节内序号</summary>
        public int levelIndex;
        /// <summary>地图宽度</summary>
        public int width = 10;
        /// <summary>地图高度</summary>
        public int height = 10;
        /// <summary>网格数据（一维数组，行优先）</summary>
        public int[] gridData;
        /// <summary>怪物路径点列表（有序）</summary>
        public List<Vector2Int> pathPoints = new List<Vector2Int>();
        /// <summary>出生点坐标</summary>
        public Vector2Int spawnPoint;
        /// <summary>基地坐标</summary>
        public Vector2Int basePoint;
        /// <summary>关卡描述</summary>
        public string description;

        /// <summary>初始化网格</summary>
        public void InitGrid()
        {
            gridData = new int[width * height];
        }

        /// <summary>获取网格类型</summary>
        public CellType GetCell(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return CellType.Empty;
            return (CellType)gridData[y * width + x];
        }

        /// <summary>设置网格类型</summary>
        public void SetCell(int x, int y, CellType type)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            gridData[y * width + x] = (int)type;
        }
    }

    /// <summary>
    /// 关卡编辑器窗口 — Unity Editor扩展
    /// 
    /// 功能：
    /// 1. 可视化网格编辑（点击/拖拽绘制不同类型的网格）
    /// 2. 路径点编辑（有序路径标记）
    /// 3. 出生点/基地位置设置
    /// 4. 关卡数据保存/加载（JSON格式）
    /// 5. 关卡列表管理
    /// 
    /// 打开方式：
    ///   Unity菜单 → AetheraSurvivors → Level Editor
    /// </summary>
    public class LevelEditor : EditorWindow
    {
        // ========== 常量 ==========

        /// <summary>网格单元大小（像素）</summary>
        private const float CellSize = 30f;

        /// <summary>关卡数据保存路径</summary>
        private const string SavePath = "Assets/Resources/Configs/Levels/";

        // ========== 编辑器状态 ==========

        /// <summary>当前编辑的关卡数据</summary>
        private LevelEditorData _currentLevel;

        /// <summary>当前选择的绘制工具</summary>
        private CellType _currentTool = CellType.Path;

        /// <summary>滚动位置</summary>
        private Vector2 _scrollPosition;

        /// <summary>是否显示网格坐标</summary>
        private bool _showCoordinates = false;

        /// <summary>是否显示路径序号</summary>
        private bool _showPathOrder = true;

        /// <summary>新建关卡参数</summary>
        private int _newWidth = 10;
        private int _newHeight = 10;
        private int _newChapter = 1;
        private int _newLevelIndex = 1;

        // ========== 颜色定义 ==========

        private static readonly Color ColorEmpty = new Color(0.3f, 0.3f, 0.3f, 1f);
        private static readonly Color ColorPath = new Color(0.9f, 0.8f, 0.5f, 1f);
        private static readonly Color ColorTowerSlot = new Color(0.4f, 0.7f, 0.4f, 1f);
        private static readonly Color ColorObstacle = new Color(0.5f, 0.3f, 0.2f, 1f);
        private static readonly Color ColorSpawn = new Color(1f, 0.3f, 0.3f, 1f);
        private static readonly Color ColorBase = new Color(0.3f, 0.5f, 1f, 1f);

        // ========== 菜单入口 ==========

        [MenuItem("AetheraSurvivors/关卡编辑器 (Level Editor)")]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelEditor>("关卡编辑器");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }

        // ========== GUI绘制 ==========

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.Space(5);

            if (_currentLevel == null)
            {
                DrawNewLevelPanel();
                EditorGUILayout.Space(10);
                DrawLoadPanel();
            }
            else
            {
                DrawLevelInfo();
                EditorGUILayout.Space(5);
                DrawToolSelector();
                EditorGUILayout.Space(5);
                DrawGrid();
                EditorGUILayout.Space(5);
                DrawActionButtons();
            }
        }

        /// <summary>绘制顶部工具栏</summary>
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("新建关卡", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                _currentLevel = null; // 回到新建界面
            }

            if (GUILayout.Button("加载关卡", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                LoadLevel();
            }

            if (_currentLevel != null)
            {
                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    SaveLevel();
                }
            }

            GUILayout.FlexibleSpace();

            _showCoordinates = GUILayout.Toggle(_showCoordinates, "坐标", EditorStyles.toolbarButton);
            _showPathOrder = GUILayout.Toggle(_showPathOrder, "路径序号", EditorStyles.toolbarButton);

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>绘制新建关卡面板</summary>
        private void DrawNewLevelPanel()
        {
            EditorGUILayout.LabelField("═══ 新建关卡 ═══", EditorStyles.boldLabel);

            _newChapter = EditorGUILayout.IntSlider("章节", _newChapter, 1, 30);
            _newLevelIndex = EditorGUILayout.IntSlider("关卡序号", _newLevelIndex, 1, 5);
            _newWidth = EditorGUILayout.IntSlider("地图宽度", _newWidth, 5, 20);
            _newHeight = EditorGUILayout.IntSlider("地图高度", _newHeight, 5, 20);

            EditorGUILayout.Space(5);

            if (GUILayout.Button("创建新关卡", GUILayout.Height(30)))
            {
                CreateNewLevel();
            }
        }

        /// <summary>绘制加载面板</summary>
        private void DrawLoadPanel()
        {
            EditorGUILayout.LabelField("═══ 已有关卡 ═══", EditorStyles.boldLabel);

            if (!Directory.Exists(SavePath))
            {
                EditorGUILayout.HelpBox("暂无已保存的关卡数据", MessageType.Info);
                return;
            }

            var files = Directory.GetFiles(SavePath, "*.json");
            foreach (var file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(fileName);
                if (GUILayout.Button("编辑", GUILayout.Width(60)))
                {
                    LoadLevelFromFile(file);
                }
                if (GUILayout.Button("删除", GUILayout.Width(60)))
                {
                    if (EditorUtility.DisplayDialog("确认删除", $"确定要删除 {fileName} 吗？", "删除", "取消"))
                    {
                        File.Delete(file);
                        AssetDatabase.Refresh();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>绘制关卡基本信息</summary>
        private void DrawLevelInfo()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"关卡: Ch{_currentLevel.chapter}-{_currentLevel.levelIndex}",
                EditorStyles.boldLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField($"尺寸: {_currentLevel.width}×{_currentLevel.height}",
                GUILayout.Width(100));
            _currentLevel.description = EditorGUILayout.TextField("描述", _currentLevel.description);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>绘制工具选择器</summary>
        private void DrawToolSelector()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("绘制工具:", GUILayout.Width(60));

            DrawToolButton(CellType.Path, "路径", ColorPath);
            DrawToolButton(CellType.TowerSlot, "塔位", ColorTowerSlot);
            DrawToolButton(CellType.Obstacle, "障碍", ColorObstacle);
            DrawToolButton(CellType.SpawnPoint, "出生点", ColorSpawn);
            DrawToolButton(CellType.BasePoint, "基地", ColorBase);
            DrawToolButton(CellType.Empty, "橡皮擦", ColorEmpty);

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>绘制工具按钮</summary>
        private void DrawToolButton(CellType type, string label, Color color)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = _currentTool == type ? Color.white : color;

            var style = _currentTool == type
                ? new GUIStyle(EditorStyles.miniButtonMid) { fontStyle = FontStyle.Bold }
                : EditorStyles.miniButtonMid;

            if (GUILayout.Button(label, style, GUILayout.Width(60)))
            {
                _currentTool = type;
            }

            GUI.backgroundColor = oldColor;
        }

        /// <summary>绘制网格</summary>
        private void DrawGrid()
        {
            if (_currentLevel == null || _currentLevel.gridData == null) return;

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            float totalWidth = _currentLevel.width * CellSize;
            float totalHeight = _currentLevel.height * CellSize;

            var gridRect = GUILayoutUtility.GetRect(totalWidth + 20, totalHeight + 20);

            // 绘制每个网格
            for (int y = 0; y < _currentLevel.height; y++)
            {
                for (int x = 0; x < _currentLevel.width; x++)
                {
                    var cellRect = new Rect(
                        gridRect.x + x * CellSize,
                        gridRect.y + y * CellSize,
                        CellSize - 1,
                        CellSize - 1
                    );

                    CellType cellType = _currentLevel.GetCell(x, y);
                    Color cellColor = GetCellColor(cellType);

                    EditorGUI.DrawRect(cellRect, cellColor);

                    // 显示坐标
                    if (_showCoordinates)
                    {
                        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = 7
                        };
                        GUI.Label(cellRect, $"{x},{y}", labelStyle);
                    }

                    // 显示路径序号
                    if (_showPathOrder && cellType == CellType.Path)
                    {
                        int pathIndex = _currentLevel.pathPoints.IndexOf(new Vector2Int(x, y));
                        if (pathIndex >= 0)
                        {
                            var numStyle = new GUIStyle(EditorStyles.boldLabel)
                            {
                                alignment = TextAnchor.MiddleCenter,
                                fontSize = 10,
                                normal = { textColor = Color.black }
                            };
                            GUI.Label(cellRect, pathIndex.ToString(), numStyle);
                        }
                    }

                    // 处理点击
                    if (Event.current.type == EventType.MouseDown &&
                        cellRect.Contains(Event.current.mousePosition))
                    {
                        HandleCellClick(x, y);
                        Event.current.Use();
                        Repaint();
                    }

                    // 处理拖拽
                    if (Event.current.type == EventType.MouseDrag &&
                        cellRect.Contains(Event.current.mousePosition))
                    {
                        // 拖拽只支持路径/塔位/障碍/橡皮擦
                        if (_currentTool != CellType.SpawnPoint && _currentTool != CellType.BasePoint)
                        {
                            HandleCellClick(x, y);
                            Event.current.Use();
                            Repaint();
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>绘制操作按钮</summary>
        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("清空地图"))
            {
                if (EditorUtility.DisplayDialog("确认", "确定要清空当前地图吗？", "确定", "取消"))
                {
                    _currentLevel.InitGrid();
                    _currentLevel.pathPoints.Clear();
                    Repaint();
                }
            }

            if (GUILayout.Button("清空路径点"))
            {
                _currentLevel.pathPoints.Clear();
                Repaint();
            }

            if (GUILayout.Button("验证地图"))
            {
                ValidateLevel();
            }

            EditorGUILayout.EndHorizontal();

            // 信息显示
            EditorGUILayout.HelpBox(
                $"路径点数量: {_currentLevel.pathPoints.Count}  |  " +
                $"出生点: ({_currentLevel.spawnPoint.x},{_currentLevel.spawnPoint.y})  |  " +
                $"基地: ({_currentLevel.basePoint.x},{_currentLevel.basePoint.y})",
                MessageType.None
            );
        }

        // ========== 逻辑方法 ==========

        /// <summary>处理网格点击</summary>
        private void HandleCellClick(int x, int y)
        {
            if (_currentTool == CellType.SpawnPoint)
            {
                // 清除旧出生点
                ClearCellType(CellType.SpawnPoint);
                _currentLevel.spawnPoint = new Vector2Int(x, y);
            }
            else if (_currentTool == CellType.BasePoint)
            {
                ClearCellType(CellType.BasePoint);
                _currentLevel.basePoint = new Vector2Int(x, y);
            }

            CellType oldType = _currentLevel.GetCell(x, y);
            _currentLevel.SetCell(x, y, _currentTool);

            // 路径点管理
            var pos = new Vector2Int(x, y);
            if (_currentTool == CellType.Path)
            {
                if (!_currentLevel.pathPoints.Contains(pos))
                {
                    _currentLevel.pathPoints.Add(pos);
                }
            }
            else
            {
                _currentLevel.pathPoints.Remove(pos);
            }
        }

        /// <summary>清除指定类型的所有网格</summary>
        private void ClearCellType(CellType type)
        {
            for (int i = 0; i < _currentLevel.gridData.Length; i++)
            {
                if (_currentLevel.gridData[i] == (int)type)
                {
                    _currentLevel.gridData[i] = (int)CellType.Empty;
                }
            }
        }

        /// <summary>获取网格颜色</summary>
        private Color GetCellColor(CellType type)
        {
            switch (type)
            {
                case CellType.Path: return ColorPath;
                case CellType.TowerSlot: return ColorTowerSlot;
                case CellType.Obstacle: return ColorObstacle;
                case CellType.SpawnPoint: return ColorSpawn;
                case CellType.BasePoint: return ColorBase;
                default: return ColorEmpty;
            }
        }

        /// <summary>创建新关卡</summary>
        private void CreateNewLevel()
        {
            _currentLevel = new LevelEditorData
            {
                levelId = $"ch{_newChapter}_lv{_newLevelIndex}",
                chapter = _newChapter,
                levelIndex = _newLevelIndex,
                width = _newWidth,
                height = _newHeight,
                description = $"第{_newChapter}章 第{_newLevelIndex}关"
            };
            _currentLevel.InitGrid();
        }

        /// <summary>保存关卡</summary>
        private void SaveLevel()
        {
            if (_currentLevel == null) return;

            if (!Directory.Exists(SavePath))
            {
                Directory.CreateDirectory(SavePath);
            }

            string fileName = $"{_currentLevel.levelId}.json";
            string filePath = SavePath + fileName;
            string json = JsonUtility.ToJson(_currentLevel, true);
            File.WriteAllText(filePath, json);

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("保存成功", $"关卡数据已保存到:\n{filePath}", "确定");
        }

        /// <summary>加载关卡（文件选择器）</summary>
        private void LoadLevel()
        {
            string path = EditorUtility.OpenFilePanel("选择关卡文件", SavePath, "json");
            if (!string.IsNullOrEmpty(path))
            {
                LoadLevelFromFile(path);
            }
        }

        /// <summary>从文件加载关卡</summary>
        private void LoadLevelFromFile(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                _currentLevel = JsonUtility.FromJson<LevelEditorData>(json);
                Repaint();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("加载失败", e.Message, "确定");
            }
        }

        /// <summary>验证关卡数据</summary>
        private void ValidateLevel()
        {
            var errors = new List<string>();

            // 检查出生点
            if (_currentLevel.GetCell(_currentLevel.spawnPoint.x, _currentLevel.spawnPoint.y) != CellType.SpawnPoint)
            {
                errors.Add("❌ 缺少出生点");
            }

            // 检查基地
            if (_currentLevel.GetCell(_currentLevel.basePoint.x, _currentLevel.basePoint.y) != CellType.BasePoint)
            {
                errors.Add("❌ 缺少基地位置");
            }

            // 检查路径连通性
            if (_currentLevel.pathPoints.Count < 2)
            {
                errors.Add("❌ 路径点不足（至少需要2个）");
            }

            // 检查塔位数量
            int towerSlotCount = 0;
            for (int i = 0; i < _currentLevel.gridData.Length; i++)
            {
                if (_currentLevel.gridData[i] == (int)CellType.TowerSlot) towerSlotCount++;
            }
            if (towerSlotCount < 3)
            {
                errors.Add($"⚠️ 塔位数量过少（当前{towerSlotCount}个，建议至少3个）");
            }

            if (errors.Count == 0)
            {
                EditorUtility.DisplayDialog("验证通过", "✅ 关卡数据验证通过！\n" +
                    $"路径点: {_currentLevel.pathPoints.Count}\n" +
                    $"塔位: {towerSlotCount}", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("验证发现问题",
                    string.Join("\n", errors), "确定");
            }
        }
    }
}

#endif
