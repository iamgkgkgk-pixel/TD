// ============================================================
// 文件名：EnemyVisualAnimator.cs
// 功能描述：怪物视觉动画组件（方案A：单图+代码驱动动画）
//   用纯代码实现以下视觉效果：
//   1. 行走动画 — 上下弹跳 + 左右微摆 + 朝向翻转
//   2. 受击反馈 — 短暂闪白 + 抖动
//   3. 死亡效果 — 缩小+淡出+弹跳落地
//   4. 冰冻状态 — 蓝色色调 + 停止弹跳
//   5. 中毒状态 — 绿色脉冲闪烁
//   6. 灼烧状态 — 橙红色闪烁
//   7. 眩晕状态 — 旋转晃动
//   8. 减速状态 — 弹跳频率降低
//   9. 阴影 — 脚底投影圆形阴影
//
//   设计思想：
//   - 不依赖Animator/Animation组件，0 AnimationClip开销
//   - 所有动画用 sin/cos + Mathf.Lerp 驱动
//   - 与EnemyBase松耦合：通过GetComponent获取，可选挂载
//   - 完全兼容后续SpriteSheet升级（届时替换弹跳为帧动画）
//
// 创建时间：2026-03-25
// 所属模块：Battle/Enemy
// 对应交互：#160 美术品质优化
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using System.Collections.Generic;

namespace AetheraSurvivors.Battle.Enemy
{

    /// <summary>
    /// 怪物视觉动画组件（方案A：代码驱动）
    /// 挂载到怪物GameObject后自动处理所有视觉动画效果。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class EnemyVisualAnimator : MonoBehaviour
    {
        // ====================================================================
        // 动画参数（可按怪物类型调整）
        // ====================================================================

        [Header("=== 行走弹跳 ===")]
        [Tooltip("弹跳幅度（世界坐标单位）")]
        [SerializeField] private float _bounceHeight = 0.10f;

        [Tooltip("弹跳频率（每秒几次，即步频）")]
        [SerializeField] private float _bounceFrequency = 3.5f;

        [Tooltip("左右倾斜角度（模拟迈步摇摆）")]
        [SerializeField] private float _tiltAngle = 8f;

        [Tooltip("行走时的挤压拉伸幅度（0~0.2）")]
        [SerializeField] private float _squashStretch = 0.10f;

        [Tooltip("行走时身体水平摇摆幅度（模拟重心转移）")]
        [SerializeField] private float _swayAmount = 0.03f;

        [Tooltip("行走时前倾角度（模拟前进姿态）")]
        [SerializeField] private float _leanForwardAngle = 3f;

        [Header("=== 腿部分离动画 ===")]
        [Tooltip("是否启用腿部分离动画（将sprite切成上下两半）")]
        [SerializeField] private bool _enableLegSplit = true;

        [Tooltip("腿部占sprite总高度的比例（0.3 = 下方30%是腿）")]
        [SerializeField] private float _legRatio = 0.35f;

        [Tooltip("腿部迈步旋转角度（左右交替）")]
        [SerializeField] private float _legSwingAngle = 15f;

        [Tooltip("腿部迈步频率倍率（相对于弹跳频率）")]
        [SerializeField] private float _legSwingFreqMultiplier = 1f;


        [Header("=== 受击闪烁 ===")]

        [Tooltip("受击闪白持续时间")]
        [SerializeField] private float _hitFlashDuration = 0.1f;

        [Tooltip("受击抖动幅度")]
        [SerializeField] private float _hitShakeIntensity = 0.05f;

        [Tooltip("受击抖动持续时间")]
        [SerializeField] private float _hitShakeDuration = 0.15f;

        [Header("=== 死亡动画 ===")]
        [Tooltip("死亡动画总时长")]
        [SerializeField] private float _deathDuration = 0.4f;

        [Tooltip("死亡时弹起高度")]
        [SerializeField] private float _deathBounceHeight = 0.3f;

        [Header("=== 状态色调 ===")]
        [Tooltip("冰冻色调")]
        [SerializeField] private Color _freezeColor = new Color(0.5f, 0.8f, 1f, 1f);

        [Tooltip("中毒色调")]
        [SerializeField] private Color _poisonColor = new Color(0.5f, 1f, 0.3f, 1f);

        [Tooltip("灼烧色调")]
        [SerializeField] private Color _burnColor = new Color(1f, 0.5f, 0.2f, 1f);

        [Tooltip("受击闪白色")]
        [SerializeField] private Color _hitFlashColor = Color.white;

        [Header("=== 帧动画 ===")]
        [Tooltip("帧动画播放速率（帧/秒）")]
        [SerializeField] private float _frameRate = 10f;

        // ====================================================================
        // 运行时引用
        // ====================================================================

        private SpriteRenderer _spriteRenderer;
        private EnemyBase _enemy;
        private Transform _spriteTransform;

        // --- 帧动画相关 ---
        /// <summary>是否使用帧动画模式（有SpriteSheet时为true）</summary>
        private bool _useSpriteSheet;

        /// <summary>SpriteSheet切割后的帧数组</summary>
        private Sprite[] _walkFrames;

        /// <summary>当前帧索引</summary>
        private int _currentFrameIndex;

        /// <summary>帧动画计时器</summary>
        private float _frameTimer;

        /// <summary>当前行进方向角度（用于俯视角旋转朝向）</summary>
        private float _facingAngle;

        /// <summary>是否使用帧动画模式（公开属性，供EnemyBase查询）</summary>
        public bool UseSpriteSheet => _useSpriteSheet;



        /// <summary>原始颜色（用于恢复）</summary>
        private Color _originalColor = Color.white;

        /// <summary>原始局部位置Y偏移</summary>
        private float _baseLocalY;

        /// <summary>原始缩放</summary>
        private Vector3 _baseScale;

        /// <summary>静态单帧Sprite（帧动画模式下保留，用于回退）</summary>
        private Sprite _staticSprite;

        // --- 腿部分离相关 ---

        /// <summary>是否已完成sprite切割</summary>
        private bool _legSplitDone;

        /// <summary>腿部子对象</summary>
        private GameObject _legsObj;

        /// <summary>腿部SpriteRenderer</summary>
        private SpriteRenderer _legsSpriteRenderer;

        /// <summary>腿部Transform</summary>
        private Transform _legsTransform;

        /// <summary>原始完整Sprite（切割前备份）</summary>
        private Sprite _originalFullSprite;

        /// <summary>上半身Sprite</summary>
        private Sprite _upperBodySprite;

        /// <summary>下半身（腿部）Sprite</summary>
        private Sprite _legsSprite;

        /// <summary>腿部pivot的本地Y偏移（腿部旋转中心）</summary>
        private float _legsPivotLocalY;

        /// <summary>上半身需要的Y偏移补偿</summary>
        private float _upperBodyYOffset;


        // ====================================================================
        // 动画状态机
        // ====================================================================

        /// <summary>弹跳相位（0~2π循环）</summary>
        private float _bouncePhase;

        /// <summary>是否正在行走（有速度）</summary>
        private bool _isWalking;

        /// <summary>上一帧位置（用于检测移动）</summary>
        private Vector3 _lastPosition;

        /// <summary>当前水平摇摆偏移（用于LateUpdate叠加）</summary>
        private float _currentSwayX;

        /// <summary>移动方向（用于前倾计算）</summary>
        private Vector3 _moveDirection;


        // --- 受击状态 ---
        private float _hitFlashTimer;
        private float _hitShakeTimer;
        private Vector3 _hitShakeOffset;

        // --- 死亡状态 ---
        private bool _isPlayingDeath;
        private float _deathTimer;
        private Vector3 _deathStartScale;
        private float _deathStartAlpha;

        // --- 阴影 ---
        private GameObject _shadowObj;
        private SpriteRenderer _shadowRenderer;
        private static Sprite _cachedShadowSprite;

        // --- Buff视觉状态 ---
        private bool _isFrozen;
        private bool _isPoisoned;
        private bool _isBurning;
        private bool _isStunned;
        private float _slowPercent;
        private float _buffColorPhase; // Buff闪烁相位

        // --- 受击缩放因子引用（从LEnemyHitFlash读取） ---
        private AetheraSurvivors.Battle.Visual.EnemyHitFlash _hitFlashRef;


        // --- 上一帧的视觉偏移（用于每帧开始时还原） ---
        private Vector3 _lastVisualOffset;

        // --- 已初始化标记 ---
        private bool _initialized;

        // ====================================================================
        // ★ 视觉增强：描边轮廓
        // ====================================================================
        private GameObject[] _outlineObjs;
        private SpriteRenderer[] _outlineRenderers;
        private const int OUTLINE_DIRECTIONS = 8;
        private const float OUTLINE_OFFSET = 0.015f;
        private static readonly Color ENEMY_OUTLINE_COLOR = new Color(0.05f, 0.03f, 0.02f, 0.65f);

        // ====================================================================
        // ★ 视觉增强：怪物类型光环
        // ====================================================================
        private GameObject _typeGlowObj;
        private SpriteRenderer _typeGlowRenderer;
        private float _typeGlowPhase;

        // ====================================================================
        // ★ 视觉增强：入场动画
        // ====================================================================
        private float _spawnAnimTimer = -1f;
        private const float SPAWN_ANIM_DURATION = 0.45f;

        // ====================================================================
        // ★ 视觉增强：低血量警告
        // ====================================================================
        private float _lowHPPulsePhase;
        private bool _isLowHP;
        private const float LOW_HP_THRESHOLD = 0.3f;

        // ====================================================================
        // ★ 视觉增强：Buff环绕粒子
        // ====================================================================
        private GameObject[] _buffParticles;
        private const int BUFF_PARTICLE_COUNT = 4;
        private float[] _buffParticlePhases;
        private float[] _buffParticleSpeeds;

        // ====================================================================
        // ★ 视觉增强：眼睛系统（让怪物更有生命感）
        // ====================================================================
        private GameObject _eyeLeftObj;
        private GameObject _eyeRightObj;
        private SpriteRenderer _eyeLeftRenderer;
        private SpriteRenderer _eyeRightRenderer;
        private float _blinkTimer;
        private float _nextBlinkTime;
        private bool _isBlinking;
        private const float BLINK_DURATION = 0.08f;
        private Vector3 _eyeLookOffset; // 眼睛看向方向的偏移

        // ====================================================================
        // ★ 视觉增强：Boss专属光柱
        // ====================================================================
        private GameObject _bossAuraObj;
        private SpriteRenderer _bossAuraRenderer;
        private float _bossAuraPhase;

        // ====================================================================
        // ★ 视觉增强：受击方向倾斜
        // ====================================================================
        private float _hitTiltAngle;
        private float _hitTiltTimer;
        private const float HIT_TILT_DURATION = 0.2f;

        // ====================================================================
        // ★ 缓存纹理
        // ====================================================================
        private static Texture2D _cachedCircleTex;
        private static Texture2D _cachedEyeTex;
        private static Sprite _cachedCircleSprite;
        private static Sprite _cachedEyeSprite;



        // ====================================================================
        // 初始化
        // ====================================================================

        /// <summary>
        /// 初始化动画器（由EnemyBase或EnemySpawner调用）
        /// </summary>
        /// <param name="originalColor">怪物的基础颜色</param>
        public void Initialize(Color originalColor)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _enemy = GetComponent<EnemyBase>();

            // 使用自身Transform作为动画目标
            _spriteTransform = transform;
            _originalColor = originalColor;
            _baseLocalY = 0f;
            _baseScale = _spriteTransform.localScale;
            _lastPosition = transform.position;

            // 重置所有动画状态
            _bouncePhase = Random.Range(0f, Mathf.PI * 2f); // 随机初始相位，避免所有怪物同步弹跳
            _hitFlashTimer = 0f;
            _hitShakeTimer = 0f;
            _hitShakeOffset = Vector3.zero;
            _isPlayingDeath = false;
            _deathTimer = 0f;
            _isFrozen = false;
            _isPoisoned = false;
            _isBurning = false;
            _isStunned = false;
            _slowPercent = 0f;
            _buffColorPhase = 0f;
            _currentSwayX = 0f;
            _moveDirection = Vector3.right;

            // 帧动画状态重置
            _currentFrameIndex = 0;
            _frameTimer = 0f;

            // 如果有帧动画，跳过腿部分离（帧动画已包含行走姿态）
            if (_useSpriteSheet && _walkFrames != null && _walkFrames.Length > 0)
            {
                _enableLegSplit = false; // 禁用腿部分离
                // 设置第一帧
                _spriteRenderer.sprite = _walkFrames[0];
                Debug.Log($"[EnemyVisualAnimator] 帧动画模式启用: {_walkFrames.Length}帧, 帧率={_frameRate}fps");
            }
            else
            {
                // 无帧动画，尝试切割sprite为上下两半（腿部分离）
                TrySplitSprite();
            }

            // 创建阴影
            CreateShadow();

            // ★ 创建增强视觉层
            CreateOutline();           // 描边轮廓
            CreateTypeGlow();          // 类型光环
            CreateEyes();              // 眼睛系统
            CreateBuffParticles();     // Buff环绕粒子
            if (_enemy != null && _enemy.IsBoss)
            {
                CreateBossAura();      // Boss专属光柱
            }

            // 触发入场动画
            _spawnAnimTimer = SPAWN_ANIM_DURATION;

            // 初始化眨眼计时
            _nextBlinkTime = Random.Range(2f, 5f);
            _blinkTimer = 0f;

            // 获取EnemyHitFlash引用（用于读取受击缩放因子）
            _hitFlashRef = GetComponent<AetheraSurvivors.Battle.Visual.EnemyHitFlash>();

            _initialized = true;


        }


