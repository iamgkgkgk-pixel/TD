// ============================================================
// 文件名：BattleSceneSetup.cs
// 功能描述：战斗场景入口 — 负责初始化所有战斗子系统并启动战斗
//          挂载到场景中的空GameObject即可自动运行完整一局
// 创建时间：2026-03-25
// 所属模块：Battle
// 对应交互：阶段三 G3-1（核心战斗可玩）
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Map;
using AetheraSurvivors.Battle.Tower;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Wave;
using AetheraSurvivors.Battle.Rune;
using AetheraSurvivors.Battle.Visual;
using AetheraSurvivors.Battle.Projectile;
using AetheraSurvivors.Battle.Polish;
using AetheraSurvivors.Battle.Performance;
using Logger = AetheraSurvivors.Framework.Logger;


namespace AetheraSurvivors.Battle
{
    /// <summary>
    /// 战斗场景入口 — 自动初始化所有子系统并启动战斗
    /// 
    /// 使用方式：
    ///   1. 在场景中创建空GameObject命名为 "BattleSetup"
    ///   2. 挂载此脚本
    ///   3. 运行场景即可看到完整战斗
    /// </summary>
    public class BattleSceneSetup : MonoBehaviour
    {
        [Header("自动启动")]
        [Tooltip("是否在Start时自动启动测试战斗")]
        [SerializeField] private bool _autoStartBattle = true;

        [Header("摄像机")]
        [Tooltip("主摄像机（留空则自动查找）")]
        [SerializeField] private Camera _mainCamera;

        private void Start()
        {
            Logger.I("BattleSetup", "═══ 战斗场景初始化开始 ═══");

            // 0. 确保主摄像机就绪
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            // 0.5 加载战斗背景图
            SetupBattleBackground();

            // 1. 预加载所有MonoSingleton（确保它们的GameObject在场景中创建）
            PreloadSingletons();

            // 2. 预加载纯C#单例
            RuneSystem.Preload();
            RuneSystem.Instance.Initialize();

            // 3. 创建战斗UI
            EnsureBattleUI();

            Logger.I("BattleSetup", "═══ 所有子系统初始化完成 ═══");

            // 4. 自动开始战斗
            if (_autoStartBattle)
            {
                Invoke(nameof(StartTestBattle), 0.2f); // 延迟一帧确保所有系统就绪
            }
        }

        /// <summary>
        /// 战斗背景 — 深色主题底色（与章节匹配）
        /// </summary>
        private void SetupBattleBackground()
        {
            if (_mainCamera == null) return;

            // 相机背景色：深绿（与草原章节融合）
            _mainCamera.backgroundColor = new Color(0.06f, 0.12f, 0.06f, 1f);

            Logger.I("BattleSetup", "战斗背景：深绿底色，地图加载后添加面板框");
        }

        /// <summary>
        /// 创建地图面板框 + 外围主题色填充
        /// 参考手游效果：圆角白框包裹游戏区域，外围用主题色装饰
        /// </summary>
        private void CreateMapEdgeVignette()
        {
            if (!Map.GridSystem.HasInstance || !Map.GridSystem.Instance.IsMapLoaded) return;

            var grid = Map.GridSystem.Instance;
            var bounds = grid.GetMapBounds();
            float mapW = bounds.size.x;
            float mapH = bounds.size.y;
            Vector3 mapCenter = bounds.center;

            var frameRoot = new GameObject("[MapFrame]");
            frameRoot.transform.SetParent(transform); // 作为BattleSceneSetup子对象，退出战斗时自动销毁
            frameRoot.transform.position = new Vector3(mapCenter.x, mapCenter.y, -0.3f);

            // ======== 1. 地图面板框（圆角矩形描边） ========
            // 生成圆角矩形纹理用于9-slice
            int texW = 128, texH = 128;
            int cornerR = 16;
            int borderW = 3;

            var frameTex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            Color borderColor = new Color(1f, 1f, 1f, 0.55f); // 半透明白描边
            Color innerColor = Color.clear; // 内部透明
            Color outerColor = Color.clear; // 外部透明

            for (int py = 0; py < texH; py++)
            {
                for (int px = 0; px < texW; px++)
                {
                    // 计算到圆角矩形边缘的距离
                    float dx = Mathf.Max(0, Mathf.Max(cornerR - px, px - (texW - 1 - cornerR)));
                    float dy = Mathf.Max(0, Mathf.Max(cornerR - py, py - (texH - 1 - cornerR)));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > cornerR + 1)
                        frameTex.SetPixel(px, py, outerColor); // 圆角外
                    else if (dist > cornerR - borderW)
                    {
                        // 描边区域（带抗锯齿）
                        float aa = Mathf.Clamp01(cornerR + 1 - dist);
                        frameTex.SetPixel(px, py, new Color(borderColor.r, borderColor.g, borderColor.b, borderColor.a * aa));
                    }
                    else
                        frameTex.SetPixel(px, py, innerColor); // 内部透明
                }
            }
            frameTex.Apply();
            frameTex.filterMode = FilterMode.Bilinear;

