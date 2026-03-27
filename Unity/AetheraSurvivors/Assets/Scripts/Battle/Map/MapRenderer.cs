// ============================================================
// 文件名：MapRenderer.cs
// 功能描述：地图渲染 — 混合渲染系统
//   底层：Shader Blend Quad（草地↔路径自然过渡）
//   上层：Tilemap（障碍物/塔位/出生点/基地等非地面元素）
//   高亮层：Tilemap（塔位高亮/无效放置提示）
// 创建时间：2026-03-25
// 所属模块：Battle/Map
// 对应交互：阶段三 #119 + 方案C Shader Blend
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
            CleanupBlendRendering();
        }

        // ========== Blend渲染方法 ==========

        /// <summary>
        /// 判断格子类型是否由Blend渲染处理（地面类型）
        /// </summary>
        private bool IsBlendedType(GridCellType type)
        {
            return type == GridCellType.Empty
                || type == GridCellType.Path
                || type == GridCellType.SpawnPoint
                || type == GridCellType.BasePoint
                || type == GridCellType.TowerSlot;
        }

        /// <summary>
        /// 初始化Blend渲染系统
        /// 创建Blend Quad + 材质 + 生成BlendMask
        /// </summary>
        private void SetupBlendRendering(GridSystem grid)
        {
            Logger.I("MapRenderer", "========== 开始SetupBlendRendering诊断 ==========");
            Logger.I("MapRenderer", "地图尺寸: {0}×{1}, CellSize={2}, MapOrigin={3}",
                grid.Width, grid.Height, grid.CellSize, grid.MapOrigin);

            // 先清理旧的Blend资源
            CleanupBlendRendering();

            // 加载草地和路径纹理
            Logger.I("MapRenderer", "--- 加载草地纹理 ---");
            Sprite grassSprite = SpriteLoader.LoadMapTile("grass");
            Logger.I("MapRenderer", "--- 加载路径纹理 ---");
            Sprite pathSprite = SpriteLoader.LoadMapTile("path");

            Logger.I("MapRenderer", "grassSprite={0}, pathSprite={1}",
                grassSprite != null ? grassSprite.name : "NULL",
                pathSprite != null ? pathSprite.name : "NULL");

            if (grassSprite == null || pathSprite == null)
            {
                Logger.W("MapRenderer", "Blend渲染：缺少grass或path纹理，回退到Tilemap渲染。grass={0}, path={1}",
                    grassSprite != null, pathSprite != null);
                _blendRenderingActive = false;
                return;
            }


            // ===== 关键：强制设置纹理为Repeat模式 =====
            // Sprite类型纹理在Unity中默认被强制为Clamp，但Shader平铺采样需要Repeat
            // 必须在运行时手动覆盖，否则会出现明显的网格接缝
            Texture2D grassTex = grassSprite.texture;
            Texture2D pathTex = pathSprite.texture;

            grassTex.wrapMode = TextureWrapMode.Repeat;
            grassTex.filterMode = FilterMode.Trilinear;
            grassTex.anisoLevel = 4;
            pathTex.wrapMode = TextureWrapMode.Repeat;
            pathTex.filterMode = FilterMode.Trilinear;
            pathTex.anisoLevel = 4;


            Logger.I("MapRenderer", "草地纹理详情: {0}×{1}, format={2}, isReadable={3}, wrapMode={4}, filterMode={5}",
                grassTex.width, grassTex.height, grassTex.format, grassTex.isReadable, grassTex.wrapMode, grassTex.filterMode);
            Logger.I("MapRenderer", "路径纹理详情: {0}×{1}, format={2}, isReadable={3}, wrapMode={4}, filterMode={5}",
                pathTex.width, pathTex.height, pathTex.format, pathTex.isReadable, pathTex.wrapMode, pathTex.filterMode);


            // 加载Shader（先尝试Resources.Load确保WebGL不被strip，再回退Shader.Find）

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


            // 生成BlendMask纹理
            _blendMaskTexture = MapBlendMaskGenerator.GenerateBlendMask();
            if (_blendMaskTexture == null)
            {
                Logger.W("MapRenderer", "Blend渲染：BlendMask生成失败，回退到Tilemap渲染");
                _blendRenderingActive = false;
                return;
            }

            // 创建材质
            _blendMaterial = new Material(blendShader);
            _blendMaterial.SetTexture("_GrassTex", grassTex);
            _blendMaterial.SetTexture("_PathTex", pathTex);

            _blendMaterial.SetTexture("_BlendMask", _blendMaskTexture);
            // _tileScale控制纹理在地图上的重复次数
            // 值越大纹理重复越多（细节越密），值越小纹理越拉伸
            // 默认使用配置值，如果<=1则自动根据地图尺寸计算合理值
            // 使用Stochastic Tiling后，每3-4格重复一次纹理效果最佳
            float effectiveTileScale = _tileScale;
            if (_tileScale <= 1.0f)
            {
                // 自动计算：让纹理每4格重复一次（Stochastic Tiling下效果最佳）
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
            Logger.I("MapRenderer", "✅ Blend渲染启动成功: Shader={0}, TileScale={1}, BlendSoftness={2}, NoiseStrength={3}",
                blendShader.name, effectiveTileScale, _blendSoftness, _noiseStrength);
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
