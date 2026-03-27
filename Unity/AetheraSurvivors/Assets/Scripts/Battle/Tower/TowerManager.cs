// ============================================================
// 文件名：TowerManager.cs
// 功能描述：塔管理器+放置系统 — 管理所有塔的生命周期
//          拖拽放置、合法位置判断、选中/取消、升级/出售面板
// 创建时间：2026-03-25
// 所属模块：Battle/Tower
// 对应交互：阶段三 #127
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Map;
using Logger = AetheraSurvivors.Framework.Logger;

// SpriteLoader 用于加载真实美术资源（有资源时自动替换占位图）

namespace AetheraSurvivors.Battle.Tower



{
    /// <summary>塔选中事件</summary>
    public struct TowerSelectedEvent : IEvent
    {
        public int TowerId;
        public TowerBase Tower;
    }

    /// <summary>塔取消选中事件</summary>
    public struct TowerDeselectedEvent : IEvent { }

    /// <summary>请求放置塔事件</summary>
    public struct TowerPlaceRequestEvent : IEvent
    {
        public TowerType TowerType;
        public Vector2Int GridPos;
    }

    /// <summary>
    /// 塔管理器 — 管理所有塔的生命周期和放置逻辑
    /// 
    /// 职责：
    /// 1. 维护所有活跃塔的列表
    /// 2. 处理放塔请求（合法性检查→扣金→创建→注册）
    /// 3. 处理升级/出售请求
    /// 4. 塔的选中/取消选中状态管理
    /// 5. 提供塔的查询接口
    /// </summary>
    public class TowerManager : MonoSingleton<TowerManager>
    {
        // ========== 配置 ==========

        [Header("塔预制体映射（Inspector中赋值）")]
        [SerializeField] private GameObject _archerTowerPrefab;
        [SerializeField] private GameObject _mageTowerPrefab;
        [SerializeField] private GameObject _iceTowerPrefab;
        [SerializeField] private GameObject _cannonTowerPrefab;
        [SerializeField] private GameObject _poisonTowerPrefab;
        [SerializeField] private GameObject _goldMinePrefab;

        // ========== 运行时数据 ==========

        /// <summary>所有活跃的塔 (实例ID → TowerBase)</summary>
        private readonly Dictionary<int, TowerBase> _activeTowers = new Dictionary<int, TowerBase>();

        /// <summary>网格坐标 → 塔的映射</summary>
        private readonly Dictionary<Vector2Int, TowerBase> _gridToTower = new Dictionary<Vector2Int, TowerBase>();

        /// <summary>当前选中的塔</summary>
        private TowerBase _selectedTower;

        /// <summary>塔配置缓存（类型 → 配置）</summary>
        private readonly Dictionary<TowerType, TowerConfig> _configCache = new Dictionary<TowerType, TowerConfig>();

        /// <summary>是否处于放塔模式</summary>
        private bool _isPlacementMode = false;

        /// <summary>当前要放置的塔类型</summary>
        private TowerType _placementType;

        // ========== 公共属性 ==========

        /// <summary>当前选中的塔</summary>
        public TowerBase SelectedTower => _selectedTower;

        /// <summary>活跃塔数量</summary>
        public int ActiveTowerCount => _activeTowers.Count;

        /// <summary>是否处于放塔模式</summary>
        public bool IsPlacementMode => _isPlacementMode;

        /// <summary>[G3-5] 当前放塔模式选中的塔类型</summary>
        public TowerType CurrentPlacementType => _placementType;

        /// <summary>[G3-5] 获取当前放塔模式的塔配置（用于连续放塔的金币检查）</summary>
        public TowerConfig GetCurrentPlacementConfig()
        {
            if (!_isPlacementMode) return null;
            return GetTowerConfig(_placementType);
        }

        // ========== 生命周期 ==========


        protected override void OnInit()
        {
            InitDefaultConfigs();
            Logger.I("TowerManager", "塔管理器初始化");
        }

        protected override void OnDispose()
        {
            ClearAllTowers();
            Logger.I("TowerManager", "塔管理器已销毁");
        }

