// ============================================================
// 文件名：Pathfinding.cs
// 功能描述：A*寻路算法 — 适配塔防地图
//          支持多起点多终点、动态障碍（放塔后重新寻路）、
//          路径缓存、性能优化（二叉堆优先队列）
// 创建时间：2026-03-25
// 所属模块：Battle/Map
// 对应交互：阶段三 #117
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Map

{
    // ====================================================================
    // 寻路节点
    // ====================================================================

    /// <summary>
    /// A*寻路节点
    /// </summary>
    internal class PathNode : IComparable<PathNode>
    {
        /// <summary>网格坐标</summary>
        public Vector2Int Coord;

        /// <summary>从起点到该节点的实际代价（G值）</summary>
        public float G;

        /// <summary>从该节点到终点的估算代价（H值/启发值）</summary>
        public float H;

        /// <summary>总代价 F = G + H</summary>
        public float F => G + H;

        /// <summary>父节点（用于回溯路径）</summary>
        public PathNode Parent;

        /// <summary>是否在开放列表中</summary>
        public bool InOpenList;

        /// <summary>是否在关闭列表中</summary>
        public bool InClosedList;

        /// <summary>比较器（用于优先队列排序）</summary>
        public int CompareTo(PathNode other)
        {
            int result = F.CompareTo(other.F);
            if (result == 0)
            {
                // F值相同时，优先选择H值小的（更接近终点）
                result = H.CompareTo(other.H);
            }
            return result;
        }

        /// <summary>重置节点数据（用于对象池复用）</summary>
        public void Reset()
        {
            G = 0;
            H = 0;
            Parent = null;
            InOpenList = false;
            InClosedList = false;
        }
    }

    // ====================================================================
    // 二叉堆优先队列（高性能版本，避免SortedSet的GC）
    // ====================================================================

    /// <summary>
    /// 最小堆优先队列 — 用于A*的开放列表
    /// 比 SortedList/SortedSet 更高效，O(log n) 的插入和弹出
    /// </summary>
    internal class MinHeap<T> where T : IComparable<T>
    {
        private readonly List<T> _data;

        public int Count => _data.Count;

        public MinHeap(int capacity = 64)
        {
            _data = new List<T>(capacity);
        }

        /// <summary>插入元素</summary>
        public void Push(T item)
        {
            _data.Add(item);
            BubbleUp(_data.Count - 1);
        }

        /// <summary>弹出最小元素</summary>
        public T Pop()
        {
            if (_data.Count == 0)
                throw new InvalidOperationException("堆为空");

            T min = _data[0];
            int last = _data.Count - 1;

            _data[0] = _data[last];
            _data.RemoveAt(last);

            if (_data.Count > 0)
            {
                BubbleDown(0);
            }

            return min;
        }

        /// <summary>查看最小元素（不移除）</summary>
        public T Peek()
        {
            if (_data.Count == 0)
                throw new InvalidOperationException("堆为空");
            return _data[0];
        }

        /// <summary>更新元素位置（当元素的优先级降低时调用）</summary>
        public void Update(T item)
        {
            int index = _data.IndexOf(item);
            if (index < 0) return;
            BubbleUp(index);
            BubbleDown(index);
        }

        /// <summary>清空堆</summary>
        public void Clear()
        {
            _data.Clear();
        }

        private void BubbleUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_data[index].CompareTo(_data[parent]) < 0)
                {
                    Swap(index, parent);
                    index = parent;
                }
                else
                {
                    break;
                }
            }
        }

        private void BubbleDown(int index)
        {
            int count = _data.Count;
            while (true)
            {
                int smallest = index;
                int left = 2 * index + 1;
                int right = 2 * index + 2;

                if (left < count && _data[left].CompareTo(_data[smallest]) < 0)
                    smallest = left;
                if (right < count && _data[right].CompareTo(_data[smallest]) < 0)
                    smallest = right;

                if (smallest != index)
                {
                    Swap(index, smallest);
                    index = smallest;
                }
                else
                {
                    break;
                }
            }
        }

        private void Swap(int a, int b)
        {
            T temp = _data[a];
            _data[a] = _data[b];
            _data[b] = temp;
        }
    }

    // ====================================================================
    // 寻路结果
    // ====================================================================

    /// <summary>
    /// 寻路结果
    /// </summary>
    public class PathResult
    {
        /// <summary>是否找到路径</summary>
        public bool Success;

        /// <summary>路径节点列表（网格坐标，从起点到终点有序）</summary>
        public List<Vector2Int> Path;

        /// <summary>路径总代价</summary>
        public float TotalCost;

        /// <summary>寻路耗时（毫秒）</summary>
        public float ElapsedMs;

        /// <summary>搜索的节点数量</summary>
        public int NodesSearched;

        /// <summary>获取路径对应的世界坐标列表</summary>
        public List<Vector3> GetWorldPath(GridSystem grid)
        {
            if (!Success || Path == null) return new List<Vector3>();

            var worldPath = new List<Vector3>(Path.Count);
            for (int i = 0; i < Path.Count; i++)
            {
                worldPath.Add(grid.GridToWorld(Path[i]));
            }
            return worldPath;
        }
    }

    // ====================================================================
    // Pathfinding 核心类
    // ====================================================================

    /// <summary>
    /// A*寻路系统 — 适配塔防地图
    /// 
    /// 特性：
    /// 1. 标准A*算法 + 二叉堆优先队列（高性能）
    /// 2. 支持多起点/多终点寻路
    /// 3. 支持动态障碍（放塔后重新寻路）
    /// 4. 路径缓存（相同起点终点且地图未变化时直接返回缓存）
    /// 5. 低GC设计：节点池复用、预分配容器
    /// 6. 可配置的启发函数（曼哈顿/欧氏/对角线）
    /// 
    /// 使用示例：
    ///   var result = Pathfinding.Instance.FindPath(startPos, endPos);
    ///   if (result.Success)
    ///   {
    ///       var worldPath = result.GetWorldPath(GridSystem.Instance);
    ///       // 让怪物沿worldPath移动
    ///   }
    /// </summary>
    public class Pathfinding : Singleton<Pathfinding>
    {
        // ========== 配置常量 ==========

        /// <summary>直线移动代价</summary>
        private const float CostStraight = 1.0f;

        /// <summary>对角线移动代价（√2 ≈ 1.414）</summary>
        private const float CostDiagonal = 1.414f;

        /// <summary>最大搜索节点数（防止死循环/超时）</summary>
        private const int MaxSearchNodes = 5000;

        /// <summary>缓存过期帧数（超过此帧数的缓存无效）</summary>
        private const int CacheExpireFrames = 1;

        // ========== 启发函数类型 ==========

        /// <summary>
        /// 启发函数类型
        /// </summary>
        public enum HeuristicType
        {
            /// <summary>曼哈顿距离（四方向移动时最优）</summary>
            Manhattan,
            /// <summary>欧氏距离</summary>
            Euclidean,
            /// <summary>对角线距离（八方向移动时最优）</summary>
            Diagonal
        }

        // ========== 私有字段 ==========

        /// <summary>节点池（避免每次寻路都new对象）</summary>
        private PathNode[,] _nodeGrid;

        /// <summary>节点池宽度</summary>
        private int _nodeGridWidth;

        /// <summary>节点池高度</summary>
        private int _nodeGridHeight;

        /// <summary>开放列表（最小堆）</summary>
        private readonly MinHeap<PathNode> _openList = new MinHeap<PathNode>(256);

        /// <summary>路径缓存</summary>
        private readonly Dictionary<long, CachedPath> _pathCache = new Dictionary<long, CachedPath>();

        /// <summary>缓存版本号（地图变化时递增，使所有缓存失效）</summary>
        private int _cacheVersion = 0;

        /// <summary>当前使用的启发函数</summary>
        private HeuristicType _heuristicType = HeuristicType.Manhattan;

        /// <summary>是否允许对角线移动</summary>
        private bool _allowDiagonal = false;

        /// <summary>邻居缓冲区（避免每帧分配）</summary>
        private readonly Vector2Int[] _neighborBuffer = new Vector2Int[8];

        // ========== 缓存数据结构 ==========

        private struct CachedPath
        {
            public List<Vector2Int> Path;
            public float TotalCost;
            public int Version;
            public int Frame;
        }

        // ========== 公共属性 ==========

        /// <summary>启发函数类型</summary>
        public HeuristicType Heuristic
        {
            get => _heuristicType;
            set => _heuristicType = value;
        }

        /// <summary>是否允许对角线移动（塔防一般不允许）</summary>
        public bool AllowDiagonal
        {
            get => _allowDiagonal;
            set => _allowDiagonal = value;
        }

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            Logger.I("Pathfinding", "A*寻路系统初始化");
        }

        protected override void OnDispose()
        {
            _nodeGrid = null;
            _pathCache.Clear();
            _openList.Clear();
            Logger.I("Pathfinding", "A*寻路系统已销毁");
        }

        // ========== 初始化 ==========

        /// <summary>
        /// 初始化寻路系统（地图加载后调用）
        /// </summary>
        /// <param name="width">地图宽度</param>
        /// <param name="height">地图高度</param>
        public void InitForMap(int width, int height)
        {
            _nodeGridWidth = width;
            _nodeGridHeight = height;
            _nodeGrid = new PathNode[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    _nodeGrid[x, y] = new PathNode { Coord = new Vector2Int(x, y) };
                }
            }

            InvalidateCache();
            Logger.I("Pathfinding", "寻路系统已为{0}×{1}地图初始化", width, height);
        }

        // ========== 核心方法：A*寻路 ==========

        /// <summary>
        /// A*寻路：从起点到终点
        /// </summary>
        /// <param name="start">起点网格坐标</param>
        /// <param name="end">终点网格坐标</param>
        /// <param name="useCache">是否使用缓存</param>
        /// <returns>寻路结果</returns>
        public PathResult FindPath(Vector2Int start, Vector2Int end, bool useCache = true)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = new PathResult { Success = false, Path = new List<Vector2Int>() };

            var grid = GridSystem.Instance;
            if (grid == null || !grid.IsMapLoaded)
            {
                Logger.W("Pathfinding", "寻路失败：地图未加载");
                return result;
            }

            // 基础检查
            if (!grid.IsValidCoord(start) || !grid.IsValidCoord(end))
            {
                Logger.W("Pathfinding", "寻路失败：坐标越界 start=({0},{1}) end=({2},{3})",
                    start.x, start.y, end.x, end.y);
                return result;
            }

            // 起点和终点必须可行走
            if (!grid.IsWalkable(start))
            {
                Logger.W("Pathfinding", "寻路失败：起点({0},{1})不可行走", start.x, start.y);
                return result;
            }

            if (!grid.IsWalkable(end))
            {
                Logger.W("Pathfinding", "寻路失败：终点({0},{1})不可行走", end.x, end.y);
                return result;
            }

            // 起点=终点
            if (start == end)
            {
                result.Success = true;
                result.Path.Add(start);
                result.TotalCost = 0;
                sw.Stop();
                result.ElapsedMs = (float)sw.Elapsed.TotalMilliseconds;
                return result;
            }

            // 查缓存
            if (useCache)
            {
                long cacheKey = GetCacheKey(start, end);
                if (_pathCache.TryGetValue(cacheKey, out var cached))
                {
                    if (cached.Version == _cacheVersion &&
                        (Time.frameCount - cached.Frame) <= CacheExpireFrames)
                    {
                        result.Success = true;
                        result.Path = new List<Vector2Int>(cached.Path);
                        result.TotalCost = cached.TotalCost;
                        sw.Stop();
                        result.ElapsedMs = (float)sw.Elapsed.TotalMilliseconds;
                        return result;
                    }
                }
            }

            // 执行A*
            result = DoAStar(start, end);
            sw.Stop();
            result.ElapsedMs = (float)sw.Elapsed.TotalMilliseconds;

            // 存缓存
            if (useCache && result.Success)
            {
                long cacheKey = GetCacheKey(start, end);
                _pathCache[cacheKey] = new CachedPath
                {
                    Path = new List<Vector2Int>(result.Path),
                    TotalCost = result.TotalCost,
                    Version = _cacheVersion,
                    Frame = Time.frameCount
                };
            }

            return result;
        }

        /// <summary>
        /// 多终点寻路：从起点到最近的终点
        /// </summary>
        /// <param name="start">起点</param>
        /// <param name="ends">多个候选终点</param>
        /// <returns>到最近终点的路径</returns>
        public PathResult FindPathToNearest(Vector2Int start, List<Vector2Int> ends)
        {
            if (ends == null || ends.Count == 0)
            {
                return new PathResult { Success = false, Path = new List<Vector2Int>() };
            }

            if (ends.Count == 1)
            {
                return FindPath(start, ends[0]);
            }

            // 对每个终点计算启发值，按距离排序尝试
            PathResult bestResult = null;

            // 先按曼哈顿距离排序候选终点
            ends.Sort((a, b) =>
                GridSystem.ManhattanDistance(start, a).CompareTo(
                    GridSystem.ManhattanDistance(start, b)));

            foreach (var end in ends)
            {
                var result = FindPath(start, end);
                if (result.Success)
                {
                    if (bestResult == null || result.TotalCost < bestResult.TotalCost)
                    {
                        bestResult = result;
                    }
                }
            }

            return bestResult ?? new PathResult { Success = false, Path = new List<Vector2Int>() };
        }

        /// <summary>
        /// 检查从起点到终点是否存在可达路径（不返回完整路径，仅判断连通性）
        /// 比 FindPath 更轻量
        /// </summary>
        public bool IsReachable(Vector2Int start, Vector2Int end)
        {
            var result = FindPath(start, end, useCache: true);
            return result.Success;
        }

        // ========== 缓存管理 ==========

        /// <summary>
        /// 使所有路径缓存失效（地图发生变化时调用）
        /// </summary>
        public void InvalidateCache()
        {
            _cacheVersion++;
            _pathCache.Clear();
            Logger.D("Pathfinding", "路径缓存已失效 (版本={0})", _cacheVersion);
        }

        /// <summary>
        /// 通知地图变化（塔放置/移除后调用）
        /// </summary>
        public void OnMapChanged()
        {
            InvalidateCache();
            // 发布路径重新计算请求事件
            EventBus.Instance.Publish(new PathRecalculateRequestEvent());
        }

        // ========== A*核心实现 ==========

        /// <summary>
        /// 执行A*算法
        /// </summary>
        private PathResult DoAStar(Vector2Int start, Vector2Int end)
        {
            var result = new PathResult
            {
                Success = false,
                Path = new List<Vector2Int>(),
                NodesSearched = 0
            };

            var grid = GridSystem.Instance;

            // 重置所有节点状态
            ResetNodes();

            // 初始化开放列表
            _openList.Clear();

            var startNode = GetNode(start);
            startNode.G = 0;
            startNode.H = CalculateHeuristic(start, end);
            startNode.InOpenList = true;
            _openList.Push(startNode);

            // A*主循环
            while (_openList.Count > 0)
            {
                // 取出F值最小的节点
                var current = _openList.Pop();
                current.InOpenList = false;
                current.InClosedList = true;
                result.NodesSearched++;

                // 安全限制：防止死循环
                if (result.NodesSearched > MaxSearchNodes)
                {
                    Logger.W("Pathfinding", "寻路搜索超过上限({0}节点)，终止", MaxSearchNodes);
                    return result;
                }

                // 到达终点
                if (current.Coord == end)
                {
                    result.Success = true;
                    result.TotalCost = current.G;
                    result.Path = ReconstructPath(current);
                    return result;
                }

                // 展开邻居
                int neighborCount = GetWalkableNeighbors(current.Coord, grid);

                for (int i = 0; i < neighborCount; i++)
                {
                    var neighborCoord = _neighborBuffer[i];
                    var neighbor = GetNode(neighborCoord);

                    // 跳过已在关闭列表中的节点
                    if (neighbor.InClosedList) continue;

                    // 计算从当前节点到邻居的移动代价
                    float moveCost = GetMoveCost(current.Coord, neighborCoord);
                    float tentativeG = current.G + moveCost;

                    if (!neighbor.InOpenList)
                    {
                        // 新发现的节点
                        neighbor.G = tentativeG;
                        neighbor.H = CalculateHeuristic(neighborCoord, end);
                        neighbor.Parent = current;
                        neighbor.InOpenList = true;
                        _openList.Push(neighbor);
                    }
                    else if (tentativeG < neighbor.G)
                    {
                        // 发现更短的路径到达此节点
                        neighbor.G = tentativeG;
                        neighbor.Parent = current;
                        _openList.Update(neighbor);
                    }
                }
            }

            // 开放列表为空，无路径
            Logger.D("Pathfinding", "无法找到路径: ({0},{1}) → ({2},{3}), 搜索了{4}个节点",
                start.x, start.y, end.x, end.y, result.NodesSearched);
            return result;
        }

        // ========== 内部辅助方法 ==========

        /// <summary>获取节点（从节点池）</summary>
        private PathNode GetNode(Vector2Int coord)
        {
            return _nodeGrid[coord.x, coord.y];
        }

        /// <summary>重置所有节点状态</summary>
        private void ResetNodes()
        {
            for (int x = 0; x < _nodeGridWidth; x++)
            {
                for (int y = 0; y < _nodeGridHeight; y++)
                {
                    _nodeGrid[x, y].Reset();
                }
            }
        }

        /// <summary>获取可行走邻居</summary>
        private int GetWalkableNeighbors(Vector2Int coord, GridSystem grid)
        {
            if (_allowDiagonal)
            {
                return GetWalkableNeighbors8(coord, grid);
            }
            else
            {
                return grid.GetWalkableNeighbors4(coord, _neighborBuffer);
            }
        }

        /// <summary>获取八方向可行走邻居（含对角线碰撞检查）</summary>
        private int GetWalkableNeighbors8(Vector2Int coord, GridSystem grid)
        {
            int count = 0;
            var dirs = GridSystem.EightDirections;

            for (int i = 0; i < dirs.Length; i++)
            {
                var neighbor = coord + dirs[i];

                if (!grid.IsValidCoord(neighbor) || !grid.IsWalkable(neighbor))
                    continue;

                // 对角线移动时，检查两个相邻的正交方向是否都可行走（防止穿墙）
                if (i >= 4) // 后4个是对角线方向
                {
                    var dx = new Vector2Int(dirs[i].x, 0);
                    var dy = new Vector2Int(0, dirs[i].y);

                    if (!grid.IsWalkable(coord + dx) || !grid.IsWalkable(coord + dy))
                        continue;
                }

                _neighborBuffer[count++] = neighbor;
            }

            return count;
        }

        /// <summary>计算两个相邻格子间的移动代价</summary>
        private float GetMoveCost(Vector2Int from, Vector2Int to)
        {
            int dx = Mathf.Abs(from.x - to.x);
            int dy = Mathf.Abs(from.y - to.y);

            // 对角线移动
            if (dx > 0 && dy > 0) return CostDiagonal;

            return CostStraight;
        }

        /// <summary>计算启发值</summary>
        private float CalculateHeuristic(Vector2Int from, Vector2Int to)
        {
            switch (_heuristicType)
            {
                case HeuristicType.Manhattan:
                    return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);

                case HeuristicType.Euclidean:
                    float dx = from.x - to.x;
                    float dy = from.y - to.y;
                    return Mathf.Sqrt(dx * dx + dy * dy);

                case HeuristicType.Diagonal:
                    int adx = Mathf.Abs(from.x - to.x);
                    int ady = Mathf.Abs(from.y - to.y);
                    return CostStraight * (adx + ady) +
                           (CostDiagonal - 2 * CostStraight) * Mathf.Min(adx, ady);

                default:
                    return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
            }
        }

        /// <summary>回溯重建路径</summary>
        private List<Vector2Int> ReconstructPath(PathNode endNode)
        {
            var path = new List<Vector2Int>();
            var current = endNode;

            while (current != null)
            {
                path.Add(current.Coord);
                current = current.Parent;
            }

            // 反转为从起点到终点的顺序
            path.Reverse();
            return path;
        }

        /// <summary>生成缓存键</summary>
        private long GetCacheKey(Vector2Int start, Vector2Int end)
        {
            // 将两个坐标打包为一个long
            // 每个坐标分量用16位表示，总共64位
            long key = ((long)start.x & 0xFFFF) << 48 |
                       ((long)start.y & 0xFFFF) << 32 |
                       ((long)end.x & 0xFFFF) << 16 |
                       ((long)end.y & 0xFFFF);
            return key;
        }

        // ========== 批量寻路与路径平滑 ==========

        /// <summary>
        /// 批量寻路（多个怪物共享同一条路径时优化）
        /// </summary>
        /// <param name="starts">多个起点</param>
        /// <param name="end">共同终点</param>
        /// <returns>每个起点对应的寻路结果</returns>
        public List<PathResult> FindPathsBatch(List<Vector2Int> starts, Vector2Int end)
        {
            var results = new List<PathResult>(starts.Count);
            foreach (var start in starts)
            {
                results.Add(FindPath(start, end));
            }
            return results;
        }

        /// <summary>
        /// 路径平滑（将锯齿路径简化为更少的关键拐点）
        /// 用于怪物移动时减少不必要的方向变化
        /// </summary>
        /// <param name="path">原始A*路径</param>
        /// <returns>平滑后的关键拐点列表</returns>
        public static List<Vector2Int> SmoothPath(List<Vector2Int> path)
        {
            if (path == null || path.Count <= 2) return path;

            var smoothed = new List<Vector2Int> { path[0] };

            for (int i = 1; i < path.Count - 1; i++)
            {
                var prev = path[i - 1];
                var curr = path[i];
                var next = path[i + 1];

                // 计算方向变化
                var dir1 = curr - prev;
                var dir2 = next - curr;

                // 方向改变 = 拐点，需要保留
                if (dir1 != dir2)
                {
                    smoothed.Add(curr);
                }
            }

            smoothed.Add(path[path.Count - 1]);
            return smoothed;
        }

        /// <summary>
        /// 将网格路径转换为世界坐标路径，并进行平滑
        /// </summary>
        /// <param name="gridPath">网格坐标路径</param>
        /// <param name="grid">网格系统引用</param>
        /// <param name="smooth">是否平滑（只保留拐点）</param>
        /// <returns>世界坐标路径</returns>
        public static List<Vector3> GridPathToWorldPath(List<Vector2Int> gridPath, GridSystem grid, bool smooth = true)
        {
            var processedPath = smooth ? SmoothPath(gridPath) : gridPath;
            var worldPath = new List<Vector3>(processedPath.Count);

            for (int i = 0; i < processedPath.Count; i++)
            {
                worldPath.Add(grid.GridToWorld(processedPath[i]));
            }

            return worldPath;
        }

        // ========== 调试方法 ==========

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"寻路系统: 地图={_nodeGridWidth}×{_nodeGridHeight}, " +
                   $"缓存={_pathCache.Count}条(v{_cacheVersion}), " +
                   $"启发={_heuristicType}, 对角线={_allowDiagonal}";
        }
    }
}
