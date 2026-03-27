// ============================================================
// 文件名：GridSystem.cs
// 功能描述：地图网格系统 — 塔防战斗场景的空间管理核心
//          管理方格网格数据、网格坐标与世界坐标转换、
//          可放塔位/路径/障碍物标记、动态障碍物更新
// 创建时间：2026-03-25
// 所属模块：Battle/Map
// 对应交互：阶段三 #116
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;


namespace AetheraSurvivors.Battle.Map
{
    // ====================================================================
    // 枚举与数据定义
    // ====================================================================

    /// <summary>
    /// 网格单元类型（运行时版本）
    /// 与Editor中的CellType一一对应，但运行时独立定义避免依赖Editor代码
    /// </summary>
    public enum GridCellType
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
    /// 网格单元数据
    /// </summary>
    public struct GridCell
    {
        /// <summary>网格坐标</summary>
        public Vector2Int Coord;

        /// <summary>单元类型</summary>
        public GridCellType Type;

        /// <summary>是否已放置塔（TowerSlot被占用）</summary>
        public bool HasTower;

        /// <summary>放置的塔ID（0=无塔）</summary>
        public int TowerId;

        /// <summary>是否可行走（路径/出生点/基地都可行走）</summary>
        public bool IsWalkable => Type == GridCellType.Path
                               || Type == GridCellType.SpawnPoint
                               || Type == GridCellType.BasePoint;

        /// <summary>是否可放置塔（TowerSlot且未放塔）</summary>
        public bool CanPlaceTower => Type == GridCellType.TowerSlot && !HasTower;
    }

