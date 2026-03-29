// ============================================================
// 文件名：GachaAnimation.cs
// 功能描述：抽卡动画系统 — 十连翻牌效果、SSR出场特效、跳过按钮
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #262
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle;
using AetheraSurvivors.Battle.Visual;


namespace AetheraSurvivors.MetaGame
{
    /// <summary>
    /// 抽卡动画状态
    /// </summary>
    public enum GachaAnimState
    {
        Idle,           // 空闲
        Summoning,      // 召唤中（光效聚集）
        Revealing,      // 翻牌中
        SSRCutscene,    // SSR特写
        ShowResult,     // 展示结果
        Finished        // 完成
    }

    /// <summary>
    /// 抽卡动画完成事件
    /// </summary>
    public struct GachaAnimFinishedEvent : IEvent
    {
        public int ResultCount;
        public bool WasSkipped;
    }

    /// <summary>
    /// 抽卡动画管理器
    /// 
    /// 功能：
    /// 1. 单抽动画：光效聚集→卡牌翻转→稀有度特效→结果展示
    /// 2. 十连动画：光效聚集→10张卡牌依次翻转→SSR特写→结果汇总
    /// 3. SSR出场特效：金色光柱+粒子爆发+屏幕震动+慢动作
    /// 4. 跳过按钮：任意时刻可跳过直接显示结果
    /// 5. 稀有度视觉区分：R=蓝光, SR=紫光, SSR=金光
    /// </summary>
    public class GachaAnimationManager : MonoBehaviour
    {
        // ========== 常量 ==========
        private const float SummonDuration = 1.5f;       // 召唤聚集时间
        private const float CardFlipDuration = 0.4f;     // 单张翻牌时间
        private const float CardRevealInterval = 0.25f;  // 翻牌间隔
        private const float SSRCutsceneDuration = 2.0f;  // SSR特写时间
        private const float ResultDisplayDelay = 0.5f;   // 结果展示延迟

        // ========== 稀有度颜色 ==========
        private static readonly Color ColorR = new Color(0.3f, 0.5f, 0.8f, 1f);      // 蓝色
        private static readonly Color ColorSR = new Color(0.6f, 0.3f, 0.8f, 1f);     // 紫色
        private static readonly Color ColorSSR = new Color(1f, 0.85f, 0.3f, 1f);     // 金色
        private static readonly Color ColorSSRGlow = new Color(1f, 0.9f, 0.4f, 0.6f);

        // ========== 状态 ==========
        private GachaAnimState _state = GachaAnimState.Idle;
        private List<GachaResult> _results;
        private bool _skipRequested;
        private int _currentRevealIndex;
        private float _stateTimer;

        // ========== UI引用 ==========
        private RectTransform _animRoot;
        private Image _bgOverlay;
        private Button _btnSkip;
        private Text _txtSkip;
        private RectTransform _summonCircle;
        private List<GachaCardUI> _cards = new List<GachaCardUI>();
        private RectTransform _ssrCutsceneRoot;
        private Image _ssrGlowImage;
        private Text _txtSSRName;

        // ========== 回调 ==========
        private Action _onFinished;

        // ========== 公共方法 ==========

        /// <summary>
        /// 播放抽卡动画
        /// </summary>
        /// <param name="parent">父节点</param>
        /// <param name="results">抽卡结果</param>
        /// <param name="onFinished">完成回调</param>
        public void PlayAnimation(RectTransform parent, List<GachaResult> results, Action onFinished)
        {
            _results = results;
            _onFinished = onFinished;
            _skipRequested = false;
            _currentRevealIndex = 0;

            BuildAnimUI(parent);
            StartCoroutine(AnimationSequence());
        }

        /// <summary>
        /// 请求跳过动画
        /// </summary>
        public void RequestSkip()
        {
            _skipRequested = true;
        }

        /// <summary>
        /// 当前动画状态
        /// </summary>
        public GachaAnimState CurrentState => _state;

        // ========== UI构建 ==========

