// ============================================================
// 文件名：TutorialSystemFull.cs
// 功能描述：完整版新手引导系统 — 主界面引导、各系统首次使用引导
//          扩展Battle/Tutorial/TutorialSystem.cs的战斗引导
//          增加配置化引导步骤、弱引导箭头、中断恢复
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #269-275
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Data;

namespace AetheraSurvivors.MetaGame
{
    // ========== 引导类型 ==========

    /// <summary>引导模式</summary>
    public enum GuideMode
    {
        /// <summary>强制引导：遮罩+高亮目标，必须完成</summary>
        Forced,
        /// <summary>弱引导：箭头提示，可忽略</summary>
        Soft,
        /// <summary>提示引导：Toast提示，自动消失</summary>
        Toast
    }

    /// <summary>引导触发条件</summary>
    public enum GuideTrigger
    {
        /// <summary>首次进入主界面</summary>
        FirstEnterMainMenu,
        /// <summary>首次打开英雄面板</summary>
        FirstOpenHero,
        /// <summary>首次打开商城</summary>
        FirstOpenShop,
        /// <summary>首次打开抽卡</summary>
        FirstOpenGacha,
        /// <summary>首次打开战令</summary>
        FirstOpenBattlePass,
        /// <summary>首次打开任务</summary>
        FirstOpenQuest,
        /// <summary>首次打开邮件</summary>
        FirstOpenMail,
        /// <summary>首次签到</summary>
        FirstCheckIn,
        /// <summary>首次通关第1关</summary>
        FirstClearLevel1,
        /// <summary>首次通关第3关</summary>
        FirstClearLevel3,
        /// <summary>首次获得英雄</summary>
        FirstGetHero,
        /// <summary>首次升级英雄</summary>
        FirstUpgradeHero,
    }

    // ========== 引导步骤配置 ==========

    /// <summary>
    /// 元游戏引导步骤配置
    /// </summary>
    [Serializable]
    public class MetaGuideStep
    {
        public string StepId;
        public GuideMode Mode;
        public string Title;
        public string Message;
        public string TargetButtonName; // 需要高亮的按钮名称
        public Vector2 ArrowOffset;     // 箭头偏移
        public float AutoDismissTime;   // 自动消失时间（Toast模式）
        public Action OnComplete;       // 完成回调
    }

    /// <summary>
    /// 引导序列配置
    /// </summary>
    [Serializable]
    public class MetaGuideSequence
    {
        public string SequenceId;
        public GuideTrigger Trigger;
        public List<MetaGuideStep> Steps;
        public int Priority; // 优先级（数字越小越优先）
    }

    // ========== 引导存档 ==========

    [Serializable]
    public class GuideSaveData
    {
        /// <summary>已完成的引导序列ID列表</summary>
        public List<string> CompletedSequences = new List<string>();

        /// <summary>当前正在进行的序列ID</summary>
        public string CurrentSequenceId;

        /// <summary>当前步骤索引</summary>
        public int CurrentStepIndex;

        /// <summary>已触发过的弱引导列表</summary>
        public List<string> TriggeredSoftGuides = new List<string>();
    }

    // ========== 完整版新手引导系统 ==========

    /// <summary>
    /// 完整版新手引导系统
    /// 
    /// 与Battle/Tutorial/TutorialSystem的关系：
    /// - TutorialSystem 负责战斗内引导（放塔、开波次、升级塔等）
    /// - MetaGuidanceSystem 负责元游戏引导（主界面导航、系统首次使用等）
    /// - 两者通过 PlayerData.TutorialStep 共享进度
    /// 
    /// 功能：
    /// 1. 配置化引导序列
    /// 2. 强制引导（遮罩+高亮）和弱引导（箭头提示）
    /// 3. 引导进度存储和中断恢复
    /// 4. 各系统首次使用引导
    /// </summary>
    public class MetaGuidanceSystem : Singleton<MetaGuidanceSystem>
    {
        // ========== 引导序列库 ==========
        private List<MetaGuideSequence> _sequences;
        private GuideSaveData _saveData;

        // ========== 运行时状态 ==========
        private MetaGuideSequence _activeSequence;
        private int _activeStepIndex;
        private bool _isGuiding;

