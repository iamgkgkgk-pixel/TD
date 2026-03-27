// ============================================================
// 文件名：SpecialEnemies.cs
// 功能描述：特殊机制怪物合集 — 治疗者/分裂史莱姆/隐身盗贼/护盾法师
// 创建时间：2026-03-25
// 所属模块：Battle/Enemy/SpecialEnemies
// 对应交互：阶段三 #133-#136
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Tower;

namespace AetheraSurvivors.Battle.Enemy
{
    // ====================================================================
    // #133 治疗者（Healer）— 定时为周围友军回血
    // ====================================================================

    /// <summary>
    /// 治疗者 — 定时为周围友军回血
    /// 特性：
    /// - 每3秒为半径2内的友军回复MaxHP的10%
    /// - 不攻击塔，但自身血量适中
    /// - 优先被攻击（威胁度高）
    /// </summary>
    public class Healer : EnemyBase
    {
        /// <summary>治疗间隔</summary>
        private const float HealInterval = 3f;

        /// <summary>治疗范围</summary>
        private const float HealRadius = 2f;

        /// <summary>治疗量（友军MaxHP的百分比）</summary>
        private const float HealPercent = 0.1f;

        private float _healTimer;

        public override void Initialize(EnemyConfig config, List<Vector3> pathPoints)
        {
            base.Initialize(config, pathPoints);
            _healTimer = HealInterval;
        }

        protected override void Update()
        {
            base.Update();

            if (!_isInitialized || _isDead) return;

            _healTimer -= Time.deltaTime;
            if (_healTimer <= 0f)
            {
                _healTimer = HealInterval;
                HealNearbyAllies();
            }
        }

