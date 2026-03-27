// ============================================================
// 文件名：TutorialSystem.cs
// 功能描述：新手引导系统原型
//          步骤驱动引导框架、高亮遮罩、手指提示、文字气泡
//          引导：放置第一个塔→开始波次→升级塔→选词条→通关
// 创建时间：2026-03-25
// 所属模块：Battle/Tutorial
// 对应交互：阶段三 #246（第一关新手引导原型）
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle;
using AetheraSurvivors.Battle.Tower;

using AetheraSurvivors.Battle.Wave;
using AetheraSurvivors.Battle.Rune;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Tutorial

{
    // ====================================================================
    // 引导步骤定义
    // ====================================================================

    /// <summary>引导步骤类型</summary>
    public enum TutorialStepType
    {
        /// <summary>显示文字提示（点击继续）</summary>
        ShowMessage,
        /// <summary>引导点击指定区域</summary>
        GuideClick,
        /// <summary>引导放置塔</summary>
        GuidePlaceTower,
        /// <summary>引导开始波次</summary>
        GuideStartWave,
        /// <summary>引导升级塔</summary>
        GuideUpgradeTower,
        /// <summary>引导选择词条</summary>
        GuideSelectRune,
        /// <summary>等待条件满足</summary>
        WaitForCondition,
        /// <summary>延迟等待</summary>
        Delay,
        /// <summary>自由操作（无引导，等待触发下一步）</summary>
        FreePlay,
    }

    /// <summary>
    /// 引导步骤配置
    /// </summary>
    [Serializable]
    public class TutorialStep
    {
        /// <summary>步骤ID（用于跳转和持久化）</summary>
        public string stepId;

        /// <summary>步骤类型</summary>
        public TutorialStepType type;

        /// <summary>提示文字</summary>
        public string message = "";

        /// <summary>高亮的目标区域（屏幕坐标/世界坐标）</summary>
        public Vector2 highlightTarget;

        /// <summary>是否使用世界坐标（否则为屏幕坐标）</summary>
        public bool useWorldPos = false;

        /// <summary>引导手指方向（上下左右）</summary>
        public Vector2 fingerDirection = Vector2.down;

        /// <summary>延迟时间（Delay类型用）</summary>
        public float delay = 0f;

        /// <summary>自动完成的条件（WaitForCondition类型用）</summary>
        public Func<bool> condition;

        /// <summary>引导放塔时的目标格子坐标</summary>
        public Vector2Int targetGridPos;

        /// <summary>引导放塔时的塔类型</summary>
        public TowerType targetTowerType;

        /// <summary>是否可跳过此步</summary>
        public bool skippable = false;

        /// <summary>步骤完成回调</summary>
        public Action onComplete;
    }

    // ====================================================================
    // 引导事件
    // ====================================================================

    /// <summary>引导开始事件</summary>
    public struct TutorialStartEvent : IEvent { }

    /// <summary>引导步骤变化事件</summary>
    public struct TutorialStepEvent : IEvent
    {
        public string StepId;
        public TutorialStepType StepType;
        public string Message;
    }

    /// <summary>引导完成事件</summary>
    public struct TutorialCompleteEvent : IEvent { }

    /// <summary>引导跳过事件</summary>
    public struct TutorialSkipEvent : IEvent { }

    // ====================================================================
    // TutorialSystem 核心类
    // ====================================================================

    /// <summary>
    /// 新手引导系统原型
    /// 
    /// 设计原则：
    /// 1. 步骤驱动：每步有明确的触发条件和完成条件
    /// 2. 非侵入式：通过遮罩+高亮引导，不修改核心战斗逻辑
    /// 3. 可跳过：玩家可以随时跳过引导
    /// 4. 可持久化：记录已完成的引导步骤，避免重复引导
    /// 5. 原型版本：后续阶段四会完善UI和动画
    /// </summary>
    public class TutorialSystem : MonoSingleton<TutorialSystem>
    {
        // ========== UI引用（运行时动态创建） ==========

        /// <summary>遮罩Canvas</summary>
        private Canvas _tutorialCanvas;

        /// <summary>全屏半透明遮罩</summary>
        private Image _maskImage;

        /// <summary>高亮孔洞</summary>
        private RectTransform _highlightHole;

        /// <summary>提示文字框</summary>
        private Text _messageText;

        /// <summary>提示文字背景</summary>
        private Image _messageBg;

        /// <summary>手指引导图标</summary>
        private RectTransform _fingerIcon;

        /// <summary>"跳过引导"按钮</summary>
        private Button _skipButton;

        /// <summary>"继续"按钮（ShowMessage类型步骤用）</summary>
        private Button _continueButton;

        // ========== 运行时数据 ==========

        /// <summary>所有步骤列表</summary>
        private readonly List<TutorialStep> _steps = new List<TutorialStep>();

        /// <summary>当前步骤索引</summary>
        private int _currentStepIndex = -1;

        /// <summary>是否正在引导</summary>
        private bool _isActive = false;

        /// <summary>当前步骤是否已完成</summary>
        private bool _currentStepComplete = false;

        /// <summary>手指动画计时器</summary>
        private float _fingerAnimTimer = 0f;

        /// <summary>主摄像机引用</summary>
        private Camera _mainCamera;

        // ========== 公共属性 ==========

        /// <summary>是否正在引导</summary>
        public bool IsActive => _isActive;

        /// <summary>当前步骤索引</summary>
        public int CurrentStepIndex => _currentStepIndex;

        /// <summary>总步骤数</summary>
        public int TotalSteps => _steps.Count;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            _mainCamera = Camera.main;

            Logger.I("TutorialSystem", "新手引导系统初始化");
        }

        protected override void OnDispose()
        {
            StopAllCoroutines();
            CleanupUI();
        }

        private void Update()
        {
            if (!_isActive) return;

            // 手指动画
            AnimateFinger(Time.unscaledDeltaTime);

            // 检查当前步骤的自动完成条件
            CheckStepCompletion();
        }

        // ====================================================================
        // 核心方法：引导流程控制
        // ====================================================================

        /// <summary>
        /// 开始第一关新手引导
        /// </summary>
        public void StartTutorial()
        {
            // 检查是否已完成引导
            if (IsTutorialCompleted())
            {
                Logger.I("TutorialSystem", "引导已完成，跳过");
                return;
            }

            _steps.Clear();
            BuildFirstLevelSteps();

            if (_steps.Count == 0)
            {
                Logger.W("TutorialSystem", "无引导步骤");
                return;
            }

            _isActive = true;
            _currentStepIndex = -1;
            _currentStepComplete = false;

            CreateTutorialUI();

            EventBus.Instance.Publish(new TutorialStartEvent());

            Logger.I("TutorialSystem", "开始新手引导，共{0}步", _steps.Count);

            // 开始第一步
            NextStep();
        }

        /// <summary>
        /// 跳过引导
        /// </summary>
        public void SkipTutorial()
        {
            Logger.I("TutorialSystem", "玩家跳过引导");

            _isActive = false;
            HideAllUI();

            // 恢复正常游戏状态
            if (BattleInputHandler.HasInstance)
            {
                BattleInputHandler.Instance.InputEnabled = true;
            }

            EventBus.Instance.Publish(new TutorialSkipEvent());
            MarkTutorialCompleted();
        }

        /// <summary>
        /// 进入下一步
        /// </summary>
        public void NextStep()
        {
            // 完成当前步骤的回调
            if (_currentStepIndex >= 0 && _currentStepIndex < _steps.Count)
            {
                _steps[_currentStepIndex].onComplete?.Invoke();
            }

            _currentStepIndex++;
            _currentStepComplete = false;

            if (_currentStepIndex >= _steps.Count)
            {
                // 引导完成
                CompleteTutorial();
                return;
            }

            ExecuteStep(_steps[_currentStepIndex]);
        }

        /// <summary>
        /// 强制完成当前步骤
        /// </summary>
        public void ForceCompleteCurrentStep()
        {
            _currentStepComplete = true;
        }

        // ====================================================================
        // 步骤执行
        // ====================================================================

        /// <summary>执行一个引导步骤</summary>
        private void ExecuteStep(TutorialStep step)
        {
            Logger.D("TutorialSystem", "执行步骤 [{0}] {1}: {2}",
                _currentStepIndex, step.type, step.stepId);

            // 发布步骤事件
            EventBus.Instance.Publish(new TutorialStepEvent
            {
                StepId = step.stepId,
                StepType = step.type,
                Message = step.message
            });

            switch (step.type)
            {
                case TutorialStepType.ShowMessage:
                    ShowMessageStep(step);
                    break;

                case TutorialStepType.GuidePlaceTower:
                    GuidePlaceTowerStep(step);
                    break;

                case TutorialStepType.GuideStartWave:
                    GuideStartWaveStep(step);
                    break;

                case TutorialStepType.GuideUpgradeTower:
                    GuideUpgradeTowerStep(step);
                    break;

                case TutorialStepType.GuideSelectRune:
                    GuideSelectRuneStep(step);
                    break;

                case TutorialStepType.WaitForCondition:
                    WaitForConditionStep(step);
                    break;

                case TutorialStepType.Delay:
                    StartCoroutine(DelayStep(step));
                    break;

                case TutorialStepType.FreePlay:
                    FreePlayStep(step);
                    break;

                case TutorialStepType.GuideClick:
                    GuideClickStep(step);
                    break;
            }
        }

        // ========== 各类型步骤的具体实现 ==========

        /// <summary>显示文字提示步骤</summary>
        private void ShowMessageStep(TutorialStep step)
        {
            ShowMessage(step.message);
            ShowContinueButton(true);
            ShowMask(true);
            ShowFinger(false);
        }

        /// <summary>引导放塔步骤</summary>
        private void GuidePlaceTowerStep(TutorialStep step)
        {
            ShowMessage(step.message);
            ShowContinueButton(false);
            ShowMask(true);

            // 高亮目标格子
            if (Map.GridSystem.HasInstance)
            {
                Vector3 worldPos = Map.GridSystem.Instance.GridToWorld(step.targetGridPos);
                ShowHighlight(worldPos, new Vector2(1.2f, 1.2f));
                ShowFinger(true, worldPos);
            }

            // 自动进入放塔模式
            if (TowerManager.HasInstance)
            {
                TowerManager.Instance.EnterPlacementMode(step.targetTowerType);
            }

            // 监听放塔成功事件
            EventBus.Instance.Subscribe<TowerAttackEvent>(OnTutorialTowerPlaced);
        }

        /// <summary>引导开始波次步骤</summary>
        private void GuideStartWaveStep(TutorialStep step)
        {
            ShowMessage(step.message);
            ShowContinueButton(false);
            ShowMask(true);
            ShowFinger(false);

            // 等待波次开始
            EventBus.Instance.Subscribe<WaveStartEvent>(OnTutorialWaveStarted);
        }

        /// <summary>引导升级塔步骤</summary>
        private void GuideUpgradeTowerStep(TutorialStep step)
        {
            ShowMessage(step.message);
            ShowContinueButton(false);
            ShowMask(true);

            // 高亮目标塔位置
            if (Map.GridSystem.HasInstance)
            {
                Vector3 worldPos = Map.GridSystem.Instance.GridToWorld(step.targetGridPos);
                ShowHighlight(worldPos, new Vector2(1.5f, 1.5f));
                ShowFinger(true, worldPos);
            }

            // 监听升级事件
            EventBus.Instance.Subscribe<TowerUpgradedEvent>(OnTutorialTowerUpgraded);
        }

        /// <summary>引导选择词条步骤</summary>
        private void GuideSelectRuneStep(TutorialStep step)
        {
            ShowMessage(step.message);
            ShowContinueButton(false);
            ShowMask(true);
            ShowFinger(false);

            // 监听词条选择事件
            EventBus.Instance.Subscribe<RuneSelectedEvent>(OnTutorialRuneSelected);
        }

        /// <summary>等待条件步骤</summary>
        private void WaitForConditionStep(TutorialStep step)
        {
            ShowMessage(step.message);
            ShowContinueButton(false);

            if (!string.IsNullOrEmpty(step.message))
            {
                ShowMask(true);
            }
            else
            {
                ShowMask(false);
            }

            ShowFinger(false);
            // 条件检查在Update中进行
        }

        /// <summary>延迟步骤</summary>
        private IEnumerator DelayStep(TutorialStep step)
        {
            if (!string.IsNullOrEmpty(step.message))
            {
                ShowMessage(step.message);
            }

            ShowMask(false);
            ShowFinger(false);

            yield return new WaitForSecondsRealtime(step.delay);

            _currentStepComplete = true;
        }

        /// <summary>自由操作步骤</summary>
        private void FreePlayStep(TutorialStep step)
        {
            if (!string.IsNullOrEmpty(step.message))
            {
                ShowMessage(step.message);
            }
            else
            {
                HideMessage();
            }

            ShowMask(false);
            ShowFinger(false);

            // 启用完整输入
            if (BattleInputHandler.HasInstance)
            {
                BattleInputHandler.Instance.InputEnabled = true;
            }
        }

        /// <summary>引导点击步骤</summary>
        private void GuideClickStep(TutorialStep step)
        {
            ShowMessage(step.message);
            ShowContinueButton(false);
            ShowMask(true);

            if (step.useWorldPos)
            {
                ShowHighlight(new Vector3(step.highlightTarget.x, step.highlightTarget.y, 0),
                    new Vector2(1.5f, 1.5f));
                ShowFinger(true, new Vector3(step.highlightTarget.x, step.highlightTarget.y, 0));
            }
        }

        // ====================================================================
        // 步骤完成检查
        // ====================================================================

        /// <summary>检查当前步骤的自动完成条件</summary>
        private void CheckStepCompletion()
        {
            if (_currentStepComplete)
            {
                NextStep();
                return;
            }

            if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Count) return;

            var step = _steps[_currentStepIndex];

            // WaitForCondition类型：检查条件
            if (step.type == TutorialStepType.WaitForCondition && step.condition != null)
            {
                if (step.condition())
                {
                    _currentStepComplete = true;
                }
            }
        }

        // ====================================================================
        // 引导事件回调
        // ====================================================================

        private void OnTutorialTowerPlaced(TowerAttackEvent evt)
        {
            // 放塔步骤完成（简化判断：只要放了任意塔就算完成）
            EventBus.Instance.Unsubscribe<TowerAttackEvent>(OnTutorialTowerPlaced);
            _currentStepComplete = true;
        }

        private void OnTutorialWaveStarted(WaveStartEvent evt)
        {
            EventBus.Instance.Unsubscribe<WaveStartEvent>(OnTutorialWaveStarted);
            _currentStepComplete = true;
        }

        private void OnTutorialTowerUpgraded(TowerUpgradedEvent evt)
        {
            EventBus.Instance.Unsubscribe<TowerUpgradedEvent>(OnTutorialTowerUpgraded);
            _currentStepComplete = true;
        }

        private void OnTutorialRuneSelected(RuneSelectedEvent evt)
        {
            EventBus.Instance.Unsubscribe<RuneSelectedEvent>(OnTutorialRuneSelected);
            _currentStepComplete = true;
        }

        // ====================================================================
        // 引导完成
        // ====================================================================

        /// <summary>引导完成</summary>
        private void CompleteTutorial()
        {
            _isActive = false;
            HideAllUI();

            // 恢复正常游戏状态
            if (BattleInputHandler.HasInstance)
            {
                BattleInputHandler.Instance.InputEnabled = true;
            }

            MarkTutorialCompleted();

            EventBus.Instance.Publish(new TutorialCompleteEvent());

            Logger.I("TutorialSystem", "✅ 新手引导完成!");
        }

        // ====================================================================
        // 构建第一关引导步骤
        // ====================================================================

        /// <summary>构建第一关的引导步骤序列</summary>
        private void BuildFirstLevelSteps()
        {
            // 步骤1：欢迎文字
            _steps.Add(new TutorialStep
            {
                stepId = "welcome",
                type = TutorialStepType.ShowMessage,
                message = "欢迎来到以太守卫者！\n你需要建造防御塔来抵挡怪物的入侵。"
            });

            // 步骤2：介绍地图
            _steps.Add(new TutorialStep
            {
                stepId = "intro_map",
                type = TutorialStepType.ShowMessage,
                message = "怪物会沿着路径前进。\n你需要在路径两侧放置防御塔。"
            });

            // 步骤3：引导放置第一个箭塔
            _steps.Add(new TutorialStep
            {
                stepId = "place_first_tower",
                type = TutorialStepType.GuidePlaceTower,
                message = "👆 点击高亮区域放置一座箭塔！",
                targetGridPos = new Vector2Int(4, 0), // 路径旁的塔位
                targetTowerType = TowerType.Archer
            });

            // 步骤4：放塔成功提示
            _steps.Add(new TutorialStep
            {
                stepId = "tower_placed",
                type = TutorialStepType.ShowMessage,
                message = "很好！箭塔会自动攻击射程内的怪物。\n现在开始第一波战斗吧！"
            });

            // 步骤5：引导开始波次
            _steps.Add(new TutorialStep
            {
                stepId = "start_wave",
                type = TutorialStepType.GuideStartWave,
                message = "👆 点击「开始波次」按钮！"
            });

            // 步骤6：等待第一波完成
            _steps.Add(new TutorialStep
            {
                stepId = "wait_wave1",
                type = TutorialStepType.WaitForCondition,
                message = "战斗中...观察箭塔自动攻击怪物！",
                condition = () => WaveManager.HasInstance && !WaveManager.Instance.IsWaveActive
                                  && WaveManager.Instance.CurrentWaveNumber >= 1
            });

            // 步骤7：提示升级
            _steps.Add(new TutorialStep
            {
                stepId = "intro_upgrade",
                type = TutorialStepType.ShowMessage,
                message = "第一波完成！\n击杀怪物可以获得金币。\n试试升级你的箭塔吧！"
            });

            // 步骤8：引导升级塔
            _steps.Add(new TutorialStep
            {
                stepId = "upgrade_tower",
                type = TutorialStepType.GuideUpgradeTower,
                message = "👆 点击箭塔，然后点击「升级」按钮！",
                targetGridPos = new Vector2Int(4, 0)
            });

            // 步骤9：提示再放一座塔
            _steps.Add(new TutorialStep
            {
                stepId = "place_second_tower",
                type = TutorialStepType.ShowMessage,
                message = "升级成功！塔升级后伤害和攻速都会提升。\n再放一座新塔来加强防御吧！"
            });

            // 步骤10：自由放塔
            _steps.Add(new TutorialStep
            {
                stepId = "free_build",
                type = TutorialStepType.FreePlay,
                message = "自由建造时间！在路径旁放置更多防御塔。\n准备好后点击「开始波次」继续战斗。"
            });

            // 步骤11：等待第2波完成
            _steps.Add(new TutorialStep
            {
                stepId = "wait_wave2",
                type = TutorialStepType.WaitForCondition,
                message = "",
                condition = () => WaveManager.HasInstance && WaveManager.Instance.CurrentWaveNumber >= 2
                                  && !WaveManager.Instance.IsWaveActive
            });

            // 步骤12：介绍词条系统
            _steps.Add(new TutorialStep
            {
                stepId = "intro_rune",
                type = TutorialStepType.ShowMessage,
                message = "波次间你可以选择一个词条来强化！\n每个词条都有不同的效果。"
            });

            // 步骤13：引导选择词条
            _steps.Add(new TutorialStep
            {
                stepId = "select_rune",
                type = TutorialStepType.GuideSelectRune,
                message = "👆 选择一个你喜欢的词条！"
            });

            // 步骤14：引导完成，自由游戏
            _steps.Add(new TutorialStep
            {
                stepId = "tutorial_complete",
                type = TutorialStepType.ShowMessage,
message = "* 引导完成！\n\n现在你已经掌握了基本操作：\n√ 放置防御塔\n√ 升级塔\n√ 选择词条\n\n继续战斗，击败所有波次！"

            });
        }

        // ====================================================================
        // UI 创建与控制
        // ====================================================================

        /// <summary>动态创建引导UI</summary>
        private void CreateTutorialUI()
        {
            // 创建Canvas
            var canvasObj = new GameObject("TutorialCanvas");
            canvasObj.transform.SetParent(transform);
            _tutorialCanvas = canvasObj.AddComponent<Canvas>();
            _tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _tutorialCanvas.sortingOrder = 1000; // 最高层
            _tutorialCanvas.pixelPerfect = true;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;


            canvasObj.AddComponent<GraphicRaycaster>();

            // 创建遮罩
            var maskObj = new GameObject("Mask");
            maskObj.transform.SetParent(canvasObj.transform, false);
            _maskImage = maskObj.AddComponent<Image>();
            _maskImage.color = new Color(0, 0, 0, 0.6f);
            _maskImage.raycastTarget = true;
            var maskRect = maskObj.GetComponent<RectTransform>();
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.sizeDelta = Vector2.zero;

            // 创建消息背景
            var msgBgObj = new GameObject("MessageBg");
            msgBgObj.transform.SetParent(canvasObj.transform, false);
            _messageBg = msgBgObj.AddComponent<Image>();
            _messageBg.color = new Color(0.1f, 0.1f, 0.2f, 0.9f);
            var msgBgRect = msgBgObj.GetComponent<RectTransform>();
            msgBgRect.anchorMin = new Vector2(0.05f, 0.7f);
            msgBgRect.anchorMax = new Vector2(0.95f, 0.9f);
            msgBgRect.sizeDelta = Vector2.zero;

            // 创建消息文字
            var msgObj = new GameObject("MessageText");
            msgObj.transform.SetParent(msgBgObj.transform, false);
            _messageText = msgObj.AddComponent<Text>();
_messageText.font = BattleUI.GetFont();

            _messageText.fontSize = 32;
            _messageText.color = Color.white;
            _messageText.alignment = TextAnchor.MiddleCenter;
            _messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _messageText.verticalOverflow = VerticalWrapMode.Overflow;
            var msgRect = msgObj.GetComponent<RectTransform>();
            msgRect.anchorMin = new Vector2(0.05f, 0.05f);
            msgRect.anchorMax = new Vector2(0.95f, 0.95f);
            msgRect.sizeDelta = Vector2.zero;

            // 创建"继续"按钮
            var continueObj = new GameObject("ContinueButton");
            continueObj.transform.SetParent(canvasObj.transform, false);
            var continueBg = continueObj.AddComponent<Image>();
            continueBg.color = new Color(0.2f, 0.6f, 1f, 1f);
            _continueButton = continueObj.AddComponent<Button>();
            _continueButton.onClick.AddListener(OnContinueClicked);
            var continueRect = continueObj.GetComponent<RectTransform>();
            continueRect.anchorMin = new Vector2(0.3f, 0.62f);
            continueRect.anchorMax = new Vector2(0.7f, 0.68f);
            continueRect.sizeDelta = Vector2.zero;

            var continueTxt = new GameObject("Text");
            continueTxt.transform.SetParent(continueObj.transform, false);
            var ctText = continueTxt.AddComponent<Text>();
            ctText.text = "点击继续";
ctText.font = BattleUI.GetFont();

            ctText.fontSize = 28;
            ctText.color = Color.white;
            ctText.alignment = TextAnchor.MiddleCenter;
            var ctRect = continueTxt.GetComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.sizeDelta = Vector2.zero;

            // 创建"跳过引导"按钮
            var skipObj = new GameObject("SkipButton");
            skipObj.transform.SetParent(canvasObj.transform, false);
            var skipBg = skipObj.AddComponent<Image>();
            skipBg.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            _skipButton = skipObj.AddComponent<Button>();
            _skipButton.onClick.AddListener(SkipTutorial);
            var skipRect = skipObj.GetComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(0.75f, 0.93f);
            skipRect.anchorMax = new Vector2(0.98f, 0.98f);
            skipRect.sizeDelta = Vector2.zero;

            var skipTxt = new GameObject("Text");
            skipTxt.transform.SetParent(skipObj.transform, false);
            var stText = skipTxt.AddComponent<Text>();
            stText.text = "跳过";
stText.font = BattleUI.GetFont();

            stText.fontSize = 24;
            stText.color = Color.white;
            stText.alignment = TextAnchor.MiddleCenter;
            var stRect = skipTxt.GetComponent<RectTransform>();
            stRect.anchorMin = Vector2.zero;
            stRect.anchorMax = Vector2.one;
            stRect.sizeDelta = Vector2.zero;

            // 创建手指图标（用简单的三角形代替，原型阶段）
            var fingerObj = new GameObject("FingerIcon");
            fingerObj.transform.SetParent(canvasObj.transform, false);
            var fingerImg = fingerObj.AddComponent<Image>();
            fingerImg.color = new Color(1f, 1f, 0f, 0.9f);
            _fingerIcon = fingerObj.GetComponent<RectTransform>();
            _fingerIcon.sizeDelta = new Vector2(60, 60);

            // 初始隐藏所有
            HideAllUI();

            Logger.D("TutorialSystem", "引导UI创建完成");
        }

        // ========== UI控制方法 ==========

        /// <summary>显示/隐藏遮罩</summary>
        private void ShowMask(bool show)
        {
            if (_maskImage != null)
                _maskImage.gameObject.SetActive(show);
        }

        /// <summary>显示消息</summary>
        private void ShowMessage(string message)
        {
            if (_messageText != null)
            {
                _messageText.text = message;
                _messageBg.gameObject.SetActive(true);
            }
        }

        /// <summary>隐藏消息</summary>
        private void HideMessage()
        {
            if (_messageBg != null)
                _messageBg.gameObject.SetActive(false);
        }

        /// <summary>显示/隐藏"继续"按钮</summary>
        private void ShowContinueButton(bool show)
        {
            if (_continueButton != null)
                _continueButton.gameObject.SetActive(show);
        }

        /// <summary>显示高亮区域</summary>
        private void ShowHighlight(Vector3 worldPos, Vector2 size)
        {
            // 原型阶段简单实现：在遮罩上开一个"孔"
            // 后续使用自定义shader实现圆形遮罩孔洞
            // 当前暂不实现孔洞，只通过手指引导和文字引导
        }

        /// <summary>显示手指图标</summary>
        private void ShowFinger(bool show, Vector3 worldPos = default)
        {
            if (_fingerIcon == null) return;

            _fingerIcon.gameObject.SetActive(show);

            if (show && _mainCamera != null)
            {
                Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);
                _fingerIcon.position = screenPos + new Vector3(0, 40, 0); // 偏移到目标上方
            }
        }

        /// <summary>隐藏所有UI</summary>
        private void HideAllUI()
        {
            ShowMask(false);
            HideMessage();
            ShowContinueButton(false);
            ShowFinger(false);
        }

        /// <summary>清理UI</summary>
        private void CleanupUI()
        {
            if (_tutorialCanvas != null)
            {
                Destroy(_tutorialCanvas.gameObject);
            }
        }

        /// <summary>手指动画（上下浮动）</summary>
        private void AnimateFinger(float deltaTime)
        {
            if (_fingerIcon == null || !_fingerIcon.gameObject.activeInHierarchy) return;

            _fingerAnimTimer += deltaTime * 3f;
            float offset = Mathf.Sin(_fingerAnimTimer) * 10f;
            _fingerIcon.anchoredPosition += new Vector2(0, offset * deltaTime);
        }

        // ========== UI回调 ==========

        private void OnContinueClicked()
        {
            _currentStepComplete = true;
        }

        // ====================================================================
        // 持久化
        // ====================================================================

        /// <summary>标记引导已完成</summary>
        private void MarkTutorialCompleted()
        {
            PlayerPrefs.SetInt("TutorialCompleted", 1);
            PlayerPrefs.Save();

            Logger.I("TutorialSystem", "引导完成标记已保存");
        }

        /// <summary>检查引导是否已完成</summary>
        public bool IsTutorialCompleted()
        {
            return PlayerPrefs.GetInt("TutorialCompleted", 0) == 1;
        }

        /// <summary>重置引导完成状态（调试用）</summary>
        public void ResetTutorialProgress()
        {
            PlayerPrefs.SetInt("TutorialCompleted", 0);
            PlayerPrefs.Save();
            Logger.I("TutorialSystem", "引导进度已重置");
        }

        // ====================================================================
        // 调试
        // ====================================================================

        public string GetDebugInfo()
        {
            if (!_isActive) return "引导未激活";
            var step = _currentStepIndex >= 0 && _currentStepIndex < _steps.Count
                ? _steps[_currentStepIndex] : null;
            return $"引导中 {_currentStepIndex + 1}/{_steps.Count} " +
                   $"[{step?.stepId ?? "?"}] {step?.type}";
        }
    }
}