        // ========== UI引用 ==========
        private GameObject _guideCanvas;
        private Image _maskOverlay;
        private RectTransform _highlightFrame;
        private Text _txtGuideTitle;
        private Text _txtGuideMessage;
        private Button _btnGuideContinue;
        private Button _btnGuideSkip;
        private RectTransform _arrowIndicator;
        private RectTransform _toastPanel;
        private Text _txtToast;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            _sequences = new List<MetaGuideSequence>();
            LoadSaveData();
            RegisterAllSequences();
            Debug.Log("[MetaGuidanceSystem] 初始化完成");
        }

        protected override void OnDispose()
        {
            SaveProgress();
        }

        // ========== 公共方法 ==========

        /// <summary>尝试触发引导</summary>
        public bool TryTrigger(GuideTrigger trigger)
        {
            if (_isGuiding) return false;

            for (int i = 0; i < _sequences.Count; i++)
            {
                var seq = _sequences[i];
                if (seq.Trigger != trigger) continue;
                if (IsSequenceCompleted(seq.SequenceId)) continue;

                StartSequence(seq);
                return true;
            }

            return false;
        }

        /// <summary>检查引导序列是否已完成</summary>
        public bool IsSequenceCompleted(string sequenceId)
        {
            return _saveData.CompletedSequences.Contains(sequenceId);
        }

        /// <summary>是否正在引导</summary>
        public bool IsGuiding => _isGuiding;

        /// <summary>强制完成当前步骤</summary>
        public void ForceCompleteStep()
        {
            if (_isGuiding) AdvanceStep();
        }

        /// <summary>跳过当前引导序列</summary>
        public void SkipCurrentSequence()
        {
            if (!_isGuiding || _activeSequence == null) return;

            Debug.Log($"[MetaGuidance] 跳过引导序列: {_activeSequence.SequenceId}");
            CompleteSequence();
        }

        /// <summary>恢复中断的引导</summary>
        public void ResumeIfNeeded()
        {
            if (string.IsNullOrEmpty(_saveData.CurrentSequenceId)) return;

            for (int i = 0; i < _sequences.Count; i++)
            {
                if (_sequences[i].SequenceId == _saveData.CurrentSequenceId)
                {
                    _activeSequence = _sequences[i];
                    _activeStepIndex = _saveData.CurrentStepIndex;
                    _isGuiding = true;
                    ExecuteCurrentStep();
                    Debug.Log($"[MetaGuidance] 恢复引导: {_saveData.CurrentSequenceId} step={_activeStepIndex}");
                    return;
                }
            }
        }

        /// <summary>重置所有引导进度（调试用）</summary>
        public void ResetAll()
        {
            _saveData = new GuideSaveData();
            SaveProgress();
            Debug.Log("[MetaGuidance] 所有引导进度已重置");
        }

        // ========== 私有方法 ==========

        private void StartSequence(MetaGuideSequence sequence)
        {
            _activeSequence = sequence;
            _activeStepIndex = 0;
            _isGuiding = true;

            _saveData.CurrentSequenceId = sequence.SequenceId;
            _saveData.CurrentStepIndex = 0;
            SaveProgress();

            Debug.Log($"[MetaGuidance] 开始引导序列: {sequence.SequenceId}, 共{sequence.Steps.Count}步");

            EnsureUICreated();
            ExecuteCurrentStep();
        }

        private void ExecuteCurrentStep()
        {
            if (_activeSequence == null || _activeStepIndex >= _activeSequence.Steps.Count)
            {
                CompleteSequence();
                return;
            }

            var step = _activeSequence.Steps[_activeStepIndex];

            switch (step.Mode)
            {
                case GuideMode.Forced:
                    ShowForcedGuide(step);
                    break;
                case GuideMode.Soft:
                    ShowSoftGuide(step);
                    break;
                case GuideMode.Toast:
                    ShowToastGuide(step);
                    break;
            }
        }

        private void AdvanceStep()
        {
            if (_activeSequence == null) return;

            var currentStep = _activeSequence.Steps[_activeStepIndex];
            currentStep.OnComplete?.Invoke();

            _activeStepIndex++;
            _saveData.CurrentStepIndex = _activeStepIndex;
            SaveProgress();

            if (_activeStepIndex >= _activeSequence.Steps.Count)
            {
                CompleteSequence();
            }
            else
            {
                ExecuteCurrentStep();
            }
        }

        private void CompleteSequence()
        {
            if (_activeSequence != null)
            {
                _saveData.CompletedSequences.Add(_activeSequence.SequenceId);
                _saveData.CurrentSequenceId = null;
                _saveData.CurrentStepIndex = 0;
                SaveProgress();

                if (EventBus.HasInstance)
                {
                    EventBus.Instance.Publish(new TutorialStepCompletedEvent
                    {
                        StepIndex = -1,
                        StepId = _activeSequence.SequenceId
                    });
                }
            }

            _activeSequence = null;
            _isGuiding = false;
            HideAllGuideUI();
        }

