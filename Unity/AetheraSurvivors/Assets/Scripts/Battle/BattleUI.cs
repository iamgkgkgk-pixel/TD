// ============================================================
// 文件名：BattleUI.cs
// 功能描述：战斗UI — 完整的战斗界面
//          金币显示、波次信息、生命值、放塔按钮、词条选择、
//          暂停/加速、战斗结算界面
// 创建时间：2026-03-25
// 所属模块：Battle
// 对应交互：阶段三 G3-1（核心战斗可玩）
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Tower;
using AetheraSurvivors.Battle.Map;
using AetheraSurvivors.Battle.Wave;
using AetheraSurvivors.Battle.Rune;
using AetheraSurvivors.Battle.Visual;
using Logger = AetheraSurvivors.Framework.Logger;



namespace AetheraSurvivors.Battle
{
    /// <summary>
    /// 战斗UI — 完整的战斗单局界面
    /// 
    /// 功能：
    /// 1. 顶部状态栏（金币、波次、基地HP、时间）
    /// 2. 底部放塔栏（6种塔按钮，显示费用）
    /// 3. 右上角控制按钮（暂停、加速、开始波次）
    /// 4. 词条选择面板（3选1弹出层）
    /// 5. 战斗结算面板（胜利/失败）
    /// 6. 选中塔详情面板（升级/出售）
    /// 
    /// 全部使用代码动态创建UI，不依赖预制体
    /// </summary>
    public class BattleUI : MonoBehaviour
    {
        // ========== UI根节点 ==========

        private Canvas _canvas;
        private CanvasScaler _scaler;

        // ========== 顶部状态栏 ==========

        private Text _goldText;
        private Text _waveText;
        private Text _hpText;
        private Text _timerText;

        // ========== 底部放塔栏 ==========

        private RectTransform _towerBar;
        private Button[] _towerButtons = new Button[6];
        private Text[] _towerCostTexts = new Text[6];

        // ========== 右侧控制按钮 ==========

        private Button _pauseBtn;
        private Text _pauseText;
        private Button _speedBtn;
        private Text _speedText;
        private Button _startWaveBtn;
        private Text _startWaveText;

        // ========== 词条选择面板 ==========

        private GameObject _runePanel;
        private Button[] _runeButtons = new Button[3];
        private Text[] _runeNameTexts = new Text[3];
        private Text[] _runeDescTexts = new Text[3];
        private Text _runeTitleText;

        // ========== 结算面板 ==========

        private GameObject _resultPanel;
        private Text _resultTitleText;
        private Text _resultDetailText;
        private Button _resultRestartBtn;
        private Button _resultExitBtn;

        // ========== 选中塔面板 ==========

        private GameObject _towerInfoPanel;
        private Text _towerInfoName;
        private Text _towerInfoLevel;
        private Button _upgradeBtn;
        private Text _upgradeBtnText;
        private Button _sellBtn;
        private Text _sellBtnText;

        // ========== 提示文本 ==========

        private Text _centerNotice;
        private float _noticeTimer;

        // ========== [G3-5] 快捷建造面板（单击空地弹出） ==========

        private GameObject _quickBuildPanel;
        private Button[] _quickBuildButtons = new Button[6];
        private Text[] _quickBuildCostTexts = new Text[6];
        private Vector2Int _quickBuildGridPos;
        private bool _quickBuildVisible = false;

        // ========== [G3-5] 拖拽放塔相关 ==========

        private int _dragTowerIndex = -1;
        private bool _isDragPending = false;
        private Vector2 _dragStartPos;
        private static readonly float DragStartThreshold = 15f;

        // ========== [G3-5] 放塔模式高亮 ==========

        private Image[] _towerBtnImages = new Image[6];

        // ========== 缓存数据 ==========

        private RuneConfig[] _currentRuneOptions;
        private bool _isSubscribed = false;


        // ========== 塔类型信息 ==========

        private static readonly TowerType[] TowerTypes = {
            TowerType.Archer, TowerType.Mage, TowerType.Ice,
            TowerType.Cannon, TowerType.Poison, TowerType.GoldMine
        };

        private static readonly string[] TowerNames = {
            "箭塔", "法塔", "冰塔", "炮塔", "毒塔", "金矿"
        };

        private static readonly string[] TowerIcons = {
            "[弓]", "[法]", "[冰]", "[炮]", "[毒]", "[$]"
        };


        // ========== 生命周期 ==========

        private void Awake()
        {
            try
            {
                Logger.I("BattleUI", "BattleUI.Awake 开始构建UI...");
                BuildUI();
                Logger.I("BattleUI", "BattleUI.BuildUI 完成，Canvas={0}, sortingOrder={1}",
                    _canvas != null, _canvas != null ? _canvas.sortingOrder : -1);
            }
            catch (System.Exception e)
            {
                Logger.E("BattleUI", "BuildUI异常: {0}\n{1}", e.Message, e.StackTrace);
            }

            try
            {
                SubscribeEvents();
                RegisterInputCallbacks();
            }
            catch (System.Exception e)
            {
                Logger.E("BattleUI", "事件订阅异常: {0}", e.Message);
            }
        }


        private void OnDestroy()
        {
            UnsubscribeEvents();
            UnregisterInputCallbacks();
        }


        private bool _firstFrameLogged = false;

        private void Update()
        {
            // 第一帧运行时诊断（此时Canvas布局已完成）
            if (!_firstFrameLogged)
            {
                _firstFrameLogged = true;
                var canvasRT = transform as RectTransform;
                Logger.W("BattleUI", "===== 第一帧运行时诊断 =====");
                Logger.W("BattleUI", "Canvas GO: active={0}, activeInHierarchy={1}, enabled={2}",
                    gameObject.activeSelf, gameObject.activeInHierarchy, _canvas?.enabled);
                Logger.W("BattleUI", "Canvas RT: rect={0}, sizeDelta={1}, localScale={2}",
                    canvasRT?.rect, canvasRT?.sizeDelta, canvasRT?.localScale);
                Logger.W("BattleUI", "CanvasScaler: scaleMode={0}, scaleFactor={1}, refRes={2}",
                    _scaler?.uiScaleMode, _scaler?.scaleFactor, _scaler?.referenceResolution);

                // 检查所有一级子物体的实际rect
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i) as RectTransform;
                    if (child == null) continue;
                    var img = child.GetComponent<Image>();
                    var canvasGroup = child.GetComponent<CanvasGroup>();
                    Logger.W("BattleUI", "  子[{0}] {1}: active={2}, rect={3}, pos={4}, hasImg={5}, imgEnabled={6}, imgColor={7}, canvasGroup={8}",
                        i, child.name, child.gameObject.activeSelf,
                        child.rect, child.anchoredPosition,
                        img != null, img?.enabled, img?.color,
                        canvasGroup != null ? $"alpha={canvasGroup.alpha},blocksRaycasts={canvasGroup.blocksRaycasts}" : "none");

                    // 检查子物体的子物体（文字等）
                    for (int j = 0; j < child.childCount && j < 5; j++)
                    {
                        var grandChild = child.GetChild(j) as RectTransform;
                        if (grandChild == null) continue;
                        var txt = grandChild.GetComponent<Text>();
                        var gImg = grandChild.GetComponent<Image>();
                        Logger.W("BattleUI", "    孙[{0}.{1}] {2}: active={3}, rect={4}, text={5}, img={6}",
                            i, j, grandChild.name, grandChild.gameObject.activeSelf,
                            grandChild.rect,
                            txt != null ? $"'{txt.text}' font={txt.font?.name} size={txt.fontSize} color={txt.color}" : "null",
                            gImg != null ? $"enabled={gImg.enabled} color={gImg.color}" : "null");
                    }
                }

                // 检查是否有其他Canvas在场景中
                var allCanvases = FindObjectsOfType<Canvas>();
                Logger.W("BattleUI", "场景中Canvas总数={0}", allCanvases.Length);
                foreach (var c in allCanvases)
                {
                    Logger.W("BattleUI", "  Canvas: {0}, renderMode={1}, sortingOrder={2}, enabled={3}, worldCamera={4}",
                        c.gameObject.name, c.renderMode, c.sortingOrder, c.enabled,
                        c.worldCamera?.name ?? "null");
                }

                Logger.W("BattleUI", "===== 第一帧诊断结束 =====");

                // 调试红色方块已移除（Canvas渲染已验证正常）

            }

            UpdateStatusBar();