        /// <summary>
        /// 设置帧动画数据（由EnemySpawner在Initialize之前调用）
        /// </summary>
        /// <param name="frames">SpriteSheet切割后的帧数组</param>
        /// <param name="fps">播放帧率（默认10fps）</param>
        public void SetWalkFrames(Sprite[] frames, float fps = 10f)
        {
            if (frames == null || frames.Length == 0)
            {
                _useSpriteSheet = false;
                _walkFrames = null;
                return;
            }

            _useSpriteSheet = true;
            _walkFrames = frames;
            _frameRate = fps;
            _currentFrameIndex = Random.Range(0, frames.Length); // 随机起始帧，避免同步
            _frameTimer = 0f;

            // 保存当前静态sprite作为备份
            if (_spriteRenderer != null)
            {
                _staticSprite = _spriteRenderer.sprite;
            }
        }


        // ====================================================================
        // Unity 生命周期
        // ====================================================================

        private void Update()
        {
            if (!_initialized) return;

            // ★ 每帧开始时先还原上一帧的视觉偏移，
            //   保证 EnemyBase.Update() 执行时读取的是「逻辑位置」
            //   （EnemyBase 的 Update 和本组件的 Update 执行顺序不确定，
            //    但我们在本组件 Update 开头还原、LateUpdate 叠加，
            //    即使本组件先执行也能保证正确）
            transform.position -= _lastVisualOffset;
            _lastVisualOffset = Vector3.zero;


            // 死亡动画优先级最高
            if (_isPlayingDeath)
            {
                UpdateDeathAnimation();
                return;
            }

            // 检测是否在移动
            Vector3 moveDelta = transform.position - _lastPosition;
            float deltaMove = moveDelta.sqrMagnitude;
            _isWalking = deltaMove > 0.0001f;
            if (_isWalking)
            {
                _moveDirection = moveDelta.normalized;
            }
            _lastPosition = transform.position;


            // 读取Buff状态
            UpdateBuffVisualState();

            // 更新各层动画
            if (_useSpriteSheet)
            {
                UpdateSpriteSheetAnimation();
            }
            else
            {
                UpdateWalkAnimation();
            }
            UpdateHitAnimation();
            UpdateColorTint();
            UpdateShadow();

            // ★ 更新增强视觉效果
            UpdateSpawnAnimation();
            UpdateOutlineSync();
            UpdateTypeGlow();
            UpdateEyes();
            UpdateLowHPWarning();
            UpdateBuffParticles();
            UpdateBossAura();
            UpdateHitTilt();
        }


        private void OnDestroy()
        {
            // 清理阴影对象
            if (_shadowObj != null)
                Destroy(_shadowObj);
            // 清理腿部子对象
            if (_legsObj != null)
                Destroy(_legsObj);
            // ★ 清理增强视觉对象
            SafeDestroyArray(_outlineObjs);
            SafeDestroy(_typeGlowObj);
            SafeDestroy(_eyeLeftObj);
            SafeDestroy(_eyeRightObj);
            SafeDestroy(_bossAuraObj);
            SafeDestroyArray(_buffParticles);
        }

        private void SafeDestroy(GameObject obj)
        {
            if (obj != null) Destroy(obj);
        }

