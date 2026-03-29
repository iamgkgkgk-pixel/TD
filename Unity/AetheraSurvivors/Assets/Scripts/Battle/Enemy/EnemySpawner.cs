// ============================================================
// 文件名：EnemySpawner.cs
// 功能描述：怪物生成器 — 从配置读取波次数据，按时间生成怪物
// 创建时间：2026-03-25
// 所属模块：Battle/Enemy
// 对应交互：阶段三 #139
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

using AetheraSurvivors.Battle.Map;
using AetheraSurvivors.Battle.Performance;

namespace AetheraSurvivors.Battle.Enemy
{
    /// <summary>怪物生成完成事件</summary>
    public struct EnemySpawnedEvent : IEvent
    {
        public int EnemyId;
        public EnemyType EnemyType;
        public Vector3 Position;
    }

    /// <summary>
    /// 怪物生成器 — 根据波次配置生成怪物
    /// </summary>
    public class EnemySpawner : MonoSingleton<EnemySpawner>
    {
        // ========== 配置 ==========

        [Header("怪物预制体")]
        [SerializeField] private GameObject _defaultEnemyPrefab;

        // ========== 运行时数据 ==========

        /// <summary>所有活跃怪物</summary>
        private readonly List<EnemyBase> _activeEnemies = new List<EnemyBase>(64);

        /// <summary>默认怪物配置</summary>
        private readonly Dictionary<EnemyType, EnemyConfig> _enemyConfigs = new Dictionary<EnemyType, EnemyConfig>();

        // ========== 公共属性 ==========

        /// <summary>当前活跃怪物数量（排除已销毁的null引用）</summary>
        public int ActiveEnemyCount
        {
            get
            {
                // 先清理列表中的null引用（被Destroy后变成null）
                CleanupNullEnemies();
                return _activeEnemies.Count;
            }
        }


        /// <summary>所有活跃怪物列表</summary>
        public IReadOnlyList<EnemyBase> ActiveEnemies => _activeEnemies;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            InitDefaultConfigs();

            // 订阅怪物死亡事件，从列表中移除
            EventBus.Instance.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
            EventBus.Instance.Subscribe<EnemyReachedBaseEvent>(OnEnemyReachedBase);

            Logger.I("EnemySpawner", "怪物生成器初始化");
        }

        protected override void OnDispose()
        {
            EventBus.Instance.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
            EventBus.Instance.Unsubscribe<EnemyReachedBaseEvent>(OnEnemyReachedBase);

            ClearAllEnemies();
            Logger.I("EnemySpawner", "怪物生成器已销毁");
        }

        // ========== 核心方法 ==========

        /// <summary>
        /// 生成一个怪物
        /// </summary>
        /// <param name="enemyType">怪物类型</param>
        /// <param name="pathPoints">路径点（世界坐标）</param>
        /// <param name="hpMultiplier">HP倍率（用于难度调节）</param>
        /// <returns>生成的怪物</returns>
        public EnemyBase SpawnEnemy(EnemyType enemyType, List<Vector3> pathPoints, float hpMultiplier = 1f)
        {
            var config = GetEnemyConfig(enemyType);
            if (config == null)
            {
                Logger.E("EnemySpawner", "未找到怪物配置: {0}", enemyType);
                return null;
            }

            // 应用HP倍率
            var runtimeConfig = new EnemyConfig
            {
                enemyType = config.enemyType,
                displayName = config.displayName,
                maxHP = config.maxHP * hpMultiplier,
                moveSpeed = config.moveSpeed,
                armor = config.armor,
                magicResist = config.magicResist,
                dodgeRate = config.dodgeRate,
                goldDrop = config.goldDrop,
                isFlying = config.isFlying,
                isBoss = config.isBoss,
                scale = config.scale
            };

            // 创建GameObject
            GameObject enemyObj = CreateEnemyObject(enemyType);
            var enemy = enemyObj.GetComponent<EnemyBase>();

            if (enemy == null)
            {
                Logger.E("EnemySpawner", "创建怪物失败：无EnemyBase组件");
                Destroy(enemyObj);
                return null;
            }

            enemy.Initialize(runtimeConfig, pathPoints);
            _activeEnemies.Add(enemy);

            // 注册到空间分区系统
            if (SpatialPartition.HasInstance)
            {
                SpatialPartition.Instance.Register(enemy);
            }

            EventBus.Instance.Publish(new EnemySpawnedEvent
            {
                EnemyId = enemy.InstanceId,
                EnemyType = enemyType,
                Position = enemyObj.transform.position
            });

            return enemy;
        }

        /// <summary>
        /// 注册外部创建的怪物到管理列表（分裂怪等动态生成的子怪调用）
        /// </summary>
        public void RegisterExternalEnemy(EnemyBase enemy)
        {
            if (enemy == null) return;
            _activeEnemies.Add(enemy);

            if (SpatialPartition.HasInstance)
            {
                SpatialPartition.Instance.Register(enemy);
            }
        }

