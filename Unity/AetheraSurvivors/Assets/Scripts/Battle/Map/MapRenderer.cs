// ============================================================
// 文件名：MapRenderer.cs
// 功能描述：地图渲染 — 混合渲染系统
//   底层：Shader Blend Quad（草地/路径/岩石/花朵 四纹理自然过渡）
//   高亮层：Tilemap（塔位高亮/无效放置提示）
// 创建时间：2026-03-25
// 所属模块：Battle/Map
// 对应交互：阶段三 #119 + 方案C Shader Blend（扩展为4纹理）
// ============================================================


using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;
// SpriteLoader 用于加载真实美术资源（有资源时自动替换占位Tile）



namespace AetheraSurvivors.Battle.Map
{
    /// <summary>
    /// 地图渲染器 — 将GridSystem数据可视化到Tilemap
    /// 
    /// 职责：
    /// 1. 从GridSystem读取网格数据，渲染到Tilemap
    /// 2. 为不同类型的格子使用不同的Tile
    /// 3. 支持塔位高亮（可放塔位的视觉提示）
    /// 4. 支持动态更新（放塔/移塔后局部刷新）
    /// </summary>
    public class MapRenderer : MonoSingleton<MapRenderer>
    {
        // ========== 配置参数 ==========

        [Header("Tilemap引用")]
        [SerializeField] private Tilemap _groundTilemap;
        [SerializeField] private Tilemap _decorationTilemap;
        [SerializeField] private Tilemap _highlightTilemap;

        [Header("Tile资源（运行时可通过代码赋值）")]
        [SerializeField] private TileBase _emptyTile;
        [SerializeField] private TileBase _pathTile;
        [SerializeField] private TileBase _towerSlotTile;
        [SerializeField] private TileBase _obstacleTile;
        [SerializeField] private TileBase _spawnPointTile;
        [SerializeField] private TileBase _basePointTile;
        [SerializeField] private TileBase _towerSlotHighlightTile;
        [SerializeField] private TileBase _invalidHighlightTile;

        // ========== Shader Blend配置 ==========

        [Header("Blend渲染配置")]
        [Tooltip("是否启用Shader Blend渲染（草地↔路径自然过渡）")]
        [SerializeField] private bool _enableBlendRendering = true;

        [Tooltip("纹理平铺系数（控制纹理在地图上的重复次数，1=铺满整个地图不重复）")]
        [SerializeField] private float _tileScale = 1.0f;


        [Tooltip("过渡边缘柔和度")]
        [SerializeField] [Range(0.01f, 0.5f)] private float _blendSoftness = 0.25f;


        [Tooltip("噪声扰动强度（让过渡边缘不规则）")]
        [SerializeField] [Range(0f, 0.3f)] private float _noiseStrength = 0.08f;

        // ========== 运行时数据 ==========

        /// <summary>是否显示塔位高亮</summary>
        private bool _showTowerSlotHighlight = false;

        /// <summary>动态生成的Tile缓存</summary>
        private Dictionary<GridCellType, TileBase> _tileCache = new Dictionary<GridCellType, TileBase>();

        /// <summary>Blend渲染用的Quad对象</summary>
        private GameObject _blendQuadObj;

        /// <summary>Blend材质</summary>
        private Material _blendMaterial;

        /// <summary>BlendMask纹理（运行时生成）</summary>
        private Texture2D _blendMaskTexture;

        /// <summary>Blend渲染是否激活（有真实纹理资源时才启用）</summary>
        private bool _blendRenderingActive = false;


        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            EnsureTilemaps();
            EnsureTiles();
            Logger.I("MapRenderer", "地图渲染器初始化");
        }

        protected override void OnDispose()
        {
            // 清理Blend渲染资源
            CleanupBlendRendering();
            Logger.I("MapRenderer", "地图渲染器已销毁");
        }


        // ========== 核心方法 ==========