        // ========== UI显示 ==========

        private void ShowForcedGuide(MetaGuideStep step)
        {
            EnsureUICreated();

            // 显示遮罩
            if (_maskOverlay != null)
            {
                _maskOverlay.gameObject.SetActive(true);
                _maskOverlay.color = new Color(0, 0, 0, 0.65f);
            }

            // 显示标题和消息
            if (_txtGuideTitle != null)
            {
                _txtGuideTitle.text = step.Title ?? "";
                _txtGuideTitle.gameObject.SetActive(!string.IsNullOrEmpty(step.Title));
            }
            if (_txtGuideMessage != null)
            {
                _txtGuideMessage.text = step.Message;
                _txtGuideMessage.transform.parent.gameObject.SetActive(true);
            }

            // 显示继续按钮
            if (_btnGuideContinue != null)
                _btnGuideContinue.gameObject.SetActive(true);

            // 显示跳过按钮
            if (_btnGuideSkip != null)
                _btnGuideSkip.gameObject.SetActive(true);

            // 隐藏箭头和Toast
            if (_arrowIndicator != null) _arrowIndicator.gameObject.SetActive(false);
            if (_toastPanel != null) _toastPanel.gameObject.SetActive(false);
        }

        private void ShowSoftGuide(MetaGuideStep step)
        {
            EnsureUICreated();

            // 弱引导：不显示遮罩，只显示箭头和提示
            if (_maskOverlay != null) _maskOverlay.gameObject.SetActive(false);

            if (_arrowIndicator != null)
            {
                _arrowIndicator.gameObject.SetActive(true);
                _arrowIndicator.anchoredPosition = step.ArrowOffset;
            }

            if (_txtGuideMessage != null)
            {
                _txtGuideMessage.text = step.Message;
                _txtGuideMessage.transform.parent.gameObject.SetActive(true);
            }

            // 弱引导自动3秒后消失
            if (step.AutoDismissTime > 0)
            {
                // 使用TimerManager延迟
                AdvanceStepDelayed(step.AutoDismissTime);
            }
            else
            {
                if (_btnGuideContinue != null)
                    _btnGuideContinue.gameObject.SetActive(true);
            }

            // 记录已触发
            if (!_saveData.TriggeredSoftGuides.Contains(step.StepId))
            {
                _saveData.TriggeredSoftGuides.Add(step.StepId);
            }
        }

        private void ShowToastGuide(MetaGuideStep step)
        {
            EnsureUICreated();

            // Toast模式：只显示底部提示条
            if (_maskOverlay != null) _maskOverlay.gameObject.SetActive(false);
            if (_arrowIndicator != null) _arrowIndicator.gameObject.SetActive(false);

            if (_toastPanel != null)
            {
                _toastPanel.gameObject.SetActive(true);
                if (_txtToast != null) _txtToast.text = step.Message;
            }

            float dismissTime = step.AutoDismissTime > 0 ? step.AutoDismissTime : 2f;
            AdvanceStepDelayed(dismissTime);
        }

        private void AdvanceStepDelayed(float delay)
        {
            if (TimerManager.HasInstance)
            {
                TimerManager.Instance.DelayCall(delay, () =>
                {
                    if (_isGuiding) AdvanceStep();
                });

            }
        }

        private void HideAllGuideUI()
        {
            if (_guideCanvas != null) _guideCanvas.SetActive(false);
        }

        // ========== UI创建 ==========

