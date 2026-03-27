// ============================================================
// 文件名：BattleInputHandler.cs
// 功能描述：战斗场景玩家操作系统
//          触屏放塔、点击选中、双指缩放、加速、暂停
//          G3-5优化：连续放塔、拖拽放塔、单击空地快捷建造、塔跟手预览
// 创建时间：2026-03-25
// 所属模块：Battle
// 对应交互：阶段三 #142 / G3-5
// ============================================================


using UnityEngine;
using UnityEngine.EventSystems;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Tower;

using AetheraSurvivors.Battle.Map;
using Logger = AetheraSurvivors.Framework.Logger;


namespace AetheraSurvivors.Battle
{
    /// <summary>
    /// 战斗操作系统 — 处理玩家在战斗场景中的所有输入
    /// 
    /// 功能：
    /// 1. 点击/触摸选中塔
    /// 2. 拖拽放塔 + 从按钮拖拽放塔
    /// 3. 双指缩放地图
    /// 4. 拖拽移动地图视角
    /// 5. 加速按钮（1x/2x）
    /// 6. 暂停
    /// 7. [G3-5] 连续放塔模式（放完不退出，继续放同类型）
    /// 8. [G3-5] 单击空地弹出快捷建造菜单
    /// 9. [G3-5] 塔跟手预览（半透明塔跟随手指）
    /// </summary>

    public class BattleInputHandler : MonoSingleton<BattleInputHandler>
    {
        // ========== 配置 ==========

        [Header("触摸配置")]
        [SerializeField] private float _tapThreshold = 0.2f;  // 点击判定时间阈值
        [SerializeField] private float _dragThreshold = 10f;   // 拖拽判定距离（像素）

        // ========== 运行时数据 ==========

        /// <summary>触摸开始时间</summary>
        private float _touchStartTime;

        /// <summary>触摸开始位置（屏幕坐标）</summary>
        private Vector2 _touchStartScreenPos;

        /// <summary>是否正在拖拽</summary>
        private bool _isDragging = false;

        /// <summary>当前游戏速度（1=正常，2=加速）</summary>
        private float _gameSpeed = 1f;

        /// <summary>是否暂停</summary>
        private bool _isPaused = false;

        /// <summary>放塔预览对象</summary>
        private GameObject _placementPreview;

        /// <summary>主摄像机引用</summary>
        private Camera _mainCamera;

        /// <summary>是否启用输入</summary>
        private bool _inputEnabled = true;

        /// <summary>[G3-5] 是否从塔按钮开始拖拽（拖拽放塔模式）</summary>
        private bool _isDragFromButton = false;

        /// <summary>[G3-5] 塔跟手预览对象</summary>
        private GameObject _towerGhostPreview;
        private SpriteRenderer _towerGhostRenderer;

        /// <summary>[G3-5] 单击空地快捷建造的回调（由BattleUI注册）</summary>
        private System.Action<Vector2Int, Vector2> _onEmptyTileClicked;

        // ========== 公共属性 ==========

        public float GameSpeed => _gameSpeed;
        public bool IsPaused => _isPaused;
        public bool InputEnabled { get => _inputEnabled; set => _inputEnabled = value; }

        /// <summary>[G3-5] 是否正在从按钮拖拽中</summary>
        public bool IsDragFromButton => _isDragFromButton;


        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            _mainCamera = Camera.main;
            Logger.I("BattleInputHandler", "战斗输入系统初始化");
        }

        protected override void OnDispose()
        {
            // 恢复时间缩放
            Time.timeScale = 1f;

            if (_placementPreview != null)
            {
                Destroy(_placementPreview);
            }

            // 清理跟手预览
            DestroyTowerGhost();
        }


        private void Update()
        {
            if (!_inputEnabled || _isPaused) return;

            // 处理鼠标/触摸输入
            HandleTouchInput();
        }

        // ========== 核心方法：输入处理 ==========

        /// <summary>处理触摸/鼠标输入</summary>
        private void HandleTouchInput()
        {
            // 检查是否点击在UI上（防止UI点击穿透到游戏世界）
            bool isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            // 鼠标输入（PC开发阶段）
            if (Input.GetMouseButtonDown(0))
            {
                if (!isPointerOverUI)
                    OnTouchBegan(Input.mousePosition);
            }
            else if (Input.GetMouseButton(0))
            {
                if (!isPointerOverUI)
                    OnTouchMoved(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (!isPointerOverUI)
                    OnTouchEnded(Input.mousePosition);
            }


            // 滚轮缩放（PC开发阶段，映射到双指缩放）
            float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                OnPinchZoom(scrollDelta * 2f);
            }
        }

