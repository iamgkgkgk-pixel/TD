// ============================================================
// 文件名：BossEnemies.cs
// 功能描述：Boss怪物 — 龙Boss（多阶段/火焰吐息）和巨人Boss（高血量/践踏）
// 创建时间：2026-03-25
// 所属模块：Battle/Enemy
// 对应交互：阶段三 #137-#138
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

using AetheraSurvivors.Battle.Tower;

namespace AetheraSurvivors.Battle.Enemy
{
    /// <summary>Boss阶段变化事件</summary>
    public struct BossPhaseChangedEvent : IEvent
    {
        public int BossId;
        public int NewPhase;
        public float HPPercent;
    }

    // ====================================================================
    // #137 龙Boss — 多阶段、火焰吐息
    // ====================================================================

    /// <summary>
    /// 龙Boss — 多阶段血量、火焰吐息范围伤害
    /// 特性：
    /// - Phase 1 (100%-60%HP)：正常移动，偶尔火焰吐息
    /// - Phase 2 (60%-30%HP)：加速+火焰吐息频率提高
    /// - Phase 3 (30%-0%HP)：狂暴状态，大范围火焰吐息
    /// </summary>
    public class DragonBoss : EnemyBase
    {
        /// <summary>当前阶段</summary>
        private int _currentPhase = 1;

        /// <summary>火焰吐息冷却</summary>
        private float _breathTimer;

        /// <summary>各阶段火焰吐息间隔</summary>
        private float BreathInterval
        {
            get
            {
                switch (_currentPhase)
                {
                    case 1: return 8f;
                    case 2: return 5f;
                    default: return 3f;
                }
            }
        }

        /// <summary>火焰吐息范围</summary>
        private float BreathRadius
        {
            get
            {
                switch (_currentPhase)
                {
                    case 1: return 1.5f;
                    case 2: return 2f;
                    default: return 2.5f;
                }
            }
        }

        /// <summary>火焰吐息伤害（对塔的伤害比例）</summary>
        private const float BreathDamage = 50f;

        public override void Initialize(EnemyConfig config, List<Vector3> pathPoints)
        {
            base.Initialize(config, pathPoints);
            _currentPhase = 1;
            _breathTimer = BreathInterval;
        }

        protected override void Update()
        {
            base.Update();

            if (!_isInitialized || _isDead) return;

            // 检查阶段变化
            CheckPhaseTransition();

            // 火焰吐息冷却
            _breathTimer -= Time.deltaTime;
            if (_breathTimer <= 0f)
            {
                _breathTimer = BreathInterval;
                FireBreath();
            }
        }

        /// <summary>检查阶段转换</summary>
        private void CheckPhaseTransition()
        {
            int newPhase = _currentPhase;
            float hpPercent = HPPercent;

            if (hpPercent <= 0.3f) newPhase = 3;
            else if (hpPercent <= 0.6f) newPhase = 2;
            else newPhase = 1;

            if (newPhase != _currentPhase)
            {
                _currentPhase = newPhase;
                OnPhaseChanged();
            }
        }

        /// <summary>阶段变化回调</summary>
        private void OnPhaseChanged()
        {
            EventBus.Instance.Publish(new BossPhaseChangedEvent
            {
                BossId = InstanceId,
                NewPhase = _currentPhase,
                HPPercent = HPPercent
            });

            Logger.I("DragonBoss", "龙Boss进入阶段{0}, HP={1:P0}", _currentPhase, HPPercent);

            // Phase 2：加速
            if (_currentPhase >= 2)
            {
                ApplyBuff(BuffSystem.BUFF_SPEED_UP, 0.3f, 999f);
            }
        }

        /// <summary>火焰吐息攻击</summary>
        private void FireBreath()
        {
            // 以自身前方位置为中心的范围攻击
            Vector3 breathCenter = transform.position;

            Logger.D("DragonBoss", "龙Boss释放火焰吐息 Phase{0} 范围{1}", _currentPhase, BreathRadius);

            // 注：这里可以对范围内的塔造伤害（毁塔机制）
            // 暂时只做视觉效果提示，毁塔机制后续完善
            // 对范围内的塔施加灼烧效果可以在这里扩展
        }

        protected override void OnDeathEffect()
        {
            Logger.I("DragonBoss", "龙Boss被击杀！");
            // Boss死亡特效（后续添加）
            base.OnDeathEffect();
        }
    }

    // ====================================================================
    // #138 巨人Boss — 超高血量、践踏AOE
    // ====================================================================

    /// <summary>
    /// 巨人Boss — 超高血量、践踏AOE
    /// 特性：
    /// - 极高血量，极慢移动速度
    /// - 定时践踏：对周围小范围造成伤害并击退
    /// - 每损失25%HP，践踏频率提高
    /// - 势不可挡：免疫冰冻和击退
    /// </summary>
    public class GiantBoss : EnemyBase
    {
        /// <summary>践踏冷却</summary>
        private float _stompTimer;

        /// <summary>践踏间隔（随HP减少而降低）</summary>
        private float StompInterval => Mathf.Lerp(4f, 2f, 1f - HPPercent);

        /// <summary>践踏范围</summary>
        private const float StompRadius = 1.8f;

        /// <summary>践踏伤害（暂时为视觉效果标记）</summary>
        private const float StompDamage = 30f;

        public override void Initialize(EnemyConfig config, List<Vector3> pathPoints)
        {
            base.Initialize(config, pathPoints);
            _stompTimer = 4f;
        }

        protected override void Update()
        {
            base.Update();

            if (!_isInitialized || _isDead) return;

            // 践踏冷却
            _stompTimer -= Time.deltaTime;
            if (_stompTimer <= 0f)
            {
                _stompTimer = StompInterval;
                Stomp();
            }
        }

        /// <summary>践踏攻击</summary>
        private void Stomp()
        {
            Logger.D("GiantBoss", "巨人Boss践踏！范围{0}", StompRadius);

            // 践踏视觉效果（后续接入粒子系统）
            // 对塔造成伤害的机制后续扩展
        }

        /// <summary>巨人免疫冰冻和击退</summary>
        public override void ApplyKnockback(float distance)
        {
            // 势不可挡：免疫击退
            Logger.D("GiantBoss", "巨人Boss免疫击退");
        }

        public override void TakeDamage(DamageInfo damageInfo)
        {
            // 免疫冰冻
            if (damageInfo.BuffId == BuffSystem.BUFF_FREEZE)
            {
                damageInfo.BuffId = 0; // 移除冰冻效果
            }

            base.TakeDamage(damageInfo);
        }

        protected override void OnDeathEffect()
        {
            Logger.I("GiantBoss", "巨人Boss被击杀！");
            base.OnDeathEffect();
        }
    }
}