        // ========== 核心方法：放塔 ==========

        /// <summary>
        /// 进入放塔模式
        /// </summary>
        public void EnterPlacementMode(TowerType type)
        {
            _isPlacementMode = true;
            _placementType = type;
            DeselectTower();

            // 显示可放塔位高亮
            if (MapRenderer.HasInstance)
            {
                MapRenderer.Instance.ShowTowerSlotHighlight();
            }

            Logger.D("TowerManager", "进入放塔模式: {0}", type);
        }

        /// <summary>
        /// 退出放塔模式
        /// </summary>
        public void ExitPlacementMode()
        {
            _isPlacementMode = false;

            if (MapRenderer.HasInstance)
            {
                MapRenderer.Instance.HideTowerSlotHighlight();
            }

            if (PathVisualizer.HasInstance)
            {
                PathVisualizer.Instance.EndPlacementPreview();
            }

            Logger.D("TowerManager", "退出放塔模式");
        }

        /// <summary>
        /// 尝试在指定位置放置塔
        /// </summary>
        /// <param name="gridPos">目标网格坐标</param>
        /// <returns>是否放置成功</returns>
        public bool TryPlaceTower(Vector2Int gridPos)
        {
            return TryPlaceTower(_placementType, gridPos);
        }

        /// <summary>
        /// 尝试在指定位置放置指定类型的塔
        /// </summary>
        public bool TryPlaceTower(TowerType type, Vector2Int gridPos)
        {
            var grid = GridSystem.Instance;
            if (grid == null || !grid.IsMapLoaded)
            {
                Logger.W("TowerManager", "放塔失败：地图未加载");
                return false;
            }

            // 1. 检查位置是否可放塔
            if (!grid.CanPlaceTower(gridPos))
            {
                Logger.D("TowerManager", "放塔失败：({0},{1})不可放塔", gridPos.x, gridPos.y);
                return false;
            }

            // 2. 获取塔配置
            var config = GetTowerConfig(type);
            if (config == null)
            {
                Logger.E("TowerManager", "放塔失败：未找到{0}的配置", type);
                return false;
            }

            // 3. 金矿数量限制检查
            if (type == TowerType.GoldMine && !GoldMine.CanBuildMore)
            {
                Logger.D("TowerManager", "放塔失败：金矿数量已达上限({0})", GoldMine.MaxGoldMines);
                return false;
            }

            // 4. 金币检查
            if (BattleEconomyManager.HasInstance && !BattleEconomyManager.Instance.CanAfford(config.buildCost))
            {
                Logger.D("TowerManager", "放塔失败：金币不足，需要{0}，当前{1}", config.buildCost, BattleEconomyManager.Instance.CurrentGold);
                return false;
            }


            // 5. 创建塔实例
            var towerObj = CreateTowerObject(type);
            if (towerObj == null)
            {
                Logger.E("TowerManager", "放塔失败：无法创建{0}对象", type);
                return false;
            }

            var tower = towerObj.GetComponent<TowerBase>();
            if (tower == null)
            {
                Logger.E("TowerManager", "放塔失败：{0}预制体没有TowerBase组件", type);
                Destroy(towerObj);
                return false;
            }

            // 6. 初始化塔
            tower.Initialize(config, gridPos);

            // 6.5 初始化视觉增强组件
            var visualEffect = towerObj.GetComponent<TowerVisualEffect>();
            if (visualEffect != null)
            {
                visualEffect.Initialize(tower);
            }

            // 7. 注册到网格系统

            grid.PlaceTower(gridPos, tower.InstanceId);

            // 8. 注册到管理器
            _activeTowers[tower.InstanceId] = tower;
            _gridToTower[gridPos] = tower;

            // 9. 通知寻路系统路径可能变化
            if (Pathfinding.HasInstance)
            {
                Pathfinding.Instance.OnMapChanged();
            }

            // 10. 扣金币
            if (BattleEconomyManager.HasInstance)
            {
                BattleEconomyManager.Instance.SpendGold(config.buildCost, $"建造{config.displayName}");
            }


            Logger.D("TowerManager", "塔放置成功: {0} @({1},{2}) ID={3}",
                config.displayName, gridPos.x, gridPos.y, tower.InstanceId);

            return true;
        }