            UpdateNotice();
            UpdateTowerButtonStates();
            UpdateStartWaveButton();
            UpdateDragFromButton();
            UpdatePlacementHighlight();
            HandleExitPlacementMode();
        }



        // ========== 事件订阅 ==========

        private void SubscribeEvents()
        {
            if (_isSubscribed) return;
            _isSubscribed = true;

            EventBus.Instance.Subscribe<BattleStateChangedEvent>(OnBattleStateChanged);
            EventBus.Instance.Subscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Instance.Subscribe<BaseHealthChangedEvent>(OnBaseHealthChanged);
            EventBus.Instance.Subscribe<WaveStartEvent>(OnWaveStart);
            EventBus.Instance.Subscribe<WaveCompleteEvent>(OnWaveComplete);
            EventBus.Instance.Subscribe<RuneSelectionEvent>(OnRuneSelection);
            EventBus.Instance.Subscribe<BattleResultEvent>(OnBattleResult);
            EventBus.Instance.Subscribe<TowerSelectedEvent>(OnTowerSelected);
            EventBus.Instance.Subscribe<TowerDeselectedEvent>(OnTowerDeselected);
        }

        private void UnsubscribeEvents()
        {
            if (!_isSubscribed) return;
            _isSubscribed = false;

            if (EventBus.HasInstance)
            {
                EventBus.Instance.Unsubscribe<BattleStateChangedEvent>(OnBattleStateChanged);
                EventBus.Instance.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
                EventBus.Instance.Unsubscribe<BaseHealthChangedEvent>(OnBaseHealthChanged);
                EventBus.Instance.Unsubscribe<WaveStartEvent>(OnWaveStart);
                EventBus.Instance.Unsubscribe<WaveCompleteEvent>(OnWaveComplete);
                EventBus.Instance.Unsubscribe<RuneSelectionEvent>(OnRuneSelection);
                EventBus.Instance.Unsubscribe<BattleResultEvent>(OnBattleResult);
                EventBus.Instance.Unsubscribe<TowerSelectedEvent>(OnTowerSelected);
                EventBus.Instance.Unsubscribe<TowerDeselectedEvent>(OnTowerDeselected);
            }
        }

        // ================================================================
        // UI构建 — 全部代码动态创建
        // ================================================================

        private void BuildUI()
        {
            // 创建Canvas
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            _canvas.pixelPerfect = true; // 像素对齐，消除UI模糊和锯齿

            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // 根据屏幕方向自动选择参考分辨率，确保横竖屏都能正确显示
            bool isLandscape = Screen.width >= Screen.height;
            _scaler.referenceResolution = isLandscape ? new Vector2(1920, 1080) : new Vector2(1080, 1920);
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.matchWidthOrHeight = 0.5f;
            _scaler.referencePixelsPerUnit = 100;


            gameObject.AddComponent<GraphicRaycaster>();

            Logger.W("BattleUI", "===== BuildUI 开始 =====");
            Logger.W("BattleUI", "Canvas: renderMode={0}, sortingOrder={1}, enabled={2}",
                _canvas.renderMode, _canvas.sortingOrder, _canvas.enabled);
            Logger.W("BattleUI", "Screen: {0}x{1}, CanvasRect: {2}",
                Screen.width, Screen.height, (transform as RectTransform)?.rect);

            // 检查字体
            var testFont = GetFont();
            Logger.W("BattleUI", "字体检查: font={0}, fontName={1}",
                testFont != null, testFont?.name ?? "NULL");

            try { BuildTopStatusBar(); }
            catch (System.Exception e) { Logger.E("BattleUI", "BuildTopStatusBar异常: {0}\n{1}", e.Message, e.StackTrace); }
            Logger.W("BattleUI", "TopStatusBar: _goldText={0}, _waveText={1}, _hpText={2}, _timerText={3}",
                _goldText != null, _waveText != null, _hpText != null, _timerText != null);
            if (_goldText != null)
                Logger.W("BattleUI", "  _goldText: active={0}, text='{1}', font={2}, fontSize={3}, color={4}, rectPos={5}, rectSize={6}",
                    _goldText.gameObject.activeInHierarchy, _goldText.text, _goldText.font?.name ?? "NULL",
                    _goldText.fontSize, _goldText.color, _goldText.rectTransform.anchoredPosition, _goldText.rectTransform.sizeDelta);

            try { BuildBottomTowerBar(); }
            catch (System.Exception e) { Logger.E("BattleUI", "BuildBottomTowerBar异常: {0}\n{1}", e.Message, e.StackTrace); }
            Logger.W("BattleUI", "BottomTowerBar: _towerBar={0}, active={1}",
                _towerBar != null, _towerBar?.gameObject.activeInHierarchy);
            if (_towerBar != null)
                Logger.W("BattleUI", "  _towerBar: anchorMin={0}, anchorMax={1}, sizeDelta={2}, pivot={3}",
                    _towerBar.anchorMin, _towerBar.anchorMax, _towerBar.sizeDelta, _towerBar.pivot);

            try { BuildControlButtons(); }
            catch (System.Exception e) { Logger.E("BattleUI", "BuildControlButtons异常: {0}\n{1}", e.Message, e.StackTrace); }
            Logger.W("BattleUI", "ControlButtons: _pauseBtn={0}, _speedBtn={1}, _startWaveBtn={2}",
                _pauseBtn != null, _speedBtn != null, _startWaveBtn != null);
            if (_pauseBtn != null)
            {
                var pr = _pauseBtn.GetComponent<RectTransform>();
                Logger.W("BattleUI", "  _pauseBtn: active={0}, pos={1}, size={2}, img={3}",
                    _pauseBtn.gameObject.activeInHierarchy, pr.anchoredPosition, pr.sizeDelta,
                    (_pauseBtn.targetGraphic as Image)?.sprite != null);
            }

            try { BuildRunePanel(); }
            catch (System.Exception e) { Logger.E("BattleUI", "BuildRunePanel异常: {0}\n{1}", e.Message, e.StackTrace); }

            try { BuildResultPanel(); }
            catch (System.Exception e) { Logger.E("BattleUI", "BuildResultPanel异常: {0}\n{1}", e.Message, e.StackTrace); }

            try { BuildTowerInfoPanel(); }
            catch (System.Exception e) { Logger.E("BattleUI", "BuildTowerInfoPanel异常: {0}\n{1}", e.Message, e.StackTrace); }

            try { BuildQuickBuildPanel(); }
            catch (System.Exception e) { Logger.E("BattleUI", "BuildQuickBuildPanel异常: {0}\n{1}", e.Message, e.StackTrace); }

            try { BuildCenterNotice(); }
            catch (System.Exception e) { Logger.E("BattleUI", "BuildCenterNotice异常: {0}\n{1}", e.Message, e.StackTrace); }

            // 最终检查：遍历所有子物体，输出层级信息
            Logger.W("BattleUI", "===== BuildUI 完成，子物体数量={0} =====", transform.childCount);
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var crt = child as RectTransform;
                Logger.W("BattleUI", "  子物体[{0}]: name={1}, active={2}, anchorMin={3}, anchorMax={4}, sizeDelta={5}",
                    i, child.name, child.gameObject.activeSelf,
                    crt?.anchorMin, crt?.anchorMax, crt?.sizeDelta);
            }



            // 初始状态隐藏弹出面板
            _runePanel.SetActive(false);
            _resultPanel.SetActive(false);
            _towerInfoPanel.SetActive(false);
            _quickBuildPanel.SetActive(false);

            Logger.W("BattleUI", "===== 弹出面板已隐藏，BuildUI全部完成 =====");

            // ★ 入场动效 — 顶部栏从上方滑入，底部栏从下方滑入
            UIAnimator.SlideFromTop(_goldText.rectTransform.parent as RectTransform, 80f, 0.4f);
            UIAnimator.FadeIn(_goldText.rectTransform.parent as RectTransform, 0.3f);
            UIAnimator.SlideFromBottom(_towerBar, 130f, 0.45f).SetDelay(0.1f);
            UIAnimator.FadeIn(_towerBar, 0.35f).SetDelay(0.1f);
            // 塔按钮依次入场
            var towerBtnRects = new RectTransform[6];
            for (int i = 0; i < 6; i++)
                towerBtnRects[i] = _towerButtons[i].GetComponent<RectTransform>();
            UIAnimator.StaggeredSlideIn(towerBtnRects, 0.05f, 0.3f, 40f);
        }



        // ---------- 顶部状态栏 ----------

        private void BuildTopStatusBar()
        {
            // 顶部背景条 — 渐变背景（上深下浅，自然过渡）
            var topBar = CreatePanel("TopBar", transform as RectTransform);
            SetAnchors(topBar, new Vector2(0, 1), new Vector2(1, 1));
            topBar.anchoredPosition = new Vector2(0, 0);
            topBar.sizeDelta = new Vector2(0, 90);
            topBar.pivot = new Vector2(0.5f, 1f);

            var topBarImg = topBar.gameObject.AddComponent<Image>();
            topBarImg.color = new Color(0.12f, 0.14f, 0.18f, 0.92f); // 深色半透明背景
            topBarImg.raycastTarget = true;





            // 底部分隔线
            UIStyleKit.CreateSeparator(topBar, 0f, UIStyleKit.BorderGold * 0.6f, 1.5f);

            // 金币 — 金色文字+阴影
            _goldText = CreateText("GoldText", topBar, "200", 32, TextAnchor.MiddleLeft, UIStyleKit.TextGold);

            SetAnchors(_goldText.rectTransform, new Vector2(0.03f, 0), new Vector2(0.22f, 1));
            _goldText.rectTransform.anchoredPosition = Vector2.zero;
            _goldText.rectTransform.sizeDelta = Vector2.zero;
            _goldText.fontStyle = FontStyle.Bold;
            UIStyleKit.AddTextShadow(_goldText);

            // 波次 — 白色文字
            _waveText = CreateText("WaveText", topBar, "波次 0/5", 28, TextAnchor.MiddleCenter, UIStyleKit.TextWhite);

            SetAnchors(_waveText.rectTransform, new Vector2(0.25f, 0), new Vector2(0.50f, 1));
            _waveText.rectTransform.anchoredPosition = Vector2.zero;
            _waveText.rectTransform.sizeDelta = Vector2.zero;
            UIStyleKit.AddTextShadow(_waveText);

            // 基地HP — 红色系
_hpText = CreateText("HPText", topBar, "HP 20/20", 28, TextAnchor.MiddleCenter, UIStyleKit.TextHP);


            SetAnchors(_hpText.rectTransform, new Vector2(0.50f, 0), new Vector2(0.75f, 1));
            _hpText.rectTransform.anchoredPosition = Vector2.zero;
            _hpText.rectTransform.sizeDelta = Vector2.zero;
            UIStyleKit.AddTextShadow(_hpText);

            // 时间 — 灰色
            _timerText = CreateText("TimerText", topBar, "0:00", 24, TextAnchor.MiddleRight, UIStyleKit.TextGray);

            SetAnchors(_timerText.rectTransform, new Vector2(0.78f, 0), new Vector2(0.97f, 1));
            _timerText.rectTransform.anchoredPosition = Vector2.zero;
            _timerText.rectTransform.sizeDelta = Vector2.zero;
            UIStyleKit.AddTextShadow(_timerText);
        }


        // ---------- 底部放塔栏 ----------

        private void BuildBottomTowerBar()
        {
            _towerBar = CreatePanel("TowerBar", transform as RectTransform);
            SetAnchors(_towerBar, new Vector2(0, 0), new Vector2(1, 0));
            _towerBar.anchoredPosition = new Vector2(0, 0);
            _towerBar.sizeDelta = new Vector2(0, 140);

            _towerBar.pivot = new Vector2(0.5f, 0f);
            var towerBarImg = _towerBar.gameObject.AddComponent<Image>();
            towerBarImg.color = new Color(0.12f, 0.14f, 0.18f, 0.92f); // 深色半透明背景
            towerBarImg.raycastTarget = true;





            // 顶部分隔线
            UIStyleKit.CreateSeparator(_towerBar, 1f, UIStyleKit.BorderGold * 0.5f, 1f);

            float btnWidth = 1f / 6f;
            for (int i = 0; i < 6; i++)
            {
                int index = i; // 闭包捕获
                var btnRect = CreatePanel($"TowerBtn_{i}", _towerBar);
                SetAnchors(btnRect, new Vector2(btnWidth * i + 0.008f, 0.08f), new Vector2(btnWidth * (i + 1) - 0.008f, 0.92f));
                btnRect.anchoredPosition = Vector2.zero;
                btnRect.sizeDelta = Vector2.zero;

                // 卡片式背景（圆角+边框）
                var btnImg = UIStyleKit.CreateStyledPanel(btnRect, UIStyleKit.TowerCardBg,
                    UIStyleKit.BorderSilver * 0.5f, 8, 1);
                _towerBtnImages[i] = btnImg;

                _towerButtons[i] = btnRect.gameObject.AddComponent<Button>();
                _towerButtons[i].targetGraphic = btnImg;

                // 设置按钮交互颜色
                var colors = _towerButtons[i].colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.2f, 1.2f, 1.3f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.85f);
                colors.disabledColor = new Color(0.5f, 0.4f, 0.4f);
                colors.fadeDuration = 0.1f;
                _towerButtons[i].colors = colors;

                // [G3-5] 添加拖拽检测（EventTrigger）
                AddDragDetection(btnRect.gameObject, index);

                // 塔图标 — 更大更醒目
                var iconText = CreateText($"Icon_{i}", btnRect, TowerIcons[i], 36, TextAnchor.MiddleCenter, Color.white);

                SetAnchors(iconText.rectTransform, new Vector2(0, 0.48f), new Vector2(1, 0.95f));
                iconText.rectTransform.anchoredPosition = Vector2.zero;
                iconText.rectTransform.sizeDelta = Vector2.zero;

                // 名称 — 白色+阴影
                var nameText = CreateText($"Name_{i}", btnRect, TowerNames[i], 20, TextAnchor.UpperCenter, UIStyleKit.TextWhite);

                SetAnchors(nameText.rectTransform, new Vector2(0, 0.24f), new Vector2(1, 0.50f));
                nameText.rectTransform.anchoredPosition = Vector2.zero;
                nameText.rectTransform.sizeDelta = Vector2.zero;
                UIStyleKit.AddTextShadow(nameText);

                // 费用 — 金色+阴影
                _towerCostTexts[i] = CreateText($"Cost_{i}", btnRect, "$80", 18, TextAnchor.MiddleCenter, UIStyleKit.TextGold);

                SetAnchors(_towerCostTexts[i].rectTransform, new Vector2(0, 0.02f), new Vector2(1, 0.26f));
                _towerCostTexts[i].rectTransform.anchoredPosition = Vector2.zero;
                _towerCostTexts[i].rectTransform.sizeDelta = Vector2.zero;
                UIStyleKit.AddTextShadow(_towerCostTexts[i]);

                _towerButtons[i].onClick.AddListener(() => OnTowerButtonClick(index));
            }

            // 初始化费用显示
            UpdateTowerCosts();
        }


        // ---------- [G3-5] 快捷建造面板 ----------

        private void BuildQuickBuildPanel()
        {
            _quickBuildPanel = new GameObject("QuickBuildPanel");
            _quickBuildPanel.transform.SetParent(transform, false);
            var panelRect = _quickBuildPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.sizeDelta = new Vector2(380, 320);


            // 圆角背景+边框
            UIStyleKit.CreateStyledPanel(panelRect, UIStyleKit.PanelBgDark, UIStyleKit.BorderGold * 0.7f, 14, 2);

            // 标题 — 金色+描边
var title = CreateText("QBTitle", panelRect, "■ 选择建造", 24, TextAnchor.MiddleCenter, UIStyleKit.TextGold);


            SetAnchors(title.rectTransform, new Vector2(0, 0.88f), new Vector2(0.85f, 1f));
            title.rectTransform.anchoredPosition = Vector2.zero;
            title.rectTransform.sizeDelta = Vector2.zero;
            title.fontStyle = FontStyle.Bold;
            UIStyleKit.AddTextShadow(title);

            // 分隔线
            UIStyleKit.CreateSeparator(panelRect, 0.87f, UIStyleKit.BorderGold * 0.4f);

            // 6个塔按钮（排成3×2网格）
            for (int i = 0; i < 6; i++)
            {
                int index = i;
                int col = i % 3;
                int row = i / 3;

                float xStart = 0.03f + col * 0.32f;
                float xEnd = xStart + 0.30f;
                float yTop = 0.84f - row * 0.42f;
                float yBot = yTop - 0.38f;

                var btnRect = CreatePanel($"QBBtn_{i}", panelRect);
                SetAnchors(btnRect, new Vector2(xStart, yBot), new Vector2(xEnd, yTop));
                btnRect.anchoredPosition = Vector2.zero;
                btnRect.sizeDelta = Vector2.zero;

                // 卡片式背景
                var btnImg = UIStyleKit.CreateStyledPanel(btnRect, UIStyleKit.TowerCardBg,
                    UIStyleKit.BorderSilver * 0.4f, 8, 1);
                _quickBuildButtons[i] = btnRect.gameObject.AddComponent<Button>();
                _quickBuildButtons[i].targetGraphic = btnImg;
                _quickBuildButtons[i].onClick.AddListener(() => OnQuickBuildClick(index));

                var colors = _quickBuildButtons[i].colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.15f, 1.15f, 1.25f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.85f);
                colors.disabledColor = new Color(0.5f, 0.4f, 0.4f);
                colors.fadeDuration = 0.1f;
                _quickBuildButtons[i].colors = colors;

                // 图标
                var icon = CreateText($"QBIcon_{i}", btnRect, TowerIcons[i], 32, TextAnchor.MiddleCenter, Color.white);

                SetAnchors(icon.rectTransform, new Vector2(0, 0.45f), new Vector2(1, 0.95f));
                icon.rectTransform.anchoredPosition = Vector2.zero;
                icon.rectTransform.sizeDelta = Vector2.zero;


                // 名称
                var name = CreateText($"QBName_{i}", btnRect, TowerNames[i], 18, TextAnchor.MiddleCenter, UIStyleKit.TextWhite);

                SetAnchors(name.rectTransform, new Vector2(0, 0.22f), new Vector2(1, 0.48f));
                name.rectTransform.anchoredPosition = Vector2.zero;
                name.rectTransform.sizeDelta = Vector2.zero;
                UIStyleKit.AddTextShadow(name);

                // 费用
                _quickBuildCostTexts[i] = CreateText($"QBCost_{i}", btnRect, "$80", 18, TextAnchor.MiddleCenter, UIStyleKit.TextGold);

                SetAnchors(_quickBuildCostTexts[i].rectTransform, new Vector2(0, 0.02f), new Vector2(1, 0.25f));
                _quickBuildCostTexts[i].rectTransform.anchoredPosition = Vector2.zero;
                _quickBuildCostTexts[i].rectTransform.sizeDelta = Vector2.zero;
                UIStyleKit.AddTextShadow(_quickBuildCostTexts[i]);

            }

            // 关闭按钮 — 红色圆角
            var closeBtn = CreateButton("QBClose", panelRect, "✖", 22,

                UIStyleKit.BtnRedNormal, () => HideQuickBuildPanel());
            var closeRect = closeBtn.GetComponent<RectTransform>();
            SetAnchors(closeRect, new Vector2(0.85f, 0.88f), new Vector2(1f, 1f));
            closeRect.anchoredPosition = Vector2.zero;
            closeRect.sizeDelta = Vector2.zero;
            UIStyleKit.StyleRedButton(closeBtn);


            _quickBuildPanel.SetActive(false);
        }


        // ---------- 右侧控制按钮 ----------

        private void BuildControlButtons()
        {
            // 暂停按钮 — 灰色圆角
            _pauseBtn = CreateButton("PauseBtn", transform as RectTransform, "⏸", 28,
                UIStyleKit.BtnGrayNormal, OnPauseClick);
            var pauseRect = _pauseBtn.GetComponent<RectTransform>();
            SetAnchors(pauseRect, new Vector2(1, 1), new Vector2(1, 1));
            pauseRect.anchoredPosition = new Vector2(-42, -115);
            pauseRect.sizeDelta = new Vector2(68, 48);

            _pauseText = _pauseBtn.GetComponentInChildren<Text>();
            UIStyleKit.StyleGrayButton(_pauseBtn);
            UIStyleKit.AddTextShadow(_pauseText);

            // 加速按钮 — 蓝色圆角
            _speedBtn = CreateButton("SpeedBtn", transform as RectTransform, "1x", 24,
                UIStyleKit.BtnBlueNormal, OnSpeedClick);
            var speedRect = _speedBtn.GetComponent<RectTransform>();
            SetAnchors(speedRect, new Vector2(1, 1), new Vector2(1, 1));
            speedRect.anchoredPosition = new Vector2(-42, -172);
            speedRect.sizeDelta = new Vector2(68, 48);

            _speedText = _speedBtn.GetComponentInChildren<Text>();
            UIStyleKit.StyleBlueButton(_speedBtn);
            UIStyleKit.AddTextShadow(_speedText);

            // 开始波次按钮 — 绿色圆角，更大更醒目
            _startWaveBtn = CreateButton("StartWaveBtn", transform as RectTransform, "▶ 开始", 22,
                UIStyleKit.BtnGreenNormal, OnStartWaveClick);
            var swRect = _startWaveBtn.GetComponent<RectTransform>();
            SetAnchors(swRect, new Vector2(1, 1), new Vector2(1, 1));
            swRect.anchoredPosition = new Vector2(-60, -232);
            swRect.sizeDelta = new Vector2(105, 50);

            _startWaveText = _startWaveBtn.GetComponentInChildren<Text>();
            UIStyleKit.StyleGreenButton(_startWaveBtn);
            UIStyleKit.AddTextShadow(_startWaveText);
        }


        // ---------- 词条选择面板 ----------

        private void BuildRunePanel()
        {
            _runePanel = new GameObject("RunePanel");
            _runePanel.transform.SetParent(transform, false);
            var panelRect = _runePanel.AddComponent<RectTransform>();
            SetAnchors(panelRect, Vector2.zero, Vector2.one);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = Vector2.zero;

            // 半透明遮罩
            AddImage(panelRect, new Color(0, 0, 0, 0.65f));

            // 中央内容区域（圆角面板）
            var contentPanel = CreatePanel("RuneContent", panelRect);
            SetAnchors(contentPanel, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.85f));
            contentPanel.anchoredPosition = Vector2.zero;
            contentPanel.sizeDelta = Vector2.zero;
            UIStyleKit.CreateStyledPanel(contentPanel, UIStyleKit.PanelBgDark, UIStyleKit.BorderGold, 16, 2);
            UIStyleKit.AddCornerDecorations(contentPanel, UIStyleKit.BorderGold * 0.7f);

            // 标题 — 金色大字+描边