        private void BuildAnimUI(RectTransform parent)
        {
            // 动画根节点
            var rootObj = new GameObject("GachaAnim");
            rootObj.transform.SetParent(parent, false);
            _animRoot = rootObj.AddComponent<RectTransform>();
            _animRoot.anchorMin = Vector2.zero;
            _animRoot.anchorMax = Vector2.one;
            _animRoot.offsetMin = Vector2.zero;
            _animRoot.offsetMax = Vector2.zero;

            // 半透明背景遮罩
            var bgObj = new GameObject("BgOverlay");
            bgObj.transform.SetParent(_animRoot, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            _bgOverlay = bgObj.AddComponent<Image>();
            _bgOverlay.color = new Color(0, 0, 0, 0);
            _bgOverlay.raycastTarget = true;

            // 召唤法阵（中心圆形光效）
            BuildSummonCircle();

            // 跳过按钮
            BuildSkipButton();

            // SSR特写根节点（默认隐藏）
            BuildSSRCutscene();
        }

        private void BuildSummonCircle()
        {
            var circleObj = new GameObject("SummonCircle");
            circleObj.transform.SetParent(_animRoot, false);
            _summonCircle = circleObj.AddComponent<RectTransform>();
            _summonCircle.anchorMin = new Vector2(0.3f, 0.3f);
            _summonCircle.anchorMax = new Vector2(0.7f, 0.7f);
            _summonCircle.offsetMin = Vector2.zero;
            _summonCircle.offsetMax = Vector2.zero;

            var circleImg = circleObj.AddComponent<Image>();
            circleImg.color = new Color(0.3f, 0.5f, 1f, 0f);
            circleImg.raycastTarget = false;

            _summonCircle.localScale = Vector3.zero;
        }

        private void BuildSkipButton()
        {
            var skipObj = new GameObject("BtnSkip");
            skipObj.transform.SetParent(_animRoot, false);
            var skipRect = skipObj.AddComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(0.78f, 0.90f);
            skipRect.anchorMax = new Vector2(0.98f, 0.98f);
            skipRect.offsetMin = Vector2.zero;
            skipRect.offsetMax = Vector2.zero;

            var skipImg = skipObj.AddComponent<Image>();
            skipImg.color = new Color(0.2f, 0.2f, 0.3f, 0.8f);

            _btnSkip = skipObj.AddComponent<Button>();
            _btnSkip.onClick.AddListener(RequestSkip);

            var txtObj = new GameObject("SkipTxt");
            txtObj.transform.SetParent(skipObj.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            _txtSkip = txtObj.AddComponent<Text>();
            _txtSkip.text = "跳过 ▶▶";
            _txtSkip.fontSize = 14;
            _txtSkip.color = Color.white;
            _txtSkip.alignment = TextAnchor.MiddleCenter;
            _txtSkip.font = BattleUI.GetFont();

            _txtSkip.raycastTarget = false;
        }

        private void BuildSSRCutscene()
        {
            var ssrObj = new GameObject("SSRCutscene");
            ssrObj.transform.SetParent(_animRoot, false);
            _ssrCutsceneRoot = ssrObj.AddComponent<RectTransform>();
            _ssrCutsceneRoot.anchorMin = Vector2.zero;
            _ssrCutsceneRoot.anchorMax = Vector2.one;
            _ssrCutsceneRoot.offsetMin = Vector2.zero;
            _ssrCutsceneRoot.offsetMax = Vector2.zero;

            // 金色光芒
            var glowObj = new GameObject("SSRGlow");
            glowObj.transform.SetParent(_ssrCutsceneRoot, false);
            var glowRect = glowObj.AddComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0.1f, 0.1f);
            glowRect.anchorMax = new Vector2(0.9f, 0.9f);
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;

            _ssrGlowImage = glowObj.AddComponent<Image>();
            _ssrGlowImage.color = ColorSSRGlow;
            _ssrGlowImage.raycastTarget = false;

            // SSR英雄名称
            var nameObj = new GameObject("SSRName");
            nameObj.transform.SetParent(_ssrCutsceneRoot, false);
            var nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.1f, 0.35f);
            nameRect.anchorMax = new Vector2(0.9f, 0.55f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;

            _txtSSRName = nameObj.AddComponent<Text>();
            _txtSSRName.text = "";
            _txtSSRName.fontSize = 36;
            _txtSSRName.color = ColorSSR;
            _txtSSRName.alignment = TextAnchor.MiddleCenter;
            _txtSSRName.font = BattleUI.GetFont();

            _txtSSRName.raycastTarget = false;

            _ssrCutsceneRoot.gameObject.SetActive(false);
        }

        // ========== 动画序列 ==========

        private IEnumerator AnimationSequence()
        {
            if (_results == null || _results.Count == 0)
            {
                Finish(true);
                yield break;
            }

            // 阶段1：背景渐暗
            _state = GachaAnimState.Summoning;
            yield return StartCoroutine(FadeBackground(0f, 0.85f, 0.3f));

            if (_skipRequested) { Finish(true); yield break; }

            // 阶段2：召唤法阵旋转放大
            yield return StartCoroutine(SummonEffect());

            if (_skipRequested) { Finish(true); yield break; }

            // 阶段3：创建卡牌并依次翻转
            _state = GachaAnimState.Revealing;
            CreateCards();

            // 检查是否有SSR
            int ssrIndex = -1;
            for (int i = 0; i < _results.Count; i++)
            {
                if (_results[i].Rarity == HeroRarity.SSR)
                {
                    ssrIndex = i;
                    break;
                }
            }

            // 依次翻牌
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_skipRequested) { RevealAllCards(); Finish(true); yield break; }

                _currentRevealIndex = i;

                // SSR翻牌前的特殊暂停
                if (_results[i].Rarity == HeroRarity.SSR)
                {
                    yield return new WaitForSeconds(0.3f);
                    if (_skipRequested) { RevealAllCards(); Finish(true); yield break; }

                    // SSR特写
                    _state = GachaAnimState.SSRCutscene;
                    yield return StartCoroutine(PlaySSRCutscene(_results[i]));

                    if (_skipRequested) { RevealAllCards(); Finish(true); yield break; }
                    _state = GachaAnimState.Revealing;
                }

                // 翻牌动画
                yield return StartCoroutine(FlipCard(_cards[i], _results[i]));

                yield return new WaitForSeconds(CardRevealInterval);
            }

