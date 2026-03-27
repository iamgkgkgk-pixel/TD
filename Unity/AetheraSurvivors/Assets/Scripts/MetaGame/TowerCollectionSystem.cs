// ============================================================
// 文件名：TowerCollectionSystem.cs
// 功能描述：塔图鉴/收集系统 & 局外永久升级
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #253
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Data;
using AetheraSurvivors.Battle.Tower;
using AetheraSurvivors.Battle.Visual;

namespace AetheraSurvivors.MetaGame
{
    // ========== 塔图鉴配置 ==========

    /// <summary>
    /// 塔图鉴静态配置
    /// </summary>
    [Serializable]
    public class TowerCodexConfig
    {
        public string TowerId;         // 对应TowerType名称
        public string DisplayName;     // 显示名称
        public string Icon;            // emoji图标
        public string Description;     // 描述
        public string Element;         // 元素属性
        public string SpecialAbility;  // 特殊能力描述

        // 基础属性（1级）
        public float BaseDamage;
        public float BaseAtkSpeed;     // 攻击间隔（秒）
        public float BaseRange;

        // 永久升级上限
        public int MaxAtkLevel;
        public int MaxAtkSpeedLevel;
        public int MaxRangeLevel;

        // 每级提升百分比
        public float AtkPerLevel;       // 攻击力每级提升%
        public float AtkSpeedPerLevel;  // 攻速每级提升%
        public float RangePerLevel;     // 射程每级提升%

        // 解锁所需碎片
        public int UnlockFragments;
    }

    /// <summary>
    /// 塔永久升级事件
    /// </summary>
    public struct TowerCollectionUpgradeEvent : IEvent
    {
        public string TowerId;
        public string UpgradeType; // "atk", "atkSpeed", "range"
        public int NewLevel;
        public float NewBonusPercent;
    }

    /// <summary>
    /// 塔碎片获取事件
    /// </summary>
    public struct TowerFragmentGainEvent : IEvent
    {
        public string TowerId;
        public int Amount;
        public int TotalFragments;
    }

    // ========== 塔图鉴/收集系统 ==========

    /// <summary>
    /// 塔图鉴/收集系统管理器
    /// 
    /// 职责：
    /// 1. 管理塔的图鉴数据（解锁状态、碎片数量）
    /// 2. 塔的局外永久升级（攻击力/攻速/射程）
    /// 3. 计算永久升级对局内战斗的加成
    /// 4. 提供图鉴UI所需的数据
    /// </summary>
    public class TowerCollectionSystem : Singleton<TowerCollectionSystem>
    {
        // ========== 配置表 ==========
        private static Dictionary<string, TowerCodexConfig> _codexConfigs;
        private static List<TowerCodexConfig> _allTowers;

        // ========== 运行时数据 ==========
        private Dictionary<string, int> _fragments; // 塔ID → 碎片数量
        private Dictionary<string, TowerUpgradeData> _upgradeCache; // 缓存升级数据

        // ========== 升级消耗表 ==========

        // 永久升级每级消耗金币
        private static readonly int[] UpgradeGoldCost = {
            200, 400, 700, 1100, 1600, 2200, 3000, 4000, 5200, 6600,  // 1-10
            8200, 10000, 12000, 14200, 16600, 19200, 22000, 25000, 28200, 31600 // 11-20
        };

        // 永久升级每级消耗碎片
        private static readonly int[] UpgradeFragmentCost = {
            2, 3, 4, 5, 7, 9, 12, 15, 18, 22,
            26, 30, 35, 40, 46, 52, 58, 65, 72, 80
        };

        // ========== 初始化 ==========

        protected override void OnInit()
        {
            InitCodexConfigs();
            LoadFragments();
            CacheUpgradeData();
            Debug.Log("[TowerCollection] 初始化完成");
        }

        protected override void OnDispose()
        {
            SaveFragments();
        }