        // ========== 核心方法：升级/出售 ==========

        /// <summary>
        /// 升级选中的塔
        /// </summary>
        public bool UpgradeSelectedTower()
        {
            if (_selectedTower == null || _selectedTower.IsMaxLevel) return false;

            // 注意：扣金逻辑由 BattleUI.OnUpgradeClick 负责（先扣金再调此方法）
            if (_selectedTower.Upgrade())
            {
                return true;
            }
            return false;
        }


        /// <summary>
        /// 出售选中的塔
        /// </summary>
        public bool SellSelectedTower()
        {
            if (_selectedTower == null) return false;

            var gridPos = _selectedTower.GridPos;
            int refund = _selectedTower.Sell();

            // 从管理器中移除
            _activeTowers.Remove(_selectedTower.InstanceId);
            _gridToTower.Remove(gridPos);

            // 注意：返还金币由 BattleUI.OnSellClick 负责


            _selectedTower = null;

            // 通知寻路系统
            if (Pathfinding.HasInstance)
            {
                Pathfinding.Instance.OnMapChanged();
            }

            EventBus.Instance.Publish(new TowerDeselectedEvent());
            return true;
        }

        // ========== 核心方法：选中管理 ==========

        /// <summary>
        /// 选中一个塔
        /// </summary>
        public void SelectTower(TowerBase tower)
        {
            if (tower == null) return;

            // 先取消之前的选中
            DeselectTower();

            _selectedTower = tower;
            tower.ShowRangeIndicator();

            EventBus.Instance.Publish(new TowerSelectedEvent
            {
                TowerId = tower.InstanceId,
                Tower = tower
            });
        }

        /// <summary>
        /// 取消选中
        /// </summary>
        public void DeselectTower()
        {
            if (_selectedTower != null)
            {
                _selectedTower.HideRangeIndicator();
                _selectedTower = null;

                EventBus.Instance.Publish(new TowerDeselectedEvent());
            }
        }

        /// <summary>
        /// 通过世界坐标尝试选中塔
        /// </summary>
        public bool TrySelectAtWorldPos(Vector3 worldPos)
        {
            if (!GridSystem.HasInstance) return false;

            var gridPos = GridSystem.Instance.WorldToGrid(worldPos);
            if (_gridToTower.TryGetValue(gridPos, out var tower))
            {
                SelectTower(tower);
                return true;
            }

            DeselectTower();
            return false;
        }

        // ========== 查询方法 ==========

        /// <summary>获取指定位置的塔</summary>
        public TowerBase GetTowerAt(Vector2Int gridPos)
        {
            _gridToTower.TryGetValue(gridPos, out var tower);
            return tower;
        }

        /// <summary>通过实例ID获取塔</summary>
        public TowerBase GetTowerById(int instanceId)
        {
            _activeTowers.TryGetValue(instanceId, out var tower);
            return tower;
        }

        /// <summary>获取所有活跃的塔</summary>
        public IReadOnlyDictionary<int, TowerBase> GetAllTowers()
        {
            return _activeTowers;
        }

        /// <summary>获取塔配置</summary>
        public TowerConfig GetTowerConfig(TowerType type)
        {
            _configCache.TryGetValue(type, out var config);
            return config;
        }

        // ========== 管理方法 ==========

