// ============================================================
// 文件名：MetaGamePanels.cs
// 功能描述：元游戏所有UI面板集合 — 抽卡/战令/任务/邮件/排行/设置/社交/反馈
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #262,257,258-260,267-268,263-265,248
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Visual;
using AetheraSurvivors.Data;

namespace AetheraSurvivors.MetaGame
{
    // ================================================================
    // 抽卡面板
    // ================================================================
    public class GachaPanel : BasePanel
    {
        public override UILayer Layer => UILayer.Normal;
        public override bool IsCached => true;

        private Text _txtPityInfo;
        private Text _txtRateInfo;
        private Text _txtDiamonds;
        private RectTransform _resultArea;
        private List<GameObject> _resultItems = new List<GameObject>();

        protected override void OnOpen(object param)
        {
            BuildUI();
            RefreshInfo();
        }

        protected override void OnShow() { RefreshInfo(); }

        private void BuildUI()
        {
            // 背景
            var bg = CreateFullRect("BG", transform);
            UIStyleKit.CreateGradientPanel(bg,
                new Color(0.05f, 0.02f, 0.12f, 1f),
                new Color(0.10f, 0.05f, 0.22f, 1f));

            // 顶部
            var topBar = CreateAnchoredRect("TopBar", transform, 0, 0.93f, 1, 1);
            UIStyleKit.CreateStyledPanel(topBar, new Color(0.06f, 0.06f, 0.14f, 0.95f));
            var btnBack = CreateBtn(topBar, "← 返回", 0.01f, 0.1f, 0.15f, 0.9f);
            btnBack.onClick.AddListener(CloseSelf);
            UIStyleKit.StyleGrayButton(btnBack);
            CreateTxt(topBar, "🎰 召唤", 20, UIStyleKit.TextGold, 0.3f, 0f, 0.7f, 1f);

            // 钻石显示
_txtDiamonds = CreateTxt(topBar, "◇ 0", 16, new Color(0.4f, 0.7f, 1f), 0.72f, 0.2f, 0.98f, 0.8f);


            // 概率公示
            _txtRateInfo = CreateTxt(CreateAnchoredRect("RateArea", transform, 0.05f, 0.72f, 0.95f, 0.92f),
                "", 13, UIStyleKit.TextGray, 0f, 0f, 1f, 1f);
            _txtRateInfo.alignment = TextAnchor.UpperLeft;

            // 保底信息
            _txtPityInfo = CreateTxt(CreateAnchoredRect("PityArea", transform, 0.05f, 0.65f, 0.95f, 0.72f),
                "", 16, UIStyleKit.TextGold, 0f, 0f, 1f, 1f);

            // 结果展示区
            _resultArea = CreateAnchoredRect("ResultArea", transform, 0.05f, 0.22f, 0.95f, 0.64f);
            UIStyleKit.CreateStyledPanel(_resultArea,
                new Color(0.06f, 0.06f, 0.14f, 0.7f), UIStyleKit.BorderSilver, 10, 1);

            // 按钮
            var btnSingle = CreateBtn(CreateAnchoredRect("BtnArea", transform, 0.05f, 0.05f, 0.95f, 0.20f),
$"单抽 ◇{GachaSystem.SingleCostDiamond}", 0.02f, 0.1f, 0.48f, 0.9f);

            btnSingle.onClick.AddListener(OnSinglePull);
            UIStyleKit.StyleBlueButton(btnSingle);

            var btnTen = CreateBtn(btnSingle.transform.parent.GetComponent<RectTransform>(),
$"十连 ◇{GachaSystem.TenCostDiamond}", 0.52f, 0.1f, 0.98f, 0.9f);

            btnTen.onClick.AddListener(OnTenPull);
            UIStyleKit.StyleGreenButton(btnTen);
        }

        private void RefreshInfo()
        {
            if (!GachaSystem.HasInstance) return;

            _txtRateInfo.text = GachaSystem.Instance.GetRateDisplayText();
            _txtPityInfo.text = $"距离SSR保底: {GachaSystem.Instance.GetPityRemaining()}次 | 总抽卡: {GachaSystem.Instance.GetTotalPulls()}次";

            if (PlayerDataManager.HasInstance)
_txtDiamonds.text = $"◇ {PlayerDataManager.Instance.Data.Diamonds}";

        }

        private void OnSinglePull()
        {
            var result = GachaSystem.Instance.PullSingle();
            if (result != null)
            {
                ShowResults(new List<GachaResult> { result });
                RefreshInfo();
            }
        }