    /// <summary>
    /// 关卡地图配置数据（运行时加载，与LevelEditorData格式兼容）
    /// </summary>
    [Serializable]
    public class LevelMapData
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
        /// <summary>网格数据（一维数组，行优先：index = y * width + x）</summary>
        public int[] gridData;
        /// <summary>怪物路径点列表（有序，从出生点到基地）</summary>
        public List<Vector2Int> pathPoints = new List<Vector2Int>();
        /// <summary>出生点坐标</summary>
        public Vector2Int spawnPoint;
        /// <summary>基地坐标</summary>
        public Vector2Int basePoint;
        /// <summary>关卡描述</summary>
        public string description;
    }

    // ====================================================================
    // 网格事件定义
    // ====================================================================

    /// <summary>塔放置事件</summary>
    public struct TowerPlacedEvent : IEvent
    {
        public Vector2Int GridPos;
        public int TowerId;
    }

    /// <summary>塔移除事件</summary>
    public struct TowerRemovedEvent : IEvent
    {
        public Vector2Int GridPos;
        public int TowerId;
    }

    /// <summary>路径需要重新计算事件</summary>
    public struct PathRecalculateRequestEvent : IEvent
    {
    }

    // ====================================================================
    // GridSystem 核心类
    // ====================================================================

    /// <summary>
    /// 地图网格系统 — 战斗场景空间管理核心
    /// 
    /// 职责：
    /// 1. 管理方格网格数据（从关卡配置加载）
    /// 2. 提供网格坐标 ↔ 世界坐标的双向转换
    /// 3. 查询网格状态（可放塔/可行走/已占用）
    /// 4. 管理塔的放置/移除对网格状态的影响
    /// 5. 提供邻居查询（供A*寻路使用）
    /// 
    /// 坐标约定：
    ///   网格坐标：(0,0) 在左下角，x向右，y向上
    ///   世界坐标：网格中心 = (x + 0.5) * cellSize + offset
    ///   
    /// 使用示例：
    ///   GridSystem.Instance.LoadMap(levelMapData);
    ///   Vector3 worldPos = GridSystem.Instance.GridToWorld(new Vector2Int(3, 5));
    ///   bool canPlace = GridSystem.Instance.CanPlaceTower(new Vector2Int(3, 5));
    /// </summary>
    public class GridSystem : MonoSingleton<GridSystem>
    {
        // ========== 配置参数 ==========

        /// <summary>每个网格单元的世界尺寸（单位：Unity单位）</summary>
        [Header("网格配置")]
        [SerializeField] private float _cellSize = 1.0f;

        /// <summary>地图原点偏移（世界坐标，左下角第一个格子的中心位置）</summary>
        [SerializeField] private Vector3 _mapOrigin = Vector3.zero;

        // ========== 运行时数据 ==========

        /// <summary>地图宽度（格子数）</summary>
        private int _width;

        /// <summary>地图高度（格子数）</summary>
        private int _height;

        /// <summary>网格数据（二维数组扁平为一维，index = y * width + x）</summary>
        private GridCell[] _cells;

        /// <summary>出生点坐标</summary>
        private Vector2Int _spawnPoint;

        /// <summary>基地坐标</summary>
        private Vector2Int _basePoint;

        /// <summary>预设路径点（从关卡配置加载的有序路径）</summary>
        private List<Vector2Int> _pathPoints = new List<Vector2Int>();

        /// <summary>是否已加载地图</summary>
        private bool _isMapLoaded = false;

        /// <summary>当前地图中的塔位总数</summary>
        private int _totalTowerSlots = 0;

        /// <summary>当前已放置塔的数量</summary>
        private int _placedTowerCount = 0;

        // ========== 公共属性 ==========

        /// <summary>地图宽度（格子数）</summary>
        public int Width => _width;

        /// <summary>地图高度（格子数）</summary>
        public int Height => _height;

        /// <summary>每格子世界尺寸</summary>
        public float CellSize => _cellSize;

        /// <summary>地图原点</summary>
        public Vector3 MapOrigin => _mapOrigin;

        /// <summary>是否已加载地图</summary>
        public bool IsMapLoaded => _isMapLoaded;

        /// <summary>出生点网格坐标</summary>
        public Vector2Int SpawnPoint => _spawnPoint;

        /// <summary>基地网格坐标</summary>
        public Vector2Int BasePoint => _basePoint;

        /// <summary>预设路径点列表（有序）</summary>
        public IReadOnlyList<Vector2Int> PathPoints => _pathPoints;

        /// <summary>塔位总数</summary>
        public int TotalTowerSlots => _totalTowerSlots;

        /// <summary>已放置塔数量</summary>
        public int PlacedTowerCount => _placedTowerCount;

        /// <summary>剩余可放塔数量</summary>
        public int AvailableTowerSlots => _totalTowerSlots - _placedTowerCount;

        // ========== 静态常量：邻居方向 ==========

        /// <summary>四方向邻居偏移（上下左右）</summary>
        public static readonly Vector2Int[] FourDirections = new Vector2Int[]
        {
            new Vector2Int(0, 1),   // 上
            new Vector2Int(0, -1),  // 下
            new Vector2Int(-1, 0),  // 左
            new Vector2Int(1, 0)    // 右
        };

        /// <summary>八方向邻居偏移（含对角线）</summary>
        public static readonly Vector2Int[] EightDirections = new Vector2Int[]
        {
            new Vector2Int(0, 1),    // 上
            new Vector2Int(0, -1),   // 下
            new Vector2Int(-1, 0),   // 左
            new Vector2Int(1, 0),    // 右
            new Vector2Int(-1, 1),   // 左上
            new Vector2Int(1, 1),    // 右上
            new Vector2Int(-1, -1),  // 左下
            new Vector2Int(1, -1)    // 右下
        };

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            Logger.I("GridSystem", "地图网格系统初始化");
        }

        protected override void OnDispose()
        {
            _cells = null;
            _pathPoints.Clear();
            _isMapLoaded = false;
            Logger.I("GridSystem", "地图网格系统已销毁");
        }

        // ========== 核心方法：地图加载 ==========

        /// <summary>
        /// 从关卡配置数据加载地图
        /// </summary>
        /// <param name="mapData">关卡地图配置</param>
        /// <param name="origin">地图原点世界坐标（可选，默认(0,0,0)）</param>
        public void LoadMap(LevelMapData mapData, Vector3? origin = null)
        {
            if (mapData == null)
            {
                Logger.E("GridSystem", "LoadMap失败：mapData为null");
                return;
            }

            if (mapData.gridData == null || mapData.gridData.Length != mapData.width * mapData.height)
            {
                Logger.E("GridSystem", "LoadMap失败：gridData数据异常, 期望长度={0}, 实际={1}",
                    mapData.width * mapData.height, mapData.gridData?.Length ?? 0);
                return;
            }

            _width = mapData.width;
            _height = mapData.height;
            _spawnPoint = mapData.spawnPoint;
            _basePoint = mapData.basePoint;
            _mapOrigin = origin ?? Vector3.zero;
            _totalTowerSlots = 0;
            _placedTowerCount = 0;

            // 初始化网格数据
            _cells = new GridCell[_width * _height];
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int index = y * _width + x;
                    int typeValue = mapData.gridData[index];

                    _cells[index] = new GridCell
                    {
                        Coord = new Vector2Int(x, y),
                        Type = (GridCellType)typeValue,
                        HasTower = false,
                        TowerId = 0
                    };

                    if ((GridCellType)typeValue == GridCellType.TowerSlot)
                    {
                        _totalTowerSlots++;
                    }
                }
            }

            // 加载路径点
            _pathPoints.Clear();
            if (mapData.pathPoints != null)
            {
                _pathPoints.AddRange(mapData.pathPoints);
            }

            _isMapLoaded = true;

            Logger.I("GridSystem", "✅ 地图加载完成: {0}×{1}, 塔位={2}, 路径点={3}, 出生点=({4},{5}), 基地=({6},{7})",
                _width, _height, _totalTowerSlots, _pathPoints.Count,
                _spawnPoint.x, _spawnPoint.y, _basePoint.x, _basePoint.y);
        }

        /// <summary>
        /// 卸载当前地图（战斗结束时调用）
        /// </summary>
        public void UnloadMap()
        {
            _cells = null;
            _pathPoints.Clear();
            _isMapLoaded = false;
            _width = 0;
            _height = 0;
            _totalTowerSlots = 0;
            _placedTowerCount = 0;

            Logger.I("GridSystem", "地图已卸载");
        }

        // ========== 核心方法：坐标转换 ==========

        /// <summary>
        /// 网格坐标 → 世界坐标（返回格子中心点）
        /// </summary>
        /// <param name="gridPos">网格坐标</param>
        /// <returns>世界坐标（格子中心）</returns>
        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            float worldX = _mapOrigin.x + (gridPos.x + 0.5f) * _cellSize;
            float worldY = _mapOrigin.y + (gridPos.y + 0.5f) * _cellSize;
            return new Vector3(worldX, worldY, 0f);
        }

        /// <summary>
        /// 世界坐标 → 网格坐标
        /// </summary>
        /// <param name="worldPos">世界坐标</param>
        /// <returns>对应的网格坐标（可能越界，需用IsValidCoord检查）</returns>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            int gridX = Mathf.FloorToInt((worldPos.x - _mapOrigin.x) / _cellSize);
            int gridY = Mathf.FloorToInt((worldPos.y - _mapOrigin.y) / _cellSize);
            return new Vector2Int(gridX, gridY);
        }

        /// <summary>
        /// 世界坐标 → 网格坐标（带吸附：返回最近的有效格子中心）
        /// </summary>
        /// <param name="worldPos">世界坐标</param>
        /// <param name="snappedWorldPos">吸附后的世界坐标（格子中心）</param>
        /// <returns>网格坐标</returns>
        public Vector2Int WorldToGridSnapped(Vector3 worldPos, out Vector3 snappedWorldPos)
        {
            var gridPos = WorldToGrid(worldPos);
            gridPos.x = Mathf.Clamp(gridPos.x, 0, _width - 1);
            gridPos.y = Mathf.Clamp(gridPos.y, 0, _height - 1);
            snappedWorldPos = GridToWorld(gridPos);
            return gridPos;
        }

        // ========== 核心方法：网格查询 ==========

        /// <summary>
        /// 检查坐标是否在地图范围内
        /// </summary>
        public bool IsValidCoord(Vector2Int coord)
        {
            return coord.x >= 0 && coord.x < _width && coord.y >= 0 && coord.y < _height;
        }

        /// <summary>
        /// 检查坐标是否在地图范围内
        /// </summary>
        public bool IsValidCoord(int x, int y)
        {
            return x >= 0 && x < _width && y >= 0 && y < _height;
        }

        /// <summary>
        /// 获取指定坐标的网格单元数据
        /// </summary>
        /// <param name="coord">网格坐标</param>
        /// <returns>网格单元数据（越界返回Empty类型的默认值）</returns>
        public GridCell GetCell(Vector2Int coord)
        {
            if (!IsValidCoord(coord))
            {
                return new GridCell { Coord = coord, Type = GridCellType.Empty };
            }
            return _cells[coord.y * _width + coord.x];
        }

        /// <summary>
        /// 获取指定坐标的网格单元数据
        /// </summary>
        public GridCell GetCell(int x, int y)
        {
            if (!IsValidCoord(x, y))
            {
                return new GridCell { Coord = new Vector2Int(x, y), Type = GridCellType.Empty };
            }
            return _cells[y * _width + x];
        }

        /// <summary>
        /// 获取指定坐标的单元类型
        /// </summary>
        public GridCellType GetCellType(Vector2Int coord)
        {
            return GetCell(coord).Type;
        }

        /// <summary>
        /// 检查指定坐标是否可行走
        /// </summary>
        public bool IsWalkable(Vector2Int coord)
        {
            return GetCell(coord).IsWalkable;
        }

        /// <summary>
        /// 检查指定坐标是否可放置塔
        /// </summary>
        public bool CanPlaceTower(Vector2Int coord)
        {
            return GetCell(coord).CanPlaceTower;
        }

        /// <summary>
        /// 获取四方向邻居列表（只返回有效坐标内的邻居）
        /// </summary>
        /// <param name="coord">中心坐标</param>
        /// <param name="neighborsBuffer">输出缓冲区（避免GC分配）</param>
        /// <returns>邻居数量</returns>
        public int GetNeighbors4(Vector2Int coord, Vector2Int[] neighborsBuffer)
        {
            int count = 0;
            for (int i = 0; i < FourDirections.Length; i++)
            {
                var neighbor = coord + FourDirections[i];
                if (IsValidCoord(neighbor) && count < neighborsBuffer.Length)
                {
                    neighborsBuffer[count++] = neighbor;
                }
            }
            return count;
        }

        /// <summary>
        /// 获取四方向可行走邻居列表
        /// </summary>
        /// <param name="coord">中心坐标</param>
        /// <param name="neighborsBuffer">输出缓冲区</param>
        /// <returns>可行走邻居数量</returns>
        public int GetWalkableNeighbors4(Vector2Int coord, Vector2Int[] neighborsBuffer)
        {
            int count = 0;
            for (int i = 0; i < FourDirections.Length; i++)
            {
                var neighbor = coord + FourDirections[i];
                if (IsValidCoord(neighbor) && IsWalkable(neighbor) && count < neighborsBuffer.Length)
                {
                    neighborsBuffer[count++] = neighbor;
                }
            }
            return count;
        }

        /// <summary>
        /// 获取所有指定类型的格子坐标
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <returns>坐标列表</returns>
        public List<Vector2Int> GetCellsByType(GridCellType type)
        {
            var result = new List<Vector2Int>();
            if (_cells == null) return result;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (_cells[y * _width + x].Type == type)
                    {
                        result.Add(new Vector2Int(x, y));
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 获取所有可放塔的空闲位置
        /// </summary>
        public List<Vector2Int> GetAvailableTowerSlots()
        {
            var result = new List<Vector2Int>();
            if (_cells == null) return result;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var cell = _cells[y * _width + x];
                    if (cell.CanPlaceTower)
                    {
                        result.Add(new Vector2Int(x, y));
                    }
                }
            }
            return result;
        }

        // ========== 核心方法：塔的放置/移除 ==========

        /// <summary>
        /// 在指定位置放置塔
        /// </summary>
        /// <param name="coord">网格坐标</param>
        /// <param name="towerId">塔的实例ID</param>
        /// <returns>是否放置成功</returns>
        public bool PlaceTower(Vector2Int coord, int towerId)
        {
            if (!IsValidCoord(coord))
            {
                Logger.W("GridSystem", "PlaceTower失败：坐标越界({0},{1})", coord.x, coord.y);
                return false;
            }

            int index = coord.y * _width + coord.x;
            ref var cell = ref _cells[index];

            if (!cell.CanPlaceTower)
            {
                Logger.W("GridSystem", "PlaceTower失败：({0},{1})不可放塔 (类型={2}, 已有塔={3})",
                    coord.x, coord.y, cell.Type, cell.HasTower);
                return false;
            }

            cell.HasTower = true;
            cell.TowerId = towerId;
            _placedTowerCount++;

            // 发布事件
            EventBus.Instance.Publish(new TowerPlacedEvent
            {
                GridPos = coord,
                TowerId = towerId
            });

            Logger.D("GridSystem", "塔已放置: ({0},{1}) ID={2}", coord.x, coord.y, towerId);
            return true;
        }

        /// <summary>
        /// 移除指定位置的塔
        /// </summary>
        /// <param name="coord">网格坐标</param>
        /// <returns>被移除的塔ID（0=该位置无塔）</returns>
        public int RemoveTower(Vector2Int coord)
        {
            if (!IsValidCoord(coord))
            {
                Logger.W("GridSystem", "RemoveTower失败：坐标越界({0},{1})", coord.x, coord.y);
                return 0;
            }

            int index = coord.y * _width + coord.x;
            ref var cell = ref _cells[index];

            if (!cell.HasTower)
            {
                Logger.W("GridSystem", "RemoveTower失败：({0},{1})没有塔", coord.x, coord.y);
                return 0;
            }

            int removedTowerId = cell.TowerId;
            cell.HasTower = false;
            cell.TowerId = 0;
            _placedTowerCount--;

            // 发布事件
            EventBus.Instance.Publish(new TowerRemovedEvent
            {
                GridPos = coord,
                TowerId = removedTowerId
            });

            Logger.D("GridSystem", "塔已移除: ({0},{1}) ID={2}", coord.x, coord.y, removedTowerId);
            return removedTowerId;
        }

        // ========== 工具方法 ==========

        /// <summary>
        /// 计算两个网格坐标之间的曼哈顿距离
        /// </summary>
        public static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        /// <summary>
        /// 计算两个网格坐标之间的欧氏距离
        /// </summary>
        public static float EuclideanDistance(Vector2Int a, Vector2Int b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 获取从出生点到基地的预设路径世界坐标列表
        /// </summary>
        /// <returns>有序的世界坐标列表</returns>
        public List<Vector3> GetPathWorldPositions()
        {
            var worldPositions = new List<Vector3>(_pathPoints.Count);
            for (int i = 0; i < _pathPoints.Count; i++)
            {
                worldPositions.Add(GridToWorld(_pathPoints[i]));
            }
            return worldPositions;
        }

        /// <summary>
        /// 获取地图的世界空间包围盒
        /// </summary>
        public Bounds GetMapBounds()
        {
            var center = new Vector3(
                _mapOrigin.x + _width * _cellSize * 0.5f,
                _mapOrigin.y + _height * _cellSize * 0.5f,
                0f
            );
            var size = new Vector3(_width * _cellSize, _height * _cellSize, 0f);
            return new Bounds(center, size);
        }

        /// <summary>
        /// 获取调试信息字符串
        /// </summary>
        public string GetDebugInfo()
        {
            if (!_isMapLoaded) return "地图未加载";
            return $"地图:{_width}×{_height} 塔位:{_placedTowerCount}/{_totalTowerSlots} 路径:{_pathPoints.Count}点";
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器中绘制网格辅助线（Scene视图）
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!_isMapLoaded || _cells == null) return;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var cell = _cells[y * _width + x];
                    Vector3 center = GridToWorld(new Vector2Int(x, y));
                    Vector3 size = new Vector3(_cellSize * 0.9f, _cellSize * 0.9f, 0.01f);

                    // 按类型设置颜色
                    switch (cell.Type)
                    {
                        case GridCellType.Path:
                            Gizmos.color = new Color(0.9f, 0.8f, 0.5f, 0.5f);
                            break;
                        case GridCellType.TowerSlot:
                            Gizmos.color = cell.HasTower
                                ? new Color(1f, 0.5f, 0f, 0.5f) // 已放塔：橙色
                                : new Color(0.4f, 0.7f, 0.4f, 0.5f); // 空塔位：绿色
                            break;
                        case GridCellType.Obstacle:
                            Gizmos.color = new Color(0.5f, 0.3f, 0.2f, 0.5f);
                            break;
                        case GridCellType.SpawnPoint:
                            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.7f);
                            break;
                        case GridCellType.BasePoint:
                            Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.7f);
                            break;
                        default:
                            Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.2f);
                            break;
                    }

                    Gizmos.DrawCube(center, size);

                    // 绘制格子边框
                    Gizmos.color = new Color(1f, 1f, 1f, 0.1f);
                    Gizmos.DrawWireCube(center, size);
                }
            }

            // 绘制路径连线
            if (_pathPoints.Count > 1)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < _pathPoints.Count - 1; i++)
                {
                    Vector3 from = GridToWorld(_pathPoints[i]);
                    Vector3 to = GridToWorld(_pathPoints[i + 1]);
                    Gizmos.DrawLine(from, to);
                }
            }
        }
#endif
    }
}
