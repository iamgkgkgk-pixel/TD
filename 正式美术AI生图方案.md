# 正式美术 AI 生图方案 — AetheraSurvivors

> **对应交互**：#382
> **生成日期**：2026-03-29
> **依赖文档**：`正式美术资源清单.md`（#381）
> **目标**：为 116 张正式美术资源制定 AI 生图工具选型、Prompt 模板、后处理流程

---

## 一、工具选型总表

| 资源类别 | 推荐工具 | 原因 | 备选 |
|---------|---------|------|------|
| 塔 (18张) | **Midjourney V6.1** | 2D 游戏资产风格一致性最强，`--style raw` 控制力好 | SDXL + ControlNet (以lv1为参考图) |
| 怪物静态 (10张) | **Midjourney V6.1** | 角色设计质量高，俯视角控制好 | Flux Pro |
| 怪物行走帧 (10张) | **SDXL + AnimateDiff** | MJ 无法生成 SpriteSheet，需要专门的序列帧工具 | Aseprite 手动 / Rive |
| 投射物 (6张) | **SDXL** | 小尺寸特效图，SDXL 出图快且可控 | Midjourney |
| 地图 Tiles (14张) | **Midjourney V6.1** + **SDXL Tile模式** | MJ 生成底图，SDXL 的 Tile 模式保证无缝平铺 | Stable Diffusion `--tiling` |
| UI 图标 (20张) | **Midjourney V6.1** | 图标设计 MJ 质量最稳 | DALL-E 3 / IconKitchen |
| 英雄立绘 (18张) | **Midjourney V6.1** | 角色立绘是 MJ 最强项 | Flux Pro / NovelAI |
| 特效纹理 (12张) | **SDXL** | 抽象纹理/粒子效果 SDXL 更灵活 | Midjourney |
| 背景 (8张) | **Midjourney V6.1** | 场景/氛围图 MJ 质量最高 | Flux Pro |

**总结**：~70% 用 Midjourney，~30% 用 SDXL/AnimateDiff。MJ 做设计定稿，SDXL 做需要精确控制（平铺/序列帧/小尺寸）的资源。

---

## 二、统一美术风格锚定

所有 Prompt 共用以下**风格锚定后缀**：

```
2D top-down tower defense game asset, hand-painted cartoon style,
vibrant colors, clean outlines, slight cel-shading,
transparent PNG background, game-ready sprite,
inspired by Kingdom Rush and Arknights chibi art style,
--ar 1:1 --style raw --v 6.1
```

**关键词说明**：
- `top-down`：俯视角（塔/怪物/地图统一视角）
- `hand-painted cartoon`：手绘卡通风（与现有 lv1 资源风格匹配）
- `clean outlines`：清晰描边（在小屏幕上辨识度高）
- `cel-shading`：赛璐珞阴影（统一光影风格）
- `Kingdom Rush + Arknights chibi`：风格参考锚点

---

## 三、分类 Prompt 模板

### 3A. 塔 (Towers) — Midjourney

#### Lv1 品质提升（以现有图为参考）

```
/imagine prompt: [Tower Name] defense tower, level 1,
[specific description],
2D top-down tower defense sprite, hand-painted cartoon,
vibrant colors, clean outlines, cel-shading,
simple wooden/stone base, small and compact design,
transparent background, game-ready asset,
--ar 1:1 --style raw --v 6.1 --s 200
```

**6种塔的 `[specific description]`**：

| 塔 | Lv1 描述 |
|----|---------|
| Archer | wooden archer tower with a small bow on top, green flag, simple wooden structure |
| Mage | magical crystal orb tower, glowing purple crystal on stone pillar, small arcane runes |
| Ice | frozen ice spire tower, pale blue ice crystal formation, frost mist around base |
| Cannon | iron cannon turret on wooden platform, single barrel, rusty metal with rivets |
| Poison | bubbling cauldron tower, green toxic fumes, wooden frame with hanging vials |
| GoldMine | small gold mine entrance, pickaxe and gold nuggets, wooden mine cart |

#### Lv2 升级版

```
/imagine prompt: [Tower Name] defense tower, level 2 upgraded,
[Lv1 description] but enhanced with [upgrade details],
more ornate, additional decorations, slightly larger,
reinforced structure, metallic trim,
2D top-down tower defense sprite, hand-painted cartoon,
vibrant colors, clean outlines, cel-shading,
transparent background, game-ready,
--ar 1:1 --style raw --v 6.1 --s 250
```

**Lv2 `[upgrade details]`**：