        /// <summary>
        /// 清除所有塔（战斗结束时调用）
        /// </summary>
        public void ClearAllTowers()
        {
            foreach (var pair in _activeTowers)
            {
                if (pair.Value != null && pair.Value.gameObject != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }
            _activeTowers.Clear();
            _gridToTower.Clear();
            _selectedTower = null;
            GoldMine.ResetCount();
        }

        // ========== 内部方法 ==========

        /// <summary>创建塔的GameObject</summary>
        private GameObject CreateTowerObject(TowerType type)
        {
            GameObject prefab = GetPrefabForType(type);

            if (prefab != null && ObjectPoolManager.HasInstance)
            {
                return ObjectPoolManager.Instance.Get(prefab);
            }

            // 无预制体时，动态创建（开发阶段）
            var obj = new GameObject($"Tower_{type}");
            switch (type)
            {
                case TowerType.Archer:
                    obj.AddComponent<ArcherTower>();
                    break;
                case TowerType.Mage:
                    obj.AddComponent<MageTower>();
                    break;
                case TowerType.Ice:
                    obj.AddComponent<IceTower>();
                    break;
                case TowerType.Cannon:
                    obj.AddComponent<CannonTower>();
                    break;
                case TowerType.Poison:
                    obj.AddComponent<PoisonTower>();
                    break;
                case TowerType.GoldMine:
                    obj.AddComponent<GoldMine>();
                    break;
            }

            // 添加基础视觉组件
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;

            // 优先尝试加载真实美术资源
            Sprite realSprite = SpriteLoader.LoadTower((int)type, 1);
            if (realSprite != null)
            {
                // 有真实资源 — 使用真实Sprite，颜色保持白色（不染色）
                sr.sprite = realSprite;
                sr.color = Color.white;
            }
            else
            {
                // 无真实资源 — 使用占位纯色方块
                sr.sprite = CreateTowerPlaceholderSprite();
                sr.color = GetTowerColor(type);
            }

            // 添加碰撞体（用于点击选中）
            var col = obj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.8f);

            // 添加视觉增强组件（呼吸动画、攻击后坐力、光环等）
            if (type != TowerType.GoldMine) // 金矿不需要攻击视觉
            {
                var visualEffect = obj.AddComponent<TowerVisualEffect>();
                // Initialize会在TowerBase.Initialize之后由TryPlaceTower调用
            }

            return obj;

        }



        /// <summary>缓存的塔占位Sprite</summary>
        private static Sprite _cachedTowerSprite;

        /// <summary>创建1x1纯色占位Sprite</summary>
        private static Sprite CreateTowerPlaceholderSprite()
        {
            if (_cachedTowerSprite != null) return _cachedTowerSprite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            _cachedTowerSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _cachedTowerSprite;
        }

        /// <summary>根据塔类型返回不同颜色（占位区分用）</summary>
        private static Color GetTowerColor(TowerType type)
        {
            switch (type)
            {
                case TowerType.Archer:   return new Color(0.2f, 0.6f, 0.2f, 1f); // 深绿 箭塔
                case TowerType.Mage:     return new Color(0.5f, 0.2f, 0.8f, 1f); // 紫色 法塔
                case TowerType.Ice:      return new Color(0.4f, 0.7f, 1f, 1f);   // 浅蓝 冰塔
                case TowerType.Cannon:   return new Color(0.8f, 0.5f, 0.1f, 1f); // 橙色 炮塔
                case TowerType.Poison:   return new Color(0.1f, 0.7f, 0.1f, 1f); // 毒绿 毒塔
                case TowerType.GoldMine: return new Color(1f, 0.85f, 0f, 1f);    // 金色 金矿
                default:                 return new Color(0.5f, 0.5f, 0.5f, 1f); // 默认灰色
            }
        }

        /// <summary>获取塔类型对应的预制体</summary>

        private GameObject GetPrefabForType(TowerType type)
        {
            switch (type)
            {
                case TowerType.Archer: return _archerTowerPrefab;
                case TowerType.Mage: return _mageTowerPrefab;
                case TowerType.Ice: return _iceTowerPrefab;
                case TowerType.Cannon: return _cannonTowerPrefab;
                case TowerType.Poison: return _poisonTowerPrefab;
                case TowerType.GoldMine: return _goldMinePrefab;
                default: return null;
            }
        }