            // 9-slice 边距
            float slicePad = cornerR + 2;
            var frameSprite = Sprite.Create(frameTex, new Rect(0, 0, texW, texH),
                new Vector2(0.5f, 0.5f), texW / (mapW + 0.6f), 0, SpriteMeshType.FullRect,
                new Vector4(slicePad, slicePad, slicePad, slicePad));

            var frameObj = new GameObject("MapFrameBorder");
            frameObj.transform.SetParent(frameRoot.transform, false);
            frameObj.transform.localPosition = Vector3.zero;
            var frameSR = frameObj.AddComponent<SpriteRenderer>();
            frameSR.sprite = frameSprite;
            frameSR.drawMode = SpriteDrawMode.Sliced;
            frameSR.size = new Vector2(mapW + 0.6f, mapH + 0.6f);
            frameSR.sortingOrder = 8;
            frameSR.color = Color.white;

            // ======== 2. 内侧微暗边（给地图一点立体感/内凹阴影） ========
            var innerShadowTex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            int shadowWidth = 8;
            for (int py = 0; py < texH; py++)
            {
                for (int px = 0; px < texW; px++)
                {
                    // 同样的圆角矩形，但渲染内侧渐变阴影
                    float dx2 = Mathf.Max(0, Mathf.Max(cornerR - px, px - (texW - 1 - cornerR)));
                    float dy2 = Mathf.Max(0, Mathf.Max(cornerR - py, py - (texH - 1 - cornerR)));
                    float dist2 = Mathf.Sqrt(dx2 * dx2 + dy2 * dy2);

                    // 边缘内侧的渐变
                    float edgeDist = cornerR - dist2; // 距离圆角边缘的内侧距离
                    if (edgeDist < 0)
                        innerShadowTex.SetPixel(px, py, Color.clear);
                    else if (edgeDist < shadowWidth)
                    {
                        float t = edgeDist / shadowWidth;
                        float a = (1f - t) * 0.25f; // 边缘暗，中心透明
                        innerShadowTex.SetPixel(px, py, new Color(0, 0, 0, a));
                    }
                    else
                        innerShadowTex.SetPixel(px, py, Color.clear);
                }
            }
            innerShadowTex.Apply();
            innerShadowTex.filterMode = FilterMode.Bilinear;

            var innerShadowSprite = Sprite.Create(innerShadowTex, new Rect(0, 0, texW, texH),
                new Vector2(0.5f, 0.5f), texW / (mapW + 0.3f), 0, SpriteMeshType.FullRect,
                new Vector4(slicePad, slicePad, slicePad, slicePad));

            var shadowObj = new GameObject("MapInnerShadow");
            shadowObj.transform.SetParent(frameRoot.transform, false);
            shadowObj.transform.localPosition = new Vector3(0, 0, 0.01f);
            var shadowSR = shadowObj.AddComponent<SpriteRenderer>();
            shadowSR.sprite = innerShadowSprite;
            shadowSR.drawMode = SpriteDrawMode.Sliced;
            shadowSR.size = new Vector2(mapW + 0.3f, mapH + 0.3f);
            shadowSR.sortingOrder = 7;

