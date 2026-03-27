// ============================================================
// 文件名：UIAnimator.cs
// 功能描述：轻量级UI动效引擎 — 提供缩放、淡入淡出、滑入滑出、
//          弹跳、脉冲、抖动等常用UI动效
//          零GC分配、不依赖第三方库、支持链式调用
// 创建时间：2026-03-27
// 所属模块：Battle/Visual
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Visual
{
    /// <summary>
    /// UI动效类型
    /// </summary>
    public enum UIAnimType
    {
        Scale,          // 缩放
        Fade,           // 淡入淡出（CanvasGroup.alpha）
        SlideX,         // 水平滑入
        SlideY,         // 垂直滑入
        Rotate,         // 旋转
        Color,          // 颜色变化
        AnchoredPos,    // 锚点位置
        SizeDelta,      // 尺寸变化
    }

    /// <summary>
    /// 缓动函数类型
    /// </summary>
    public enum EaseType
    {
        Linear,
        EaseInQuad,
        EaseOutQuad,
        EaseInOutQuad,
        EaseInCubic,
        EaseOutCubic,
        EaseInOutCubic,
        EaseOutBack,        // 回弹（超出目标后回弹）
        EaseOutElastic,     // 弹性（弹簧效果）
        EaseOutBounce,      // 弹跳（落地弹跳）
        EaseInExpo,
        EaseOutExpo,
        Punch,              // 冲击（先超出后回归）
    }

    /// <summary>
    /// 单个动效实例
    /// </summary>
    public class UITween
    {
        public UIAnimType animType;
        public RectTransform target;
        public CanvasGroup canvasGroup;
        public Graphic graphic;

        public float duration;
        public float elapsed;
        public float delay;
        public EaseType easeType;

        // 通用起止值
        public Vector3 fromVec3;
        public Vector3 toVec3;
        public float fromFloat;
        public float toFloat;
        public Color fromColor;
        public Color toColor;

        // 回调
        public System.Action onComplete;
        public System.Action<float> onUpdate;

        // 状态
        public bool isActive;
        public bool isDelaying;
        public bool useUnscaledTime;
        public int loopCount;       // -1=无限循环, 0=不循环, >0=循环次数
        public bool pingPong;       // 来回播放
        public bool isReversing;    // 当前是否在反向播放

        /// <summary>重置为初始状态（对象池复用）</summary>
        public void Reset()
        {
            target = null;
            canvasGroup = null;
            graphic = null;
            duration = 0.3f;
            elapsed = 0f;
            delay = 0f;
            easeType = EaseType.EaseOutQuad;
            fromVec3 = Vector3.zero;
            toVec3 = Vector3.zero;
            fromFloat = 0f;
            toFloat = 0f;
            fromColor = Color.white;
            toColor = Color.white;
            onComplete = null;
            onUpdate = null;
            isActive = false;
            isDelaying = false;
            useUnscaledTime = false;
            loopCount = 0;
            pingPong = false;
            isReversing = false;
        }

        // ========== 链式调用API ==========

        public UITween SetEase(EaseType ease) { easeType = ease; return this; }
        public UITween SetDelay(float d) { delay = d; isDelaying = d > 0; return this; }
        public UITween SetUnscaled(bool unscaled = true) { useUnscaledTime = unscaled; return this; }
        public UITween SetLoop(int count, bool pp = false) { loopCount = count; pingPong = pp; return this; }
        public UITween OnComplete(System.Action cb) { onComplete = cb; return this; }
        public UITween OnUpdate(System.Action<float> cb) { onUpdate = cb; return this; }
    }

    /// <summary>
    /// UI动效管理器 — 全局单例，驱动所有UI动效
    /// 
    /// 使用方式：
    ///   UIAnimator.ScaleTo(rect, Vector3.one, 0.3f).SetEase(EaseType.EaseOutBack);
    ///   UIAnimator.FadeIn(canvasGroup, 0.25f);
    ///   UIAnimator.SlideFromBottom(rect, 100f, 0.4f);
    ///   UIAnimator.Punch(rect, 0.15f);
    /// </summary>
    public class UIAnimator : MonoBehaviour
    {
        // ========== 单例 ==========
        private static UIAnimator _instance;
        public static UIAnimator Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[UIAnimator]");
                    _instance = go.AddComponent<UIAnimator>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ========== 动效池 ==========
        private const int POOL_SIZE = 64;
        private readonly List<UITween> _activeTweens = new List<UITween>(32);
        private readonly Stack<UITween> _pool = new Stack<UITween>(POOL_SIZE);

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // 预热对象池
            for (int i = 0; i < POOL_SIZE; i++)
                _pool.Push(new UITween());
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ========== 核心更新 ==========

        private void LateUpdate()
        {
            for (int i = _activeTweens.Count - 1; i >= 0; i--)
            {
                var tw = _activeTweens[i];
                if (!tw.isActive || tw.target == null)
                {
                    RecycleTween(tw, i);
                    continue;
                }

                float dt = tw.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

                // 延迟阶段
                if (tw.isDelaying)
                {
                    tw.delay -= dt;
                    if (tw.delay > 0) continue;
                    tw.isDelaying = false;
                    dt = -tw.delay; // 补偿超出的时间
                }

                tw.elapsed += dt;
                float t = Mathf.Clamp01(tw.elapsed / tw.duration);

                // PingPong反向
                float easedT = tw.isReversing ? Ease(1f - t, tw.easeType) : Ease(t, tw.easeType);

                // 应用动效
                ApplyTween(tw, easedT);
                tw.onUpdate?.Invoke(t);

                // 完成检测
                if (t >= 1f)
                {
                    if (tw.pingPong && !tw.isReversing)
                    {
                        tw.isReversing = true;
                        tw.elapsed = 0f;
                    }
                    else if (tw.loopCount != 0)
                    {
                        if (tw.loopCount > 0) tw.loopCount--;
                        tw.elapsed = 0f;
                        tw.isReversing = false;
                    }
                    else
                    {
                        // 确保最终值精确
                        ApplyTween(tw, tw.isReversing ? 0f : 1f);
                        tw.onComplete?.Invoke();
                        RecycleTween(tw, i);
                    }
                }
            }
        }

        // ========== 动效应用 ==========

        private void ApplyTween(UITween tw, float t)
        {
            switch (tw.animType)
            {
                case UIAnimType.Scale:
                    tw.target.localScale = Vector3.LerpUnclamped(tw.fromVec3, tw.toVec3, t);
                    break;

                case UIAnimType.Fade:
                    if (tw.canvasGroup != null)
                        tw.canvasGroup.alpha = Mathf.LerpUnclamped(tw.fromFloat, tw.toFloat, t);
                    break;

                case UIAnimType.SlideX:
                    var posX = tw.target.anchoredPosition;
                    posX.x = Mathf.LerpUnclamped(tw.fromFloat, tw.toFloat, t);
                    tw.target.anchoredPosition = posX;
                    break;

                case UIAnimType.SlideY:
                    var posY = tw.target.anchoredPosition;
                    posY.y = Mathf.LerpUnclamped(tw.fromFloat, tw.toFloat, t);
                    tw.target.anchoredPosition = posY;
                    break;

                case UIAnimType.Rotate:
                    tw.target.localEulerAngles = Vector3.LerpUnclamped(tw.fromVec3, tw.toVec3, t);
                    break;

                case UIAnimType.Color:
                    if (tw.graphic != null)
                        tw.graphic.color = Color.LerpUnclamped(tw.fromColor, tw.toColor, t);
                    break;

                case UIAnimType.AnchoredPos:
                    tw.target.anchoredPosition = Vector2.LerpUnclamped(
                        new Vector2(tw.fromVec3.x, tw.fromVec3.y),
                        new Vector2(tw.toVec3.x, tw.toVec3.y), t);
                    break;

                case UIAnimType.SizeDelta:
                    tw.target.sizeDelta = Vector2.LerpUnclamped(
                        new Vector2(tw.fromVec3.x, tw.fromVec3.y),
                        new Vector2(tw.toVec3.x, tw.toVec3.y), t);
                    break;
            }
        }

        // ========== 对象池 ==========

        private UITween GetTween()
        {
            UITween tw = _pool.Count > 0 ? _pool.Pop() : new UITween();
            tw.Reset();
            tw.isActive = true;
            _activeTweens.Add(tw);
            return tw;
        }

        private void RecycleTween(UITween tw, int index)
        {
            tw.isActive = false;
            tw.Reset();
            _activeTweens.RemoveAt(index);
            if (_pool.Count < POOL_SIZE * 2)
                _pool.Push(tw);
        }

        // ========== 公共API — 缩放动效 ==========

        /// <summary>缩放到目标值</summary>
        public static UITween ScaleTo(RectTransform target, Vector3 to, float duration = 0.3f)
        {
            var tw = Instance.GetTween();
            tw.animType = UIAnimType.Scale;
            tw.target = target;
            tw.fromVec3 = target.localScale;
            tw.toVec3 = to;
            tw.duration = duration;
            return tw;
        }

        /// <summary>从指定值缩放到目标值</summary>
        public static UITween ScaleFromTo(RectTransform target, Vector3 from, Vector3 to, float duration = 0.3f)
        {
            target.localScale = from;
            var tw = Instance.GetTween();
            tw.animType = UIAnimType.Scale;
            tw.target = target;
            tw.fromVec3 = from;
            tw.toVec3 = to;
            tw.duration = duration;
            return tw;
        }

        /// <summary>弹出效果（从0缩放到1，带回弹）</summary>
        public static UITween PopIn(RectTransform target, float duration = 0.35f)
        {
            return ScaleFromTo(target, Vector3.zero, Vector3.one, duration)
                .SetEase(EaseType.EaseOutBack);
        }

        /// <summary>收缩消失效果</summary>
        public static UITween PopOut(RectTransform target, float duration = 0.2f)
        {
            return ScaleTo(target, Vector3.zero, duration)
                .SetEase(EaseType.EaseInCubic);
        }

        /// <summary>冲击缩放（先放大后恢复，用于强调）</summary>
        public static UITween PunchScale(RectTransform target, float intensity = 0.2f, float duration = 0.3f)
        {
            Vector3 original = target.localScale;
            return ScaleFromTo(target, original * (1f + intensity), original, duration)
                .SetEase(EaseType.EaseOutElastic);
        }

        /// <summary>脉冲呼吸效果（循环缩放）</summary>
        public static UITween Pulse(RectTransform target, float minScale = 0.95f, float maxScale = 1.05f, float duration = 0.8f)
        {
            return ScaleFromTo(target, Vector3.one * minScale, Vector3.one * maxScale, duration)
                .SetEase(EaseType.EaseInOutQuad)
                .SetLoop(-1, true);
        }

        // ========== 公共API — 淡入淡出 ==========

        /// <summary>确保目标有CanvasGroup组件</summary>
        private static CanvasGroup EnsureCanvasGroup(RectTransform target)
        {
            var cg = target.GetComponent<CanvasGroup>();
            if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();
            return cg;
        }

        /// <summary>淡入（alpha 0→1）</summary>
        public static UITween FadeIn(RectTransform target, float duration = 0.25f)
        {
            var cg = EnsureCanvasGroup(target);
            cg.alpha = 0f;
            var tw = Instance.GetTween();
            tw.animType = UIAnimType.Fade;
            tw.target = target;
            tw.canvasGroup = cg;
            tw.fromFloat = 0f;
            tw.toFloat = 1f;
            tw.duration = duration;
            return tw;
        }

        /// <summary>淡出（alpha 1→0）</summary>
        public static UITween FadeOut(RectTransform target, float duration = 0.2f)
        {
            var cg = EnsureCanvasGroup(target);
            var tw = Instance.GetTween();
            tw.animType = UIAnimType.Fade;
            tw.target = target;
            tw.canvasGroup = cg;
            tw.fromFloat = cg.alpha;
            tw.toFloat = 0f;
            tw.duration = duration;
            return tw;
        }

        /// <summary>淡入淡出到指定alpha</summary>
        public static UITween FadeTo(RectTransform target, float toAlpha, float duration = 0.25f)
        {
            var cg = EnsureCanvasGroup(target);
            var tw = Instance.GetTween();
            tw.animType = UIAnimType.Fade;
            tw.target = target;
            tw.canvasGroup = cg;
            tw.fromFloat = cg.alpha;
            tw.toFloat = toAlpha;
            tw.duration = duration;
            return tw;
        }

        // ========== 公共API — 滑入滑出 ==========

        /// <summary>从底部滑入</summary>
        public static UITween SlideFromBottom(RectTransform target, float distance = 120f, float duration = 0.35f)
        {
            var pos = target.anchoredPosition;
            float originalY = pos.y;
            var tw = Instance.GetTween();
            tw.animType = UIAnimType.SlideY;
            tw.target = target;
            tw.fromFloat = originalY - distance;
            tw.toFloat = originalY;
            tw.duration = duration;
            tw.easeType = EaseType.EaseOutCubic;
            pos.y = tw.fromFloat;
            target.anchoredPosition = pos;
            return tw;
        }

        /// <summary>从顶部滑入</summary>
        public static UITween SlideFromTop(RectTransform target, float distance = 80f, float duration = 0.35f)
        {
            var pos = target.anchoredPosition;
            float originalY = pos.y;
            var tw = Instance.GetTween();
            tw.animType = UIAnimType.SlideY;
            tw.target = target;
            tw.fromFloat = originalY + distance;
            tw.toFloat = originalY;
            tw.duration = duration;
            tw.easeType = EaseType.EaseOutCubic;
            pos.y = tw.fromFloat;
            target.anchoredPosition = pos;
            return tw;
        }

        /// <summary>从左侧滑入</summary>
        public static UITween SlideFromLeft(RectTransform target, float distance = 200f, float duration = 0.35f)
        {
            var pos = target.anchoredPosition;
            float originalX = pos.x;
            var tw = Instance.GetTween();
            tw.animType = UIAnimType.SlideX;
            tw.target = target;
            tw.fromFloat = originalX - distance;
            tw.toFloat = originalX;
            tw.duration = duration;
            tw.easeType = EaseType.EaseOutCubic;
            pos.x = tw.fromFloat;
            target.anchoredPosition = pos;
            return tw;
        }

        /// <summary>从右侧滑入</summary>
        public static UITween SlideFromRight(RectTransform target, float distance = 200f, float duration = 0.35f)
        {
            var pos = target.anchoredPosition;
            float originalX = pos.x;
            var tw = Instance.GetTween();
            tw.animType = UIAnimType.SlideX;
            tw.target = target;
            tw.fromFloat = originalX + distance;
            tw.toFloat = originalX;
            tw.duration = duration;
            tw.easeType = EaseType.EaseOutCubic;
            pos.x = tw.fromFloat;
            target.anchoredPosition = pos;
            return tw;
        }

        /// <summary>滑出到底部</summary>
        public static UITween SlideToBottom(RectTransform target, float distance = 120f, float duration = 0.25f)
        {
            var pos = target.anchoredPosition;
            var tw = Instance.GetTween();
            tw.animType = UIAnimType.SlideY;
            tw.target = target;
            tw.fromFloat = pos.y;
            tw.toFloat = pos.y - distance;
            tw.duration = duration;
            tw.easeType = EaseType.EaseInCubic;
            return tw;
        }

        // ========== 公共API — 抖动效果 ==========

        /// <summary>水平抖动（用于错误提示）</summary>
        public static UITween ShakeX(RectTransform target, float intensity = 10f, float duration = 0.4f)
        {
            float originalX = target.anchoredPosition.x;
            var tw = Instance.GetTween();
            tw.animType = UIAnimType.SlideX;
            tw.target = target;
            tw.fromFloat = originalX;
            tw.toFloat = originalX;
            tw.duration = duration;
            tw.easeType = EaseType.Linear;

            // 使用onUpdate实现抖动
            tw.onUpdate = (t) =>
            {
                if (tw.target == null) return;
                float shake = Mathf.Sin(t * Mathf.PI * 8f) * intensity * (1f - t);
                var p = tw.target.anchoredPosition;
                p.x = originalX + shake;
                tw.target.anchoredPosition = p;
            };
            tw.onComplete = () =>
            {
                if (target != null)
                {
                    var p = target.anchoredPosition;
                    p.x = originalX;
                    target.anchoredPosition = p;
                }
            };
            return tw;
        }

        // ========== 公共API — 颜色动效 ==========

        /// <summary>颜色渐变</summary>
        public static UITween ColorTo(Graphic graphic, Color to, float duration = 0.3f)
        {
            var tw = Instance.GetTween();
            tw.animType = UIAnimType.Color;
            tw.target = graphic.rectTransform;
            tw.graphic = graphic;
            tw.fromColor = graphic.color;
            tw.toColor = to;
            tw.duration = duration;
            return tw;
        }

        /// <summary>颜色闪烁（高亮后恢复）</summary>
        public static UITween ColorFlash(Graphic graphic, Color flashColor, float duration = 0.4f)
        {
            Color original = graphic.color;
            graphic.color = flashColor;
            var tw = Instance.GetTween();
            tw.animType = UIAnimType.Color;
            tw.target = graphic.rectTransform;
            tw.graphic = graphic;
            tw.fromColor = flashColor;
            tw.toColor = original;
            tw.duration = duration;
            tw.easeType = EaseType.EaseOutQuad;
            return tw;
        }

        // ========== 公共API — 组合动效（常用预设） ==========

        /// <summary>面板弹出（缩放+淡入）</summary>
        public static void PanelShow(RectTransform panel, float duration = 0.35f)
        {
            panel.gameObject.SetActive(true);
            PopIn(panel, duration);
            FadeIn(panel, duration * 0.6f);
        }

        /// <summary>面板关闭（缩放+淡出）</summary>
        public static void PanelHide(RectTransform panel, float duration = 0.2f, System.Action onDone = null)
        {
            PopOut(panel, duration);
            FadeOut(panel, duration).OnComplete(() =>
            {
                if (panel != null) panel.gameObject.SetActive(false);
                onDone?.Invoke();
            });
        }

        /// <summary>通知弹出（从上方滑入+淡入，自动消失）</summary>
        public static void NoticeShow(RectTransform notice, float stayDuration = 2f)
        {
            notice.gameObject.SetActive(true);
            notice.localScale = Vector3.one;
            SlideFromTop(notice, 40f, 0.3f);
            FadeIn(notice, 0.2f);
            PunchScale(notice, 0.08f, 0.25f).SetDelay(0.1f);
        }

        /// <summary>通知消失（淡出+缩小）</summary>
        public static void NoticeHide(RectTransform notice, float duration = 0.3f)
        {
            FadeOut(notice, duration);
            ScaleTo(notice, Vector3.one * 0.9f, duration)
                .SetEase(EaseType.EaseInQuad)
                .OnComplete(() =>
                {
                    if (notice != null)
                    {
                        notice.gameObject.SetActive(false);
                        notice.localScale = Vector3.one;
                    }
                });
        }

        /// <summary>按钮点击反馈（快速缩小再弹回）</summary>
        public static void ButtonPress(RectTransform btn)
        {
            ScaleFromTo(btn, Vector3.one * 0.88f, Vector3.one, 0.2f)
                .SetEase(EaseType.EaseOutBack);
        }

        /// <summary>金币/数值变化强调（放大+颜色闪烁）</summary>
        public static void ValueChangeEmphasis(Text text, Color flashColor = default)
        {
            if (flashColor == default) flashColor = new Color(1f, 1f, 0.5f);
            PunchScale(text.rectTransform, 0.15f, 0.3f);
            ColorFlash(text, flashColor, 0.4f);
        }

        /// <summary>列表项依次入场（带延迟的滑入）</summary>
        public static void StaggeredSlideIn(RectTransform[] items, float staggerDelay = 0.06f,
            float duration = 0.3f, float distance = 60f)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                items[i].localScale = Vector3.one;
                SlideFromBottom(items[i], distance, duration).SetDelay(staggerDelay * i);
                FadeIn(items[i], duration * 0.7f).SetDelay(staggerDelay * i);
            }
        }

        // ========== 工具方法 — 取消动效 ==========

        /// <summary>取消目标上的所有动效</summary>
        public static void Kill(RectTransform target)
        {
            if (_instance == null) return;
            for (int i = _instance._activeTweens.Count - 1; i >= 0; i--)
            {
                if (_instance._activeTweens[i].target == target)
                {
                    _instance._activeTweens[i].isActive = false;
                }
            }
        }

        /// <summary>取消所有动效</summary>
        public static void KillAll()
        {
            if (_instance == null) return;
            for (int i = _instance._activeTweens.Count - 1; i >= 0; i--)
            {
                _instance._activeTweens[i].isActive = false;
            }
        }

        // ========== 缓动函数 ==========

        public static float Ease(float t, EaseType type)
        {
            switch (type)
            {
                case EaseType.Linear:
                    return t;

                case EaseType.EaseInQuad:
                    return t * t;

                case EaseType.EaseOutQuad:
                    return t * (2f - t);

                case EaseType.EaseInOutQuad:
                    return t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

                case EaseType.EaseInCubic:
                    return t * t * t;

                case EaseType.EaseOutCubic:
                    float f = t - 1f;
                    return f * f * f + 1f;

                case EaseType.EaseInOutCubic:
                    return t < 0.5f ? 4f * t * t * t : (t - 1f) * (2f * t - 2f) * (2f * t - 2f) + 1f;

                case EaseType.EaseOutBack:
                    float c1 = 1.70158f;
                    float c3 = c1 + 1f;
                    float t1 = t - 1f;
                    return 1f + c3 * t1 * t1 * t1 + c1 * t1 * t1;

                case EaseType.EaseOutElastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    float p = 0.3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p) + 1f;

                case EaseType.EaseOutBounce:
                    return EaseOutBounceFunc(t);

                case EaseType.EaseInExpo:
                    return t <= 0f ? 0f : Mathf.Pow(2f, 10f * (t - 1f));

                case EaseType.EaseOutExpo:
                    return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);

                case EaseType.Punch:
                    // 冲击效果：先超出后回归
                    if (t < 0.5f)
                        return Mathf.Sin(t * Mathf.PI) * 1.2f;
                    else
                        return Mathf.Sin(t * Mathf.PI) * (1f - (t - 0.5f) * 2f) * 0.5f;

                default:
                    return t;
            }
        }

        private static float EaseOutBounceFunc(float t)
        {
            if (t < 1f / 2.75f)
                return 7.5625f * t * t;
            else if (t < 2f / 2.75f)
            {
                t -= 1.5f / 2.75f;
                return 7.5625f * t * t + 0.75f;
            }
            else if (t < 2.5f / 2.75f)
            {
                t -= 2.25f / 2.75f;
                return 7.5625f * t * t + 0.9375f;
            }
            else
            {
                t -= 2.625f / 2.75f;
                return 7.5625f * t * t + 0.984375f;
            }
        }
    }
}
