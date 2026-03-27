
// ============================================================
// 文件名：UIManager.cs
// 功能描述：UI管理器 — 栈式UI管理、层级控制、异步加载、面板缓存
//          提供统一的面板打开/关闭/切换接口
// 创建时间：2026-03-25
// 所属模块：Framework
// 对应交互：阶段二 #48
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AetheraSurvivors.Framework
{
    /// <summary>
    /// UI管理器 — 全局单例
    /// 
    /// 职责：
    /// 1. 栈式面板管理（打开新面板自动隐藏前一个）
    /// 2. 多层级Canvas管理（Bottom/Normal/Popup/Top/System）
    /// 3. 面板缓存池（已关闭的面板可缓存复用）
    /// 4. 从Resources异步加载面板预制体
    /// 
    /// 使用示例：
    ///   // 打开面板
    ///   UIManager.Instance.Open&lt;BattleHUDPanel&gt;();
    ///   
    ///   // 打开面板并传参
    ///   UIManager.Instance.Open&lt;BattleResultPanel&gt;(new ResultData { stars = 3 });
    ///   
    ///   // 关闭面板
    ///   UIManager.Instance.Close&lt;BattleHUDPanel&gt;();
    ///   
    ///   // 关闭栈顶面板（返回上一个）
    ///   UIManager.Instance.CloseTop();
    ///   
    ///   // 获取已打开的面板
    ///   var panel = UIManager.Instance.GetPanel&lt;BattleHUDPanel&gt;();
    /// </summary>
    public class UIManager : MonoSingleton<UIManager>
    {
        // ========== 常量 ==========

        /// <summary>面板预制体路径前缀</summary>
        private const string PanelPrefabPath = "Prefabs/UI/";

        // ========== 私有字段 ==========

        /// <summary>UI根Canvas</summary>
        private Canvas _rootCanvas;

        /// <summary>各层级的Transform节点</summary>
        private readonly Dictionary<UILayer, Transform> _layerRoots = new Dictionary<UILayer, Transform>();

        /// <summary>面板栈（当前显示的面板栈）</summary>
        private readonly List<BasePanel> _panelStack = new List<BasePanel>();

        /// <summary>面板缓存池（已关闭但未销毁的面板）</summary>
        private readonly Dictionary<Type, BasePanel> _panelCache = new Dictionary<Type, BasePanel>();

        /// <summary>当前已打开的面板（类型→实例映射）</summary>
        private readonly Dictionary<Type, BasePanel> _openPanels = new Dictionary<Type, BasePanel>();

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            CreateUIRoot();
        }

        protected override void OnDispose()
        {
            CloseAll();
            _panelCache.Clear();
        }

        // ========== 公共方法：打开面板 ==========

        /// <summary>
        /// 打开指定类型的面板
        /// </summary>
        /// <typeparam name="T">面板类型（必须继承BasePanel）</typeparam>
        /// <param name="param">传给面板的参数</param>
        /// <returns>面板实例</returns>
        public T Open<T>(object param = null) where T : BasePanel
        {
            return Open(typeof(T), param) as T;
        }

        /// <summary>
        /// 打开指定类型的面板（非泛型版本）
        /// </summary>
        public BasePanel Open(Type panelType, object param = null)
        {
            // 如果已经打开了，直接返回
            if (_openPanels.TryGetValue(panelType, out var existingPanel))
            {
                if (existingPanel != null && existingPanel.IsShowing)
                {
                    Debug.LogWarning($"[UIManager] 面板已打开: {panelType.Name}");
                    return existingPanel;
                }
            }

            BasePanel panel = null;

            // 1. 先从缓存中查找
            if (_panelCache.TryGetValue(panelType, out panel) && panel != null)
            {
                _panelCache.Remove(panelType);
                panel.InternalShow();
            }
            else
            {
                // 2. 从Resources加载预制体
                panel = LoadPanel(panelType);
                if (panel == null)
                {
                    Debug.LogError($"[UIManager] 加载面板失败: {panelType.Name}");
                    return null;
                }

                // 放到对应层级节点下
                SetPanelLayer(panel);

                // 首次打开
                panel.InternalOpen(param);
            }

            // 加入打开列表和栈
            _openPanels[panelType] = panel;
            _panelStack.Add(panel);

            return panel;
        }

        // ========== 公共方法：关闭面板 ==========

        /// <summary>
        /// 关闭指定类型的面板
        /// </summary>
        /// <typeparam name="T">面板类型</typeparam>
        public void Close<T>() where T : BasePanel
        {
            Close(typeof(T));
        }

        /// <summary>
        /// 关闭指定类型的面板（非泛型版本）
        /// </summary>
        public void Close(Type panelType)
        {
            if (!_openPanels.TryGetValue(panelType, out var panel))
            {
                return;
            }

            ClosePanel(panel, panelType);
        }

        /// <summary>
        /// 关闭栈顶面板（返回上一个）
        /// </summary>
        public void CloseTop()
        {
            if (_panelStack.Count == 0) return;

            var topPanel = _panelStack[_panelStack.Count - 1];
            Close(topPanel.GetType());
        }

        /// <summary>
        /// 关闭所有面板
        /// </summary>
        public void CloseAll()
        {
            // 从栈顶开始逐个关闭
            for (int i = _panelStack.Count - 1; i >= 0; i--)
            {
                var panel = _panelStack[i];
                if (panel != null)
                {
                    panel.InternalClose();
                    if (!panel.IsCached)
                    {
                        Destroy(panel.gameObject);
                    }
                    else
                    {
                        panel.gameObject.SetActive(false);
                        _panelCache[panel.GetType()] = panel;
                    }
                }
            }

            _panelStack.Clear();
            _openPanels.Clear();
        }

        /// <summary>
        /// 关闭指定层级的所有面板
        /// </summary>
        public void CloseLayer(UILayer layer)
        {
            var toClose = new List<Type>();

            foreach (var pair in _openPanels)
            {
                if (pair.Value != null && pair.Value.Layer == layer)
                {
                    toClose.Add(pair.Key);
                }
            }

            for (int i = 0; i < toClose.Count; i++)
            {
                Close(toClose[i]);
            }
        }

        // ========== 公共方法：查询 ==========

        /// <summary>
        /// 获取已打开的面板实例
        /// </summary>
        /// <typeparam name="T">面板类型</typeparam>
        /// <returns>面板实例，未打开则返回null</returns>
        public T GetPanel<T>() where T : BasePanel
        {
            if (_openPanels.TryGetValue(typeof(T), out var panel))
            {
                return panel as T;
            }
            return null;
        }

        /// <summary>
        /// 检查面板是否已打开
        /// </summary>
        public bool IsOpen<T>() where T : BasePanel
        {
            return _openPanels.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 获取当前栈顶面板
        /// </summary>
        public BasePanel GetTopPanel()
        {
            return _panelStack.Count > 0 ? _panelStack[_panelStack.Count - 1] : null;
        }

        /// <summary>
        /// 获取当前打开的面板数量
        /// </summary>
        public int OpenCount => _openPanels.Count;

        /// <summary>
        /// 获取UI根Canvas
        /// </summary>
        public Canvas RootCanvas => _rootCanvas;

        // ========== 私有方法 ==========

        /// <summary>创建UI根节点和各层级节点</summary>
        private void CreateUIRoot()
        {
            // 创建根Canvas
            var rootGo = new GameObject("[UIRoot]");
            rootGo.transform.SetParent(transform);

            _rootCanvas = rootGo.AddComponent<Canvas>();
            _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _rootCanvas.sortingOrder = 0;
            _rootCanvas.pixelPerfect = true;


            var scaler = rootGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // 宽高各占一半权重

            rootGo.AddComponent<GraphicRaycaster>();

            // 创建各层级节点
            CreateLayerRoot(rootGo.transform, UILayer.Bottom, 0);
            CreateLayerRoot(rootGo.transform, UILayer.Normal, 100);
            CreateLayerRoot(rootGo.transform, UILayer.Popup, 200);
            CreateLayerRoot(rootGo.transform, UILayer.Top, 300);
            CreateLayerRoot(rootGo.transform, UILayer.System, 400);
        }

        /// <summary>创建单个层级节点</summary>
        private void CreateLayerRoot(Transform parent, UILayer layer, int sortOrder)
        {
            var go = new GameObject($"Layer_{layer}");
            go.transform.SetParent(parent, false);

            // 使用RectTransform铺满父节点
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 子Canvas控制层级排序
            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortOrder;

            go.AddComponent<GraphicRaycaster>();

            _layerRoots[layer] = go.transform;
        }

        /// <summary>从Resources加载面板预制体，找不到时动态创建（支持纯代码构建的面板）</summary>
        private BasePanel LoadPanel(Type panelType)
        {
            // 面板预制体命名约定：类名即预制体名
            string path = PanelPrefabPath + panelType.Name;

            var prefab = Resources.Load<GameObject>(path);
            if (prefab != null)
            {
                // 从预制体实例化
                var go = Instantiate(prefab);
                go.name = panelType.Name;

                var panel = go.GetComponent<BasePanel>();
                if (panel == null)
                {
                    Debug.LogError($"[UIManager] 预制体缺少BasePanel组件: {path}");
                    Destroy(go);
                    return null;
                }

                return panel;
            }

            // 找不到预制体 → 动态创建（纯代码构建UI的面板）
            Debug.Log($"[UIManager] 未找到预制体 {path}，动态创建面板: {panelType.Name}");
            var dynamicGo = new GameObject(panelType.Name);
            var rectTransform = dynamicGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            var dynamicPanel = dynamicGo.AddComponent(panelType) as BasePanel;
            if (dynamicPanel == null)
            {
                Debug.LogError($"[UIManager] 动态创建面板失败: {panelType.Name}");
                Destroy(dynamicGo);
                return null;
            }

            return dynamicPanel;
        }


        /// <summary>将面板放到对应层级节点下</summary>
        private void SetPanelLayer(BasePanel panel)
        {
            if (_layerRoots.TryGetValue(panel.Layer, out var layerRoot))
            {
                panel.transform.SetParent(layerRoot, false);

                // 重置RectTransform铺满
                var rect = panel.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
            }
        }

        /// <summary>关闭面板的内部实现</summary>
        private void ClosePanel(BasePanel panel, Type panelType)
        {
            if (panel == null) return;

            // 执行关闭回调
            panel.InternalClose();

            // 从栈和打开列表中移除
            _panelStack.Remove(panel);
            _openPanels.Remove(panelType);

            // 根据缓存策略决定销毁或缓存
            if (panel.IsCached)
            {
                panel.gameObject.SetActive(false);
                _panelCache[panelType] = panel;
            }
            else
            {
                Destroy(panel.gameObject);
            }
        }
    }
}