| 塔 | Lv2 升级描述 |
|----|-------------|
| Archer | reinforced with iron bands, twin crossbows, red battle flag |
| Mage | larger crystal, floating arcane books, glowing purple aura ring |
| Ice | bigger ice formation, ice crown ornament, frozen chains |
| Cannon | dual barrels, reinforced iron plating, ammunition rack |
| Poison | larger cauldron, toxic mushrooms growing, skull ornament |
| GoldMine | reinforced mine entrance, gold veins visible, minecart full of gold |

#### Lv3 满级华丽版

```
/imagine prompt: [Tower Name] defense tower, level 3 maximum upgrade,
legendary ultimate form, [Lv3 description],
golden ornaments, glowing magical effects, epic aura,
highly detailed, premium quality,
2D top-down tower defense sprite, hand-painted cartoon,
vibrant colors, clean outlines, cel-shading,
transparent background, game-ready,
--ar 1:1 --style raw --v 6.1 --s 300
```

**Lv3 `[upgrade details]`**：

| 塔 | Lv3 描述 |
|----|---------|
| Archer | golden eagle-topped tower, enchanted triple crossbow, magical arrows trail, royal banner |
| Mage | arcane observatory, massive floating crystal, purple lightning, ancient rune circle |
| Ice | ice palace spire, diamond-like ice crown, blizzard vortex, frozen throne |
| Cannon | triple-barrel siege cannon, steam-powered, golden imperial eagle, explosive shells visible |
| Poison | plague laboratory, giant toxic cauldron, tentacle vines, skull smoke stacks |
| GoldMine | golden mountain vault, overflowing treasure, gem-studded entrance, golden dragon statue |

---

### 3B. 怪物静态图 (Enemies) — Midjourney

```
/imagine prompt: [Enemy Name] enemy character,
[specific description],
2D top-down RPG enemy sprite, hand-painted cartoon,
facing downward (walking toward viewer),
vibrant colors, clean outlines, cel-shading,
dynamic pose suggesting movement,
transparent background, game-ready sprite,
--ar 1:1 --style raw --v 6.1 --s 200
```

**10种怪物的 `[specific description]`**：

| 怪物 | 描述 |
|------|------|
| Infantry | red-armored foot soldier with sword and shield, standard medieval infantry |
| Assassin | dark-cloaked rogue with twin daggers, shadowy purple aura, nimble pose |
| Knight | heavily armored knight with great sword, silver full plate armor, blue cape |
| Flyer | winged imp/gargoyle creature, bat-like wings spread open, hovering pose |
| Healer | green-robed priest/shaman, glowing healing staff, nature magic aura |
| Slime | translucent green jelly creature, cute round shape, glowing core visible |
| Rogue | dark hooded thief, cloak of shadows, dual daggers, smoke trail |
| ShieldMage | blue-robed mage with magical barrier shield, floating spell book, arcane runes |
| Boss Dragon | massive fire dragon, red scales, burning eyes, fire breath, epic proportions |
| Boss Giant | colossal stone golem/iron giant, glowing rune cracks, earth-shaking presence |

---

### 3C. 怪物行走帧动画 — SDXL + AnimateDiff / 手动流程

**MJ 无法直接生成 SpriteSheet**，推荐两种方案：

#### 方案 A：SDXL + AnimateDiff（推荐）

```python
# ComfyUI 工作流参数
prompt = "[Enemy Name] walking cycle, top-down view, [description], pixel art style"
negative = "3D, realistic, blurry, deformed"
model = "sdxl_base_1.0"
animatediff_model = "mm_sdxl_v10_beta"
frames = 8
fps = 10
width = 512  # 总宽=8帧×64
height = 64
```

生成后用 FFmpeg 或 Python 脚本拼接为水平 SpriteSheet。

#### 方案 B：MJ 生成关键帧 + Aseprite 补帧

1. MJ 生成 4 个关键姿势（静立/迈左腿/双脚着地/迈右腿）
2. 导入 Aseprite，用 Onion Skin 手动补间到 8 帧
3. 导出为水平排列的 SpriteSheet PNG

**输出规格**：512×64px（普通怪）或 768×96px（Boss），一行 6-8 帧，PPU = 帧高度

---

### 3D. 投射物 — SDXL

```
prompt: [Projectile Name], 2D game projectile sprite,
[specific description],
top-down view, glowing energy trail,
clean design, transparent background,
--cfg 7 --steps 30 --size 128x128
```

