// ============================================================
// 文件名：MainMenuUI.cs
// 功能描述：主界面UI — 角色展示、各功能入口、UI动效、红点标记
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #247-248
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Visual;
using AetheraSurvivors.Data;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>
    /// 主界面UI面板
    /// 
    /// 功能：
    /// 1. 顶部信息栏（玩家等级/昵称/货币显示）
    /// 2. 中央角色展示区
    /// 3. 功能入口按钮（关卡/英雄/商城/社交/抽卡）
    /// 4. 底部导航栏
    /// 5. 红点标记系统联动
    /// 6. UI入场/退场动效
    /// </summary>
    public class MainMenuUI : BasePanel
    {
        public override UILayer Layer => UILayer.Bottom;
        public override bool IsCached => true;

        // ========== UI引用 ==========
        private RectTransform _rootRect;

        // 顶部信息栏
        private Text _txtPlayerName;
        private Text _txtPlayerLevel;
        private Text _txtDiamonds;
        private Text _txtGold;
        private Text _txtStamina;

        // 中央区域
        private RectTransform _heroDisplayArea;
        private Text _txtHeroName;
        private Image _imgHeroAvatar;

        // 功能入口按钮
        private Button _btnBattle;
        private Button _btnHero;
        private Button _btnShop;
        private Button _btnGacha;
        private Button _btnSocial;
        private Button _btnSettings;
        private Button _btnMail;
        private Button _btnQuest;
        private Button _btnBattlePass;

        // 底部导航栏
        private RectTransform _bottomNavBar;
        private Button[] _navButtons;
        private int _currentNavIndex = 0;

        // 红点标记
        private Dictionary<string, GameObject> _redDots = new Dictionary<string, GameObject>();

        // 动画状态
        private float _animTimer;
        private bool _isAnimating;
        private List<RectTransform> _animTargets = new List<RectTransform>();

        // ========== 生命周期 ==========

        protected override void OnOpen(object param)
        {
            BuildUI();
            BindEvents();
            RefreshAll();
        }

        protected override void OnShow()
        {
            RefreshAll();
            PlayEnterAnimation();

            // 播放主界面BGM
            if (Framework.AudioManager.HasInstance)
            {
                Framework.AudioManager.Instance.PlayBGM("Audio/BGM/bgm_main_menu", 1.0f);
            }
        }

        protected override void OnHide()
        {
        }

        protected override void OnClose()
        {
            UnbindEvents();
        }

        // ========== UI构建 ==========

        private void BuildUI()
        {
            _rootRect = GetComponent<RectTransform>();
            if (_rootRect == null)
                _rootRect = gameObject.AddComponent<RectTransform>();

            // 全屏背景（使用Live Shader驱动动效）
            BuildBackground();

            // 星辰+萤火叠加层（轻量叠加，不阻挡点击）
            BuildOverlayEffects();

            // 顶部信息栏
            BuildTopBar();

            // 中央英雄展示区（暂时隐藏——英雄立绘资源未就位，影响美观）
            // BuildHeroDisplay();

            // 功能入口按钮组
            BuildFunctionButtons();

            // 底部导航栏
            BuildBottomNavBar();
        }

        /// <summary>构建背景 — 使用BackgroundLive Shader让图片动起来</summary>
        private void BuildBackground()
        {
            var bgObj = new GameObject("BG");
            bgObj.transform.SetParent(transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // 加载背景图
            var bgSprite = Resources.Load<Sprite>("Sprites/Backgrounds/bg_main_menu");
            if (bgSprite == null)
            {
                var bgTex = Resources.Load<Texture2D>("Sprites/Backgrounds/bg_main_menu");
                if (bgTex != null)
                    bgSprite = Sprite.Create(bgTex, new Rect(0, 0, bgTex.width, bgTex.height), new Vector2(0.5f, 0.5f));
            }

            if (bgSprite != null)
            {
                var bgImg = bgObj.AddComponent<Image>();
                bgImg.sprite = bgSprite;
                bgImg.type = Image.Type.Simple;
                bgImg.preserveAspect = false;
                bgImg.raycastTarget = false;

                // 加载 BackgroundLive Shader 材质
                var liveShader = Shader.Find("UI/BackgroundLive");
                if (liveShader != null)
                {
                    var liveMat = new Material(liveShader);
                    liveMat.SetTexture("_MainTex", bgSprite.texture);

                    // 云区参数（画面上半部分 55%~95% 高度的云）
                    liveMat.SetFloat("_CloudSpeed", 0.015f);
                    liveMat.SetFloat("_CloudAmplitude", 0.006f);
                    liveMat.SetFloat("_CloudFrequency", 1.5f);
                    liveMat.SetFloat("_CloudYStart", 0.50f);
                    liveMat.SetFloat("_CloudYEnd", 0.92f);

                    // 树区参数（下方35%以下，左右两侧）
                    liveMat.SetFloat("_TreeSpeed", 1.2f);
                    liveMat.SetFloat("_TreeAmplitude", 0.003f);
                    liveMat.SetFloat("_TreeFrequency", 2.5f);
                    liveMat.SetFloat("_TreeYMax", 0.35f);

                    // 太阳参数（中偏左 40%,55%）
                    liveMat.SetVector("_SunCenter", new Vector4(0.40f, 0.55f, 0f, 0f));
                    liveMat.SetFloat("_SunRadius", 0.18f);
                    liveMat.SetFloat("_SunPulseSpeed", 0.6f);
                    liveMat.SetFloat("_SunPulseAmplitude", 0.004f);
                    liveMat.SetFloat("_SunGlowAmplitude", 0.12f);

                    // 全局微呼吸
                    liveMat.SetFloat("_BreathSpeed", 0.25f);
                    liveMat.SetFloat("_BreathAmplitude", 0.0008f);

                    bgImg.material = liveMat;
                    Debug.Log("[MainMenuUI] BackgroundLive Shader 已应用");
                }
                else
                {
                    Debug.LogWarning("[MainMenuUI] 找不到 UI/BackgroundLive Shader，背景将静态显示");
                }
            }
            else
            {
                UIStyleKit.CreateGradientPanel(bgRect,
                    new Color(0.04f, 0.04f, 0.10f, 1f),
                    new Color(0.08f, 0.10f, 0.20f, 1f));
            }
        }

        /// <summary>星辰+萤火叠加层（保留轻量粒子效果）</summary>
        private void BuildOverlayEffects()
        {
            var effectLayer = new GameObject("OverlayEffects");
            effectLayer.transform.SetParent(transform, false);
            var layerRect = effectLayer.AddComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = Vector2.zero;
            layerRect.offsetMax = Vector2.zero;

            var cg = effectLayer.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var liveEffect = effectLayer.AddComponent<MainMenuLiveEffect>();
            liveEffect.Initialize(layerRect);
        }

        /// <summary>构建顶部信息栏 — 高品质胶囊底图+清晰大字号</summary>
        private void BuildTopBar()
        {
            var topBar = CreateRect("TopBar", transform);
            topBar.anchorMin = new Vector2(0, 0.90f);
            topBar.anchorMax = new Vector2(1, 1f);
            topBar.offsetMin = Vector2.zero;
            topBar.offsetMax = Vector2.zero;

            // 半透明底
            var topBg = topBar.gameObject.AddComponent<Image>();
            topBg.sprite = UIAtlasGenerator.GetTabBar();
            topBg.type = Image.Type.Sliced;
            topBg.raycastTarget = false;

            // 等级徽章
            var lvBadge = CreateRect("LvBadge", topBar);
            lvBadge.anchorMin = new Vector2(0.01f, 0.12f);
            lvBadge.anchorMax = new Vector2(0.09f, 0.88f);
            lvBadge.offsetMin = Vector2.zero;
            lvBadge.offsetMax = Vector2.zero;
            var lvImg = lvBadge.gameObject.AddComponent<Image>();
            lvImg.sprite = UIAtlasGenerator.GetCapsule(new Color(0.65f, 0.50f, 0.10f, 0.95f));
            lvImg.type = Image.Type.Sliced;
            lvImg.raycastTarget = false;

            _txtPlayerLevel = CreateText("LvText", lvBadge.GetComponent<RectTransform>(), "Lv.1", 18,
                Color.white, TextAnchor.MiddleCenter);
            _txtPlayerLevel.fontStyle = FontStyle.Bold;
            _txtPlayerLevel.font = Battle.BattleUI.GetFont();

            // 昵称
            _txtPlayerName = CreateText("Name", topBar, "指挥官", 18,
                UIStyleKit.TextWhite, TextAnchor.MiddleLeft);
            _txtPlayerName.font = Battle.BattleUI.GetFont();
            SetAnchors(_txtPlayerName.rectTransform, 0.10f, 0.35f, 0.1f, 0.9f);

            // 钻石胶囊
            BuildCurrencyCapsule(topBar, "Diamond",
                0.42f, 0.12f, 0.60f, 0.88f,
                new Color(0.08f, 0.12f, 0.30f, 0.90f),
                "nav_gacha", ref _txtDiamonds, new Color(0.5f, 0.85f, 1f));

            // 金币胶囊
            BuildCurrencyCapsule(topBar, "Gold",
                0.62f, 0.12f, 0.80f, 0.88f,
                new Color(0.22f, 0.16f, 0.04f, 0.90f),
                "icon_coin", ref _txtGold, new Color(1f, 0.90f, 0.35f));

            // 体力胶囊
            BuildCurrencyCapsule(topBar, "Stamina",
                0.82f, 0.12f, 0.99f, 0.88f,
                new Color(0.08f, 0.20f, 0.08f, 0.90f),
                "icon_heart", ref _txtStamina, new Color(0.45f, 1f, 0.55f));
        }

        private void BuildCurrencyCapsule(RectTransform parent, string name,
            float xMin, float yMin, float xMax, float yMax,
            Color bgColor, string iconName, ref Text txtRef, Color textColor)
        {
            var capsule = CreateRect($"Capsule_{name}", parent);
            capsule.anchorMin = new Vector2(xMin, yMin);
            capsule.anchorMax = new Vector2(xMax, yMax);
            capsule.offsetMin = Vector2.zero;
            capsule.offsetMax = Vector2.zero;

            var capsuleImg = capsule.gameObject.AddComponent<Image>();
            capsuleImg.sprite = UIAtlasGenerator.GetCapsule(bgColor);
            capsuleImg.type = Image.Type.Sliced;
            capsuleImg.raycastTarget = false;

            // 图标
            Sprite icon = SpriteLoader.LoadUI(iconName);
            if (icon != null)
            {
                var iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(capsule, false);
                var iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.03f, 0.10f);
                iconRect.anchorMax = new Vector2(0.25f, 0.90f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                var iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            // 数值
            txtRef = CreateText($"Txt_{name}", capsule.GetComponent<RectTransform>(), "0", 17,
                textColor, TextAnchor.MiddleRight);
            txtRef.font = Battle.BattleUI.GetFont();
            txtRef.fontStyle = FontStyle.Bold;
            SetAnchors(txtRef.rectTransform, 0.26f, 0.95f, 0.05f, 0.95f);
        }

        /// <summary>构建英雄展示区 — 立绘直接展示+底部光晕底座+名称装饰</summary>
        private void BuildHeroDisplay()
        {
            _heroDisplayArea = CreateRect("HeroDisplay", transform);
            _heroDisplayArea.anchorMin = new Vector2(0.15f, 0.28f);
            _heroDisplayArea.anchorMax = new Vector2(0.85f, 0.90f);
            _heroDisplayArea.offsetMin = Vector2.zero;
            _heroDisplayArea.offsetMax = Vector2.zero;

            // 底部椭圆光晕底座（在立绘下方）
            var glowObj = new GameObject("HeroGlow");
            glowObj.transform.SetParent(_heroDisplayArea, false);
            var glowRect = glowObj.AddComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0.15f, 0.02f);
            glowRect.anchorMax = new Vector2(0.85f, 0.15f);
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;
            var glowImg = glowObj.AddComponent<Image>();
            // 椭圆光晕纹理
            var glowTex = new Texture2D(64, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 64; x++)
                {
                    float nx = (x - 32f) / 32f;
                    float ny = (y - 8f) / 8f;
                    float d = nx * nx + ny * ny;
                    float a = Mathf.Clamp01(1f - d) * 0.5f;
                    glowTex.SetPixel(x, y, new Color(0.9f, 0.75f, 0.3f, a));
                }
            glowTex.Apply();
            glowTex.filterMode = FilterMode.Bilinear;
            glowImg.sprite = Sprite.Create(glowTex, new Rect(0, 0, 64, 16), new Vector2(0.5f, 0.5f));
            glowImg.raycastTarget = false;

            // 英雄立绘（无框直接展示）
            var avatarObj = new GameObject("HeroAvatar");
            avatarObj.transform.SetParent(_heroDisplayArea, false);
            var avatarRect = avatarObj.AddComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0.2f, 0.08f);
            avatarRect.anchorMax = new Vector2(0.8f, 0.92f);
            avatarRect.offsetMin = Vector2.zero;
            avatarRect.offsetMax = Vector2.zero;
            _imgHeroAvatar = avatarObj.AddComponent<Image>();
            _imgHeroAvatar.color = new Color(1, 1, 1, 0); // 初始透明，有图时显示
            _imgHeroAvatar.preserveAspect = true;
            _imgHeroAvatar.raycastTarget = false;

            // 英雄名称装饰标签（底部居中，带半透明背景条）
            var nameBg = CreateRect("HeroNameBg", _heroDisplayArea);
            nameBg.anchorMin = new Vector2(0.2f, 0.0f);
            nameBg.anchorMax = new Vector2(0.8f, 0.10f);
            nameBg.offsetMin = Vector2.zero;
            nameBg.offsetMax = Vector2.zero;
            var nameBgTex = UIStyleKit.GetRoundedRectTexture(64, 16, 6,
                new Color(0f, 0f, 0f, 0.6f), new Color(0.8f, 0.6f, 0.2f, 0.4f), 1);
            var nameBgImg = nameBg.gameObject.AddComponent<Image>();
            nameBgImg.sprite = Sprite.Create(nameBgTex, new Rect(0, 0, 64, 16),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
            nameBgImg.type = Image.Type.Sliced;
            nameBgImg.raycastTarget = false;

            _txtHeroName = CreateText("HeroName", nameBg.GetComponent<RectTransform>(), "铁壁骑士", 18,
                UIStyleKit.TextGold, TextAnchor.MiddleCenter);
            _txtHeroName.font = Battle.BattleUI.GetFont();
            _txtHeroName.fontStyle = FontStyle.Bold;
            UIStyleKit.AddTextShadow(_txtHeroName);
        }

        /// <summary>构建功能入口按钮 — CTA大按钮+右侧竖排圆形副按钮</summary>
        private void BuildFunctionButtons()
        {
            // ===== 大CTA按钮：开始战斗（底部居中，全宽）=====
            var ctaArea = CreateRect("CTAArea", transform);
            ctaArea.anchorMin = new Vector2(0.15f, 0.14f);
            ctaArea.anchorMax = new Vector2(0.85f, 0.24f);
            ctaArea.offsetMin = Vector2.zero;
            ctaArea.offsetMax = Vector2.zero;

            _btnBattle = CreateCTAButton(ctaArea);
            _btnBattle.onClick.AddListener(OnBattleClick);

            // ===== 右侧竖排圆形副按钮 =====
            var sideArea = CreateRect("SideButtons", transform);
            sideArea.anchorMin = new Vector2(0.88f, 0.28f);
            sideArea.anchorMax = new Vector2(0.98f, 0.88f);
            sideArea.offsetMin = Vector2.zero;
            sideArea.offsetMax = Vector2.zero;

            string[] sideNames = { "英雄", "商城", "召唤", "战令", "社交" };
            string[] sideIcons = { "nav_hero", "nav_shop", "nav_gacha", "nav_battlepass", "nav_social" };
            Button[] sideBtns = new Button[5];
            float btnH = 1f / 5f;

            for (int i = 0; i < 5; i++)
            {
                sideBtns[i] = CreateCircleButton(sideArea, $"Side_{sideNames[i]}", sideNames[i], sideIcons[i],
                    new Vector2(0f, 1f - (i + 1) * btnH + 0.01f),
                    new Vector2(1f, 1f - i * btnH - 0.01f));
            }

            _btnHero = sideBtns[0]; _btnHero.onClick.AddListener(OnHeroClick);
            _btnShop = sideBtns[1]; _btnShop.onClick.AddListener(OnShopClick);
            _btnGacha = sideBtns[2]; _btnGacha.onClick.AddListener(OnGachaClick);
            _btnBattlePass = sideBtns[3]; _btnBattlePass.onClick.AddListener(OnBattlePassClick);
            _btnSocial = sideBtns[4]; _btnSocial.onClick.AddListener(OnSocialClick);

            // 收集动画目标
            _animTargets.Add(_btnBattle.GetComponent<RectTransform>());
            for (int i = 0; i < sideBtns.Length; i++)
                _animTargets.Add(sideBtns[i].GetComponent<RectTransform>());
        }

        /// <summary>创建CTA大按钮（开始战斗）— 高品质渐变+金边+投影</summary>
        private Button CreateCTAButton(RectTransform parent)
        {
            var obj = new GameObject("BtnBattle");
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = obj.AddComponent<Image>();
            img.sprite = UIAtlasGenerator.GetCTAButton();
            img.type = Image.Type.Sliced;

            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.12f, 1.08f);
            colors.pressedColor = new Color(0.85f, 0.90f, 0.85f);
            colors.fadeDuration = 0.1f;
            btn.colors = colors;

            // 图标
            Sprite battleIcon = SpriteLoader.LoadUI("nav_battle");
            if (battleIcon != null)
            {
                var iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(obj.transform, false);
                var iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.28f, 0.12f);
                iconRect.anchorMax = new Vector2(0.42f, 0.88f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                var iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = battleIcon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            // 文字
            var txtObj = new GameObject("Label");
            txtObj.transform.SetParent(obj.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = new Vector2(0.42f, 0.05f);
            txtRect.anchorMax = new Vector2(0.78f, 0.95f);
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var txt = txtObj.AddComponent<Text>();
            txt.text = "开始战斗";
            txt.fontSize = 28;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Battle.BattleUI.GetFont();
            txt.fontStyle = FontStyle.Bold;
            txt.raycastTarget = false;
            UIStyleKit.AddTextShadow(txt);

            return btn;
        }

        /// <summary>创建右侧圆形按钮 — 高品质圆形底图+投影</summary>
        private Button CreateCircleButton(RectTransform parent, string name, string label, string iconName,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = obj.AddComponent<Image>();
            img.sprite = UIAtlasGenerator.GetCircleButton(new Color(0.06f, 0.06f, 0.18f, 0.88f));
            img.preserveAspect = true;

            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.25f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.85f);
            colors.fadeDuration = 0.1f;
            btn.colors = colors;

            // 图标
            Sprite iconSprite = SpriteLoader.LoadUI(iconName);
            if (iconSprite != null)
            {
                var iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(obj.transform, false);
                var iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.18f, 0.30f);
                iconRect.anchorMax = new Vector2(0.82f, 0.88f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                var iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = iconSprite;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            // 文字标签
            var txtObj = new GameObject("Label");
            txtObj.transform.SetParent(obj.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = new Vector2(-0.15f, -0.02f);
            txtRect.anchorMax = new Vector2(1.15f, 0.26f);
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var txt = txtObj.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = 13;
            txt.color = new Color(0.85f, 0.88f, 1f);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Battle.BattleUI.GetFont();
            txt.raycastTarget = false;
            UIStyleKit.AddTextShadow(txt);

            return btn;
        }

        /// <summary>构建底部导航栏 — iOS标签栏风格</summary>
        private void BuildBottomNavBar()
        {
            _bottomNavBar = CreateRect("BottomNavBar", transform);
            _bottomNavBar.anchorMin = new Vector2(0, 0);
            _bottomNavBar.anchorMax = new Vector2(1, 0.13f);
            _bottomNavBar.offsetMin = Vector2.zero;
            _bottomNavBar.offsetMax = Vector2.zero;

            // 高品质标签栏底（含顶部金线）
            var barBg = _bottomNavBar.gameObject.AddComponent<Image>();
            barBg.sprite = UIAtlasGenerator.GetTabBar();
            barBg.type = Image.Type.Sliced;
            barBg.raycastTarget = true;

            // 导航按钮
            string[] navLabels = { "任务", "邮件", "主页", "排行", "设置" };
            string[] navIconNames = { "nav_quest", "nav_mail", "nav_battle", "nav_rank", "nav_shop" };
            _navButtons = new Button[navLabels.Length];
            float navBtnW = 1f / navLabels.Length;

            for (int i = 0; i < navLabels.Length; i++)
            {
                int idx = i;
                float xMin = navBtnW * i;
                float xMax = navBtnW * (i + 1);

                var btnObj = new GameObject($"Nav_{i}");
                btnObj.transform.SetParent(_bottomNavBar, false);
                var btnRect = btnObj.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(xMin + 0.005f, 0.05f);
                btnRect.anchorMax = new Vector2(xMax - 0.005f, 0.93f);
                btnRect.offsetMin = Vector2.zero;
                btnRect.offsetMax = Vector2.zero;

                // 透明按钮（不需要可见背景，由选中态控制）
                var btnImg = btnObj.AddComponent<Image>();
                btnImg.color = Color.clear;

                var btn = btnObj.AddComponent<Button>();
                btn.targetGraphic = btnImg;
                var colors = btn.colors;
                colors.normalColor = Color.clear;
                colors.highlightedColor = new Color(1, 1, 1, 0.05f);
                colors.pressedColor = new Color(1, 1, 1, 0.1f);
                colors.fadeDuration = 0.1f;
                btn.colors = colors;
                btn.onClick.AddListener(() => OnNavClick(idx));
                _navButtons[i] = btn;

                // 图标（上方 65%）
                Sprite iconSprite = SpriteLoader.LoadUI(navIconNames[i]);
                if (iconSprite != null)
                {
                    var iconObj = new GameObject("Icon");
                    iconObj.transform.SetParent(btnObj.transform, false);
                    var iconRect = iconObj.AddComponent<RectTransform>();
                    iconRect.anchorMin = new Vector2(0.25f, 0.35f);
                    iconRect.anchorMax = new Vector2(0.75f, 0.95f);
                    iconRect.offsetMin = Vector2.zero;
                    iconRect.offsetMax = Vector2.zero;
                    var iconImg = iconObj.AddComponent<Image>();
                    iconImg.sprite = iconSprite;
                    iconImg.preserveAspect = true;
                    iconImg.raycastTarget = false;
                    // 未选中时半透明
                    iconImg.color = (i == 2) ? Color.white : new Color(0.6f, 0.6f, 0.7f);
                }

                // 文字标签（下方 30%）
                var txtObj = new GameObject("Label");
                txtObj.transform.SetParent(btnObj.transform, false);
                var txtRect = txtObj.AddComponent<RectTransform>();
                txtRect.anchorMin = new Vector2(0, 0.0f);
                txtRect.anchorMax = new Vector2(1, 0.33f);
                txtRect.offsetMin = Vector2.zero;
                txtRect.offsetMax = Vector2.zero;
                var txt = txtObj.AddComponent<Text>();
                txt.text = navLabels[i];
                txt.fontSize = 11;
                txt.color = (i == 2) ? UIStyleKit.TextGold : new Color(0.5f, 0.5f, 0.6f);
                txt.alignment = TextAnchor.MiddleCenter;
                txt.font = Battle.BattleUI.GetFont();
                txt.raycastTarget = false;

                // 红点
                if (i == 0) AddRedDot(btnRect, "nav_quest");
                if (i == 1) AddRedDot(btnRect, "nav_mail");
            }

            UpdateNavSelection(2);
        }

        // ========== 按钮回调 ==========

        private void OnBattleClick()
        {
            Debug.Log("[MainMenuUI] 点击开始战斗 → 打开关卡选择");
            UIManager.Instance.Open<LevelSelectPanel>();
        }

        private void OnHeroClick()
        {
            Debug.Log("[MainMenuUI] 点击英雄");
            UIManager.Instance.Open<HeroPanel>();
        }

        private void OnShopClick()
        {
            Debug.Log("[MainMenuUI] 点击商城");
            UIManager.Instance.Open<ShopPanel>();
        }

        private void OnGachaClick()
        {
            Debug.Log("[MainMenuUI] 点击召唤");
            UIManager.Instance.Open<GachaPanel>();
        }

        private void OnBattlePassClick()
        {
            Debug.Log("[MainMenuUI] 点击战令");
            UIManager.Instance.Open<BattlePassPanel>();
        }

        private void OnSocialClick()
        {
            Debug.Log("[MainMenuUI] 点击社交");
            UIManager.Instance.Open<SocialPanel>();
        }

        private void OnNavClick(int index)
        {
            UpdateNavSelection(index);
            switch (index)
            {
                case 0: // 任务
                    UIManager.Instance.Open<QuestPanel>();
                    break;
                case 1: // 邮件
                    UIManager.Instance.Open<MailPanel>();
                    break;
                case 2: // 主页（已在主页，不做操作）
                    break;
                case 3: // 排行
                    UIManager.Instance.Open<RankPanel>();
                    break;
                case 4: // 设置
                    UIManager.Instance.Open<SettingsPanel>();
                    break;
            }
        }

        private void UpdateNavSelection(int index)
        {
            _currentNavIndex = index;
            for (int i = 0; i < _navButtons.Length; i++)
            {
                bool selected = (i == index);
                var btnTransform = _navButtons[i].transform;

                // 更新图标颜色
                var iconTransform = btnTransform.Find("Icon");
                if (iconTransform != null)
                {
                    var iconImg = iconTransform.GetComponent<Image>();
                    if (iconImg != null)
                        iconImg.color = selected ? Color.white : new Color(0.5f, 0.5f, 0.6f);
                }

                // 更新文字颜色
                var labelTransform = btnTransform.Find("Label");
                if (labelTransform != null)
                {
                    var txt = labelTransform.GetComponent<Text>();
                    if (txt != null)
                        txt.color = selected ? UIStyleKit.TextGold : new Color(0.5f, 0.5f, 0.6f);
                }
            }
        }

        // ========== 数据刷新 ==========

        private void RefreshAll()
        {
            if (!PlayerDataManager.HasInstance) return;
            var data = PlayerDataManager.Instance.Data;

            if (_txtPlayerLevel != null) _txtPlayerLevel.text = $"Lv.{data.Level}";
            if (_txtPlayerName != null) _txtPlayerName.text = data.Nickname;
if (_txtDiamonds != null) _txtDiamonds.text = FormatNumber(data.Diamonds);
            if (_txtGold != null) _txtGold.text = FormatNumber(data.Gold);
            if (_txtStamina != null) _txtStamina.text = $"{data.Stamina}/{data.MaxStamina}";


            // 刷新英雄展示
            RefreshHeroDisplay();
        }

        private void RefreshHeroDisplay()
        {
            if (!PlayerDataManager.HasInstance) return;
            var data = PlayerDataManager.Instance.Data;

            string heroId = data.ActiveHeroId;
            if (string.IsNullOrEmpty(heroId))
            {
                if (_txtHeroName != null) _txtHeroName.text = "未选择英雄";
            }
            else
            {
                var heroConfig = HeroConfigTable.GetHero(heroId);
                if (heroConfig != null)
                {
                    if (_txtHeroName != null) _txtHeroName.text = heroConfig.Name;

                    // 加载英雄全身立绘（无框直接展示）
                    if (_imgHeroAvatar != null && !string.IsNullOrEmpty(heroConfig.SpriteName))
                    {
                        Sprite heroSprite = SpriteLoader.LoadHeroFull(heroConfig.SpriteName);
                        if (heroSprite != null)
                        {
                            _imgHeroAvatar.sprite = heroSprite;
                            _imgHeroAvatar.preserveAspect = true;
                            _imgHeroAvatar.color = Color.white;
                        }
                        else
                        {
                            _imgHeroAvatar.color = new Color(1, 1, 1, 0);
                        }
                    }
                }
            }
        }

        // ========== 事件绑定 ==========

        private void BindEvents()
        {
            if (EventBus.HasInstance)
            {
                EventBus.Instance.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
                EventBus.Instance.Subscribe<HeroActiveChangedEvent>(OnHeroActiveChanged);
                EventBus.Instance.Subscribe<RedDotChangedEvent>(OnRedDotChanged);
                EventBus.Instance.Subscribe<StaminaChangedEvent>(OnStaminaChanged);
            }
        }

        private void UnbindEvents()
        {
            if (EventBus.HasInstance)
            {
                EventBus.Instance.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
                EventBus.Instance.Unsubscribe<HeroActiveChangedEvent>(OnHeroActiveChanged);
                EventBus.Instance.Unsubscribe<RedDotChangedEvent>(OnRedDotChanged);
                EventBus.Instance.Unsubscribe<StaminaChangedEvent>(OnStaminaChanged);
            }
        }

        private void OnCurrencyChanged(CurrencyChangedEvent evt)
        {
            RefreshAll();
        }

        private void OnHeroActiveChanged(HeroActiveChangedEvent evt)
        {
            RefreshHeroDisplay();
        }

        private void OnRedDotChanged(RedDotChangedEvent evt)
        {
            UpdateRedDot(evt.NodeId, evt.HasRedDot, evt.Count);
        }

        private void OnStaminaChanged(StaminaChangedEvent evt)
        {
            if (_txtStamina != null)
_txtStamina.text = $"{evt.NewStamina}/{evt.MaxStamina}";

        }

        // ========== 动画 ==========

        private void PlayEnterAnimation()
        {
            _isAnimating = true;
            _animTimer = 0f;

            // 按钮从下方弹入
            for (int i = 0; i < _animTargets.Count; i++)
            {
                var rt = _animTargets[i];
                if (rt != null)
                {
                    var cg = rt.gameObject.GetComponent<CanvasGroup>();
                    if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                }
            }
        }

        private void Update()
        {
            if (!_isAnimating) return;

            _animTimer += Time.deltaTime;

            for (int i = 0; i < _animTargets.Count; i++)
            {
                float delay = i * 0.06f;
                float t = Mathf.Clamp01((_animTimer - delay) / 0.3f);

                if (t <= 0) continue;

                var rt = _animTargets[i];
                if (rt == null) continue;

                var cg = rt.gameObject.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    // EaseOutBack
                    float eased = EaseOutBack(t);
                    cg.alpha = Mathf.Clamp01(t * 3f);
                }
            }

            if (_animTimer > _animTargets.Count * 0.06f + 0.4f)
            {
                _isAnimating = false;
                // 确保所有元素完全显示
                for (int i = 0; i < _animTargets.Count; i++)
                {
                    var cg = _animTargets[i]?.gameObject.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 1f;
                }
            }
        }

        private float EaseOutBack(float t)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        // ========== 红点系统 ==========

        private void AddRedDot(RectTransform parent, string nodeId)
        {
            var dotObj = new GameObject($"RedDot_{nodeId}");
            dotObj.transform.SetParent(parent, false);
            var dotRect = dotObj.AddComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0.8f, 0.7f);
            dotRect.anchorMax = new Vector2(0.95f, 0.95f);
            dotRect.offsetMin = Vector2.zero;
            dotRect.offsetMax = Vector2.zero;

            var img = dotObj.AddComponent<Image>();

            // 优先加载真实红点图标
            Sprite redDotSprite = SpriteLoader.LoadUI("deco_red_dot");
            if (redDotSprite != null)
            {
                img.sprite = redDotSprite;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(1f, 0.2f, 0.15f, 1f);
                var tex = new Texture2D(16, 16);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(8, 8)) / 8f;
                        tex.SetPixel(x, y, dist < 1f ? Color.white : Color.clear);
                    }
                tex.Apply();
                img.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
            }

            dotObj.SetActive(false);
            _redDots[nodeId] = dotObj;
        }

        private void UpdateRedDot(string nodeId, bool show, int count)
        {
            if (_redDots.TryGetValue(nodeId, out var dot))
            {
                dot.SetActive(show);
            }
        }

        // ========== 工具方法 ==========

        private RectTransform CreateRect(string name, Transform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj.AddComponent<RectTransform>();
        }

        private Text CreateText(string name, RectTransform parent, string text, int fontSize,
            Color color, TextAnchor alignment)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = alignment;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            return txt;
        }

        private void SetAnchors(RectTransform rect, float xMin, float xMax, float yMin, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private string FormatNumber(long num)
        {
            if (num >= 1000000) return $"{num / 1000000f:F1}M";
            if (num >= 10000) return $"{num / 1000f:F1}K";
            return num.ToString();
        }
    }
}
