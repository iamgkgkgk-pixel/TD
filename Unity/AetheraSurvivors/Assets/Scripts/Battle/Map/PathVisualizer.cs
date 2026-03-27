// ============================================================
// 文件名：PathVisualizer.cs
// 功能描述：路径可视化 — 怪物行进路线预览、放塔阻断检查、路径高亮
// 创建时间：2026-03-25
// 所属模块：Battle/Map
// 对应交互：阶段三 #118
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;


namespace AetheraSurvivors.Battle.Map
{
    /// <summary>
    /// 路径可视化系统
    /// 
    /// 功能：
    /// 1. 显示怪物行进路线（黄色路径线）
    /// 2. 放塔预览时显示路径是否被阻断（绿色=安全 / 红色=阻断）
    /// 3. 可选显示路径上的方向箭头
    /// </summary>
    public class PathVisualizer : MonoSingleton<PathVisualizer>
    {
        // ========== 配置参数 ==========

        [Header("路径线配置")]
        [SerializeField] private float _lineWidth = 0.025f;
        [SerializeField] private float _lineZOffset = -0.1f;
        [SerializeField] private Color _normalPathColor = new Color(1f, 0.95f, 0.6f, 0.2f);
        [SerializeField] private Color _blockedPathColor = new Color(1f, 0.2f, 0.2f, 0.6f);
        [SerializeField] private Color _safePathColor = new Color(0.3f, 1f, 0.3f, 0.6f);



        // ========== 运行时数据 ==========

        /// <summary>主路径LineRenderer</summary>
        private LineRenderer _mainPathLine;

        /// <summary>预览路径LineRenderer</summary>
        private LineRenderer _previewPathLine;

        /// <summary>当前显示的路径点</summary>
        private List<Vector3> _currentPathPoints = new List<Vector3>();

        /// <summary>是否显示路径</summary>
        private bool _isPathVisible = true;

        /// <summary>是否处于放塔预览模式</summary>
        private bool _isPreviewMode = false;

        // ========== 公共属性 ==========

        /// <summary>路径是否可见</summary>
        public bool IsPathVisible
        {
            get => _isPathVisible;
            set
            {
                _isPathVisible = value;
                if (_mainPathLine != null)
                    _mainPathLine.enabled = value;
            }
        }

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            CreateLineRenderers();
            Logger.I("PathVisualizer", "路径可视化系统初始化");
        }

        protected override void OnDispose()
        {
            Logger.I("PathVisualizer", "路径可视化系统已销毁");
        }

        // ========== 核心方法 ==========

        /// <summary>
        /// 显示主路径（怪物行进路线）
        /// </summary>
        /// <param name="worldPoints">世界坐标路径点列表</param>
        public void ShowMainPath(List<Vector3> worldPoints)
        {
            if (worldPoints == null || worldPoints.Count < 2)
            {
                HideMainPath();
                return;
            }

            _currentPathPoints.Clear();
            _currentPathPoints.AddRange(worldPoints);

            SetLinePositions(_mainPathLine, worldPoints, _normalPathColor);
            _mainPathLine.enabled = _isPathVisible;
        }

        /// <summary>
        /// 使用GridSystem的预设路径显示主路径
        /// </summary>
        public void ShowMainPathFromGrid()
        {
            var grid = GridSystem.Instance;
            if (grid == null || !grid.IsMapLoaded) return;

            var worldPath = grid.GetPathWorldPositions();
            ShowMainPath(worldPath);
        }

        /// <summary>
        /// 隐藏主路径
        /// </summary>
        public void HideMainPath()
        {
            if (_mainPathLine != null)
            {
                _mainPathLine.positionCount = 0;
                _mainPathLine.enabled = false;
            }
            _currentPathPoints.Clear();
        }

        /// <summary>
        /// 开始放塔预览模式 — 检查路径是否被阻断
        /// </summary>
        /// <param name="towerGridPos">预计放塔的网格坐标</param>
        /// <returns>true=路径安全（未阻断），false=路径被阻断</returns>
        public bool ShowPlacementPreview(Vector2Int towerGridPos)
        {
            _isPreviewMode = true;

            var grid = GridSystem.Instance;
            if (grid == null || !grid.IsMapLoaded)
            {
                EndPlacementPreview();
                return false;
            }

            // 检查该位置是否可放塔
            if (!grid.CanPlaceTower(towerGridPos))
            {
                // 不可放塔位，显示红色
                SetLinePositions(_previewPathLine, _currentPathPoints, _blockedPathColor);
                _previewPathLine.enabled = true;
                _mainPathLine.enabled = false;
                return false;
            }

            // 模拟放塔后检查路径是否连通
            // 使用Pathfinding检查出生点到基地是否仍可达
            bool pathSafe = Pathfinding.Instance.IsReachable(grid.SpawnPoint, grid.BasePoint);

            if (pathSafe)
            {
                // 路径安全，显示绿色
                SetLinePositions(_previewPathLine, _currentPathPoints, _safePathColor);
            }
            else
            {
                // 路径被阻断，显示红色
                SetLinePositions(_previewPathLine, _currentPathPoints, _blockedPathColor);
            }

            _previewPathLine.enabled = true;
            _mainPathLine.enabled = false;

            return pathSafe;
        }

        /// <summary>
        /// 结束放塔预览模式
        /// </summary>
        public void EndPlacementPreview()
        {
            _isPreviewMode = false;

            if (_previewPathLine != null)
            {
                _previewPathLine.positionCount = 0;
                _previewPathLine.enabled = false;
            }

            if (_mainPathLine != null)
            {
                _mainPathLine.enabled = _isPathVisible;
            }
        }

        /// <summary>
        /// 刷新路径显示（路径变化后调用）
        /// </summary>
        public void RefreshPath()
        {
            ShowMainPathFromGrid();
        }

        /// <summary>
        /// 清除所有路径显示
        /// </summary>
        public void ClearAll()
        {
            HideMainPath();
            EndPlacementPreview();
        }

        // ========== 内部方法 ==========

        /// <summary>创建LineRenderer组件</summary>
        private void CreateLineRenderers()
        {
            // 主路径线
            var mainObj = new GameObject("MainPathLine");
            mainObj.transform.SetParent(transform);
            _mainPathLine = mainObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(_mainPathLine, _normalPathColor);
            _mainPathLine.enabled = false;

            // 预览路径线
            var previewObj = new GameObject("PreviewPathLine");
            previewObj.transform.SetParent(transform);
            _previewPathLine = previewObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(_previewPathLine, _safePathColor);
            _previewPathLine.enabled = false;
        }

        /// <summary>配置LineRenderer的通用属性</summary>
        private void ConfigureLineRenderer(LineRenderer lr, Color color)
        {
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startWidth = _lineWidth;
            lr.endWidth = _lineWidth;
            lr.startColor = color;
            lr.endColor = color;
            lr.useWorldSpace = true;
            lr.sortingOrder = 10;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
        }

        /// <summary>设置LineRenderer的路径点和颜色</summary>
        private void SetLinePositions(LineRenderer lr, List<Vector3> points, Color color)
        {
            if (lr == null || points == null || points.Count == 0)
            {
                if (lr != null) lr.positionCount = 0;
                return;
            }

            lr.positionCount = points.Count;
            lr.startColor = color;
            lr.endColor = color;

            for (int i = 0; i < points.Count; i++)
            {
                var pos = points[i];
                pos.z = _lineZOffset; // 确保路径线在地图上方
                lr.SetPosition(i, pos);
            }
        }
    }
}
