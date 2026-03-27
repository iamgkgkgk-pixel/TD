// ============================================================
// 文件名：TowerVisualEffect.cs
// 功能描述：防御塔视觉增强系统 — 纯程序化实现
//   多层立体感：底座厚度层、高光层、描边轮廓、增强阴影、等级装饰
//   动效：呼吸动画、攻击后坐力、射程光环、瞄准线、冷却进度环
//   让防御塔从"平面贴图"变成有厚度、有质感的战斗单位
// 创建时间：2026-03-26
// 所属模块：Battle/Tower
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;

namespace AetheraSurvivors.Battle.Tower
{
    /// <summary>
    /// 防御塔视觉增强组件 — 多层叠加 + 动效系统
    /// 
    /// 立体感层次（从下到上）：
    /// 1. 底座阴影层（椭圆投影阴影，增加接地感）
    /// 2. 底座厚度层（深色侧面条，模拟底座厚度）
    /// 3. 等级光环层（随等级变化的地面光效）
    /// 4. 主体Sprite（原始塔图片）
    /// 5. 描边轮廓层（深色描边，让塔从背景中弹出）
    /// 6. 高光层（顶部半透明高光，模拟光照）
    /// 7. 等级星星装饰（显示当前等级）
    /// 
    /// 动效列表：
    /// 1. 呼吸动画（轻微缩放脉动）
    /// 2. 攻击后坐力（开火时弹跳+闪光）
    /// 3. 瞄准线（锁定目标时的激光瞄准线）
    /// 4. 攻击蓄力指示（冷却进度环）
    /// 5. 攻击粒子爆发
    /// </summary>
    public class TowerVisualEffect : MonoBehaviour
    {
        // ========== 引用 ==========
        private TowerBase _tower;
        private SpriteRenderer _spriteRenderer;
        private Transform _spriteTransform;

        // ========== 底座阴影层 ==========
        private GameObject _shadowObj;
        private SpriteRenderer _shadowRenderer;

        // ========== 底座厚度层（模拟侧面） ==========
        private GameObject _thicknessObj;
        private SpriteRenderer _thicknessRenderer;

        // ========== 描边轮廓层 ==========
        private GameObject[] _outlineObjs;
        private SpriteRenderer[] _outlineRenderers;
        private const int OUTLINE_DIRECTIONS = 8; // 8方向描边

        // ========== 高光层 ==========
        private GameObject _highlightObj;
        private SpriteRenderer _highlightRenderer;

        // ========== 等级光环 ==========
        private GameObject _glowObj;
        private SpriteRenderer _glowRenderer;
        private float _glowPulseTimer;

        // ========== 等级星星装饰 ==========
        private GameObject[] _starObjs;
        private SpriteRenderer[] _starRenderers;
        private const int MAX_STARS = 3;

        // ========== 攻击后坐力 ==========
        private float _recoilTimer;
        private float _recoilDuration = 0.15f;
        private Vector3 _recoilDirection;
        private float _flashTimer;

        // ========== 呼吸动画 ==========
        private Vector3 _baseScale;
        private float _breathePhase;

        // ========== 位置偏移（后坐力用） ==========
        private Vector3 _recoilOffset;


        // ========== 瞄准线 ==========
        private LineRenderer _aimLine;
        private GameObject _aimLineObj;

        // ========== 冷却进度环 ==========
        private LineRenderer _cooldownRing;
        private GameObject _cooldownObj;

        // ========== 攻击蓄力粒子 ==========
        private GameObject[] _chargeParticles;
        private const int CHARGE_PARTICLE_COUNT = 6;

        // ========== 放置入场动画 ==========
        private float _placeAnimTimer = -1f;
        private const float PLACE_ANIM_DURATION = 0.4f;

        // ========== 选中高亮 ==========
        private bool _isSelected;
        private float _selectGlowTimer;
        private GameObject _selectGlowObj;
        private SpriteRenderer _selectGlowRenderer;

        // ========== 塔类型环境粒子 ==========
        private GameObject[] _ambientParticles;
        private const int AMBIENT_PARTICLE_COUNT = 4;
        private float[] _ambientPhases;
        private float[] _ambientSpeeds;
        private float[] _ambientRadii;

        // ========== 升级闪光 ==========
        private float _upgradeFlashTimer = -1f;
        private const float UPGRADE_FLASH_DURATION = 0.6f;

        // ========== 空闲摇摆 ==========
        private float _idleSwayPhase;
        private bool _hasTarget;

        // ========== 攻击描边闪亮 ==========
        private float _outlineBrightTimer;

        // ========== 内阴影/浮雕层 ==========
        private GameObject _innerShadowObj;
        private SpriteRenderer _innerShadowRenderer;

        // ========== 底部反光层 ==========
        private GameObject _bottomReflectObj;
        private SpriteRenderer _bottomReflectRenderer;

        // ========== 配置 ==========

        /// <summary>描边像素偏移量（世界单位）</summary>
        private const float OUTLINE_OFFSET = 0.018f;

        /// <summary>描边颜色（深色轮廓）</summary>
        private static readonly Color OUTLINE_COLOR = new Color(0.08f, 0.06f, 0.04f, 0.7f);

        /// <summary>厚度层颜色（底座侧面深色）</summary>
        private static readonly Color THICKNESS_COLOR = new Color(0.15f, 0.12f, 0.1f, 0.65f);

        /// <summary>高光颜色</summary>
        private static readonly Color HIGHLIGHT_COLOR = new Color(1f, 1f, 0.95f, 0.18f);

