// ============================================================
// 文件名：BattleCamera.cs
// 功能描述：战斗摄像机系统 — 正交摄像机、边界限制、缩放、拖拽
// 创建时间：2026-03-25
// 所属模块：Battle
// 对应交互：阶段三 #148
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

using AetheraSurvivors.Battle.Map;

namespace AetheraSurvivors.Battle
{
    /// <summary>
    /// 战斗摄像机 — 控制战斗场景中的摄像机行为
    /// 
    /// 功能：
    /// 1. 正交摄像机初始设置
    /// 2. 拖拽移动视角
    /// 3. 双指/滚轮缩放
    /// 4. 边界限制（不能移出地图范围）
    /// 5. 自动适配地图大小
    /// </summary>
    public class BattleCamera : MonoSingleton<BattleCamera>
    {
        // ========== 配置 ==========

        [Header("缩放配置")]
        [SerializeField] private float _minOrthoSize = 3f;
        [SerializeField] private float _maxOrthoSize = 12f;
        [SerializeField] private float _zoomSpeed = 2f;
        [SerializeField] private float _zoomSmoothing = 5f;

        [Header("拖拽配置")]
        [SerializeField] private float _dragSmoothing = 8f;

        [Header("边界")]
        [SerializeField] private float _boundsPadding = 1f;

        // ========== 运行时数据 ==========

        private Camera _camera;
        private float _targetOrthoSize;
        private Vector3 _targetPosition;
        private Bounds _mapBounds;
        private bool _isMapBoundsSet = false;

        /// <summary>拖拽相关</summary>
        private bool _isDragging = false;
        private Vector3 _dragStartWorldPos;

        /// <summary>双指缩放相关</summary>
        private float _lastPinchDistance;

        // ========== 公共属性 ==========

        /// <summary>当前缩放级别</summary>
        public float OrthoSize => _camera != null ? _camera.orthographicSize : 5f;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) _camera = gameObject.AddComponent<Camera>();

            _camera.orthographic = true;
            _camera.orthographicSize = 6f;
            _camera.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 1f);
            _targetOrthoSize = _camera.orthographicSize;
            _targetPosition = transform.position;

            Logger.I("BattleCamera", "战斗摄像机初始化");
        }

        private void LateUpdate()
        {
            if (_camera == null) return;

            // 平滑缩放
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _targetOrthoSize, Time.deltaTime * _zoomSmoothing);

            // 平滑移动
            var pos = transform.position;
            pos = Vector3.Lerp(pos, _targetPosition, Time.deltaTime * _dragSmoothing);

            // 应用边界限制
            if (_isMapBoundsSet)
            {
                pos = ClampToMapBounds(pos);
            }

            pos.z = -10f; // 确保摄像机在z=-10
            transform.position = pos;

            // 处理输入
            HandleInput();


        }

        // ========== 核心方法 ==========

        /// <summary>
        /// 设置地图边界（加载地图后调用）
        /// </summary>
        public void SetMapBounds(Bounds bounds)
        {
            _mapBounds = bounds;
            _isMapBoundsSet = true;
        }

        /// <summary>
        /// 自动适配地图大小
        /// </summary>
        public void FitToMap()
        {
            if (!GridSystem.HasInstance || !GridSystem.Instance.IsMapLoaded) return;

            var bounds = GridSystem.Instance.GetMapBounds();
            SetMapBounds(bounds);

            // 计算需要的正交大小
            float screenAspect = (float)Screen.width / Screen.height;
            float mapAspect = bounds.size.x / bounds.size.y;

            if (mapAspect > screenAspect)
            {
                // 地图更宽，以宽度适配
                _targetOrthoSize = bounds.size.x / (2f * screenAspect);
            }
            else
            {
                // 地图更高，以高度适配
                _targetOrthoSize = bounds.size.y / 2f;
            }

            _targetOrthoSize += _boundsPadding;
            _targetOrthoSize = Mathf.Clamp(_targetOrthoSize, _minOrthoSize, _maxOrthoSize);

            // 居中
            _targetPosition = new Vector3(bounds.center.x, bounds.center.y, -10f);
            transform.position = _targetPosition;
            _camera.orthographicSize = _targetOrthoSize;
        }

        /// <summary>
        /// 缩放
        /// </summary>
        public void Zoom(float delta)
        {
            _targetOrthoSize -= delta * _zoomSpeed;
            _targetOrthoSize = Mathf.Clamp(_targetOrthoSize, _minOrthoSize, _maxOrthoSize);
        }

        /// <summary>
        /// 移动到指定世界坐标
        /// </summary>
        public void MoveTo(Vector3 worldPos)
        {
            _targetPosition = new Vector3(worldPos.x, worldPos.y, -10f);
        }

        // ========== 输入处理 ==========

        private void HandleInput()
        {
            // 鼠标滚轮缩放
            float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scrollDelta) > 0.001f)
            {
                Zoom(scrollDelta * 5f);
            }

            // 鼠标中键拖拽
            if (Input.GetMouseButtonDown(2))
            {
                _isDragging = true;
                _dragStartWorldPos = _camera.ScreenToWorldPoint(Input.mousePosition);
            }
            if (Input.GetMouseButtonUp(2))
            {
                _isDragging = false;
            }
            if (_isDragging && Input.GetMouseButton(2))
            {
                Vector3 currentWorldPos = _camera.ScreenToWorldPoint(Input.mousePosition);
                Vector3 diff = _dragStartWorldPos - currentWorldPos;
                _targetPosition += diff;
            }

            // 触摸双指缩放
            if (Input.touchCount == 2)
            {
                var touch0 = Input.GetTouch(0);
                var touch1 = Input.GetTouch(1);

                float currentDist = Vector2.Distance(touch0.position, touch1.position);

                if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
                {
                    _lastPinchDistance = currentDist;
                }
                else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
                {
                    float delta = currentDist - _lastPinchDistance;
                    Zoom(delta * 0.01f);
                    _lastPinchDistance = currentDist;
                }
            }
        }

        // ========== 边界限制 ==========

        private Vector3 ClampToMapBounds(Vector3 pos)
        {
            float orthoHeight = _camera.orthographicSize;
            float orthoWidth = orthoHeight * _camera.aspect;

            float minX = _mapBounds.min.x + orthoWidth - _boundsPadding;
            float maxX = _mapBounds.max.x - orthoWidth + _boundsPadding;
            float minY = _mapBounds.min.y + orthoHeight - _boundsPadding;
            float maxY = _mapBounds.max.y - orthoHeight + _boundsPadding;

            // 地图比摄像机视口小时，居中
            if (minX > maxX) pos.x = _mapBounds.center.x;
            else pos.x = Mathf.Clamp(pos.x, minX, maxX);

            if (minY > maxY) pos.y = _mapBounds.center.y;
            else pos.y = Mathf.Clamp(pos.y, minY, maxY);

            return pos;
        }
    }
}