        /// <summary>治疗附近友军</summary>
        private void HealNearbyAllies()
        {
            var colliders = Physics2D.OverlapCircleAll(transform.position, HealRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || !colliders[i].CompareTag("Enemy")) continue;

                var ally = colliders[i].GetComponent<EnemyBase>();
                if (ally == null || ally.IsDead || ally == this) continue;

                float healAmount = ally.MaxHP * HealPercent;
                ally.Heal(healAmount);
            }
        }
    }

    // ====================================================================
    // #134 分裂史莱姆（Slime）— 死亡时分裂为2-3个小史莱姆
    // ====================================================================

    /// <summary>
    /// 分裂史莱姆 — 死亡时分裂
    /// 特性：
    /// - 死亡时分裂为2-3个小史莱姆
    /// - 小史莱姆HP和体型为原来的40%，速度+20%
    /// - 分裂只发生一次（小史莱姆不再分裂）
    /// </summary>
    public class Slime : EnemyBase
    {
        /// <summary>是否可分裂（小史莱姆不可分裂）</summary>
        [SerializeField] private bool _canSplit = true;

        /// <summary>分裂数量</summary>
        private const int SplitCount = 2;

        /// <summary>小史莱姆HP比例</summary>
        private const float SplitHPRatio = 0.4f;

        /// <summary>小史莱姆速度加成</summary>
        private const float SplitSpeedBonus = 1.2f;

        protected override void Die(bool killedByPlayer)
        {
            if (_canSplit && killedByPlayer)
            {
                SpawnSplitSlimes();
            }
            base.Die(killedByPlayer);
        }

        /// <summary>生成分裂后的小史莱姆</summary>
        private void SpawnSplitSlimes()
        {
            for (int i = 0; i < SplitCount; i++)
            {
                // 创建小史莱姆配置
                var miniConfig = new EnemyConfig
                {
                    enemyType = EnemyType.Slime,
                    displayName = "小史莱姆",
                    maxHP = _config.maxHP * SplitHPRatio,
                    moveSpeed = _config.moveSpeed * SplitSpeedBonus,
                    armor = _config.armor * 0.5f,
                    magicResist = _config.magicResist * 0.5f,
                    goldDrop = Mathf.Max(1, _config.goldDrop / SplitCount),
                    scale = _config.scale * 0.6f
                };

                // 从当前位置继续沿路径移动
                var remainingPath = new List<Vector3>();
                remainingPath.Add(transform.position); // 当前位置作为起点
                for (int j = _currentPathIndex; j < _pathPoints.Count; j++)
                {
                    remainingPath.Add(_pathPoints[j]);
                }

                // 通过EnemySpawner创建（需要后续接入）
                // 简化实现：直接创建
                var slimeObj = new GameObject($"MiniSlime_{i}");
                slimeObj.tag = "Enemy";
                var slime = slimeObj.AddComponent<Slime>();
                slime._canSplit = false; // 小史莱姆不再分裂
                slimeObj.AddComponent<SpriteRenderer>();
                slimeObj.AddComponent<CircleCollider2D>().radius = 0.3f;
                slime.Initialize(miniConfig, remainingPath);

                // 偏移一小段距离避免重叠
                Vector3 offset = new Vector3(
                    Random.Range(-0.3f, 0.3f),
                    Random.Range(-0.3f, 0.3f),
                    0f);
                slimeObj.transform.position += offset;
            }
        }
    }

    // ====================================================================
    // #135 隐身盗贼（Rogue）— 定时隐身
    // ====================================================================

    /// <summary>
    /// 隐身盗贼 — 定时隐身不可被选为目标
    /// 特性：
    /// - 每5秒进入隐身状态，持续3秒
    /// - 隐身时不可被选为攻击目标
    /// - 被AOE命中时取消隐身
    /// - 隐身时半透明显示（玩家可看到位置）
    /// </summary>
    public class Rogue : EnemyBase
    {
        /// <summary>隐身间隔</summary>
        private const float StealthCooldown = 5f;

        /// <summary>隐身持续时间</summary>
        private const float StealthDuration = 3f;

        /// <summary>是否处于隐身状态</summary>
        private bool _isStealthed = false;

        private float _stealthTimer;
        private float _stealthCooldownTimer;

        /// <summary>是否处于隐身状态（外部查询）</summary>
        public bool IsStealthed => _isStealthed;

        public override void Initialize(EnemyConfig config, List<Vector3> pathPoints)
        {
            base.Initialize(config, pathPoints);
            _isStealthed = false;
            _stealthCooldownTimer = StealthCooldown;
        }

        protected override void Update()
        {
            base.Update();

            if (!_isInitialized || _isDead) return;

            if (_isStealthed)
            {
                _stealthTimer -= Time.deltaTime;
                if (_stealthTimer <= 0f)
                {
                    ExitStealth();
                }
            }
            else
            {
                _stealthCooldownTimer -= Time.deltaTime;
                if (_stealthCooldownTimer <= 0f)
                {
                    EnterStealth();
                }
            }
        }

        /// <summary>进入隐身</summary>
        private void EnterStealth()
        {
            _isStealthed = true;
            _stealthTimer = StealthDuration;

            // 半透明显示
            if (_spriteRenderer != null)
            {
                var color = _spriteRenderer.color;
                color.a = 0.3f;
                _spriteRenderer.color = color;
            }

            // 更改tag使其不被FindGameObjectsWithTag("Enemy")找到
            gameObject.tag = "Untagged";
        }

        /// <summary>退出隐身</summary>
        private void ExitStealth()
        {
            _isStealthed = false;
            _stealthCooldownTimer = StealthCooldown;

            // 恢复不透明
            if (_spriteRenderer != null)
            {
                var color = _spriteRenderer.color;
                color.a = 1f;
                _spriteRenderer.color = color;
            }

            gameObject.tag = "Enemy";
        }

        /// <summary>受伤时：AOE命中取消隐身</summary>
        public override void TakeDamage(DamageInfo damageInfo)
        {
            // AOE命中打断隐身
            if (_isStealthed && damageInfo.IsAOEHit)
            {
                ExitStealth();
            }

            base.TakeDamage(damageInfo);
        }
    }

    // ====================================================================
    // #136 护盾法师（ShieldMage）— 为周围友军施加护盾
    // ====================================================================

    /// <summary>
    /// 护盾法师 — 为周围友军施加护盾
    /// 特性：
    /// - 每6秒为半径2.5内的友军施加护盾（吸收一定伤害）
    /// - 护盾值 = 自身MaxHP的15%
    /// - 自身没有护盾能力（只给队友）
    /// </summary>
    public class ShieldMage : EnemyBase
    {
        /// <summary>施盾间隔</summary>
        private const float ShieldInterval = 6f;

        /// <summary>施盾范围</summary>
        private const float ShieldRadius = 2.5f;

        /// <summary>护盾值（自身MaxHP的百分比）</summary>
        private const float ShieldPercent = 0.15f;

        /// <summary>护盾持续时间</summary>
        private const float ShieldDuration = 8f;

        private float _shieldTimer;

        public override void Initialize(EnemyConfig config, List<Vector3> pathPoints)
        {
            base.Initialize(config, pathPoints);
            _shieldTimer = ShieldInterval;
        }

        protected override void Update()
        {
            base.Update();

            if (!_isInitialized || _isDead) return;

            _shieldTimer -= Time.deltaTime;
            if (_shieldTimer <= 0f)
            {
                _shieldTimer = ShieldInterval;
                ApplyShieldToAllies();
            }
        }

        /// <summary>为附近友军施加护盾</summary>
        private void ApplyShieldToAllies()
        {
            float shieldAmount = _maxHP * ShieldPercent;

            var colliders = Physics2D.OverlapCircleAll(transform.position, ShieldRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || !colliders[i].CompareTag("Enemy")) continue;

                var ally = colliders[i].GetComponent<EnemyBase>();
                if (ally == null || ally.IsDead || ally == this) continue;

                // 如果已有护盾，刷新
                ally.ApplyBuff(BuffSystem.BUFF_SHIELD, shieldAmount, ShieldDuration);
            }
        }
    }
}