        /// <summary>等级光环颜色</summary>
        private static readonly Color[] LevelGlowColors = new Color[]
        {
            new Color(0.3f, 0.8f, 0.3f, 0.2f),   // Lv1 绿色
            new Color(0.3f, 0.5f, 1.0f, 0.3f),    // Lv2 蓝色
            new Color(1.0f, 0.75f, 0.15f, 0.4f),  // Lv3 金色
        };

        /// <summary>等级星星颜色</summary>
        private static readonly Color[] LevelStarColors = new Color[]
        {
            new Color(0.6f, 0.9f, 0.6f, 0.9f),    // Lv1 浅绿
            new Color(0.5f, 0.7f, 1.0f, 0.9f),    // Lv2 浅蓝
            new Color(1.0f, 0.85f, 0.2f, 1.0f),   // Lv3 金色
        };

        /// <summary>塔类型环境粒子颜色</summary>
        private static readonly Color[] TowerAmbientColors = new Color[]
        {
            new Color(1.0f, 0.9f, 0.4f, 0.5f),    // Archer 金色光点
            new Color(0.7f, 0.3f, 1.0f, 0.5f),    // Mage 紫色魔法粒子
            new Color(0.4f, 0.85f, 1.0f, 0.5f),   // Ice 冰蓝色冰晶
            new Color(1.0f, 0.6f, 0.15f, 0.5f),   // Cannon 火焰色火星
            new Color(0.3f, 0.95f, 0.3f, 0.5f),   // Poison 毒绿色毒雾
        };

        // ========== 缓存的纹理资源 ==========
        private static System.Collections.Generic.Dictionary<int, Texture2D> _cachedCircleTextures
            = new System.Collections.Generic.Dictionary<int, Texture2D>();
        private static Texture2D _cachedStarTex;
        private static Texture2D _cachedHighlightTex;


        // ========== 初始化 ==========

        public void Initialize(TowerBase tower)
        {
            _tower = tower;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _spriteTransform = transform;
            _baseScale = transform.localScale;
            _breathePhase = Random.Range(0f, Mathf.PI * 2f);

            // 按层次从下到上创建
            CreateShadow();          // 最底层：投影阴影
            CreateThicknessLayer();   // 底座厚度（侧面）
            CreateGlow();             // 等级光环
            // 主体Sprite在中间（已存在）
            CreateOutline();          // 描边轮廓
            CreateInnerShadow();      // 内阴影/浮雕层
            CreateHighlight();        // 顶部高光
            CreateBottomReflect();    // 底部反光
            CreateLevelStars();       // 等级星星
            CreateSelectGlow();       // 选中高亮
            CreateAmbientParticles(); // 塔类型环境粒子
            CreateAimLine();          // 瞄准线
            CreateCooldownRing();     // 冷却环
            CreateChargeParticles();  // 蓄力粒子

            // 初始化空闲摇摆
            _idleSwayPhase = Random.Range(0f, Mathf.PI * 2f);

            // 触发入场动画
            _placeAnimTimer = PLACE_ANIM_DURATION;

            // 监听攻击事件
            EventBus.Instance.Subscribe<TowerAttackEvent>(OnTowerAttack);
        }

        private void OnDestroy()
        {
            if (EventBus.HasInstance)
            {
                EventBus.Instance.Unsubscribe<TowerAttackEvent>(OnTowerAttack);
            }

            // 清理所有子对象
            SafeDestroy(_shadowObj);
            SafeDestroy(_thicknessObj);
            SafeDestroy(_glowObj);
            SafeDestroy(_highlightObj);
            SafeDestroy(_aimLineObj);
            SafeDestroy(_cooldownObj);
            SafeDestroy(_selectGlowObj);
            SafeDestroy(_innerShadowObj);
            SafeDestroy(_bottomReflectObj);

            if (_outlineObjs != null)
                for (int i = 0; i < _outlineObjs.Length; i++)
                    SafeDestroy(_outlineObjs[i]);

            if (_starObjs != null)
                for (int i = 0; i < _starObjs.Length; i++)
                    SafeDestroy(_starObjs[i]);

            if (_chargeParticles != null)
                for (int i = 0; i < _chargeParticles.Length; i++)
                    SafeDestroy(_chargeParticles[i]);

            if (_ambientParticles != null)
                for (int i = 0; i < _ambientParticles.Length; i++)
                    SafeDestroy(_ambientParticles[i]);
        }

        private void SafeDestroy(GameObject obj)
        {
            if (obj != null) Destroy(obj);
        }

        // ================================================================
        // 创建立体感层次
        // ================================================================