_runeTitleText = CreateText("RuneTitle", contentPanel, "◆ 选择词条", 36, TextAnchor.MiddleCenter, UIStyleKit.TextGold);


            SetAnchors(_runeTitleText.rectTransform, new Vector2(0.1f, 0.85f), new Vector2(0.9f, 0.97f));
            _runeTitleText.rectTransform.anchoredPosition = Vector2.zero;
            _runeTitleText.rectTransform.sizeDelta = Vector2.zero;
            _runeTitleText.fontStyle = FontStyle.Bold;
            UIStyleKit.AddTextOutline(_runeTitleText);

            // 分隔线
            UIStyleKit.CreateSeparator(contentPanel, 0.84f, UIStyleKit.BorderGold * 0.5f);

            // 3个词条按钮 — 卡片式设计
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                float yTop = 0.80f - i * 0.26f;
                float yBot = yTop - 0.22f;

                var btnRect = CreatePanel($"RuneBtn_{i}", contentPanel);
                SetAnchors(btnRect, new Vector2(0.04f, yBot), new Vector2(0.96f, yTop));
                btnRect.anchoredPosition = Vector2.zero;
                btnRect.sizeDelta = Vector2.zero;

                // 卡片背景（圆角+边框）
                var btnImg = UIStyleKit.CreateStyledPanel(btnRect,
                    new Color(0.12f, 0.15f, 0.30f, 0.95f),
                    new Color(0.4f, 0.45f, 0.65f, 0.4f), 10, 1);
                _runeButtons[i] = btnRect.gameObject.AddComponent<Button>();
                _runeButtons[i].targetGraphic = btnImg;
                _runeButtons[i].onClick.AddListener(() => OnRuneOptionClick(index));

                // 按钮交互色
                var colors = _runeButtons[i].colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.15f, 1.15f, 1.25f);
                colors.pressedColor = new Color(0.85f, 0.85f, 0.9f);
                colors.fadeDuration = 0.1f;
                _runeButtons[i].colors = colors;

                // 词条名 — 白色加粗+阴影
                _runeNameTexts[i] = CreateText($"RuneName_{i}", btnRect, "词条名称", 28, TextAnchor.MiddleLeft, Color.white);

                SetAnchors(_runeNameTexts[i].rectTransform, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.95f));
                _runeNameTexts[i].rectTransform.anchoredPosition = Vector2.zero;
                _runeNameTexts[i].rectTransform.sizeDelta = Vector2.zero;
                _runeNameTexts[i].fontStyle = FontStyle.Bold;
                UIStyleKit.AddTextShadow(_runeNameTexts[i]);

                // 词条描述 — 浅灰色
                _runeDescTexts[i] = CreateText($"RuneDesc_{i}", btnRect, "效果描述", 22, TextAnchor.MiddleLeft, UIStyleKit.TextGray);

                SetAnchors(_runeDescTexts[i].rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.5f));
                _runeDescTexts[i].rectTransform.anchoredPosition = Vector2.zero;
                _runeDescTexts[i].rectTransform.sizeDelta = Vector2.zero;
            }
        }


        // ---------- 结算面板 ----------

        private void BuildResultPanel()
        {
            _resultPanel = new GameObject("ResultPanel");
            _resultPanel.transform.SetParent(transform, false);
            var panelRect = _resultPanel.AddComponent<RectTransform>();
            SetAnchors(panelRect, Vector2.zero, Vector2.one);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = Vector2.zero;

            // 半透明遮罩
            AddImage(panelRect, new Color(0, 0, 0, 0.75f));

            // 中央面板（圆角+金色边框+角标装饰）
            var centerPanel = CreatePanel("CenterPanel", panelRect);
            SetAnchors(centerPanel, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.78f));
            centerPanel.anchoredPosition = Vector2.zero;
            centerPanel.sizeDelta = Vector2.zero;
            UIStyleKit.CreateStyledPanel(centerPanel, UIStyleKit.PanelBgDark, UIStyleKit.BorderGold, 16, 2);
            UIStyleKit.AddCornerDecorations(centerPanel, UIStyleKit.BorderGold * 0.8f);

            // 标题 — 大字+描边
            _resultTitleText = CreateText("ResultTitle", centerPanel, "胜利！", 44, TextAnchor.MiddleCenter, UIStyleKit.TextGold);

            SetAnchors(_resultTitleText.rectTransform, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.95f));
            _resultTitleText.rectTransform.anchoredPosition = Vector2.zero;
            _resultTitleText.rectTransform.sizeDelta = Vector2.zero;
            _resultTitleText.fontStyle = FontStyle.Bold;
            UIStyleKit.AddTextOutline(_resultTitleText);

            // 分隔线
            UIStyleKit.CreateSeparator(centerPanel, 0.70f, UIStyleKit.BorderGold * 0.5f);

            // 详细信息 — 白色+阴影
            _resultDetailText = CreateText("ResultDetail", centerPanel, "", 26, TextAnchor.UpperCenter, UIStyleKit.TextWhite);

            SetAnchors(_resultDetailText.rectTransform, new Vector2(0.1f, 0.25f), new Vector2(0.9f, 0.68f));
            _resultDetailText.rectTransform.anchoredPosition = Vector2.zero;
            _resultDetailText.rectTransform.sizeDelta = Vector2.zero;
            UIStyleKit.AddTextShadow(_resultDetailText);

            // 重新开始按钮 — 绿色圆角
            _resultRestartBtn = CreateButton("RestartBtn", centerPanel, "🔄 重新开始", 24,

                UIStyleKit.BtnGreenNormal, OnRestartClick);
            var restartRect = _resultRestartBtn.GetComponent<RectTransform>();
            SetAnchors(restartRect, new Vector2(0.08f, 0.05f), new Vector2(0.47f, 0.20f));
            restartRect.anchoredPosition = Vector2.zero;
            restartRect.sizeDelta = Vector2.zero;
            UIStyleKit.StyleGreenButton(_resultRestartBtn);
            UIStyleKit.AddTextShadow(_resultRestartBtn.GetComponentInChildren<Text>());

            // 退出按钮 — 红色圆角
            _resultExitBtn = CreateButton("ExitBtn", centerPanel, "🚪 退出", 24,

                UIStyleKit.BtnRedNormal, OnExitClick);
            var exitRect = _resultExitBtn.GetComponent<RectTransform>();
            SetAnchors(exitRect, new Vector2(0.53f, 0.05f), new Vector2(0.92f, 0.20f));
            exitRect.anchoredPosition = Vector2.zero;
            exitRect.sizeDelta = Vector2.zero;
            UIStyleKit.StyleRedButton(_resultExitBtn);
            UIStyleKit.AddTextShadow(_resultExitBtn.GetComponentInChildren<Text>());
        }


        // ---------- 选中塔信息面板 ----------

        private void BuildTowerInfoPanel()
        {
            _towerInfoPanel = new GameObject("TowerInfoPanel");
            _towerInfoPanel.transform.SetParent(transform, false);
            var panelRect = _towerInfoPanel.AddComponent<RectTransform>();
            SetAnchors(panelRect, new Vector2(0.04f, 0.15f), new Vector2(0.96f, 0.28f));
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = Vector2.zero;
            // 圆角面板+边框
            UIStyleKit.CreateStyledPanel(panelRect, UIStyleKit.PanelBgDark, UIStyleKit.BorderSilver, 12, 1);

            // 塔名称 — 白色加粗
            _towerInfoName = CreateText("TowerInfoName", panelRect, "箭塔", 26, TextAnchor.MiddleLeft, UIStyleKit.TextWhite);

            SetAnchors(_towerInfoName.rectTransform, new Vector2(0.03f, 0.55f), new Vector2(0.35f, 0.95f));
            _towerInfoName.rectTransform.anchoredPosition = Vector2.zero;
            _towerInfoName.rectTransform.sizeDelta = Vector2.zero;
            _towerInfoName.fontStyle = FontStyle.Bold;
            UIStyleKit.AddTextShadow(_towerInfoName);

            // 等级 — 绿色
            _towerInfoLevel = CreateText("TowerInfoLevel", panelRect, "Lv.1", 20, TextAnchor.MiddleLeft, UIStyleKit.TextGreen);

            SetAnchors(_towerInfoLevel.rectTransform, new Vector2(0.03f, 0.05f), new Vector2(0.35f, 0.55f));
            _towerInfoLevel.rectTransform.anchoredPosition = Vector2.zero;
            _towerInfoLevel.rectTransform.sizeDelta = Vector2.zero;

            // 升级按钮 — 蓝色圆角
            _upgradeBtn = CreateButton("UpgradeBtn", panelRect, "⬆ 升级 $60", 20,

                UIStyleKit.BtnBlueNormal, OnUpgradeClick);
            var upgradeRect = _upgradeBtn.GetComponent<RectTransform>();
            SetAnchors(upgradeRect, new Vector2(0.38f, 0.15f), new Vector2(0.65f, 0.85f));
            upgradeRect.anchoredPosition = Vector2.zero;
            upgradeRect.sizeDelta = Vector2.zero;
            _upgradeBtnText = _upgradeBtn.GetComponentInChildren<Text>();
            UIStyleKit.StyleBlueButton(_upgradeBtn);

            // 出售按钮 — 红色圆角
_sellBtn = CreateButton("SellBtn", panelRect, "$ 出售 $40", 20,


                UIStyleKit.BtnRedNormal, OnSellClick);
            var sellRect = _sellBtn.GetComponent<RectTransform>();
            SetAnchors(sellRect, new Vector2(0.68f, 0.15f), new Vector2(0.97f, 0.85f));
            sellRect.anchoredPosition = Vector2.zero;
            sellRect.sizeDelta = Vector2.zero;
            _sellBtnText = _sellBtn.GetComponentInChildren<Text>();
            UIStyleKit.StyleRedButton(_sellBtn);
        }


        // ---------- 中央通知文本 ----------

        private void BuildCenterNotice()
        {
            _centerNotice = CreateText("CenterNotice", transform as RectTransform, "", 36,
                TextAnchor.MiddleCenter, UIStyleKit.TextWhite);

            SetAnchors(_centerNotice.rectTransform, new Vector2(0.12f, 0.44f), new Vector2(0.88f, 0.56f));
            _centerNotice.rectTransform.anchoredPosition = Vector2.zero;
            _centerNotice.rectTransform.sizeDelta = Vector2.zero;
            _centerNotice.gameObject.SetActive(false);
            _centerNotice.fontStyle = FontStyle.Bold;
            UIStyleKit.AddTextOutline(_centerNotice);

            // 给通知文本加一个精美的圆角背景
            var bgObj = new GameObject("NoticeBG");
            bgObj.transform.SetParent(_centerNotice.transform, false);
            bgObj.transform.SetAsFirstSibling();
            var bgRect = bgObj.AddComponent<RectTransform>();
            SetAnchors(bgRect, Vector2.zero, Vector2.one);
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = new Vector2(30, 16);
            UIStyleKit.CreateStyledPanel(bgRect, new Color(0.05f, 0.05f, 0.12f, 0.80f),
                UIStyleKit.BorderGold * 0.4f, 10, 1);
        }


        // ================================================================
        // UI更新
        // ================================================================

        /// <summary>每帧更新状态栏</summary>
        private void UpdateStatusBar()
        {
            // 金币
            if (BattleEconomyManager.HasInstance)
            {
_goldText.text = $"$ {BattleEconomyManager.Instance.CurrentGold}";

            }

            // 波次
            if (WaveManager.HasInstance)
            {
                int cur = WaveManager.Instance.CurrentWaveNumber;
                int total = WaveManager.Instance.TotalWaves;
                _waveText.text = $"波次 {cur}/{total}";
            }

            // 基地HP
            if (BaseHealth.HasInstance)
            {
                int cur = BaseHealth.Instance.CurrentHP;
                int max = BaseHealth.Instance.MaxHP;
_hpText.text = $"HP {cur}/{max}";

                _hpText.color = cur <= max * 0.3f ? UIStyleKit.TextRed : UIStyleKit.TextHP;
            }

            // 时间
            if (BattleManager.HasInstance)
            {
                float t = BattleManager.Instance.BattleDuration;
                int mins = (int)(t / 60);
                int secs = (int)(t % 60);
                _timerText.text = $"{mins}:{secs:D2}";
            }
        }


        /// <summary>更新放塔按钮状态（金币不足时变灰）</summary>
        private void UpdateTowerButtonStates()
        {
            if (!TowerManager.HasInstance || !BattleEconomyManager.HasInstance) return;

            int gold = BattleEconomyManager.Instance.CurrentGold;

            for (int i = 0; i < TowerTypes.Length; i++)
            {
                var config = TowerManager.Instance.GetTowerConfig(TowerTypes[i]);
                if (config != null)
                {
                    bool canAfford = gold >= config.buildCost;
                    var colors = _towerButtons[i].colors;
                    colors.normalColor = canAfford ? Color.white : new Color(0.5f, 0.4f, 0.4f);
                    _towerButtons[i].colors = colors;
                    _towerButtons[i].interactable = canAfford;

                }
            }
        }

        /// <summary>更新塔费用显示</summary>
        private void UpdateTowerCosts()
        {
            if (!TowerManager.HasInstance) return;

            for (int i = 0; i < TowerTypes.Length; i++)
            {
                var config = TowerManager.Instance.GetTowerConfig(TowerTypes[i]);
                if (config != null)
                {
                    _towerCostTexts[i].text = $"${config.buildCost}";
                }
            }
        }

        /// <summary>更新开始波次按钮</summary>
        private void UpdateStartWaveButton()
        {
            if (!BattleManager.HasInstance || !WaveManager.HasInstance) return;

            var state = BattleManager.Instance.CurrentState;
            bool canStart = state == BattleState.Preparing || state == BattleState.RuneSelection
                || (state == BattleState.Fighting && !WaveManager.Instance.IsWaveActive);

            _startWaveBtn.interactable = canStart;

            if (WaveManager.Instance.IsWaveActive)
            {
                _startWaveText.text = "战斗中...";
            }
            else if (WaveManager.Instance.WaveCountdown > 0)
            {
                _startWaveText.text = $"跳过 {WaveManager.Instance.WaveCountdown:F0}s";
            }
            else if (state == BattleState.Preparing)
            {
                _startWaveText.text = "▶ 开始";
            }
            else
            {
                _startWaveText.text = "▶ 下一波";
            }
        }

        /// <summary>更新通知显示</summary>
        private void UpdateNotice()
        {
            if (_noticeTimer > 0)
            {
                _noticeTimer -= Time.unscaledDeltaTime;
                if (_noticeTimer <= 0 && _noticeTimer > -0.5f)
                {
                    // 触发淡出动效（只触发一次）
                    _noticeTimer = -1f;
                    UIAnimator.NoticeHide(_centerNotice.rectTransform, 0.3f);
                }
            }
        }

        /// <summary>显示中央通知（带弹出动效）</summary>
        private void ShowNotice(string text, float duration = 2f)
        {
            // 取消之前的动效
            UIAnimator.Kill(_centerNotice.rectTransform);
            _centerNotice.text = text;
            _centerNotice.gameObject.SetActive(true);
            _centerNotice.rectTransform.localScale = Vector3.one;
            // 确保CanvasGroup alpha为1
            var cg = _centerNotice.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
            // 弹出动效
            UIAnimator.NoticeShow(_centerNotice.rectTransform);
            _noticeTimer = duration;
        }


        // ================================================================
        // 事件处理
        // ================================================================

        private void OnBattleStateChanged(BattleStateChangedEvent evt)
        {
            switch (evt.NewState)
            {
                case BattleState.Preparing:
                    ShowNotice("🏰 准备阶段 — 放置塔防御！", 3f);
                    _towerBar.gameObject.SetActive(true);
                    break;

                case BattleState.Fighting:
                    if (_runePanel.activeSelf)
                    {
                        var rRect = _runePanel.GetComponent<RectTransform>();
                        UIAnimator.PanelHide(rRect, 0.2f);
                    }
                    break;


                case BattleState.RuneSelection:
                    // 词条面板在RuneSelectionEvent中处理
                    break;

                case BattleState.Victory:
                case BattleState.Defeat:
                    // 结算面板在BattleResultEvent中处理
                    if (_towerInfoPanel.activeSelf)
                    {
                        var tiRect = _towerInfoPanel.GetComponent<RectTransform>();
                        UIAnimator.PanelHide(tiRect, 0.18f);
                    }
                    break;


                case BattleState.Paused:
                    ShowNotice("⏸ 已暂停", 999f);
                    break;
            }

            // 从暂停恢复时隐藏通知
            if (evt.OldState == BattleState.Paused && evt.NewState != BattleState.Paused)
            {
                _centerNotice.gameObject.SetActive(false);
                _noticeTimer = 0;
            }
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
_goldText.text = $"$ {evt.CurrentGold}";

            UpdateTowerCosts();
            // 金币变化强调动效
            UIAnimator.ValueChangeEmphasis(_goldText, UIStyleKit.TextGold);
        }

        private void OnBaseHealthChanged(BaseHealthChangedEvent evt)
        {
_hpText.text = $"HP {evt.CurrentHP}/{evt.MaxHP}";

            // HP减少时红色闪烁+抖动
            if (evt.CurrentHP < evt.MaxHP)
            {
                UIAnimator.PunchScale(_hpText.rectTransform, 0.12f, 0.25f);
                UIAnimator.ColorFlash(_hpText, UIStyleKit.TextRed, 0.3f);
            }
        }


        private void OnWaveStart(WaveStartEvent evt)
        {
string prefix = evt.IsBoss ? "!! BOSS " : evt.IsElite ? "! 精英" : "";

            ShowNotice($"{prefix}第 {evt.WaveIndex} 波来袭！", 2f);

            // 波次开始时波次文字强调动效
            UIAnimator.PunchScale(_waveText.rectTransform, 0.15f, 0.3f);
            UIAnimator.ColorFlash(_waveText, UIStyleKit.TextGold, 0.4f);

            // Boss波次额外强调：全屏轻微抖动
            if (evt.IsBoss)
            {
                UIAnimator.ShakeX(transform as RectTransform, 5f, 0.5f);
            }

            // 退出放塔模式
            if (TowerManager.HasInstance && TowerManager.Instance.IsPlacementMode)
            {
                TowerManager.Instance.ExitPlacementMode();
            }
        }

        private void OnWaveComplete(WaveCompleteEvent evt)
        {
ShowNotice($"√ 第 {evt.WaveIndex} 波清除！", 2f);

            // 波次完成时开始波次按钮脉冲提示
            if (_startWaveBtn != null)
            {
                UIAnimator.PunchScale(_startWaveBtn.GetComponent<RectTransform>(), 0.12f, 0.3f);
            }
        }


        private void OnRuneSelection(RuneSelectionEvent evt)
        {
            _currentRuneOptions = evt.Options;
            _runePanel.SetActive(true);

            // 词条面板弹出动效 — 整体淡入
            var runePanelRect = _runePanel.GetComponent<RectTransform>();
            UIAnimator.FadeIn(runePanelRect, 0.25f).SetUnscaled(true);

            // 收集有效的词条按钮用于依次入场动效
            var validBtnRects = new System.Collections.Generic.List<RectTransform>();

            for (int i = 0; i < 3; i++)
            {
                if (i < evt.Options.Length && evt.Options[i] != null)
                {
                    _runeButtons[i].gameObject.SetActive(true);
                    var rune = evt.Options[i];

                    // 按稀有度设置颜色
                    Color rarityColor = GetRarityColor(rune.rarity);
                    _runeNameTexts[i].text = $"<color=#{ColorUtility.ToHtmlStringRGB(rarityColor)}>{rune.displayName}</color> [{GetRarityName(rune.rarity)}]";
                    _runeDescTexts[i].text = rune.description;

                    validBtnRects.Add(_runeButtons[i].GetComponent<RectTransform>());
                }
                else
                {
                    _runeButtons[i].gameObject.SetActive(false);
                }
            }

            // 词条卡片依次滑入（交错动效）
            if (validBtnRects.Count > 0)
            {
                UIAnimator.StaggeredSlideIn(validBtnRects.ToArray(), 0.08f, 0.35f, 50f);
            }
        }


        private void OnBattleResult(BattleResultEvent evt)
        {
            _resultPanel.SetActive(true);

            if (evt.IsVictory)
            {
_resultTitleText.text = "* 胜利！";

                _resultTitleText.color = UIStyleKit.TextGold;
            }
            else
            {
_resultTitleText.text = "X 失败...";

                _resultTitleText.color = UIStyleKit.TextRed;
            }

            _resultDetailText.text =
                $"波次进度: {evt.WavesCleared}/{evt.TotalWaves}\n" +
                $"基地剩余: {evt.BaseHPRemaining} HP\n" +
                $"战斗时长: {(int)(evt.Duration / 60)}分{(int)(evt.Duration % 60)}秒\n" +
                $"获得金币: {evt.GoldEarned}\n" +
                $"选择词条: {evt.RunesSelected}个";

            // 结算面板弹出动效 — 缩放+淡入
            var resultRect = _resultPanel.GetComponent<RectTransform>();
            UIAnimator.PanelShow(resultRect, 0.4f);
            // 标题冲击缩放强调
            UIAnimator.PunchScale(_resultTitleText.rectTransform, 0.2f, 0.5f).SetDelay(0.2f);
        }


        private void OnTowerSelected(TowerSelectedEvent evt)
        {
            bool wasHidden = !_towerInfoPanel.activeSelf;
            _towerInfoPanel.SetActive(true);
            var tower = evt.Tower;
            if (tower == null) return;

            // 塔信息面板滑入动效（仅首次显示时）
            if (wasHidden)
            {
                var infoRect = _towerInfoPanel.GetComponent<RectTransform>();
                UIAnimator.SlideFromBottom(infoRect, 60f, 0.25f);
                UIAnimator.FadeIn(infoRect, 0.2f);
            }


            _towerInfoName.text = tower.Config?.displayName ?? "塔";
            _towerInfoLevel.text = $"Lv.{tower.CurrentLevel} | ATK:{tower.Damage:F0} | SPD:{tower.AttackInterval:F1}s";


            if (tower.IsMaxLevel)
            {
                _upgradeBtnText.text = "已满级";
                _upgradeBtn.interactable = false;
            }
            else
            {
                _upgradeBtnText.text = $"⬆ 升级 ${tower.UpgradeCost}";
                _upgradeBtn.interactable = BattleEconomyManager.HasInstance &&
                    BattleEconomyManager.Instance.CanAfford(tower.UpgradeCost);
            }

            int sellPrice = tower.SellPrice;
_sellBtnText.text = $"$ 出售 ${sellPrice}";

        }

        private void OnTowerDeselected(TowerDeselectedEvent evt)
        {
            // 塔信息面板滑出动效
            var infoRect = _towerInfoPanel.GetComponent<RectTransform>();
            UIAnimator.PanelHide(infoRect, 0.18f);
        }


        // ================================================================
        // 按钮回调
        // ================================================================

        private void OnTowerButtonClick(int index)
        {
            if (!TowerManager.HasInstance) return;

            // [G3-5] 隐藏快捷建造面板（如果正在显示）
            HideQuickBuildPanel();

            var type = TowerTypes[index];
            var config = TowerManager.Instance.GetTowerConfig(type);
            if (config == null) return;

            // 检查金币
            if (BattleEconomyManager.HasInstance && !BattleEconomyManager.Instance.CanAfford(config.buildCost))
            {
ShowNotice("$ 金币不足！", 1.5f);
                return;
            }

            // [G3-5] 如果已在放塔模式且是同类型，再次点击则退出（开关式）

            if (TowerManager.Instance.IsPlacementMode)
            {
                if (TowerManager.Instance.CurrentPlacementType == type)
                {
                    TowerManager.Instance.ExitPlacementMode();
                    return;
                }
                TowerManager.Instance.ExitPlacementMode();
            }

            // 进入放塔模式（连续模式）
            TowerManager.Instance.EnterPlacementMode(type);
            ShowNotice($"点击绿色格子放置{TowerNames[index]}（再次点击按钮退出）", 2f);
        }



        private void OnPauseClick()
        {
            if (!BattleManager.HasInstance) return;

            if (BattleManager.Instance.CurrentState == BattleState.Paused)
            {
                BattleManager.Instance.ResumeBattle();
                _pauseText.text = "⏸";
            }
            else
            {
                BattleManager.Instance.PauseBattle();
                _pauseText.text = "▶";
            }
        }

        private void OnSpeedClick()
        {
            if (!BattleInputHandler.HasInstance) return;

            BattleInputHandler.Instance.ToggleSpeed();
            float speed = BattleInputHandler.Instance.GameSpeed;
            _speedText.text = $"{speed:F0}x";
        }

        private void OnStartWaveClick()
        {
            if (!BattleManager.HasInstance) return;

            var state = BattleManager.Instance.CurrentState;

            if (state == BattleState.Preparing || state == BattleState.RuneSelection)
            {
                BattleManager.Instance.PlayerStartWave();
            }
            else if (state == BattleState.Fighting)
            {
                if (WaveManager.HasInstance && WaveManager.Instance.WaveCountdown > 0)
                {
                    // 跳过倒计时
                    WaveManager.Instance.SkipCountdown();
                }
                else if (WaveManager.HasInstance && !WaveManager.Instance.IsWaveActive && !WaveManager.Instance.AllWavesCleared)
                {
                    // 倒计时已结束但下一波还没自动开始（边界情况），手动触发
                    WaveManager.Instance.StartNextWave();
                }
            }
        }


        private void OnRuneOptionClick(int index)
        {
            if (_currentRuneOptions == null || index >= _currentRuneOptions.Length) return;
            var rune = _currentRuneOptions[index];
            if (rune == null) return;

            BattleManager.Instance.OnRuneSelected(rune.runeId);
            // 词条面板关闭动效
            var runePanelRect = _runePanel.GetComponent<RectTransform>();
            UIAnimator.PanelHide(runePanelRect, 0.2f);
ShowNotice($"★ 获得: {rune.displayName}", 2f);


        }

        private void OnUpgradeClick()
        {
            if (!TowerManager.HasInstance) return;

            var tower = TowerManager.Instance.SelectedTower;
            if (tower == null || tower.IsMaxLevel) return;

            // 扣金币
            int cost = tower.UpgradeCost;
            if (BattleEconomyManager.HasInstance && BattleEconomyManager.Instance.SpendGold(cost, "升级塔"))
            {
                TowerManager.Instance.UpgradeSelectedTower();
                ShowNotice($"⬆ {tower.Config?.displayName} 升级到 Lv.{tower.CurrentLevel}", 1.5f);


                // 刷新面板
                OnTowerSelected(new TowerSelectedEvent { TowerId = tower.InstanceId, Tower = tower });
            }
            else
            {
ShowNotice("$ 金币不足！", 1.5f);
            }
        }

        private void OnSellClick()

        {
            if (!TowerManager.HasInstance) return;

            var tower = TowerManager.Instance.SelectedTower;
            if (tower == null) return;

            int refund = tower.SellPrice;
            TowerManager.Instance.SellSelectedTower();

            // 返还金币
            if (BattleEconomyManager.HasInstance)
            {
                BattleEconomyManager.Instance.AddGold(refund, "出售塔");
            }

ShowNotice($"$ 出售获得 ${refund}", 1.5f);

        }

        private void OnRestartClick()
        {
            // 按钮点击反馈
            UIAnimator.ButtonPress(_resultRestartBtn.GetComponent<RectTransform>());
            // 结算面板关闭动效
            var resultRect = _resultPanel.GetComponent<RectTransform>();
            UIAnimator.PanelHide(resultRect, 0.2f, () =>
            {
                if (BattleManager.HasInstance)
                    BattleManager.Instance.RestartBattle();
            });
        }

        private void OnExitClick()
        {
            // 按钮点击反馈
            UIAnimator.ButtonPress(_resultExitBtn.GetComponent<RectTransform>());
            // 结算面板关闭动效
            var resultRect = _resultPanel.GetComponent<RectTransform>();
            UIAnimator.PanelHide(resultRect, 0.2f, () =>
            {
                // 通过GameManager执行完整的退出战斗流程（清理战斗对象 + 返回大厅）
                if (GameManager.HasInstance)
                {
                    GameManager.Instance.ExitBattle();
                }
            });
        }



        // ================================================================
        // [G3-5] 输入回调注册
        // ================================================================

        /// <summary>[G3-5] 注册空地点击回调</summary>
        private void RegisterInputCallbacks()
        {
            if (BattleInputHandler.HasInstance)
            {
                BattleInputHandler.Instance.RegisterEmptyTileCallback(OnEmptyTileClicked);
            }
        }

        /// <summary>[G3-5] 取消注册</summary>
        private void UnregisterInputCallbacks()
        {
            if (BattleInputHandler.HasInstance)
            {
                BattleInputHandler.Instance.UnregisterEmptyTileCallback();
            }
        }

        // ================================================================
        // [G3-5] 拖拽放塔 — 从底部塔按钮拖拽到地图
        // ================================================================

        /// <summary>[G3-5] 为塔按钮添加拖拽检测（使用EventTrigger）</summary>
        private void AddDragDetection(GameObject btnObj, int towerIndex)
        {
            var trigger = btnObj.AddComponent<EventTrigger>();

            // 按下开始
            var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener((data) =>
            {
                _dragTowerIndex = towerIndex;
                _isDragPending = true;
                _dragStartPos = ((PointerEventData)data).position;
            });
            trigger.triggers.Add(pointerDown);

            // 拖拽中
            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener((data) =>
            {
                if (!_isDragPending && _dragTowerIndex < 0) return;
                var pointerData = (PointerEventData)data;

                // 超过阈值才开始拖拽（避免误触）
                if (_isDragPending)
                {
                    float dist = Vector2.Distance(pointerData.position, _dragStartPos);
                    if (dist > DragStartThreshold)
                    {
                        _isDragPending = false;
                        StartDragFromButton(_dragTowerIndex);
                    }
                }

                // 已经在拖拽中，更新位置
                if (BattleInputHandler.HasInstance && BattleInputHandler.Instance.IsDragFromButton)
                {
                    // 模拟触摸移动让InputHandler更新预览
                    // InputHandler在Update中已经处理了，这里只需确保它知道
                }
            });
            trigger.triggers.Add(drag);

            // 抬起
            var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            pointerUp.callback.AddListener((data) =>
            {
                _isDragPending = false;
                _dragTowerIndex = -1;
            });
            trigger.triggers.Add(pointerUp);
        }

        /// <summary>[G3-5] 开始从按钮拖拽放塔</summary>
        private void StartDragFromButton(int towerIndex)
        {
            if (!TowerManager.HasInstance || !BattleEconomyManager.HasInstance) return;

            var type = TowerTypes[towerIndex];
            var config = TowerManager.Instance.GetTowerConfig(type);
            if (config == null) return;

            if (!BattleEconomyManager.Instance.CanAfford(config.buildCost))
            {
ShowNotice("$ 金币不足！", 1.5f);
                return;
            }

            // 隐藏快捷建造面板

            HideQuickBuildPanel();

            // 委托给InputHandler处理
            BattleInputHandler.Instance.BeginDragFromButton(type);
            ShowNotice($"拖到绿色格子放置{TowerNames[towerIndex]}", 2f);
        }

        /// <summary>[G3-5] Update中检测从按钮拖拽</summary>
        private void UpdateDragFromButton()
        {
            // 从按钮拖拽时，InputHandler负责处理，这里不需额外逻辑
        }

        // ================================================================
        // [G3-5] 快捷建造面板 — 单击空地弹出塔选择
        // ================================================================

        /// <summary>[G3-5] 空地点击回调</summary>
        private void OnEmptyTileClicked(Vector2Int gridPos, Vector2 screenPos)
        {
            // 如果正在放塔模式，不弹出快捷建造（直接放塔优先）
            if (TowerManager.HasInstance && TowerManager.Instance.IsPlacementMode) return;

            _quickBuildGridPos = gridPos;
            ShowQuickBuildPanel(screenPos);
        }

        /// <summary>[G3-5] 显示快捷建造面板</summary>
        private void ShowQuickBuildPanel(Vector2 screenPos)
        {
            if (_quickBuildPanel == null) return;

            _quickBuildVisible = true;
            _quickBuildPanel.SetActive(true);

            // 将面板定位到点击位置附近（稍微偏上，避免遮挡手指）
            var panelRect = _quickBuildPanel.GetComponent<RectTransform>();
            // 弹出动效
            UIAnimator.PopIn(panelRect, 0.25f);
            UIAnimator.FadeIn(panelRect, 0.15f);

            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform as RectTransform, screenPos, null, out localPos);
            // 面板偏上显示
            localPos.y += 30f;
            // 确保不超出屏幕（简单限制）
            var canvasRect = transform as RectTransform;
            float halfW = panelRect.sizeDelta.x * 0.5f;
            float panelH = panelRect.sizeDelta.y;
            localPos.x = Mathf.Clamp(localPos.x, -canvasRect.rect.width * 0.5f + halfW + 10f,
                                                    canvasRect.rect.width * 0.5f - halfW - 10f);
            localPos.y = Mathf.Clamp(localPos.y, -canvasRect.rect.height * 0.5f + panelH + 10f,
                                                    canvasRect.rect.height * 0.5f - 10f);
            panelRect.anchoredPosition = localPos;

            // 更新各塔按钮状态（金币、费用）
            UpdateQuickBuildButtons();

            Logger.D("BattleUI", "[G3-5] 快捷建造面板已显示 @({0},{1})", _quickBuildGridPos.x, _quickBuildGridPos.y);
        }

        /// <summary>[G3-5] 隐藏快捷建造面板（带收缩动效）</summary>
        private void HideQuickBuildPanel()
        {
            if (_quickBuildPanel != null && _quickBuildVisible)
            {
                _quickBuildVisible = false;
                var panelRect = _quickBuildPanel.GetComponent<RectTransform>();
                UIAnimator.PanelHide(panelRect, 0.15f);
            }
        }


        /// <summary>[G3-5] 更新快捷建造面板按钮状态</summary>
        private void UpdateQuickBuildButtons()
        {
            if (!TowerManager.HasInstance || !BattleEconomyManager.HasInstance) return;

            int gold = BattleEconomyManager.Instance.CurrentGold;

            for (int i = 0; i < TowerTypes.Length; i++)
            {
                var config = TowerManager.Instance.GetTowerConfig(TowerTypes[i]);
                if (config == null) continue;

                bool canAfford = gold >= config.buildCost;
                _quickBuildButtons[i].interactable = canAfford;
                _quickBuildCostTexts[i].text = $"${config.buildCost}";
                _quickBuildCostTexts[i].color = canAfford ? UIStyleKit.TextGold : UIStyleKit.TextRed;

            }
        }

        /// <summary>[G3-5] 快捷建造按钮点击</summary>
        private void OnQuickBuildClick(int index)
        {
            if (!TowerManager.HasInstance) return;

            var type = TowerTypes[index];

            // 直接在记录的格子位置放塔
            if (TowerManager.Instance.TryPlaceTower(type, _quickBuildGridPos))
            {
ShowNotice($"√ {TowerNames[index]}建造成功！", 1.5f);

                Logger.D("BattleUI", "[G3-5] 快捷建造成功: {0} @({1},{2})", TowerNames[index], _quickBuildGridPos.x, _quickBuildGridPos.y);
            }
            else
            {
ShowNotice($"X 无法在此建造{TowerNames[index]}", 1.5f);

            }

            HideQuickBuildPanel();
        }

        // ================================================================
        // [G3-5] 放塔模式底部栏高亮 + ESC退出
        // ================================================================

        /// <summary>[G3-5] 高亮当前放塔模式选中的塔按钮</summary>
        private void UpdatePlacementHighlight()
        {
            if (!TowerManager.HasInstance) return;

            bool inPlacement = TowerManager.Instance.IsPlacementMode;
            var currentType = TowerManager.Instance.CurrentPlacementType;

            for (int i = 0; i < TowerTypes.Length; i++)
            {
                if (_towerBtnImages[i] == null) continue;

                if (inPlacement && TowerTypes[i] == currentType)
                {
                    // 选中态：亮绿色高亮
                    _towerBtnImages[i].color = new Color(0.7f, 1f, 0.7f, 1f);
                }
                else
                {
                    // 恢复默认色
                    bool canAfford = BattleEconomyManager.HasInstance &&
                        BattleEconomyManager.Instance.CurrentGold >= (TowerManager.Instance.GetTowerConfig(TowerTypes[i])?.buildCost ?? 9999);
                    _towerBtnImages[i].color = canAfford ? Color.white : new Color(0.5f, 0.4f, 0.4f);
                }

            }
        }

        /// <summary>[G3-5] ESC/右键退出放塔模式</summary>
        private void HandleExitPlacementMode()
        {
            if (!TowerManager.HasInstance || !TowerManager.Instance.IsPlacementMode) return;

            // ESC退出
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TowerManager.Instance.ExitPlacementMode();
                HideQuickBuildPanel();
                ShowNotice("已退出放塔模式", 1f);
            }

            // 右键退出
            if (Input.GetMouseButtonDown(1))
            {
                TowerManager.Instance.ExitPlacementMode();
                HideQuickBuildPanel();
                ShowNotice("已退出放塔模式", 1f);
            }
        }

        // ================================================================
        // UI工具方法
        // ================================================================


        private RectTransform CreatePanel(string name, RectTransform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj.AddComponent<RectTransform>();
        }

        private Image AddImage(RectTransform rect, Color color)
        {
            var img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return img;
        }

        // 缓存字体实例，避免每次CreateText都重新创建
        private static Font _cachedFont;
        private static bool _wxFontRequested;

        /// <summary>
        /// 在游戏启动时调用，预加载微信系统字体（异步）。
        /// 加载完成后会自动刷新所有已创建的Text组件的字体。
        /// </summary>
        internal static void PreloadWXFont()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_wxFontRequested) return;
            _wxFontRequested = true;
            try
            {
                Logger.I("BattleUI", "开始预加载微信系统字体...");
                WeChatWASM.WX.GetWXFont("", (font) =>
                {
                    if (font != null)
                    {
                        Logger.I("BattleUI", "微信系统字体加载成功: {0}", font.name);
                        _cachedFont = font;
                        // 刷新所有已创建的Text组件的字体
                        RefreshAllTextFonts(font);
                    }
                    else
                    {
                        Logger.W("BattleUI", "微信系统字体加载失败，使用回退字体");
                    }
                });
            }
            catch (System.Exception e)
            {
                Logger.E("BattleUI", "预加载微信字体异常: {0}", e.Message);
            }
