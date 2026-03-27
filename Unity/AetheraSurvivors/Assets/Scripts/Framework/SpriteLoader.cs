// ============================================================
// 文件名：SpriteLoader.cs
// 功能描述：运行时Sprite资源加载器
//   从 Resources/Sprites/ 加载真实美术Sprite
//   当有真实PNG资源时自动使用，否则由调用方回退到占位图
//   支持预加载、缓存、按类型查询
// 创建时间：2026-03-25
// 所属模块：Framework
// 对应交互：#160.9
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace AetheraSurvivors.Framework
{
    /// <summary>
    /// Sprite资源加载器 — 运行时从Resources加载真实Sprite
    /// 当 Resources/Sprites/ 下有对应PNG时自动使用真实图片，
    /// 否则返回null（由调用方决定回退策略）。
    /// 
    /// 使用方式：
    ///   var sprite = SpriteLoader.LoadTower(TowerType.Archer, level);
    ///   var sprite = SpriteLoader.LoadEnemy(EnemyType.Infantry);
    ///   var sprite = SpriteLoader.LoadProjectile("arrow");
    ///   var sprite = SpriteLoader.LoadUI("icon_coin");
    /// </summary>
    public static class SpriteLoader
    {
        // ====================================================================
        // 缓存
        // ====================================================================

        /// <summary>已加载的Sprite缓存（避免重复Resources.Load）</summary>
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        /// <summary>已加载的SpriteSheet帧缓存（key=资源路径，value=帧数组）</summary>
        private static readonly Dictionary<string, Sprite[]> _sheetCache = new Dictionary<string, Sprite[]>();

        /// <summary>已确认不存在的路径集合（避免重复尝试加载不存在的资源）</summary>
        private static readonly HashSet<string> _missingPaths = new HashSet<string>();

        /// <summary>是否有真实美术资源可用</summary>
        public static bool HasRealSprites { get; private set; }


        // ====================================================================
        // 塔资源加载
        // ====================================================================

        /// <summary>
        /// 加载塔的Sprite
        /// </summary>
        /// <param name="type">塔类型（int值对应TowerType枚举）</param>
        /// <param name="level">塔等级（1-3）</param>
        /// <returns>Sprite（真实图）或 null（无资源）</returns>
        public static Sprite LoadTower(int type, int level)
        {
            string name = GetTowerName(type);
            string path = $"Sprites/Towers/tower_{name}_lv{level}";
            return LoadOrNull(path);
        }

        /// <summary>根据塔类型int值获取资源名</summary>
        private static string GetTowerName(int type)
        {
            switch (type)
            {
                case 0: return "archer";
                case 1: return "mage";
                case 2: return "ice";
                case 3: return "cannon";
                case 4: return "poison";
                case 5: return "goldmine";
                default: return "archer";
            }
        }

        // ====================================================================
        // 怪物资源加载
        // ====================================================================

        /// <summary>
        /// 加载怪物的Sprite
        /// </summary>
        /// <param name="type">怪物类型（int值对应EnemyType枚举）</param>
        /// <returns>Sprite（真实图）或 null（无资源）</returns>
        public static Sprite LoadEnemy(int type)
        {
            string name = GetEnemyName(type);
            string path = $"Sprites/Enemies/enemy_{name}";
            return LoadOrNull(path);
        }

        /// <summary>根据怪物类型int值获取资源名</summary>
        private static string GetEnemyName(int type)
        {
            switch (type)
            {
                case 0: return "infantry";
                case 1: return "assassin";
                case 2: return "knight";
                case 3: return "flyer";
                case 10: return "healer";
                case 11: return "slime";
                case 12: return "rogue";
                case 13: return "shieldmage";
                case 50: return "boss_dragon";
                case 51: return "boss_giant";
                default: return "infantry";
            }
        }

        /// <summary>
        /// 加载怪物的行走SpriteSheet（帧动画）
        /// 资源命名规则：enemy_{name}_walk.png（一行排列的多帧图）
        /// </summary>
        /// <param name="type">怪物类型（int值对应EnemyType枚举）</param>
        /// <param name="frameCount">SpriteSheet中的帧数（默认自动检测：宽/高）</param>
        /// <returns>Sprite帧数组，无资源时返回null</returns>
        public static Sprite[] LoadEnemyWalkSheet(int type, int frameCount = 0)
        {
            string name = GetEnemyName(type);
            string path = $"Sprites/Enemies/enemy_{name}_walk";
            return LoadSpriteSheet(path, frameCount);
        }

        // ====================================================================
        // SpriteSheet 加载与切割
        // ====================================================================

        /// <summary>
        /// 加载SpriteSheet并切割成帧数组
        /// 支持一行排列（水平）的SpriteSheet，自动按帧数切割
        /// </summary>
        /// <param name="resourcePath">Resources下的相对路径（不含.png后缀）</param>
        /// <param name="frameCount">帧数（0=自动检测：假设每帧为正方形，帧数=宽/高）</param>
        /// <returns>Sprite帧数组，无资源时返回null</returns>
        public static Sprite[] LoadSpriteSheet(string resourcePath, int frameCount = 0)
        {
            // 检查缓存
            if (_sheetCache.TryGetValue(resourcePath, out Sprite[] cached))
            {
                return cached;
            }

            // 检查已知不存在
            if (_missingPaths.Contains(resourcePath))
            {
                return null;
            }

            // 先尝试加载Texture2D（SpriteSheet需要从纹理切割）
            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null)
            {
                _missingPaths.Add(resourcePath);
                return null;
            }

            // 确保纹理过滤模式正确
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;

            // 自动检测帧数：假设一行排列，每帧为正方形（宽/高）
            int texW = tex.width;
            int texH = tex.height;
            if (frameCount <= 0)
            {
                frameCount = Mathf.Max(1, texW / texH);
            }

            int frameW = texW / frameCount;
            int frameH = texH;
            float ppu = frameH; // PPU = 帧高度，使每帧sprite为1x1世界单位

            Sprite[] frames = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                // Unity纹理坐标：左下角为(0,0)
                Rect rect = new Rect(i * frameW, 0, frameW, frameH);
                Vector2 pivot = new Vector2(0.5f, 0.5f);
                frames[i] = Sprite.Create(tex, rect, pivot, ppu);
                frames[i].name = $"{tex.name}_frame{i}";
            }

            _sheetCache[resourcePath] = frames;
            HasRealSprites = true;

            Debug.Log($"[SpriteLoader] SpriteSheet加载成功: {resourcePath}, " +
                $"纹理={texW}x{texH}, 帧数={frameCount}, 每帧={frameW}x{frameH}");

            return frames;
        }


        // ====================================================================
        // 投射物资源加载
        // ====================================================================

        /// <summary>
        /// 加载投射物Sprite
        /// </summary>
        /// <param name="name">投射物名（arrow/magic_bolt/ice_shard/cannonball）</param>
        public static Sprite LoadProjectile(string name)
        {
            string path = $"Sprites/Effects/projectile_{name}";
            return LoadOrNull(path);
        }

        // ====================================================================
        // 地图Tile资源加载
        // ====================================================================

        /// <summary>
        /// 加载地图Tile Sprite
        /// </summary>
        /// <param name="name">Tile名（grass/path/rock/flowers/water/castle_wall）</param>
        public static Sprite LoadMapTile(string name)
        {
            string path = $"Sprites/Maps/tile_{name}";
            return LoadOrNull(path);
        }

        // ====================================================================
        // UI图标资源加载
        // ====================================================================

        /// <summary>
        /// 加载UI图标Sprite
        /// </summary>
        /// <param name="name">图标名（icon_coin/icon_heart/btn_tower_archer等）</param>
        public static Sprite LoadUI(string name)
        {
            string path = $"Sprites/UI/ui_{name}";
            return LoadOrNull(path);
        }

        /// <summary>
        /// 加载词条图标Sprite
        /// </summary>
        /// <param name="name">词条名（atk_up/aspd_up/range_up等）</param>
        public static Sprite LoadRoguelikeIcon(string name)
        {
            string path = $"Sprites/UI/roguelike_{name}";
            return LoadOrNull(path);
        }

        // ====================================================================
        // 核心加载逻辑
        // ====================================================================

        /// <summary>
        /// 从Resources加载Sprite，不存在则返回null
        /// 当纹理的textureType不是Sprite时，自动回退到Texture2D并运行时创建Sprite
        /// </summary>
        /// <param name="resourcePath">Resources下的相对路径（不含.png后缀）</param>
        /// <returns>Sprite或null</returns>
        public static Sprite LoadOrNull(string resourcePath)
        {
            // 检查缓存
            if (_cache.TryGetValue(resourcePath, out Sprite cached))
            {
                return cached;
            }

            // 检查已知不存在的路径
            if (_missingPaths.Contains(resourcePath))
            {
                return null;
            }

            // 尝试从Resources加载Sprite（textureType=Sprite时可用）
            Sprite sprite = Resources.Load<Sprite>(resourcePath);

            if (sprite != null)
            {
                _cache[resourcePath] = sprite;
                HasRealSprites = true;
                Texture2D tex = sprite.texture;

                // 确保纹理过滤模式正确（避免缩小显示时像素化/锯齿）
                if (tex.filterMode != FilterMode.Trilinear)
                {
                    tex.filterMode = FilterMode.Trilinear;
                }
                tex.anisoLevel = Mathf.Max(tex.anisoLevel, 8);


                Debug.Log($"[SpriteLoader] ✅ Sprite加载成功: {resourcePath} → {sprite.name}, " +
                    $"纹理={tex.width}×{tex.height}, format={tex.format}, " +
                    $"isReadable={tex.isReadable}, wrapMode={tex.wrapMode}, filterMode={tex.filterMode}");
                return sprite;
            }


            // Sprite加载失败，尝试作为Texture2D加载并运行时创建Sprite
            // 这是对textureType不是Sprite(8)的兼容处理
            Texture2D rawTex = Resources.Load<Texture2D>(resourcePath);
            if (rawTex != null)
            {
            // 从Texture2D运行时创建Sprite
                // 注意：PPU需要与纹理尺寸匹配，使sprite在世界空间中大小合理
                // 怪物图片通常是600x600，PPU=600则sprite为1x1世界单位
                float ppu = Mathf.Max(rawTex.width, rawTex.height); // PPU = 图片最大边长，确保sprite为1x1世界单位

                // 确保纹理过滤模式为Trilinear（避免缩小时像素化/锯齿）
                rawTex.filterMode = FilterMode.Trilinear;
                rawTex.anisoLevel = 8; // 提高各向异性过滤质量，消除锯齿


                Sprite createdSprite = Sprite.Create(
                    rawTex,
                    new Rect(0, 0, rawTex.width, rawTex.height),
                    new Vector2(0.5f, 0.5f),
                    ppu
                );


                createdSprite.name = rawTex.name + "_runtime";

                _cache[resourcePath] = createdSprite;
                HasRealSprites = true;
                Debug.Log($"[SpriteLoader] ✅ Texture2D→Sprite回退成功: {resourcePath} → {createdSprite.name}, " +
                    $"纹理={rawTex.width}×{rawTex.height}, format={rawTex.format}, " +
                    $"isReadable={rawTex.isReadable}, wrapMode={rawTex.wrapMode}");
                return createdSprite;
            }

            // 两种方式都加载失败，文件确实不存在
            Debug.LogWarning($"[SpriteLoader] ❌ {resourcePath}: Load<Sprite>和Load<Texture2D>均返回null，文件不存在");

            // 记录为不存在（避免后续重复尝试加载）
            _missingPaths.Add(resourcePath);
            return null;
        }



        /// <summary>
        /// 检查指定路径是否有真实Sprite资源
        /// </summary>
        public static bool HasSprite(string resourcePath)
        {
            return LoadOrNull(resourcePath) != null;
        }

        /// <summary>
        /// 清除所有缓存（场景切换时调用）
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
            _sheetCache.Clear();
            _missingPaths.Clear();
            HasRealSprites = false;
        }


        /// <summary>
        /// 预加载所有塔Sprite（避免战斗中首次加载卡顿）
        /// </summary>
        public static void PreloadTowerSprites()
        {
            for (int type = 0; type <= 5; type++)
            {
                for (int level = 1; level <= 3; level++)
                {
                    LoadTower(type, level);
                }
            }
        }

        /// <summary>
        /// 预加载所有怪物Sprite
        /// </summary>
        public static void PreloadEnemySprites()
        {
            int[] types = { 0, 1, 2, 3, 10, 11, 12, 13, 50, 51 };
            foreach (int type in types)
            {
                LoadEnemy(type);
            }
        }

        /// <summary>
        /// 预加载第一关所有资源（启动时调用一次）
        /// </summary>
        public static void PreloadAll()
        {
            PreloadTowerSprites();
            PreloadEnemySprites();

            // 投射物
            string[] projectiles = { "arrow", "magic_bolt", "ice_shard", "cannonball" };
            foreach (string p in projectiles) LoadProjectile(p);

            // 地图Tile
            string[] tiles = { "grass", "path", "rock", "flowers", "water", "castle_wall" };
            foreach (string t in tiles) LoadMapTile(t);

            // UI图标
            string[] uiIcons = { "icon_coin", "icon_heart", "icon_wave_flag",
                "btn_tower_archer", "btn_tower_mage", "btn_tower_ice",
                "btn_tower_cannon", "btn_tower_poison", "btn_tower_goldmine",
                "icon_upgrade", "icon_sell", "icon_speedup" };
            foreach (string u in uiIcons) LoadUI(u);

            // 词条图标
            string[] roguelikes = { "atk_up", "aspd_up", "range_up", "crit_up",
                "freeze", "gold_bonus", "poison_up", "aoe_up" };
            foreach (string r in roguelikes) LoadRoguelikeIcon(r);

            Debug.Log($"[SpriteLoader] 预加载完成，缓存 {_cache.Count} 张Sprite，HasRealSprites={HasRealSprites}");
        }
    }
}