            // ======== 3. 外围深色遮罩（上下左右四条大色块，遮住地图外的空白） ========
            float camH = _mainCamera != null ? _mainCamera.orthographicSize * 2f : 20f;
            float camW = _mainCamera != null ? camH * _mainCamera.aspect : 30f;
            float fillSize = Mathf.Max(camW, camH) * 2f; // 足够大的遮罩

            Color bgFill = new Color(0.06f, 0.12f, 0.06f, 1f); // 与相机背景色一致

            // 上
            CreateFillRect(frameRoot.transform, "FillTop", bgFill,
                new Vector3(0, mapH * 0.5f + fillSize * 0.5f + 0.3f, 0.02f),
                new Vector3(fillSize, fillSize, 1));
            // 下
            CreateFillRect(frameRoot.transform, "FillBottom", bgFill,
                new Vector3(0, -mapH * 0.5f - fillSize * 0.5f - 0.3f, 0.02f),
                new Vector3(fillSize, fillSize, 1));
            // 左
            CreateFillRect(frameRoot.transform, "FillLeft", bgFill,
                new Vector3(-mapW * 0.5f - fillSize * 0.5f - 0.3f, 0, 0.02f),
                new Vector3(fillSize, fillSize, 1));
            // 右
            CreateFillRect(frameRoot.transform, "FillRight", bgFill,
                new Vector3(mapW * 0.5f + fillSize * 0.5f + 0.3f, 0, 0.02f),
                new Vector3(fillSize, fillSize, 1));

            Logger.I("BattleSetup", "地图面板框已创建: {0:F1}x{1:F1}", mapW, mapH);
        }