        private void EnsureUICreated()
        {
            if (_guideCanvas != null)
            {
                _guideCanvas.SetActive(true);
                return;
            }

            // 创建引导Canvas
            _guideCanvas = new GameObject("[MetaGuidanceCanvas]");
            var canvas = _guideCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var scaler = _guideCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            _guideCanvas.AddComponent<GraphicRaycaster>();
            UnityEngine.Object.DontDestroyOnLoad(_guideCanvas);

            var canvasRect = _guideCanvas.GetComponent<RectTransform>();

            // 遮罩
            var maskObj = new GameObject("Mask");
            maskObj.transform.SetParent(canvasRect, false);
            _maskOverlay = maskObj.AddComponent<Image>();
            _maskOverlay.color = new Color(0, 0, 0, 0.65f);
            _maskOverlay.raycastTarget = true;
            var maskRect = maskObj.GetComponent<RectTransform>();
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = Vector2.zero;
            maskRect.offsetMax = Vector2.zero;

            // 消息面板
            var msgPanel = new GameObject("MsgPanel");
            msgPanel.transform.SetParent(canvasRect, false);
            var msgPanelRect = msgPanel.AddComponent<RectTransform>();
            msgPanelRect.anchorMin = new Vector2(0.05f, 0.55f);
            msgPanelRect.anchorMax = new Vector2(0.95f, 0.80f);
            msgPanelRect.offsetMin = Vector2.zero;
            msgPanelRect.offsetMax = Vector2.zero;
            var msgBg = msgPanel.AddComponent<Image>();
            msgBg.color = new Color(0.08f, 0.08f, 0.18f, 0.95f);

            // 标题
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(msgPanel.transform, false);
            _txtGuideTitle = titleObj.AddComponent<Text>();
            _txtGuideTitle.fontSize = 24;
            _txtGuideTitle.color = new Color(1f, 0.85f, 0.35f);
            _txtGuideTitle.alignment = TextAnchor.MiddleCenter;
            _txtGuideTitle.font = Font.CreateDynamicFontFromOSFont("Arial", 24);
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.7f);
            titleRect.anchorMax = new Vector2(0.95f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            // 消息
            var msgObj = new GameObject("Message");
            msgObj.transform.SetParent(msgPanel.transform, false);
            _txtGuideMessage = msgObj.AddComponent<Text>();
            _txtGuideMessage.fontSize = 18;
            _txtGuideMessage.color = Color.white;
            _txtGuideMessage.alignment = TextAnchor.MiddleCenter;
            _txtGuideMessage.font = Font.CreateDynamicFontFromOSFont("Arial", 18);
            _txtGuideMessage.horizontalOverflow = HorizontalWrapMode.Wrap;
            var msgRect = msgObj.GetComponent<RectTransform>();
            msgRect.anchorMin = new Vector2(0.05f, 0.1f);
            msgRect.anchorMax = new Vector2(0.95f, 0.68f);
            msgRect.offsetMin = Vector2.zero;
            msgRect.offsetMax = Vector2.zero;

            // 继续按钮
            var continueObj = new GameObject("BtnContinue");
            continueObj.transform.SetParent(canvasRect, false);
            var continueRect = continueObj.AddComponent<RectTransform>();
            continueRect.anchorMin = new Vector2(0.3f, 0.45f);
            continueRect.anchorMax = new Vector2(0.7f, 0.52f);
            continueRect.offsetMin = Vector2.zero;
            continueRect.offsetMax = Vector2.zero;
            continueObj.AddComponent<Image>().color = new Color(0.15f, 0.30f, 0.55f, 1f);
            _btnGuideContinue = continueObj.AddComponent<Button>();
            _btnGuideContinue.onClick.AddListener(() => { if (_isGuiding) AdvanceStep(); });
            var continueTxt = new GameObject("Txt");
            continueTxt.transform.SetParent(continueObj.transform, false);
            var ct = continueTxt.AddComponent<Text>();
            ct.text = "点击继续";
            ct.fontSize = 20;
            ct.color = Color.white;
            ct.alignment = TextAnchor.MiddleCenter;
            ct.font = Font.CreateDynamicFontFromOSFont("Arial", 20);
            var ctRect = continueTxt.GetComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.offsetMin = Vector2.zero;
            ctRect.offsetMax = Vector2.zero;

            // 跳过按钮
            var skipObj = new GameObject("BtnSkip");
            skipObj.transform.SetParent(canvasRect, false);
            var skipRect = skipObj.AddComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(0.78f, 0.92f);
            skipRect.anchorMax = new Vector2(0.98f, 0.97f);
            skipRect.offsetMin = Vector2.zero;
            skipRect.offsetMax = Vector2.zero;
            skipObj.AddComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            _btnGuideSkip = skipObj.AddComponent<Button>();
            _btnGuideSkip.onClick.AddListener(SkipCurrentSequence);
            var skipTxt = new GameObject("Txt");
            skipTxt.transform.SetParent(skipObj.transform, false);
            var st = skipTxt.AddComponent<Text>();
            st.text = "跳过";
            st.fontSize = 16;
            st.color = Color.white;
            st.alignment = TextAnchor.MiddleCenter;
            st.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            var stRect = skipTxt.GetComponent<RectTransform>();
            stRect.anchorMin = Vector2.zero;
            stRect.anchorMax = Vector2.one;
            stRect.offsetMin = Vector2.zero;
            stRect.offsetMax = Vector2.zero;

            // 箭头指示器
            var arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(canvasRect, false);
            _arrowIndicator = arrowObj.AddComponent<RectTransform>();
            _arrowIndicator.sizeDelta = new Vector2(50, 50);
            var arrowImg = arrowObj.AddComponent<Image>();
            arrowImg.color = new Color(1f, 0.85f, 0.35f, 0.9f);
            _arrowIndicator.gameObject.SetActive(false);

            // Toast面板
            var toastObj = new GameObject("Toast");
            toastObj.transform.SetParent(canvasRect, false);
            _toastPanel = toastObj.AddComponent<RectTransform>();
            _toastPanel.anchorMin = new Vector2(0.1f, 0.02f);
            _toastPanel.anchorMax = new Vector2(0.9f, 0.08f);
            _toastPanel.offsetMin = Vector2.zero;
            _toastPanel.offsetMax = Vector2.zero;
            toastObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.2f, 0.9f);
            var toastTxtObj = new GameObject("Txt");
            toastTxtObj.transform.SetParent(toastObj.transform, false);
            _txtToast = toastTxtObj.AddComponent<Text>();
            _txtToast.fontSize = 16;
            _txtToast.color = Color.white;
            _txtToast.alignment = TextAnchor.MiddleCenter;
            _txtToast.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            var toastTxtRect = toastTxtObj.GetComponent<RectTransform>();
            toastTxtRect.anchorMin = Vector2.zero;
            toastTxtRect.anchorMax = Vector2.one;
            toastTxtRect.offsetMin = Vector2.zero;
            toastTxtRect.offsetMax = Vector2.zero;
            _toastPanel.gameObject.SetActive(false);
        }

        // ========== 引导序列注册 ==========

        private void RegisterAllSequences()
        {
            // ===== 1. 首次进入主界面引导 =====
            _sequences.Add(new MetaGuideSequence
            {
                SequenceId = "main_menu_intro",
                Trigger = GuideTrigger.FirstEnterMainMenu,
                Priority = 0,
                Steps = new List<MetaGuideStep>
                {
                    new MetaGuideStep
                    {
                        StepId = "welcome_back",
                        Mode = GuideMode.Forced,
                        Title = "🏠 欢迎回到基地！",
                        Message = "这里是你的指挥中心。\n你可以在这里管理英雄、购买装备、\n准备下一场战斗！"
                    },
                    new MetaGuideStep
                    {
                        StepId = "intro_battle_btn",
                        Mode = GuideMode.Forced,
Title = "[剑] 开始战斗",

                        Message = "点击「开始战斗」进入关卡选择。\n选择关卡后即可开始塔防战斗！",
                        TargetButtonName = "BtnBattle"
                    },
                    new MetaGuideStep
                    {
                        StepId = "intro_hero_btn",
                        Mode = GuideMode.Soft,
                        Message = "👆 这里可以查看和升级你的英雄",
                        TargetButtonName = "BtnHero",
                        AutoDismissTime = 3f
                    },
                    new MetaGuideStep
                    {
                        StepId = "intro_nav_bar",
                        Mode = GuideMode.Toast,
                        Message = "💡 底部导航栏可以快速访问任务、邮件、排行等功能",
                        AutoDismissTime = 3f
                    }
                }
            });

            // ===== 2. 首次打开英雄面板引导 =====
            _sequences.Add(new MetaGuideSequence
            {
                SequenceId = "hero_panel_intro",
                Trigger = GuideTrigger.FirstOpenHero,
                Priority = 1,
                Steps = new List<MetaGuideStep>
                {
                    new MetaGuideStep
                    {
                        StepId = "hero_list",
                        Mode = GuideMode.Forced,
                        Title = "🦸 英雄系统",
                        Message = "这里展示你拥有的所有英雄。\n每个英雄都有独特的技能和属性。\n选择一个英雄出战吧！"
                    },
                    new MetaGuideStep
                    {
                        StepId = "hero_upgrade",
                        Mode = GuideMode.Soft,
                        Message = "👆 点击英雄可以查看详情和升级",
                        AutoDismissTime = 3f
                    }
                }
            });

            // ===== 3. 首次打开抽卡引导 =====
            _sequences.Add(new MetaGuideSequence
            {
                SequenceId = "gacha_intro",
                Trigger = GuideTrigger.FirstOpenGacha,
                Priority = 1,
                Steps = new List<MetaGuideStep>
                {
                    new MetaGuideStep
                    {
                        StepId = "gacha_explain",
                        Mode = GuideMode.Forced,
                        Title = "🎰 召唤系统",
                        Message = "消耗钻石或召唤券来召唤新英雄！\n\n" +
                                  "📊 概率公示：\n" +
                                  "  R(普通): 85%  SR(稀有): 12%  SSR(传说): 3%\n\n" +
                                  "🎯 50次保底必出SSR！"
                    },
                    new MetaGuideStep
                    {
                        StepId = "gacha_tip",
                        Mode = GuideMode.Toast,
                        Message = "💡 十连抽保底至少1个SR，更划算哦！",
                        AutoDismissTime = 3f
                    }
                }
            });

            // ===== 4. 首次打开战令引导 =====
            _sequences.Add(new MetaGuideSequence
            {
                SequenceId = "battlepass_intro",
                Trigger = GuideTrigger.FirstOpenBattlePass,
                Priority = 1,
                Steps = new List<MetaGuideStep>
                {
                    new MetaGuideStep
                    {
                        StepId = "bp_explain",
                        Mode = GuideMode.Forced,
                        Title = "🎫 赛季战令",
                        Message = "通过战斗获得战令经验，解锁丰厚奖励！\n\n" +
                                  "🆓 免费轨：所有玩家可领取\n" +
                                  "💎 付费轨：解锁后获得双倍奖励\n\n" +
                                  "赛季结束后重置，抓紧时间！"
                    }
                }
            });

            // ===== 5. 首次打开商城引导 =====
            _sequences.Add(new MetaGuideSequence
            {
                SequenceId = "shop_intro",
                Trigger = GuideTrigger.FirstOpenShop,
                Priority = 1,
                Steps = new List<MetaGuideStep>
                {
                    new MetaGuideStep
                    {
                        StepId = "shop_explain",
                        Mode = GuideMode.Forced,
                        Title = "🛒 商城",
                        Message = "在商城可以购买钻石、礼包和道具。\n\n" +
                                  "🎁 首充6元即可获得SSR英雄！\n" +
                                  "📅 月卡每天领取钻石，超值推荐！"
                    }
                }
            });

            // ===== 6. 首次打开任务引导 =====
            _sequences.Add(new MetaGuideSequence
            {
                SequenceId = "quest_intro",
                Trigger = GuideTrigger.FirstOpenQuest,
                Priority = 1,
                Steps = new List<MetaGuideStep>
                {
                    new MetaGuideStep
                    {
                        StepId = "quest_explain",
                        Mode = GuideMode.Forced,
                        Title = "📋 每日任务",
                        Message = "完成每日任务获得奖励和活跃度！\n\n" +
                                  "活跃度达到指定值可领取额外奖励。\n" +
                                  "每天凌晨自动刷新。"
                    }
                }
            });

            // ===== 7. 首次签到引导 =====
            _sequences.Add(new MetaGuideSequence
            {
                SequenceId = "checkin_intro",
                Trigger = GuideTrigger.FirstCheckIn,
                Priority = 2,
                Steps = new List<MetaGuideStep>
                {
                    new MetaGuideStep
                    {
                        StepId = "checkin_explain",
                        Mode = GuideMode.Toast,
                        Message = "🎉 每日签到可获得丰厚奖励，连续签到7天有额外惊喜！",
                        AutoDismissTime = 3f
                    }
                }
            });

            // ===== 8. 首次打开邮件引导 =====
            _sequences.Add(new MetaGuideSequence
            {
                SequenceId = "mail_intro",
                Trigger = GuideTrigger.FirstOpenMail,
                Priority = 2,
                Steps = new List<MetaGuideStep>
                {
                    new MetaGuideStep
                    {
                        StepId = "mail_explain",
                        Mode = GuideMode.Toast,
                        Message = "📬 邮件中可能有系统奖励，记得及时领取附件！",
                        AutoDismissTime = 3f
                    }
                }
            });

            // 按优先级排序
            _sequences.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        // ========== 存档 ==========

        private void LoadSaveData()
        {
            if (SaveManager.HasInstance)
            {
                _saveData = SaveManager.Instance.Load<GuideSaveData>("meta_guide_data");
            }
            if (_saveData == null)
            {
                _saveData = new GuideSaveData();
            }
        }

        private void SaveProgress()
        {
            if (SaveManager.HasInstance)
            {
                SaveManager.Instance.Save("meta_guide_data", _saveData);
            }
        }
    }
}