        private void SafeDestroyArray(GameObject[] arr)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
                SafeDestroy(arr[i]);
        }



        // ====================================================================
        // 外部触发接口
        // ====================================================================

        /// <summary>
        /// 触发受击闪烁+抖动（由EnemyBase.OnDamaged调用）
        /// </summary>
        public void PlayHitEffect()
        {
            if (_isPlayingDeath) return;
            _hitFlashTimer = _hitFlashDuration;
            _hitShakeTimer = _hitShakeDuration;
        }

        /// <summary>
        /// 触发死亡动画（由EnemyBase.Die调用）
        /// </summary>
        /// <param name="onComplete">动画完成回调</param>
        public void PlayDeathAnimation(System.Action onComplete)
        {
            _isPlayingDeath = true;
            _deathTimer = 0f;
            _deathStartScale = _spriteTransform.localScale;
            _deathStartAlpha = _spriteRenderer.color.a;
            _deathCompleteCallback = onComplete;
        }

        private System.Action _deathCompleteCallback;

        // ====================================================================
        // 腿部分离（运行时切割Sprite）
        // ====================================================================

        /// <summary>
        /// 尝试将sprite切成上下两半：上半身 + 腿部
        /// 腿部作为独立子对象，可以独立旋转模拟迈步
        /// </summary>
        private void TrySplitSprite()
        {
            _legSplitDone = false;

            // 不启用腿部分离时跳过
            if (!_enableLegSplit) return;

            // 检查sprite是否有效
            if (_spriteRenderer == null || _spriteRenderer.sprite == null) return;

            Sprite fullSprite = _spriteRenderer.sprite;
            Texture2D tex = fullSprite.texture;

            // 占位sprite（1x1）不需要切割
            if (tex.width <= 2 || tex.height <= 2) return;

            // 检查纹理是否可读（isReadable）
            if (!tex.isReadable)
            {
                // 纹理不可读时，无法切割，回退到整体动画
                Debug.LogWarning($"[EnemyVisualAnimator] 纹理不可读，无法切割腿部: {tex.name}，回退到整体动画");
                _enableLegSplit = false;
                return;
            }

            // 确保纹理过滤模式正确（避免缩小显示时像素化/锯齿）
            if (tex.filterMode == FilterMode.Point)
            {
                tex.filterMode = FilterMode.Bilinear;
            }


            _originalFullSprite = fullSprite;

            // 计算切割位置（基于sprite的rect，而非整个纹理）
            Rect spriteRect = fullSprite.rect;
            float ppu = fullSprite.pixelsPerUnit;
            int spriteW = (int)spriteRect.width;
            int spriteH = (int)spriteRect.height;
            int spriteX = (int)spriteRect.x;
            int spriteY = (int)spriteRect.y;

            // 腿部高度（像素）
            int legPixelHeight = Mathf.Max(1, Mathf.RoundToInt(spriteH * _legRatio));
            int upperPixelHeight = spriteH - legPixelHeight;

            if (upperPixelHeight < 2 || legPixelHeight < 2) return;

            // === 创建上半身Sprite ===
            // 上半身：从sprite底部 + legPixelHeight 开始，到顶部
            Rect upperRect = new Rect(spriteX, spriteY + legPixelHeight, spriteW, upperPixelHeight);
            // pivot在底部中心（这样上半身的底边对齐腿部顶边）
            Vector2 upperPivot = new Vector2(0.5f, 0f);
            _upperBodySprite = Sprite.Create(tex, upperRect, upperPivot, ppu);
            _upperBodySprite.name = tex.name + "_upper";

            // === 创建腿部Sprite ===
            // 腿部：从sprite底部开始，高度为legPixelHeight
            Rect legsRect = new Rect(spriteX, spriteY, spriteW, legPixelHeight);
            // pivot在顶部中心（这样旋转时围绕腰部旋转）
            Vector2 legsPivot = new Vector2(0.5f, 1f);
            _legsSprite = Sprite.Create(tex, legsRect, legsPivot, ppu);
            _legsSprite.name = tex.name + "_legs";

            // === 计算切割连接点（腰部）在原始sprite本地坐标中的Y位置 ===
            // 原始sprite的pivot是(0.5, 0.5)，即中心点在(0,0)
            // sprite底部在 Y = -fullSpriteWorldH/2
            // 腰部（切割线）在底部 + legWorldH
            float fullSpriteWorldH = spriteH / ppu;
            float legWorldH = legPixelHeight / ppu;
            float waistLocalY = -fullSpriteWorldH * 0.5f + legWorldH;

            // === 设置上半身 ===
            _spriteRenderer.sprite = _upperBodySprite;
            // 上半身pivot在底部(0.5, 0)，意味着sprite从pivot点向上渲染
            // 我们需要上半身的底边（pivot点）对齐到腰部位置
            // 但主SpriteRenderer的位置就是transform.position（不能改）
            // 所以不能直接移动，而是通过SpriteRenderer的偏移来补偿
            // 
            // 原始sprite pivot(0.5,0.5)时，中心在(0,0)，腰部在waistLocalY
            // 新的上半身pivot(0.5,0)时，pivot点（底边）需要在waistLocalY处
            // 但SpriteRenderer默认把pivot点放在localPosition(0,0)
            // 所以上半身实际需要的偏移 = waistLocalY（让底边对齐到腰部）
            // 不过这里不移动主transform，而是在LateUpdate中通过视觉偏移处理
            _upperBodyYOffset = waistLocalY;

            // === 创建腿部独立对象（不作为子对象，避免受父对象视觉偏移影响） ===
            _legsObj = new GameObject("Legs");
            _legsObj.transform.SetParent(null); // 独立对象，不跟随父对象
            _legsTransform = _legsObj.transform;

            _legsSpriteRenderer = _legsObj.AddComponent<SpriteRenderer>();
            _legsSpriteRenderer.sprite = _legsSprite;
            _legsSpriteRenderer.color = _spriteRenderer.color;
            _legsSpriteRenderer.sortingOrder = _spriteRenderer.sortingOrder - 1; // 腿在身体后面
            _legsSpriteRenderer.flipX = _spriteRenderer.flipX;

            // 腿部pivot在顶部(0.5, 1)，意味着sprite从pivot点向下渲染
            // 腿部位置在LateUpdate中手动同步
            _legsTransform.position = transform.position + new Vector3(0f, waistLocalY, 0f);
            _legsTransform.localScale = transform.localScale; // 与怪物保持同样缩放
            _legsPivotLocalY = waistLocalY;



            _legSplitDone = true;

            Debug.Log($"[EnemyVisualAnimator] ✅ Sprite腿部分离成功: {tex.name}, " +
                $"上半身={upperPixelHeight}px, 腿部={legPixelHeight}px, 腰部Y={waistLocalY:F3}");
        }

        // ====================================================================
        // 帧动画播放（SpriteSheet模式）
        // ====================================================================

        /// <summary>
        /// 更新SpriteSheet帧动画
        /// 行走时播放帧动画 + 轻微上下浮动增加动感
        /// 停止时显示第一帧 + idle呼吸
        /// </summary>
        private void UpdateSpriteSheetAnimation()
        {
            if (_walkFrames == null || _walkFrames.Length == 0) return;

            if (_isFrozen || _isStunned)
            {
                // 冰冻/眩晕时停止帧动画
                if (_isStunned && !_isFrozen)
                {
                    // 眩晕时做旋转晃动
                    float stunAngle = Mathf.Sin(Time.time * 8f) * 15f;
                    _spriteTransform.localRotation = Quaternion.Euler(0f, 0f, stunAngle);
                }
                else
                {
                    _spriteTransform.localRotation = Quaternion.identity;
                }

                ApplyVerticalOffset(0f);
                _currentSwayX = 0f;
                ApplyBaseScale();
                return;
            }


            if (!_isWalking)
            {
                // 停止时：显示第一帧 + 轻微呼吸动画
                _spriteRenderer.sprite = _walkFrames[0];
                float idleBreathe = Mathf.Sin(Time.time * 1.5f) * 0.01f;
                ApplyVerticalOffset(idleBreathe);
                _currentSwayX = 0f;
                _spriteTransform.localRotation = Quaternion.identity;

                float breatheScale = 1f + Mathf.Sin(Time.time * 1.5f) * 0.015f;
                ApplyScale(new Vector3(
                    _baseScale.x * breatheScale,
                    _baseScale.y * (2f - breatheScale),
                    _baseScale.z
                ));

                return;
            }


            // === 行走中：播放帧动画 ===

            // 根据减速调整帧率
            float effectiveFps = _frameRate * (1f - _slowPercent * 0.5f);
            effectiveFps = Mathf.Max(effectiveFps, 3f); // 最低3fps

            // 更新帧计时器
            _frameTimer += Time.deltaTime * effectiveFps;
            if (_frameTimer >= 1f)
            {
                int framesToAdvance = (int)_frameTimer;
                _currentFrameIndex = (_currentFrameIndex + framesToAdvance) % _walkFrames.Length;
                _frameTimer -= framesToAdvance;
            }

            // 设置当前帧
            _spriteRenderer.sprite = _walkFrames[_currentFrameIndex];

            // 轻微上下浮动增加动感（幅度比纯弹跳模式小很多）
            float bouncePhase = Time.time * _bounceFrequency * Mathf.PI * 2f + _bouncePhase;
            float microBounce = Mathf.Abs(Mathf.Sin(bouncePhase)) * _bounceHeight * 0.3f;
            ApplyVerticalOffset(microBounce);

            // 保持正常缩放
            ApplyBaseScale();


            // 帧动画模式：不旋转sprite，朝向由flipX控制（侧面视角精灵图）
            _spriteTransform.localRotation = Quaternion.identity;
            _currentSwayX = 0f;

        }

        /// <summary>
        /// 更新俯视角朝向（由EnemyBase调用，替代flipX）
        /// 根据行进方向旋转sprite，使角色面朝行进方向
        /// </summary>
        /// <param name="direction">行进方向向量</param>
        public void UpdateFacingAngle(Vector3 direction)
        {
            if (!_useSpriteSheet) return;
            if (direction.sqrMagnitude < 0.001f) return;

            // 计算目标角度（Unity 2D中，0度=右，90度=上）
            // spritesheet中角色默认面朝下（俯视角），所以需要+90度偏移
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;

            // 平滑旋转（避免突然转向）
            _facingAngle = Mathf.LerpAngle(_facingAngle, targetAngle, Time.deltaTime * 10f);

            // 只在非冰冻/眩晕状态下应用旋转
            if (!_isFrozen && !_isStunned)
            {
                _spriteTransform.localRotation = Quaternion.Euler(0f, 0f, _facingAngle);
            }
        }


        // ====================================================================
        // 行走弹跳动画（无SpriteSheet时的回退方案）
        // ====================================================================

        private void UpdateWalkAnimation()

        {
            if (_isFrozen || _isStunned)
            {
                // 冰冻/眩晕时停止弹跳，但眩晕时做旋转
                if (_isStunned && !_isFrozen)
                {
                    // 眩晕旋转
                    float stunAngle = Mathf.Sin(Time.time * 8f) * 15f;
                    _spriteTransform.localRotation = Quaternion.Euler(0f, 0f, stunAngle);
                }
                else
                {
                    _spriteTransform.localRotation = Quaternion.identity;
                }

                // 重置Y偏移和水平摇摆
                ApplyVerticalOffset(0f);
                _currentSwayX = 0f;
                ApplyBaseScale();

                // 重置腿部
                if (_legSplitDone && _legsTransform != null)

                {
                    _legsTransform.rotation = Quaternion.identity;
                }

                return;
            }

            if (!_isWalking)
            {
                // 停止时保持微弱的idle呼吸动画
                float idleBreathe = Mathf.Sin(Time.time * 1.5f) * 0.015f;
                ApplyVerticalOffset(idleBreathe);
                _currentSwayX = 0f;
                _spriteTransform.localRotation = Quaternion.identity;

                // idle时轻微缩放呼吸
                float breatheScale = 1f + Mathf.Sin(Time.time * 1.5f) * 0.02f;
                ApplyScale(new Vector3(
                    _baseScale.x * breatheScale,
                    _baseScale.y * (2f - breatheScale), // 反向，做呼吸感
                    _baseScale.z
                ));


                // 停止时腿部回正
                if (_legSplitDone && _legsTransform != null)
                {
                    // 平滑回正
                    Quaternion current = _legsTransform.rotation;
                    _legsTransform.rotation = Quaternion.Lerp(current, Quaternion.identity, Time.deltaTime * 8f);
                }

                return;
            }

            // ===== 行走动画 — 模拟迈步效果 =====

            // 根据减速调整步频
            float effectiveFreq = _bounceFrequency * (1f - _slowPercent * 0.5f);
            effectiveFreq = Mathf.Max(effectiveFreq, 1f); // 最低1Hz

            // 更新弹跳相位（一个完整周期 = 左脚+右脚各一步）
            _bouncePhase += Time.deltaTime * effectiveFreq * Mathf.PI * 2f;
            if (_bouncePhase > Mathf.PI * 100f) _bouncePhase -= Mathf.PI * 100f;

            float sinVal = Mathf.Sin(_bouncePhase);
            float absSin = Mathf.Abs(sinVal);

            // === 腿部分离模式 vs 整体模式 ===
            if (_legSplitDone && _legsTransform != null)
            {
                // ★★★ 腿部分离模式：上半身轻微晃动，腿部大幅摆动 ★★★

                // --- 上半身动画（轻微弹跳+微摇） ---
                // 弹跳幅度减半（因为腿部在动，上半身不需要太大弹跳）
                float bounce = Mathf.Pow(absSin, 0.7f) * _bounceHeight * 0.5f;
                ApplyVerticalOffset(bounce);

                // 上半身轻微摇摆（幅度减小，因为主要动感来自腿部）
                float tilt = sinVal * _tiltAngle * 0.4f;
                float leanAngle = _leanForwardAngle;
                _spriteTransform.localRotation = Quaternion.Euler(0f, 0f, tilt + leanAngle);

                // 水平摇摆（减小）
                _currentSwayX = sinVal * _swayAmount * 0.5f;

                // 上半身挤压拉伸（减小）
                float normalizedBounce = bounce / Mathf.Max(_bounceHeight * 0.5f, 0.001f);
                float groundContact = 1f - normalizedBounce;
                float squashX = 1f + _squashStretch * groundContact * 0.5f;
                float squashY = 1f - _squashStretch * groundContact * 0.4f;
                ApplyScale(new Vector3(
                    _baseScale.x * squashX,
                    _baseScale.y * squashY,
                    _baseScale.z
                ));


                // --- 腿部动画（大幅前后摆动，模拟迈步） ---
                float legPhase = _bouncePhase * _legSwingFreqMultiplier;

                // 腿部用sin做前后摆动，正值=前踢，负值=后摆
                float legAngle = Mathf.Sin(legPhase) * _legSwingAngle;
                _legsTransform.rotation = Quaternion.Euler(0f, 0f, legAngle);

            }
            else
            {
                // ★★★ 整体模式（无腿部分离，保持原有动画） ★★★

                // === 1. 弹跳高度 ===
                float bounce = Mathf.Pow(absSin, 0.7f) * _bounceHeight;
                ApplyVerticalOffset(bounce);

                // === 2. 左右摇摆倾斜 ===
                float tilt = sinVal * _tiltAngle;
                float leanAngle = _leanForwardAngle;
                _spriteTransform.localRotation = Quaternion.Euler(0f, 0f, tilt + leanAngle);

                // === 3. 水平摇摆 ===
                _currentSwayX = sinVal * _swayAmount;

                // === 4. 挤压与拉伸 ===
                float normalizedBounce = bounce / Mathf.Max(_bounceHeight, 0.001f);
                float groundContact = 1f - normalizedBounce;
                float squashX = 1f + _squashStretch * groundContact;
                float squashY = 1f - _squashStretch * groundContact * 0.8f;

                ApplyScale(new Vector3(
                    _baseScale.x * squashX,
                    _baseScale.y * squashY,
                    _baseScale.z
                ));

            }
        }



        /// <summary>应用缩放（统一叠加受击缩放因子，避免与EnemyHitFlash互相覆盖导致缩小bug）</summary>
        private void ApplyScale(Vector3 targetScale)
        {
            if (_hitFlashRef != null)
            {
                targetScale.x *= _hitFlashRef.HitBounceScaleX;
                targetScale.y *= _hitFlashRef.HitBounceScaleY;
            }
            _spriteTransform.localScale = targetScale;
        }

        /// <summary>应用基础缩放（统一叠加受击缩放因子）</summary>
        private void ApplyBaseScale()
        {
            ApplyScale(_baseScale);
        }

        /// <summary>应用垂直偏移（用于弹跳）</summary>
        private void ApplyVerticalOffset(float yOffset)

        {
            // 注意：不直接修改position.y，因为EnemyBase在控制position
            // 我们通过修改SpriteRenderer的偏移来实现视觉弹跳
            // 由于整个GO的position被EnemyBase控制，我们把弹跳做在localScale和rotation上
            // 但弹跳Y偏移需要用一个子对象或material偏移
            // 
            // 最简方案：利用SpriteRenderer的drawMode + size 或者直接偏移sprite的pivot
            // 更好方案：把sprite放在子对象中
            //
            // 当前实现：我们直接在Update末尾对position做微小偏移，
            // 然后在MoveAlongPath之后再叠加，因为EnemyBase的Update先于此组件
            // 实际上由于Script Execution Order不确定，改用安全的方式：
            // 把偏移存在_bounceYOffset中，在LateUpdate里应用

            _currentBounceY = yOffset;
        }

        private float _currentBounceY;

        private void LateUpdate()
        {
            if (!_initialized || _isPlayingDeath) return;

            // 计算本帧视觉偏移
            Vector3 offset = Vector3.zero;
            offset.y += _currentBounceY;
            offset.x += _currentSwayX; // 水平摇摆偏移

            // 腿部分离模式下，需要补偿上半身因pivot变化导致的位置偏移
            // 原始sprite pivot(0.5,0.5)在中心，切割后上半身pivot(0.5,0)在底部
            // 上半身需要向下偏移到腰部位置，使整体视觉位置与原始一致
            if (_legSplitDone)
            {
                offset.y += _upperBodyYOffset;
            }


            // 叠加受击抖动
            if (_hitShakeTimer > 0f)
            {
                offset += _hitShakeOffset;
            }


            // 应用偏移并记录（下一帧 Update 开头会还原）
            transform.position += offset;
            _lastVisualOffset = offset;

            // === 同步腿部位置（独立对象，需要手动同步） ===
            if (_legSplitDone && _legsTransform != null)
            {
                // 腿部的基准位置 = 怪物逻辑位置 + 腰部偏移
                // 注意：此时transform.position已经包含了视觉偏移
                // 腿部不需要上半身的弹跳偏移，但需要水平摇摆偏移
                Vector3 logicPos = transform.position - offset; // 还原到逻辑位置
                Vector3 legsPos = logicPos + new Vector3(_currentSwayX, _upperBodyYOffset, 0f);
                _legsTransform.position = legsPos;
                _legsTransform.localScale = _spriteTransform.localScale; // 同步缩放
            }

            // 更新阴影位置（阴影不跟随弹跳Y，保持在地面）
            if (_shadowObj != null)
            {
                // 阴影始终在怪物脚下
                Vector3 logicPos = transform.position - offset;
                float spriteBottomY = _legSplitDone ? _upperBodyYOffset : 0f;
                // 脚底 = 逻辑位置 + sprite底部偏移（约为sprite高度的一半取负）
                float footY = logicPos.y + spriteBottomY - (_legSplitDone ? _legsPivotLocalY : 0f);
                _shadowObj.transform.position = new Vector3(
                    logicPos.x,
                    logicPos.y - 0.2f, // 简单放在逻辑位置下方
                    logicPos.z
                );
            }


        }


        // ====================================================================
        // 受击动画
        // ====================================================================

        private void UpdateHitAnimation()
        {
            // === 闪白 ===
            if (_hitFlashTimer > 0f)
            {
                _hitFlashTimer -= Time.deltaTime;
                // 闪白在UpdateColorTint中处理
            }

            // === 抖动 ===
            if (_hitShakeTimer > 0f)
            {
                _hitShakeTimer -= Time.deltaTime;
                float intensity = _hitShakeIntensity * (_hitShakeTimer / _hitShakeDuration);
                _hitShakeOffset = new Vector3(
                    Random.Range(-intensity, intensity),
                    Random.Range(-intensity, intensity),
                    0f
                );
            }
            else
            {
                _hitShakeOffset = Vector3.zero;
            }
        }

        // ====================================================================
        // 颜色/色调混合
        // ====================================================================

        private void UpdateColorTint()
        {
            if (_spriteRenderer == null) return;

            Color targetColor = _originalColor;

            // 受击闪白（最高优先级）
            if (_hitFlashTimer > 0f)
            {
                float flashT = _hitFlashTimer / _hitFlashDuration;
                targetColor = Color.Lerp(targetColor, _hitFlashColor, flashT);
                _spriteRenderer.color = targetColor;
                // 同步腿部颜色
                if (_legSplitDone && _legsSpriteRenderer != null)
                    _legsSpriteRenderer.color = targetColor;
                return;
            }

            // Buff色调叠加
            _buffColorPhase += Time.deltaTime * 3f; // 闪烁频率

            if (_isFrozen)
            {
                // 冰冻：稳定蓝色调
                targetColor = Color.Lerp(targetColor, _freezeColor, 0.6f);
            }
            else if (_isPoisoned && _isBurning)
            {
                // 中毒+灼烧：交替闪烁
                float t = (Mathf.Sin(_buffColorPhase * 2f) + 1f) * 0.5f;
                Color mixed = Color.Lerp(_poisonColor, _burnColor, t);
                targetColor = Color.Lerp(targetColor, mixed, 0.5f);
            }
            else if (_isPoisoned)
            {
                // 中毒：绿色脉冲
                float pulse = (Mathf.Sin(_buffColorPhase * 2f) + 1f) * 0.5f;
                targetColor = Color.Lerp(targetColor, _poisonColor, 0.3f + pulse * 0.3f);
            }
            else if (_isBurning)
            {
                // 灼烧：橙红脉冲
                float pulse = (Mathf.Sin(_buffColorPhase * 3f) + 1f) * 0.5f;
                targetColor = Color.Lerp(targetColor, _burnColor, 0.3f + pulse * 0.3f);
            }

            // 减速时整体微暗
            if (_slowPercent > 0.1f && !_isFrozen)
            {
                float dim = 1f - _slowPercent * 0.2f;
                targetColor *= dim;
                targetColor.a = 1f;
            }

            _spriteRenderer.color = targetColor;

            // 同步腿部颜色（保持上下半身颜色一致）
            if (_legSplitDone && _legsSpriteRenderer != null)
            {
                _legsSpriteRenderer.color = targetColor;
            }
        }


        // ====================================================================
        // Buff状态读取
        // ====================================================================

        private void UpdateBuffVisualState()
        {
            if (_enemy == null || _enemy.Buffs == null) return;

            var buffs = _enemy.Buffs;
            _isFrozen = buffs.HasBuff(BuffSystem.BUFF_FREEZE);
            _isPoisoned = buffs.HasBuff(BuffSystem.BUFF_POISON);
            _isBurning = buffs.HasBuff(BuffSystem.BUFF_BURN);
            _isStunned = buffs.HasBuff(BuffSystem.BUFF_STUN);
            _slowPercent = Mathf.Clamp01(buffs.TotalSlowPercent);
        }

        // ====================================================================
        // 死亡动画
        // ====================================================================

        private void UpdateDeathAnimation()
        {
            _deathTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_deathTimer / _deathDuration);

            // 缓出曲线
            float easeOut = 1f - (1f - t) * (1f - t);

            // === 缩小 ===
            float scale = Mathf.Lerp(1f, 0f, easeOut);
            // 死亡动画不叠加受击缩放因子，直接设置
            _spriteTransform.localScale = _deathStartScale * scale;


            // === 弹起后坠落 ===
            // 抛物线：先弹起再落下
            float bounceT = t * 2f; // 加速
            float bounceY = _deathBounceHeight * (bounceT - bounceT * bounceT); // 抛物线
            _currentBounceY = Mathf.Max(bounceY, 0f);

            // === 旋转 ===
            float deathSpin = easeOut * 180f; // 旋转半圈
            _spriteTransform.localRotation = Quaternion.Euler(0f, 0f, deathSpin);

            // === 淡出 ===
            if (_spriteRenderer != null)
            {
                Color c = _spriteRenderer.color;
                c.a = Mathf.Lerp(_deathStartAlpha, 0f, easeOut);
                _spriteRenderer.color = c;

                // 同步腿部淡出+缩小
                if (_legSplitDone && _legsSpriteRenderer != null)
                {
                    _legsSpriteRenderer.color = c;
                    _legsTransform.localScale = _baseScale * scale;
                    _legsTransform.rotation = Quaternion.Euler(0f, 0f, deathSpin);
                }

            }

            // === 阴影淡出 ===

            if (_shadowRenderer != null)
            {
                Color sc = _shadowRenderer.color;
                sc.a = Mathf.Lerp(0.3f, 0f, easeOut);
                _shadowRenderer.color = sc;
            }

            // 动画完成
            if (t >= 1f)
            {
                _isPlayingDeath = false;
                _deathCompleteCallback?.Invoke();
                _deathCompleteCallback = null;
            }
        }

        // ====================================================================
        // 阴影
        // ====================================================================

        /// <summary>创建脚底阴影</summary>
        private void CreateShadow()
        {
            _shadowObj = new GameObject("Shadow");
            _shadowObj.transform.SetParent(null); // 不跟随父对象弹跳
            _shadowObj.transform.position = transform.position + Vector3.down * 0.2f;

            _shadowRenderer = _shadowObj.AddComponent<SpriteRenderer>();
            _shadowRenderer.sprite = GetShadowSprite();
            _shadowRenderer.color = new Color(0f, 0f, 0f, 0.3f);
            _shadowRenderer.sortingOrder = 5; // 低于怪物（怪物是8）

            // 阴影缩放（椭圆形）
            float shadowScale = _baseScale.x * 0.6f;
            _shadowObj.transform.localScale = new Vector3(shadowScale, shadowScale * 0.4f, 1f);
        }

        /// <summary>获取/创建阴影Sprite（复用1x1白色圆形）</summary>
        private static Sprite GetShadowSprite()
        {
            if (_cachedShadowSprite != null) return _cachedShadowSprite;

            // 创建一个简单的圆形纹理（16x16）
            int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float radius = center - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01((radius - dist) / 2f); // 柔和边缘
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;

            _cachedShadowSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                16f
            );

            return _cachedShadowSprite;
        }

        /// <summary>更新阴影</summary>
        private void UpdateShadow()
        {
            if (_shadowObj == null) return;

            // 阴影大小随弹跳高度变化（越高阴影越小越淡）
            float normalizedBounce = _currentBounceY / Mathf.Max(_bounceHeight, 0.001f);
            float shadowScale = _baseScale.x * 0.6f * (1f - normalizedBounce * 0.3f);
            _shadowObj.transform.localScale = new Vector3(shadowScale, shadowScale * 0.4f, 1f);

            float shadowAlpha = 0.3f * (1f - normalizedBounce * 0.5f);
            Color sc = _shadowRenderer.color;
            sc.a = shadowAlpha;
            _shadowRenderer.color = sc;
        }

        // ====================================================================
        // 工具方法
        // ====================================================================

        /// <summary>
        /// 按怪物类型返回推荐的弹跳参数
        /// </summary>
        public static void GetTypePreset(EnemyType type, out float bounceHeight, out float bounceFreq, out float tiltAngle)
        {
            switch (type)
            {
                case EnemyType.Infantry:
                    bounceHeight = 0.10f; bounceFreq = 3.5f; tiltAngle = 8f; // 步兵：明显迈步
                    break;
                case EnemyType.Assassin:
                    bounceHeight = 0.08f; bounceFreq = 5.5f; tiltAngle = 10f; // 刺客：快速小步
                    break;
                case EnemyType.Knight:
                    bounceHeight = 0.07f; bounceFreq = 2.0f; tiltAngle = 5f; // 骑士：沉重大步
                    break;
                case EnemyType.Flyer:
                    bounceHeight = 0.12f; bounceFreq = 2.5f; tiltAngle = 12f; // 飞行：飘浮摇摆
                    break;
                case EnemyType.Healer:
                    bounceHeight = 0.08f; bounceFreq = 3.0f; tiltAngle = 6f; // 治疗：轻快
                    break;
                case EnemyType.Slime:
                    bounceHeight = 0.18f; bounceFreq = 1.8f; tiltAngle = 3f; // 史莱姆：大幅弹跳
                    break;
                case EnemyType.Rogue:
                    bounceHeight = 0.09f; bounceFreq = 5.0f; tiltAngle = 9f; // 盗贼：敏捷
                    break;
                case EnemyType.ShieldMage:
                    bounceHeight = 0.06f; bounceFreq = 2.8f; tiltAngle = 4f; // 法师：稳重
                    break;
                case EnemyType.BossDragon:
                    bounceHeight = 0.08f; bounceFreq = 1.8f; tiltAngle = 6f; // Boss龙：威严
                    break;
                case EnemyType.BossGiant:
                    bounceHeight = 0.06f; bounceFreq = 1.2f; tiltAngle = 4f; // 巨人：极缓重步
                    break;
                default:
                    bounceHeight = 0.10f; bounceFreq = 3.5f; tiltAngle = 8f;
                    break;
            }
        }


        /// <summary>
        /// 应用怪物类型预设参数
        /// </summary>
        public void ApplyTypePreset(EnemyType type)
        {
            GetTypePreset(type, out float bh, out float bf, out float ta);
            _bounceHeight = bh;
            _bounceFrequency = bf;
            _tiltAngle = ta;

            // 按怪物类型设置摇摆、前倾、腿部分离参数
            switch (type)
            {
                case EnemyType.Infantry:
                    _swayAmount = 0.035f; _leanForwardAngle = 3f; _squashStretch = 0.10f;
                    _enableLegSplit = true; _legRatio = 0.35f; _legSwingAngle = 18f;
                    break;
                case EnemyType.Assassin:
                    _swayAmount = 0.025f; _leanForwardAngle = 5f; _squashStretch = 0.08f;
                    _enableLegSplit = true; _legRatio = 0.35f; _legSwingAngle = 22f; // 跑步感，腿摆更大
                    break;
                case EnemyType.Knight:
                    _swayAmount = 0.04f; _leanForwardAngle = 2f; _squashStretch = 0.12f;
                    _enableLegSplit = true; _legRatio = 0.30f; _legSwingAngle = 12f; // 重甲，腿摆小
                    break;
                case EnemyType.Flyer:
                    _swayAmount = 0.05f; _leanForwardAngle = 0f; _squashStretch = 0.05f;
                    _enableLegSplit = false; // 飞行单位不切割腿部，保持飘浮整体动画
                    break;
                case EnemyType.Slime:
                    _swayAmount = 0.02f; _leanForwardAngle = 0f; _squashStretch = 0.20f;
                    _enableLegSplit = false; // 史莱姆没有腿，保持弹跳整体动画
                    break;
                case EnemyType.Healer:
                    _swayAmount = 0.03f; _leanForwardAngle = 2f; _squashStretch = 0.08f;
                    _enableLegSplit = true; _legRatio = 0.35f; _legSwingAngle = 14f;
                    break;
                case EnemyType.Rogue:
                    _swayAmount = 0.025f; _leanForwardAngle = 4f; _squashStretch = 0.08f;
                    _enableLegSplit = true; _legRatio = 0.35f; _legSwingAngle = 20f; // 敏捷，腿摆大
                    break;
                case EnemyType.ShieldMage:
                    _swayAmount = 0.03f; _leanForwardAngle = 1f; _squashStretch = 0.06f;
                    _enableLegSplit = true; _legRatio = 0.30f; _legSwingAngle = 10f; // 稳重
                    break;
                case EnemyType.BossDragon:
                    _swayAmount = 0.05f; _leanForwardAngle = 2f; _squashStretch = 0.08f;
                    _enableLegSplit = true; _legRatio = 0.30f; _legSwingAngle = 10f;
                    _deathDuration = 0.8f; _deathBounceHeight = 0.5f;
                    break;
                case EnemyType.BossGiant:
                    _swayAmount = 0.05f; _leanForwardAngle = 2f; _squashStretch = 0.08f;
                    _enableLegSplit = true; _legRatio = 0.30f; _legSwingAngle = 8f; // 巨人，腿摆小但沉重
                    _deathDuration = 0.8f; _deathBounceHeight = 0.5f;
                    break;
                default:
                    _swayAmount = 0.03f; _leanForwardAngle = 3f; _squashStretch = 0.10f;
                    _enableLegSplit = true; _legRatio = 0.35f; _legSwingAngle = 15f;
                    break;
            }


            // 飞行怪特殊：基础Y偏移更高（悬浮）
            if (type == EnemyType.Flyer)
            {
                _baseLocalY = 0.2f;
            }
        }


        /// <summary>
        /// 同步腿部的flipX朝向（由EnemyBase.UpdateFacing调用）
        /// </summary>
        public void SyncLegsFlip(bool flipX)
        {
            if (_legSplitDone && _legsSpriteRenderer != null)
            {
                _legsSpriteRenderer.flipX = flipX;
            }
        }

        // ====================================================================
        // ★ 增强视觉：描边轮廓系统
        // ====================================================================

        /// <summary>创建8方向描边轮廓（让怪物从背景中弹出）</summary>
        private void CreateOutline()
        {
            if (_spriteRenderer == null || _spriteRenderer.sprite == null) return;

            _outlineObjs = new GameObject[OUTLINE_DIRECTIONS];
            _outlineRenderers = new SpriteRenderer[OUTLINE_DIRECTIONS];

            // 根据怪物类型选择描边颜色
            Color outlineColor = GetOutlineColor();

            for (int i = 0; i < OUTLINE_DIRECTIONS; i++)
            {
                float angle = (float)i / OUTLINE_DIRECTIONS * Mathf.PI * 2f;
                float ox = Mathf.Cos(angle) * OUTLINE_OFFSET;
                float oy = Mathf.Sin(angle) * OUTLINE_OFFSET;

                _outlineObjs[i] = new GameObject($"EnemyOutline_{i}");
                _outlineObjs[i].transform.SetParent(transform);
                _outlineObjs[i].transform.localPosition = new Vector3(ox, oy, 0.002f);
                _outlineObjs[i].transform.localScale = Vector3.one;

                _outlineRenderers[i] = _outlineObjs[i].AddComponent<SpriteRenderer>();
                _outlineRenderers[i].sortingOrder = _spriteRenderer.sortingOrder - 1;
                _outlineRenderers[i].sprite = _spriteRenderer.sprite;
                _outlineRenderers[i].color = outlineColor;
            }
        }

        /// <summary>根据怪物类型获取描边颜色</summary>
        private Color GetOutlineColor()
        {
            if (_enemy == null) return ENEMY_OUTLINE_COLOR;
            switch (_enemy.Type)
            {
                case EnemyType.BossDragon:  return new Color(0.6f, 0.1f, 0.0f, 0.75f);  // 暗红
                case EnemyType.BossGiant:   return new Color(0.3f, 0.2f, 0.1f, 0.75f);  // 暗棕
                case EnemyType.Assassin:    return new Color(0.25f, 0.1f, 0.35f, 0.7f);  // 暗紫
                case EnemyType.Knight:      return new Color(0.2f, 0.2f, 0.25f, 0.7f);   // 暗银
                case EnemyType.Flyer:       return new Color(0.1f, 0.25f, 0.35f, 0.65f);  // 暗蓝
                case EnemyType.Slime:       return new Color(0.1f, 0.3f, 0.05f, 0.65f);  // 暗绿
                case EnemyType.Rogue:       return new Color(0.1f, 0.05f, 0.15f, 0.7f);  // 深紫
                default:                    return ENEMY_OUTLINE_COLOR;
            }
        }

        /// <summary>同步描边层的Sprite（当主体Sprite变化时）</summary>
        private void UpdateOutlineSync()
        {
            if (_outlineRenderers == null || _spriteRenderer == null) return;

            Sprite currentSprite = _spriteRenderer.sprite;
            if (currentSprite == null) return;

            // 只在Sprite变化时更新
            if (_outlineRenderers[0] != null && _outlineRenderers[0].sprite != currentSprite)
            {
                for (int i = 0; i < OUTLINE_DIRECTIONS; i++)
                {
                    if (_outlineRenderers[i] != null)
                        _outlineRenderers[i].sprite = currentSprite;
                }
            }

            // 同步flipX
            bool flipX = _spriteRenderer.flipX;
            for (int i = 0; i < OUTLINE_DIRECTIONS; i++)
            {
                if (_outlineRenderers[i] != null)
                    _outlineRenderers[i].flipX = flipX;
            }
        }

        // ====================================================================
        // ★ 增强视觉：怪物类型光环
        // ====================================================================

        /// <summary>创建脚下类型光环（不同怪物类型不同颜色）</summary>
        private void CreateTypeGlow()
        {
            _typeGlowObj = new GameObject("EnemyTypeGlow");
            _typeGlowObj.transform.SetParent(transform);
            _typeGlowObj.transform.localPosition = new Vector3(0f, -0.15f, 0.008f);

            float glowScale = (_enemy != null && _enemy.IsBoss) ? 1.8f : 1.0f;
            _typeGlowObj.transform.localScale = new Vector3(glowScale, glowScale * 0.4f, 1f);

            _typeGlowRenderer = _typeGlowObj.AddComponent<SpriteRenderer>();
            _typeGlowRenderer.sortingOrder = _spriteRenderer.sortingOrder - 2;
            _typeGlowRenderer.sprite = GetCircleSprite();
            _typeGlowRenderer.color = GetTypeGlowColor();

            _typeGlowPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        /// <summary>获取怪物类型光环颜色</summary>
        private Color GetTypeGlowColor()
        {
            if (_enemy == null) return new Color(0.8f, 0.3f, 0.3f, 0.2f);
            switch (_enemy.Type)
            {
                case EnemyType.Infantry:    return new Color(0.9f, 0.3f, 0.3f, 0.18f);
                case EnemyType.Assassin:    return new Color(0.6f, 0.2f, 0.8f, 0.22f);
                case EnemyType.Knight:      return new Color(0.6f, 0.6f, 0.7f, 0.2f);
                case EnemyType.Flyer:       return new Color(0.3f, 0.7f, 0.9f, 0.2f);
                case EnemyType.Healer:      return new Color(0.3f, 0.9f, 0.4f, 0.22f);
                case EnemyType.Slime:       return new Color(0.4f, 0.9f, 0.2f, 0.2f);
                case EnemyType.Rogue:       return new Color(0.3f, 0.2f, 0.4f, 0.2f);
                case EnemyType.ShieldMage:  return new Color(0.3f, 0.5f, 0.9f, 0.22f);
                case EnemyType.BossDragon:  return new Color(1f, 0.3f, 0.1f, 0.35f);
                case EnemyType.BossGiant:   return new Color(0.7f, 0.5f, 0.2f, 0.35f);
                default:                    return new Color(0.8f, 0.3f, 0.3f, 0.18f);
            }
        }

        private void UpdateTypeGlow()
        {
            if (_typeGlowRenderer == null) return;

            _typeGlowPhase += Time.deltaTime;
            float pulse = 0.85f + Mathf.Sin(_typeGlowPhase * 2.5f) * 0.15f;

            Color baseColor = GetTypeGlowColor();
            _typeGlowRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * pulse);
        }

        // ====================================================================
        // ★ 增强视觉：入场动画
        // ====================================================================

        private void UpdateSpawnAnimation()
        {
            if (_spawnAnimTimer <= 0f) return;

            _spawnAnimTimer -= Time.deltaTime;
            float t = 1f - (_spawnAnimTimer / SPAWN_ANIM_DURATION);

            if (t >= 1f)
            {
                _spawnAnimTimer = -1f;
                // 恢复正常状态
                if (_spriteRenderer != null)
                {
                    Color c = _spriteRenderer.color;
                    c.a = 1f;
                    _spriteRenderer.color = c;
                }
                return;
            }

            // 弹性入场：从小到大带回弹
            float elastic = EaseOutBack(t);
            // 入场动画不叠加受击缩放因子，直接设置
            _spriteTransform.localScale = _baseScale * elastic;


            // 淡入
            if (_spriteRenderer != null)
            {
                float alpha = Mathf.Clamp01(t * 2.5f);
                Color c = _originalColor;
                c.a = alpha;
                _spriteRenderer.color = c;
            }

            // 描边也跟随淡入
            if (_outlineRenderers != null)
            {
                float outlineAlpha = Mathf.Clamp01(t * 2f);
                Color oc = GetOutlineColor();
                oc.a *= outlineAlpha;
                for (int i = 0; i < OUTLINE_DIRECTIONS; i++)
                {
                    if (_outlineRenderers[i] != null)
                        _outlineRenderers[i].color = oc;
                }
            }
        }

        private float EaseOutBack(float t)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        // ====================================================================
        // ★ 增强视觉：眼睛系统
        // ====================================================================

        /// <summary>创建简单的眼睛点（让怪物更有生命感）</summary>
        private void CreateEyes()
        {
            // Boss和史莱姆用不同的眼睛配置
            if (_enemy == null) return;
            if (_enemy.Type == EnemyType.Slime)
            {
                // 史莱姆：大眼睛
                CreateEyePair(0.08f, 0.06f, 0.06f, new Color(1f, 1f, 1f, 0.9f));
            }
            else if (_enemy.IsBoss)
            {
                // Boss：发光红眼
                CreateEyePair(0.1f, 0.08f, 0.05f, new Color(1f, 0.3f, 0.1f, 0.9f));
            }
            else
            {
                // 普通怪：小白眼
                CreateEyePair(0.06f, 0.04f, 0.04f, new Color(1f, 1f, 1f, 0.85f));
            }
        }

        private void CreateEyePair(float spacing, float eyeSize, float yOffset, Color eyeColor)
        {
            var eyeSprite = GetEyeSprite();

            // 左眼
            _eyeLeftObj = new GameObject("EyeL");
            _eyeLeftObj.transform.SetParent(transform);
            _eyeLeftObj.transform.localPosition = new Vector3(-spacing * 0.5f, yOffset, -0.003f);
            _eyeLeftObj.transform.localScale = new Vector3(eyeSize, eyeSize, 1f);
            _eyeLeftRenderer = _eyeLeftObj.AddComponent<SpriteRenderer>();
            _eyeLeftRenderer.sprite = eyeSprite;
            _eyeLeftRenderer.sortingOrder = _spriteRenderer.sortingOrder + 1;
            _eyeLeftRenderer.color = eyeColor;

            // 右眼
            _eyeRightObj = new GameObject("EyeR");
            _eyeRightObj.transform.SetParent(transform);
            _eyeRightObj.transform.localPosition = new Vector3(spacing * 0.5f, yOffset, -0.003f);
            _eyeRightObj.transform.localScale = new Vector3(eyeSize, eyeSize, 1f);
            _eyeRightRenderer = _eyeRightObj.AddComponent<SpriteRenderer>();
            _eyeRightRenderer.sprite = eyeSprite;
            _eyeRightRenderer.sortingOrder = _spriteRenderer.sortingOrder + 1;
            _eyeRightRenderer.color = eyeColor;
        }

        private void UpdateEyes()
        {
            if (_eyeLeftObj == null || _eyeRightObj == null) return;

            // 眨眼
            _blinkTimer += Time.deltaTime;
            if (!_isBlinking && _blinkTimer >= _nextBlinkTime)
            {
                _isBlinking = true;
                _blinkTimer = 0f;
            }

            if (_isBlinking)
            {
                // 眨眼：快速缩小Y再恢复
                float blinkT = _blinkTimer / BLINK_DURATION;
                float scaleY;
                if (blinkT < 0.5f)
                    scaleY = Mathf.Lerp(1f, 0.1f, blinkT * 2f);
                else
                    scaleY = Mathf.Lerp(0.1f, 1f, (blinkT - 0.5f) * 2f);

                Vector3 ls = _eyeLeftObj.transform.localScale;
                _eyeLeftObj.transform.localScale = new Vector3(ls.x, ls.x * scaleY, 1f);
                _eyeRightObj.transform.localScale = new Vector3(ls.x, ls.x * scaleY, 1f);

                if (blinkT >= 1f)
                {
                    _isBlinking = false;
                    _blinkTimer = 0f;
                    _nextBlinkTime = Random.Range(2f, 6f);
                }
            }

            // 眼睛看向移动方向（微小偏移）
            Vector3 targetLookOffset = Vector3.zero;
            if (_isWalking && _moveDirection.sqrMagnitude > 0.001f)
            {
                targetLookOffset = new Vector3(
                    _moveDirection.x * 0.012f,
                    _moveDirection.y * 0.008f,
                    0f
                );
            }
            _eyeLookOffset = Vector3.Lerp(_eyeLookOffset, targetLookOffset, Time.deltaTime * 5f);

            // 应用偏移到眼睛位置
            float spacing = 0.06f;
            if (_enemy != null && _enemy.Type == EnemyType.Slime) spacing = 0.08f;
            if (_enemy != null && _enemy.IsBoss) spacing = 0.1f;

            float yOff = 0.04f;
            if (_enemy != null && _enemy.Type == EnemyType.Slime) yOff = 0.06f;
            if (_enemy != null && _enemy.IsBoss) yOff = 0.05f;

            _eyeLeftObj.transform.localPosition = new Vector3(-spacing * 0.5f + _eyeLookOffset.x, yOff + _eyeLookOffset.y, -0.003f);
            _eyeRightObj.transform.localPosition = new Vector3(spacing * 0.5f + _eyeLookOffset.x, yOff + _eyeLookOffset.y, -0.003f);

            // 同步flipX（眼睛跟随身体翻转）
            // 翻转时交换左右眼位置
            if (_spriteRenderer != null && _spriteRenderer.flipX)
            {
                _eyeLeftObj.transform.localPosition = new Vector3(spacing * 0.5f - _eyeLookOffset.x, yOff + _eyeLookOffset.y, -0.003f);
                _eyeRightObj.transform.localPosition = new Vector3(-spacing * 0.5f - _eyeLookOffset.x, yOff + _eyeLookOffset.y, -0.003f);
            }

            // 死亡时眼睛变X
            if (_isPlayingDeath)
            {
                if (_eyeLeftRenderer != null)
                {
                    Color c = _eyeLeftRenderer.color;
                    c.a *= 0.5f;
                    _eyeLeftRenderer.color = c;
                    _eyeRightRenderer.color = c;
                }
                // 旋转45度变成X
                _eyeLeftObj.transform.localRotation = Quaternion.Euler(0, 0, 45f);
                _eyeRightObj.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            }
        }

        // ====================================================================
        // ★ 增强视觉：低血量警告
        // ====================================================================

        private void UpdateLowHPWarning()
        {
            if (_enemy == null || _isPlayingDeath) return;

            bool wasLowHP = _isLowHP;
            _isLowHP = _enemy.HPPercent < LOW_HP_THRESHOLD && _enemy.HPPercent > 0f;

            if (!_isLowHP) return;

            _lowHPPulsePhase += Time.deltaTime;

            // 红色脉冲闪烁（叠加在当前颜色上）
            float pulse = (Mathf.Sin(_lowHPPulsePhase * 6f) + 1f) * 0.5f;
            float intensity = pulse * 0.25f;

            if (_spriteRenderer != null)
            {
                Color c = _spriteRenderer.color;
                c.r = Mathf.Min(c.r + intensity, 1f);
                c.g *= (1f - intensity * 0.5f);
                c.b *= (1f - intensity * 0.5f);
                _spriteRenderer.color = c;
            }

            // 描边也跟随脉冲变红
            if (_outlineRenderers != null)
            {
                Color redOutline = Color.Lerp(GetOutlineColor(), new Color(0.8f, 0.1f, 0.05f, 0.8f), pulse * 0.6f);
                for (int i = 0; i < OUTLINE_DIRECTIONS; i++)
                {
                    if (_outlineRenderers[i] != null)
                        _outlineRenderers[i].color = redOutline;
                }
            }

            // 光环变红
            if (_typeGlowRenderer != null)
            {
                Color glowColor = Color.Lerp(GetTypeGlowColor(), new Color(1f, 0.15f, 0.05f, 0.35f), pulse * 0.7f);
                _typeGlowRenderer.color = glowColor;
            }
        }

        // ====================================================================
        // ★ 增强视觉：Buff环绕粒子
        // ====================================================================

        private void CreateBuffParticles()
        {
            _buffParticles = new GameObject[BUFF_PARTICLE_COUNT];
            _buffParticlePhases = new float[BUFF_PARTICLE_COUNT];
            _buffParticleSpeeds = new float[BUFF_PARTICLE_COUNT];

            var sprite = GetCircleSprite();

            for (int i = 0; i < BUFF_PARTICLE_COUNT; i++)
            {
                _buffParticles[i] = new GameObject($"BuffP_{i}");
                _buffParticles[i].transform.SetParent(transform);

                var sr = _buffParticles[i].AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = _spriteRenderer.sortingOrder + 2;
                sr.color = new Color(1f, 1f, 1f, 0f); // 初始不可见

                float s = Random.Range(0.3f, 0.5f);
                _buffParticles[i].transform.localScale = new Vector3(s, s, 1f);

                _buffParticlePhases[i] = Random.Range(0f, Mathf.PI * 2f);
                _buffParticleSpeeds[i] = Random.Range(1.5f, 3f);

                _buffParticles[i].SetActive(false);
            }
        }

        private void UpdateBuffParticles()
        {
            if (_buffParticles == null) return;

            bool hasActiveBuff = _isFrozen || _isPoisoned || _isBurning;

            for (int i = 0; i < BUFF_PARTICLE_COUNT; i++)
            {
                if (_buffParticles[i] == null) continue;

                _buffParticles[i].SetActive(hasActiveBuff);
                if (!hasActiveBuff) continue;

                _buffParticlePhases[i] += Time.deltaTime * _buffParticleSpeeds[i];
                float phase = _buffParticlePhases[i];

                // 环绕运动
                float radius = 0.2f + Mathf.Sin(phase * 0.5f) * 0.05f;
                float x = Mathf.Cos(phase) * radius;
                float y = Mathf.Sin(phase * 0.7f) * radius * 0.6f + 0.05f;
                _buffParticles[i].transform.localPosition = new Vector3(x, y, -0.01f);

                // 根据Buff类型设置颜色
                var sr = _buffParticles[i].GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color buffColor;
                    if (_isFrozen)
                        buffColor = new Color(0.5f, 0.85f, 1f, 0.5f + Mathf.Sin(phase * 2f) * 0.2f);
                    else if (_isBurning)
                        buffColor = new Color(1f, 0.5f + Mathf.Sin(phase * 3f) * 0.3f, 0.1f, 0.5f + Mathf.Sin(phase * 2f) * 0.2f);
                    else // 中毒
                        buffColor = new Color(0.3f, 0.9f, 0.2f, 0.4f + Mathf.Sin(phase * 2f) * 0.2f);

                    sr.color = buffColor;

                    // 大小脉动
                    float s = 0.35f + Mathf.Sin(phase * 1.5f) * 0.1f;
                    _buffParticles[i].transform.localScale = new Vector3(s, s, 1f);
                }
            }
        }

        // ====================================================================
        // ★ 增强视觉：Boss专属光柱
        // ====================================================================

        private void CreateBossAura()
        {
            _bossAuraObj = new GameObject("BossAura");
            _bossAuraObj.transform.SetParent(transform);
            _bossAuraObj.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            _bossAuraObj.transform.localScale = new Vector3(2f, 2f, 1f);

            _bossAuraRenderer = _bossAuraObj.AddComponent<SpriteRenderer>();
            _bossAuraRenderer.sortingOrder = _spriteRenderer.sortingOrder - 3;
            _bossAuraRenderer.sprite = GetCircleSprite();

            Color auraColor = (_enemy != null && _enemy.Type == EnemyType.BossDragon)
                ? new Color(1f, 0.3f, 0.05f, 0.15f)
                : new Color(0.6f, 0.4f, 0.15f, 0.15f);
            _bossAuraRenderer.color = auraColor;

            _bossAuraPhase = 0f;
        }

        private void UpdateBossAura()
        {
            if (_bossAuraRenderer == null) return;

            _bossAuraPhase += Time.deltaTime;

            // 脉动缩放
            float pulse = 2f + Mathf.Sin(_bossAuraPhase * 1.5f) * 0.3f;
            _bossAuraObj.transform.localScale = new Vector3(pulse, pulse, 1f);

            // 透明度呼吸
            float alpha = 0.12f + Mathf.Sin(_bossAuraPhase * 2f) * 0.06f;
            Color c = _bossAuraRenderer.color;
            c.a = alpha;
            _bossAuraRenderer.color = c;

            // 缓慢旋转
            _bossAuraObj.transform.localRotation = Quaternion.Euler(0, 0, _bossAuraPhase * 15f);
        }

        // ====================================================================
        // ★ 增强视觉：受击方向倾斜
        // ====================================================================

        /// <summary>触发受击方向倾斜（由外部调用，传入伤害来源方向）</summary>
        public void TriggerHitTilt(Vector3 hitDirection)
        {
            if (_isPlayingDeath) return;
            // 根据伤害方向计算倾斜角度
            float tiltDir = hitDirection.x > 0 ? -1f : 1f;
            _hitTiltAngle = tiltDir * Random.Range(8f, 15f);
            _hitTiltTimer = HIT_TILT_DURATION;
        }

        private void UpdateHitTilt()
        {
            if (_hitTiltTimer <= 0f) return;

            _hitTiltTimer -= Time.deltaTime;
            float t = _hitTiltTimer / HIT_TILT_DURATION;

            // 弹性回正
            float currentTilt = _hitTiltAngle * t * Mathf.Cos(t * Mathf.PI * 2f);

            // 叠加到当前旋转上
            Quaternion currentRot = _spriteTransform.localRotation;
            _spriteTransform.localRotation = currentRot * Quaternion.Euler(0, 0, currentTilt * Time.deltaTime * 10f);

            if (_hitTiltTimer <= 0f)
            {
                _hitTiltTimer = 0f;
                _hitTiltAngle = 0f;
            }
        }

        // ====================================================================
        // ★ 纹理工具
        // ====================================================================

        private static Sprite GetCircleSprite()
        {
            if (_cachedCircleSprite != null) return _cachedCircleSprite;

            int size = 32;
            if (_cachedCircleTex == null)
            {
                _cachedCircleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float center = size / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                        float alpha = Mathf.Clamp01(1f - dist * dist);
                        _cachedCircleTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                    }
                }
                _cachedCircleTex.Apply();
                _cachedCircleTex.filterMode = FilterMode.Bilinear;
            }

            _cachedCircleSprite = Sprite.Create(_cachedCircleTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedCircleSprite;
        }

        private static Sprite GetEyeSprite()
        {
            if (_cachedEyeSprite != null) return _cachedEyeSprite;

            int size = 12;
            if (_cachedEyeTex == null)
            {
                _cachedEyeTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float center = size / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                        // 眼睛：中心亮，边缘柔和
                        float alpha;
                        if (dist < 0.4f)
                            alpha = 1f; // 瞳孔
                        else if (dist < 0.8f)
                            alpha = Mathf.Clamp01(1f - (dist - 0.4f) / 0.4f) * 0.9f;
                        else
                            alpha = Mathf.Clamp01(1f - (dist - 0.8f) / 0.2f) * 0.3f;
                        _cachedEyeTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                    }
                }
                _cachedEyeTex.Apply();
                _cachedEyeTex.filterMode = FilterMode.Bilinear;
            }

            _cachedEyeSprite = Sprite.Create(_cachedEyeTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedEyeSprite;
        }

        /// <summary>获取调试信息</summary>
        public string GetDebugInfo()

        {
            return $"Walking={_isWalking} LegSplit={_legSplitDone} Frozen={_isFrozen} Poison={_isPoisoned} " +
                   $"Burn={_isBurning} Stun={_isStunned} Slow={_slowPercent:P0} " +
                   $"Death={_isPlayingDeath}";
        }
    }
}