#endif
        }

        /// <summary>刷新场景中所有Text组件的字体</summary>
        private static void RefreshAllTextFonts(Font font)
        {
            if (font == null) return;
            var allTexts = Object.FindObjectsOfType<Text>(true);
            foreach (var t in allTexts)
            {
                if (t != null)
                {
                    t.font = font;
                }
            }
            Logger.I("BattleUI", "已刷新 {0} 个Text组件的字体", allTexts.Length);
        }

        /// <summary>获取支持中文的字体（微信小游戏兼容）</summary>
        internal static Font GetFont()
        {
            if (_cachedFont != null) return _cachedFont;

#if UNITY_WEBGL && !UNITY_EDITOR
            // 微信小游戏环境：触发异步加载微信字体
            PreloadWXFont();
#endif

            // 优先从Resources加载内嵌中文字体（SimHei黑体）
            try
            {
                _cachedFont = Resources.Load<Font>("Fonts/SimHei");
            }
            catch (System.Exception)
            {
                _cachedFont = null;
            }
            if (_cachedFont != null)
            {
                Logger.I("BattleUI", "使用内嵌中文字体: SimHei");
                return _cachedFont;
            }

            // 回退：尝试Unity内置字体
            try
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (System.Exception)
            {
                _cachedFont = null;
            }
            if (_cachedFont != null) return _cachedFont;

            try
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (System.Exception)
            {
                _cachedFont = null;
            }
            if (_cachedFont != null) return _cachedFont;

            // 最终回退：尝试系统中文字体
            string[] chineseFonts = { "SimHei", "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "Arial" };
            foreach (var fontName in chineseFonts)
            {
                _cachedFont = Font.CreateDynamicFontFromOSFont(fontName, 24);
                if (_cachedFont != null)
                {
                    Logger.I("BattleUI", "使用系统字体: {0}", fontName);
                    return _cachedFont;
                }
            }

            _cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 24);
            return _cachedFont;
        }




        private Text CreateText(string name, RectTransform parent, string content, int fontSize,
            TextAnchor alignment, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();

            var text = obj.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.font = GetFont();
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }


        private Button CreateButton(string name, RectTransform parent, string text, int fontSize,
            Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreatePanel(name, parent);

            // 使用圆角背景
            var tex = UIStyleKit.GetRoundedRectTexture(64, 64, 8, Color.white, new Color(1, 1, 1, 0.25f), 1);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = bgColor;
            img.raycastTarget = true;

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            // 设置按钮交互色
            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.25f;
            colors.pressedColor = bgColor * 0.75f;
            colors.fadeDuration = 0.1f;
            btn.colors = colors;

            var label = CreateText($"{name}_Text", rect, text, fontSize, TextAnchor.MiddleCenter, Color.white);
            SetAnchors(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = Vector2.zero;
            UIStyleKit.AddTextShadow(label);

            return btn;
        }


        private void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
        }

        private Color GetRarityColor(RuneRarity rarity)
        {
            switch (rarity)
            {
                case RuneRarity.Common: return Color.white;
                case RuneRarity.Rare: return new Color(0.4f, 0.6f, 1f);
                case RuneRarity.Epic: return new Color(0.8f, 0.4f, 1f);
                case RuneRarity.Legendary: return new Color(1f, 0.7f, 0.2f);
                default: return Color.white;
            }
        }

        private string GetRarityName(RuneRarity rarity)
        {
            switch (rarity)
            {
                case RuneRarity.Common: return "普通";
                case RuneRarity.Rare: return "稀有";
                case RuneRarity.Epic: return "史诗";
                case RuneRarity.Legendary: return "传说";
                default: return "";
            }
        }
    }
}
