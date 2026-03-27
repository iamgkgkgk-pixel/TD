// ============================================================
// 文件名：CheckInPanel.cs
// 功能描述：签到面板UI — 7日签到展示、签到按钮、连续签到奖励
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #260
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Visual;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>
    /// 签到面板
    /// </summary>
    public class CheckInPanel : BasePanel
    {
        public override UILayer Layer => UILayer.Popup;
        public override bool IsCached => true;

        private Text _txtConsecutive;
        private Button _btnCheckIn;
        private RectTransform _dayGrid;

        protected override void OnOpen(object param)
        {
            BuildUI();
            RefreshInfo();
        }

        protected override void OnShow() { RefreshInfo(); }

        private void BuildUI()
        {
            // 半透明背景
            var bgOverlay = PanelHelper.CreateFullRect("BgOverlay", transform);
            bgOverlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.6f);

            // 面板
            var panel = PanelHelper.CreateAnchoredRect("Panel", transform, 0.08f, 0.25f, 0.92f, 0.75f);
            UIStyleKit.CreateStyledPanel(panel, UIStyleKit.PanelBgDark, UIStyleKit.BorderGold, 16, 3);

            // 标题
            PanelHelper.CreateTxt(panel, "📅 每日签到", 22, UIStyleKit.TextGold, 0.1f, 0.88f, 0.9f, 0.98f);

            // 连续签到天数
            _txtConsecutive = PanelHelper.CreateTxt(panel, "连续签到: 0天", 16,
                UIStyleKit.TextWhite, 0.1f, 0.78f, 0.9f, 0.88f);

            // 7日签到格子
            _dayGrid = PanelHelper.CreateAnchoredRect("DayGrid", panel, 0.05f, 0.30f, 0.95f, 0.76f);

            float cellW = 1f / 4f;
            float cellH = 1f / 2f;

            for (int i = 0; i < 7; i++)
            {
                int col = i % 4;
                int row = i / 4;

                var cellRect = PanelHelper.CreateAnchoredRect($"Day_{i + 1}", _dayGrid,
                    col * cellW + 0.01f, 1f - (row + 1) * cellH + 0.01f,
                    (col + 1) * cellW - 0.01f, 1f - row * cellH - 0.01f);

                UIStyleKit.CreateStyledPanel(cellRect,
                    new Color(0.10f, 0.10f, 0.22f, 0.9f), UIStyleKit.BorderSilver, 8, 1);

                // 天数标签
                PanelHelper.CreateTxt(cellRect, $"第{i + 1}天", 13,
                    UIStyleKit.TextWhite, 0.05f, 0.65f, 0.95f, 0.95f);

                // 奖励信息
                var (rewardType, rewardAmount) = CheckInSystem.Instance.GetRewardForDay(i + 1);
string rewardIcon = rewardType == "gold" ? "G" :
                    rewardType == "diamonds" ? "◇" : "#";

                PanelHelper.CreateTxt(cellRect, $"{rewardIcon}×{rewardAmount}", 14,
                    UIStyleKit.TextGold, 0.05f, 0.25f, 0.95f, 0.65f);

                // 已签到标记（后续RefreshInfo中更新）
                PanelHelper.CreateTxt(cellRect, "", 12,
                    UIStyleKit.TextGreen, 0.05f, 0.02f, 0.95f, 0.25f);
            }

            // 签到按钮
_btnCheckIn = PanelHelper.CreateBtn(panel, "√ 签到", 0.2f, 0.08f, 0.55f, 0.24f);

            _btnCheckIn.onClick.AddListener(OnCheckIn);
            UIStyleKit.StyleGreenButton(_btnCheckIn);

            // 关闭按钮
            var btnClose = PanelHelper.CreateBtn(panel, "✕ 关闭", 0.6f, 0.08f, 0.85f, 0.24f);
            btnClose.onClick.AddListener(CloseSelf);
            UIStyleKit.StyleGrayButton(btnClose);
        }

        private void RefreshInfo()
        {
            if (!CheckInSystem.HasInstance) return;

            int consecutive = CheckInSystem.Instance.GetConsecutiveDays();
            bool checkedToday = CheckInSystem.Instance.HasCheckedInToday();

            _txtConsecutive.text = $"连续签到: {consecutive}天";

            // 更新签到按钮状态
            if (checkedToday)
            {
                _btnCheckIn.interactable = false;
                UIStyleKit.StyleGrayButton(_btnCheckIn);
                // 更新按钮文字
                var txt = _btnCheckIn.GetComponentInChildren<Text>();
if (txt != null) txt.text = "√ 已签到";

            }
            else
            {
                _btnCheckIn.interactable = true;
                UIStyleKit.StyleGreenButton(_btnCheckIn);
            }

            // 更新每日格子的已签到标记
            int currentDay = consecutive % 7;
            for (int i = 0; i < 7; i++)
            {
                var dayCell = _dayGrid.Find($"Day_{i + 1}");
                if (dayCell == null) continue;

                // 找到状态文字（第3个Text子对象）
                var texts = dayCell.GetComponentsInChildren<Text>();
                if (texts.Length >= 3)
                {
                    if (i < consecutive % 7 || (checkedToday && i == (consecutive - 1) % 7))
                    {
texts[2].text = "√ 已领";

                        texts[2].color = UIStyleKit.TextGreen;
                    }
                    else
                    {
                        texts[2].text = "";
                    }
                }
            }
        }

        private void OnCheckIn()
        {
            if (CheckInSystem.Instance.DoCheckIn())
            {
                RefreshInfo();
            }
        }
    }

    /// <summary>
    /// 成就面板
    /// </summary>
    public class AchievementPanel : BasePanel
    {
        public override UILayer Layer => UILayer.Normal;
        public override bool IsCached => true;

        private RectTransform _achievementListArea;

        protected override void OnOpen(object param) { BuildUI(); }
        protected override void OnShow() { RefreshAchievements(); }

        private void BuildUI()
        {
            var bg = PanelHelper.CreateFullRect("BG", transform);
            UIStyleKit.CreateGradientPanel(bg,
                new Color(0.04f, 0.04f, 0.10f, 1f),
                new Color(0.06f, 0.08f, 0.16f, 1f));

            var topBar = PanelHelper.CreateAnchoredRect("TopBar", transform, 0, 0.93f, 1, 1);
            UIStyleKit.CreateStyledPanel(topBar, new Color(0.06f, 0.06f, 0.14f, 0.95f));
            var btnBack = PanelHelper.CreateBtn(topBar, "← 返回", 0.01f, 0.1f, 0.15f, 0.9f);
            btnBack.onClick.AddListener(CloseSelf);
            UIStyleKit.StyleGrayButton(btnBack);
PanelHelper.CreateTxt(topBar, "☆ 成就", 20, UIStyleKit.TextGold, 0.3f, 0f, 0.7f, 1f);


            _achievementListArea = PanelHelper.CreateAnchoredRect("AchList", transform, 0.03f, 0.05f, 0.97f, 0.92f);
        }

        private void RefreshAchievements()
        {
            if (!AchievementSystem.HasInstance) return;

            for (int i = _achievementListArea.childCount - 1; i >= 0; i--)
                Destroy(_achievementListArea.GetChild(i).gameObject);

            var achievements = AchievementSystem.Instance.GetAllAchievements();
            float itemH = 0.10f;

            for (int i = 0; i < achievements.Count; i++)
            {
                var config = achievements[i];
                float yMax = 1f - i * itemH;
                float yMin = yMax - itemH + 0.005f;
                if (yMin < 0) break;

                bool unlocked = AchievementSystem.Instance.IsUnlocked(config.AchievementId);

                var itemRect = PanelHelper.CreateAnchoredRect($"Ach_{i}", _achievementListArea,
                    0.01f, yMin, 0.99f, yMax - 0.005f);

                Color bgColor = unlocked
                    ? new Color(0.12f, 0.20f, 0.15f, 0.85f)
                    : new Color(0.08f, 0.08f, 0.15f, 0.7f);
                UIStyleKit.CreateStyledPanel(itemRect, bgColor, UIStyleKit.BorderSilver, 6, 1);

string statusIcon = unlocked ? "☆" : "■";

                PanelHelper.CreateTxt(itemRect, $"{statusIcon} {config.Name}", 16,
                    unlocked ? UIStyleKit.TextGold : UIStyleKit.TextWhite,
                    0.02f, 0.5f, 0.65f, 0.95f);

                PanelHelper.CreateTxt(itemRect, config.Description, 12,
                    UIStyleKit.TextGray, 0.02f, 0.05f, 0.65f, 0.5f);

string rewardStr = config.RewardType == "gold" ? $"G{config.RewardAmount}" :
                    config.RewardType == "diamonds" ? $"◇{config.RewardAmount}" : "";

                PanelHelper.CreateTxt(itemRect, rewardStr, 14,
                    UIStyleKit.TextGold, 0.68f, 0.3f, 0.98f, 0.7f);
            }
        }
    }
}
