// ============================================================
// 文件名：BattleAudioFeedback.cs
// 功能描述：战斗音效反馈系统
//          攻击音效、击杀音效、波次提示音、连击音效、UI操作音
//          让战斗音效与手感系统配合，提升战斗爽感
// 创建时间：2026-03-25
// 所属模块：Battle/Polish
// 对应交互：阶段三 #171-195（战斗手感调整——音效配合）
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Tower;
using AetheraSurvivors.Battle.Wave;
using AetheraSurvivors.Battle.Rune;
using Logger = AetheraSurvivors.Framework.Logger;


namespace AetheraSurvivors.Battle.Polish

{
    // ====================================================================
    // 音效枚举
    // ====================================================================

    /// <summary>战斗音效类型枚举（用于音效管理和预加载）</summary>
    public enum BattleSFX
    {
        // ---- 攻击音效 ----
        ArrowShoot,         // 箭塔射击
        MagicCast,          // 法塔释放
        IceBlast,           // 冰塔攻击
        CannonFire,         // 炮塔开火
        PoisonSpit,         // 毒塔喷射

        // ---- 命中音效 ----
        HitPhysical,        // 物理命中
        HitMagical,         // 魔法命中
        HitIce,             // 冰冻命中
        HitExplosion,       // 爆炸命中
        HitPoison,          // 中毒命中
        HitCritical,        // 暴击命中

        // ---- 击杀音效 ----
        EnemyDeath,         // 普通怪物死亡
        EliteDeath,         // 精英死亡
        BossDeath,          // Boss死亡

        // ---- 连击音效 ----
        ComboHit,           // 连击计数（升调）
        ComboGradeUp,       // 连击等级提升
        ComboBreak,         // 连击断裂

        // ---- 波次音效 ----
        WaveStart,          // 波次开始
        WaveComplete,       // 波次完成
        EliteWaveWarning,   // 精英波预警
        BossWaveWarning,    // Boss波预警
        AllWavesCleared,    // 全部波次清除

        // ---- 操作音效 ----
        TowerPlace,         // 放塔
        TowerUpgrade,       // 升级
        TowerSell,          // 出售
        TowerSelect,        // 选中塔
        TowerMaxLevel,      // 满级升级
        GoldEarned,         // 获得金币
        RuneSelect,         // 选择词条
        ButtonClick,        // 按钮点击
        SpeedToggle,        // 加速切换
        Pause,              // 暂停

        // ---- 基地相关 ----
        BaseDamaged,        // 基地受损
        BaseLowHP,          // 基地低血量警告
        BaseDestroyed,      // 基地被摧毁

        // ---- 战斗结果 ----
        Victory,            // 胜利
        Defeat,             // 失败
    }

    // ====================================================================
    // BattleAudioFeedback 核心类
    // ====================================================================

    /// <summary>
    /// 战斗音效反馈系统
    /// 
    /// 设计原则：
    /// 1. 音效节流：同一音效在短时间内不重复播放（防止叠加刺耳）
    /// 2. 音调随机化：每次播放音效微调音调（±5%），避免机械感
    /// 3. 连击音效升调：连击数越高音调越高，增强爽感
    /// 4. 优先级系统：Boss/连击音效优先于普通攻击音效
    /// 5. 空间音效：攻击/命中音效有位置感（左右声道）
    /// </summary>
    public class BattleAudioFeedback : MonoSingleton<BattleAudioFeedback>
    {
        // ========== 配置 ==========

        /// <summary>全局战斗音效音量（0~1）</summary>
        [SerializeField] private float _sfxVolume = 0.8f;

        /// <summary>同一音效最短间隔（防叠加）</summary>
        private const float SFXCooldown = 0.05f;

        /// <summary>音调随机化范围（±百分比）</summary>
        private const float PitchVariation = 0.05f;

        /// <summary>连击音调基础值</summary>
        private const float ComboPitchBase = 0.8f;