        /// <summary>触摸开始</summary>
        private void OnTouchBegan(Vector2 screenPos)
        {
            _touchStartTime = Time.unscaledTime;
            _touchStartScreenPos = screenPos;
            _isDragging = false;
        }

        /// <summary>触摸移动</summary>
        private void OnTouchMoved(Vector2 screenPos)
        {
            float dragDist = Vector2.Distance(screenPos, _touchStartScreenPos);

            if (!_isDragging && dragDist > _dragThreshold)
            {
                _isDragging = true;
            }

            if (_isDragging)
            {
                // 如果在放塔模式（含拖拽放塔），更新预览位置 + 塔跟手
                if (TowerManager.HasInstance && TowerManager.Instance.IsPlacementMode)
                {
                    UpdatePlacementPreview(screenPos);
                    UpdateTowerGhostPosition(screenPos);
                }
            }
        }


        /// <summary>触摸结束</summary>
        private void OnTouchEnded(Vector2 screenPos)
        {
            float touchDuration = Time.unscaledTime - _touchStartTime;
            float dragDist = Vector2.Distance(screenPos, _touchStartScreenPos);

            bool isTap = touchDuration < _tapThreshold && dragDist < _dragThreshold;

            if (isTap)
            {
                OnTap(screenPos);
            }
            else if (TowerManager.HasInstance && TowerManager.Instance.IsPlacementMode)
            {
                // 拖拽结束时尝试放塔（连续模式：放完不退出）
                TryPlaceTowerAtScreen(screenPos);
            }

            // 拖拽放塔结束时清理状态
            if (_isDragFromButton)
            {
                _isDragFromButton = false;
                // 隐藏跟手预览
                HideTowerGhost();
                // 如果是从按钮拖拽的，放完后退出放塔模式
                if (TowerManager.HasInstance && TowerManager.Instance.IsPlacementMode)
                {
                    TowerManager.Instance.ExitPlacementMode();
                }
            }
            else
            {
                // 普通模式下也隐藏跟手预览（但不退出放塔模式 = 连续放塔）
                HideTowerGhost();
            }

            _isDragging = false;
        }


        /// <summary>点击处理</summary>
        private void OnTap(Vector2 screenPos)
        {
            Vector3 worldPos = ScreenToWorld(screenPos);

            // 如果在放塔模式，点击直接放塔（连续模式：成功后不退出）
            if (TowerManager.HasInstance && TowerManager.Instance.IsPlacementMode)
            {
                TryPlaceTowerAtScreen(screenPos);
                return;
            }

            // 尝试选中塔
            if (TowerManager.HasInstance)
            {
                bool selected = TowerManager.Instance.TrySelectAtWorldPos(worldPos);
                if (selected) return;

                // 没选中塔，取消已有选中
                TowerManager.Instance.DeselectTower();
            }

            // [G3-5] 点击了空地 → 检查是否是可放塔位，弹出快捷建造菜单
            if (GridSystem.HasInstance && _onEmptyTileClicked != null)
            {
                Vector2Int gridPos = GridSystem.Instance.WorldToGrid(worldPos);
                if (GridSystem.Instance.CanPlaceTower(gridPos))
                {
                    _onEmptyTileClicked.Invoke(gridPos, screenPos);
                    Logger.D("BattleInput", "[G3-5] 点击空地触发快捷建造 @({0},{1})", gridPos.x, gridPos.y);
                }
            }
        }


        /// <summary>缩放处理</summary>
        private void OnPinchZoom(float delta)
        {
            if (_mainCamera == null) return;

            // 正交摄像机缩放
            if (_mainCamera.orthographic)
            {
                _mainCamera.orthographicSize -= delta;
                _mainCamera.orthographicSize = Mathf.Clamp(_mainCamera.orthographicSize, 3f, 15f);
            }
        }

        // ========== 放塔相关 ==========

