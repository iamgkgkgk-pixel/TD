// ============================================================
// 文件名：PoisonTower.cs
// 功能描述：毒塔 — DOT持续伤害、降低护甲、3级后扩散
// 创建时间：2026-03-25
// 所属模块：Battle/Tower
// 对应交互：阶段三 #125
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Projectile;


namespace AetheraSurvivors.Battle.Tower
{

    /// <summary>
    /// 毒塔 — DOT持续伤害型
    /// 特性：
    /// - 攻击附带中毒DOT效果（每秒造成伤害，持续数秒）
    /// - Lv2：额外附带护甲降低效果
    /// - Lv3：中毒目标死亡时，毒素扩散到附近敌人
    /// </summary>
    public class PoisonTower : TowerBase
    {
        /// <summary>DOT持续时间（秒）</summary>
        private float PoisonDuration
        {
            get
            {
                switch (_currentLevel)
                {
                    case 1: return 3f;
                    case 2: return 4f;
                    default: return 5f;
                }
            }
        }

        /// <summary>每秒DOT伤害（占基础伤害的比例）</summary>
        private float DotDamagePerSecond => Damage * 0.3f;

        /// <summary>Lv2护甲降低值</summary>
        private const float ArmorReductionValue = 15f;

        /// <summary>Lv3毒素扩散半径</summary>
        private const float SpreadRadius = 1.5f;

        protected override void OnAttack(Transform target)
        {
            if (target == null) return;

            // 发射毒液弹（追踪弹）
            var damage = new DamageInfo
            {
                Damage = Damage * 0.5f, // 毒塔直接伤害较低，主要靠DOT
                DamageType = DamageType.Magical,
                SourceTowerId = InstanceId,
                SourcePosition = GetFirePoint(),
                PierceCount = 1,
                IsCritical = false,
                BuffId = BuffSystem.BUFF_POISON,
                BuffDuration = PoisonDuration,
                BuffValue = DotDamagePerSecond
            };

            if (ProjectileManager.HasInstance)
            {
                ProjectileManager.Instance.Fire(
                    ProjectileType.Homing,
                    GetFirePoint(),
                    target,
                    damage,
                    7f,
                    TowerType.Poison
                );
            }
            else
            {
                var enemy = target.GetComponent<Enemy.EnemyBase>();
                if (enemy == null) return;
                enemy.TakeDamage(damage);
            }

            // Lv2：额外附带护甲降低（直接应用，不需要投射物）
            if (_currentLevel >= 2)
            {
                var enemy = target.GetComponent<Enemy.EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    enemy.ApplyBuff(BuffSystem.BUFF_ARMOR_REDUCE, ArmorReductionValue, PoisonDuration);
                }
            }

            // Lv3：注册死亡扩散回调
            if (_currentLevel >= 3)
            {
                var enemy = target.GetComponent<Enemy.EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    enemy.RegisterDeathCallback(OnPoisonedEnemyDeath);
                }
            }
        }


        /// <summary>Lv3：中毒目标死亡时毒素扩散</summary>
        private void OnPoisonedEnemyDeath(Enemy.EnemyBase deadEnemy)
        {
            if (deadEnemy == null) return;

            // 查找死亡位置附近的敌人
            var nearby = Physics2D.OverlapCircleAll(deadEnemy.transform.position, SpreadRadius);
            for (int i = 0; i < nearby.Length; i++)
            {
                if (nearby[i] == null || !nearby[i].CompareTag("Enemy")) continue;

                var nearbyEnemy = nearby[i].GetComponent<Enemy.EnemyBase>();
                if (nearbyEnemy == null || nearbyEnemy.IsDead) continue;
                if (nearbyEnemy == deadEnemy) continue;

                // 扩散毒素（持续时间减半）
                nearbyEnemy.ApplyBuff(BuffSystem.BUFF_POISON, DotDamagePerSecond * 0.7f, PoisonDuration * 0.5f);
            }
        }
    }
}
