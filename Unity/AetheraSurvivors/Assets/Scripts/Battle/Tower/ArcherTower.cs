// ============================================================
// 文件名：ArcherTower.cs
// 功能描述：箭塔 — 单体高DPS、快速攻击、3级后穿透
// 创建时间：2026-03-25
// 所属模块：Battle/Tower
// 对应交互：阶段三 #121
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Projectile;

namespace AetheraSurvivors.Battle.Tower
{

    /// <summary>
    /// 箭塔 — 单体物理伤害
    /// 特性：
    /// - 高攻速、高单体DPS
    /// - Lv3特殊能力：箭矢穿透，可命中路径上的第二个目标
    /// </summary>
    public class ArcherTower : TowerBase
    {
        /// <summary>穿透目标数（Lv3=2，否则=1）</summary>
        private int PierceCount => _currentLevel >= 3 ? 2 : 1;

        protected override void OnAttack(Transform target)
        {
            var damage = new DamageInfo
            {
                Damage = Damage,
                DamageType = DamageType.Physical,
                SourceTowerId = InstanceId,
                SourcePosition = GetFirePoint(),
                PierceCount = PierceCount,
                IsCritical = false
            };

            // 发射箭矢投射物（追踪弹，速度快所以视觉上接近直线）
            if (ProjectileManager.HasInstance)
            {
                ProjectileManager.Instance.Fire(
                    ProjectileType.Homing,
                    GetFirePoint(),
                    target,
                    damage,
                    14f,
                    TowerType.Archer
                );

            }
            else
            {
                // 回退：直接伤害
                ApplyDamageToTarget(target, damage);
            }

            // Lv3穿透：寻找第二个目标
            if (PierceCount > 1)
            {
                var secondTarget = FindPierceTarget(target);
                if (secondTarget != null && ProjectileManager.HasInstance)
                {
                    ProjectileManager.Instance.Fire(
                        ProjectileType.Homing,
                        GetFirePoint(),
                        secondTarget,
                        damage,
                        14f,
                        TowerType.Archer
                    );

                }
                else if (secondTarget != null)
                {
                    ApplyDamageToTarget(secondTarget, damage);
                }
            }
        }


        /// <summary>寻找穿透目标（当前目标后方的最近敌人）</summary>
        private Transform FindPierceTarget(Transform primaryTarget)
        {
            var enemies = GetEnemiesInRange();
            Transform nearest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] == primaryTarget) continue;

                float dist = (enemies[i].position - primaryTarget.position).sqrMagnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = enemies[i];
                }
            }
            return nearest;
        }

        /// <summary>对目标应用伤害</summary>
        private void ApplyDamageToTarget(Transform target, DamageInfo damage)
        {
            var enemy = target.GetComponent<Enemy.EnemyBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }
        }
    }
}