| 投射物 | 描述 |
|--------|------|
| Arrow | wooden arrow with metal tip, slight motion blur trail |
| Magic Bolt | glowing purple energy orb, arcane spark trail |
| Ice Shard | sharp ice crystal, cold blue glow, frost particles |
| Cannonball | iron cannonball with fire trail, smoke behind |
| Poison Glob | green toxic bubble, dripping acid, toxic vapor |
| Ice Beam | concentrated ice beam, white-blue laser, frost crystals |

---

### 3E. 地图 Tiles — Midjourney + SDXL Tiling

#### 步骤 1：MJ 生成底图

```
/imagine prompt: [Terrain Type] terrain texture for top-down 2D game,
seamless tileable pattern, [specific description],
hand-painted, vibrant, detailed ground texture,
flat top-down view, game-ready tile,
--ar 1:1 --tile --style raw --v 6.1 --s 150
```

#### 步骤 2：SDXL 确保无缝平铺

```python
# img2img with tiling enabled
prompt = "seamless tileable [terrain] texture, top-down game tile"
tiling = True
denoising_strength = 0.35  # 轻微修正接缝
```

| 地形 | 描述 |
|------|------|
| Grass | lush green grass with small wildflowers, soft shadows |
| Path | dirt road with cobblestones, worn tracks, sandy edges |
| Rock | rough grey stone, moss covered, cracked surface |
| Water | clear blue water, gentle ripples, reflective surface |
| Desert | golden sand dunes, dry cracked earth, scattered pebbles |
| Snow | white snow covered ground, ice patches, frost crystals |
| Lava | molten orange lava, dark volcanic rock, glowing cracks |
| Swamp | murky green swamp, lily pads, twisted roots |
| Dungeon | dark stone floor, ancient tiles, torch light reflections |
| Void | ethereal purple void, floating particles, reality cracks |

---

### 3F. UI 图标 — Midjourney

```
/imagine prompt: [Icon Name] game UI icon,
[specific description],
2D hand-painted style, clean design,
slight 3D depth, soft gradient shadow,
transparent background, game-ready icon,
--ar 1:1 --style raw --v 6.1 --s 200
```

**图标描述表（20张）**：

| # | 图标 | 文件名 | 尺寸 | 描述 |
|---|------|--------|------|------|
| U01 | Coin | `ui_icon_coin.png` | 64×64 | shiny golden coin with embossed crown symbol |
| U02 | Heart | `ui_icon_heart.png` | 64×64 | glossy red heart with inner highlight glow |
| U03 | Wave Flag | `ui_icon_wave_flag.png` | 64×64 | tattered battle flag on wooden pole, red cloth |
| U04 | Archer Btn | `ui_btn_tower_archer.png` | 96×96 | wooden bow with arrow nocked, green quiver, ranger theme |
| U05 | Mage Btn | `ui_btn_tower_mage.png` | 96×96 | glowing purple crystal orb on arcane pedestal, magical sparkles |
| U06 | Ice Btn | `ui_btn_tower_ice.png` | 96×96 | sharp ice crystal shard, frost aura, pale blue glow |
| U07 | Cannon Btn | `ui_btn_tower_cannon.png` | 96×96 | iron cannon barrel with lit fuse, smoke wisps, military theme |
| U08 | Poison Btn | `ui_btn_tower_poison.png` | 96×96 | bubbling green potion flask, toxic drip, skull cork stopper |
| U09 | GoldMine Btn | `ui_btn_tower_goldmine.png` | 96×96 | golden pickaxe crossed with gold nugget, sparkling gems |
| U10 | Upgrade | `ui_icon_upgrade.png` | 48×48 | green upward arrow with sparkle effect |
| U11 | Sell | `ui_icon_sell.png` | 48×48 | golden coin with circular recycle arrow |
| U12 | Speed Up | `ui_icon_speedup.png` | 48×48 | double forward arrows, blue glowing |
| U13 | ATK Up | `roguelike_atk_up.png` | 64×64 | red crossed swords with flame |
| U14 | ASPD Up | `roguelike_aspd_up.png` | 64×64 | clock with speed lines, blue glow |
| U15 | Range Up | `roguelike_range_up.png` | 64×64 | expanding concentric circles, green |
| U16 | Crit Up | `roguelike_crit_up.png` | 64×64 | golden star with impact burst |
| U17 | Freeze | `roguelike_freeze.png` | 64×64 | blue snowflake crystal |
| U18 | Gold Bonus | `roguelike_gold_bonus.png` | 64×64 | stack of gold coins with plus symbol |
| U19 | Poison Up | `roguelike_poison_up.png` | 64×64 | green skull with toxic drip |
| U20 | AOE Up | `roguelike_aoe_up.png` | 64×64 | orange explosion circle expanding |

