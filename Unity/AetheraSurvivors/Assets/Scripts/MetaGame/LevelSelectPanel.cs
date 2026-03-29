// ============================================================
// 文件名：LevelSelectPanel.cs
// 功能描述：关卡选择系统 — 章节地图、关卡节点、星级显示、难度选择
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #249-250
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
    /// <summary>难度枚举</summary>
    public enum DifficultyMode { Normal = 0, Hard = 1, Nightmare = 2 }

    /// <summary>
    /// 关卡选择面板
    /// 
    /// 功能：
    /// 1. 章节横向滚动选择
    /// 2. 每章5个关卡节点（带星级/锁定状态）
    /// 3. 难度选择（普通/困难/噩梦）
    /// 4. 关卡详情弹窗
    /// 5. 扫荡功能（3星通关后可用）
    /// </summary>
    public class LevelSelectPanel : BasePanel
    {
        public override UILayer Layer => UILayer.Normal;
        public override bool IsCached => true;

        // ========== 常量 ==========
        private const int ChaptersCount = 30;
        private const int LevelsPerChapter = 5;

        // ========== UI引用 ==========
        private ScrollRect _chapterScroll;
        private RectTransform _chapterContent;
        private Text _txtChapterTitle;
        private RectTransform _levelNodesArea;
        private Button _btnBack;
        private Button _btnDiffNormal;
        private Button _btnDiffHard;
        private Button _btnDiffNightmare;

        // 关卡详情弹窗
        private GameObject _detailPopup;
        private Text _txtDetailTitle;
        private Text _txtDetailInfo;
        private Text _txtDetailStamina;
        private Button _btnStartBattle;
        private Button _btnSweep;
        private Button _btnCloseDetail;

        // 扫荡结果弹窗
        private GameObject _sweepResultPopup;
        private Text _txtSweepResult;
        private Button _btnSweepClose;
        private Button _btnSweepMore;
        private int _sweepCount = 1;


        // 状态
        private int _currentChapter = 1;
        private DifficultyMode _currentDifficulty = DifficultyMode.Normal;
        private int _selectedLevel = -1;
        private List<Button> _levelButtons = new List<Button>();
        private List<Image> _starImages = new List<Image>();

        // ========== 生命周期 ==========

        protected override void OnOpen(object param)
        {
            BuildUI();
            RefreshChapter(_currentChapter);
        }

        protected override void OnShow()
        {
            RefreshChapter(_currentChapter);
            // 播放关卡选择BGM
            if (Framework.AudioManager.HasInstance)
                Framework.AudioManager.Instance.PlayBGM("Audio/BGM/bgm_level_select", 0.8f);
        }

        protected override void OnClose()
        {
        }

        // ========== UI构建 ==========

        private void BuildUI()
        {
            var root = GetComponent<RectTransform>();

            // 背景
            var bgObj = new GameObject("BG");
            bgObj.transform.SetParent(transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            UIStyleKit.CreateGradientPanel(bgRect,
                new Color(0.03f, 0.06f, 0.12f, 1f),
                new Color(0.06f, 0.10f, 0.18f, 1f));

            // 顶部栏
            BuildTopBar();

            // 章节选择区（横向滚动）
            BuildChapterSelector();

            // 关卡节点区
            BuildLevelNodes();

            // 难度选择
            BuildDifficultySelector();

            // 关卡详情弹窗（默认隐藏）
            BuildDetailPopup();
        }

        private void BuildTopBar()
        {
            var topBar = CreateRect("TopBar", transform);
            topBar.anchorMin = new Vector2(0, 0.92f);
            topBar.anchorMax = new Vector2(1, 1f);
            topBar.offsetMin = Vector2.zero;
            topBar.offsetMax = Vector2.zero;
            UIStyleKit.CreateStyledPanel(topBar, new Color(0.06f, 0.06f, 0.14f, 0.95f));

            // 返回按钮
            _btnBack = CreateButton(topBar, "BtnBack", "← 返回",
                new Vector2(0.01f, 0.1f), new Vector2(0.15f, 0.9f));
            _btnBack.onClick.AddListener(CloseSelf);
            UIStyleKit.StyleGrayButton(_btnBack);

            // 章节标题
            _txtChapterTitle = CreateText("ChapterTitle", topBar, "第1章 新手之路", 20,
                UIStyleKit.TextGold, TextAnchor.MiddleCenter);
            SetAnchors(_txtChapterTitle.rectTransform, 0.2f, 0.8f, 0f, 1f);
            UIStyleKit.AddTextShadow(_txtChapterTitle);
        }

        private void BuildChapterSelector()
        {
            // 章节横向滚动
            var scrollObj = new GameObject("ChapterScroll");
            scrollObj.transform.SetParent(transform, false);
            var scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 0.82f);
            scrollRect.anchorMax = new Vector2(1, 0.92f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            var scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0.05f, 0.05f, 0.12f, 0.8f);

            _chapterScroll = scrollObj.AddComponent<ScrollRect>();
            _chapterScroll.horizontal = true;
            _chapterScroll.vertical = false;

            // Viewport
            var viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(scrollObj.transform, false);
            var viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            var mask = viewportObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            viewportObj.AddComponent<Image>().color = Color.white;

            // Content
            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);
            _chapterContent = contentObj.AddComponent<RectTransform>();
            _chapterContent.anchorMin = new Vector2(0, 0);
            _chapterContent.anchorMax = new Vector2(0, 1);
            _chapterContent.pivot = new Vector2(0, 0.5f);
            _chapterContent.sizeDelta = new Vector2(ChaptersCount * 120f, 0);

            _chapterScroll.content = _chapterContent;
            _chapterScroll.viewport = viewportRect;

            // 创建章节按钮
            var playerData = PlayerDataManager.HasInstance ? PlayerDataManager.Instance.Data : null;
            int unlockedChapter = playerData?.UnlockedChapter ?? 1;

            for (int i = 1; i <= ChaptersCount; i++)
            {
                int chapter = i;
                var btnObj = new GameObject($"Chapter_{i}");
                btnObj.transform.SetParent(_chapterContent, false);
                var btnRect = btnObj.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0, 0.1f);
                btnRect.anchorMax = new Vector2(0, 0.9f);
                btnRect.pivot = new Vector2(0, 0.5f);
                btnRect.anchoredPosition = new Vector2((i - 1) * 120f + 10f, 0);
                btnRect.sizeDelta = new Vector2(110f, 0);

                var img = btnObj.AddComponent<Image>();
                bool unlocked = i <= unlockedChapter;
                img.color = unlocked
                    ? (i == _currentChapter ? UIStyleKit.BtnBlueNormal : new Color(0.15f, 0.18f, 0.28f, 0.9f))
                    : new Color(0.1f, 0.1f, 0.12f, 0.5f);

                var btn = btnObj.AddComponent<Button>();
                btn.interactable = unlocked;
                btn.onClick.AddListener(() => OnChapterClick(chapter));

                var txt = CreateText($"Txt_{i}", btnRect, $"第{i}章", 14,
                    unlocked ? UIStyleKit.TextWhite : UIStyleKit.TextGray, TextAnchor.MiddleCenter);
            }
        }

        private void BuildLevelNodes()
        {
            _levelNodesArea = CreateRect("LevelNodes", transform);
            _levelNodesArea.anchorMin = new Vector2(0.05f, 0.25f);
            _levelNodesArea.anchorMax = new Vector2(0.95f, 0.80f);
            _levelNodesArea.offsetMin = Vector2.zero;
            _levelNodesArea.offsetMax = Vector2.zero;

            // 5个关卡节点，蜿蜒排列
            Vector2[] nodePositions = {
                new Vector2(0.1f, 0.15f),
                new Vector2(0.3f, 0.55f),
                new Vector2(0.5f, 0.25f),
                new Vector2(0.7f, 0.65f),
                new Vector2(0.9f, 0.40f)
            };

            _levelButtons.Clear();
            _starImages.Clear();

            for (int i = 0; i < LevelsPerChapter; i++)
            {
                int levelIdx = i;
                var nodeObj = new GameObject($"Level_{i + 1}");
                nodeObj.transform.SetParent(_levelNodesArea, false);
                var nodeRect = nodeObj.AddComponent<RectTransform>();
                nodeRect.anchorMin = nodePositions[i] - new Vector2(0.06f, 0.08f);
                nodeRect.anchorMax = nodePositions[i] + new Vector2(0.06f, 0.08f);
                nodeRect.offsetMin = Vector2.zero;
                nodeRect.offsetMax = Vector2.zero;

                // 节点背景
                var nodeImg = nodeObj.AddComponent<Image>();
                UIStyleKit.CreateStyledPanel(nodeRect,
                    new Color(0.12f, 0.15f, 0.28f, 0.95f),
                    UIStyleKit.BorderGold, 8, 2);

                var btn = nodeObj.AddComponent<Button>();
                btn.onClick.AddListener(() => OnLevelClick(levelIdx));
                _levelButtons.Add(btn);

                // 关卡编号
                CreateText($"Num_{i}", nodeRect, $"{i + 1}", 24,
                    UIStyleKit.TextWhite, TextAnchor.MiddleCenter);

                // 星级显示（节点下方）
                var starObj = new GameObject($"Stars_{i}");
                starObj.transform.SetParent(_levelNodesArea, false);
                var starRect = starObj.AddComponent<RectTransform>();
                starRect.anchorMin = nodePositions[i] - new Vector2(0.05f, 0.12f);
                starRect.anchorMax = nodePositions[i] + new Vector2(0.05f, -0.06f);
                starRect.offsetMin = Vector2.zero;
                starRect.offsetMax = Vector2.zero;

                var starTxt = starObj.AddComponent<Text>();
                starTxt.text = "☆☆☆";
                starTxt.fontSize = 12;
                starTxt.color = UIStyleKit.TextGray;
                starTxt.alignment = TextAnchor.MiddleCenter;
                starTxt.font = Font.CreateDynamicFontFromOSFont("Arial", 12);

                // 连接线（除了第一个）
                if (i > 0)
                {
                    DrawConnectionLine(_levelNodesArea, nodePositions[i - 1], nodePositions[i]);
                }
            }
        }

        private void BuildDifficultySelector()
        {
            var diffArea = CreateRect("DifficultyArea", transform);
            diffArea.anchorMin = new Vector2(0.1f, 0.16f);
            diffArea.anchorMax = new Vector2(0.9f, 0.24f);
            diffArea.offsetMin = Vector2.zero;
            diffArea.offsetMax = Vector2.zero;

            _btnDiffNormal = CreateButton(diffArea, "BtnNormal", "普通",
                new Vector2(0f, 0.1f), new Vector2(0.32f, 0.9f));
            _btnDiffNormal.onClick.AddListener(() => SetDifficulty(DifficultyMode.Normal));
            UIStyleKit.StyleGreenButton(_btnDiffNormal);

            _btnDiffHard = CreateButton(diffArea, "BtnHard", "困难",
                new Vector2(0.34f, 0.1f), new Vector2(0.66f, 0.9f));
            _btnDiffHard.onClick.AddListener(() => SetDifficulty(DifficultyMode.Hard));
            UIStyleKit.StyleBlueButton(_btnDiffHard);

            _btnDiffNightmare = CreateButton(diffArea, "BtnNightmare", "噩梦",
                new Vector2(0.68f, 0.1f), new Vector2(1f, 0.9f));
            _btnDiffNightmare.onClick.AddListener(() => SetDifficulty(DifficultyMode.Nightmare));
            UIStyleKit.StyleRedButton(_btnDiffNightmare);
        }

        private void BuildDetailPopup()
        {
            _detailPopup = new GameObject("DetailPopup");
            _detailPopup.transform.SetParent(transform, false);
            var popRect = _detailPopup.AddComponent<RectTransform>();
            popRect.anchorMin = new Vector2(0.1f, 0.3f);
            popRect.anchorMax = new Vector2(0.9f, 0.75f);
            popRect.offsetMin = Vector2.zero;
            popRect.offsetMax = Vector2.zero;

            UIStyleKit.CreateStyledPanel(popRect,
                new Color(0.08f, 0.08f, 0.18f, 0.98f),
                UIStyleKit.BorderGold, 16, 3);

            // 标题
            _txtDetailTitle = CreateText("DetailTitle", popRect, "关卡 1-1", 22,
                UIStyleKit.TextGold, TextAnchor.MiddleCenter);
            SetAnchors(_txtDetailTitle.rectTransform, 0.1f, 0.9f, 0.78f, 0.95f);

            // 信息
            _txtDetailInfo = CreateText("DetailInfo", popRect, "波次: 8\n推荐战力: 100", 16,
                UIStyleKit.TextWhite, TextAnchor.MiddleLeft);
            SetAnchors(_txtDetailInfo.rectTransform, 0.1f, 0.9f, 0.45f, 0.75f);

            // 体力消耗
_txtDetailStamina = CreateText("StaminaCost", popRect, "!消耗: 6", 16,

                UIStyleKit.TextGreen, TextAnchor.MiddleCenter);
            SetAnchors(_txtDetailStamina.rectTransform, 0.1f, 0.9f, 0.32f, 0.45f);

            // 开始战斗按钮
            _btnStartBattle = CreateButton(popRect, "BtnStart", "⚔️ 开始战斗",
                new Vector2(0.1f, 0.05f), new Vector2(0.48f, 0.28f));
            _btnStartBattle.onClick.AddListener(OnStartBattle);
            UIStyleKit.StyleGreenButton(_btnStartBattle);

            // 扫荡按钮
            _btnSweep = CreateButton(popRect, "BtnSweep", "🔄 扫荡",
                new Vector2(0.52f, 0.05f), new Vector2(0.9f, 0.28f));
            _btnSweep.onClick.AddListener(OnSweep);
            UIStyleKit.StyleBlueButton(_btnSweep);

            // 关闭按钮
            _btnCloseDetail = CreateButton(popRect, "BtnClose", "✕",
                new Vector2(0.88f, 0.85f), new Vector2(0.98f, 0.98f));
            _btnCloseDetail.onClick.AddListener(() => _detailPopup.SetActive(false));
            UIStyleKit.StyleRedButton(_btnCloseDetail);

            _detailPopup.SetActive(false);
        }

        // ========== 逻辑 ==========

        private void OnChapterClick(int chapter)
        {
            _currentChapter = chapter;
            RefreshChapter(chapter);
        }

        private void OnLevelClick(int levelIndex)
        {
            _selectedLevel = levelIndex + 1;
            ShowDetailPopup(_currentChapter, _selectedLevel);
        }

        private void SetDifficulty(DifficultyMode mode)
        {
            _currentDifficulty = mode;
            RefreshChapter(_currentChapter);
        }

        private void ShowDetailPopup(int chapter, int level)
        {
            if (_detailPopup == null) return;

            string[] chapterNames = { "新手之路", "觉醒之森", "冰霜山脉" };
            string chapterName = chapter <= chapterNames.Length ? chapterNames[chapter - 1] : $"章节{chapter}";

            _txtDetailTitle.text = $"第{chapter}章 {chapterName} - 关卡{level}";

            int staminaCost = 6 + (chapter - 1);
            int waves = 8;
            int recommendPower = 100 + (chapter - 1) * 50 + (level - 1) * 10;

            _txtDetailInfo.text = $"波次数: {waves}\n推荐战力: {recommendPower}\n难度: {_currentDifficulty}";
_txtDetailStamina.text = $"!消耗: {staminaCost}";


            // 检查是否可以扫荡（3星通关）
            int stars = GetLevelStars(chapter, level);
            _btnSweep.interactable = stars >= 3;

            _detailPopup.SetActive(true);
        }

        private void OnStartBattle()
        {
            Debug.Log($"[LevelSelect] 开始战斗: 第{_currentChapter}章 关卡{_selectedLevel} 难度:{_currentDifficulty}");

            // 体力检查（测试模式：不扣体力）
            if (PlayerDataManager.HasInstance)
            {
                var data = PlayerDataManager.Instance.Data;
                if (data.Stamina < 100) data.Stamina = 99999; // 自动补满
                Debug.Log($"[LevelSelect] 测试模式：体力无限，当前={data.Stamina}");
            }

            // 关闭弹窗，进入战斗
            _detailPopup.SetActive(false);

            // 通过GameManager进入战斗（传递章节和关卡号）
            if (GameManager.HasInstance)
            {
                GameManager.Instance.EnterBattle(_currentChapter, _selectedLevel);
            }
        }

        private void OnSweep()
        {
            if (!SweepSystem.HasInstance)
            {
                Debug.LogWarning("[LevelSelect] SweepSystem未初始化");
                return;
            }

            if (!SweepSystem.Instance.CanSweep(_currentChapter, _selectedLevel))
            {
                Debug.LogWarning("[LevelSelect] 未达到3星，无法扫荡");
                return;
            }

            // 执行批量扫荡
            int maxTimes = SweepSystem.Instance.GetMaxSweepTimes(_currentChapter, _selectedLevel);
            int sweepTimes = Mathf.Min(_sweepCount, maxTimes);

            if (sweepTimes <= 0)
            {
                Debug.LogWarning("[LevelSelect] 体力不足，无法扫荡");
                return;
            }

            var results = SweepSystem.Instance.DoBatchSweep(_currentChapter, _selectedLevel, sweepTimes);
            if (results != null && results.Count > 0)
            {
                ShowSweepResult(results);
            }
        }

        private void ShowSweepResult(List<SweepResult> results)
        {
            if (_sweepResultPopup == null)
                BuildSweepResultPopup();

            // 汇总奖励
            int totalGold = 0;
            int totalDiamonds = 0;
            int totalExpBooks = 0;
            int totalHeroFrags = 0;
            int totalTowerFrags = 0;

            foreach (var result in results)
            {
                foreach (var reward in result.Rewards)
                {
                    switch (reward.RewardType)
                    {
                        case "gold": totalGold += reward.Amount; break;
                        case "diamonds": totalDiamonds += reward.Amount; break;
                        case "exp_book": totalExpBooks += reward.Amount; break;
                        case "hero_fragment": totalHeroFrags += reward.Amount; break;
                        case "tower_fragment": totalTowerFrags += reward.Amount; break;
                    }
                }
            }

            string resultText = $"扫荡完成 ×{results.Count}\n\n";
resultText += $"G 金币: +{totalGold}\n";
            if (totalDiamonds > 0) resultText += $"◇ 钻石: +{totalDiamonds}\n";
            if (totalExpBooks > 0) resultText += $"≡ 经验书: +{totalExpBooks}\n";
            if (totalHeroFrags > 0) resultText += $"# 英雄碎片: +{totalHeroFrags}\n";
            if (totalTowerFrags > 0) resultText += $"+ 塔碎片: +{totalTowerFrags}\n";

            int staminaCost = results.Count > 0 ? results[0].StaminaCost * results.Count : 0;
            resultText += $"\n! 消耗体力: {staminaCost}";


            _txtSweepResult.text = resultText;
            _sweepResultPopup.SetActive(true);

            // 刷新详情弹窗
            ShowDetailPopup(_currentChapter, _selectedLevel);
        }

        private void BuildSweepResultPopup()
        {
            _sweepResultPopup = new GameObject("SweepResultPopup");
            _sweepResultPopup.transform.SetParent(transform, false);
            var popRect = _sweepResultPopup.AddComponent<RectTransform>();
            popRect.anchorMin = new Vector2(0.15f, 0.25f);
            popRect.anchorMax = new Vector2(0.85f, 0.75f);
            popRect.offsetMin = Vector2.zero;
            popRect.offsetMax = Vector2.zero;

            UIStyleKit.CreateStyledPanel(popRect,
                new Color(0.06f, 0.08f, 0.16f, 0.98f),
                UIStyleKit.BorderGold, 16, 3);

            // 标题
            CreateText("SweepTitle", popRect, "🔄 扫荡结果", 20,
                UIStyleKit.TextGold, TextAnchor.MiddleCenter);
            var titleRect = popRect.GetChild(popRect.childCount - 1).GetComponent<RectTransform>();
            SetAnchors(titleRect, 0.1f, 0.9f, 0.85f, 0.98f);

            // 结果文本
            _txtSweepResult = CreateText("SweepResultText", popRect, "", 15,
                UIStyleKit.TextWhite, TextAnchor.UpperCenter);
            SetAnchors(_txtSweepResult.rectTransform, 0.08f, 0.92f, 0.20f, 0.82f);

            // 关闭按钮
            _btnSweepClose = CreateButton(popRect, "BtnSweepClose", "✓ 确认",
                new Vector2(0.25f, 0.04f), new Vector2(0.75f, 0.18f));
            _btnSweepClose.onClick.AddListener(() => _sweepResultPopup.SetActive(false));
            UIStyleKit.StyleGreenButton(_btnSweepClose);

            _sweepResultPopup.SetActive(false);
        }


        private void RefreshChapter(int chapter)
        {
            string[] chapterNames = { "新手之路", "觉醒之森", "冰霜山脉" };
            string name = chapter <= chapterNames.Length ? chapterNames[chapter - 1] : $"未知领域";
            _txtChapterTitle.text = $"第{chapter}章 {name}";

            // 刷新关卡节点状态
            if (!PlayerDataManager.HasInstance) return;
            var data = PlayerDataManager.Instance.Data;

            for (int i = 0; i < _levelButtons.Count && i < LevelsPerChapter; i++)
            {
                int level = i + 1;
                bool unlocked = (chapter < data.UnlockedChapter) ||
                                (chapter == data.UnlockedChapter && level <= data.UnlockedLevel);

                _levelButtons[i].interactable = unlocked;

                var img = _levelButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = unlocked
                        ? new Color(0.12f, 0.15f, 0.28f, 0.95f)
                        : new Color(0.08f, 0.08f, 0.10f, 0.5f);
                }
            }
        }

        private int GetLevelStars(int chapter, int level)
        {
            if (!PlayerDataManager.HasInstance) return 0;
            var data = PlayerDataManager.Instance.Data;
            if (data.LevelStars == null) return 0;

            for (int i = 0; i < data.LevelStars.Count; i++)
            {
                var ls = data.LevelStars[i];
                if (ls.Chapter == chapter && ls.Level == level)
                    return ls.Stars;
            }
            return 0;
        }

        private void DrawConnectionLine(RectTransform parent, Vector2 from, Vector2 to)
        {
            var lineObj = new GameObject("Line");
            lineObj.transform.SetParent(parent, false);
            var lineRect = lineObj.AddComponent<RectTransform>();

            Vector2 mid = (from + to) * 0.5f;
            lineRect.anchorMin = new Vector2(Mathf.Min(from.x, to.x), Mathf.Min(from.y, to.y));
            lineRect.anchorMax = new Vector2(Mathf.Max(from.x, to.x), Mathf.Max(from.y, to.y));
            lineRect.offsetMin = Vector2.zero;
            lineRect.offsetMax = Vector2.zero;

            var img = lineObj.AddComponent<Image>();
            img.color = new Color(0.4f, 0.5f, 0.7f, 0.3f);
            img.raycastTarget = false;
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
            return txt;
        }

        private void SetAnchors(RectTransform rect, float xMin, float xMax, float yMin, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private Button CreateButton(RectTransform parent, string name, string label,
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
            var btn = obj.AddComponent<Button>();

            var txtObj = new GameObject("Label");
            txtObj.transform.SetParent(obj.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            var txt = txtObj.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = 16;
            txt.color = UIStyleKit.TextWhite;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            txt.raycastTarget = false;

            return btn;
        }
    }
}
