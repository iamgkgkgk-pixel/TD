// ============================================================
// 文件名：BaseHealth.cs
// 功能描述：玩家基地/生命系统 — 管理基地血量、怪物到达扣血、游戏失败判定
// 创建时间：2026-03-25
// 所属模块：Battle
// 对应交互：阶段三 #144
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

using AetheraSurvivors.Battle.Enemy;

namespace AetheraSurvivors.Battle
{
    /// <summary>基地血量变化事件</summary>
    public struct BaseHealthChangedEvent : IEvent
    {
        public int CurrentHP;
        public int MaxHP;
        public int Damage;
    }

    /// <summary>基地被摧毁事件（游戏失败）</summary>
    public struct BaseDestroyedEvent : IEvent { }

    /// <summary>
    /// 玩家基地生命系统
    /// </summary>
    public class BaseHealth : MonoSingleton<BaseHealth>
    {
        // ========== 运行时数据 ==========

        private int _maxHP = 20;
        private int _currentHP = 20;

        // ========== 公共属性 ==========

        public int MaxHP => _maxHP;
        public int CurrentHP => _currentHP;
        public float HPPercent => _maxHP > 0 ? (float)_currentHP / _maxHP : 0f;
        public bool IsDestroyed => _currentHP <= 0;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            EventBus.Instance.Subscribe<EnemyReachedBaseEvent>(OnEnemyReachedBase);
            Logger.I("BaseHealth", "基地生命系统初始化");
        }

        protected override void OnDispose()
        {
            EventBus.Instance.Unsubscribe<EnemyReachedBaseEvent>(OnEnemyReachedBase);
        }

        // ========== 核心方法 ==========

        /// <summary>初始化基地血量</summary>
        public void InitHealth(int maxHP)
        {
            _maxHP = maxHP;
            _currentHP = maxHP;

            EventBus.Instance.Publish(new BaseHealthChangedEvent
            {
                CurrentHP = _currentHP,
                MaxHP = _maxHP,
                Damage = 0
            });
        }

        /// <summary>基地受伤</summary>
        public void TakeDamage(int damage)
        {
            if (IsDestroyed) return;

            _currentHP = Mathf.Max(0, _currentHP - damage);

            EventBus.Instance.Publish(new BaseHealthChangedEvent
            {
                CurrentHP = _currentHP,
                MaxHP = _maxHP,
                Damage = damage
            });

            Logger.D("BaseHealth", "基地受伤: -{0} 剩余{1}/{2}", damage, _currentHP, _maxHP);

            if (_currentHP <= 0)
            {
                OnBaseDestroyed();
            }
        }

        /// <summary>基地被摧毁</summary>
        private void OnBaseDestroyed()
        {
            EventBus.Instance.Publish(new BaseDestroyedEvent());
            Logger.I("BaseHealth", "⚠️ 基地被摧毁！游戏失败");
        }

        /// <summary>重置</summary>
        public void Reset()
        {
            _currentHP = _maxHP;
        }

        // ========== 事件处理 ==========

        private void OnEnemyReachedBase(EnemyReachedBaseEvent evt)
        {
            // 每个到达基地的怪物扣1点生命
            TakeDamage(1);
        }
    }
}