        private void OnTenPull()
        {
            var results = GachaSystem.Instance.PullTen();
            if (results != null)
            {
                ShowResults(results);
                RefreshInfo();
            }
        }

        private void ShowResults(List<GachaResult> results)
        {
            foreach (var item in _resultItems) if (item != null) Destroy(item);
            _resultItems.Clear();

            int cols = 5;
            float itemW = 1f / cols;
            float itemH = results.Count > 5 ? 0.48f : 0.9f;

            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var config = HeroConfigTable.GetHero(r.HeroId);
                if (config == null) continue;

                int col = i % cols;
                int row = i / cols;

                var itemObj = new GameObject($"Result_{i}");
                itemObj.transform.SetParent(_resultArea, false);
                var itemRect = itemObj.AddComponent<RectTransform>();
                itemRect.anchorMin = new Vector2(col * itemW + 0.01f, 1f - (row + 1) * itemH);
                itemRect.anchorMax = new Vector2((col + 1) * itemW - 0.01f, 1f - row * itemH - 0.02f);
                itemRect.offsetMin = Vector2.zero;
                itemRect.offsetMax = Vector2.zero;

                Color bgColor = r.Rarity == HeroRarity.SSR ? new Color(0.4f, 0.3f, 0.1f, 0.9f) :
                    r.Rarity == HeroRarity.SR ? new Color(0.25f, 0.15f, 0.4f, 0.9f) :
                    new Color(0.15f, 0.18f, 0.28f, 0.9f);
                UIStyleKit.CreateStyledPanel(itemRect, bgColor, UIStyleKit.BorderGold, 6, 1);

                string label = $"{config.Icon}\n{config.Name}";
if (r.IsNew) label += "\nNEW";
                else label += $"\n#×{r.FragmentCount}";


                CreateTxt(itemRect, label, 11, UIStyleKit.TextWhite, 0.05f, 0.05f, 0.95f, 0.95f);

                _resultItems.Add(itemObj);
            }
        }

