// ============================================================
// 文件名：UIAnimationSystem.cs
// 功能描述：UI动效系统 — 面板入场/退场动画、按钮反馈、数字跳动
//          Toast提示、飘字效果、货币飞入动画
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #276-310（UI动效）
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AetheraSurvivors.Framework;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>
    /// UI动效系统 — 提供通用的UI动画效果
    /// </summary>
    public class UIAnimationSystem : MonoSingleton<UIAnimationSystem>
    {
        // ========== Toast系统 ==========
        private Canvas _toastCanvas;
        private Queue<string> _toastQueue = new Queue<string>();
        private bool _isShowingToast;

        // ========== 飘字系统 ==========
        private Canvas _floatTextCanvas;

        protected override void OnInit()
        {
            CreateToastCanvas();
            CreateFloatTextCanvas();
            Debug.Log("[UIAnimationSystem] 初始化完成");
        }

        protected override void OnDispose()
        {
            StopAllCoroutines();
        }

        // ================================================================
        // 面板动画
        // ================================================================

        /// <summary>面板缩放弹入动画</summary>
        public void PlayPanelScaleIn(RectTransform panel, float duration = 0.3f, Action onComplete = null)
        {
            StartCoroutine(ScaleInCoroutine(panel, duration, onComplete));
        }

        /// <summary>面板缩放弹出动画</summary>
        public void PlayPanelScaleOut(RectTransform panel, float duration = 0.2f, Action onComplete = null)
        {
            StartCoroutine(ScaleOutCoroutine(panel, duration, onComplete));
        }

        /// <summary>面板从右侧滑入</summary>
        public void PlaySlideInFromRight(RectTransform panel, float duration = 0.3f, Action onComplete = null)
        {
            StartCoroutine(SlideInCoroutine(panel, new Vector2(1f, 0f), duration, onComplete));
        }

        /// <summary>面板从底部滑入</summary>
        public void PlaySlideInFromBottom(RectTransform panel, float duration = 0.3f, Action onComplete = null)
        {
            StartCoroutine(SlideInCoroutine(panel, new Vector2(0f, -1f), duration, onComplete));
        }

        /// <summary>面板淡入</summary>
        public void PlayFadeIn(CanvasGroup cg, float duration = 0.3f, Action onComplete = null)
        {
            StartCoroutine(FadeCoroutine(cg, 0f, 1f, duration, onComplete));
        }

        /// <summary>面板淡出</summary>
        public void PlayFadeOut(CanvasGroup cg, float duration = 0.2f, Action onComplete = null)
        {
            StartCoroutine(FadeCoroutine(cg, 1f, 0f, duration, onComplete));
        }

        // ================================================================
        // 按钮反馈
        // ================================================================

        /// <summary>按钮点击缩放反馈</summary>
        public void PlayButtonPunch(RectTransform btn, float scale = 0.9f, float duration = 0.15f)
        {
            StartCoroutine(ButtonPunchCoroutine(btn, scale, duration));
        }

        /// <summary>按钮摇晃（错误操作反馈）</summary>
        public void PlayButtonShake(RectTransform btn, float intensity = 10f, float duration = 0.3f)
        {
            StartCoroutine(ShakeCoroutine(btn, intensity, duration));
        }

        // ================================================================
        // 数字跳动
        // ================================================================

        /// <summary>数字跳动动画（从oldValue到newValue）</summary>
        public void PlayNumberRoll(Text text, long oldValue, long newValue, float duration = 0.5f,
            string format = "{0}", Color? flashColor = null)
        {
            StartCoroutine(NumberRollCoroutine(text, oldValue, newValue, duration, format, flashColor));
        }

        // ================================================================
        // Toast提示
        // ================================================================

        /// <summary>显示Toast提示</summary>
        public void ShowToast(string message, float duration = 2f)
        {
            _toastQueue.Enqueue(message);
            if (!_isShowingToast)
            {
                StartCoroutine(ShowToastCoroutine(duration));
            }
        }

        // ================================================================
        // 飘字效果
        // ================================================================

        /// <summary>显示飘字（如+100金币）</summary>
        public void ShowFloatText(string text, Vector3 worldPos, Color color, float duration = 1.5f)
        {
            StartCoroutine(FloatTextCoroutine(text, worldPos, color, duration));
        }

        /// <summary>显示屏幕中央飘字</summary>
        public void ShowCenterFloatText(string text, Color color, float duration = 1.5f)
        {
            StartCoroutine(CenterFloatTextCoroutine(text, color, duration));
        }

        // ================================================================
        // 货币飞入动画
        // ================================================================

        /// <summary>货币图标飞入动画（从源位置飞到目标位置）</summary>
        public void PlayCurrencyFlyIn(Vector2 fromScreenPos, Vector2 toScreenPos,
            string currencyIcon, int count = 5, float duration = 0.8f)
        {
            StartCoroutine(CurrencyFlyCoroutine(fromScreenPos, toScreenPos, currencyIcon, count, duration));
        }

        // ================================================================
        // 协程实现
        // ================================================================

        private IEnumerator ScaleInCoroutine(RectTransform panel, float duration, Action onComplete)
        {
            float timer = 0f;
            panel.localScale = Vector3.zero;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                float eased = EaseOutBack(t);
                panel.localScale = Vector3.one * eased;
                yield return null;
            }

            panel.localScale = Vector3.one;
            onComplete?.Invoke();
        }

        private IEnumerator ScaleOutCoroutine(RectTransform panel, float duration, Action onComplete)
        {
            float timer = 0f;
            panel.localScale = Vector3.one;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                float eased = EaseInBack(t);
                panel.localScale = Vector3.one * (1f - eased);
                yield return null;
            }

            panel.localScale = Vector3.zero;
            onComplete?.Invoke();
        }

        private IEnumerator SlideInCoroutine(RectTransform panel, Vector2 direction, float duration, Action onComplete)
        {
            float timer = 0f;
            Vector2 startOffset = direction * Screen.width;
            Vector2 originalPos = panel.anchoredPosition;

            panel.anchoredPosition = originalPos + startOffset;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                float eased = EaseOutCubic(t);
                panel.anchoredPosition = Vector2.Lerp(originalPos + startOffset, originalPos, eased);
                yield return null;
            }

            panel.anchoredPosition = originalPos;
            onComplete?.Invoke();
        }

        private IEnumerator FadeCoroutine(CanvasGroup cg, float from, float to, float duration, Action onComplete)
        {
            float timer = 0f;
            cg.alpha = from;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                cg.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            cg.alpha = to;
            onComplete?.Invoke();
        }

        private IEnumerator ButtonPunchCoroutine(RectTransform btn, float scale, float duration)
        {
            Vector3 originalScale = btn.localScale;
            float halfDuration = duration * 0.5f;

            // 缩小
            float timer = 0f;
            while (timer < halfDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / halfDuration);
                btn.localScale = Vector3.Lerp(originalScale, originalScale * scale, t);
                yield return null;
            }

            // 弹回
            timer = 0f;
            while (timer < halfDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / halfDuration);
                float eased = EaseOutBack(t);
                btn.localScale = Vector3.Lerp(originalScale * scale, originalScale, eased);
                yield return null;
            }

            btn.localScale = originalScale;
        }

        private IEnumerator ShakeCoroutine(RectTransform target, float intensity, float duration)
        {
            Vector2 originalPos = target.anchoredPosition;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float decay = 1f - (timer / duration);
                float offsetX = UnityEngine.Random.Range(-intensity, intensity) * decay;
                target.anchoredPosition = originalPos + new Vector2(offsetX, 0);
                yield return null;
            }

            target.anchoredPosition = originalPos;
        }

        private IEnumerator NumberRollCoroutine(Text text, long oldValue, long newValue,
            float duration, string format, Color? flashColor)
        {
            Color originalColor = text.color;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                long current = (long)Mathf.Lerp(oldValue, newValue, EaseOutCubic(t));
                text.text = string.Format(format, current);

                // 闪烁颜色
                if (flashColor.HasValue)
                {
                    float flash = Mathf.PingPong(timer * 6f, 1f);
                    text.color = Color.Lerp(originalColor, flashColor.Value, flash * (1f - t));
                }

                yield return null;
            }

            text.text = string.Format(format, newValue);
            text.color = originalColor;
        }

        private IEnumerator ShowToastCoroutine(float duration)
        {
            _isShowingToast = true;

            while (_toastQueue.Count > 0)
            {
                string message = _toastQueue.Dequeue();

                // 创建Toast对象
                var toastObj = new GameObject("Toast");
                toastObj.transform.SetParent(_toastCanvas.transform, false);
                var toastRect = toastObj.AddComponent<RectTransform>();
                toastRect.anchorMin = new Vector2(0.15f, 0.45f);
                toastRect.anchorMax = new Vector2(0.85f, 0.55f);
                toastRect.offsetMin = Vector2.zero;
                toastRect.offsetMax = Vector2.zero;

                var bg = toastObj.AddComponent<Image>();
                bg.color = new Color(0.1f, 0.1f, 0.2f, 0.9f);

                var txtObj = new GameObject("Txt");
                txtObj.transform.SetParent(toastObj.transform, false);
                var txtRect = txtObj.AddComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.offsetMin = Vector2.zero;
                txtRect.offsetMax = Vector2.zero;

                var txt = txtObj.AddComponent<Text>();
                txt.text = message;
                txt.fontSize = 18;
                txt.color = Color.white;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.font = Font.CreateDynamicFontFromOSFont("Arial", 18);

                var cg = toastObj.AddComponent<CanvasGroup>();

                // 淡入
                float fadeIn = 0.2f;
                float timer = 0f;
                while (timer < fadeIn)
                {
                    timer += Time.unscaledDeltaTime;
                    cg.alpha = Mathf.Clamp01(timer / fadeIn);
                    yield return null;
                }

                // 停留
                yield return new WaitForSecondsRealtime(duration);

                // 淡出
                timer = 0f;
                float fadeOut = 0.3f;
                while (timer < fadeOut)
                {
                    timer += Time.unscaledDeltaTime;
                    cg.alpha = 1f - Mathf.Clamp01(timer / fadeOut);
                    toastRect.anchoredPosition += new Vector2(0, Time.unscaledDeltaTime * 50f);
                    yield return null;
                }

                Destroy(toastObj);
            }

            _isShowingToast = false;
        }

        private IEnumerator FloatTextCoroutine(string text, Vector3 worldPos, Color color, float duration)
        {
            var cam = Camera.main;
            if (cam == null) yield break;

            var obj = new GameObject("FloatText");
            obj.transform.SetParent(_floatTextCanvas.transform, false);
            var rect = obj.AddComponent<RectTransform>();

            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = 20;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 20);
            txt.raycastTarget = false;

            var outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(1, -1);

            float timer = 0f;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;

                // 上浮
                float yOffset = t * 80f;
                rect.position = screenPos + new Vector3(0, yOffset, 0);

                // 淡出
                float alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                txt.color = new Color(color.r, color.g, color.b, alpha);

                // 缩放
                float scale = t < 0.1f ? EaseOutBack(t / 0.1f) : 1f;
                rect.localScale = Vector3.one * scale;

                yield return null;
            }

            Destroy(obj);
        }

        private IEnumerator CenterFloatTextCoroutine(string text, Color color, float duration)
        {
            var obj = new GameObject("CenterFloat");
            obj.transform.SetParent(_floatTextCanvas.transform, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.45f);
            rect.anchorMax = new Vector2(0.9f, 0.55f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = 28;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 28);
            txt.raycastTarget = false;

            var outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(2, -2);

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = timer / duration;

                // 弹入 → 停留 → 淡出上浮
                if (t < 0.15f)
                {
                    float s = EaseOutBack(t / 0.15f);
                    rect.localScale = Vector3.one * s;
                }
                else if (t > 0.7f)
                {
                    float fadeT = (t - 0.7f) / 0.3f;
                    txt.color = new Color(color.r, color.g, color.b, 1f - fadeT);
                    rect.anchoredPosition += new Vector2(0, Time.unscaledDeltaTime * 30f);
                }

                yield return null;
            }

            Destroy(obj);
        }

        private IEnumerator CurrencyFlyCoroutine(Vector2 from, Vector2 to,
            string icon, int count, float duration)
        {
            var flyObjects = new List<GameObject>();

            for (int i = 0; i < count; i++)
            {
                var obj = new GameObject($"CurrencyFly_{i}");
                obj.transform.SetParent(_floatTextCanvas.transform, false);
                var rect = obj.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(30, 30);

                var txt = obj.AddComponent<Text>();
                txt.text = icon;
                txt.fontSize = 24;
                txt.color = Color.white;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.font = Font.CreateDynamicFontFromOSFont("Arial", 24);

                flyObjects.Add(obj);
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);

                for (int i = 0; i < flyObjects.Count; i++)
                {
                    if (flyObjects[i] == null) continue;

                    float delay = i * 0.05f;
                    float localT = Mathf.Clamp01((timer - delay) / (duration - delay));

                    if (localT <= 0) continue;

                    float eased = EaseInOutCubic(localT);

                    // 添加随机弧度
                    float arcHeight = 50f * Mathf.Sin(localT * Mathf.PI);
                    float randomX = (i - count / 2f) * 15f * (1f - localT);

                    Vector2 pos = Vector2.Lerp(from, to, eased);
                    pos.y += arcHeight;
                    pos.x += randomX;

                    flyObjects[i].GetComponent<RectTransform>().position = pos;
                }

                yield return null;
            }

            for (int i = 0; i < flyObjects.Count; i++)
            {
                if (flyObjects[i] != null) Destroy(flyObjects[i]);
            }
        }

        // ================================================================
        // Canvas创建
        // ================================================================

        private void CreateToastCanvas()
        {
            var obj = new GameObject("[ToastCanvas]");
            obj.transform.SetParent(transform);
            _toastCanvas = obj.AddComponent<Canvas>();
            _toastCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _toastCanvas.sortingOrder = 998;

            var scaler = obj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            obj.AddComponent<GraphicRaycaster>();
        }

        private void CreateFloatTextCanvas()
        {
            var obj = new GameObject("[FloatTextCanvas]");
            obj.transform.SetParent(transform);
            _floatTextCanvas = obj.AddComponent<Canvas>();
            _floatTextCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _floatTextCanvas.sortingOrder = 997;

            var scaler = obj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // ================================================================
        // 缓动函数
        // ================================================================

        private float EaseOutBack(float t)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private float EaseInBack(float t)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        private float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private float EaseInOutCubic(float t)
        {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }
    }
}
