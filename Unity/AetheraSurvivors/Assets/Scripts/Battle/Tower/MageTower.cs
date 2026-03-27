// ============================================================
// 文件名：MageTower.cs
// 功能描述：法塔 — AOE范围魔法攻击、攻速较慢、3级后减速
// 创建时间：2026-03-25
// 所属模块：Battle/Tower
// 对应交互：阶段三 #122
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Projectile;


namespace AetheraSurvivors.Battle.Tower
{

    /// <summary>
    /// 法塔 — AOE范围魔法伤害
    /// 特性：
    /// - 范围攻击，对区域内所有怪物造成魔法伤害
    /// - 攻速较慢但面板伤害高
    /// - Lv3特殊能力：命中目标附带30%减速效果（持续2秒）
    /// </summary>
    public class MageTower : TowerBase
    {
        /// <summary>AOE半径（从配置获取，默认1.5）</summary>
        private float AOERadius => _config != null && _config.aoeRadius > 0 ? _config.aoeRadius : 1.5f;

        /// <summary>Lv3减速效果值</summary>
        private const float SlowValue = 0.3f;

        /// <summary>Lv3减速持续时间</summary>
        private const float SlowDuration = 2f;

        protected override void OnAttack(Transform target)
        {
            if (target == null) return;

            // 发射追踪魔法弹（命中后由投射物系统处理伤害）
            // AOE伤害在命中时由特效系统触发
            Vector3 aoeCenter = target.position;

            // 发射追踪弹
            if (ProjectileManager.HasInstance)
            {
                var damage = new DamageInfo
                {
                    Damage = Damage,
                    DamageType = DamageType.Magical,
                    SourceTowerId = InstanceId,
                    SourcePosition = GetFirePoint(),
                    PierceCount = 1,
                    IsCritical = false
                };

                if (_currentLevel >= 3)
                {
                    damage.BuffId = BuffSystem.BUFF_SLOW;
                    damage.BuffDuration = SlowDuration;
                    damage.BuffValue = SlowValue;
                }

                ProjectileManager.Instance.Fire(
                    ProjectileType.Homing,
                    GetFirePoint(),
                    target,
                    damage,
                    8f,
                    TowerType.Mage
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
            Vector3 aoeCenter = target.position;
            var enemies = Physics2D.OverlapCircleAll(aoeCenter, AOERadius);

            for (int i = 0; i < enemies.Length; i++)
            {
                var col = enemies[i];
                if (col == null || !col.CompareTag("Enemy")) continue;

                var enemy = col.GetComponent<Enemy.EnemyBase>();
                if (enemy == null || enemy.IsDead) continue;

                var damage = new DamageInfo
                {
                    Damage = Damage,
                    DamageType = DamageType.Magical,
                    SourceTowerId = InstanceId,
                    SourcePosition = GetFirePoint(),
                    PierceCount = 1,
                    IsCritical = false,
                    IsAOEHit = (col.transform != target)
                };

                if (_currentLevel >= 3)
                {
                    damage.BuffId = BuffSystem.BUFF_SLOW;
                    damage.BuffDuration = SlowDuration;
                    damage.BuffValue = SlowValue;
                }

                enemy.TakeDamage(damage);
            }
        }

    }
}
