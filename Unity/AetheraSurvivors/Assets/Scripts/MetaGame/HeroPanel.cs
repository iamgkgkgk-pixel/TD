// ============================================================
// 文件名：HeroPanel.cs
// 功能描述：英雄面板UI — 英雄列表、详情、升级、升星、出战选择
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #251-252
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Visual;
using AetheraSurvivors.Data;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>
    /// 英雄面板 — 英雄列表+详情+养成操作
    /// </summary>
    public class HeroPanel : BasePanel
    {
        public override UILayer Layer => UILayer.Normal;
        public override bool IsCached => true;

        // UI引用
        private RectTransform _heroListArea;
        private RectTransform _heroDetailArea;
        private Image _imgHeroPortrait;
        private Text _txtHeroName;
        private Text _txtHeroRarity;
        private Text _txtHeroLevel;
        private Text _txtHeroStar;
        private Text _txtHeroHP;
        private Text _txtHeroDamage;
        private Text _txtHeroPower;
        private Text _txtActiveSkill;
        private Text _txtPassiveSkill;
        private Button _btnLevelUp;
        private Button _btnStarUp;
        private Button _btnSetActive;
        private Button _btnBack;
        private Text _txtLevelUpCost;
        private Text _txtStarUpCost;

        private string _selectedHeroId;
        private List<Button> _heroListButtons = new List<Button>();

        protected override void OnOpen(object param)
        {
            BuildUI();
            RefreshHeroList();

            // 默认选中出战英雄
            if (PlayerDataManager.HasInstance)
            {
                string activeId = PlayerDataManager.Instance.Data.ActiveHeroId;
                if (!string.IsNullOrEmpty(activeId))
                    SelectHero(activeId);
            }
        }

        protected override void OnShow()
        {
            RefreshHeroList();
            if (!string.IsNullOrEmpty(_selectedHeroId))
                RefreshHeroDetail(_selectedHeroId);
        }

        protected override void OnClose()
        {
        }

        private void BuildUI()
        {
            // 背景
            var bgObj = new GameObject("BG");
            bgObj.transform.SetParent(transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            UIStyleKit.CreateGradientPanel(bgRect,
                new Color(0.04f, 0.04f, 0.10f, 1f),
                new Color(0.06f, 0.08f, 0.16f, 1f));

            // 顶部栏
            var topBar = CreateRect("TopBar", transform);
            topBar.anchorMin = new Vector2(0, 0.93f);
            topBar.anchorMax = Vector2.one;
            topBar.offsetMin = Vector2.zero;
            topBar.offsetMax = Vector2.zero;
            UIStyleKit.CreateStyledPanel(topBar, new Color(0.06f, 0.06f, 0.14f, 0.95f));

            _btnBack = CreateButton(topBar, "BtnBack", "← 返回",
                new Vector2(0.01f, 0.1f), new Vector2(0.15f, 0.9f));
            _btnBack.onClick.AddListener(CloseSelf);
            UIStyleKit.StyleGrayButton(_btnBack);

            var title = CreateText("Title", topBar, "🦸 英雄", 20,
                UIStyleKit.TextGold, TextAnchor.MiddleCenter);
            SetAnchors(title.rectTransform, 0.3f, 0.7f, 0f, 1f);

            // 左侧英雄列表
            _heroListArea = CreateRect("HeroList", transform);
            _heroListArea.anchorMin = new Vector2(0.02f, 0.05f);
            _heroListArea.anchorMax = new Vector2(0.35f, 0.92f);
            _heroListArea.offsetMin = Vector2.zero;
            _heroListArea.offsetMax = Vector2.zero;
            UIStyleKit.CreateStyledPanel(_heroListArea,
                new Color(0.06f, 0.06f, 0.14f, 0.8f), UIStyleKit.BorderSilver, 10, 1);

            // 右侧英雄详情
            _heroDetailArea = CreateRect("HeroDetail", transform);
            _heroDetailArea.anchorMin = new Vector2(0.37f, 0.05f);
            _heroDetailArea.anchorMax = new Vector2(0.98f, 0.92f);
            _heroDetailArea.offsetMin = Vector2.zero;
            _heroDetailArea.offsetMax = Vector2.zero;
            UIStyleKit.CreateStyledPanel(_heroDetailArea,
                new Color(0.06f, 0.06f, 0.14f, 0.8f), UIStyleKit.BorderSilver, 10, 1);

            BuildDetailArea();
        }

        private void BuildDetailArea()
        {
            // 英雄半身像（详情区顶部）
            var portraitObj = new GameObject("HeroPortrait");
            portraitObj.transform.SetParent(_heroDetailArea, false);
            var portraitRect = portraitObj.AddComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.25f, 0.55f);
            portraitRect.anchorMax = new Vector2(0.75f, 0.95f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;
            _imgHeroPortrait = portraitObj.AddComponent<Image>();
            _imgHeroPortrait.color = new Color(1, 1, 1, 0); // 初始透明，有图时显示
            _imgHeroPortrait.preserveAspect = true;
            _imgHeroPortrait.raycastTarget = false;

            // 英雄名称（移到中间偏下位置）
            _txtHeroName = CreateText("HeroName", _heroDetailArea, "选择英雄", 24,
                UIStyleKit.TextGold, TextAnchor.MiddleCenter);
            SetAnchors(_txtHeroName.rectTransform, 0.05f, 0.95f, 0.48f, 0.56f);
            UIStyleKit.AddTextShadow(_txtHeroName);

            // 稀有度
            _txtHeroRarity = CreateText("Rarity", _heroDetailArea, "", 16,
                new Color(0.8f, 0.6f, 1f), TextAnchor.MiddleCenter);
            SetAnchors(_txtHeroRarity.rectTransform, 0.05f, 0.95f, 0.82f, 0.88f);

            // 等级和星级
            _txtHeroLevel = CreateText("Level", _heroDetailArea, "Lv.1", 18,
                UIStyleKit.TextWhite, TextAnchor.MiddleLeft);
            SetAnchors(_txtHeroLevel.rectTransform, 0.05f, 0.5f, 0.74f, 0.82f);

            _txtHeroStar = CreateText("Star", _heroDetailArea, "☆☆☆☆☆☆", 18,
                new Color(1f, 0.85f, 0.2f), TextAnchor.MiddleRight);
            SetAnchors(_txtHeroStar.rectTransform, 0.5f, 0.95f, 0.74f, 0.82f);

            // 属性
            _txtHeroHP = CreateText("HP", _heroDetailArea, "HP: 0", 16,
                UIStyleKit.TextHP, TextAnchor.MiddleLeft);
            SetAnchors(_txtHeroHP.rectTransform, 0.05f, 0.5f, 0.66f, 0.74f);

            _txtHeroDamage = CreateText("Damage", _heroDetailArea, "技能伤害: 0", 16,
                UIStyleKit.TextRed, TextAnchor.MiddleLeft);
            SetAnchors(_txtHeroDamage.rectTransform, 0.5f, 0.95f, 0.66f, 0.74f);

            _txtHeroPower = CreateText("Power", _heroDetailArea, "战力: 0", 18,
                UIStyleKit.TextGold, TextAnchor.MiddleCenter);
            SetAnchors(_txtHeroPower.rectTransform, 0.05f, 0.95f, 0.58f, 0.66f);

            // 技能
            _txtActiveSkill = CreateText("ActiveSkill", _heroDetailArea, "主动技能: -", 14,
                UIStyleKit.TextGreen, TextAnchor.MiddleLeft);
            SetAnchors(_txtActiveSkill.rectTransform, 0.05f, 0.95f, 0.46f, 0.56f);

            _txtPassiveSkill = CreateText("PassiveSkill", _heroDetailArea, "被动技能: -", 14,
                new Color(0.6f, 0.8f, 1f), TextAnchor.MiddleLeft);
            SetAnchors(_txtPassiveSkill.rectTransform, 0.05f, 0.95f, 0.36f, 0.46f);

            // 操作按钮
            _btnLevelUp = CreateButton(_heroDetailArea, "BtnLevelUp", "📈 升级",
                new Vector2(0.05f, 0.18f), new Vector2(0.32f, 0.32f));
            _btnLevelUp.onClick.AddListener(OnLevelUp);
            UIStyleKit.StyleGreenButton(_btnLevelUp);

            _txtLevelUpCost = CreateText("LevelUpCost", _heroDetailArea, "金币: 100", 12,
                UIStyleKit.TextGray, TextAnchor.MiddleCenter);
            SetAnchors(_txtLevelUpCost.rectTransform, 0.05f, 0.32f, 0.12f, 0.18f);

_btnStarUp = CreateButton(_heroDetailArea, "BtnStarUp", "★ 升星",

                new Vector2(0.35f, 0.18f), new Vector2(0.65f, 0.32f));
            _btnStarUp.onClick.AddListener(OnStarUp);
            UIStyleKit.StyleBlueButton(_btnStarUp);

            _txtStarUpCost = CreateText("StarUpCost", _heroDetailArea, "碎片: 10", 12,
                UIStyleKit.TextGray, TextAnchor.MiddleCenter);
            SetAnchors(_txtStarUpCost.rectTransform, 0.35f, 0.65f, 0.12f, 0.18f);

            _btnSetActive = CreateButton(_heroDetailArea, "BtnSetActive", "🎯 出战",
                new Vector2(0.68f, 0.18f), new Vector2(0.95f, 0.32f));
            _btnSetActive.onClick.AddListener(OnSetActive);
            UIStyleKit.StyleGreenButton(_btnSetActive);
        }

        private void RefreshHeroList()
        {
            // 清除旧按钮
            foreach (var btn in _heroListButtons)
            {
                if (btn != null) Destroy(btn.gameObject);
            }
            _heroListButtons.Clear();

            var allHeroes = HeroConfigTable.GetAllHeroes();
            float btnHeight = 1f / Mathf.Max(allHeroes.Count, 1);

            for (int i = 0; i < allHeroes.Count; i++)
            {
                var config = allHeroes[i];
                string heroId = config.Id;
                bool unlocked = HeroSystem.Instance.IsHeroUnlocked(heroId);

                float yMin = 1f - (i + 1) * btnHeight;
                float yMax = 1f - i * btnHeight;

                var btn = CreateButton(_heroListArea, $"Hero_{config.Id}",
                    $"{config.Icon} {config.Name}",
                    new Vector2(0.05f, yMin + 0.005f), new Vector2(0.95f, yMax - 0.005f));

                btn.interactable = unlocked;
                string id = heroId;
                btn.onClick.AddListener(() => SelectHero(id));

                // 样式
                var img = btn.GetComponent<Image>();
                if (unlocked)
                {
                    Color btnColor = config.Rarity == HeroRarity.SSR
                        ? new Color(0.35f, 0.25f, 0.10f, 0.9f)
                        : config.Rarity == HeroRarity.SR
                            ? new Color(0.20f, 0.15f, 0.35f, 0.9f)
                            : new Color(0.12f, 0.15f, 0.25f, 0.9f);
                    UIStyleKit.StyleButton(btn, btnColor,
                        btnColor * 1.3f, btnColor * 0.7f);
                }
                else
                {
                    UIStyleKit.StyleButton(btn,
                        new Color(0.1f, 0.1f, 0.12f, 0.5f),
                        new Color(0.1f, 0.1f, 0.12f, 0.5f),
                        new Color(0.1f, 0.1f, 0.12f, 0.5f));
                }

                _heroListButtons.Add(btn);
            }
        }

        private void SelectHero(string heroId)
        {
            _selectedHeroId = heroId;
            RefreshHeroDetail(heroId);
        }

        private void RefreshHeroDetail(string heroId)
        {
            var config = HeroConfigTable.GetHero(heroId);
            if (config == null) return;

            _txtHeroName.text = $"{config.Icon} {config.Name}";

            // 加载英雄半身像
            if (_imgHeroPortrait != null && !string.IsNullOrEmpty(config.SpriteName))
            {
                Sprite portrait = SpriteLoader.LoadHeroHalf(config.SpriteName);
                if (portrait != null)
                {
                    _imgHeroPortrait.sprite = portrait;
                    _imgHeroPortrait.color = Color.white;
                }
                else
                {
                    _imgHeroPortrait.color = new Color(1, 1, 1, 0); // 无图则隐藏
                }
            }

            string rarityStr = config.Rarity == HeroRarity.SSR ? "SSR" :
                               config.Rarity == HeroRarity.SR ? "SR" : "R";
            Color rarityColor = config.Rarity == HeroRarity.SSR ? new Color(1f, 0.8f, 0.2f) :
                                config.Rarity == HeroRarity.SR ? new Color(0.8f, 0.5f, 1f) :
                                new Color(0.5f, 0.7f, 1f);
            _txtHeroRarity.text = $"[{rarityStr}] {config.Role}";
            _txtHeroRarity.color = rarityColor;

            var heroData = HeroSystem.Instance.GetHeroData(heroId);
            bool unlocked = heroData != null;

            if (unlocked)
            {
                _txtHeroLevel.text = $"Lv.{heroData.Level}/{config.MaxLevel}";

                string stars = "";
                for (int i = 0; i < config.MaxStar; i++)
                    stars += i < heroData.Star ? "★" : "☆";
                _txtHeroStar.text = stars;

                float hp = HeroSystem.Instance.GetHeroHP(heroId);
                float dmg = HeroSystem.Instance.GetHeroSkillDamage(heroId);
                int power = HeroSystem.Instance.GetHeroPower(heroId);

                _txtHeroHP.text = $"HP: {hp:F0}";
                _txtHeroDamage.text = $"技能伤害: {dmg:F0}";
_txtHeroPower.text = $"! 战力: {power}";


                int levelCost = HeroSystem.Instance.GetLevelUpGoldCost(heroId);
                int starCost = HeroSystem.Instance.GetStarUpFragmentCost(heroId);
_txtLevelUpCost.text = $"G {levelCost}";

_txtStarUpCost.text = $"# {starCost}/{heroData.Fragments}";


                _btnLevelUp.interactable = heroData.Level < config.MaxLevel;
                _btnStarUp.interactable = heroData.Star < config.MaxStar && heroData.Fragments >= starCost;

                bool isActive = PlayerDataManager.HasInstance &&
                    PlayerDataManager.Instance.Data.ActiveHeroId == heroId;
                _btnSetActive.interactable = !isActive;
            }
            else
            {
                _txtHeroLevel.text = "未解锁";
                _txtHeroStar.text = "☆☆☆☆☆☆";
                _txtHeroHP.text = "HP: ???";
                _txtHeroDamage.text = "技能伤害: ???";
_txtHeroPower.text = "! 战力: ???";

                _txtLevelUpCost.text = "-";
                _txtStarUpCost.text = "-";
                _btnLevelUp.interactable = false;
                _btnStarUp.interactable = false;
                _btnSetActive.interactable = false;
            }

            _txtActiveSkill.text = $"🔴 {config.ActiveSkillName}: {config.ActiveSkillDesc} (CD:{config.ActiveSkillCD}s)";
            _txtPassiveSkill.text = $"🔵 {config.PassiveSkillName}: {config.PassiveSkillDesc}";
        }

        // ========== 按钮回调 ==========

        private void OnLevelUp()
        {
            if (string.IsNullOrEmpty(_selectedHeroId)) return;
            if (HeroSystem.Instance.LevelUpHero(_selectedHeroId))
            {
                RefreshHeroDetail(_selectedHeroId);
            }
        }

        private void OnStarUp()
        {
            if (string.IsNullOrEmpty(_selectedHeroId)) return;
            if (HeroSystem.Instance.StarUpHero(_selectedHeroId))
            {
                RefreshHeroDetail(_selectedHeroId);
                RefreshHeroList();
            }
        }

        private void OnSetActive()
        {
            if (string.IsNullOrEmpty(_selectedHeroId)) return;
            if (HeroSystem.Instance.SetActiveHero(_selectedHeroId))
            {
                RefreshHeroDetail(_selectedHeroId);
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
            txtRect.offsetMin = new Vector2(4, 0);
            txtRect.offsetMax = new Vector2(-4, 0);

            var txt = txtObj.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = 14;
            txt.color = UIStyleKit.TextWhite;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            txt.raycastTarget = false;

            return btn;
        }
    }
}