---

### 3G. 英雄立绘 — Midjourney

```
/imagine prompt: [Hero Name] hero character portrait,
[specific description],
2D anime/game illustration style, vivid colors,
dynamic pose, detailed armor/clothing,
soft lighting, painterly finish,
[form-specific suffix],
--ar [ratio] --style raw --v 6.1 --s 350
```

**形态参数**：

| 形态 | 后缀 | 比例 | 尺寸 |
|------|------|------|------|
| Full (全身立绘) | `full body standing pose, transparent background` | `--ar 1:1` | 1024×1024 |
| Half (半身像) | `upper body portrait, dramatic lighting` | `--ar 1:1` | 512×512 |
| Avatar (头像) | `close-up face portrait, circular frame` | `--ar 1:1` | 128×128 |

**6英雄描述**：

| 英雄 | 描述 |
|------|------|
| 精灵游侠·希尔薇 | elven ranger girl, long silver hair, green cloak, crystal bow, forest theme, pointed ears |
| 暗铁骑士·加尔文 | dark iron knight, heavy black plate armor, glowing red visor, massive greatsword |
| 符文女巫·艾拉 | rune witch, purple robes, floating spell books, arcane symbols, mystical staff |
| 炎魔法师·伊格尼斯 | fire mage, red and gold robes, flame crown, burning hands, phoenix motif |
| 矮人矿工·布隆 | dwarf miner, thick beard, mining helmet with lamp, golden pickaxe, barrel-chested |
| 天选者·阿斯特拉 | celestial chosen one, white and gold armor, angel wings, holy sword, divine halo |

---

### 3H. 特效纹理 — SDXL

```
prompt: [VFX Name], 2D game visual effect texture,
[description], transparent background,
glowing energy, soft particle look,
--cfg 7 --steps 25 --size 64x64
```

适合 SDXL img2img 微调：先生成基础形状，再在 Photoshop/GIMP 中调整透明度和混合模式。

---

### 3I. 背景 & 抽卡UI素材 — Midjourney

#### 场景背景（3张，16:9）

```
/imagine prompt: [Scene] background for 2D mobile tower defense game,
[description],
wide panoramic view, atmospheric lighting,
hand-painted illustration style, vibrant fantasy colors,
high detail, 1920x1080 resolution,
--ar 16:9 --style raw --v 6.1 --s 400
```

| 背景 | 描述 |
|------|------|
| Main Menu | enchanted castle on hilltop, sunset sky, floating islands, magical particles |
| Battle | green meadow battlefield, distant mountains, clear sky with clouds |
| Gacha | mystical summoning chamber, swirling magic portal, starry void |

#### 抽卡UI素材（5张）

```
/imagine prompt: [Asset Name],
[description],
2D game UI element, hand-painted fantasy style,
clean design, vibrant colors,
transparent background, game-ready asset,
--ar [ratio] --style raw --v 6.1 --s 250
```

| 素材 | 比例 | 尺寸 | 描述 |
|------|------|------|------|
| Summon Circle | `--ar 1:1` | 512×512 | magical summoning circle, glowing arcane runes on ground, rotating mystic symbols, purple and gold energy lines, top-down view |
| Card Back | `--ar 2:3` | 256×384 | fantasy card back design, ornate golden border, central crystal gem, dark blue velvet pattern, mysterious rune symbols |
| Card Frame R | `--ar 2:3` | 256×384 | blue-tier card frame border, silver ornamental edges, sapphire gem corners, subtle blue glow, empty center for portrait |
| Card Frame SR | `--ar 2:3` | 256×384 | purple-tier card frame border, amethyst ornamental edges, purple gem corners, purple magical aura, empty center for portrait |
| Card Frame SSR | `--ar 2:3` | 256×384 | golden legendary card frame border, diamond ornamental edges, golden dragon motif, brilliant golden glow and particles, empty center for portrait |

---

## 四、后处理流水线

### 4.1 通用后处理步骤

```
MJ/SDXL 原图
  → ① 去背景 (rembg / remove.bg / Photoshop)
  → ② 统一尺寸 (ImageMagick: convert -resize 512x512)
  → ③ 描边强化 (Photoshop: Stroke 2px dark outline)
  → ④ 色彩校准 (统一色温/饱和度，匹配游戏色板)
  → ⑤ 导出 PNG-24 (透明背景，sRGB色彩空间)
  → ⑥ 压缩 (pngquant --quality=80-95 --speed=1)
  → ⑦ 放入 Resources/Sprites/ 对应目录
```

