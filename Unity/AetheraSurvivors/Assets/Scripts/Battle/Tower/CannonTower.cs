// ============================================================
// 文件名：CannonTower.cs
// 功能描述：炮塔 — 范围爆炸伤害、高单发低攻速、3级后击退
// 创建时间：2026-03-25
// 所属模块：Battle/Tower
// 对应交互：阶段三 #124
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Projectile;

namespace AetheraSurvivors.Battle.Tower
{

    /// <summary>
    /// 炮塔 — 高爆发AOE物理伤害
    /// 特性：
    /// - 高单发伤害、慢攻速
    /// - 爆炸范围伤害，AOE范围内怪物受到溅射（70%伤害）
    /// - Lv3特殊能力：击退效果（将怪物往路径反方向推一段距离）
    /// </summary>
    public class CannonTower : TowerBase
    {
        /// <summary>AOE半径</summary>
        private float AOERadius => _config != null && _config.aoeRadius > 0 ? _config.aoeRadius : 1.2f;

        /// <summary>溅射伤害比例</summary>
        private const float SplashDamageRatio = 0.7f;

        /// <summary>Lv3击退距离（路径回退格数）</summary>
        private const float KnockbackDistance = 0.5f;

        protected override void OnAttack(Transform target)
        {
            if (target == null) return;

            // 发射抛物线炮弹
            if (ProjectileManager.HasInstance)
            {
                var damage = new DamageInfo
                {
                    Damage = Damage,
                    DamageType = DamageType.Physical,
                    SourceTowerId = InstanceId,
                    SourcePosition = GetFirePoint(),
                    PierceCount = 1,
                    IsCritical = false
                };

                ProjectileManager.Instance.Fire(
                    ProjectileType.Parabolic,
                    GetFirePoint(),
                    target,
                    damage,
                    6f,
                    TowerType.Cannon
                );
            }
            else
            {
                // 回退：直接AOE伤害
                ApplyAOEDamage(target);
            }
        }

        /// <summary>回退用AOE伤害</summary>
        private void ApplyAOEDamage(Transform target)
        {
            Vector3 impactPos = target.position;
            var enemies = Physics2D.OverlapCircleAll(impactPos, AOERadius);

            for (int i = 0; i < enemies.Length; i++)
            {
                var col = enemies[i];
                if (col == null || !col.CompareTag("Enemy")) continue;

                var enemy = col.GetComponent<Enemy.EnemyBase>();
                if (enemy == null || enemy.IsDead) continue;

                bool isPrimaryTarget = (col.transform == target);

                var damage = new DamageInfo
                {
                    Damage = isPrimaryTarget ? Damage : Damage * SplashDamageRatio,
                    DamageType = DamageType.Physical,
                    SourceTowerId = InstanceId,
                    SourcePosition = GetFirePoint(),
                    PierceCount = 1,
                    IsCritical = false,
                    IsAOEHit = !isPrimaryTarget
                };

                enemy.TakeDamage(damage);

                if (_currentLevel >= 3)
                {
                    enemy.ApplyKnockback(KnockbackDistance);
                }
            }
        }

    }
}