        /// <summary>连击音调每级增量</summary>
        private const float ComboPitchStep = 0.05f;

        /// <summary>连击音调最大值</summary>
        private const float ComboPitchMax = 1.5f;

        // ========== 运行时数据 ==========

        /// <summary>音效冷却时间戳</summary>
        private readonly System.Collections.Generic.Dictionary<BattleSFX, float> _sfxCooldowns
            = new System.Collections.Generic.Dictionary<BattleSFX, float>();

        /// <summary>音效名称映射（枚举 → AudioClip名称）</summary>
        private readonly System.Collections.Generic.Dictionary<BattleSFX, string> _sfxNames
            = new System.Collections.Generic.Dictionary<BattleSFX, string>();

        /// <summary>上一次基地低血量警告时间</summary>
        private float _lastBaseLowHPWarningTime = -999f;

        // ========== 公共属性 ==========

        /// <summary>战斗音效音量</summary>
        public float SFXVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Mathf.Clamp01(value);
        }

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            InitSFXNames();

            // 订阅事件
            EventBus.Instance.Subscribe<TowerAttackEvent>(OnTowerAttack);
            EventBus.Instance.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
            EventBus.Instance.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged);
            EventBus.Instance.Subscribe<WaveStartEvent>(OnWaveStart);
            EventBus.Instance.Subscribe<WaveCompleteEvent>(OnWaveComplete);
            EventBus.Instance.Subscribe<AllWavesClearedEvent>(OnAllWavesCleared);
            EventBus.Instance.Subscribe<TowerUpgradedEvent>(OnTowerUpgraded);
            EventBus.Instance.Subscribe<TowerSoldEvent>(OnTowerSold);
            EventBus.Instance.Subscribe<TowerSelectedEvent>(OnTowerSelected);
            EventBus.Instance.Subscribe<ComboEvent>(OnComboHit);
            EventBus.Instance.Subscribe<ComboBreakEvent>(OnComboBreak);
            EventBus.Instance.Subscribe<BossKillEvent>(OnBossKill);
            EventBus.Instance.Subscribe<BattleResultEvent>(OnBattleResult);
            EventBus.Instance.Subscribe<RuneSelectedEvent>(OnRuneSelected);

            EventBus.Instance.Subscribe<EnemyReachedBaseEvent>(OnEnemyReachedBase);

            Logger.I("BattleAudioFeedback", "战斗音效反馈系统初始化");
        }

        protected override void OnDispose()
        {
            EventBus.Instance.Unsubscribe<TowerAttackEvent>(OnTowerAttack);
            EventBus.Instance.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
            EventBus.Instance.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
            EventBus.Instance.Unsubscribe<WaveStartEvent>(OnWaveStart);
            EventBus.Instance.Unsubscribe<WaveCompleteEvent>(OnWaveComplete);
            EventBus.Instance.Unsubscribe<AllWavesClearedEvent>(OnAllWavesCleared);
            EventBus.Instance.Unsubscribe<TowerUpgradedEvent>(OnTowerUpgraded);
            EventBus.Instance.Unsubscribe<TowerSoldEvent>(OnTowerSold);
            EventBus.Instance.Unsubscribe<TowerSelectedEvent>(OnTowerSelected);
            EventBus.Instance.Unsubscribe<ComboEvent>(OnComboHit);
            EventBus.Instance.Unsubscribe<ComboBreakEvent>(OnComboBreak);
            EventBus.Instance.Unsubscribe<BossKillEvent>(OnBossKill);
            EventBus.Instance.Unsubscribe<BattleResultEvent>(OnBattleResult);
            EventBus.Instance.Unsubscribe<RuneSelectedEvent>(OnRuneSelected);

            EventBus.Instance.Unsubscribe<EnemyReachedBaseEvent>(OnEnemyReachedBase);
        }

        // ========== 核心方法：播放音效 ==========

        /// <summary>
        /// 播放战斗音效
        /// </summary>
        /// <param name="sfx">音效类型</param>
        /// <param name="volume">音量（-1=使用默认）</param>
        /// <param name="pitchOverride">音调覆盖（-1=使用默认+随机）</param>
        public void PlaySFX(BattleSFX sfx, float volume = -1f, float pitchOverride = -1f)
        {
            // 冷却检查
            if (_sfxCooldowns.TryGetValue(sfx, out float lastTime))
            {
                if (Time.unscaledTime - lastTime < SFXCooldown)
                    return; // 冷却中，跳过
            }

            _sfxCooldowns[sfx] = Time.unscaledTime;

            // 获取音效名称
            if (!_sfxNames.TryGetValue(sfx, out string clipName))
            {
                clipName = sfx.ToString();
            }

            // 音量
            float finalVolume = volume >= 0 ? volume : _sfxVolume;

            // 音调随机化
            float finalPitch = pitchOverride >= 0 ? pitchOverride
                : 1f + Random.Range(-PitchVariation, PitchVariation);

            // 通过AudioManager播放
            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.PlaySFX(clipName, finalVolume);
                // 注意：AudioManager目前不支持pitch参数，后续可扩展
            }
        }

        /// <summary>
        /// 播放连击音效（自动根据连击数调整音调）
        /// </summary>
        /// <param name="comboCount">当前连击数</param>
        private void PlayComboSFX(int comboCount)
        {
            float pitch = ComboPitchBase + comboCount * ComboPitchStep;
            pitch = Mathf.Min(pitch, ComboPitchMax);

            PlaySFX(BattleSFX.ComboHit, _sfxVolume * 0.6f, pitch);
        }

        // ========== 事件处理 ==========

        /// <summary>塔攻击 → 播放对应塔的射击音效</summary>
        private void OnTowerAttack(TowerAttackEvent evt)
        {
            switch (evt.TowerType)
            {
                case TowerType.Archer: PlaySFX(BattleSFX.ArrowShoot, _sfxVolume * 0.5f); break;
                case TowerType.Mage: PlaySFX(BattleSFX.MagicCast, _sfxVolume * 0.6f); break;
                case TowerType.Ice: PlaySFX(BattleSFX.IceBlast, _sfxVolume * 0.5f); break;
                case TowerType.Cannon: PlaySFX(BattleSFX.CannonFire, _sfxVolume * 0.8f); break;
                case TowerType.Poison: PlaySFX(BattleSFX.PoisonSpit, _sfxVolume * 0.5f); break;
            }
        }

        /// <summary>怪物受伤 → 播放命中音效</summary>
        private void OnEnemyDamaged(EnemyDamagedEvent evt)
        {
            if (evt.IsDodged) return; // 闪避不播放命中音

            if (evt.IsCritical)
            {
                PlaySFX(BattleSFX.HitCritical, _sfxVolume * 0.9f);
            }
            else
            {
                switch (evt.DamageType)
                {
                    case DamageType.Physical: PlaySFX(BattleSFX.HitPhysical, _sfxVolume * 0.4f); break;
                    case DamageType.Magical: PlaySFX(BattleSFX.HitMagical, _sfxVolume * 0.4f); break;
                    case DamageType.True: PlaySFX(BattleSFX.HitPhysical, _sfxVolume * 0.5f); break;
                }
            }
        }

        /// <summary>怪物死亡 → 播放死亡音效</summary>
        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            if (evt.IsBoss)
            {
                // Boss死亡音效在OnBossKill中处理
                return;
            }

            PlaySFX(BattleSFX.EnemyDeath, _sfxVolume * 0.6f);
            PlaySFX(BattleSFX.GoldEarned, _sfxVolume * 0.3f); // 金币获取声
        }

        /// <summary>Boss击杀 → 特殊音效</summary>
        private void OnBossKill(BossKillEvent evt)
        {
            PlaySFX(BattleSFX.BossDeath, _sfxVolume);
        }

        /// <summary>连击 → 播放升调音效</summary>
        private void OnComboHit(ComboEvent evt)
        {
            if (evt.ComboCount > 1)
            {
                PlayComboSFX(evt.ComboCount);
            }
        }

        /// <summary>连击断裂</summary>
        private void OnComboBreak(ComboBreakEvent evt)
        {
            if (evt.FinalCombo >= 5)
            {
                PlaySFX(BattleSFX.ComboBreak, _sfxVolume * 0.5f);
            }
        }

        /// <summary>波次开始</summary>
        private void OnWaveStart(WaveStartEvent evt)
        {
            if (evt.IsBoss)
            {
                PlaySFX(BattleSFX.BossWaveWarning, _sfxVolume);
            }
            else if (evt.IsElite)
            {
                PlaySFX(BattleSFX.EliteWaveWarning, _sfxVolume * 0.8f);
            }
            else
            {
                PlaySFX(BattleSFX.WaveStart, _sfxVolume * 0.6f);
            }
        }

        /// <summary>波次完成</summary>
        private void OnWaveComplete(WaveCompleteEvent evt)
        {
            PlaySFX(BattleSFX.WaveComplete, _sfxVolume * 0.7f);
        }

        /// <summary>全部波次完成</summary>
        private void OnAllWavesCleared(AllWavesClearedEvent evt)
        {
            PlaySFX(BattleSFX.AllWavesCleared, _sfxVolume);
        }

        /// <summary>塔升级</summary>
        private void OnTowerUpgraded(TowerUpgradedEvent evt)
        {
            if (evt.NewLevel >= 3)
            {
                PlaySFX(BattleSFX.TowerMaxLevel, _sfxVolume * 0.8f);
            }
            else
            {
                PlaySFX(BattleSFX.TowerUpgrade, _sfxVolume * 0.7f);
            }
        }

        /// <summary>塔出售</summary>
        private void OnTowerSold(TowerSoldEvent evt)
        {
            PlaySFX(BattleSFX.TowerSell, _sfxVolume * 0.6f);
            PlaySFX(BattleSFX.GoldEarned, _sfxVolume * 0.4f);
        }

        /// <summary>塔选中</summary>
        private void OnTowerSelected(TowerSelectedEvent evt)
        {
            PlaySFX(BattleSFX.TowerSelect, _sfxVolume * 0.4f);
        }

        /// <summary>选择词条</summary>
        private void OnRuneSelected(RuneSelectedEvent evt)

        {
            PlaySFX(BattleSFX.RuneSelect, _sfxVolume * 0.7f);
        }

        /// <summary>怪物到达基地</summary>
        private void OnEnemyReachedBase(EnemyReachedBaseEvent evt)
        {
            PlaySFX(BattleSFX.BaseDamaged, _sfxVolume * 0.9f);

            // 低血量持续警告（每3秒最多一次）
            if (BaseHealth.HasInstance)
            {
                float hpPercent = (float)BaseHealth.Instance.CurrentHP / BaseHealth.Instance.MaxHP;
                if (hpPercent < 0.3f && Time.time - _lastBaseLowHPWarningTime > 3f)
                {
                    _lastBaseLowHPWarningTime = Time.time;
                    PlaySFX(BattleSFX.BaseLowHP, _sfxVolume * 0.8f);
                }
            }
        }

        /// <summary>战斗结果</summary>
        private void OnBattleResult(BattleResultEvent evt)
        {
            PlaySFX(evt.IsVictory ? BattleSFX.Victory : BattleSFX.Defeat, _sfxVolume);
        }

        // ========== 公共方法：UI操作音效 ==========

        /// <summary>播放按钮点击音效</summary>
        public void PlayButtonClick() => PlaySFX(BattleSFX.ButtonClick, _sfxVolume * 0.5f);

        /// <summary>播放放塔音效</summary>
        public void PlayTowerPlace() => PlaySFX(BattleSFX.TowerPlace, _sfxVolume * 0.7f);

        /// <summary>播放加速切换音效</summary>
        public void PlaySpeedToggle() => PlaySFX(BattleSFX.SpeedToggle, _sfxVolume * 0.5f);

        /// <summary>播放暂停音效</summary>
        public void PlayPause() => PlaySFX(BattleSFX.Pause, _sfxVolume * 0.5f);

        // ========== 初始化音效名称映射 ==========

        private void InitSFXNames()
        {
            // 音效名称映射（对应AudioManager中的clip名称）
            // 开发阶段使用占位名称，后续替换为正式音效文件名
            _sfxNames[BattleSFX.ArrowShoot] = "sfx_arrow_shoot";
            _sfxNames[BattleSFX.MagicCast] = "sfx_magic_cast";
            _sfxNames[BattleSFX.IceBlast] = "sfx_ice_blast";
            _sfxNames[BattleSFX.CannonFire] = "sfx_cannon_fire";
            _sfxNames[BattleSFX.PoisonSpit] = "sfx_poison_spit";

            _sfxNames[BattleSFX.HitPhysical] = "sfx_hit_physical";
            _sfxNames[BattleSFX.HitMagical] = "sfx_hit_magical";
            _sfxNames[BattleSFX.HitIce] = "sfx_hit_ice";
            _sfxNames[BattleSFX.HitExplosion] = "sfx_hit_explosion";
            _sfxNames[BattleSFX.HitPoison] = "sfx_hit_poison";
            _sfxNames[BattleSFX.HitCritical] = "sfx_hit_critical";

            _sfxNames[BattleSFX.EnemyDeath] = "sfx_enemy_death";
            _sfxNames[BattleSFX.EliteDeath] = "sfx_elite_death";
            _sfxNames[BattleSFX.BossDeath] = "sfx_boss_death";

            _sfxNames[BattleSFX.ComboHit] = "sfx_combo_hit";
            _sfxNames[BattleSFX.ComboGradeUp] = "sfx_combo_grade_up";
            _sfxNames[BattleSFX.ComboBreak] = "sfx_combo_break";

            _sfxNames[BattleSFX.WaveStart] = "sfx_wave_start";
            _sfxNames[BattleSFX.WaveComplete] = "sfx_wave_complete";
            _sfxNames[BattleSFX.EliteWaveWarning] = "sfx_elite_warning";
            _sfxNames[BattleSFX.BossWaveWarning] = "sfx_boss_warning";
            _sfxNames[BattleSFX.AllWavesCleared] = "sfx_all_waves_cleared";

            _sfxNames[BattleSFX.TowerPlace] = "sfx_tower_place";
            _sfxNames[BattleSFX.TowerUpgrade] = "sfx_tower_upgrade";
            _sfxNames[BattleSFX.TowerSell] = "sfx_tower_sell";
            _sfxNames[BattleSFX.TowerSelect] = "sfx_tower_select";
            _sfxNames[BattleSFX.TowerMaxLevel] = "sfx_tower_max_level";
            _sfxNames[BattleSFX.GoldEarned] = "sfx_gold_earned";
            _sfxNames[BattleSFX.RuneSelect] = "sfx_rune_select";
            _sfxNames[BattleSFX.ButtonClick] = "sfx_button_click";
            _sfxNames[BattleSFX.SpeedToggle] = "sfx_speed_toggle";
            _sfxNames[BattleSFX.Pause] = "sfx_pause";

            _sfxNames[BattleSFX.BaseDamaged] = "sfx_base_damaged";
            _sfxNames[BattleSFX.BaseLowHP] = "sfx_base_low_hp";
            _sfxNames[BattleSFX.BaseDestroyed] = "sfx_base_destroyed";

            _sfxNames[BattleSFX.Victory] = "sfx_victory";
            _sfxNames[BattleSFX.Defeat] = "sfx_defeat";
        }
    }
}