        /// <summary>创建底座投影阴影（椭圆形，增加接地感）</summary>
        private void CreateShadow()
        {
            _shadowObj = new GameObject("TowerShadow");
            _shadowObj.transform.SetParent(transform);
            _shadowObj.transform.localPosition = new Vector3(0.03f, -0.38f, 0.01f);
            _shadowObj.transform.localScale = new Vector3(0.9f, 0.32f, 1f);

            _shadowRenderer = _shadowObj.AddComponent<SpriteRenderer>();
            _shadowRenderer.sortingOrder = _spriteRenderer.sortingOrder - 3;

            // 创建柔和的椭圆阴影纹理
            var tex = GetOrCreateCircleTexture(48);
            _shadowRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f), 48);
            _shadowRenderer.color = new Color(0f, 0f, 0f, 0.4f);
        }

        /// <summary>创建底座厚度层（模拟塔底座的侧面厚度）</summary>
        private void CreateThicknessLayer()
        {
            if (_spriteRenderer == null || _spriteRenderer.sprite == null) return;

            _thicknessObj = new GameObject("TowerThickness");
            _thicknessObj.transform.SetParent(transform);
            // 向下偏移，模拟底座侧面
            _thicknessObj.transform.localPosition = new Vector3(0.01f, -0.05f, 0.005f);
            _thicknessObj.transform.localScale = new Vector3(1.01f, 0.95f, 1f);

            _thicknessRenderer = _thicknessObj.AddComponent<SpriteRenderer>();
            _thicknessRenderer.sortingOrder = _spriteRenderer.sortingOrder - 1;
            _thicknessRenderer.sprite = _spriteRenderer.sprite;
            _thicknessRenderer.color = THICKNESS_COLOR;
        }

        /// <summary>创建描边轮廓（8方向偏移叠加，让塔从背景中弹出）</summary>
        private void CreateOutline()
        {
            if (_spriteRenderer == null || _spriteRenderer.sprite == null) return;

            _outlineObjs = new GameObject[OUTLINE_DIRECTIONS];
            _outlineRenderers = new SpriteRenderer[OUTLINE_DIRECTIONS];

            for (int i = 0; i < OUTLINE_DIRECTIONS; i++)
            {
                float angle = (float)i / OUTLINE_DIRECTIONS * Mathf.PI * 2f;
                float ox = Mathf.Cos(angle) * OUTLINE_OFFSET;
                float oy = Mathf.Sin(angle) * OUTLINE_OFFSET;

                _outlineObjs[i] = new GameObject($"TowerOutline_{i}");
                _outlineObjs[i].transform.SetParent(transform);
                _outlineObjs[i].transform.localPosition = new Vector3(ox, oy, 0.003f);
                _outlineObjs[i].transform.localScale = Vector3.one;

                _outlineRenderers[i] = _outlineObjs[i].AddComponent<SpriteRenderer>();
                _outlineRenderers[i].sortingOrder = _spriteRenderer.sortingOrder - 1;
                _outlineRenderers[i].sprite = _spriteRenderer.sprite;
                _outlineRenderers[i].color = OUTLINE_COLOR;
            }
        }

        /// <summary>创建顶部高光层（模拟从上方照射的光源）</summary>
        private void CreateHighlight()
        {
            _highlightObj = new GameObject("TowerHighlight");
            _highlightObj.transform.SetParent(transform);
            _highlightObj.transform.localPosition = new Vector3(-0.01f, 0.04f, -0.005f);
            _highlightObj.transform.localScale = new Vector3(0.8f, 0.45f, 1f);

            _highlightRenderer = _highlightObj.AddComponent<SpriteRenderer>();
            _highlightRenderer.sortingOrder = _spriteRenderer.sortingOrder + 1;

            // 创建从上到下的渐变高光纹理
            var tex = GetOrCreateHighlightTexture(32, 64);
            _highlightRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 64), new Vector2(0.5f, 0.7f), 64);
            _highlightRenderer.color = HIGHLIGHT_COLOR;
        }

        /// <summary>创建内阴影/浮雕层（增加立体感深度）</summary>
        private void CreateInnerShadow()
        {
            if (_spriteRenderer == null || _spriteRenderer.sprite == null) return;

            _innerShadowObj = new GameObject("TowerInnerShadow");
            _innerShadowObj.transform.SetParent(transform);
            // 向右下偏移，模拟光源从左上方照射产生的内阴影
            _innerShadowObj.transform.localPosition = new Vector3(0.012f, -0.012f, -0.001f);
            _innerShadowObj.transform.localScale = Vector3.one;

            _innerShadowRenderer = _innerShadowObj.AddComponent<SpriteRenderer>();
            _innerShadowRenderer.sortingOrder = _spriteRenderer.sortingOrder + 1;
            _innerShadowRenderer.sprite = _spriteRenderer.sprite;
            _innerShadowRenderer.color = new Color(0f, 0f, 0f, 0.12f);
        }

        /// <summary>创建底部反光层（模拟地面反射光）</summary>
        private void CreateBottomReflect()
        {
            _bottomReflectObj = new GameObject("TowerBottomReflect");
            _bottomReflectObj.transform.SetParent(transform);
            _bottomReflectObj.transform.localPosition = new Vector3(0f, -0.02f, -0.003f);
            _bottomReflectObj.transform.localScale = new Vector3(0.7f, 0.25f, 1f);

            _bottomReflectRenderer = _bottomReflectObj.AddComponent<SpriteRenderer>();
            _bottomReflectRenderer.sortingOrder = _spriteRenderer.sortingOrder + 1;

            var tex = GetOrCreateHighlightTexture(32, 64);
            _bottomReflectRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 64), new Vector2(0.5f, 0.3f), 64);
            // 底部反光用暖色调
            _bottomReflectRenderer.color = new Color(1f, 0.95f, 0.85f, 0.08f);
        }

        /// <summary>创建选中高亮光环</summary>
        private void CreateSelectGlow()
        {
            _selectGlowObj = new GameObject("TowerSelectGlow");
            _selectGlowObj.transform.SetParent(transform);
            _selectGlowObj.transform.localPosition = new Vector3(0f, 0f, 0.009f);
            _selectGlowObj.transform.localScale = new Vector3(1.6f, 1.6f, 1f);

            _selectGlowRenderer = _selectGlowObj.AddComponent<SpriteRenderer>();
            _selectGlowRenderer.sortingOrder = _spriteRenderer.sortingOrder - 1;

            var tex = GetOrCreateCircleTexture(48);
            _selectGlowRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f), 48);
            _selectGlowRenderer.color = new Color(1f, 1f, 1f, 0f); // 初始不可见
            _selectGlowObj.SetActive(false);
        }

        /// <summary>创建塔类型环境粒子（漂浮在塔周围的特征粒子）</summary>
        private void CreateAmbientParticles()
        {
            _ambientParticles = new GameObject[AMBIENT_PARTICLE_COUNT];
            _ambientPhases = new float[AMBIENT_PARTICLE_COUNT];
            _ambientSpeeds = new float[AMBIENT_PARTICLE_COUNT];
            _ambientRadii = new float[AMBIENT_PARTICLE_COUNT];

            var tex = GetOrCreateCircleTexture(8);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 32);

            int typeIdx = GetTowerTypeIndex();
            Color ambientColor = typeIdx >= 0 && typeIdx < TowerAmbientColors.Length
                ? TowerAmbientColors[typeIdx]
                : new Color(1f, 1f, 1f, 0.3f);

            for (int i = 0; i < AMBIENT_PARTICLE_COUNT; i++)
            {
                _ambientParticles[i] = new GameObject($"AmbientP_{i}");
                _ambientParticles[i].transform.SetParent(transform);

                var sr = _ambientParticles[i].AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = _spriteRenderer.sortingOrder + 2;
                sr.color = ambientColor;

                float s = Random.Range(0.4f, 0.8f);
                _ambientParticles[i].transform.localScale = new Vector3(s, s, 1f);

                _ambientPhases[i] = Random.Range(0f, Mathf.PI * 2f);
                _ambientSpeeds[i] = Random.Range(0.8f, 1.5f);
                _ambientRadii[i] = Random.Range(0.25f, 0.5f);
            }
        }

        /// <summary>创建等级光环</summary>
        private void CreateGlow()
        {
            _glowObj = new GameObject("TowerGlow");
            _glowObj.transform.SetParent(transform);
            _glowObj.transform.localPosition = new Vector3(0f, -0.15f, 0.008f);
            _glowObj.transform.localScale = new Vector3(1.3f, 0.55f, 1f);

            _glowRenderer = _glowObj.AddComponent<SpriteRenderer>();
            _glowRenderer.sortingOrder = _spriteRenderer.sortingOrder - 2;

            var tex = GetOrCreateCircleTexture(48);
            _glowRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f), 48);

            UpdateGlowColor();
        }

        /// <summary>创建等级星星装饰</summary>
        private void CreateLevelStars()
        {
            _starObjs = new GameObject[MAX_STARS];
            _starRenderers = new SpriteRenderer[MAX_STARS];

            var tex = GetOrCreateStarTexture(16);
            var starSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 32);

            for (int i = 0; i < MAX_STARS; i++)
            {
                _starObjs[i] = new GameObject($"TowerStar_{i}");
                _starObjs[i].transform.SetParent(transform);

                // 星星排列在塔底部，水平居中
                float xOffset = (i - (_tower != null ? (_tower.CurrentLevel - 1) * 0.5f : 0f)) * 0.12f;
                _starObjs[i].transform.localPosition = new Vector3(xOffset, -0.42f, -0.01f);
                _starObjs[i].transform.localScale = new Vector3(0.8f, 0.8f, 1f);

                _starRenderers[i] = _starObjs[i].AddComponent<SpriteRenderer>();
                _starRenderers[i].sortingOrder = _spriteRenderer.sortingOrder + 2;
                _starRenderers[i].sprite = starSprite;

                _starObjs[i].SetActive(false);
            }

            UpdateLevelStars();
        }

        /// <summary>创建瞄准线</summary>
        private void CreateAimLine()
        {
            _aimLineObj = new GameObject("AimLine");
            _aimLineObj.transform.SetParent(transform);
            _aimLineObj.transform.localPosition = Vector3.zero;

            _aimLine = _aimLineObj.AddComponent<LineRenderer>();
            _aimLine.material = new Material(Shader.Find("Sprites/Default"));
            _aimLine.startWidth = 0.03f;
            _aimLine.endWidth = 0.008f;
            _aimLine.positionCount = 2;
            _aimLine.useWorldSpace = true;
            _aimLine.sortingOrder = _spriteRenderer.sortingOrder + 1;
            _aimLine.enabled = false;
        }

        /// <summary>创建冷却进度环</summary>
        private void CreateCooldownRing()
        {
            _cooldownObj = new GameObject("CooldownRing");
            _cooldownObj.transform.SetParent(transform);
            _cooldownObj.transform.localPosition = Vector3.zero;

            _cooldownRing = _cooldownObj.AddComponent<LineRenderer>();
            _cooldownRing.material = new Material(Shader.Find("Sprites/Default"));
            _cooldownRing.startWidth = 0.035f;
            _cooldownRing.endWidth = 0.035f;
            _cooldownRing.useWorldSpace = true;
            _cooldownRing.loop = false;
            _cooldownRing.sortingOrder = _spriteRenderer.sortingOrder + 2;
            _cooldownRing.positionCount = 0;
        }

        /// <summary>创建蓄力粒子</summary>
        private void CreateChargeParticles()
        {
            _chargeParticles = new GameObject[CHARGE_PARTICLE_COUNT];
            var tex = GetOrCreateCircleTexture(8);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 32);

            for (int i = 0; i < CHARGE_PARTICLE_COUNT; i++)
            {
                _chargeParticles[i] = new GameObject($"ChargeP_{i}");
                _chargeParticles[i].transform.SetParent(transform);
                var sr = _chargeParticles[i].AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = _spriteRenderer.sortingOrder + 3;
                _chargeParticles[i].SetActive(false);
            }
        }

        // ================================================================
        // 纹理生成（带缓存）
        // ================================================================

        /// <summary>获取或创建圆形渐变纹理</summary>
        private static Texture2D GetOrCreateCircleTexture(int size)
        {
            if (_cachedCircleTextures.TryGetValue(size, out Texture2D cached) && cached != null)
                return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float alpha = Mathf.Clamp01(1f - dist * dist);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _cachedCircleTextures[size] = tex;
            return tex;
        }


        /// <summary>获取或创建星星纹理</summary>
        private static Texture2D GetOrCreateStarTexture(int size)
        {
            if (_cachedStarTex != null) return _cachedStarTex;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // 星形：用极坐标的角度调制半径
                    float angle = Mathf.Atan2(dy, dx);
                    float starRadius = 0.5f + 0.5f * Mathf.Cos(angle * 5f); // 5角星
                    starRadius = Mathf.Lerp(0.4f, 1f, starRadius);

                    float alpha = 0f;
                    if (dist < starRadius)
                    {
                        alpha = Mathf.Clamp01(1f - dist / starRadius);
                        alpha = alpha * alpha; // 更柔和的边缘
                    }
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _cachedStarTex = tex;
            return tex;
        }

        /// <summary>获取或创建高光渐变纹理（从上到下渐变）</summary>
        private static Texture2D GetOrCreateHighlightTexture(int w, int h)
        {
            if (_cachedHighlightTex != null) return _cachedHighlightTex;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float centerX = w / 2f;
            for (int y = 0; y < h; y++)
            {
                // 从顶部到底部渐变（顶部亮，底部透明）
                float verticalFade = Mathf.Clamp01((float)y / h);
                verticalFade = verticalFade * verticalFade; // 二次曲线，顶部更集中

                for (int x = 0; x < w; x++)
                {
                    // 水平方向也有渐变（中心亮，边缘暗）
                    float horizontalDist = Mathf.Abs(x - centerX) / centerX;
                    float horizontalFade = Mathf.Clamp01(1f - horizontalDist * horizontalDist);

                    float alpha = verticalFade * horizontalFade;
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _cachedHighlightTex = tex;
            return tex;
        }

        // ================================================================
        // Update
        // ================================================================

        private void Update()
        {
            if (_tower == null) return;

            UpdatePlaceAnimation();
            UpdateBreathe();
            UpdateIdleSway();
            UpdateRecoil();
            UpdateFlash();
            UpdateOutlineBright();
            UpdateGlow();
            UpdateDynamicShadow();
            UpdateSelectGlow();
            UpdateAmbientParticles();
            UpdateUpgradeFlash();
            UpdateAimLine();
            UpdateCooldownRing();
            UpdateChargeParticles();
            UpdateOutlineSprite();
            UpdateInnerShadowSprite();
        }

        // ================================================================
        // 入场动画
        // ================================================================

        private void UpdatePlaceAnimation()
        {
            if (_placeAnimTimer <= 0f) return;

            _placeAnimTimer -= Time.deltaTime;
            float t = 1f - (_placeAnimTimer / PLACE_ANIM_DURATION);

            if (t >= 1f)
            {
                _placeAnimTimer = -1f;
                _spriteTransform.localScale = _baseScale;
                return;
            }

            // 弹性入场：从小到大，带回弹
            float elastic = EaseOutBack(t);
            _spriteTransform.localScale = _baseScale * elastic;

            // 入场时淡入
            if (_spriteRenderer != null)
            {
                float alpha = Mathf.Clamp01(t * 2f); // 前半段完成淡入
                Color c = _spriteRenderer.color;
                c.a = alpha;
                _spriteRenderer.color = c;
            }
        }

        private float EaseOutBack(float t)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        // ================================================================
        // 呼吸动画
        // ================================================================

        private void UpdateBreathe()
        {
            if (_placeAnimTimer > 0f) return; // 入场动画期间不呼吸

            _breathePhase += Time.deltaTime;
            float breathe = Mathf.Sin(_breathePhase * 1.8f) * 0.015f;
            float squash = 1f + breathe;
            float stretch = 1f - breathe * 0.6f;

            // 叠加后坐力缩放
            if (_recoilTimer > 0)
            {
                float rt = _recoilTimer / _recoilDuration;
                float recoilCurve = Mathf.Sin(rt * Mathf.PI);
                squash += recoilCurve * 0.08f;
                stretch -= recoilCurve * 0.04f;
            }

            _spriteTransform.localScale = new Vector3(
                _baseScale.x * stretch,
                _baseScale.y * squash,
                _baseScale.z
            );
        }

        // ================================================================
        // 空闲摇摆（无目标时轻微左右摇摆，增加生动感）
        // ================================================================

        private void UpdateIdleSway()
        {
            if (_placeAnimTimer > 0f) return;

            _hasTarget = _tower.CurrentTarget != null;

            if (!_hasTarget)
            {
                _idleSwayPhase += Time.deltaTime;
                float sway = Mathf.Sin(_idleSwayPhase * 1.2f) * 0.4f;
                // 轻微旋转摇摆
                _spriteTransform.localRotation = Quaternion.Euler(0, 0, sway);
            }
            else
            {
                // 有目标时平滑回正
                _spriteTransform.localRotation = Quaternion.Lerp(
                    _spriteTransform.localRotation,
                    Quaternion.identity,
                    Time.deltaTime * 8f
                );
            }
        }

        // ================================================================
        // 攻击后坐力
        // ================================================================

        private void OnTowerAttack(TowerAttackEvent evt)
        {
            if (_tower == null || evt.TowerId != _tower.InstanceId) return;

            _recoilTimer = _recoilDuration;
            Vector3 dir = (evt.TargetPos - evt.TowerPos).normalized;
            _recoilDirection = -dir;

            _flashTimer = 0.12f;
            _outlineBrightTimer = 0.18f; // 攻击时描边闪亮

            TriggerChargeParticleBurst(evt.TowerPos, dir);
        }

        private void UpdateRecoil()
        {
            if (_recoilTimer > 0)
            {
                _recoilTimer -= Time.deltaTime;

                // 位移回弹（向攻击反方向短暂位移后弹回）
                float rt = _recoilTimer / _recoilDuration;
                float displacement = Mathf.Sin(rt * Mathf.PI) * 0.035f;
                _recoilOffset = _recoilDirection * displacement;
            }
            else if (_recoilOffset != Vector3.zero)
            {
                // 平滑回到原位
                _recoilOffset = Vector3.Lerp(_recoilOffset, Vector3.zero, Time.deltaTime * 15f);
                if (_recoilOffset.magnitude < 0.001f)
                    _recoilOffset = Vector3.zero;
            }

            // 将后坐力偏移应用到所有子视觉层（而非塔自身的transform）
            ApplyRecoilOffset();
        }

        /// <summary>将后坐力偏移应用到子视觉对象，而不是塔自身的position</summary>
        private void ApplyRecoilOffset()
        {
            // 偏移描边层
            if (_outlineObjs != null)
            {
                for (int i = 0; i < OUTLINE_DIRECTIONS; i++)
                {
                    if (_outlineObjs[i] != null)
                    {
                        float angle = (float)i / OUTLINE_DIRECTIONS * Mathf.PI * 2f;
                        float ox = Mathf.Cos(angle) * OUTLINE_OFFSET;
                        float oy = Mathf.Sin(angle) * OUTLINE_OFFSET;
                        _outlineObjs[i].transform.localPosition = new Vector3(ox + _recoilOffset.x, oy + _recoilOffset.y, 0.003f);
                    }
                }
            }

            // 偏移厚度层
            if (_thicknessObj != null)
            {
                _thicknessObj.transform.localPosition = new Vector3(0.01f + _recoilOffset.x, -0.05f + _recoilOffset.y, 0.005f);
            }

            // 偏移内阴影层
            if (_innerShadowObj != null)
            {
                _innerShadowObj.transform.localPosition = new Vector3(0.012f + _recoilOffset.x, -0.012f + _recoilOffset.y, -0.001f);
            }

            // 偏移高光层
            if (_highlightObj != null)
            {
                _highlightObj.transform.localPosition = new Vector3(-0.01f + _recoilOffset.x, 0.04f + _recoilOffset.y, -0.005f);
            }

            // 主体Sprite的偏移通过SpriteRenderer的transform来实现
            // 注意：不能直接修改_spriteTransform.localPosition（那是塔自身的世界位置）
            // 这里通过视觉子层的偏移来模拟后坐力效果
        }


        private void UpdateFlash()
        {
            if (_flashTimer > 0)
            {
                _flashTimer -= Time.deltaTime;
                float t = _flashTimer / 0.12f;
                if (_spriteRenderer != null)
                {
                    // 攻击时短暂变亮（白色闪光）
                    _spriteRenderer.color = Color.Lerp(Color.white, new Color(1.4f, 1.3f, 1.2f, 1f), t);
                }
            }
            else if (_spriteRenderer != null && _spriteRenderer.color != Color.white && _placeAnimTimer <= 0f)
            {
                _spriteRenderer.color = Color.white;
            }
        }

        // ================================================================
        // 攻击时描边闪亮
        // ================================================================

        private void UpdateOutlineBright()
        {
            if (_outlineRenderers == null) return;

            if (_outlineBrightTimer > 0)
            {
                _outlineBrightTimer -= Time.deltaTime;
                float t = _outlineBrightTimer / 0.18f;

                // 攻击瞬间描边变亮（从塔类型颜色渐变回深色）
                Color brightColor = GetAimLineColor();
                brightColor.a = 0.8f * t;
                Color blended = Color.Lerp(OUTLINE_COLOR, brightColor, t);

                for (int i = 0; i < OUTLINE_DIRECTIONS; i++)
                {
                    if (_outlineRenderers[i] != null)
                        _outlineRenderers[i].color = blended;
                }
            }
        }

        // ================================================================
        // 动态阴影（随呼吸微动）
        // ================================================================

        private void UpdateDynamicShadow()
        {
            if (_shadowObj == null) return;

            float breathe = Mathf.Sin(_breathePhase * 1.8f);
            // 阴影随呼吸轻微缩放（塔"高"时阴影小，"矮"时阴影大）
            float shadowScale = 0.9f - breathe * 0.03f;
            float shadowAlpha = 0.4f - breathe * 0.03f;

            _shadowObj.transform.localScale = new Vector3(shadowScale, 0.32f, 1f);
            _shadowRenderer.color = new Color(0f, 0f, 0f, shadowAlpha);
        }

        // ================================================================
        // 选中高亮
        // ================================================================

        /// <summary>设置塔的选中状态（外部调用）</summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (_selectGlowObj != null)
                _selectGlowObj.SetActive(selected);
        }

        private void UpdateSelectGlow()
        {
            if (!_isSelected || _selectGlowRenderer == null) return;

            _selectGlowTimer += Time.deltaTime;
            float pulse = 0.3f + Mathf.Sin(_selectGlowTimer * 4f) * 0.15f;

            Color glowColor = GetAimLineColor();
            glowColor.a = pulse;
            _selectGlowRenderer.color = glowColor;

            // 选中光环轻微缩放脉动
            float scale = 1.6f + Mathf.Sin(_selectGlowTimer * 3f) * 0.1f;
            _selectGlowObj.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // ================================================================
        // 塔类型环境粒子
        // ================================================================

        private void UpdateAmbientParticles()
        {
            if (_ambientParticles == null) return;

            for (int i = 0; i < AMBIENT_PARTICLE_COUNT; i++)
            {
                if (_ambientParticles[i] == null) continue;

                _ambientPhases[i] += Time.deltaTime * _ambientSpeeds[i];
                float phase = _ambientPhases[i];
                float radius = _ambientRadii[i];

                // 环绕塔的椭圆轨道运动
                float x = Mathf.Cos(phase) * radius;
                float y = Mathf.Sin(phase * 0.7f) * radius * 0.6f + 0.1f;
                _ambientParticles[i].transform.localPosition = new Vector3(x, y, -0.01f);

                // 透明度呼吸
                var sr = _ambientParticles[i].GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    float alpha = (0.3f + Mathf.Sin(phase * 2f) * 0.2f);
                    Color c = sr.color;
                    c.a = alpha;
                    sr.color = c;

                    // 大小脉动
                    float s = 0.5f + Mathf.Sin(phase * 1.5f) * 0.15f;
                    _ambientParticles[i].transform.localScale = new Vector3(s, s, 1f);
                }
            }
        }

        // ================================================================
        // 升级闪光动画
        // ================================================================

        /// <summary>触发升级闪光（外部调用）</summary>
        public void TriggerUpgradeFlash()
        {
            _upgradeFlashTimer = UPGRADE_FLASH_DURATION;
        }

        private void UpdateUpgradeFlash()
        {
            if (_upgradeFlashTimer <= 0f) return;

            _upgradeFlashTimer -= Time.deltaTime;
            float t = 1f - (_upgradeFlashTimer / UPGRADE_FLASH_DURATION);

            if (t >= 1f)
            {
                _upgradeFlashTimer = -1f;
                if (_spriteRenderer != null)
                    _spriteRenderer.color = Color.white;
                return;
            }

            // 升级闪光：先变白再变金色再恢复
            if (_spriteRenderer != null)
            {
                Color flashColor;
                if (t < 0.2f)
                {
                    // 快速变白
                    flashColor = Color.Lerp(Color.white, new Color(1.5f, 1.5f, 1.5f, 1f), t / 0.2f);
                }
                else if (t < 0.5f)
                {
                    // 变金色
                    float ft = (t - 0.2f) / 0.3f;
                    flashColor = Color.Lerp(new Color(1.5f, 1.5f, 1.5f, 1f), new Color(1.2f, 1.0f, 0.5f, 1f), ft);
                }
                else
                {
                    // 恢复正常
                    float ft = (t - 0.5f) / 0.5f;
                    flashColor = Color.Lerp(new Color(1.2f, 1.0f, 0.5f, 1f), Color.white, ft);
                }
                _spriteRenderer.color = flashColor;
            }

            // 升级时缩放弹跳
            if (t < 0.3f)
            {
                float bounce = 1f + Mathf.Sin(t / 0.3f * Mathf.PI) * 0.12f;
                _spriteTransform.localScale = _baseScale * bounce;
            }
        }

        // ================================================================
        // 等级光环
        // ================================================================

        private void UpdateGlow()
        {
            if (_glowRenderer == null || _tower == null) return;

            _glowPulseTimer += Time.deltaTime;
            float pulse = 0.85f + Mathf.Sin(_glowPulseTimer * 2.2f) * 0.15f;

            int lvIdx = Mathf.Clamp(_tower.CurrentLevel - 1, 0, LevelGlowColors.Length - 1);
            Color baseColor = LevelGlowColors[lvIdx];
            _glowRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * pulse);

            // 等级越高光环越大
            float scale = 1.3f + (_tower.CurrentLevel - 1) * 0.2f;
            _glowObj.transform.localScale = new Vector3(scale, scale * 0.45f, 1f);
        }

        public void UpdateGlowColor()
        {
            if (_glowRenderer == null || _tower == null) return;
            int lvIdx = Mathf.Clamp(_tower.CurrentLevel - 1, 0, LevelGlowColors.Length - 1);
            _glowRenderer.color = LevelGlowColors[lvIdx];

            UpdateLevelStars();
        }

        // ================================================================
        // 等级星星
        // ================================================================

        private void UpdateLevelStars()
        {
            if (_starObjs == null || _tower == null) return;

            int level = _tower.CurrentLevel;
            int lvIdx = Mathf.Clamp(level - 1, 0, LevelStarColors.Length - 1);
            Color starColor = LevelStarColors[lvIdx];

            for (int i = 0; i < MAX_STARS; i++)
            {
                bool active = i < level;
                _starObjs[i].SetActive(active);

                if (active)
                {
                    // 重新计算位置（居中排列）
                    float totalWidth = (level - 1) * 0.12f;
                    float startX = -totalWidth / 2f;
                    _starObjs[i].transform.localPosition = new Vector3(startX + i * 0.12f, -0.42f, -0.01f);
                    _starRenderers[i].color = starColor;

                    // 星星轻微旋转动画
                    float rot = Mathf.Sin(Time.time * 1.5f + i * 1.2f) * 15f;
                    _starObjs[i].transform.localRotation = Quaternion.Euler(0, 0, rot);
                }
            }
        }

        // ================================================================
        // 描边轮廓同步
        // ================================================================

        /// <summary>当主体Sprite变化时同步描边层的Sprite</summary>
        private void UpdateOutlineSprite()
        {
            if (_outlineRenderers == null || _spriteRenderer == null) return;

            Sprite currentSprite = _spriteRenderer.sprite;
            if (currentSprite == null) return;

            // 只在Sprite变化时更新（升级换图）
            if (_outlineRenderers[0] != null && _outlineRenderers[0].sprite != currentSprite)
            {
                for (int i = 0; i < OUTLINE_DIRECTIONS; i++)
                {
                    if (_outlineRenderers[i] != null)
                    {
                        _outlineRenderers[i].sprite = currentSprite;
                    }
                }

                // 同步厚度层
                if (_thicknessRenderer != null)
                {
                    _thicknessRenderer.sprite = currentSprite;
                }
            }
        }

        /// <summary>同步内阴影层Sprite</summary>
        private void UpdateInnerShadowSprite()
        {
            if (_innerShadowRenderer == null || _spriteRenderer == null) return;

            Sprite currentSprite = _spriteRenderer.sprite;
            if (currentSprite != null && _innerShadowRenderer.sprite != currentSprite)
            {
                _innerShadowRenderer.sprite = currentSprite;
            }
        }

        // ================================================================
        // 瞄准线
        // ================================================================

        private void UpdateAimLine()
        {
            if (_aimLine == null || _tower == null) return;

            Transform target = _tower.CurrentTarget;
            if (target != null && target.gameObject.activeInHierarchy)
            {
                _aimLine.enabled = true;
                Vector3 start = transform.position;
                Vector3 end = target.position;

                _aimLine.SetPosition(0, start);
                _aimLine.SetPosition(1, end);

                Color aimColor = GetAimLineColor();
                aimColor.a = 0.25f + Mathf.Sin(Time.time * 5f) * 0.1f;
                _aimLine.startColor = aimColor;
                _aimLine.endColor = new Color(aimColor.r, aimColor.g, aimColor.b, 0.03f);
            }
            else
            {
                _aimLine.enabled = false;
            }
        }

        private Color GetAimLineColor()
        {
            if (_tower == null) return Color.white;
            switch (_tower.Type)
            {
                case TowerType.Archer: return new Color(1f, 0.9f, 0.3f);
                case TowerType.Mage: return new Color(0.6f, 0.3f, 1f);
                case TowerType.Ice: return new Color(0.4f, 0.8f, 1f);
                case TowerType.Cannon: return new Color(1f, 0.5f, 0.1f);
                case TowerType.Poison: return new Color(0.3f, 1f, 0.3f);
                default: return Color.white;
            }
        }

        // ================================================================
        // 冷却进度环
        // ================================================================

        private void UpdateCooldownRing()
        {
            if (_cooldownRing == null || _tower == null || !_tower.Config.canAttack) return;

            float interval = _tower.AttackInterval;
            if (interval <= 0) return;

            if (_tower.CurrentTarget != null)
            {
                int segments = 24;
                float phase = (Time.time % interval) / interval;
                int activeSegments = Mathf.CeilToInt(phase * segments);
                _cooldownRing.positionCount = activeSegments + 1;

                float radius = 0.48f;
                Color ringColor = GetAimLineColor();
                ringColor.a = 0.35f;
                _cooldownRing.startColor = ringColor;
                _cooldownRing.endColor = ringColor;

                for (int i = 0; i <= activeSegments; i++)
                {
                    float angle = (float)i / segments * 360f - 90f;
                    float rad = angle * Mathf.Deg2Rad;
                    float x = Mathf.Cos(rad) * radius + transform.position.x;
                    float y = Mathf.Sin(rad) * radius + transform.position.y;
                    _cooldownRing.SetPosition(i, new Vector3(x, y, -0.05f));
                }
            }
            else
            {
                _cooldownRing.positionCount = 0;
            }
        }

        // ================================================================
        // 蓄力粒子
        // ================================================================

        private float[] _particleTimers;
        private Vector3[] _particleVelocities;
        private float[] _particleLifetimes;

        private void TriggerChargeParticleBurst(Vector3 origin, Vector3 shootDir)
        {
            if (_chargeParticles == null) return;

            if (_particleTimers == null)
            {
                _particleTimers = new float[CHARGE_PARTICLE_COUNT];
                _particleVelocities = new Vector3[CHARGE_PARTICLE_COUNT];
                _particleLifetimes = new float[CHARGE_PARTICLE_COUNT];
            }

            Color particleColor = GetAimLineColor();

            for (int i = 0; i < CHARGE_PARTICLE_COUNT; i++)
            {
                _chargeParticles[i].SetActive(true);
                _chargeParticles[i].transform.position = origin;

                float spread = Random.Range(-60f, 60f);
                float angle = Mathf.Atan2(shootDir.y, shootDir.x) * Mathf.Rad2Deg + spread;
                float rad = angle * Mathf.Deg2Rad;
                float speed = Random.Range(1.5f, 4f);
                _particleVelocities[i] = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * speed;

                _particleLifetimes[i] = Random.Range(0.15f, 0.35f);
                _particleTimers[i] = _particleLifetimes[i];

                var sr = _chargeParticles[i].GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = new Color(particleColor.r, particleColor.g, particleColor.b, 0.9f);
                    float s = Random.Range(0.8f, 1.5f);
                    _chargeParticles[i].transform.localScale = new Vector3(s, s, 1f);
                }
            }
        }

        private void UpdateChargeParticles()
        {
            if (_chargeParticles == null || _particleTimers == null) return;

            for (int i = 0; i < CHARGE_PARTICLE_COUNT; i++)
            {
                if (!_chargeParticles[i].activeInHierarchy) continue;

                _particleTimers[i] -= Time.deltaTime;
                if (_particleTimers[i] <= 0)
                {
                    _chargeParticles[i].SetActive(false);
                    continue;
                }

                _chargeParticles[i].transform.position += _particleVelocities[i] * Time.deltaTime;
                _particleVelocities[i] *= 0.92f;

                float t = _particleTimers[i] / _particleLifetimes[i];
                var sr = _chargeParticles[i].GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = t * 0.9f;
                    sr.color = c;
                    float scale = t * 1.2f;
                    _chargeParticles[i].transform.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        // ================================================================
        // 工具方法
        // ================================================================

        private int GetTowerTypeIndex()
        {
            if (_tower == null) return 0;
            switch (_tower.Type)
            {
                case TowerType.Archer: return 0;
                case TowerType.Mage: return 1;
                case TowerType.Ice: return 2;
                case TowerType.Cannon: return 3;
                case TowerType.Poison: return 4;
                default: return 0;
            }
        }
    }
}