        /// <summary>
        /// 批量生成怪物（一组相同类型）
        /// </summary>
        public void SpawnBatch(EnemyType type, int count, List<Vector3> pathPoints,
            float interval, float hpMultiplier = 1f)
        {
            StartCoroutine(SpawnBatchCoroutine(type, count, pathPoints, interval, hpMultiplier));
        }

        private System.Collections.IEnumerator SpawnBatchCoroutine(
            EnemyType type, int count, List<Vector3> pathPoints,
            float interval, float hpMultiplier)
        {
            for (int i = 0; i < count; i++)
            {
                SpawnEnemy(type, pathPoints, hpMultiplier);
                if (interval > 0f)
                {
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        /// <summary>清除所有怪物</summary>
        public void ClearAllEnemies()
        {
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                if (_activeEnemies[i] != null && _activeEnemies[i].gameObject != null)
                {
                    Destroy(_activeEnemies[i].gameObject);
                }
            }
            _activeEnemies.Clear();
        }

        /// <summary>获取怪物配置</summary>
        public EnemyConfig GetEnemyConfig(EnemyType type)
        {
            _enemyConfigs.TryGetValue(type, out var config);
            return config;
        }

        // ========== 事件处理 ==========

        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            RemoveFromActive(evt.EnemyId);
        }

        private void OnEnemyReachedBase(EnemyReachedBaseEvent evt)
        {
            RemoveFromActive(evt.EnemyId);
        }

        private void RemoveFromActive(int enemyId)
        {
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                if (_activeEnemies[i] == null || _activeEnemies[i].InstanceId == enemyId)
                {
                    _activeEnemies.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 清理列表中已被Destroy的null引用
        /// Destroy是延迟的，下一帧物体变为null但列表中仍有引用
        /// </summary>
        private void CleanupNullEnemies()
        {
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                if (_activeEnemies[i] == null)
                {
                    _activeEnemies.RemoveAt(i);
                }
            }
        }


        // ========== 内部方法 ==========

        private GameObject CreateEnemyObject(EnemyType type)
        {
            // 后续接入预制体时改为从对象池获取
            var obj = new GameObject($"Enemy_{type}");
            obj.tag = "Enemy";

            // 添加对应的怪物组件
            switch (type)
            {
                case EnemyType.Healer:
                    obj.AddComponent<Healer>();
                    break;
                case EnemyType.Slime:
                    obj.AddComponent<Slime>();
                    break;
                case EnemyType.Rogue:
                    obj.AddComponent<Rogue>();
                    break;
                case EnemyType.ShieldMage:
                    obj.AddComponent<ShieldMage>();
                    break;
                case EnemyType.BossDragon:
                    obj.AddComponent<DragonBoss>();
                    break;
                case EnemyType.BossGiant:
                    obj.AddComponent<GiantBoss>();
                    break;
                default:
                    obj.AddComponent<EnemyBase>();
                    break;
            }

            // 添加基础组件
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 8;

            // 创建视觉组件（优先加载真实美术资源）
            sr.sprite = CreatePlaceholderSprite(); // 先设置占位Sprite
            Sprite realSprite = SpriteLoader.LoadEnemy((int)type);
            if (realSprite != null)
            {
                // 有真实资源 — 使用真实Sprite，颜色保持白色（不染色）
                sr.sprite = realSprite;
                sr.color = Color.white;
            }
            else
            {
                // 无真实资源 — 使用占位纯色方块
                sr.sprite = CreatePlaceholderSprite();
                sr.color = GetEnemyColor(type);
            }


            obj.AddComponent<CircleCollider2D>().radius = 0.3f;

            // === 挂载视觉动画组件 ===
            var visualAnimator = obj.AddComponent<EnemyVisualAnimator>();
            visualAnimator.ApplyTypePreset(type); // 按怪物类型设置弹跳参数

            // 尝试加载SpriteSheet帧动画（优先使用帧动画，无资源时回退到弹跳动画）
            Sprite[] walkFrames = SpriteLoader.LoadEnemyWalkSheet((int)type);
            if (walkFrames != null && walkFrames.Length > 0)
            {
                visualAnimator.SetWalkFrames(walkFrames, 10f); // 10fps播放
                sr.sprite = walkFrames[0]; // 设置第一帧作为初始显示
                sr.color = Color.white;
                Logger.D("EnemySpawner", "怪物 {0} 使用SpriteSheet帧动画: {1}帧", type, walkFrames.Length);
            }

            visualAnimator.Initialize(sr.color);   // 初始化，传入当前颜色

            return obj;


        }


        /// <summary>缓存的占位Sprite</summary>
        private static Sprite _cachedPlaceholderSprite;

        /// <summary>创建1x1纯色占位Sprite</summary>
        private static Sprite CreatePlaceholderSprite()
        {
            if (_cachedPlaceholderSprite != null) return _cachedPlaceholderSprite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            _cachedPlaceholderSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _cachedPlaceholderSprite;
        }

        /// <summary>根据怪物类型返回不同颜色（占位区分用）</summary>
        private static Color GetEnemyColor(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Infantry:    return new Color(0.9f, 0.3f, 0.3f, 1f); // 红色 步兵
                case EnemyType.Assassin:    return new Color(0.6f, 0.2f, 0.8f, 1f); // 紫色 刺客
                case EnemyType.Knight:      return new Color(0.5f, 0.5f, 0.5f, 1f); // 灰色 骑士
                case EnemyType.Flyer:       return new Color(0.3f, 0.8f, 0.9f, 1f); // 天蓝 飞行兵
                case EnemyType.Healer:      return new Color(0.3f, 0.9f, 0.4f, 1f); // 绿色 治疗
                case EnemyType.Slime:       return new Color(0.4f, 0.9f, 0.2f, 1f); // 黄绿 史莱姆
                case EnemyType.Rogue:       return new Color(0.2f, 0.2f, 0.2f, 1f); // 深灰 盗贼
                case EnemyType.ShieldMage:  return new Color(0.3f, 0.5f, 0.9f, 1f); // 蓝色 护盾法师
                case EnemyType.BossDragon:  return new Color(1f, 0.2f, 0f, 1f);     // 橙红 炎龙Boss
                case EnemyType.BossGiant:   return new Color(0.6f, 0.4f, 0.2f, 1f); // 棕色 巨人Boss
                default:                    return new Color(0.9f, 0.3f, 0.3f, 1f); // 默认红色
            }
        }

        /// <summary>初始化默认怪物配置</summary>

        private void InitDefaultConfigs()
        {
            _enemyConfigs[EnemyType.Infantry] = new EnemyConfig
            { enemyType = EnemyType.Infantry, displayName = "步兵", maxHP = 60, moveSpeed = 1.8f, armor = 2, goldDrop = 15, scale = 0.5f };



            _enemyConfigs[EnemyType.Assassin] = new EnemyConfig
            { enemyType = EnemyType.Assassin, displayName = "刺客", maxHP = 40, moveSpeed = 3f, armor = 0, dodgeRate = 0.05f, goldDrop = 18, scale = 0.45f };



            _enemyConfigs[EnemyType.Knight] = new EnemyConfig
            { enemyType = EnemyType.Knight, displayName = "骑士", maxHP = 120, moveSpeed = 1.3f, armor = 15, goldDrop = 20, scale = 0.6f };



            _enemyConfigs[EnemyType.Flyer] = new EnemyConfig
            { enemyType = EnemyType.Flyer, displayName = "飞行兵", maxHP = 80, moveSpeed = 3f, armor = 0, magicResist = 10, isFlying = true, goldDrop = 15, scale = 0.45f };


            _enemyConfigs[EnemyType.Healer] = new EnemyConfig
            { enemyType = EnemyType.Healer, displayName = "治疗者", maxHP = 80, moveSpeed = 1.8f, armor = 3, goldDrop = 25, scale = 0.5f };



            _enemyConfigs[EnemyType.Slime] = new EnemyConfig
            { enemyType = EnemyType.Slime, displayName = "史莱姆", maxHP = 150, moveSpeed = 1.8f, armor = 10, goldDrop = 12, scale = 0.55f };


            _enemyConfigs[EnemyType.Rogue] = new EnemyConfig
            { enemyType = EnemyType.Rogue, displayName = "盗贼", maxHP = 70, moveSpeed = 3f, armor = 0, goldDrop = 18, scale = 0.45f };


            _enemyConfigs[EnemyType.ShieldMage] = new EnemyConfig
            { enemyType = EnemyType.ShieldMage, displayName = "护盾法师", maxHP = 100, moveSpeed = 2f, armor = 5, magicResist = 20, goldDrop = 22, scale = 0.5f };


            _enemyConfigs[EnemyType.BossDragon] = new EnemyConfig
            { enemyType = EnemyType.BossDragon, displayName = "炎龙", maxHP = 1200, moveSpeed = 1.2f, armor = 10, magicResist = 10, isBoss = true, goldDrop = 200, scale = 1.2f };



            _enemyConfigs[EnemyType.BossGiant] = new EnemyConfig
            { enemyType = EnemyType.BossGiant, displayName = "巨石巨人", maxHP = 5000, moveSpeed = 1f, armor = 50, magicResist = 10, isBoss = true, goldDrop = 250, scale = 1.5f };

        }
    }
}
