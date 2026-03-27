// ============================================================
// 文件名：BattleFeelSystem.cs
// 功能描述：战斗手感打磨系统
//          屏幕震动、慢镜头、击杀连击、帧冻结、攻击节奏感
//          让战斗从"能玩"提升到"好玩"的关键系统
// 创建时间：2026-03-25
// 所属模块：Battle/Polish
// 对应交互：阶段三 #171-195（战斗手感调整）
// ============================================================

using System.Collections;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Tower;
using AetheraSurvivors.Battle.Wave;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Polish

{
    // ====================================================================
    // 手感配置
    // ====================================================================

    /// <summary>屏幕震动配置</summary>
    [System.Serializable]
    public class ScreenShakeConfig
    {
        /// <summary>震动强度</summary>
        public float intensity = 0.1f;
        /// <summary>震动持续时间</summary>
        public float duration = 0.15f;
        /// <summary>衰减曲线（1=线性衰减）</summary>
        public float decayRate = 3f;
    }

    /// <summary>慢镜头配置</summary>
    [System.Serializable]
    public class SlowMotionConfig
    {
        /// <summary>时间缩放（0.3=30%速度）</summary>
        public float timeScale = 0.3f;
        /// <summary>持续时间（真实时间秒）</summary>
        public float duration = 0.2f;
        /// <summary>恢复到正常速度的过渡时间</summary>
        public float recoveryTime = 0.1f;
    }

    // ====================================================================
    // 击杀连击数据
    // ====================================================================

    /// <summary>连击数据</summary>
    public struct ComboData
    {
        /// <summary>当前连击数</summary>
        public int Count;
        /// <summary>连击计时器（归零时连击断裂）</summary>
        public float Timer;
        /// <summary>最高连击数（本局）</summary>
        public int MaxCombo;
        /// <summary>连击等级（1=普通,2=不错,3=精彩,4=完美,5=传说）</summary>
        public int Grade;
    }

    /// <summary>连击事件</summary>
    public struct ComboEvent : IEvent
    {
        public int ComboCount;
        public int ComboGrade;
        public Vector3 Position;
    }

    /// <summary>连击断裂事件</summary>
    public struct ComboBreakEvent : IEvent
    {
        public int FinalCombo;
    }

    /// <summary>Boss击杀事件（特殊反馈）</summary>
    public struct BossKillEvent : IEvent
    {
        public EnemyType BossType;
        public Vector3 Position;
    }

    // ====================================================================
    // BattleFeelSystem 核心类
    // ====================================================================

    /// <summary>
    /// 战斗手感打磨系统
    /// 
    /// 核心职责：
    /// 1. 屏幕震动 — 不同攻击/事件触发不同强度震动
    /// 2. 慢镜头 — Boss死亡、大招等关键时刻
    /// 3. 击杀连击 — 快速击杀累积连击数，触发额外反馈
    /// 4. 帧冻结 — 打击感核心：命中瞬间短暂停顿
    /// 5. 攻击节奏 — 确保攻击间隔有节奏感
    /// 6. 操作反馈 — 放塔/升级/出售的触感反馈
    /// </summary>
    public class BattleFeelSystem : MonoSingleton<BattleFeelSystem>
    {
        // ========== 震动配置预设 ==========

        /// <summary>轻微震动（普通攻击）</summary>
        private readonly ScreenShakeConfig _lightShake = new ScreenShakeConfig
        {
            intensity = 0.03f, duration = 0.08f, decayRate = 5f
        };

        /// <summary>中等震动（炮塔/AOE）</summary>
        private readonly ScreenShakeConfig _mediumShake = new ScreenShakeConfig
        {
            intensity = 0.08f, duration = 0.12f, decayRate = 4f
        };

        /// <summary>强烈震动（Boss技能/死亡）</summary>
        private readonly ScreenShakeConfig _heavyShake = new ScreenShakeConfig
        {
            intensity = 0.15f, duration = 0.25f, decayRate = 3f
        };

        /// <summary>超强震动（Boss击杀/全屏技能）</summary>
        private readonly ScreenShakeConfig _epicShake = new ScreenShakeConfig
        {
            intensity = 0.25f, duration = 0.4f, decayRate = 2f
        };

        // ========== 慢镜头配置预设 ==========

        /// <summary>Boss击杀慢镜头</summary>
        private readonly SlowMotionConfig _bossKillSloMo = new SlowMotionConfig
        {
            timeScale = 0.2f, duration = 0.5f, recoveryTime = 0.3f
        };

        /// <summary>精彩连击慢镜头</summary>
        private readonly SlowMotionConfig _comboSloMo = new SlowMotionConfig
        {
            timeScale = 0.4f, duration = 0.2f, recoveryTime = 0.15f
        };

        // ========== 运行时数据 ==========

        /// <summary>连击数据</summary>
        private ComboData _combo;

        /// <summary>连击超时时间（秒）</summary>
        private const float ComboTimeout = 2.0f;

        /// <summary>连击等级阈值</summary>
        private static readonly int[] ComboGradeThresholds = { 0, 3, 8, 15, 25 };

        /// <summary>当前震动偏移量（叠加到相机位置上）</summary>
        private Vector3 _shakeOffset = Vector3.zero;

        /// <summary>上一帧已应用的震动偏移（用于在下一帧移除）</summary>
        private Vector3 _appliedShakeOffset = Vector3.zero;


        /// <summary>震动协程引用</summary>
        private Coroutine _shakeCoroutine;


        /// <summary>慢镜头协程引用</summary>
        private Coroutine _slowMotionCoroutine;

        /// <summary>帧冻结协程引用</summary>
        private Coroutine _hitFreezeCoroutine;

        /// <summary>是否启用手感效果</summary>
        private bool _enabled = true;

        /// <summary>当前游戏速度缓存（恢复用）</summary>
        private float _cachedTimeScale = 1f;

        /// <summary>主摄像机Transform</summary>
        private Transform _cameraTransform;

        // ========== 公共属性 ==========

        /// <summary>当前连击数据</summary>
        public ComboData CurrentCombo => _combo;

        /// <summary>是否启用手感效果</summary>
        public bool FeelEnabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            // 订阅事件
            EventBus.Instance.Subscribe<EnemyDeathEvent>(OnEnemyKilled);
            EventBus.Instance.Subscribe<TowerAttackEvent>(OnTowerAttack);
            EventBus.Instance.Subscribe<WaveStartEvent>(OnWaveStart);
            EventBus.Instance.Subscribe<BattleResultEvent>(OnBattleEnd);

            // 缓存摄像机
            var cam = Camera.main;
            if (cam != null)
            {
                _cameraTransform = cam.transform;
            }


            ResetCombo();
            Logger.I("BattleFeelSystem", "战斗手感系统初始化");
        }

        protected override void OnDispose()
        {
            EventBus.Instance.Unsubscribe<EnemyDeathEvent>(OnEnemyKilled);
            EventBus.Instance.Unsubscribe<TowerAttackEvent>(OnTowerAttack);
            EventBus.Instance.Unsubscribe<WaveStartEvent>(OnWaveStart);
            EventBus.Instance.Unsubscribe<BattleResultEvent>(OnBattleEnd);

            StopAllCoroutines();

            // 移除残留的震屏偏移
            if (_cameraTransform != null && _appliedShakeOffset != Vector3.zero)
            {
                _cameraTransform.position -= _appliedShakeOffset;
                _appliedShakeOffset = Vector3.zero;
            }
            _shakeOffset = Vector3.zero;

            // 确保时间恢复正常
            Time.timeScale = 1f;
        }


        private void Update()
        {
            if (!_enabled) return;
            UpdateCombo(Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null) return;

            // 先移除上一帧的震动偏移
            if (_appliedShakeOffset != Vector3.zero)
            {
                _cameraTransform.position -= _appliedShakeOffset;
                _appliedShakeOffset = Vector3.zero;
            }

            // 应用当前帧的震动偏移
            if (_shakeOffset != Vector3.zero)
            {
                _cameraTransform.position += _shakeOffset;
                _appliedShakeOffset = _shakeOffset;
            }
        }


        // ====================================================================
        // 1. 屏幕震动系统
        // ====================================================================

        /// <summary>
        /// 触发屏幕震动
        /// </summary>
        /// <param name="config">震动配置</param>
        public void TriggerScreenShake(ScreenShakeConfig config)
        {
            if (!_enabled || config == null || _cameraTransform == null) return;

            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
                _shakeOffset = Vector3.zero;
            }

            _shakeCoroutine = StartCoroutine(ScreenShakeCoroutine(config));
        }



        /// <summary>触发轻微震动</summary>
        public void ShakeLight() => TriggerScreenShake(_lightShake);

        /// <summary>触发中等震动</summary>
        public void ShakeMedium() => TriggerScreenShake(_mediumShake);

        /// <summary>触发强烈震动</summary>
        public void ShakeHeavy() => TriggerScreenShake(_heavyShake);

        /// <summary>触发超强震动</summary>
        public void ShakeEpic() => TriggerScreenShake(_epicShake);

        private IEnumerator ScreenShakeCoroutine(ScreenShakeConfig config)
        {
            float elapsed = 0f;

            while (elapsed < config.duration)
            {
                float progress = elapsed / config.duration;
                float currentIntensity = config.intensity * (1f - Mathf.Pow(progress, 1f / config.decayRate));

                // 只计算偏移量，不直接修改相机位置（由BattleCamera在LateUpdate中统一应用）
                float offsetX = Random.Range(-1f, 1f) * currentIntensity;
                float offsetY = Random.Range(-1f, 1f) * currentIntensity;
                _shakeOffset = new Vector3(offsetX, offsetY, 0f);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _shakeOffset = Vector3.zero;
            _shakeCoroutine = null;
        }



        /// <summary>获取当前震动偏移量（供其他系统查询）</summary>
        public Vector3 ShakeOffset => _shakeOffset;


        // ====================================================================
        // 2. 慢镜头系统
        // ====================================================================

        /// <summary>
        /// 触发慢镜头效果
        /// </summary>
        public void TriggerSlowMotion(SlowMotionConfig config)
        {
            if (!_enabled || config == null) return;

            if (_slowMotionCoroutine != null)
            {
                StopCoroutine(_slowMotionCoroutine);
            }

            _slowMotionCoroutine = StartCoroutine(SlowMotionCoroutine(config));
        }

        private IEnumerator SlowMotionCoroutine(SlowMotionConfig config)
        {
            // 缓存当前速度
            _cachedTimeScale = BattleInputHandler.HasInstance ? BattleInputHandler.Instance.GameSpeed : 1f;

            // 降速
            Time.timeScale = config.timeScale;
            Time.fixedDeltaTime = 0.02f * config.timeScale;

            // 持续
            float elapsed = 0f;
            while (elapsed < config.duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // 恢复过渡
            float recoveryElapsed = 0f;
            while (recoveryElapsed < config.recoveryTime)
            {
                recoveryElapsed += Time.unscaledDeltaTime;
                float t = recoveryElapsed / config.recoveryTime;
                Time.timeScale = Mathf.Lerp(config.timeScale, _cachedTimeScale, t);
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
                yield return null;
            }

            // 完全恢复
            Time.timeScale = _cachedTimeScale;
            Time.fixedDeltaTime = 0.02f * _cachedTimeScale;

            _slowMotionCoroutine = null;
        }

        // ====================================================================
        // 3. 帧冻结（HitStop）
        // ====================================================================

        /// <summary>
        /// 触发帧冻结（打击感核心）
        /// 命中瞬间短暂停顿，让玩家"感受"到打击力度
        /// </summary>
        /// <param name="freezeDuration">冻结时间（真实时间，推荐0.02~0.08秒）</param>
        public void TriggerHitFreeze(float freezeDuration = 0.04f)
        {
            if (!_enabled || freezeDuration <= 0f) return;

            if (_hitFreezeCoroutine != null)
            {
                StopCoroutine(_hitFreezeCoroutine);
                Time.timeScale = _cachedTimeScale;
            }

            _hitFreezeCoroutine = StartCoroutine(HitFreezeCoroutine(freezeDuration));
        }

        private IEnumerator HitFreezeCoroutine(float duration)
        {
            _cachedTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Time.timeScale = _cachedTimeScale;
            _hitFreezeCoroutine = null;
        }

        // ====================================================================
        // 4. 击杀连击系统
        // ====================================================================

        /// <summary>更新连击计时器</summary>
        private void UpdateCombo(float deltaTime)
        {
            if (_combo.Count <= 0) return;

            _combo.Timer -= deltaTime;
            if (_combo.Timer <= 0f)
            {
                // 连击断裂
                EventBus.Instance.Publish(new ComboBreakEvent { FinalCombo = _combo.Count });

                if (_combo.Count >= 10)
                {
                    Logger.D("BattleFeelSystem", "连击断裂! 最终连击: {0}", _combo.Count);
                }

                ResetCombo();
            }
        }

        /// <summary>增加连击数</summary>
        private void AddCombo(Vector3 position)
        {
            _combo.Count++;
            _combo.Timer = ComboTimeout;

            // 更新最高连击
            if (_combo.Count > _combo.MaxCombo)
            {
                _combo.MaxCombo = _combo.Count;
            }

            // 计算连击等级
            int newGrade = 1;
            for (int i = ComboGradeThresholds.Length - 1; i >= 0; i--)
            {
                if (_combo.Count >= ComboGradeThresholds[i])
                {
                    newGrade = i + 1;
                    break;
                }
            }

            // 连击升级时触发特殊效果
            if (newGrade > _combo.Grade)
            {
                _combo.Grade = newGrade;
                OnComboGradeUp(newGrade, position);
            }

            // 发布连击事件
            EventBus.Instance.Publish(new ComboEvent
            {
                ComboCount = _combo.Count,
                ComboGrade = _combo.Grade,
                Position = position
            });
        }

        /// <summary>连击升级时的特殊效果</summary>
        private void OnComboGradeUp(int grade, Vector3 position)
        {
            switch (grade)
            {
                case 2: // 不错（3连击）
                    ShakeLight();
                    break;
                case 3: // 精彩（8连击）
                    ShakeMedium();
                    break;
                case 4: // 完美（15连击）
                    ShakeHeavy();
                    TriggerSlowMotion(_comboSloMo);
                    break;
                case 5: // 传说（25连击）
                    ShakeEpic();
                    TriggerSlowMotion(_bossKillSloMo);
                    break;
            }

            Logger.D("BattleFeelSystem", "连击升级! {0}连击 等级={1}", _combo.Count, GetGradeName(grade));
        }

        /// <summary>重置连击</summary>
        private void ResetCombo()
        {
            _combo.Count = 0;
            _combo.Timer = 0f;
            _combo.Grade = 0;
        }

        /// <summary>获取连击等级名称</summary>
        public static string GetGradeName(int grade)
        {
            switch (grade)
            {
                case 1: return "普通";
                case 2: return "不错!";
                case 3: return "精彩!!";
                case 4: return "完美!!!";
                case 5: return "★传说★";
                default: return "";
            }
        }

        /// <summary>获取连击等级对应颜色</summary>
        public static Color GetGradeColor(int grade)
        {
            switch (grade)
            {
                case 1: return Color.white;
                case 2: return new Color(0.3f, 0.8f, 1f);       // 蓝
                case 3: return new Color(0.7f, 0.3f, 1f);       // 紫
                case 4: return new Color(1f, 0.6f, 0f);          // 橙
                case 5: return new Color(1f, 0.9f, 0.2f);        // 金
                default: return Color.white;
            }
        }

        // ====================================================================
        // 5. 操作反馈
        // ====================================================================

        /// <summary>
        /// 放塔成功反馈
        /// </summary>
        public void OnTowerPlaced(Vector3 position)
        {
            if (!_enabled) return;

            // 轻微震动
            ShakeLight();

            // 缩放弹跳效果（通过动画曲线）
            // 后续接入DoTween或自定义动画

            Logger.D("BattleFeelSystem", "放塔反馈 @{0}", position);
        }

        /// <summary>
        /// 塔升级反馈
        /// </summary>
        public void OnTowerUpgraded(Vector3 position, int newLevel)
        {
            if (!_enabled) return;

            if (newLevel >= 3)
            {
                ShakeMedium(); // 满级升级更强烈
            }
            else
            {
                ShakeLight();
            }
        }

        /// <summary>
        /// 塔出售反馈
        /// </summary>
        public void OnTowerSold(Vector3 position)
        {
            if (!_enabled) return;
            // 轻微震动 + 金币飞出效果
            ShakeLight();
        }

        /// <summary>
        /// 基地受损反馈
        /// </summary>
        public void OnBaseDamaged(float remainingHPPercent)
        {
            if (!_enabled) return;

            ShakeHeavy();

            // 低血量时屏幕边缘红色警告
            if (remainingHPPercent < 0.3f)
            {
                // 后续接入UI红色边框闪烁
                Logger.D("BattleFeelSystem", "⚠️ 基地血量警告: {0:P0}", remainingHPPercent);
            }
        }

        /// <summary>
        /// 波次开始反馈
        /// </summary>
        public void OnNewWaveStart(bool isBoss, bool isElite)
        {
            if (!_enabled) return;

            if (isBoss)
            {
                ShakeHeavy();
                // Boss波慢镜头预告
                TriggerSlowMotion(new SlowMotionConfig
                {
                    timeScale = 0.5f, duration = 0.3f, recoveryTime = 0.2f
                });
            }
            else if (isElite)
            {
                ShakeMedium();
            }
        }

        // ====================================================================
        // 6. 事件处理
        // ====================================================================

        /// <summary>怪物击杀事件</summary>
        private void OnEnemyKilled(EnemyDeathEvent evt)
        {
            // 连击
            AddCombo(evt.Position);

            // Boss击杀特殊反馈
            if (evt.IsBoss)
            {
                ShakeEpic();
                TriggerSlowMotion(_bossKillSloMo);
                TriggerHitFreeze(0.08f);

                EventBus.Instance.Publish(new BossKillEvent
                {
                    BossType = evt.EnemyType,
                    Position = evt.Position
                });

                Logger.I("BattleFeelSystem", "🎉 Boss击杀! 慢镜头+超强震动");
            }
            else
            {
                // 普通击杀的帧冻结（非常短暂，提升打击感）
                if (_combo.Count % 5 == 0 && _combo.Count > 0)
                {
                    TriggerHitFreeze(0.03f); // 每5连击触发一次微型帧冻结
                }
            }
        }

        /// <summary>塔攻击事件</summary>
        private void OnTowerAttack(TowerAttackEvent evt)
        {
            // 根据塔类型触发不同强度的震动
            switch (evt.TowerType)
            {
                case TowerType.Cannon:
                    ShakeMedium(); // 炮塔：中等震动
                    TriggerHitFreeze(0.02f); // 微型帧冻结
                    break;
                case TowerType.Mage:
                    ShakeLight(); // 法塔：轻微震动
                    break;
                // 箭塔、冰塔、毒塔不触发震动（频率太高会不舒服）
            }
        }

        /// <summary>波次开始事件</summary>
        private void OnWaveStart(WaveStartEvent evt)
        {
            OnNewWaveStart(evt.IsBoss, evt.IsElite);
        }

        /// <summary>战斗结束事件</summary>
        private void OnBattleEnd(BattleResultEvent evt)
        {
            if (evt.IsVictory)
            {
                ShakeEpic();
                TriggerSlowMotion(new SlowMotionConfig
                {
                    timeScale = 0.3f, duration = 0.8f, recoveryTime = 0.5f
                });
            }
            else
            {
                ShakeHeavy();
            }

            Logger.I("BattleFeelSystem", "本局最高连击: {0}", _combo.MaxCombo);
        }

        // ====================================================================
        // 调试
        // ====================================================================

        public string GetDebugInfo()
        {
            return $"连击:{_combo.Count} 等级:{GetGradeName(_combo.Grade)} " +
                   $"最高:{_combo.MaxCombo} 计时:{_combo.Timer:F1}s";
        }
    }
}