        private void CreateFillRect(Transform parent, string name, Color color,
            Vector3 localPos, Vector3 scale)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localScale = scale;
            var sr = obj.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            sr.sortingOrder = 6; // 在面板框之下
        }

        /// <summary>
        /// 预加载所有MonoSingleton实例
        /// </summary>
        private void PreloadSingletons()
        {
            // 框架层（纯C#单例需要手动Initialize）
            EventBus.Preload();
            if (!EventBus.Instance.IsInitialized)
                EventBus.Instance.Initialize();
            ObjectPoolManager.Preload();


            // 地图系统
            GridSystem.Preload();
            MapRenderer.Preload();
            PathVisualizer.Preload();
            // Pathfinding 是纯C#单例，BattleManager加载关卡时会调用InitForMap
            Pathfinding.Preload();
            Pathfinding.Instance.Initialize();

            // 战斗核心
            BattleManager.Preload();
            BattleEconomyManager.Preload();
            BaseHealth.Preload();
            BattleInputHandler.Preload();

            // 塔系统
            TowerManager.Preload();

            // 怪物系统
            EnemySpawner.Preload();

            // 波次系统
            WaveManager.Preload();

            // 投射物系统
            ProjectileManager.Preload();

            // 命中特效系统
            HitEffectSystem.Preload();


            // 视觉反馈系统
            TowerAttackVFX.Preload();
            EnemyVisualManager.Preload();
            DamagePopupManager.Preload();
            BattleFieldVFX.Preload();
            ParticleVFXSystem.Preload();

            // 战斗手感 & 打磨系统
            BattleFeelSystem.Preload();
            BattleAudioFeedback.Preload();
            BattleBugFixer.Preload();
            BattleBalanceAdjuster.Preload();

            // 性能优化系统
            SpatialPartition.Preload();
            BattlePerformanceOptimizer.Preload();

            // 战斗摄像机系统（缩放/拖拽/边界限制）
            PreloadBattleCamera();

            // 画面增强系统（纹理质量修复 + 后处理效果 + 抗锯齿）
            PreloadVisualPolish();

            Logger.I("BattleSetup", "所有子系统预加载完成");




        }

        /// <summary>
        /// 预加载战斗摄像机 — 确保BattleCamera挂载到主摄像机上
        /// 而不是创建新的空GameObject，避免相机控制权冲突
        /// </summary>
        private void PreloadBattleCamera()
        {
            // 检查场景中是否已有BattleCamera
            if (BattleCamera.HasInstance) return;

            var existingCamera = FindObjectOfType<BattleCamera>();
            if (existingCamera != null) return;

            // 将BattleCamera挂载到主摄像机对象上（而非创建新GameObject）
            if (_mainCamera != null)
            {
                _mainCamera.gameObject.AddComponent<BattleCamera>();
                Logger.I("BattleSetup", "BattleCamera已挂载到主摄像机: {0}", _mainCamera.gameObject.name);
            }
            else
            {
                // 兜底：走默认Preload流程（会创建新GameObject）
                BattleCamera.Preload();
                Logger.W("BattleSetup", "主摄像机为空，BattleCamera走默认Preload");
            }
        }

        /// <summary>
        /// 预加载画面增强系统 — 挂载到主摄像机上以支持OnRenderImage后处理
        /// </summary>
        private void PreloadVisualPolish()
        {
            if (VisualPolishSystem.HasInstance) return;


            var existing = FindObjectOfType<VisualPolishSystem>();
            if (existing != null) return;

            // 将VisualPolishSystem挂载到主摄像机上（OnRenderImage需要在Camera所在的GameObject上）
            if (_mainCamera != null)
            {
                _mainCamera.gameObject.AddComponent<VisualPolishSystem>();
                Logger.I("BattleSetup", "VisualPolishSystem已挂载到主摄像机");
            }
            else
            {
                VisualPolishSystem.Preload();
                Logger.W("BattleSetup", "主摄像机为空，VisualPolishSystem走默认Preload");
            }
        }


        /// <summary>
        /// 确保战斗UI存在
        /// </summary>


        private void EnsureBattleUI()
        {
            if (FindObjectOfType<BattleUI>() == null)
            {
                var uiObj = new GameObject("[BattleUI]");
                uiObj.AddComponent<BattleUI>();
                DontDestroyOnLoad(uiObj);
                Logger.I("BattleSetup", "BattleUI已自动创建");
            }
        }

        /// <summary>
        /// 启动测试战斗
        /// </summary>
        private void StartTestBattle()
        {
            Logger.I("BattleSetup", "正在启动测试战斗...");
            BattleManager.Instance.StartBattle("test");

            // 延迟调整摄像机适配地图
            Invoke(nameof(FitCameraToMap), 0.3f);
        }

        /// <summary>
        /// 调整摄像机以适配地图大小
        /// 优先委托给BattleCamera处理，避免两套系统争夺相机控制权
        /// </summary>
        private void FitCameraToMap()
        {
            // 优先使用BattleCamera的FitToMap（它会正确设置_targetPosition和_targetOrthoSize）
            if (BattleCamera.HasInstance)
            {
                BattleCamera.Instance.FitToMap();
                Logger.I("BattleSetup", "摄像机适配已委托给BattleCamera");
                CreateMapEdgeVignette();
                return;
            }

            // 兜底：直接操作主摄像机（BattleCamera不存在时）
            if (_mainCamera == null || !GridSystem.HasInstance || !GridSystem.Instance.IsMapLoaded) return;

            var grid = GridSystem.Instance;
            var bounds = grid.GetMapBounds();

            // 强制设为正交模式（2D塔防游戏必须使用正交摄像机）
            if (!_mainCamera.orthographic)
            {
                _mainCamera.orthographic = true;
                Logger.I("BattleSetup", "摄像机已强制切换为正交模式");
            }

            // 将摄像机移到地图中心
            var camPos = bounds.center;
            camPos.z = _mainCamera.transform.position.z;
            _mainCamera.transform.position = camPos;

            // 调整正交大小以完整显示地图（加上一些边距）
            float mapHeight = bounds.size.y;
            float mapWidth = bounds.size.x;
            float screenAspect = (float)Screen.width / Screen.height;
            float targetOrthoSize = Mathf.Max(mapHeight * 0.55f, mapWidth * 0.55f / screenAspect);
            _mainCamera.orthographicSize = targetOrthoSize;

            Logger.I("BattleSetup", "摄像机已适配地图(兜底): 中心=({0:F1},{1:F1}) 正交大小={2:F1}",
                camPos.x, camPos.y, _mainCamera.orthographicSize);

            CreateMapEdgeVignette();
        }

    }
}