        /// <summary>
        /// 初始化塔图鉴配置表
        /// </summary>
        private static void InitCodexConfigs()
        {
            if (_codexConfigs != null) return;
            _codexConfigs = new Dictionary<string, TowerCodexConfig>();
            _allTowers = new List<TowerCodexConfig>();

            Register(new TowerCodexConfig
            {
TowerId = "Archer", DisplayName = "箭塔", Icon = "[弓]",

                Description = "基础远程塔，攻速快，单体伤害",
                Element = "物理", SpecialAbility = "3级获得穿透射击",
                BaseDamage = 15f, BaseAtkSpeed = 0.8f, BaseRange = 3.5f,
                MaxAtkLevel = 20, MaxAtkSpeedLevel = 15, MaxRangeLevel = 10,
                AtkPerLevel = 3f, AtkSpeedPerLevel = 2f, RangePerLevel = 2f,
                UnlockFragments = 0 // 初始解锁
            });

            Register(new TowerCodexConfig
            {
TowerId = "Mage", DisplayName = "法塔", Icon = "[法]",

                Description = "魔法远程塔，AOE伤害，攻速较慢",
                Element = "魔法", SpecialAbility = "3级获得连锁闪电",
                BaseDamage = 25f, BaseAtkSpeed = 1.5f, BaseRange = 3.0f,
                MaxAtkLevel = 20, MaxAtkSpeedLevel = 15, MaxRangeLevel = 10,
                AtkPerLevel = 3f, AtkSpeedPerLevel = 2f, RangePerLevel = 2f,
                UnlockFragments = 0
            });

            Register(new TowerCodexConfig
            {
TowerId = "Ice", DisplayName = "冰塔", Icon = "[冰]",

                Description = "减速控制塔，降低敌人移速",
                Element = "冰霜", SpecialAbility = "3级获得冰冻效果",
                BaseDamage = 8f, BaseAtkSpeed = 1.2f, BaseRange = 3.0f,
                MaxAtkLevel = 15, MaxAtkSpeedLevel = 20, MaxRangeLevel = 10,
                AtkPerLevel = 2f, AtkSpeedPerLevel = 3f, RangePerLevel = 2f,
                UnlockFragments = 0
            });

            Register(new TowerCodexConfig
            {
TowerId = "Cannon", DisplayName = "炮塔", Icon = "[炮]",

                Description = "高伤害AOE塔，攻速最慢",
                Element = "物理", SpecialAbility = "3级获得震荡波",
                BaseDamage = 50f, BaseAtkSpeed = 2.5f, BaseRange = 2.5f,
                MaxAtkLevel = 20, MaxAtkSpeedLevel = 15, MaxRangeLevel = 10,
                AtkPerLevel = 4f, AtkSpeedPerLevel = 2f, RangePerLevel = 2f,
                UnlockFragments = 10
            });

            Register(new TowerCodexConfig
            {
TowerId = "Poison", DisplayName = "毒塔", Icon = "[毒]",

                Description = "DOT持续伤害塔，毒素叠加",
                Element = "毒素", SpecialAbility = "3级获得剧毒扩散",
                BaseDamage = 5f, BaseAtkSpeed = 1.0f, BaseRange = 2.8f,
                MaxAtkLevel = 20, MaxAtkSpeedLevel = 15, MaxRangeLevel = 10,
                AtkPerLevel = 3f, AtkSpeedPerLevel = 2f, RangePerLevel = 2f,
                UnlockFragments = 10
            });

            Register(new TowerCodexConfig
            {
TowerId = "GoldMine", DisplayName = "金矿", Icon = "[$]",

                Description = "经济塔，定期产出金币",
                Element = "经济", SpecialAbility = "3级获得宝石产出",
                BaseDamage = 0f, BaseAtkSpeed = 5.0f, BaseRange = 0f,
                MaxAtkLevel = 20, MaxAtkSpeedLevel = 15, MaxRangeLevel = 0,
                AtkPerLevel = 5f, AtkSpeedPerLevel = 3f, RangePerLevel = 0f,
                UnlockFragments = 0
            });
        }

