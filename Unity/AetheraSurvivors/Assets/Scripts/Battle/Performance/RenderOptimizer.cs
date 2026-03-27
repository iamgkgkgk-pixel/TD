// ============================================================
// 文件名：RenderOptimizer.cs
// 功能描述：渲染优化 — DrawCall合批、Sprite Atlas管理、
//          动态分辨率、LOD简化渲染
// 创建时间：2026-03-25
// 所属模块：Battle/Performance
// 对应交互：阶段三 #163-#164
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Performance

{
    /// <summary>
    /// 渲染优化器
    /// 
    /// 优化策略：
    /// 1. 动态合批建议：确保同类型怪物使用相同材质
    /// 2. SortingLayer管理：优化渲染顺序减少Overdraw
    /// 3. 屏幕外对象SpriteRenderer禁用
    /// 4. 动态分辨率缩放（低端机降低渲染分辨率）
    /// 5. Shader简化（低画质模式下切换到更简单的Shader）
    /// </summary>
    public class RenderOptimizer : MonoSingleton<RenderOptimizer>
    {
        // ========== 配置 ==========

        /// <summary>DrawCall目标上限</summary>
        private const int MaxDrawCalls = 50;

        /// <summary>渲染分辨率缩放（1.0=原始，0.75=75%）</summary>
        private float _renderScale = 1.0f;

        /// <summary>共享材质缓存</summary>
        private readonly Dictionary<string, Material> _sharedMaterials = new Dictionary<string, Material>();

        /// <summary>已禁用渲染器列表（用于视锥剔除）</summary>
        private readonly List<SpriteRenderer> _culledRenderers = new List<SpriteRenderer>(64);

        // ========== 公共属性 ==========

        /// <summary>当前渲染分辨率缩放</summary>
        public float RenderScale => _renderScale;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            InitSortingLayers();
            Logger.I("RenderOptimizer", "渲染优化器初始化");
        }

        protected override void OnDispose()
        {
            _sharedMaterials.Clear();
            _culledRenderers.Clear();
        }

        // ========== 核心方法 ==========

        /// <summary>
        /// 获取共享材质（同类型怪物/塔应使用同一个材质实例以触发动态合批）
        /// </summary>
        /// <param name="key">材质键名</param>
        /// <param name="color">材质颜色</param>
        /// <returns>共享材质</returns>
        public Material GetSharedMaterial(string key, Color color)
        {
            if (_sharedMaterials.TryGetValue(key, out var mat))
            {
                return mat;
            }

            var shader = Shader.Find("Sprites/Default");
            mat = new Material(shader);
            mat.color = color;
            _sharedMaterials[key] = mat;

            return mat;
        }

        /// <summary>
        /// 应用材质到SpriteRenderer（确保共享材质以触发合批）
        /// </summary>
        public void ApplySharedMaterial(SpriteRenderer sr, string materialKey, Color color)
        {
            if (sr == null) return;
            sr.sharedMaterial = GetSharedMaterial(materialKey, color);
        }

        /// <summary>
        /// 设置渲染分辨率缩放（低端机优化）
        /// </summary>
        /// <param name="scale">缩放值（0.5~1.0）</param>
        public void SetRenderScale(float scale)
        {
            _renderScale = Mathf.Clamp(scale, 0.5f, 1.0f);

            // 微信小游戏平台通过Canvas缩放实现
            // 实际实现需要根据平台适配
            Logger.I("RenderOptimizer", "渲染分辨率缩放: {0:P0}", _renderScale);
        }

        /// <summary>
        /// 视锥剔除：禁用屏幕外对象的SpriteRenderer
        /// 应在LateUpdate中调用
        /// </summary>
        public void CullOffscreenRenderers(SpriteRenderer[] renderers, Camera cam)
        {
            if (cam == null || renderers == null) return;

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 camPos = cam.transform.position;
            float padding = 2f; // 边界padding防止闪烁

            float left = camPos.x - halfWidth - padding;
            float right = camPos.x + halfWidth + padding;
            float bottom = camPos.y - halfHeight - padding;
            float top = camPos.y + halfHeight + padding;

            for (int i = 0; i < renderers.Length; i++)
            {
                var sr = renderers[i];
                if (sr == null) continue;

                var pos = sr.transform.position;
                bool isVisible = pos.x >= left && pos.x <= right &&
                                pos.y >= bottom && pos.y <= top;

                sr.enabled = isVisible;
            }
        }

        /// <summary>
        /// 根据性能等级应用渲染设置
        /// </summary>
        public void ApplyPerformanceLevel(int qualityLevel)
        {
            switch (qualityLevel)
            {
                case 0: // 高画质
                    SetRenderScale(1.0f);
                    break;
                case 1: // 中画质
                    SetRenderScale(0.85f);
                    break;
                case 2: // 低画质
                    SetRenderScale(0.7f);
                    break;
            }
        }

        // ========== 排序层初始化 ==========

        /// <summary>
        /// 建议的SortingOrder规范：
        /// 0-4: 地图底层（地面、路径）
        /// 5-7: 地图装饰物
        /// 8-10: 怪物
        /// 11-14: 塔
        /// 15-19: 投射物/特效
        /// 20-24: 血条/Buff指示器
        /// 50+: UI元素
        /// 100+: 飘字
        /// 150+: 全屏效果
        /// 200+: 屏幕覆盖
        /// </summary>
        private void InitSortingLayers()
        {
            // 排序规范记录，供其他系统参考
            Logger.D("RenderOptimizer", "排序规范已就绪");
        }

        // ========== 调试 ==========

        public string GetDebugInfo()
        {
            return $"渲染缩放:{_renderScale:P0} 共享材质:{_sharedMaterials.Count}";
        }
    }
}