        /// <summary>在屏幕坐标处尝试放塔（连续模式：成功后不自动退出）</summary>
        private void TryPlaceTowerAtScreen(Vector2 screenPos)
        {
            if (!TowerManager.HasInstance || !GridSystem.HasInstance) return;

            Vector3 worldPos = ScreenToWorld(screenPos);
            Vector2Int gridPos = GridSystem.Instance.WorldToGrid(worldPos);

            if (TowerManager.Instance.TryPlaceTower(gridPos))
            {
                // [G3-5] 连续放塔：放完不退出，继续保持放塔模式
                Logger.D("BattleInput", "[G3-5] 放塔成功(连续模式) @({0},{1})", gridPos.x, gridPos.y);

                // 如果金币不足以继续放同类型塔，自动退出放塔模式
                if (!CanAffordCurrentPlacement())
                {
                    TowerManager.Instance.ExitPlacementMode();
                    Logger.D("BattleInput", "[G3-5] 金币不足，自动退出放塔模式");
                }
            }
        }

        /// <summary>[G3-5] 检查当前放塔类型是否还买得起</summary>
        private bool CanAffordCurrentPlacement()
        {
            if (!TowerManager.HasInstance || !BattleEconomyManager.HasInstance) return false;
            if (!TowerManager.Instance.IsPlacementMode) return false;

            var config = TowerManager.Instance.GetCurrentPlacementConfig();
            if (config == null) return false;

            return BattleEconomyManager.Instance.CanAfford(config.buildCost);
        }


        /// <summary>更新放塔预览位置</summary>
        private void UpdatePlacementPreview(Vector2 screenPos)
        {
            if (!GridSystem.HasInstance) return;

            Vector3 worldPos = ScreenToWorld(screenPos);
            Vector3 snappedPos;
            var gridPos = GridSystem.Instance.WorldToGridSnapped(worldPos, out snappedPos);

            // 检查是否可放置，并更新路径预览
            if (PathVisualizer.HasInstance)
            {
                PathVisualizer.Instance.ShowPlacementPreview(gridPos);
            }
        }

        // ========== [G3-5] 从按钮拖拽放塔 ==========

        /// <summary>
        /// [G3-5] 从塔按钮开始拖拽（由BattleUI调用）
        /// 进入放塔模式并标记为拖拽放塔
        /// </summary>
        public void BeginDragFromButton(TowerType type)
        {
            if (!TowerManager.HasInstance) return;

            // 进入放塔模式
            if (TowerManager.Instance.IsPlacementMode)
                TowerManager.Instance.ExitPlacementMode();

            TowerManager.Instance.EnterPlacementMode(type);
            _isDragFromButton = true;
            _isDragging = true;

            // 显示跟手预览
            ShowTowerGhost(type);

            Logger.D("BattleInput", "[G3-5] 从按钮拖拽放塔开始: {0}", type);
        }

        // ========== [G3-5] 快捷建造回调注册 ==========

        /// <summary>
        /// [G3-5] 注册单击空地的回调（由BattleUI注册）
        /// </summary>
        public void RegisterEmptyTileCallback(System.Action<Vector2Int, Vector2> callback)
        {
            _onEmptyTileClicked = callback;
        }

        /// <summary>
        /// [G3-5] 取消注册
        /// </summary>
        public void UnregisterEmptyTileCallback()
        {
            _onEmptyTileClicked = null;
        }

        // ========== [G3-5] 塔跟手预览 ==========

        /// <summary>[G3-5] 显示塔跟手预览（半透明塔跟随手指）</summary>
        private void ShowTowerGhost(TowerType type)
        {
            if (_towerGhostPreview == null)
            {
                _towerGhostPreview = new GameObject("[TowerGhost]");
                _towerGhostRenderer = _towerGhostPreview.AddComponent<SpriteRenderer>();
                _towerGhostRenderer.sortingOrder = 100;
            }

            // 使用塔的颜色作为预览
            _towerGhostRenderer.color = GetTowerGhostColor(type);
            _towerGhostRenderer.sprite = CreateGhostSprite();
            _towerGhostPreview.SetActive(true);
        }

        /// <summary>[G3-5] 隐藏塔跟手预览</summary>
        private void HideTowerGhost()
        {
            if (_towerGhostPreview != null)
                _towerGhostPreview.SetActive(false);
        }