        private static void Register(TowerCodexConfig config)
        {
            _codexConfigs[config.TowerId] = config;
            _allTowers.Add(config);
        }

        // ========== 碎片管理 ==========

        /// <summary>添加塔碎片</summary>
        public void AddFragments(string towerId, int amount)
        {
            if (amount <= 0) return;

            if (!_fragments.ContainsKey(towerId))
                _fragments[towerId] = 0;

            _fragments[towerId] += amount;
            SaveFragments();

            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new TowerFragmentGainEvent
                {
                    TowerId = towerId,
                    Amount = amount,
                    TotalFragments = _fragments[towerId]
                });
            }

            Debug.Log($"[TowerCollection] 获得碎片: {towerId} ×{amount}, 总计: {_fragments[towerId]}");
        }

        /// <summary>获取碎片数量</summary>
        public int GetFragments(string towerId)
        {
            return _fragments.ContainsKey(towerId) ? _fragments[towerId] : 0;
        }

        /// <summary>消耗碎片</summary>
        private bool SpendFragments(string towerId, int amount)
        {
            if (GetFragments(towerId) < amount) return false;
            _fragments[towerId] -= amount;
            SaveFragments();
            return true;
        }

        // ========== 永久升级 ==========

        /// <summary>
        /// 升级塔的攻击力
        /// </summary>
        public bool UpgradeAtk(string towerId)
        {
            return DoUpgrade(towerId, "atk");
        }

        /// <summary>
        /// 升级塔的攻速
        /// </summary>
        public bool UpgradeAtkSpeed(string towerId)
        {
            return DoUpgrade(towerId, "atkSpeed");
        }

        /// <summary>
        /// 升级塔的射程
        /// </summary>
        public bool UpgradeRange(string towerId)
        {
            return DoUpgrade(towerId, "range");
        }

        /// <summary>
        /// 执行升级
        /// </summary>
        private bool DoUpgrade(string towerId, string upgradeType)
        {
            var config = GetCodexConfig(towerId);
            if (config == null)
            {
                Debug.LogWarning($"[TowerCollection] 塔不存在: {towerId}");
                return false;
            }

            var upgradeData = GetOrCreateUpgradeData(towerId);
            int currentLevel = GetUpgradeLevel(upgradeData, upgradeType);
            int maxLevel = GetMaxLevel(config, upgradeType);

            if (currentLevel >= maxLevel)
            {
                Debug.LogWarning($"[TowerCollection] {towerId}.{upgradeType} 已满级");
                return false;
            }

            // 检查消耗
            int goldCost = GetUpgradeGoldCost(currentLevel);
            int fragmentCost = GetUpgradeFragmentCost(currentLevel);

            if (!PlayerDataManager.HasInstance) return false;

            if (PlayerDataManager.Instance.Data.Gold < goldCost)
            {
                Debug.LogWarning("[TowerCollection] 金币不足");
                return false;
            }

            if (GetFragments(towerId) < fragmentCost)
            {
                Debug.LogWarning("[TowerCollection] 碎片不足");
                return false;
            }

            // 扣除消耗
            PlayerDataManager.Instance.SpendGold(goldCost);
            SpendFragments(towerId, fragmentCost);

            // 执行升级
            int newLevel = currentLevel + 1;
            SetUpgradeLevel(upgradeData, upgradeType, newLevel);

            PlayerDataManager.Instance.MarkDirty();
            PlayerDataManager.Instance.Save();

            // 发布事件
            float bonusPercent = GetBonusPercent(config, upgradeType, newLevel);
            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new TowerCollectionUpgradeEvent
                {
                    TowerId = towerId,
                    UpgradeType = upgradeType,
                    NewLevel = newLevel,
                    NewBonusPercent = bonusPercent
                });

                EventBus.Instance.Publish(new TowerPermanentUpgradeEvent
                {
                    TowerId = towerId,
                    UpgradeType = upgradeType,
                    NewLevel = newLevel
                });
            }

            Debug.Log($"[TowerCollection] 升级: {config.DisplayName}.{upgradeType} → Lv.{newLevel} (+{bonusPercent:F1}%)");
            return true;
        }

        // ========== 加成计算（供战斗系统查询） ==========

        /// <summary>获取塔的攻击力永久加成（百分比，如0.15表示+15%）</summary>
        public float GetAtkBonus(string towerId)
        {
            var config = GetCodexConfig(towerId);
            var data = GetUpgradeData(towerId);
            if (config == null || data == null) return 0f;
            return data.AtkUpgradeLevel * config.AtkPerLevel / 100f;
        }

        /// <summary>获取塔的攻速永久加成（百分比）</summary>
        public float GetAtkSpeedBonus(string towerId)
        {
            var config = GetCodexConfig(towerId);
            var data = GetUpgradeData(towerId);
            if (config == null || data == null) return 0f;
            return data.AtkSpeedUpgradeLevel * config.AtkSpeedPerLevel / 100f;
        }

        /// <summary>获取塔的射程永久加成（百分比）</summary>
        public float GetRangeBonus(string towerId)
        {
            var config = GetCodexConfig(towerId);
            var data = GetUpgradeData(towerId);
            if (config == null || data == null) return 0f;
            return data.RangeUpgradeLevel * config.RangePerLevel / 100f;
        }

        /// <summary>获取塔的综合战力评分</summary>
        public int GetTowerPower(string towerId)
        {
            var data = GetUpgradeData(towerId);
            if (data == null) return 0;
            return (data.AtkUpgradeLevel + data.AtkSpeedUpgradeLevel + data.RangeUpgradeLevel) * 10;
        }

        // ========== 查询方法 ==========

        /// <summary>获取所有塔图鉴配置</summary>
        public static List<TowerCodexConfig> GetAllTowers()
        {
            InitCodexConfigs();
            return _allTowers;
        }

        /// <summary>获取塔图鉴配置</summary>
        public static TowerCodexConfig GetCodexConfig(string towerId)
        {
            InitCodexConfigs();
            _codexConfigs.TryGetValue(towerId, out var config);
            return config;
        }

        /// <summary>塔是否已解锁（碎片足够或初始解锁）</summary>
        public bool IsTowerUnlocked(string towerId)
        {
            var config = GetCodexConfig(towerId);
            if (config == null) return false;
            if (config.UnlockFragments <= 0) return true; // 初始解锁
            return GetFragments(towerId) >= config.UnlockFragments;
        }

        /// <summary>获取升级所需金币</summary>
        public int GetUpgradeGoldCost(int currentLevel)
        {
            return currentLevel < UpgradeGoldCost.Length ? UpgradeGoldCost[currentLevel] : 99999;
        }

        /// <summary>获取升级所需碎片</summary>
        public int GetUpgradeFragmentCost(int currentLevel)
        {
            return currentLevel < UpgradeFragmentCost.Length ? UpgradeFragmentCost[currentLevel] : 99;
        }

        // ========== 内部工具 ==========

        private int GetUpgradeLevel(TowerUpgradeData data, string type)
        {
            switch (type)
            {
                case "atk": return data.AtkUpgradeLevel;
                case "atkSpeed": return data.AtkSpeedUpgradeLevel;
                case "range": return data.RangeUpgradeLevel;
                default: return 0;
            }
        }

        private void SetUpgradeLevel(TowerUpgradeData data, string type, int level)
        {
            switch (type)
            {
                case "atk": data.AtkUpgradeLevel = level; break;
                case "atkSpeed": data.AtkSpeedUpgradeLevel = level; break;
                case "range": data.RangeUpgradeLevel = level; break;
            }
        }

        private int GetMaxLevel(TowerCodexConfig config, string type)
        {
            switch (type)
            {
                case "atk": return config.MaxAtkLevel;
                case "atkSpeed": return config.MaxAtkSpeedLevel;
                case "range": return config.MaxRangeLevel;
                default: return 0;
            }
        }

        private float GetBonusPercent(TowerCodexConfig config, string type, int level)
        {
            switch (type)
            {
                case "atk": return level * config.AtkPerLevel;
                case "atkSpeed": return level * config.AtkSpeedPerLevel;
                case "range": return level * config.RangePerLevel;
                default: return 0f;
            }
        }

        private TowerUpgradeData GetUpgradeData(string towerId)
        {
            if (!PlayerDataManager.HasInstance) return null;
            var upgrades = PlayerDataManager.Instance.Data.TowerUpgrades;
            if (upgrades == null) return null;

            for (int i = 0; i < upgrades.Count; i++)
            {
                if (upgrades[i].TowerId == towerId)
                    return upgrades[i];
            }
            return null;
        }

        private TowerUpgradeData GetOrCreateUpgradeData(string towerId)
        {
            if (!PlayerDataManager.HasInstance) return new TowerUpgradeData { TowerId = towerId };

            var data = PlayerDataManager.Instance.Data;
            if (data.TowerUpgrades == null)
                data.TowerUpgrades = new List<TowerUpgradeData>();

            for (int i = 0; i < data.TowerUpgrades.Count; i++)
            {
                if (data.TowerUpgrades[i].TowerId == towerId)
                    return data.TowerUpgrades[i];
            }

            var newData = new TowerUpgradeData
            {
                TowerId = towerId,
                AtkUpgradeLevel = 0,
                AtkSpeedUpgradeLevel = 0,
                RangeUpgradeLevel = 0
            };
            data.TowerUpgrades.Add(newData);
            return newData;
        }

        private void CacheUpgradeData()
        {
            _upgradeCache = new Dictionary<string, TowerUpgradeData>();
            if (!PlayerDataManager.HasInstance) return;

            var upgrades = PlayerDataManager.Instance.Data.TowerUpgrades;
            if (upgrades == null) return;

            for (int i = 0; i < upgrades.Count; i++)
            {
                _upgradeCache[upgrades[i].TowerId] = upgrades[i];
            }
        }

        private void LoadFragments()
        {
            _fragments = new Dictionary<string, int>();
            if (SaveManager.HasInstance)
            {
                var state = SaveManager.Instance.Load<TowerFragmentSaveState>("tower_fragments");
                if (state?.Fragments != null)
                {
                    for (int i = 0; i < state.Fragments.Count; i++)
                    {
                        _fragments[state.Fragments[i].TowerId] = state.Fragments[i].Amount;
                    }
                }
            }
        }

        private void SaveFragments()
        {
            if (!SaveManager.HasInstance) return;

            var state = new TowerFragmentSaveState { Fragments = new List<TowerFragmentEntry>() };
            foreach (var kvp in _fragments)
            {
                state.Fragments.Add(new TowerFragmentEntry { TowerId = kvp.Key, Amount = kvp.Value });
            }
            SaveManager.Instance.Save("tower_fragments", state);
        }
    }

    // ========== 存档数据 ==========

    [Serializable]
    public class TowerFragmentSaveState
    {
        public List<TowerFragmentEntry> Fragments;
    }

    [Serializable]
    public class TowerFragmentEntry
    {
        public string TowerId;
        public int Amount;
    }

    // ================================================================
    // 塔图鉴面板（UI）
    // ================================================================

    /// <summary>
    /// 塔图鉴/收集面板
    /// 
    /// 功能：
    /// 1. 展示所有塔的图鉴信息
    /// 2. 显示碎片数量和解锁状态
    /// 3. 永久升级操作（攻击力/攻速/射程）
    /// 4. 显示升级加成效果
    /// </summary>
    public class TowerCollectionPanel : BasePanel
    {
        public override UILayer Layer => UILayer.Normal;
        public override bool IsCached => true;

        private RectTransform _towerListArea;
        private RectTransform _detailArea;
        private string _selectedTowerId;

        // 详情区UI引用
        private Text _txtTowerName;
        private Text _txtTowerDesc;
        private Text _txtFragments;
        private Text _txtAtkLevel;
        private Text _txtAtkSpeedLevel;
        private Text _txtRangeLevel;
        private Text _txtBonusInfo;
        private Button _btnUpgradeAtk;
        private Button _btnUpgradeAtkSpeed;
        private Button _btnUpgradeRange;

        protected override void OnOpen(object param)
        {
            BuildUI();
            RefreshTowerList();
        }

        protected override void OnShow()
        {
            RefreshTowerList();
            if (!string.IsNullOrEmpty(_selectedTowerId))
                RefreshDetail(_selectedTowerId);
        }

        protected override void OnClose() { }

        // ========== UI构建 ==========

        private void BuildUI()
        {
            // 背景
            var bg = PanelHelper.CreateFullRect("BG", transform);
            UIStyleKit.CreateGradientPanel(bg,
                new Color(0.04f, 0.04f, 0.10f, 1f),
                new Color(0.06f, 0.08f, 0.16f, 1f));

            // 顶部栏
            var topBar = PanelHelper.CreateAnchoredRect("TopBar", transform, 0, 0.93f, 1, 1);
            UIStyleKit.CreateStyledPanel(topBar, new Color(0.06f, 0.06f, 0.14f, 0.95f));
            var btnBack = PanelHelper.CreateBtn(topBar, "← 返回", 0.01f, 0.1f, 0.15f, 0.9f);
            btnBack.onClick.AddListener(CloseSelf);
            UIStyleKit.StyleGrayButton(btnBack);
            PanelHelper.CreateTxt(topBar, "📖 塔图鉴", 20, UIStyleKit.TextGold, 0.3f, 0f, 0.7f, 1f);

            // 左侧塔列表
            _towerListArea = PanelHelper.CreateAnchoredRect("TowerList", transform, 0.02f, 0.05f, 0.38f, 0.92f);
            UIStyleKit.CreateStyledPanel(_towerListArea,
                new Color(0.06f, 0.06f, 0.14f, 0.7f), UIStyleKit.BorderSilver, 10, 1);

            // 右侧详情区
            _detailArea = PanelHelper.CreateAnchoredRect("DetailArea", transform, 0.40f, 0.05f, 0.98f, 0.92f);
            UIStyleKit.CreateStyledPanel(_detailArea,
                new Color(0.06f, 0.06f, 0.14f, 0.7f), UIStyleKit.BorderGold, 10, 2);

            BuildDetailArea();
        }

        private void BuildDetailArea()
        {
            // 塔名称
            _txtTowerName = PanelHelper.CreateTxt(_detailArea, "选择一座塔", 22,
                UIStyleKit.TextGold, 0.05f, 0.88f, 0.95f, 0.98f);

            // 描述
            _txtTowerDesc = PanelHelper.CreateTxt(_detailArea, "", 14,
                UIStyleKit.TextGray, 0.05f, 0.78f, 0.95f, 0.88f);
            _txtTowerDesc.alignment = TextAnchor.MiddleLeft;

            // 碎片
            _txtFragments = PanelHelper.CreateTxt(_detailArea, "碎片: 0", 16,
                UIStyleKit.TextWhite, 0.05f, 0.70f, 0.95f, 0.78f);

            // 升级区域
            // 攻击力
_txtAtkLevel = PanelHelper.CreateTxt(_detailArea, "[剑] 攻击力 Lv.0", 15,

                UIStyleKit.TextWhite, 0.05f, 0.56f, 0.55f, 0.66f);
            _txtAtkLevel.alignment = TextAnchor.MiddleLeft;
            _btnUpgradeAtk = PanelHelper.CreateBtn(_detailArea, "升级", 0.60f, 0.57f, 0.90f, 0.65f);
            _btnUpgradeAtk.onClick.AddListener(() => OnUpgrade("atk"));
            UIStyleKit.StyleGreenButton(_btnUpgradeAtk);

            // 攻速
_txtAtkSpeedLevel = PanelHelper.CreateTxt(_detailArea, "! 攻速 Lv.0", 15,

                UIStyleKit.TextWhite, 0.05f, 0.44f, 0.55f, 0.54f);
            _txtAtkSpeedLevel.alignment = TextAnchor.MiddleLeft;
            _btnUpgradeAtkSpeed = PanelHelper.CreateBtn(_detailArea, "升级", 0.60f, 0.45f, 0.90f, 0.53f);
            _btnUpgradeAtkSpeed.onClick.AddListener(() => OnUpgrade("atkSpeed"));
            UIStyleKit.StyleGreenButton(_btnUpgradeAtkSpeed);

            // 射程
_txtRangeLevel = PanelHelper.CreateTxt(_detailArea, "@ 射程 Lv.0", 15,

                UIStyleKit.TextWhite, 0.05f, 0.32f, 0.55f, 0.42f);
            _txtRangeLevel.alignment = TextAnchor.MiddleLeft;
            _btnUpgradeRange = PanelHelper.CreateBtn(_detailArea, "升级", 0.60f, 0.33f, 0.90f, 0.41f);
            _btnUpgradeRange.onClick.AddListener(() => OnUpgrade("range"));
            UIStyleKit.StyleGreenButton(_btnUpgradeRange);

            // 加成信息
            _txtBonusInfo = PanelHelper.CreateTxt(_detailArea, "", 13,
                UIStyleKit.TextGreen, 0.05f, 0.05f, 0.95f, 0.28f);
            _txtBonusInfo.alignment = TextAnchor.UpperLeft;
        }

        // ========== 刷新 ==========

        private void RefreshTowerList()
        {
            // 清除旧内容
            for (int i = _towerListArea.childCount - 1; i >= 0; i--)
            {
                var child = _towerListArea.GetChild(i);
                if (child.name.StartsWith("Tower_"))
                    Destroy(child.gameObject);
            }

            var towers = TowerCollectionSystem.GetAllTowers();
            float itemH = 1f / Mathf.Max(towers.Count, 1);

            for (int i = 0; i < towers.Count; i++)
            {
                var config = towers[i];
                string tid = config.TowerId;
                float yMin = 1f - (i + 1) * itemH + 0.01f;
                float yMax = 1f - i * itemH - 0.01f;

                var itemRect = PanelHelper.CreateAnchoredRect($"Tower_{i}", _towerListArea,
                    0.03f, yMin, 0.97f, yMax);

                bool unlocked = TowerCollectionSystem.HasInstance &&
                                TowerCollectionSystem.Instance.IsTowerUnlocked(tid);

                Color bgColor = unlocked
                    ? (_selectedTowerId == tid
                        ? new Color(0.15f, 0.20f, 0.35f, 0.9f)
                        : new Color(0.10f, 0.12f, 0.22f, 0.8f))
                    : new Color(0.06f, 0.06f, 0.10f, 0.5f);

                UIStyleKit.CreateStyledPanel(itemRect, bgColor, UIStyleKit.BorderSilver, 6, 1);

                string label = unlocked
                    ? $"{config.Icon} {config.DisplayName}"
                    : $"🔒 {config.DisplayName}";

                PanelHelper.CreateTxt(itemRect, label, 15,
                    unlocked ? UIStyleKit.TextWhite : UIStyleKit.TextGray,
                    0.05f, 0.1f, 0.95f, 0.9f);

                var btn = itemRect.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    _selectedTowerId = tid;
                    RefreshTowerList();
                    RefreshDetail(tid);
                });
            }
        }

        private void RefreshDetail(string towerId)
        {
            var config = TowerCollectionSystem.GetCodexConfig(towerId);
            if (config == null) return;

            bool unlocked = TowerCollectionSystem.HasInstance &&
                            TowerCollectionSystem.Instance.IsTowerUnlocked(towerId);

            _txtTowerName.text = $"{config.Icon} {config.DisplayName}";
            _txtTowerDesc.text = $"{config.Description}\n元素: {config.Element} | 特殊: {config.SpecialAbility}";

            int fragments = TowerCollectionSystem.HasInstance
                ? TowerCollectionSystem.Instance.GetFragments(towerId) : 0;
_txtFragments.text = config.UnlockFragments > 0
                ? $"# 碎片: {fragments}/{config.UnlockFragments} {(unlocked ? "√已解锁" : "■未解锁")}"
                : $"# 碎片: {fragments} √初始解锁";


            if (!TowerCollectionSystem.HasInstance || !unlocked)
            {
_txtAtkLevel.text = "[剑] 攻击力 ■";
                _txtAtkSpeedLevel.text = "! 攻速 ■";
                _txtRangeLevel.text = "@ 射程 ■";

                _txtBonusInfo.text = "解锁后可进行永久升级";
                _btnUpgradeAtk.interactable = false;
                _btnUpgradeAtkSpeed.interactable = false;
                _btnUpgradeRange.interactable = false;
                return;
            }

            var sys = TowerCollectionSystem.Instance;
            var upgradeData = PlayerDataManager.HasInstance
                ? FindUpgradeData(towerId) : null;

            int atkLv = upgradeData?.AtkUpgradeLevel ?? 0;
            int atkSpeedLv = upgradeData?.AtkSpeedUpgradeLevel ?? 0;
            int rangeLv = upgradeData?.RangeUpgradeLevel ?? 0;

_txtAtkLevel.text = $"[剑] 攻击力 Lv.{atkLv}/{config.MaxAtkLevel} (+{atkLv * config.AtkPerLevel:F0}%)";
            _txtAtkSpeedLevel.text = $"! 攻速 Lv.{atkSpeedLv}/{config.MaxAtkSpeedLevel} (+{atkSpeedLv * config.AtkSpeedPerLevel:F0}%)";
            _txtRangeLevel.text = $"@ 射程 Lv.{rangeLv}/{config.MaxRangeLevel} (+{rangeLv * config.RangePerLevel:F0}%)";


            _btnUpgradeAtk.interactable = atkLv < config.MaxAtkLevel;
            _btnUpgradeAtkSpeed.interactable = atkSpeedLv < config.MaxAtkSpeedLevel;
            _btnUpgradeRange.interactable = rangeLv < config.MaxRangeLevel;

            // 加成信息
            int nextCostGold = sys.GetUpgradeGoldCost(Mathf.Min(atkLv, Mathf.Min(atkSpeedLv, rangeLv)));
            int nextCostFrag = sys.GetUpgradeFragmentCost(Mathf.Min(atkLv, Mathf.Min(atkSpeedLv, rangeLv)));
            int power = sys.GetTowerPower(towerId);

_txtBonusInfo.text = $"综合战力: !{power}\n" +
                                 $"下次升级消耗: G{nextCostGold} + #{nextCostFrag}\n" +
                                 $"当前金币: G{(PlayerDataManager.HasInstance ? PlayerDataManager.Instance.Data.Gold : 0)}\n" +
                                 $"当前碎片: #{fragments}";

        }

        private void OnUpgrade(string upgradeType)
        {
            if (string.IsNullOrEmpty(_selectedTowerId) || !TowerCollectionSystem.HasInstance) return;

            bool success = false;
            switch (upgradeType)
            {
                case "atk": success = TowerCollectionSystem.Instance.UpgradeAtk(_selectedTowerId); break;
                case "atkSpeed": success = TowerCollectionSystem.Instance.UpgradeAtkSpeed(_selectedTowerId); break;
                case "range": success = TowerCollectionSystem.Instance.UpgradeRange(_selectedTowerId); break;
            }

            if (success)
            {
                RefreshDetail(_selectedTowerId);
            }
        }

        private TowerUpgradeData FindUpgradeData(string towerId)
        {
            var upgrades = PlayerDataManager.Instance.Data.TowerUpgrades;
            if (upgrades == null) return null;
            for (int i = 0; i < upgrades.Count; i++)
            {
                if (upgrades[i].TowerId == towerId) return upgrades[i];
            }
            return null;
        }
    }
}
