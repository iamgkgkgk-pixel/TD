// ============================================================
// 文件名：SpatialPartition.cs
// 功能描述：空间分区系统 — 网格分区加速攻击目标查找
//          将战场划分为网格单元，避免每帧遍历所有怪物
// 创建时间：2026-03-25
// 所属模块：Battle/Performance
// 对应交互：阶段三 #162
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Enemy;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Performance

{
    /// <summary>
    /// 空间分区网格系统
    /// 
    /// 原理：
    /// 将战场划分为固定大小的网格单元（CellSize×CellSize），
    /// 每个怪物只注册在其所在的单元格中。
    /// 查找射程内目标时，只检查相关单元格内的怪物，
    /// 大幅减少遍历次数（O(N) → O(K)，K为射程内单元格中的怪物数）。
    /// 
    /// 性能目标：
    /// 100+怪物场景，目标查找从每帧遍历所有怪物
    /// 优化到只遍历射程覆盖范围内的少量怪物。
    /// </summary>
    public class SpatialPartition : MonoSingleton<SpatialPartition>
    {
        // ========== 配置 ==========

        /// <summary>单元格大小（世界单位），建议等于最大塔射程的一半</summary>
        [SerializeField] private float _cellSize = 2f;

        // ========== 数据结构 ==========

        /// <summary>网格单元：行列键 → 该单元格内的怪物列表</summary>
        private readonly Dictionary<long, List<EnemyBase>> _cells = new Dictionary<long, List<EnemyBase>>(64);

        /// <summary>怪物 → 当前所在单元格键 的反向索引</summary>
        private readonly Dictionary<int, long> _entityCellMap = new Dictionary<int, long>(128);

        /// <summary>列表对象池（避免GC）</summary>
        private readonly Queue<List<EnemyBase>> _listPool = new Queue<List<EnemyBase>>(32);

        /// <summary>查询结果缓存</summary>
        private readonly List<EnemyBase> _queryResultCache = new List<EnemyBase>(32);

        /// <summary>临时哈希（去重用）</summary>
        private readonly HashSet<int> _queryDedup = new HashSet<int>();

        // ========== 公共属性 ==========

        /// <summary>单元格大小</summary>
        public float CellSize => _cellSize;

        /// <summary>当前活跃单元格数</summary>
        public int ActiveCellCount => _cells.Count;

        /// <summary>当前注册的实体数</summary>
        public int EntityCount => _entityCellMap.Count;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            Logger.I("SpatialPartition", "空间分区系统初始化 CellSize={0}", _cellSize);
        }

        protected override void OnDispose()
        {
            Clear();
        }

        private void LateUpdate()
        {
            // 每帧更新所有活跃怪物的位置
            UpdateAllPositions();
        }

        // ========== 核心方法 ==========

        /// <summary>
        /// 注册一个怪物到空间分区
        /// </summary>
        public void Register(EnemyBase enemy)
        {
            if (enemy == null) return;

            long cellKey = GetCellKey(enemy.transform.position);
            int entityId = enemy.InstanceId;

            // 如果已注册，先移除旧位置
            if (_entityCellMap.ContainsKey(entityId))
            {
                Unregister(enemy);
            }

            // 添加到新单元格
            if (!_cells.TryGetValue(cellKey, out var list))
            {
                list = GetListFromPool();
                _cells[cellKey] = list;
            }

            list.Add(enemy);
            _entityCellMap[entityId] = cellKey;
        }

        /// <summary>
        /// 从空间分区中注销一个怪物
        /// </summary>
        public void Unregister(EnemyBase enemy)
        {
            if (enemy == null) return;

            int entityId = enemy.InstanceId;

            if (_entityCellMap.TryGetValue(entityId, out long oldKey))
            {
                if (_cells.TryGetValue(oldKey, out var list))
                {
                    list.Remove(enemy);
                    if (list.Count == 0)
                    {
                        _cells.Remove(oldKey);
                        ReturnListToPool(list);
                    }
                }
                _entityCellMap.Remove(entityId);
            }
        }

        /// <summary>
        /// 更新一个怪物的位置（如果移动到新单元格则重新注册）
        /// </summary>
        public void UpdatePosition(EnemyBase enemy)
        {
            if (enemy == null || enemy.IsDead) return;

            int entityId = enemy.InstanceId;
            long newKey = GetCellKey(enemy.transform.position);

            if (_entityCellMap.TryGetValue(entityId, out long oldKey))
            {
                if (oldKey == newKey) return; // 没有跨越单元格，无需更新

                // 从旧单元格移除
                if (_cells.TryGetValue(oldKey, out var oldList))
                {
                    oldList.Remove(enemy);
                    if (oldList.Count == 0)
                    {
                        _cells.Remove(oldKey);
                        ReturnListToPool(oldList);
                    }
                }

                // 添加到新单元格
                if (!_cells.TryGetValue(newKey, out var newList))
                {
                    newList = GetListFromPool();
                    _cells[newKey] = newList;
                }

                newList.Add(enemy);
                _entityCellMap[entityId] = newKey;
            }
            else
            {
                // 未注册，执行注册
                Register(enemy);
            }
        }

        /// <summary>
        /// 查找指定位置/半径范围内的所有怪物
        /// ⚠️ 返回的列表在下次调用时会被覆盖，请立即使用
        /// </summary>
        /// <param name="center">查找中心点</param>
        /// <param name="radius">查找半径</param>
        /// <returns>范围内的怪物列表（共享缓存，勿持有引用）</returns>
        public List<EnemyBase> QueryRadius(Vector3 center, float radius)
        {
            _queryResultCache.Clear();
            _queryDedup.Clear();

            float radiusSqr = radius * radius;

            // 计算需要检查的单元格范围
            int minCol = Mathf.FloorToInt((center.x - radius) / _cellSize);
            int maxCol = Mathf.FloorToInt((center.x + radius) / _cellSize);
            int minRow = Mathf.FloorToInt((center.y - radius) / _cellSize);
            int maxRow = Mathf.FloorToInt((center.y + radius) / _cellSize);

            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    long key = PackCellKey(col, row);

                    if (_cells.TryGetValue(key, out var list))
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var enemy = list[i];
                            if (enemy == null || enemy.IsDead) continue;

                            // 去重（同一怪物不会出现在多个单元格，但以防万一）
                            if (_queryDedup.Contains(enemy.InstanceId)) continue;

                            // 精确距离检测
                            float distSqr = (enemy.transform.position - center).sqrMagnitude;
                            if (distSqr <= radiusSqr)
                            {
                                _queryResultCache.Add(enemy);
                                _queryDedup.Add(enemy.InstanceId);
                            }
                        }
                    }
                }
            }

            return _queryResultCache;
        }

        /// <summary>
        /// 查找最近的N个怪物
        /// </summary>
        public List<EnemyBase> QueryNearestN(Vector3 center, float radius, int count)
        {
            var all = QueryRadius(center, radius);
            if (all.Count <= count) return all;

            // 按距离排序（只排前N个）
            all.Sort((a, b) =>
            {
                float da = (a.transform.position - center).sqrMagnitude;
                float db = (b.transform.position - center).sqrMagnitude;
                return da.CompareTo(db);
            });

            // 截取前N个
            if (all.Count > count)
            {
                all.RemoveRange(count, all.Count - count);
            }

            return all;
        }

        /// <summary>
        /// 查找指定位置最近的单个怪物
        /// </summary>
        public EnemyBase QueryNearest(Vector3 center, float radius)
        {
            var all = QueryRadius(center, radius);
            if (all.Count == 0) return null;

            EnemyBase nearest = null;
            float minDistSqr = float.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                float distSqr = (all[i].transform.position - center).sqrMagnitude;
                if (distSqr < minDistSqr)
                {
                    minDistSqr = distSqr;
                    nearest = all[i];
                }
            }

            return nearest;
        }

        /// <summary>清除所有数据</summary>
        public void Clear()
        {
            foreach (var pair in _cells)
            {
                ReturnListToPool(pair.Value);
            }
            _cells.Clear();
            _entityCellMap.Clear();
        }

        // ========== 内部方法 ==========

        /// <summary>更新所有活跃怪物的位置</summary>
        private void UpdateAllPositions()
        {
            if (!EnemySpawner.HasInstance) return;

            var enemies = EnemySpawner.Instance.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && !enemies[i].IsDead)
                {
                    UpdatePosition(enemies[i]);
                }
            }

            // 清理已死亡/销毁的怪物
            CleanupDeadEntities();
        }

        /// <summary>清理已无效的实体</summary>
        private void CleanupDeadEntities()
        {
            // 收集需要移除的ID
            var toRemove = new List<int>(8);
            foreach (var pair in _entityCellMap)
            {
                // 检查怪物是否仍然有效
                bool found = false;
                if (EnemySpawner.HasInstance)
                {
                    var enemies = EnemySpawner.Instance.ActiveEnemies;
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        if (enemies[i] != null && enemies[i].InstanceId == pair.Key)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    toRemove.Add(pair.Key);
                }
            }

            // 移除
            for (int i = 0; i < toRemove.Count; i++)
            {
                int entityId = toRemove[i];
                if (_entityCellMap.TryGetValue(entityId, out long cellKey))
                {
                    if (_cells.TryGetValue(cellKey, out var list))
                    {
                        list.RemoveAll(e => e == null || e.InstanceId == entityId);
                        if (list.Count == 0)
                        {
                            _cells.Remove(cellKey);
                            ReturnListToPool(list);
                        }
                    }
                    _entityCellMap.Remove(entityId);
                }
            }
        }

        // ========== 单元格键计算 ==========

        /// <summary>根据世界坐标计算单元格键</summary>
        private long GetCellKey(Vector3 worldPos)
        {
            int col = Mathf.FloorToInt(worldPos.x / _cellSize);
            int row = Mathf.FloorToInt(worldPos.y / _cellSize);
            return PackCellKey(col, row);
        }

        /// <summary>将列和行打包为long键（高32位=行，低32位=列）</summary>
        private static long PackCellKey(int col, int row)
        {
            return ((long)row << 32) | (uint)col;
        }

        // ========== 列表对象池 ==========

        private List<EnemyBase> GetListFromPool()
        {
            if (_listPool.Count > 0)
            {
                var list = _listPool.Dequeue();
                list.Clear();
                return list;
            }
            return new List<EnemyBase>(8);
        }

        private void ReturnListToPool(List<EnemyBase> list)
        {
            if (list == null || _listPool.Count >= 64) return;
            list.Clear();
            _listPool.Enqueue(list);
        }

        // ========== 调试 ==========

        /// <summary>获取调试信息</summary>
        public string GetDebugInfo()
        {
            int totalEntities = 0;
            int maxPerCell = 0;

            foreach (var pair in _cells)
            {
                totalEntities += pair.Value.Count;
                if (pair.Value.Count > maxPerCell) maxPerCell = pair.Value.Count;
            }

            return $"单元格:{_cells.Count} 实体:{totalEntities} 最大密度:{maxPerCell}/格 CellSize:{_cellSize}";
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || _cells.Count == 0) return;

            Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
            foreach (var pair in _cells)
            {
                int col = (int)(pair.Key & 0xFFFFFFFF);
                int row = (int)(pair.Key >> 32);
                Vector3 center = new Vector3(
                    (col + 0.5f) * _cellSize,
                    (row + 0.5f) * _cellSize,
                    0f
                );
                Gizmos.DrawWireCube(center, new Vector3(_cellSize, _cellSize, 0f));

                // 标注实体数量
                if (pair.Value.Count > 0)
                {
                    Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                    Gizmos.DrawCube(center, new Vector3(_cellSize * 0.9f, _cellSize * 0.9f, 0f));
                    Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
                }
            }
        }
#endif
    }
}