        // 工具方法
        private RectTransform CreateFullRect(string name, Transform parent)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            return r;
        }
        private RectTransform CreateAnchoredRect(string name, Transform parent, float xMin, float yMin, float xMax, float yMax)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(xMin, yMin); r.anchorMax = new Vector2(xMax, yMax);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            return r;
        }
        private Text CreateTxt(RectTransform parent, string text, int size, Color color, float xMin, float yMin, float xMax, float yMax)
        {
            var obj = new GameObject("Txt"); obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(xMin, yMin); r.anchorMax = new Vector2(xMax, yMax);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var t = obj.AddComponent<Text>();
            t.text = text; t.fontSize = size; t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.font = Font.CreateDynamicFontFromOSFont("Arial", size);
            t.raycastTarget = false;
            return t;
        }
        private Button CreateBtn(RectTransform parent, string label, float xMin, float yMin, float xMax, float yMax)
        {
            var obj = new GameObject("Btn"); obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(xMin, yMin); r.anchorMax = new Vector2(xMax, yMax);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            obj.AddComponent<Image>();
            var btn = obj.AddComponent<Button>();
            CreateTxt(r, label, 14, UIStyleKit.TextWhite, 0, 0, 1, 1);
            return btn;
        }
    }

    // ================================================================
    // 战令面板
    // ================================================================
    public class BattlePassPanel : BasePanel
    {
        public override UILayer Layer => UILayer.Normal;
        public override bool IsCached => true;

        private Text _txtLevel;
        private Text _txtExp;
        private Text _txtSeason;
        private ScrollRect _rewardScroll;
        private RectTransform _rewardContent;

        protected override void OnOpen(object param)
        {
            BuildUI();
            RefreshInfo();
        }

        protected override void OnShow() { RefreshInfo(); }

        private void BuildUI()
        {
            var bg = PanelHelper.CreateFullRect("BG", transform);
            UIStyleKit.CreateGradientPanel(bg, new Color(0.04f, 0.04f, 0.10f, 1f), new Color(0.08f, 0.06f, 0.16f, 1f));

            var topBar = PanelHelper.CreateAnchoredRect("TopBar", transform, 0, 0.93f, 1, 1);
            UIStyleKit.CreateStyledPanel(topBar, new Color(0.06f, 0.06f, 0.14f, 0.95f));
            var btnBack = PanelHelper.CreateBtn(topBar, "← 返回", 0.01f, 0.1f, 0.15f, 0.9f);
            btnBack.onClick.AddListener(CloseSelf);
            UIStyleKit.StyleGrayButton(btnBack);
PanelHelper.CreateTxt(topBar, "T 战令", 20, UIStyleKit.TextGold, 0.3f, 0f, 0.7f, 1f);


            // 等级和经验
            var infoArea = PanelHelper.CreateAnchoredRect("Info", transform, 0.05f, 0.82f, 0.95f, 0.92f);
            _txtLevel = PanelHelper.CreateTxt(infoArea, "Lv.0", 22, UIStyleKit.TextGold, 0f, 0f, 0.3f, 1f);
            _txtExp = PanelHelper.CreateTxt(infoArea, "0/100", 16, UIStyleKit.TextWhite, 0.3f, 0f, 0.6f, 1f);
            _txtSeason = PanelHelper.CreateTxt(infoArea, "赛季剩余: 30天", 14, UIStyleKit.TextGray, 0.6f, 0f, 1f, 1f);

            // 购买付费战令按钮
            var btnPremium = PanelHelper.CreateBtn(
                PanelHelper.CreateAnchoredRect("PremiumArea", transform, 0.05f, 0.74f, 0.95f, 0.82f),
                $"🔓 解锁付费战令 ¥{BattlePassSystem.PremiumPrice}", 0.1f, 0.1f, 0.9f, 0.9f);
            btnPremium.onClick.AddListener(OnPurchasePremium);
            UIStyleKit.StyleGreenButton(btnPremium);

            // 一键领取
            var btnClaimAll = PanelHelper.CreateBtn(
                PanelHelper.CreateAnchoredRect("ClaimArea", transform, 0.05f, 0.66f, 0.95f, 0.74f),
                "📦 一键领取", 0.3f, 0.1f, 0.7f, 0.9f);
            btnClaimAll.onClick.AddListener(OnClaimAll);
            UIStyleKit.StyleBlueButton(btnClaimAll);

            // 奖励滚动列表
            BuildRewardList();
        }

        private void BuildRewardList()
        {
            var scrollObj = new GameObject("RewardScroll");
            scrollObj.transform.SetParent(transform, false);
            var scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.02f, 0.05f);
            scrollRect.anchorMax = new Vector2(0.98f, 0.65f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            scrollObj.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.12f, 0.6f);
            _rewardScroll = scrollObj.AddComponent<ScrollRect>();
            _rewardScroll.horizontal = true;
            _rewardScroll.vertical = false;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            var vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero; vpRect.offsetMax = Vector2.zero;
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            viewport.AddComponent<Image>().color = Color.white;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            _rewardContent = content.AddComponent<RectTransform>();
            _rewardContent.anchorMin = new Vector2(0, 0);
            _rewardContent.anchorMax = new Vector2(0, 1);
            _rewardContent.pivot = new Vector2(0, 0.5f);
            _rewardContent.sizeDelta = new Vector2(BattlePassSystem.MaxLevel * 100f, 0);

            _rewardScroll.content = _rewardContent;
            _rewardScroll.viewport = vpRect;
        }

        private void RefreshInfo()
        {
            if (!BattlePassSystem.HasInstance) return;

            int level = BattlePassSystem.Instance.GetCurrentLevel();
            long exp = BattlePassSystem.Instance.GetCurrentExp();
            bool premium = BattlePassSystem.Instance.IsPremium();

            _txtLevel.text = $"Lv.{level}";
            _txtExp.text = $"{exp}/{BattlePassSystem.ExpPerLevel}";
            _txtSeason.text = $"赛季剩余: {BattlePassSystem.Instance.GetRemainingDays()}天";

            // 刷新奖励列表
            RefreshRewardItems(level, premium);
        }

        private void RefreshRewardItems(int currentLevel, bool isPremium)
        {
            // 清除旧内容
            for (int i = _rewardContent.childCount - 1; i >= 0; i--)
                Destroy(_rewardContent.GetChild(i).gameObject);

            var rewards = BattlePassSystem.Instance.GetAllRewards();
            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                var itemObj = new GameObject($"Reward_{reward.Level}");
                itemObj.transform.SetParent(_rewardContent, false);
                var itemRect = itemObj.AddComponent<RectTransform>();
                itemRect.anchorMin = new Vector2(0, 0.02f);
                itemRect.anchorMax = new Vector2(0, 0.98f);
                itemRect.pivot = new Vector2(0, 0.5f);
                itemRect.anchoredPosition = new Vector2(i * 100f + 5f, 0);
                itemRect.sizeDelta = new Vector2(90f, 0);

                bool reached = reward.Level <= currentLevel;
                bool freeClaimed = BattlePassSystem.Instance.IsFreeRewardClaimed(reward.Level);
                bool premiumClaimed = BattlePassSystem.Instance.IsPremiumRewardClaimed(reward.Level);

                Color bgColor = reached ? new Color(0.12f, 0.20f, 0.15f, 0.9f) : new Color(0.10f, 0.10f, 0.15f, 0.7f);
                UIStyleKit.CreateStyledPanel(itemRect, bgColor, UIStyleKit.BorderSilver, 6, 1);

                // 等级标签
                PanelHelper.CreateTxt(itemRect, $"Lv.{reward.Level}", 12, UIStyleKit.TextGold, 0.05f, 0.82f, 0.95f, 0.98f);

                // 免费奖励
string freeLabel = $"F{reward.FreeRewardType}\n×{reward.FreeRewardAmount}";

if (freeClaimed) freeLabel = "√ 已领";

                PanelHelper.CreateTxt(itemRect, freeLabel, 10, UIStyleKit.TextWhite, 0.05f, 0.42f, 0.95f, 0.80f);

                // 付费奖励
                string premLabel = isPremium
? (premiumClaimed ? "√ 已领" : $"◇{reward.PremiumRewardType}\n×{reward.PremiumRewardAmount}")
                    : "■ 付费";

                PanelHelper.CreateTxt(itemRect, premLabel, 10,
                    isPremium ? UIStyleKit.TextGold : UIStyleKit.TextGray,
                    0.05f, 0.02f, 0.95f, 0.40f);
            }
        }

        private void OnPurchasePremium()
        {
            if (BattlePassSystem.Instance.PurchasePremium())
                RefreshInfo();
        }

        private void OnClaimAll()
        {
            int claimed = BattlePassSystem.Instance.ClaimAllAvailable();
            Debug.Log($"[BattlePass] 一键领取: {claimed}个奖励");
            RefreshInfo();
        }
    }

    // ================================================================
    // 任务面板
    // ================================================================
    public class QuestPanel : BasePanel
    {
        public override UILayer Layer => UILayer.Normal;
        public override bool IsCached => true;

        protected override void OnOpen(object param) { BuildUI(); }
        protected override void OnShow() { RefreshQuests(); }

        private RectTransform _questListArea;
        private Text _txtActivity;

        private void BuildUI()
        {
            var bg = PanelHelper.CreateFullRect("BG", transform);
            UIStyleKit.CreateGradientPanel(bg, new Color(0.04f, 0.04f, 0.10f, 1f), new Color(0.06f, 0.08f, 0.16f, 1f));

            var topBar = PanelHelper.CreateAnchoredRect("TopBar", transform, 0, 0.93f, 1, 1);
            UIStyleKit.CreateStyledPanel(topBar, new Color(0.06f, 0.06f, 0.14f, 0.95f));
            var btnBack = PanelHelper.CreateBtn(topBar, "← 返回", 0.01f, 0.1f, 0.15f, 0.9f);
            btnBack.onClick.AddListener(CloseSelf);
            UIStyleKit.StyleGrayButton(btnBack);
            PanelHelper.CreateTxt(topBar, "📋 每日任务", 20, UIStyleKit.TextGold, 0.3f, 0f, 0.7f, 1f);

            // 活跃度
            _txtActivity = PanelHelper.CreateTxt(
                PanelHelper.CreateAnchoredRect("Activity", transform, 0.05f, 0.85f, 0.95f, 0.92f),
                "活跃度: 0/100", 18, UIStyleKit.TextGold, 0f, 0f, 1f, 1f);

            _questListArea = PanelHelper.CreateAnchoredRect("QuestList", transform, 0.03f, 0.05f, 0.97f, 0.84f);
        }

        private void RefreshQuests()
        {
            if (!DailyQuestSystem.HasInstance) return;

            // 清除旧内容
            for (int i = _questListArea.childCount - 1; i >= 0; i--)
                Destroy(_questListArea.GetChild(i).gameObject);

            var quests = DailyQuestSystem.Instance.GetDailyQuests();
            int activity = DailyQuestSystem.Instance.GetActivityPoints();
            _txtActivity.text = $"活跃度: {activity}/100";

            float itemH = 1f / Mathf.Max(quests.Count, 1);
            for (int i = 0; i < quests.Count; i++)
            {
                var config = quests[i];
                float yMin = 1f - (i + 1) * itemH + 0.005f;
                float yMax = 1f - i * itemH - 0.005f;

                var itemRect = PanelHelper.CreateAnchoredRect($"Quest_{i}", _questListArea, 0.01f, yMin, 0.99f, yMax);
                UIStyleKit.CreateStyledPanel(itemRect,
                    new Color(0.08f, 0.08f, 0.18f, 0.85f), UIStyleKit.BorderSilver, 8, 1);

                PanelHelper.CreateTxt(itemRect, config.Name, 16, UIStyleKit.TextWhite, 0.02f, 0.5f, 0.5f, 0.95f);
                PanelHelper.CreateTxt(itemRect, config.Description, 12, UIStyleKit.TextGray, 0.02f, 0.05f, 0.5f, 0.5f);

string rewardStr = config.RewardType == "gold" ? $"G{config.RewardAmount}" :
                    config.RewardType == "diamonds" ? $"◇{config.RewardAmount}" : $"{config.RewardAmount}";

                PanelHelper.CreateTxt(itemRect, rewardStr, 14, UIStyleKit.TextGold, 0.55f, 0.3f, 0.75f, 0.7f);

                string qid = config.QuestId;
                var btn = PanelHelper.CreateBtn(itemRect, "领取", 0.78f, 0.15f, 0.98f, 0.85f);
                btn.onClick.AddListener(() =>
                {
                    DailyQuestSystem.Instance.ClaimReward(qid);
                    RefreshQuests();
                });
                UIStyleKit.StyleGreenButton(btn);
            }
        }
    }

    // ================================================================
    // 邮件面板
    // ================================================================
    public class MailPanel : BasePanel
    {
        public override UILayer Layer => UILayer.Normal;
        public override bool IsCached => true;

        private RectTransform _mailListArea;

        protected override void OnOpen(object param) { BuildUI(); }
        protected override void OnShow() { RefreshMails(); }

        private void BuildUI()
        {
            var bg = PanelHelper.CreateFullRect("BG", transform);
            UIStyleKit.CreateGradientPanel(bg, new Color(0.04f, 0.04f, 0.10f, 1f), new Color(0.06f, 0.08f, 0.16f, 1f));

            var topBar = PanelHelper.CreateAnchoredRect("TopBar", transform, 0, 0.93f, 1, 1);
            UIStyleKit.CreateStyledPanel(topBar, new Color(0.06f, 0.06f, 0.14f, 0.95f));
            var btnBack = PanelHelper.CreateBtn(topBar, "← 返回", 0.01f, 0.1f, 0.15f, 0.9f);
            btnBack.onClick.AddListener(CloseSelf);
            UIStyleKit.StyleGrayButton(btnBack);
            PanelHelper.CreateTxt(topBar, "📬 邮件", 20, UIStyleKit.TextGold, 0.3f, 0f, 0.7f, 1f);

            // 一键领取
            var btnClaimAll = PanelHelper.CreateBtn(
                PanelHelper.CreateAnchoredRect("ClaimArea", transform, 0.05f, 0.86f, 0.95f, 0.92f),
                "📦 一键领取所有附件", 0.25f, 0.1f, 0.75f, 0.9f);
            btnClaimAll.onClick.AddListener(() =>
            {
                if (MailSystem.HasInstance)
                {
                    MailSystem.Instance.ClaimAllAttachments();
                    RefreshMails();
                }
            });
            UIStyleKit.StyleBlueButton(btnClaimAll);

            _mailListArea = PanelHelper.CreateAnchoredRect("MailList", transform, 0.03f, 0.05f, 0.97f, 0.85f);
        }

        private void RefreshMails()
        {
            if (!MailSystem.HasInstance) return;

            for (int i = _mailListArea.childCount - 1; i >= 0; i--)
                Destroy(_mailListArea.GetChild(i).gameObject);

            var mails = MailSystem.Instance.GetAllMails();
            float itemH = 0.12f;

            for (int i = 0; i < mails.Count; i++)
            {
                var mail = mails[i];
                float yMax = 1f - i * itemH;
                float yMin = yMax - itemH + 0.005f;
                if (yMin < 0) break;

                var itemRect = PanelHelper.CreateAnchoredRect($"Mail_{i}", _mailListArea, 0.01f, yMin, 0.99f, yMax - 0.005f);

                Color bgColor = mail.IsRead
                    ? new Color(0.06f, 0.06f, 0.12f, 0.7f)
                    : new Color(0.10f, 0.10f, 0.22f, 0.9f);
                UIStyleKit.CreateStyledPanel(itemRect, bgColor, UIStyleKit.BorderSilver, 6, 1);

                string readMark = mail.IsRead ? "" : "🔴 ";
                PanelHelper.CreateTxt(itemRect, $"{readMark}{mail.Title}", 15, UIStyleKit.TextWhite, 0.02f, 0.5f, 0.7f, 0.95f);
                PanelHelper.CreateTxt(itemRect, mail.SenderName, 12, UIStyleKit.TextGray, 0.02f, 0.05f, 0.4f, 0.5f);

                if (!string.IsNullOrEmpty(mail.AttachmentType) && !mail.AttachmentClaimed)
                {
                    string mid = mail.MailId;
                    var btn = PanelHelper.CreateBtn(itemRect, "📎 领取", 0.75f, 0.15f, 0.98f, 0.85f);
                    btn.onClick.AddListener(() =>
                    {
                        MailSystem.Instance.ClaimAttachment(mid);
                        RefreshMails();
                    });
                    UIStyleKit.StyleGreenButton(btn);
                }
                else if (mail.AttachmentClaimed)
                {
PanelHelper.CreateTxt(itemRect, "√已领", 12, UIStyleKit.TextGray, 0.78f, 0.3f, 0.98f, 0.7f);

                }
            }
        }
    }

    // ================================================================
    // 排行面板
    // ================================================================
    public class RankPanel : BasePanel
    {
        public override UILayer Layer => UILayer.Normal;
        public override bool IsCached => true;

        private RectTransform _rankListArea;

        protected override void OnOpen(object param) { BuildUI(); }
        protected override void OnShow() { RefreshRank(); }

        private void BuildUI()
        {
            var bg = PanelHelper.CreateFullRect("BG", transform);
            UIStyleKit.CreateGradientPanel(bg, new Color(0.04f, 0.04f, 0.10f, 1f), new Color(0.06f, 0.08f, 0.16f, 1f));

            var topBar = PanelHelper.CreateAnchoredRect("TopBar", transform, 0, 0.93f, 1, 1);
            UIStyleKit.CreateStyledPanel(topBar, new Color(0.06f, 0.06f, 0.14f, 0.95f));
            var btnBack = PanelHelper.CreateBtn(topBar, "← 返回", 0.01f, 0.1f, 0.15f, 0.9f);
            btnBack.onClick.AddListener(CloseSelf);
            UIStyleKit.StyleGrayButton(btnBack);
PanelHelper.CreateTxt(topBar, "# 好友排行", 20, UIStyleKit.TextGold, 0.3f, 0f, 0.7f, 1f);


            _rankListArea = PanelHelper.CreateAnchoredRect("RankList", transform, 0.03f, 0.05f, 0.97f, 0.92f);
        }

        private void RefreshRank()
        {
            if (!SocialSystem.HasInstance) return;

            for (int i = _rankListArea.childCount - 1; i >= 0; i--)
                Destroy(_rankListArea.GetChild(i).gameObject);

            var ranks = SocialSystem.Instance.GetFriendRank();
            float itemH = 0.08f;

            for (int i = 0; i < ranks.Count; i++)
            {
                var entry = ranks[i];
                float yMax = 1f - i * itemH;
                float yMin = yMax - itemH + 0.003f;
                if (yMin < 0) break;

                var itemRect = PanelHelper.CreateAnchoredRect($"Rank_{i}", _rankListArea, 0.01f, yMin, 0.99f, yMax - 0.003f);

                Color bgColor = i < 3
                    ? new Color(0.15f, 0.12f, 0.05f, 0.8f)
                    : new Color(0.08f, 0.08f, 0.15f, 0.7f);
                UIStyleKit.CreateStyledPanel(itemRect, bgColor, UIStyleKit.BorderSilver, 6, 1);

string rankIcon = i == 0 ? "1st" : i == 1 ? "2nd" : i == 2 ? "3rd" : $"{entry.Rank}";
                PanelHelper.CreateTxt(itemRect, rankIcon, 18, UIStyleKit.TextGold, 0.02f, 0.1f, 0.12f, 0.9f);
                PanelHelper.CreateTxt(itemRect, entry.Nickname, 16, UIStyleKit.TextWhite, 0.14f, 0.1f, 0.6f, 0.9f);
                PanelHelper.CreateTxt(itemRect, $"!{entry.Score}", 16, UIStyleKit.TextGold, 0.65f, 0.1f, 0.98f, 0.9f);

            }
        }
    }

    // ================================================================
    // 设置面板
    // ================================================================
    public class SettingsPanel : BasePanel
    {
        public override UILayer Layer => UILayer.Popup;
        public override bool IsCached => true;

        protected override void OnOpen(object param) { BuildUI(); }

        private void BuildUI()
        {
            var bg = PanelHelper.CreateFullRect("BG", transform);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.6f);

            var panel = PanelHelper.CreateAnchoredRect("Panel", transform, 0.1f, 0.2f, 0.9f, 0.8f);
            UIStyleKit.CreateStyledPanel(panel, UIStyleKit.PanelBgDark, UIStyleKit.BorderGold, 16, 3);

            PanelHelper.CreateTxt(panel, "⚙️ 设置", 22, UIStyleKit.TextGold, 0.1f, 0.88f, 0.9f, 0.98f);

            // BGM音量
            PanelHelper.CreateTxt(panel, "🎵 BGM音量", 16, UIStyleKit.TextWhite, 0.05f, 0.72f, 0.4f, 0.82f);
            var btnBgmDown = PanelHelper.CreateBtn(panel, "-", 0.45f, 0.74f, 0.55f, 0.82f);
            UIStyleKit.StyleGrayButton(btnBgmDown);
            var btnBgmUp = PanelHelper.CreateBtn(panel, "+", 0.75f, 0.74f, 0.85f, 0.82f);
            UIStyleKit.StyleGrayButton(btnBgmUp);

            // SFX音量
            PanelHelper.CreateTxt(panel, "🔊 SFX音量", 16, UIStyleKit.TextWhite, 0.05f, 0.58f, 0.4f, 0.68f);
            var btnSfxDown = PanelHelper.CreateBtn(panel, "-", 0.45f, 0.60f, 0.55f, 0.68f);
            UIStyleKit.StyleGrayButton(btnSfxDown);
            var btnSfxUp = PanelHelper.CreateBtn(panel, "+", 0.75f, 0.60f, 0.85f, 0.68f);
            UIStyleKit.StyleGrayButton(btnSfxUp);

            // 语言
            PanelHelper.CreateTxt(panel, "🌐 语言: 中文", 16, UIStyleKit.TextWhite, 0.05f, 0.44f, 0.95f, 0.54f);

            // 反馈
            var btnFeedback = PanelHelper.CreateBtn(panel, "📝 意见反馈", 0.1f, 0.28f, 0.9f, 0.40f);
            btnFeedback.onClick.AddListener(() => UIManager.Instance.Open<FeedbackPanel>());
            UIStyleKit.StyleBlueButton(btnFeedback);

            // 关闭
            var btnClose = PanelHelper.CreateBtn(panel, "✕ 关闭", 0.3f, 0.05f, 0.7f, 0.18f);
            btnClose.onClick.AddListener(CloseSelf);
            UIStyleKit.StyleRedButton(btnClose);
        }
    }

    // ================================================================
    // 社交面板
    // ================================================================
    public class SocialPanel : BasePanel
    {
        public override UILayer Layer => UILayer.Normal;
        public override bool IsCached => true;

        protected override void OnOpen(object param) { BuildUI(); }

        private void BuildUI()
        {
            var bg = PanelHelper.CreateFullRect("BG", transform);
            UIStyleKit.CreateGradientPanel(bg, new Color(0.04f, 0.04f, 0.10f, 1f), new Color(0.06f, 0.08f, 0.16f, 1f));

            var topBar = PanelHelper.CreateAnchoredRect("TopBar", transform, 0, 0.93f, 1, 1);
            UIStyleKit.CreateStyledPanel(topBar, new Color(0.06f, 0.06f, 0.14f, 0.95f));
            var btnBack = PanelHelper.CreateBtn(topBar, "← 返回", 0.01f, 0.1f, 0.15f, 0.9f);
            btnBack.onClick.AddListener(CloseSelf);
            UIStyleKit.StyleGrayButton(btnBack);
            PanelHelper.CreateTxt(topBar, "👥 社交", 20, UIStyleKit.TextGold, 0.3f, 0f, 0.7f, 1f);

            // 分享按钮
            var btnShare = PanelHelper.CreateBtn(
                PanelHelper.CreateAnchoredRect("ShareArea", transform, 0.05f, 0.75f, 0.95f, 0.88f),
"> 分享游戏给好友 (可获◇10)", 0.1f, 0.1f, 0.9f, 0.9f);

            btnShare.onClick.AddListener(() =>
            {
                if (SocialSystem.HasInstance) SocialSystem.Instance.ShareGame();
            });
            UIStyleKit.StyleGreenButton(btnShare);

            // 邀请按钮
            var btnInvite = PanelHelper.CreateBtn(
                PanelHelper.CreateAnchoredRect("InviteArea", transform, 0.05f, 0.60f, 0.95f, 0.73f),
"[礼] 邀请好友 (双方获◇100)", 0.1f, 0.1f, 0.9f, 0.9f);

            UIStyleKit.StyleBlueButton(btnInvite);

            // 好友体力
            var btnFriendStamina = PanelHelper.CreateBtn(
                PanelHelper.CreateAnchoredRect("StaminaArea", transform, 0.05f, 0.45f, 0.95f, 0.58f),
"! 领取好友体力", 0.1f, 0.1f, 0.9f, 0.9f);

            btnFriendStamina.onClick.AddListener(() =>
            {
                if (SocialSystem.HasInstance) SocialSystem.Instance.ClaimFriendStamina();
            });
            UIStyleKit.StyleBlueButton(btnFriendStamina);

            // 排行榜入口
            var btnRank = PanelHelper.CreateBtn(
                PanelHelper.CreateAnchoredRect("RankArea", transform, 0.05f, 0.30f, 0.95f, 0.43f),
"# 查看好友排行", 0.1f, 0.1f, 0.9f, 0.9f);

            btnRank.onClick.AddListener(() => UIManager.Instance.Open<RankPanel>());
            UIStyleKit.StyleBlueButton(btnRank);
        }
    }

    // ================================================================
    // 反馈面板
    // ================================================================
    public class FeedbackPanel : BasePanel
    {
        public override UILayer Layer => UILayer.Popup;
        public override bool IsCached => false;

        protected override void OnOpen(object param) { BuildUI(); }

        private void BuildUI()
        {
            var bg = PanelHelper.CreateFullRect("BG", transform);
            bg.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.6f);

            var panel = PanelHelper.CreateAnchoredRect("Panel", transform, 0.08f, 0.25f, 0.92f, 0.75f);
            UIStyleKit.CreateStyledPanel(panel, UIStyleKit.PanelBgDark, UIStyleKit.BorderGold, 16, 3);

            PanelHelper.CreateTxt(panel, "📝 意见反馈", 22, UIStyleKit.TextGold, 0.1f, 0.85f, 0.9f, 0.98f);
            PanelHelper.CreateTxt(panel, "请描述您遇到的问题或建议：", 14, UIStyleKit.TextGray, 0.05f, 0.72f, 0.95f, 0.82f);

            // 输入区域（占位）
            var inputArea = PanelHelper.CreateAnchoredRect("InputArea", panel, 0.05f, 0.30f, 0.95f, 0.70f);
            UIStyleKit.CreateStyledPanel(inputArea, new Color(0.04f, 0.04f, 0.08f, 0.9f), UIStyleKit.BorderSilver, 8, 1);
            PanelHelper.CreateTxt(inputArea, "点击此处输入反馈内容...", 14, UIStyleKit.TextGray, 0.05f, 0.05f, 0.95f, 0.95f);

            // 提交按钮
var btnSubmit = PanelHelper.CreateBtn(panel, "√ 提交反馈", 0.1f, 0.08f, 0.48f, 0.24f);

            btnSubmit.onClick.AddListener(() =>
            {
                Debug.Log("[Feedback] 反馈已提交");
                CloseSelf();
            });
            UIStyleKit.StyleGreenButton(btnSubmit);

            var btnCancel = PanelHelper.CreateBtn(panel, "❌ 取消", 0.52f, 0.08f, 0.9f, 0.24f);
            btnCancel.onClick.AddListener(CloseSelf);
            UIStyleKit.StyleRedButton(btnCancel);
        }
    }

    // ================================================================
    // 面板工具类（避免重复代码）
    // ================================================================
    public static class PanelHelper
    {
        public static RectTransform CreateFullRect(string name, Transform parent)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            return r;
        }

        public static RectTransform CreateAnchoredRect(string name, Transform parent,
            float xMin, float yMin, float xMax, float yMax)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(xMin, yMin); r.anchorMax = new Vector2(xMax, yMax);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            return r;
        }

        public static Text CreateTxt(RectTransform parent, string text, int size, Color color,
            float xMin, float yMin, float xMax, float yMax)
        {
            var obj = new GameObject("Txt"); obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(xMin, yMin); r.anchorMax = new Vector2(xMax, yMax);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var t = obj.AddComponent<Text>();
            t.text = text; t.fontSize = size; t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.font = Font.CreateDynamicFontFromOSFont("Arial", size);
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        public static Button CreateBtn(RectTransform parent, string label,
            float xMin, float yMin, float xMax, float yMax)
        {
            var obj = new GameObject("Btn"); obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(xMin, yMin); r.anchorMax = new Vector2(xMax, yMax);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            obj.AddComponent<Image>();
            var btn = obj.AddComponent<Button>();
            CreateTxt(r, label, 14, UIStyleKit.TextWhite, 0, 0, 1, 1);
            return btn;
        }
    }
}
