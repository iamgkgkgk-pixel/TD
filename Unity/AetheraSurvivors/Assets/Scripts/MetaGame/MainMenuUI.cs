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

            // 全屏背景
            BuildBackground();

            // 顶部信息栏
            BuildTopBar();

            // 中央英雄展示区
            BuildHeroDisplay();

            // 功能入口按钮组
            BuildFunctionButtons();

            // 底部导航栏
            BuildBottomNavBar();
        }

        /// <summary>构建背景</summary>
        private void BuildBackground()
        {
            var bgObj = new GameObject("BG");
            bgObj.transform.SetParent(transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // 深色渐变背景
            UIStyleKit.CreateGradientPanel(bgRect,
                new Color(0.04f, 0.04f, 0.10f, 1f),
                new Color(0.08f, 0.10f, 0.20f, 1f));
        }

        /// <summary>构建顶部信息栏</summary>
        private void BuildTopBar()
        {
            var topBar = CreateRect("TopBar", transform);
            topBar.anchorMin = new Vector2(0, 0.92f);
            topBar.anchorMax = new Vector2(1, 1f);
            topBar.offsetMin = Vector2.zero;
            topBar.offsetMax = Vector2.zero;

            UIStyleKit.CreateGradientPanel(topBar,
                new Color(0.06f, 0.06f, 0.14f, 0.95f),
                new Color(0.04f, 0.04f, 0.10f, 0.98f));

            // 玩家等级
            _txtPlayerLevel = CreateText("LvText", topBar, "Lv.1", 18,
                UIStyleKit.TextGold, TextAnchor.MiddleLeft);
            SetAnchors(_txtPlayerLevel.rectTransform, 0.02f, 0.2f, 0.55f, 0.95f);

            // 玩家昵称
            _txtPlayerName = CreateText("NameText", topBar, "指挥官", 16,
                UIStyleKit.TextWhite, TextAnchor.MiddleLeft);
            SetAnchors(_txtPlayerName.rectTransform, 0.02f, 0.2f, 0.1f, 0.55f);

            // 钻石显示
_txtDiamonds = CreateText("DiamondsText", topBar, "◇ 0", 16,

                new Color(0.4f, 0.7f, 1f), TextAnchor.MiddleRight);
            SetAnchors(_txtDiamonds.rectTransform, 0.55f, 0.75f, 0.2f, 0.8f);

            // 金币显示
_txtGold = CreateText("GoldText", topBar, "G 0", 16,

                new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleRight);
            SetAnchors(_txtGold.rectTransform, 0.75f, 0.92f, 0.2f, 0.8f);

            // 体力显示
_txtStamina = CreateText("StaminaText", topBar, "!60", 14,

                UIStyleKit.TextGreen, TextAnchor.MiddleRight);
            SetAnchors(_txtStamina.rectTransform, 0.92f, 1f, 0.2f, 0.8f);
        }

        /// <summary>构建英雄展示区</summary>
        private void BuildHeroDisplay()
        {
            _heroDisplayArea = CreateRect("HeroDisplay", transform);
            _heroDisplayArea.anchorMin = new Vector2(0.1f, 0.45f);
            _heroDisplayArea.anchorMax = new Vector2(0.9f, 0.88f);
            _heroDisplayArea.offsetMin = Vector2.zero;
            _heroDisplayArea.offsetMax = Vector2.zero;

            // 英雄头像占位
            var avatarObj = new GameObject("HeroAvatar");
            avatarObj.transform.SetParent(_heroDisplayArea, false);
            var avatarRect = avatarObj.AddComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0.25f, 0.15f);
            avatarRect.anchorMax = new Vector2(0.75f, 0.85f);
            avatarRect.offsetMin = Vector2.zero;
            avatarRect.offsetMax = Vector2.zero;
            _imgHeroAvatar = avatarObj.AddComponent<Image>();
            _imgHeroAvatar.color = new Color(0.2f, 0.25f, 0.4f, 0.5f);

            // 英雄名称
            _txtHeroName = CreateText("HeroName", _heroDisplayArea, "铁壁骑士", 22,
                UIStyleKit.TextGold, TextAnchor.MiddleCenter);
            SetAnchors(_txtHeroName.rectTransform, 0.1f, 0.9f, 0.0f, 0.12f);
            UIStyleKit.AddTextShadow(_txtHeroName);
        }

        /// <summary>构建功能入口按钮</summary>
        private void BuildFunctionButtons()
        {
            var btnArea = CreateRect("ButtonArea", transform);
            btnArea.anchorMin = new Vector2(0.02f, 0.15f);
            btnArea.anchorMax = new Vector2(0.98f, 0.44f);
            btnArea.offsetMin = Vector2.zero;
            btnArea.offsetMax = Vector2.zero;

            // 大按钮：开始战斗
            _btnBattle = CreateFunctionButton(btnArea, "BtnBattle", "⚔️ 开始战斗",
                new Vector2(0.2f, 0.55f), new Vector2(0.8f, 0.95f),
                UIStyleKit.BtnGreenNormal, UIStyleKit.BtnGreenHover, UIStyleKit.BtnGreenPressed, 22);
            _btnBattle.onClick.AddListener(OnBattleClick);

            // 第二排按钮
            float y0 = 0.05f, y1 = 0.48f;
            float btnW = 0.185f;
            float gap = 0.02f;

            _btnHero = CreateFunctionButton(btnArea, "BtnHero", "🦸 英雄",
                new Vector2(0f, y0), new Vector2(btnW, y1),
                UIStyleKit.BtnBlueNormal, UIStyleKit.BtnBlueHover, UIStyleKit.BtnBluePressed, 15);
            _btnHero.onClick.AddListener(OnHeroClick);

            _btnShop = CreateFunctionButton(btnArea, "BtnShop", "🛒 商城",
                new Vector2(btnW + gap, y0), new Vector2(btnW * 2 + gap, y1),
                UIStyleKit.BtnBlueNormal, UIStyleKit.BtnBlueHover, UIStyleKit.BtnBluePressed, 15);
            _btnShop.onClick.AddListener(OnShopClick);

            _btnGacha = CreateFunctionButton(btnArea, "BtnGacha", "🎰 召唤",
                new Vector2((btnW + gap) * 2, y0), new Vector2(btnW * 3 + gap * 2, y1),
                UIStyleKit.BtnBlueNormal, UIStyleKit.BtnBlueHover, UIStyleKit.BtnBluePressed, 15);
            _btnGacha.onClick.AddListener(OnGachaClick);

            _btnBattlePass = CreateFunctionButton(btnArea, "BtnBattlePass", "🎫 战令",
                new Vector2((btnW + gap) * 3, y0), new Vector2(btnW * 4 + gap * 3, y1),
                UIStyleKit.BtnBlueNormal, UIStyleKit.BtnBlueHover, UIStyleKit.BtnBluePressed, 15);
            _btnBattlePass.onClick.AddListener(OnBattlePassClick);

            _btnSocial = CreateFunctionButton(btnArea, "BtnSocial", "👥 社交",
                new Vector2((btnW + gap) * 4, y0), new Vector2(1f, y1),
                UIStyleKit.BtnBlueNormal, UIStyleKit.BtnBlueHover, UIStyleKit.BtnBluePressed, 15);
            _btnSocial.onClick.AddListener(OnSocialClick);

            // 收集动画目标
            _animTargets.Add(_btnBattle.GetComponent<RectTransform>());
            _animTargets.Add(_btnHero.GetComponent<RectTransform>());
            _animTargets.Add(_btnShop.GetComponent<RectTransform>());
            _animTargets.Add(_btnGacha.GetComponent<RectTransform>());
            _animTargets.Add(_btnBattlePass.GetComponent<RectTransform>());
            _animTargets.Add(_btnSocial.GetComponent<RectTransform>());
        }

        /// <summary>构建底部导航栏</summary>
        private void BuildBottomNavBar()
        {
            _bottomNavBar = CreateRect("BottomNavBar", transform);
            _bottomNavBar.anchorMin = new Vector2(0, 0);
            _bottomNavBar.anchorMax = new Vector2(1, 0.14f);
            _bottomNavBar.offsetMin = Vector2.zero;
            _bottomNavBar.offsetMax = Vector2.zero;

            UIStyleKit.CreateGradientPanel(_bottomNavBar,
                new Color(0.04f, 0.04f, 0.10f, 0.98f),
                new Color(0.06f, 0.06f, 0.14f, 0.95f));

            // 导航按钮
            string[] navLabels = { "📋 任务", "📬 邮件", "🏠 主页", "📊 排行", "⚙️ 设置" };
            _navButtons = new Button[navLabels.Length];
            float navBtnW = 1f / navLabels.Length;

            for (int i = 0; i < navLabels.Length; i++)
            {
                int idx = i;
                var btn = CreateNavButton(_bottomNavBar, $"Nav_{i}", navLabels[i],
                    new Vector2(navBtnW * i, 0.05f), new Vector2(navBtnW * (i + 1), 0.95f));
                btn.onClick.AddListener(() => OnNavClick(idx));
                _navButtons[i] = btn;

                // 为任务和邮件添加红点占位
                if (i == 0) AddRedDot(btn.GetComponent<RectTransform>(), "nav_quest");
                if (i == 1) AddRedDot(btn.GetComponent<RectTransform>(), "nav_mail");
            }

            // 默认选中主页
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
                var img = _navButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = (i == index)
                        ? new Color(0.15f, 0.30f, 0.55f, 0.8f)
                        : new Color(0.12f, 0.12f, 0.18f, 0.6f);
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
if (_txtDiamonds != null) _txtDiamonds.text = $"◇ {FormatNumber(data.Diamonds)}";
            if (_txtGold != null) _txtGold.text = $"G {FormatNumber(data.Gold)}";
            if (_txtStamina != null) _txtStamina.text = $"!{data.Stamina}/{data.MaxStamina}";


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
                // 从英雄配置获取名称
                var heroConfig = HeroConfigTable.GetHero(heroId);
                if (heroConfig != null && _txtHeroName != null)
                {
                    _txtHeroName.text = heroConfig.Name;
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
_txtStamina.text = $"!{evt.NewStamina}/{evt.MaxStamina}";

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
            img.color = new Color(1f, 0.2f, 0.15f, 1f);

            // 生成圆形纹理
            var tex = new Texture2D(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(8, 8)) / 8f;
                    tex.SetPixel(x, y, dist < 1f ? Color.white : Color.clear);
                }
            tex.Apply();
            img.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));

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

        private Button CreateFunctionButton(RectTransform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color normal, Color hover, Color pressed, int fontSize = 16)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = obj.AddComponent<Image>();
            img.color = normal;

            var btn = obj.AddComponent<Button>();
            UIStyleKit.StyleButton(btn, normal, hover, pressed);

            // 按钮文字
            var txtObj = new GameObject("Label");
            txtObj.transform.SetParent(obj.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = new Vector2(4, 2);
            txtRect.offsetMax = new Vector2(-4, -2);

            var txt = txtObj.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = fontSize;
            txt.color = UIStyleKit.TextWhite;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            txt.raycastTarget = false;
            UIStyleKit.AddTextShadow(txt);

            return btn;
        }

        private Button CreateNavButton(RectTransform parent, string name, string label,
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
            img.color = new Color(0.12f, 0.12f, 0.18f, 0.6f);

            var btn = obj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1, 1, 1, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            var txtObj = new GameObject("Label");
            txtObj.transform.SetParent(obj.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            var txt = txtObj.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = 13;
            txt.color = UIStyleKit.TextWhite;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 13);
            txt.raycastTarget = false;

            return btn;
        }

        private string FormatNumber(long num)
        {
            if (num >= 1000000) return $"{num / 1000000f:F1}M";
            if (num >= 10000) return $"{num / 1000f:F1}K";
            return num.ToString();
        }
    }
}
