// ============================================================
// 文件名：IceTower.cs
// 功能描述：冰塔 — 减速光环、攻击附带冰冻、3级后范围减速
// 创建时间：2026-03-25
// 所属模块：Battle/Tower
// 对应交互：阶段三 #123
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Projectile;


namespace AetheraSurvivors.Battle.Tower
{

    /// <summary>
    /// 冰塔 — 减速控制型
    /// 特性：
    /// - 攻击附带冰冻减速效果
    /// - Lv1：单体减速30%（2秒）
    /// - Lv2：单体减速40%（2.5秒）
    /// - Lv3：范围减速光环，射程内所有怪物持续减速25%
    /// </summary>
    public class IceTower : TowerBase
    {
        /// <summary>各等级减速比例</summary>
        private float SlowPercent
        {
            get
            {
                switch (_currentLevel)
                {
                    case 1: return 0.3f;
                    case 2: return 0.4f;
                    default: return 0.4f;
                }
            }
        }

        /// <summary>减速持续时间</summary>
        private float SlowDuration
        {
            get
            {
                switch (_currentLevel)
                {
                    case 1: return 2f;
                    case 2: return 2.5f;
                    default: return 2.5f;
                }
            }
        }

        /// <summary>Lv3光环减速比例</summary>
        private const float AuraSlowPercent = 0.25f;

        /// <summary>Lv3光环Tick间隔</summary>
        private float _auraTick = 0f;
        private const float AuraInterval = 1f;

        protected override void Update()
        {
            base.Update();

            // Lv3：范围减速光环
            if (_isInitialized && _currentLevel >= 3)
            {
                _auraTick -= Time.deltaTime;
                if (_auraTick <= 0f)
                {
                    _auraTick = AuraInterval;
                    ApplyAuraSlow();
                }
            }
        }

        protected override void OnAttack(Transform target)
        {
            if (target == null) return;

            var damage = new DamageInfo
            {
                Damage = Damage,
                DamageType = DamageType.Magical,
                SourceTowerId = InstanceId,
                SourcePosition = GetFirePoint(),
                PierceCount = 1,
                IsCritical = false,
                BuffId = BuffSystem.BUFF_SLOW,
                BuffDuration = SlowDuration,
                BuffValue = SlowPercent
            };

            // 发射冰弹（追踪弹）
            if (ProjectileManager.HasInstance)
            {
                ProjectileManager.Instance.Fire(
                    ProjectileType.Homing,
                    GetFirePoint(),
                    target,
                    damage,
                    9f,
                    TowerType.Ice
                );
            }
            else
            {
                var enemy = target.GetComponent<Enemy.EnemyBase>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damage);
                }
            }
        }


        /// <summary>Lv3光环减速：射程内所有怪物持续减速</summary>
        private void ApplyAuraSlow()
        {
            var enemies = GetEnemiesInRange();
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i].GetComponent<Enemy.EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    enemy.ApplyBuff(BuffSystem.BUFF_SLOW, AuraSlowPercent, AuraInterval + 0.5f);
                }
            }
        }
    }
}