            // 阶段4：展示结果
            _state = GachaAnimState.ShowResult;
            yield return new WaitForSeconds(ResultDisplayDelay);

            // 等待点击关闭
            _state = GachaAnimState.Finished;
            _txtSkip.text = "点击关闭";

            // 等待用户点击
            _skipRequested = false;
            while (!_skipRequested)
            {
                yield return null;
            }

            Finish(false);
        }

        // ========== 动画效果 ==========

        private IEnumerator FadeBackground(float fromAlpha, float toAlpha, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
                _bgOverlay.color = new Color(0, 0, 0, alpha);
                yield return null;
            }
            _bgOverlay.color = new Color(0, 0, 0, toAlpha);
        }

        private IEnumerator SummonEffect()
        {
            var circleImg = _summonCircle.GetComponent<Image>();
            float elapsed = 0f;

            while (elapsed < SummonDuration)
            {
                if (_skipRequested) yield break;

                elapsed += Time.deltaTime;
                float t = elapsed / SummonDuration;

                // 法阵放大
                float scale = EaseOutBack(t);
                _summonCircle.localScale = Vector3.one * scale;

                // 法阵旋转
                _summonCircle.localEulerAngles = new Vector3(0, 0, -t * 360f);

                // 法阵透明度
                float alpha = Mathf.Sin(t * Mathf.PI) * 0.8f;
                circleImg.color = new Color(0.3f, 0.5f, 1f, alpha);

                yield return null;
            }

            // 法阵消失
            _summonCircle.gameObject.SetActive(false);
        }

        private void CreateCards()
        {
            _cards.Clear();

            int count = _results.Count;
            int cols = count <= 1 ? 1 : 5;
            int rows = Mathf.CeilToInt((float)count / cols);

            float cardW = 0.16f;
            float cardH = count > 5 ? 0.22f : 0.35f;
            float startX = 0.5f - (Mathf.Min(count, cols) * cardW) / 2f;
            float startY = 0.5f + (rows * cardH) / 2f;

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;

                float x = startX + col * cardW + cardW * 0.5f;
                float y = startY - row * cardH - cardH * 0.5f;

                var card = CreateCardUI(i, x, y, cardW * 0.9f, cardH * 0.9f);
                _cards.Add(card);
            }
        }

        private GachaCardUI CreateCardUI(int index, float centerX, float centerY, float width, float height)
        {
            var cardObj = new GameObject($"Card_{index}");
            cardObj.transform.SetParent(_animRoot, false);
            var cardRect = cardObj.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(centerX - width / 2f, centerY - height / 2f);
            cardRect.anchorMax = new Vector2(centerX + width / 2f, centerY + height / 2f);
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;

            // 卡背（未翻转状态）
            var backImg = cardObj.AddComponent<Image>();
            backImg.color = new Color(0.15f, 0.15f, 0.25f, 0.95f);

            // 问号文字
            var questionObj = new GameObject("Question");
            questionObj.transform.SetParent(cardObj.transform, false);
            var qRect = questionObj.AddComponent<RectTransform>();
            qRect.anchorMin = Vector2.zero;
            qRect.anchorMax = Vector2.one;
            qRect.offsetMin = Vector2.zero;
            qRect.offsetMax = Vector2.zero;

            var qText = questionObj.AddComponent<Text>();
            qText.text = "?";
            qText.fontSize = 28;
            qText.color = new Color(0.4f, 0.4f, 0.6f, 0.8f);
            qText.alignment = TextAnchor.MiddleCenter;
            qText.font = BattleUI.GetFont();

            qText.raycastTarget = false;

            // 卡面内容（初始隐藏）
            var frontObj = new GameObject("Front");
            frontObj.transform.SetParent(cardObj.transform, false);
            var frontRect = frontObj.AddComponent<RectTransform>();
            frontRect.anchorMin = Vector2.zero;
            frontRect.anchorMax = Vector2.one;
            frontRect.offsetMin = Vector2.zero;
            frontRect.offsetMax = Vector2.zero;
            frontObj.SetActive(false);

            // 卡面背景
            var frontBg = frontObj.AddComponent<Image>();
            frontBg.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);

            // 英雄图标
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(frontObj.transform, false);
            var iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.1f, 0.35f);
            iconRect.anchorMax = new Vector2(0.9f, 0.85f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            var iconText = iconObj.AddComponent<Text>();
            iconText.text = "";
            iconText.fontSize = 32;
            iconText.color = Color.white;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.font = BattleUI.GetFont();

            iconText.raycastTarget = false;

            // 英雄头像图片（叠加在iconText上方，有图时隐藏文字）
            var iconImgObj = new GameObject("IconImage");
            iconImgObj.transform.SetParent(frontObj.transform, false);
            var iconImgRect = iconImgObj.AddComponent<RectTransform>();
            iconImgRect.anchorMin = new Vector2(0.15f, 0.30f);
            iconImgRect.anchorMax = new Vector2(0.85f, 0.88f);
            iconImgRect.offsetMin = Vector2.zero;
            iconImgRect.offsetMax = Vector2.zero;
            var iconImage = iconImgObj.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.color = new Color(1, 1, 1, 0); // 初始透明
            iconImgObj.SetActive(false);

            // 英雄名称
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(frontObj.transform, false);
            var nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.05f, 0.05f);
            nameRect.anchorMax = new Vector2(0.95f, 0.30f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;

            var nameText = nameObj.AddComponent<Text>();
            nameText.text = "";
            nameText.fontSize = 12;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.font = BattleUI.GetFont();

            nameText.raycastTarget = false;

            // 稀有度光边（初始隐藏）
            var glowObj = new GameObject("Glow");
            glowObj.transform.SetParent(cardObj.transform, false);
            var glowRect = glowObj.AddComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(-0.05f, -0.05f);
            glowRect.anchorMax = new Vector2(1.05f, 1.05f);
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;

            var glowImg = glowObj.AddComponent<Image>();
            glowImg.color = new Color(1, 1, 1, 0);
            glowImg.raycastTarget = false;
            glowObj.transform.SetAsFirstSibling(); // 放到最底层

            // 初始缩放为0（弹入动画）
            cardRect.localScale = Vector3.zero;
            StartCoroutine(CardAppearAnimation(cardRect, index * 0.05f));

            return new GachaCardUI
            {
                Root = cardRect,
                BackImage = backImg,
                QuestionText = qText,
                FrontRoot = frontObj,
                FrontBg = frontBg,
                IconText = iconText,
                IconImage = iconImage,
                NameText = nameText,
                GlowImage = glowImg,
                IsRevealed = false
            };
        }

        private IEnumerator CardAppearAnimation(RectTransform card, float delay)
        {
            yield return new WaitForSeconds(delay);

            float elapsed = 0f;
            float duration = 0.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = EaseOutBack(t);
                card.localScale = Vector3.one * scale;
                yield return null;
            }
            card.localScale = Vector3.one;
        }

        private IEnumerator FlipCard(GachaCardUI card, GachaResult result)
        {
            var config = HeroConfigTable.GetHero(result.HeroId);
            Color rarityColor = GetRarityColor(result.Rarity);

            float elapsed = 0f;
            float halfDuration = CardFlipDuration / 2f;

            // 前半段：缩小到0（翻转效果）
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                float scaleX = Mathf.Lerp(1f, 0f, t);
                card.Root.localScale = new Vector3(scaleX, 1f, 1f);
                yield return null;
            }

            // 切换到正面
            card.QuestionText.gameObject.SetActive(false);
            card.FrontRoot.SetActive(true);
            card.BackImage.color = rarityColor * 0.3f + new Color(0, 0, 0, 0.7f);

            // 设置内容
            if (config != null)
            {
                // 优先加载英雄头像图片
                Sprite avatarSprite = !string.IsNullOrEmpty(config.SpriteName)
                    ? AetheraSurvivors.Framework.SpriteLoader.LoadHeroAvatar(config.SpriteName) : null;
                if (avatarSprite != null && card.IconImage != null)
                {
                    card.IconImage.sprite = avatarSprite;
                    card.IconImage.color = Color.white;
                    card.IconImage.gameObject.SetActive(true);
                    card.IconText.gameObject.SetActive(false);
                }
                else
                {
                    card.IconText.text = config.Icon;
                }

                string label = config.Name;
                if (result.IsNew) label += "\nNEW";
                else label += $"\n#{result.FragmentCount}";

                card.NameText.text = label;
                card.NameText.color = rarityColor;
            }

            // 稀有度光边
            card.GlowImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.4f);

            // 后半段：从0放大到1
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                float scaleX = Mathf.Lerp(0f, 1f, t);
                card.Root.localScale = new Vector3(scaleX, 1f, 1f);
                yield return null;
            }

            card.Root.localScale = Vector3.one;
            card.IsRevealed = true;

            // SSR额外弹跳效果
            if (result.Rarity == HeroRarity.SSR)
            {
                yield return StartCoroutine(SSRCardBounce(card));
            }
        }

        private IEnumerator SSRCardBounce(GachaCardUI card)
        {
            // 放大弹跳
            float elapsed = 0f;
            float duration = 0.4f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = 1f + 0.15f * Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
                card.Root.localScale = Vector3.one * scale;

                // 光边脉冲
                float glowAlpha = 0.3f + 0.4f * Mathf.Sin(t * Mathf.PI * 3f);
                var c = card.GlowImage.color;
                card.GlowImage.color = new Color(c.r, c.g, c.b, glowAlpha);

                yield return null;
            }

            card.Root.localScale = Vector3.one;
        }

        private IEnumerator PlaySSRCutscene(GachaResult result)
        {
            var config = HeroConfigTable.GetHero(result.HeroId);
            if (config == null) yield break;

            _ssrCutsceneRoot.gameObject.SetActive(true);
            _txtSSRName.text = $"★★★ {config.Icon} {config.Name} ★★★";

            // 光芒从0放大
            float elapsed = 0f;
            _ssrCutsceneRoot.localScale = Vector3.zero;

            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.5f;
                float scale = EaseOutBack(t);
                _ssrCutsceneRoot.localScale = Vector3.one * scale;

                // 光芒旋转
                _ssrGlowImage.transform.localEulerAngles = new Vector3(0, 0, -t * 180f);

                yield return null;
            }

            // 持续展示
            elapsed = 0f;
            while (elapsed < SSRCutsceneDuration)
            {
                if (_skipRequested) break;

                elapsed += Time.deltaTime;

                // 光芒脉冲
                float pulse = 0.6f + 0.4f * Mathf.Sin(elapsed * 3f);
                _ssrGlowImage.color = new Color(ColorSSRGlow.r, ColorSSRGlow.g, ColorSSRGlow.b, pulse);

                // 持续旋转
                _ssrGlowImage.transform.localEulerAngles = new Vector3(0, 0, -elapsed * 60f);

                // 名字缩放脉冲
                float nameScale = 1f + 0.05f * Mathf.Sin(elapsed * 4f);
                _txtSSRName.transform.localScale = Vector3.one * nameScale;

                yield return null;
            }

            // 淡出
            elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.3f;
                _ssrCutsceneRoot.localScale = Vector3.one * (1f - t);
                yield return null;
            }

            _ssrCutsceneRoot.gameObject.SetActive(false);
        }

        private void RevealAllCards()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].IsRevealed) continue;

                var card = _cards[i];
                var result = _results[i];
                var config = HeroConfigTable.GetHero(result.HeroId);
                Color rarityColor = GetRarityColor(result.Rarity);

                card.QuestionText.gameObject.SetActive(false);
                card.FrontRoot.SetActive(true);
                card.BackImage.color = rarityColor * 0.3f + new Color(0, 0, 0, 0.7f);

                if (config != null)
                {
                    // 优先加载头像
                    Sprite avatarSprite = !string.IsNullOrEmpty(config.SpriteName)
                        ? AetheraSurvivors.Framework.SpriteLoader.LoadHeroAvatar(config.SpriteName) : null;
                    if (avatarSprite != null && card.IconImage != null)
                    {
                        card.IconImage.sprite = avatarSprite;
                        card.IconImage.color = Color.white;
                        card.IconImage.gameObject.SetActive(true);
                        card.IconText.gameObject.SetActive(false);
                    }
                    else
                    {
                        card.IconText.text = config.Icon;
                    }

                    string label = config.Name;
                    if (result.IsNew) label += "\nNEW";
                    else label += $"\n#{result.FragmentCount}";

                    card.NameText.text = label;
                    card.NameText.color = rarityColor;
                }

                card.GlowImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.4f);
                card.Root.localScale = Vector3.one;
                card.IsRevealed = true;
            }
        }

        // ========== 完成 ==========

        private void Finish(bool wasSkipped)
        {
            // 确保所有卡牌都翻开
            RevealAllCards();

            // 隐藏SSR特写
            if (_ssrCutsceneRoot != null)
                _ssrCutsceneRoot.gameObject.SetActive(false);

            // 发布事件
            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new GachaAnimFinishedEvent
                {
                    ResultCount = _results?.Count ?? 0,
                    WasSkipped = wasSkipped
                });
            }

            // 延迟销毁动画UI
            StartCoroutine(DelayedCleanup());

            _onFinished?.Invoke();
        }

        private IEnumerator DelayedCleanup()
        {
            yield return new WaitForSeconds(0.5f);

            if (_animRoot != null)
            {
                Destroy(_animRoot.gameObject);
            }
        }

        // ========== 工具方法 ==========

        private Color GetRarityColor(HeroRarity rarity)
        {
            switch (rarity)
            {
                case HeroRarity.SSR: return ColorSSR;
                case HeroRarity.SR: return ColorSR;
                case HeroRarity.R: return ColorR;
                default: return Color.white;
            }
        }

        /// <summary>EaseOutBack缓动函数</summary>
        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }

    // ========== 卡牌UI数据 ==========

    /// <summary>
    /// 抽卡卡牌UI组件引用
    /// </summary>
    public class GachaCardUI
    {
        public RectTransform Root;
        public Image BackImage;
        public Text QuestionText;
        public GameObject FrontRoot;
        public Image FrontBg;
        public Text IconText;
        public Image IconImage; // 英雄头像图片（有图时替代IconText）
        public Text NameText;
        public Image GlowImage;
        public bool IsRevealed;
    }
}
