
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Touch输入测试组件 — 验证微信小游戏平台的触控输入是否正常
/// 
/// 验证内容：
/// 1. 单点触控检测（按下/移动/抬起）
/// 2. 多点触控检测（触控点数量）
/// 3. 触控位置可视化（红色圆点跟随手指）
/// 4. 鼠标输入兼容（编辑器调试用）
/// </summary>
public class TouchTest : MonoBehaviour
{
    [Header("UI引用")]
    [Tooltip("触控位置指示器（一个Image，会跟随手指移动）")]
    public RectTransform touchIndicator;

    [Header("配置")]
    [Tooltip("是否在控制台输出详细日志")]
    public bool enableLog = true;

    // 触控统计数据
    private int totalTouchCount = 0;      // 累计触控次数
    private int maxSimultaneous = 0;       // 最大同时触控数
    private Vector2 lastTouchPosition;     // 最后一次触控位置
    private bool isTouching = false;       // 当前是否有触控

    // Canvas引用（用于坐标转换）
    private Canvas parentCanvas;
    private RectTransform canvasRect;

    /// <summary>
    /// 获取触控统计摘要（供主入口显示）
    /// </summary>
    public string GetTouchSummary()
    {
        return $"触控次数: {totalTouchCount} | 最大同时触控: {maxSimultaneous} | " +
               $"最后位置: ({lastTouchPosition.x:F0}, {lastTouchPosition.y:F0}) | " +
               $"状态: {(isTouching ? "触控中" : "空闲")}";
    }

    private void Start()
    {
        // 获取Canvas引用
        if (touchIndicator != null)
        {
            parentCanvas = touchIndicator.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                canvasRect = parentCanvas.GetComponent<RectTransform>();
            }
            // 初始隐藏指示器
            touchIndicator.gameObject.SetActive(false);
        }

        Debug.Log("[TouchTest] 触控测试组件已初始化");
    }

    private void Update()
    {
        // 优先处理触控输入（移动端/微信小游戏）
        if (Input.touchCount > 0)
        {
            HandleTouchInput();
        }
        // 兼容鼠标输入（编辑器调试）
        else
        {
            HandleMouseInput();
        }
    }

    /// <summary>
    /// 处理触控输入
    /// </summary>
    private void HandleTouchInput()
    {
        int touchCount = Input.touchCount;

        // 更新最大同时触控数
        if (touchCount > maxSimultaneous)
        {
            maxSimultaneous = touchCount;
        }

        // 处理第一个触控点
        Touch primaryTouch = Input.GetTouch(0);

        switch (primaryTouch.phase)
        {
            case TouchPhase.Began:
                totalTouchCount++;
                isTouching = true;
                lastTouchPosition = primaryTouch.position;
                ShowIndicator(primaryTouch.position);
                if (enableLog)
                {
                    Debug.Log($"[TouchTest] 触控开始 | 位置: {primaryTouch.position} | " +
                              $"触控点数: {touchCount} | 累计: {totalTouchCount}");
                }
                break;

            case TouchPhase.Moved:
                lastTouchPosition = primaryTouch.position;
                MoveIndicator(primaryTouch.position);
                if (enableLog)
                {
                    Debug.Log($"[TouchTest] 触控移动 | 位置: {primaryTouch.position} | " +
                              $"偏移: {primaryTouch.deltaPosition}");
                }
                break;

            case TouchPhase.Stationary:
                // 手指静止不动，不需要特殊处理
                break;

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                isTouching = false;
                HideIndicator();
                if (enableLog)
                {
                    Debug.Log($"[TouchTest] 触控结束 | 位置: {primaryTouch.position}");
                }
                break;
        }
    }

    /// <summary>
    /// 处理鼠标输入（编辑器兼容）
    /// </summary>
    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            totalTouchCount++;
            isTouching = true;
            lastTouchPosition = Input.mousePosition;
            ShowIndicator(Input.mousePosition);
            if (enableLog)
            {
                Debug.Log($"[TouchTest] 鼠标按下 | 位置: {Input.mousePosition} | 累计: {totalTouchCount}");
            }
        }
        else if (Input.GetMouseButton(0))
        {
            lastTouchPosition = Input.mousePosition;
            MoveIndicator(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isTouching = false;
            HideIndicator();
            if (enableLog)
            {
                Debug.Log($"[TouchTest] 鼠标抬起 | 位置: {Input.mousePosition}");
            }
        }
    }

    /// <summary>
    /// 在指定屏幕位置显示触控指示器
    /// </summary>
    private void ShowIndicator(Vector2 screenPosition)
    {
        if (touchIndicator == null) return;
        touchIndicator.gameObject.SetActive(true);
        MoveIndicator(screenPosition);
    }

    /// <summary>
    /// 移动触控指示器到指定屏幕位置
    /// </summary>
    private void MoveIndicator(Vector2 screenPosition)
    {
        if (touchIndicator == null || canvasRect == null) return;

        // 屏幕坐标转Canvas局部坐标
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPosition, parentCanvas.worldCamera, out localPoint))
        {
            touchIndicator.anchoredPosition = localPoint;
        }
    }

    /// <summary>
    /// 隐藏触控指示器
    /// </summary>
    private void HideIndicator()
    {
        if (touchIndicator == null) return;
        touchIndicator.gameObject.SetActive(false);
    }
}