### 4.2 SpriteSheet 后处理

```
8张关键帧图
  → ① 统一裁切到相同画布
  → ② Aseprite 排列为水平一行
  → ③ 导出为单张 PNG (宽=帧数×帧宽, 高=帧高)
  → ④ 放入 Resources/Sprites/Enemies/enemy_{name}_walk.png
```

### 4.3 Tile 无缝平铺验证

```
单张 Tile 图
  → ① SDXL img2img (tiling=true, denoise=0.3) 修正接缝
  → ② 4×4 拼接预览验证无缝
  → ③ 如有接缝 → Photoshop Clone Stamp 手动修
  → ④ 导出 256×256 PNG
```

### 4.4 批量处理脚本

```bash
#!/bin/bash
# process_sprites.sh — 批量后处理所有AI生成的图片
SRC_DIR="./ai_raw"
DST_DIR="./Assets/Resources/Sprites"

# 塔图
for f in $SRC_DIR/Towers/*.png; do
  name=$(basename "$f")
  convert "$f" -resize 512x512 -strip "$DST_DIR/Towers/$name"
  pngquant --quality=80-95 --speed=1 --force --output "$DST_DIR/Towers/$name" "$DST_DIR/Towers/$name"
done

# 怪物图
for f in $SRC_DIR/Enemies/*.png; do
  name=$(basename "$f")
  convert "$f" -resize 512x512 -strip "$DST_DIR/Enemies/$name"
  pngquant --quality=80-95 --speed=1 --force --output "$DST_DIR/Enemies/$name" "$DST_DIR/Enemies/$name"
done

# UI图标
for f in $SRC_DIR/UI/*.png; do
  name=$(basename "$f")
  target_size="64x64"
  [[ "$name" == *btn_tower* ]] && target_size="96x96"
  convert "$f" -resize $target_size -strip "$DST_DIR/UI/$name"
  pngquant --quality=85-95 --speed=1 --force --output "$DST_DIR/UI/$name" "$DST_DIR/UI/$name"
done

echo "✅ 批量处理完成"
```

---

## 五、生产优先级排序

按对游戏体验的影响程度排序，建议按以下批次制作：

| 批次 | 资源 | 数量 | 工具 | 预计工时 | 优先原因 |
|------|------|------|------|---------|---------|
| **P0** | 缺失怪物静态图 (5张) | 5 | MJ | 2h | 纯色方块严重影响游戏体验 |
| **P0** | UI图标 (20张) | 20 | MJ | 4h | 当前全是文字代替 |
| **P1** | 塔 lv2/lv3 (12张) | 12 | MJ | 3h | 升级无视觉变化 |
| **P1** | 英雄头像 (6张) | 6 | MJ | 2h | 主界面/抽卡需要 |
| **P2** | 怪物行走帧 (10张) | 10 | SDXL+AnimateDiff | 6h | 代码弹跳动画暂够用 |
| **P2** | 特效纹理 (12张) | 12 | SDXL | 3h | 程序化纹理暂够用 |
| **P3** | 英雄全身/半身 (12张) | 12 | MJ | 4h | 英雄面板展示 |
| **P3** | 新地形 Tiles (6张) | 6 | MJ+SDXL | 2h | 新章节才需要 |
| **P3** | 背景 (8张) | 8 | MJ | 2h | 视觉氛围提升 |
| **P3** | 已有资源品质提升 (25张) | 25 | MJ img2img | 4h | 锦上添花 |

**总预计工时**：~32 小时（MJ ~21h + SDXL ~11h）

---

## 六、MJ 参数速查

| 参数 | 含义 | 推荐值 |
|------|------|--------|
| `--v 6.1` | 模型版本 | 最新版，质量最高 |
| `--style raw` | 减少 MJ 自动美化 | 游戏资产必用 |
| `--ar 1:1` | 正方形（大部分 sprite） | 塔/怪物/图标 |
| `--ar 16:9` | 宽屏（背景） | 场景背景 |
| `--s 200-400` | Stylize（越高越艺术化） | 普通200，立绘350 |
| `--tile` | 无缝平铺模式 | 仅地图Tile使用 |
| `--no` | 排除元素 | `--no text, watermark, signature` |
| `--q 2` | 质量倍率 | 重要资源用2x |
| `--iw 1.5` | 参考图权重 | lv2/lv3参考lv1时使用 |