        /// <summary>
        /// 渲染整个地图（从GridSystem加载数据后调用）
        /// </summary>
        public void RenderMap()
        {
            var grid = GridSystem.Instance;
            if (grid == null || !grid.IsMapLoaded)
            {
                Logger.W("MapRenderer", "RenderMap失败：地图未加载");
                return;
            }

            ClearAllTilemaps();

            // ===== 尝试启用Blend渲染（草地↔路径自然过渡）=====
            if (_enableBlendRendering)
            {
                SetupBlendRendering(grid);
            }

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = grid.GetCell(x, y);
                    var tilePos = new Vector3Int(x, y, 0);

                    // Blend渲染激活时，草地/路径/出生点/基地由Blend Quad渲染
                    // Tilemap只渲染非地面类型（障碍、塔位花草装饰等）
                    if (_blendRenderingActive && IsBlendedType(cell.Type))
                    {
                        // 跳过，由Blend Quad统一渲染地面
                        continue;
                    }

                    var tile = GetTileForType(cell.Type);
                    if (tile != null)
                    {
                        _groundTilemap.SetTile(tilePos, tile);
                    }
                }
            }

            // 统计渲染信息
            int blendedCount = 0;
            int tilemapCount = 0;
            for (int cy = 0; cy < grid.Height; cy++)
            {
                for (int cx = 0; cx < grid.Width; cx++)
                {
                    var c = grid.GetCell(cx, cy);
                    if (_blendRenderingActive && IsBlendedType(c.Type))
                        blendedCount++;
                    else
                        tilemapCount++;
                }
            }
            string blendStatus = _blendRenderingActive ? "Blend渲染✅" : "Tilemap回退";
            Logger.I("MapRenderer", "地图渲染完成: {0}×{1} ({2}), Blend格子={3}, Tilemap格子={4}",
                grid.Width, grid.Height, blendStatus, blendedCount, tilemapCount);

            // ===== 装饰物随机放置 =====
            PlaceDecorations(grid);

            // ===== 塔位可视化标记（让玩家一眼看出哪里可以放塔）=====
            PlaceTowerSlotMarkers(grid);

        }


        /// <summary>
        /// 刷新单个格子的显示
        /// </summary>
        /// <param name="coord">网格坐标</param>
        public void RefreshCell(Vector2Int coord)
        {
            var grid = GridSystem.Instance;
            if (grid == null || !grid.IsMapLoaded) return;

            var cell = grid.GetCell(coord);
            var tilePos = new Vector3Int(coord.x, coord.y, 0);
            var tile = GetTileForType(cell.Type);
            _groundTilemap.SetTile(tilePos, tile);

            // 如果在高亮模式，也刷新高亮
            if (_showTowerSlotHighlight)
            {
                RefreshHighlight(coord, cell);
            }
        }

        /// <summary>
        /// 显示所有可放塔位的高亮
        /// </summary>
        public void ShowTowerSlotHighlight()
        {
            _showTowerSlotHighlight = true;

            var grid = GridSystem.Instance;
            if (grid == null || !grid.IsMapLoaded) return;

            _highlightTilemap.ClearAllTiles();

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = grid.GetCell(x, y);
                    RefreshHighlight(new Vector2Int(x, y), cell);
                }
            }
        }

        /// <summary>
        /// 隐藏塔位高亮
        /// </summary>
        public void HideTowerSlotHighlight()
        {
            _showTowerSlotHighlight = false;
            if (_highlightTilemap != null)
            {
                _highlightTilemap.ClearAllTiles();
            }
        }

        /// <summary>
        /// 在指定位置显示无效放置高亮（红色）
        /// </summary>
        public void ShowInvalidPlacement(Vector2Int coord)
        {
            if (_highlightTilemap == null || _invalidHighlightTile == null) return;
            var tilePos = new Vector3Int(coord.x, coord.y, 0);
            _highlightTilemap.SetTile(tilePos, _invalidHighlightTile);
        }

        /// <summary>
        /// 清除指定位置的高亮
        /// </summary>
        public void ClearHighlight(Vector2Int coord)
        {
            if (_highlightTilemap == null) return;
            var tilePos = new Vector3Int(coord.x, coord.y, 0);
            _highlightTilemap.SetTile(tilePos, null);
        }

        /// <summary>
        /// 清除所有Tilemap数据
        /// </summary>
        public void ClearAllTilemaps()
        {
            if (_groundTilemap != null) _groundTilemap.ClearAllTiles();
            if (_decorationTilemap != null) _decorationTilemap.ClearAllTiles();
            if (_highlightTilemap != null) _highlightTilemap.ClearAllTiles();
            if (_decoRoot != null) { Destroy(_decoRoot); _decoRoot = null; }
            if (_towerSlotMarkersRoot != null) { Destroy(_towerSlotMarkersRoot); _towerSlotMarkersRoot = null; }
            CleanupBlendRendering();
        }

        // ========== Blend渲染方法 ==========

        /// <summary>
        /// 判断格子类型是否由Blend渲染处理
        /// 扩展为所有地形类型都走Blend（草地/路径/岩石/花朵四纹理混合）
        /// </summary>
        private bool IsBlendedType(GridCellType type)
        {
            return type == GridCellType.Empty
                || type == GridCellType.Path
                || type == GridCellType.SpawnPoint
                || type == GridCellType.BasePoint
                || type == GridCellType.Obstacle
                || type == GridCellType.TowerSlot;
        }

        /// <summary>
        /// 初始化Blend渲染系统（4纹理：草地/路径/岩石/花朵）
        /// 创建Blend Quad + 材质 + 生成BlendMask(RGBA)
        /// </summary>
        private void SetupBlendRendering(GridSystem grid)
        {
            Logger.I("MapRenderer", "========== 开始SetupBlendRendering诊断 ==========");
            Logger.I("MapRenderer", "地图尺寸: {0}×{1}, CellSize={2}, MapOrigin={3}",
                grid.Width, grid.Height, grid.CellSize, grid.MapOrigin);

            // 先清理旧的Blend资源
            CleanupBlendRendering();

            // 加载四种地形纹理
            Logger.I("MapRenderer", "--- 加载地形纹理 ---");
            Sprite grassSprite = SpriteLoader.LoadMapTile("grass");
            Sprite pathSprite = SpriteLoader.LoadMapTile("path");
            Sprite rockSprite = SpriteLoader.LoadMapTile("rock");
            Sprite flowersSprite = SpriteLoader.LoadMapTile("flowers");

            Logger.I("MapRenderer", "grassSprite={0}, pathSprite={1}, rockSprite={2}, flowersSprite={3}",
                grassSprite != null ? grassSprite.name : "NULL",
                pathSprite != null ? pathSprite.name : "NULL",
                rockSprite != null ? rockSprite.name : "NULL",
                flowersSprite != null ? flowersSprite.name : "NULL");

            if (grassSprite == null || pathSprite == null)
            {
                Logger.W("MapRenderer", "Blend渲染：缺少grass或path纹理，回退到Tilemap渲染。grass={0}, path={1}",
                    grassSprite != null, pathSprite != null);
                _blendRenderingActive = false;
                return;
            }

            // 强制设置纹理为Repeat模式（Shader平铺采样需要）
            Texture2D grassTex = grassSprite.texture;
            Texture2D pathTex = pathSprite.texture;

            grassTex.wrapMode = TextureWrapMode.Repeat;
            grassTex.filterMode = FilterMode.Trilinear;
            grassTex.anisoLevel = 4;
            pathTex.wrapMode = TextureWrapMode.Repeat;
            pathTex.filterMode = FilterMode.Trilinear;
            pathTex.anisoLevel = 4;

            // rock和flowers纹理（可选，缺失时用草地替代）
            Texture2D rockTex = rockSprite != null ? rockSprite.texture : grassTex;
            Texture2D flowersTex = flowersSprite != null ? flowersSprite.texture : grassTex;

            rockTex.wrapMode = TextureWrapMode.Repeat;
            rockTex.filterMode = FilterMode.Trilinear;
            rockTex.anisoLevel = 4;
            flowersTex.wrapMode = TextureWrapMode.Repeat;
            flowersTex.filterMode = FilterMode.Trilinear;
            flowersTex.anisoLevel = 4;

            Logger.I("MapRenderer", "草地纹理: {0}×{1}", grassTex.width, grassTex.height);
            Logger.I("MapRenderer", "路径纹理: {0}×{1}", pathTex.width, pathTex.height);
            Logger.I("MapRenderer", "岩石纹理: {0}×{1}", rockTex.width, rockTex.height);
            Logger.I("MapRenderer", "花朵纹理: {0}×{1}", flowersTex.width, flowersTex.height);

            // 加载Shader
            Shader blendShader = Resources.Load<Shader>("Shaders/MapBlendShader");
            if (blendShader == null)
            {
                blendShader = Shader.Find("AetheraSurvivors/MapBlend");
            }
            if (blendShader == null)
            {
                Logger.W("MapRenderer", "Blend渲染：找不到MapBlend Shader，回退到Tilemap渲染");
                _blendRenderingActive = false;
                return;
            }

            // 生成BlendMask纹理（RGBA四通道）
            _blendMaskTexture = MapBlendMaskGenerator.GenerateBlendMask();
            if (_blendMaskTexture == null)
            {
                Logger.W("MapRenderer", "Blend渲染：BlendMask生成失败，回退到Tilemap渲染");
                _blendRenderingActive = false;
                return;
            }

            // 创建材质并设置四张纹理
            _blendMaterial = new Material(blendShader);
            _blendMaterial.SetTexture("_GrassTex", grassTex);
            _blendMaterial.SetTexture("_PathTex", pathTex);
            _blendMaterial.SetTexture("_RockTex", rockTex);
            _blendMaterial.SetTexture("_FlowersTex", flowersTex);
            _blendMaterial.SetTexture("_BlendMask", _blendMaskTexture);

            // TileScale计算
            float effectiveTileScale = _tileScale;
            if (_tileScale <= 1.0f)
            {
                effectiveTileScale = Mathf.Max(grid.Width, grid.Height) / 4.0f;
                Logger.D("MapRenderer", "自动计算TileScale: {0} (地图{1}×{2})", 
                    effectiveTileScale, grid.Width, grid.Height);
            }

            _blendMaterial.SetFloat("_TileScale", effectiveTileScale);
            _blendMaterial.SetFloat("_BlendSoftness", _blendSoftness);
            _blendMaterial.SetFloat("_NoiseStrength", _noiseStrength);
            _blendMaterial.SetFloat("_NoiseScale", 10f);

            // 创建Blend Quad
            _blendQuadObj = CreateBlendQuad(grid);

            _blendRenderingActive = true;
            Logger.I("MapRenderer", "✅ Blend渲染(4纹理)启动成功: TileScale={0}, BlendSoftness={1}, NoiseStrength={2}",
                effectiveTileScale, _blendSoftness, _noiseStrength);
            Logger.I("MapRenderer", "BlendQuad位置={0}, 缩放={1}",
                _blendQuadObj.transform.position, _blendQuadObj.transform.localScale);
            Logger.I("MapRenderer", "========== SetupBlendRendering诊断结束 ==========");

        }

        /// <summary>
        /// 创建覆盖整个地图的Blend Quad
        /// </summary>
        private GameObject CreateBlendQuad(GridSystem grid)
        {
            var quadObj = new GameObject("BlendQuad");
            quadObj.transform.SetParent(transform);

            // 计算Quad的位置和大小（覆盖整个地图）
            float mapWidth = grid.Width * grid.CellSize;
            float mapHeight = grid.Height * grid.CellSize;
            Vector3 mapCenter = grid.MapOrigin + new Vector3(mapWidth * 0.5f, mapHeight * 0.5f, 0f);

            quadObj.transform.position = new Vector3(mapCenter.x, mapCenter.y, 0.01f); // 略微在后面
            quadObj.transform.localScale = new Vector3(mapWidth, mapHeight, 1f);

            // 添加MeshFilter + MeshRenderer
            var meshFilter = quadObj.AddComponent<MeshFilter>();
            meshFilter.mesh = CreateQuadMesh();

            var meshRenderer = quadObj.AddComponent<MeshRenderer>();
            meshRenderer.material = _blendMaterial;
            meshRenderer.sortingOrder = -1; // 在Tilemap(sortingOrder=0)下面
            // 使用和Tilemap相同的SortingLayer确保正确叠加
            meshRenderer.sortingLayerName = "Default";
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            return quadObj;
        }

        /// <summary>
        /// 创建一个标准Quad网格（中心在原点，大小1×1）
        /// </summary>
        private Mesh CreateQuadMesh()
        {
            var mesh = new Mesh();
            mesh.name = "BlendQuadMesh";

            // 顶点（1×1大小，中心在原点）
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };

            // UV
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };

            // 三角形
            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };

            // 法线（朝向摄像机）
            mesh.normals = new Vector3[]
            {
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back
            };

            mesh.RecalculateBounds();
            return mesh;
        }

        // ========== 塔位标记系统 ==========

        /// <summary>塔位标记根节点</summary>
        private GameObject _towerSlotMarkersRoot;

        /// <summary>
        /// 在每个空闲塔位上放置可视化标记（半透明绿色方框+角标）
        /// 让玩家一眼看出哪里可以放塔
        /// </summary>
        private void PlaceTowerSlotMarkers(GridSystem grid)
        {
            // 清理旧标记
            if (_towerSlotMarkersRoot != null) Destroy(_towerSlotMarkersRoot);
            _towerSlotMarkersRoot = new GameObject("TowerSlotMarkers");
            _towerSlotMarkersRoot.transform.SetParent(transform);

            // 生成塔位标记纹理（64x64，半透明方框+内部微弱填充）
            int texSize = 64;
            int border = 3;
            var markerTex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            Color borderColor = new Color(0.3f, 0.9f, 0.3f, 0.6f); // 绿色边框
            Color fillColor = new Color(0.3f, 0.8f, 0.3f, 0.12f);  // 极淡绿色填充
            Color cornerColor = new Color(1f, 0.9f, 0.3f, 0.7f);   // 金色角标

            for (int py = 0; py < texSize; py++)
            {
                for (int px = 0; px < texSize; px++)
                {
                    bool isBorder = px < border || px >= texSize - border ||
                                    py < border || py >= texSize - border;
                    // 四个角加强（8x8金色方块）
                    bool isCorner = (px < 8 || px >= texSize - 8) && (py < 8 || py >= texSize - 8);

                    if (isCorner)
                        markerTex.SetPixel(px, py, cornerColor);
                    else if (isBorder)
                        markerTex.SetPixel(px, py, borderColor);
                    else
                        markerTex.SetPixel(px, py, fillColor);
                }
            }
            markerTex.Apply();
            markerTex.filterMode = FilterMode.Bilinear;

            Sprite markerSprite = Sprite.Create(markerTex,
                new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), texSize);

            int markerCount = 0;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = grid.GetCell(x, y);
                    if (cell.Type != GridCellType.TowerSlot) continue;

                    Vector3 worldPos = grid.GridToWorld(new Vector2Int(x, y));
                    worldPos.z = -0.05f; // 在地面之上，装饰物之下

                    var markerObj = new GameObject($"SlotMarker_{x}_{y}");
                    markerObj.transform.SetParent(_towerSlotMarkersRoot.transform);
                    markerObj.transform.position = worldPos;
                    markerObj.transform.localScale = new Vector3(
                        grid.CellSize * 0.92f, grid.CellSize * 0.92f, 1f);

                    var sr = markerObj.AddComponent<SpriteRenderer>();
                    sr.sprite = markerSprite;
                    sr.sortingOrder = 2; // 在ground(0)和deco(1)之上，highlight(5)之下
                    sr.color = Color.white;

                    markerCount++;
                }
            }

            Logger.I("MapRenderer", "塔位标记放置完成: {0}个", markerCount);
        }

        // ========== 装饰物系统 ==========

        /// <summary>装饰物定义：名称+权重+缩放范围</summary>
        private struct DecoInfo
        {
            public string Name;
            public float Weight;        // 权重（越大越常见）
            public float ScaleMin;      // 最小缩放
            public float ScaleMax;      // 最大缩放

            public DecoInfo(string name, float weight, float scaleMin, float scaleMax)
            {
                Name = name; Weight = weight; ScaleMin = scaleMin; ScaleMax = scaleMax;
            }
        }

        private static readonly DecoInfo[] _decoInfos = {
            new DecoInfo("rock_small",    4f,  0.30f, 0.45f),  // 小石头：最常见
            new DecoInfo("flowers_wild",  3f,  0.32f, 0.48f),  // 野花：常见
            new DecoInfo("bush",          3f,  0.38f, 0.55f),  // 灌木：常见，稍大
            new DecoInfo("rock_large",    2f,  0.40f, 0.55f),  // 大石头：较少
            new DecoInfo("tree_green",    1.5f, 0.50f, 0.70f), // 绿树：稀有，较大
            new DecoInfo("tree_autumn",   0.8f, 0.50f, 0.70f), // 秋树：稀有
        };

        /// <summary>装饰物GameObject根节点</summary>
        private GameObject _decoRoot;

        /// <summary>
        /// 在空地格子上随机放置装饰物（用SpriteRenderer，支持缩放/偏移/旋转）
        /// </summary>
        private void PlaceDecorations(GridSystem grid)
        {
            // 清理旧装饰物
            if (_decoRoot != null) Destroy(_decoRoot);
            _decoRoot = new GameObject("DecorationSprites");
            _decoRoot.transform.SetParent(transform);

            // 加载所有可用装饰物
            List<DecoInfo> available = new List<DecoInfo>();
            List<Sprite> sprites = new List<Sprite>();
            float totalWeight = 0f;

            foreach (var info in _decoInfos)
            {
                Sprite spr = SpriteLoader.LoadMapDecoration(info.Name);
                if (spr != null)
                {
                    available.Add(info);
                    sprites.Add(spr);
                    totalWeight += info.Weight;
                }
            }

            if (available.Count == 0)
            {
                Logger.D("MapRenderer", "无装饰物资源可用，跳过");
                return;
            }

            int seed = grid.Width * 1000 + grid.Height;
            var rng = new System.Random(seed);
            int placedCount = 0;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = grid.GetCell(x, y);
                    if (cell.Type != GridCellType.Empty) continue;

                    // 边缘密度高（40%），中心密度低（8%）
                    // 距离边缘越近概率越高，形成自然过渡带
                    int edgeDist = Mathf.Min(x, y, grid.Width - 1 - x, grid.Height - 1 - y);
                    float decoChance;
                    if (edgeDist == 0)
                        decoChance = 0.50f;  // 最外围50%
                    else if (edgeDist == 1)
                        decoChance = 0.30f;  // 次外围30%
                    else if (edgeDist == 2)
                        decoChance = 0.15f;  // 第三层15%
                    else
                        decoChance = 0.06f;  // 内部6%

                    if (rng.NextDouble() > decoChance) continue;

                    // 加权随机选择装饰物类型
                    // 边缘优先选大型装饰物（树/灌木），中心优先小型（石头/花）
                    float roll = (float)(rng.NextDouble() * totalWeight);
                    int chosenIdx = 0;
                    float cumulative = 0f;
                    for (int i = 0; i < available.Count; i++)
                    {
                        float w = available[i].Weight;
                        // 边缘2格内：大型装饰物权重×3（树/灌木遮挡硬边）
                        if (edgeDist <= 1 && (available[i].Name.StartsWith("tree") || available[i].Name == "bush"))
                            w *= 3f;
                        cumulative += w;
                        if (roll <= cumulative) { chosenIdx = i; break; }
                    }

                    DecoInfo chosen = available[chosenIdx];
                    Sprite spr = sprites[chosenIdx];

                    // 边缘装饰物更大（额外缩放1.3倍）
                    float edgeBonus = edgeDist <= 1 ? 1.3f : 1.0f;

                    // 随机缩放（边缘有额外加成）
                    float scale = (chosen.ScaleMin + (float)rng.NextDouble() * (chosen.ScaleMax - chosen.ScaleMin)) * edgeBonus;

                    // 随机偏移（在格子内±0.3范围偏移，避免死对齐格子中心）
                    float offsetX = (float)(rng.NextDouble() - 0.5) * 0.6f;
                    float offsetY = (float)(rng.NextDouble() - 0.5) * 0.6f;

                    // 随机水平翻转（50%概率）
                    bool flipX = rng.NextDouble() > 0.5;

                    // 随机轻微旋转（±8度，石头/灌木可以转，树不转）
                    float rotation = 0f;
                    if (chosen.Name.StartsWith("rock") || chosen.Name == "bush" || chosen.Name == "flowers_wild")
                        rotation = (float)(rng.NextDouble() - 0.5) * 16f;

                    // 创建 SpriteRenderer GameObject
                    Vector3 worldPos = grid.GridToWorld(new Vector2Int(x, y));
                    // GridToWorld已返回格子中心，只加随机偏移
                    worldPos.x += offsetX * grid.CellSize;
                    worldPos.y += offsetY * grid.CellSize;
                    worldPos.z = -0.01f; // 略微在地面前方

                    var decoObj = new GameObject($"Deco_{chosen.Name}_{placedCount}");
                    decoObj.transform.SetParent(_decoRoot.transform);
                    decoObj.transform.position = worldPos;
                    decoObj.transform.localScale = new Vector3(
                        flipX ? -scale : scale, scale, 1f);
                    decoObj.transform.rotation = Quaternion.Euler(0, 0, rotation);

                    var sr = decoObj.AddComponent<SpriteRenderer>();
                    sr.sprite = spr;
                    sr.sortingOrder = 1; // 在地面之上
                    // 边缘装饰物不透明（做遮挡），中心装饰物半透明（融入地面）
                    float alpha = edgeDist <= 1 ? 0.95f : (0.65f + (float)rng.NextDouble() * 0.2f);
                    sr.color = new Color(1f, 1f, 1f, alpha);

                    placedCount++;
                }
            }

            Logger.I("MapRenderer", "装饰物放置完成: {0}个（{1}种可用，边缘密度梯度模式）",
                placedCount, available.Count);
        }

        /// <summary>
        /// 清理Blend渲染资源
        /// </summary>
        private void CleanupBlendRendering()
        {
            if (_blendQuadObj != null)
            {
                Destroy(_blendQuadObj);
                _blendQuadObj = null;
            }

            if (_blendMaterial != null)
            {
                Destroy(_blendMaterial);
                _blendMaterial = null;
            }

            if (_blendMaskTexture != null)
            {
                Destroy(_blendMaskTexture);
                _blendMaskTexture = null;
            }

            _blendRenderingActive = false;
        }


        // ========== 内部方法 ==========

        /// <summary>确保Tilemap组件存在</summary>
        private void EnsureTilemaps()
        {
            // 创建Grid根节点（所有Tilemap的父级）
            Transform gridRoot = transform;
            if (GetComponent<Grid>() == null)
            {
                // MapRenderer自身添加Grid组件，让所有子Tilemap共享Grid
                gameObject.AddComponent<Grid>();
            }

            if (_groundTilemap == null)
            {
                var groundObj = new GameObject("GroundTilemap");
                groundObj.transform.SetParent(transform);
                _groundTilemap = groundObj.AddComponent<Tilemap>();
                groundObj.AddComponent<TilemapRenderer>().sortingOrder = 0;
            }

            if (_decorationTilemap == null)
            {
                var decoObj = new GameObject("DecorationTilemap");
                decoObj.transform.SetParent(transform);
                _decorationTilemap = decoObj.AddComponent<Tilemap>();
                decoObj.AddComponent<TilemapRenderer>().sortingOrder = 1;
            }

            if (_highlightTilemap == null)
            {
                var hlObj = new GameObject("HighlightTilemap");
                hlObj.transform.SetParent(transform);
                _highlightTilemap = hlObj.AddComponent<Tilemap>();
                var hlRenderer = hlObj.AddComponent<TilemapRenderer>();
                hlRenderer.sortingOrder = 5;
            }
        }


        /// <summary>确保基础Tile存在（优先加载真实美术资源，无资源时回退纯色Tile）</summary>
        private void EnsureTiles()
        {
            Logger.I("MapRenderer", "========== EnsureTiles诊断 ==========");
            // 优先尝试加载真实美术Tile
            if (_emptyTile == null) _emptyTile = CreateSpriteOrColorTile("grass", new Color(0.2f, 0.5f, 0.2f, 1f));
            if (_pathTile == null) _pathTile = CreateSpriteOrColorTile("path", new Color(0.85f, 0.75f, 0.5f, 1f));
            if (_towerSlotTile == null) _towerSlotTile = CreateSpriteOrColorTile("flowers", new Color(0.4f, 0.7f, 0.4f, 1f));
            if (_obstacleTile == null) _obstacleTile = CreateSpriteOrColorTile("rock", new Color(0.4f, 0.25f, 0.15f, 1f));
            if (_spawnPointTile == null) _spawnPointTile = CreateSpriteOrColorTile("path", new Color(1f, 0.3f, 0.3f, 1f), new Color(1f, 0.6f, 0.6f, 1f));

            if (_basePointTile == null) _basePointTile = CreateSpriteOrColorTile("castle_wall", new Color(0.3f, 0.5f, 1f, 1f));
            if (_towerSlotHighlightTile == null) _towerSlotHighlightTile = CreateColorTile(new Color(0f, 1f, 0f, 0.3f));
            if (_invalidHighlightTile == null) _invalidHighlightTile = CreateColorTile(new Color(1f, 0f, 0f, 0.3f));
            Logger.I("MapRenderer", "Tile加载结果: empty={0}, path={1}, towerSlot={2}, obstacle={3}, spawn={4}, base={5}",
                _emptyTile != null, _pathTile != null, _towerSlotTile != null,
                _obstacleTile != null, _spawnPointTile != null, _basePointTile != null);
            Logger.I("MapRenderer", "========== EnsureTiles诊断结束 ==========");
        }


        /// <summary>尝试从SpriteLoader加载真实美术Tile，加载失败则回退纯色</summary>
        /// <param name="tileName">Tile资源名</param>
        /// <param name="fallbackColor">无资源时的纯色回退</param>
        /// <param name="tintColor">可选的色调叠加（用于出生点等需要视觉区分的位置）</param>
        private Tile CreateSpriteOrColorTile(string tileName, Color fallbackColor, Color? tintColor = null)
        {
            Sprite realSprite = SpriteLoader.LoadMapTile(tileName);
            if (realSprite != null)
            {
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = realSprite;
                // 如果指定了色调叠加则应用，否则保持原色
                tile.color = tintColor ?? Color.white;
                Logger.D("MapRenderer", "加载真实Tile: {0}", tileName);
                return tile;
            }
            // 无真实资源，回退纯色
            return CreateColorTile(fallbackColor);
        }



        /// <summary>根据格子类型获取Tile</summary>
        private TileBase GetTileForType(GridCellType type)
        {
            switch (type)
            {
                case GridCellType.Empty: return _emptyTile;
                case GridCellType.Path: return _pathTile;
                case GridCellType.TowerSlot: return _towerSlotTile;
                case GridCellType.Obstacle: return _obstacleTile;
                case GridCellType.SpawnPoint: return _spawnPointTile;
                case GridCellType.BasePoint: return _basePointTile;
                default: return _emptyTile;
            }
        }

        /// <summary>刷新单个格子的高亮</summary>
        private void RefreshHighlight(Vector2Int coord, GridCell cell)
        {
            if (_highlightTilemap == null) return;

            var tilePos = new Vector3Int(coord.x, coord.y, 0);

            if (cell.CanPlaceTower && _towerSlotHighlightTile != null)
            {
                _highlightTilemap.SetTile(tilePos, _towerSlotHighlightTile);
            }
            else
            {
                _highlightTilemap.SetTile(tilePos, null);
            }
        }

        /// <summary>动态创建纯色Tile（用于开发阶段无美术资源时）</summary>
        private Tile CreateColorTile(Color color)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();

            // 创建1x1纯色纹理
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            tile.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            tile.color = Color.white;

            return tile;
        }
    }
}