        /// <summary>初始化默认塔配置（开发阶段使用，后续从配置表加载）</summary>
        private void InitDefaultConfigs()
        {
            // 箭塔
            _configCache[TowerType.Archer] = new TowerConfig
            {
                towerType = TowerType.Archer,
                displayName = "箭塔",
                description = "单体高DPS弓箭手，Lv3可穿透",
                buildCost = 50,
                canAttack = true,
                damageType = DamageType.Physical,
                levelData = new[]
                {
                    new TowerLevelData { damage = 25, attackInterval = 0.7f, range = 3.5f, upgradeCost = 40 },
                    new TowerLevelData { damage = 40, attackInterval = 0.6f, range = 4f, upgradeCost = 70 },
                    new TowerLevelData { damage = 60, attackInterval = 0.5f, range = 4.5f, upgradeCost = 0 }
                }
            };


            // 法塔
            _configCache[TowerType.Mage] = new TowerConfig
            {
                towerType = TowerType.Mage,
                displayName = "法塔",
                description = "AOE魔法攻击，Lv3附带减速",
                buildCost = 80,
                canAttack = true,
                damageType = DamageType.Magical,
                isAOE = true,
                aoeRadius = 1.5f,
                levelData = new[]
                {
                    new TowerLevelData { damage = 30, attackInterval = 1.3f, range = 3.5f, upgradeCost = 60 },
                    new TowerLevelData { damage = 50, attackInterval = 1.1f, range = 4f, upgradeCost = 100 },
                    new TowerLevelData { damage = 75, attackInterval = 0.9f, range = 4.5f, upgradeCost = 0 }
                }
            };


            // 冰塔
            _configCache[TowerType.Ice] = new TowerConfig
            {
                towerType = TowerType.Ice,
                displayName = "冰塔",
                description = "减速控制，Lv3范围减速光环",
                buildCost = 60,
                canAttack = true,
                damageType = DamageType.Magical,
                levelData = new[]
                {
                    new TowerLevelData { damage = 12, attackInterval = 1.0f, range = 3f, upgradeCost = 50 },
                    new TowerLevelData { damage = 18, attackInterval = 0.9f, range = 3.5f, upgradeCost = 80 },
                    new TowerLevelData { damage = 25, attackInterval = 0.8f, range = 4f, upgradeCost = 0 }
                }
            };


            // 炮塔
            _configCache[TowerType.Cannon] = new TowerConfig
            {
                towerType = TowerType.Cannon,
                displayName = "炮塔",
                description = "范围爆炸伤害，Lv3击退",
                buildCost = 100,
                canAttack = true,
                damageType = DamageType.Physical,
                isAOE = true,
                aoeRadius = 1.5f,
                levelData = new[]
                {
                    new TowerLevelData { damage = 55, attackInterval = 2.2f, range = 3.5f, upgradeCost = 70 },
                    new TowerLevelData { damage = 85, attackInterval = 2.0f, range = 4f, upgradeCost = 120 },
                    new TowerLevelData { damage = 130, attackInterval = 1.8f, range = 4.5f, upgradeCost = 0 }
                }
            };


            // 毒塔
            _configCache[TowerType.Poison] = new TowerConfig
            {
                towerType = TowerType.Poison,
                displayName = "毒塔",
                description = "DOT持续伤害，Lv3毒素扩散",
                buildCost = 70,
                canAttack = true,
                damageType = DamageType.Magical,
                levelData = new[]
                {
                    new TowerLevelData { damage = 18, attackInterval = 1.5f, range = 3f, upgradeCost = 55 },
                    new TowerLevelData { damage = 28, attackInterval = 1.3f, range = 3.5f, upgradeCost = 90 },
                    new TowerLevelData { damage = 42, attackInterval = 1.1f, range = 4f, upgradeCost = 0 }
                }
            };


            // 金矿
            _configCache[TowerType.GoldMine] = new TowerConfig
            {
                towerType = TowerType.GoldMine,
                displayName = "金矿",
                description = "定时产出金币",
                buildCost = 120,
                canAttack = false,
                levelData = new[]
                {
                    new TowerLevelData { damage = 0, attackInterval = 0, range = 0, upgradeCost = 100 },
                    new TowerLevelData { damage = 0, attackInterval = 0, range = 0, upgradeCost = 180 },
                    new TowerLevelData { damage = 0, attackInterval = 0, range = 0, upgradeCost = 0 }
                }
            };

        }
    }
}
