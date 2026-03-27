// ============================================================
// 文件名：DrawCallOptimizer.cs
// 功能描述：DrawCall优化 — 精灵合批分析、材质管理、
//          动态合批监控、SpriteAtlas加载策略
// 创建时间：2026-03-25
// 所属模块：Battle/Performance
// 对应交互：阶段三 #169-#170
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;


namespace AetheraSurvivors.Battle.Performance
{
    /// <summary>
    /// DrawCall优化器
    /// 
    /// 目标：DrawCall < 50（微信小游戏平台）
    /// 
    /// 策略：
    /// 1. 动态合批（Dynamic Batching）：同材质+同图集的Sprite自动合批
    /// 2. SpriteAtlas管理：确保同类资源打入同一图集
    /// 3. 材质实例控制：避免无意义的材质复制打断合批
    /// 4. SortingLayer/Order优化：合理的排序减少状态切换
    /// 5. 运行时DrawCall监控
    /// </summary>
    public class DrawCallOptimizer : MonoSingleton<DrawCallOptimizer>
    {
        // ========== 配置 ==========

        /// <summary>DrawCall目标上限</summary>
        private const int TargetMaxDrawCalls = 50;

        /// <summary>DrawCall警告阈值</summary>
        private const int WarningDrawCalls = 40;

        // ========== 运行时数据 ==========

        /// <summary>当前DrawCall估算值</summary>
        private int _estimatedDrawCalls = 0;

        /// <summary>材质使用统计</summary>
        private readonly Dictionary<Material, int> _materialUsageCount = new Dictionary<Material, int>(32);

        /// <summary>合批优化建议</summary>
        private readonly List<string> _optimizationHints = new List<string>(8);

        /// <summary>检查间隔计时器</summary>
        private float _analyzeTimer = 0f;
        private const float AnalyzeInterval = 2f;

        // ========== 公共属性 ==========

        /// <summary>当前DrawCall估算</summary>
        public int EstimatedDrawCalls => _estimatedDrawCalls;

        /// <summary>是否超标</summary>
        public bool IsOverBudget => _estimatedDrawCalls > TargetMaxDrawCalls;

        // ========== 材质管理 ==========

        /// <summary>
        /// 按类别分组的共享材质
        /// 同一类别的所有对象应使用同一个材质实例以触发合批
        /// </summary>
        private readonly Dictionary<string, Material> _batchMaterials = new Dictionary<string, Material>(16);

        /// <summary>
        /// 获取合批材质
        /// 同组的所有SpriteRenderer使用同一Material实例
        /// </summary>
        /// <param name="groupKey">合批组名（如 "enemy_normal", "tower_archer"）</param>
        /// <returns>共享材质</returns>
        public Material GetBatchMaterial(string groupKey)
        {
            if (_batchMaterials.TryGetValue(groupKey, out var mat))
            {
                return mat;
            }

            var shader = Shader.Find("Sprites/Default");
            mat = new Material(shader);
            mat.name = $"Batch_{groupKey}";
            _batchMaterials[groupKey] = mat;

            return mat;
        }

        /// <summary>
        /// 将SpriteRenderer配置为合批友好
        /// </summary>
        public void ConfigureForBatching(SpriteRenderer sr, string batchGroup, int sortingOrder)
        {
            if (sr == null) return;

            sr.sharedMaterial = GetBatchMaterial(batchGroup);
            sr.sortingOrder = sortingOrder;
        }

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            Logger.I("DrawCallOptimizer", "DrawCall优化器初始化 目标:<{0}", TargetMaxDrawCalls);
        }

        protected override void OnDispose()
        {
            _batchMaterials.Clear();
            _materialUsageCount.Clear();
        }

        private void Update()
        {
            _analyzeTimer += Time.unscaledDeltaTime;

            if (_analyzeTimer >= AnalyzeInterval)
            {
                _analyzeTimer = 0f;
                AnalyzeDrawCalls();
            }
        }

        // ========== DrawCall分析 ==========

        /// <summary>分析当前DrawCall情况</summary>
        private void AnalyzeDrawCalls()
        {
            _materialUsageCount.Clear();
            _optimizationHints.Clear();

            // 统计所有活跃的SpriteRenderer
            var allRenderers = FindObjectsOfType<SpriteRenderer>();
            int visibleCount = 0;
            int distinctMaterialCount = 0;

            var materialSet = new HashSet<Material>();

            for (int i = 0; i < allRenderers.Length; i++)
            {
                var sr = allRenderers[i];
                if (!sr.enabled || !sr.gameObject.activeInHierarchy) continue;

                visibleCount++;

                var mat = sr.sharedMaterial;
                if (mat != null)
                {
                    materialSet.Add(mat);

                    if (_materialUsageCount.ContainsKey(mat))
                        _materialUsageCount[mat]++;
                    else
                        _materialUsageCount[mat] = 1;
                }
            }

            distinctMaterialCount = materialSet.Count;

            // 估算DrawCall
            // 每种不同的材质至少产生1个DrawCall
            // 同材质+同图集+连续sortingOrder可以合批
            _estimatedDrawCalls = distinctMaterialCount + 5; // +5为UI/Camera/背景等固定DrawCall

            // 生成优化建议
            if (_estimatedDrawCalls > WarningDrawCalls)
            {
                // 找出使用独立材质的对象
                foreach (var pair in _materialUsageCount)
                {
                    if (pair.Value == 1 && pair.Key.name.Contains("Instance"))
                    {
                        _optimizationHints.Add($"材质实例'{pair.Key.name}'只有1个用户，建议使用共享材质");
                    }
                }

                if (_optimizationHints.Count > 0)
                {
                    Logger.W("DrawCallOptimizer", "DrawCall估算:{0} (目标<{1}), 发现{2}条优化建议",
                        _estimatedDrawCalls, TargetMaxDrawCalls, _optimizationHints.Count);
                }
            }
        }

        // ========== 合批策略 ==========

        /// <summary>
        /// 推荐的合批分组规则
        /// 
        /// 分组原则：同一SpriteAtlas + 同一Material = 一个合批组
        /// 
        /// 建议分组：
        /// - "enemy_normal"     所有普通怪物
        /// - "enemy_boss"       Boss怪物
        /// - "tower_base"       塔底座
        /// - "tower_top"        塔炮管
        /// - "projectile"       投射物
        /// - "vfx"              特效
        /// - "ui_world"         世界空间UI（血条等）
        /// - "map_tile"         地图瓦片（由Tilemap自动合批）
        /// </summary>
        public static class BatchGroups
        {
            public const string EnemyNormal = "enemy_normal";
            public const string EnemyBoss = "enemy_boss";
            public const string TowerBase = "tower_base";
            public const string TowerTop = "tower_top";
            public const string Projectile = "projectile";
            public const string VFX = "vfx";
            public const string UIWorld = "ui_world";
            public const string MapTile = "map_tile";
        }

        // ========== 调试 ==========

        public string GetDebugInfo()
        {
            return $"DrawCall(估):{_estimatedDrawCalls}/{TargetMaxDrawCalls} " +
                   $"材质:{_materialUsageCount.Count} " +
                   $"合批组:{_batchMaterials.Count} " +
                   $"{(IsOverBudget ? "⚠️超标" : "✅")}";
        }

        /// <summary>获取优化建议列表</summary>
        public IReadOnlyList<string> GetOptimizationHints()
        {
            return _optimizationHints;
        }
    }
}