        /// <summary>[G3-5] 销毁跟手预览</summary>
        private void DestroyTowerGhost()
        {
            if (_towerGhostPreview != null)
            {
                Destroy(_towerGhostPreview);
                _towerGhostPreview = null;
                _towerGhostRenderer = null;
            }
        }

        /// <summary>[G3-5] 更新塔跟手预览位置，吸附到格子中心</summary>
        private void UpdateTowerGhostPosition(Vector2 screenPos)
        {
            if (_towerGhostPreview == null || !_towerGhostPreview.activeSelf) return;
            if (!GridSystem.HasInstance) return;

            Vector3 worldPos = ScreenToWorld(screenPos);
            Vector3 snappedPos;
            var gridPos = GridSystem.Instance.WorldToGridSnapped(worldPos, out snappedPos);

            _towerGhostPreview.transform.position = new Vector3(snappedPos.x, snappedPos.y, -0.5f);

            // 根据是否可放置改变颜色（绿色=可放，红色=不可放）
            bool canPlace = GridSystem.Instance.CanPlaceTower(gridPos);
            if (_towerGhostRenderer != null)
            {
                Color c = _towerGhostRenderer.color;
                c.a = canPlace ? 0.6f : 0.3f;
                _towerGhostRenderer.color = canPlace
                    ? new Color(0.3f, 1f, 0.3f, 0.6f)  // 绿色半透明 = 可放
                    : new Color(1f, 0.3f, 0.3f, 0.3f);  // 红色半透明 = 不可放
            }
        }

        /// <summary>[G3-5] 获取塔类型对应的预览颜色</summary>
        private Color GetTowerGhostColor(TowerType type)
        {
            switch (type)
            {
                case TowerType.Archer: return new Color(0.4f, 0.8f, 0.4f, 0.5f);
                case TowerType.Mage:   return new Color(0.5f, 0.4f, 1f, 0.5f);
                case TowerType.Ice:    return new Color(0.4f, 0.8f, 1f, 0.5f);
                case TowerType.Cannon: return new Color(1f, 0.6f, 0.2f, 0.5f);
                case TowerType.Poison: return new Color(0.6f, 1f, 0.2f, 0.5f);
                case TowerType.GoldMine: return new Color(1f, 0.9f, 0.2f, 0.5f);
                default: return new Color(1f, 1f, 1f, 0.5f);
            }
        }

        /// <summary>[G3-5] 创建简单的方块Sprite作为预览图</summary>
        private Sprite CreateGhostSprite()
        {
            // 创建1x1白色纹理
            var tex = new Texture2D(32, 32);
            Color[] pixels = new Color[32 * 32];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            // 约0.8格大小
            return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 40f);
        }

        // ========== 游戏速度控制 ==========


        /// <summary>
        /// 切换游戏速度（1x → 2x → 1x 循环）
        /// </summary>
        public void ToggleSpeed()
        {
            _gameSpeed = _gameSpeed >= 2f ? 1f : 2f;
            Time.timeScale = _isPaused ? 0f : _gameSpeed;

            Logger.D("BattleInput", "游戏速度: {0}x", _gameSpeed);
        }

        /// <summary>
        /// 设置游戏速度
        /// </summary>
        public void SetGameSpeed(float speed)
        {
            _gameSpeed = Mathf.Clamp(speed, 0.5f, 3f);
            if (!_isPaused)
            {
                Time.timeScale = _gameSpeed;
            }
        }

        /// <summary>
        /// 暂停/恢复
        /// </summary>
        public void TogglePause()
        {
            SetPaused(!_isPaused);
        }

        /// <summary>
        /// 设置暂停状态
        /// </summary>
        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            Time.timeScale = _isPaused ? 0f : _gameSpeed;

            if (Wave.WaveManager.HasInstance)
            {
                Wave.WaveManager.Instance.SetPaused(paused);
            }

            Logger.D("BattleInput", "游戏{0}", paused ? "暂停" : "恢复");
        }

        // ========== 工具方法 ==========

        /// <summary>屏幕坐标转世界坐标</summary>
        private Vector3 ScreenToWorld(Vector2 screenPos)
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return Vector3.zero;

            Vector3 worldPos = _mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
            worldPos.z = 0f;
            return worldPos;
        }
    }
}
