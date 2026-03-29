# 地图素材 AI Prompt 方案（#406-415）

> **涵盖范围**：6种新章节地形Tileset + 配套路径纹理 + 地图装饰物
> **总计**：42张地图素材
> **核心要求**：所有Tile必须256×256可无缝平铺（seamless tileable）

---

## 一、章节-地形对应关系

| 章节 | 地形主题 | 地面纹理 | 路径纹理 | 配色 |
|------|---------|---------|---------|------|
| 1-5 | 草原（已有） | `tile_grass.png` ✅ | `tile_path.png` ✅ | 翠绿+土黄 |
| 6-10 | 沙漠 | `tile_desert.png` | `tile_desert_path.png` | 沙黄+深棕 |
| 11-15 | 雪地 | `tile_snow.png` | `tile_snow_path.png` | 白色+灰蓝 |
| 16-20 | 岩浆 | `tile_lava.png` | `tile_lava_path.png` | 暗灰+熔岩橙 |
| 21-25 | 沼泽 | `tile_swamp.png` | `tile_swamp_path.png` | 暗绿+泥褐 |
| 26-28 | 地牢 | `tile_dungeon.png` | `tile_dungeon_path.png` | 深灰+暗紫 |
| 29-30 | 虚空 | `tile_void.png` | `tile_void_path.png` | 深紫+星光 |

---

## 二、地形Tile Prompt（#406-411）

### 统一Prompt后缀

```
2D top-down game tileset texture, seamless tileable pattern,
hand-painted fantasy style, 256x256 pixels,
flat perspective, no perspective distortion,
game-ready asset, clean edges for tiling,
--ar 1:1 --tile --style raw --v 6.1 --s 300
```

> **关键参数**：`--tile` 确保MJ生成无缝可平铺纹理

### #406 — 沙漠地形（2张）

| # | 文件名 | 尺寸 | Prompt 描述 |
|---|--------|------|-------------|
| T01 | `tile_desert.png` | 256×256 | sandy desert ground texture, golden sand dunes with subtle wind ripples, scattered small pebbles, warm tone, dry cracked earth patches, sun-baked terrain |
| T02 | `tile_desert_path.png` | 256×256 | ancient stone brick road on desert sand, weathered sandstone pathway, worn cobblestone with sand between cracks, dark brown and tan tones |

### #407 — 雪地地形（2张）

| # | 文件名 | 尺寸 | Prompt 描述 |
|---|--------|------|-------------|
| T03 | `tile_snow.png` | 256×256 | snowy ground texture, fresh white snow with subtle blue shadows, small ice crystal sparkles, frozen grass tips poking through, winter terrain |
| T04 | `tile_snow_path.png` | 256×256 | trampled snow pathway with exposed frozen dirt, footprint impressions, ice patches, grey-blue packed snow, dark stone visible underneath |

### #408 — 岩浆地形（2张）

| # | 文件名 | 尺寸 | Prompt 描述 |
|---|--------|------|-------------|
| T05 | `tile_lava.png` | 256×256 | volcanic dark basalt rock ground, cracked obsidian surface, glowing orange lava veins in cracks, ember particles, hellish terrain, dark grey with orange glow |
| T06 | `tile_lava_path.png` | 256×256 | cooled lava rock pathway, dark basalt stepping stones, faint orange glow between stones, volcanic ash, charred surface |

### #409 — 沼泽地形（2张）

| # | 文件名 | 尺寸 | Prompt 描述 |
|---|--------|------|-------------|
| T07 | `tile_swamp.png` | 256×256 | murky swamp ground, dark green muddy terrain with small puddles, moss and algae patches, dead leaves, wet and slimy surface, boggy earth |
| T08 | `tile_swamp_path.png` | 256×256 | wooden plank boardwalk on swamp, old mossy timber walkway over mud, rotting wood planks, green vines growing through gaps |

### #410 — 地牢地形（2张）

| # | 文件名 | 尺寸 | Prompt 描述 |
|---|--------|------|-------------|
| T09 | `tile_dungeon.png` | 256×256 | stone dungeon floor, grey cobblestone with dark mortar lines, ancient worn surface, small cracks, dungeon atmosphere, cold stone texture |
| T10 | `tile_dungeon_path.png` | 256×256 | ornate dungeon corridor floor, polished dark stone tiles with purple rune engravings, magical glow lines between tiles, ancient arcane patterns |

### #411 — 虚空地形（2张）

| # | 文件名 | 尺寸 | Prompt 描述 |
|---|--------|------|-------------|
| T11 | `tile_void.png` | 256×256 | cosmic void space ground, deep purple and black nebula surface, floating star particles, ethereal crystalline fragments, otherworldly dimension floor |
| T12 | `tile_void_path.png` | 256×256 | luminous crystal bridge pathway floating in void, translucent glowing purple and cyan crystal tiles, starlight reflection, magical floating road |

---

## 三、地图装饰物Prompt（#412-414）

> 路径：`Resources/Sprites/Maps/deco_{name}.png`
> 用途：放在 DecorationTilemap（sortingOrder=1）上点缀地图
> 尺寸：128×128（单个装饰物，透明底）

### #412 — 通用装饰物（6张，所有章节通用）

```
2D top-down game decoration sprite, transparent background,
hand-painted fantasy style, small environmental detail,
--ar 1:1 --style raw --v 6.1 --s 250
```

| # | 文件名 | Prompt 描述 |
|---|--------|-------------|
| D01 | `deco_tree_green.png` | small green leafy tree top-down view, round canopy with shadows, lush fantasy tree |
| D02 | `deco_tree_autumn.png` | autumn tree with orange and red leaves, top-down view, warm colored canopy |
| D03 | `deco_rock_small.png` | small grey rocks cluster, 2-3 pebbles, top-down view, natural stone |
| D04 | `deco_rock_large.png` | large boulder, mossy grey stone, top-down view, imposing rock |
| D05 | `deco_bush.png` | green bush shrub, top-down view, small leafy undergrowth |
| D06 | `deco_flowers_wild.png` | cluster of colorful wildflowers, top-down view, purple yellow pink petals |

### #413 — 章节特色装饰（8张）

| # | 文件名 | 章节 | Prompt 描述 |
|---|--------|------|-------------|
| D07 | `deco_cactus.png` | 沙漠 | desert cactus plant, top-down view, green prickly pear, small spines |
| D08 | `deco_sand_bones.png` | 沙漠 | animal skull and bones on sand, top-down view, bleached white skeleton |
| D09 | `deco_ice_crystal.png` | 雪地 | ice crystal formation, top-down view, sparkling blue frozen spire |
| D10 | `deco_snowdrift.png` | 雪地 | small snow pile drift, top-down view, white powdery mound |
| D11 | `deco_lava_crack.png` | 岩浆 | lava crack vent, top-down view, glowing orange fissure with smoke |
| D12 | `deco_mushroom_toxic.png` | 沼泽 | glowing toxic mushroom cluster, top-down view, green bioluminescent caps |
| D13 | `deco_dungeon_pillar.png` | 地牢 | broken stone pillar, top-down view, ancient ruins column |
| D14 | `deco_void_crystal.png` | 虚空 | floating void crystal, top-down view, purple glowing crystal shard |

### #414 — 水域装饰（4张）

| # | 文件名 | Prompt 描述 |
|---|--------|-------------|
| D15 | `deco_water_lily.png` | water lily pad with flower, top-down view, green pad pink lotus |
| D16 | `deco_water_reeds.png` | tall water reeds, top-down view, green cattails |
| D17 | `deco_bridge_wood.png` | small wooden bridge segment, top-down view, planks over water |
| D18 | `deco_puddle.png` | small water puddle, top-down view, reflective surface, rain puddle |

---

## 四、图集打包和预处理（#415）

### 后处理脚本

```bash
#!/bin/bash
# process_map_tiles.sh — 地图素材后处理

INPUT_DIR="./raw_tiles"
OUTPUT_DIR="./Unity/AetheraSurvivors/Assets/Resources/Sprites/Maps"

# 地形Tile：确保256x256 + 可平铺
for img in "$INPUT_DIR"/tile_*.png; do
    filename=$(basename "$img")
    # 去背景不需要（地形是满铺的）
    convert "$img" -resize 256x256! "$OUTPUT_DIR/$filename"
    echo "✅ Tile: $filename"
done

# 装饰物：去背景 + 128x128
for img in "$INPUT_DIR"/deco_*.png; do
    filename=$(basename "$img")
    rembg i "$img" "/tmp/nobg_$filename"
    convert "/tmp/nobg_$filename" -resize 128x128 -gravity center -extent 128x128 "$OUTPUT_DIR/$filename"
    echo "✅ Deco: $filename"
done

# PNG压缩
cd "$OUTPUT_DIR"
pngquant --quality=85-95 --strip --ext .png --force tile_*.png deco_*.png 2>/dev/null
echo "🎉 处理完成！"
```

### 平铺测试验证

生成后务必验证Tile的无缝性：
```bash
# 用ImageMagick拼4×4检查接缝
for tile in tile_desert tile_snow tile_lava tile_swamp tile_dungeon tile_void; do
    convert "$tile.png" "$tile.png" +append \
            "$tile.png" "$tile.png" +append \
            -append "${tile}_4x4_test.png"
    echo "检查 ${tile}_4x4_test.png 是否有接缝"
done
```

---

## 五、代码集成映射

### 需要修改的文件

| 文件 | 改动内容 |
|------|---------|
| `MapRenderer.cs` | `SetupBlendRendering()` 根据 `LevelMapData.chapter` 选择不同的地面/路径纹理对 |
| `MapRenderer.cs` | `RenderMap()` 中为 DecorationTilemap 添加装饰物随机放置逻辑 |
| `SpriteLoader.cs` | `PreloadAll()` 预加载列表增加新Tile |
| `SpriteLoader.cs` | 新增 `LoadMapDecoration(name)` 方法 |

### 章节→纹理映射逻辑（伪代码）

```csharp
// MapRenderer.SetupBlendRendering() 中：
string groundTile = "grass", pathTile = "path"; // 默认草原

switch (GridSystem.Instance.CurrentChapter)
{
    case int c when c >= 6 && c <= 10:
        groundTile = "desert"; pathTile = "desert_path"; break;
    case int c when c >= 11 && c <= 15:
        groundTile = "snow"; pathTile = "snow_path"; break;
    case int c when c >= 16 && c <= 20:
        groundTile = "lava"; pathTile = "lava_path"; break;
    case int c when c >= 21 && c <= 25:
        groundTile = "swamp"; pathTile = "swamp_path"; break;
    case int c when c >= 26 && c <= 28:
        groundTile = "dungeon"; pathTile = "dungeon_path"; break;
    case int c when c >= 29:
        groundTile = "void"; pathTile = "void_path"; break;
}

Sprite grassSprite = SpriteLoader.LoadMapTile(groundTile);
Sprite pathSprite = SpriteLoader.LoadMapTile(pathTile);
```

---

## 六、资源目录结构

```
Resources/Sprites/Maps/
├── 已有 ──────────────
│   ├── tile_grass.png       ✅ 第1-5章地面
│   ├── tile_grass1.png      ✅ 变体（未引用）
│   ├── tile_grass2.png      ✅ 变体（未引用）
│   ├── tile_path.png        ✅ 第1-5章路径
│   ├── tile_rock.png        ✅ 障碍物
│   ├── tile_flowers.png     ✅ 塔位
│   ├── tile_water.png       ✅ 水域（未引用）
│   └── tile_castle_wall.png ✅ 基地
│
├── #406-411 新章节地形 ──
│   ├── tile_desert.png          沙漠地面
│   ├── tile_desert_path.png     沙漠路径
│   ├── tile_snow.png            雪地地面
│   ├── tile_snow_path.png       雪地路径
│   ├── tile_lava.png            岩浆地面
│   ├── tile_lava_path.png       岩浆路径
│   ├── tile_swamp.png           沼泽地面
│   ├── tile_swamp_path.png      沼泽路径
│   ├── tile_dungeon.png         地牢地面
│   ├── tile_dungeon_path.png    地牢路径
│   ├── tile_void.png            虚空地面
│   └── tile_void_path.png       虚空路径
│
└── #412-414 装饰物 ─────
    ├── deco_tree_green.png      通用绿树
    ├── deco_tree_autumn.png     通用秋树
    ├── deco_rock_small.png      小石头
    ├── deco_rock_large.png      大石头
    ├── deco_bush.png            灌木
    ├── deco_flowers_wild.png    野花
    ├── deco_cactus.png          沙漠仙人掌
    ├── deco_sand_bones.png      沙漠骨骸
    ├── deco_ice_crystal.png     雪地冰晶
    ├── deco_snowdrift.png       雪堆
    ├── deco_lava_crack.png      岩浆裂缝
    ├── deco_mushroom_toxic.png  沼泽毒蘑菇
    ├── deco_dungeon_pillar.png  地牢石柱
    ├── deco_void_crystal.png    虚空水晶
    ├── deco_water_lily.png      荷叶
    ├── deco_water_reeds.png     芦苇
    ├── deco_bridge_wood.png     木桥
    └── deco_puddle.png          水洼
```

---

## 七、生产优先级

| 优先级 | 素材 | 数量 | 理由 |
|--------|------|------|------|
| **P0** | 沙漠地形（#406）| 2张 | 第6章马上要用 |
| **P1** | 雪地+岩浆（#407-408）| 4张 | 11-20章 |
| **P1** | 通用装饰物（#412）| 6张 | 所有章节立刻提升品质 |
| **P2** | 沼泽+地牢+虚空（#409-411）| 6张 | 后期章节 |
| **P2** | 章节特色装饰（#413）| 8张 | 差异化 |
| **P3** | 水域装饰（#414）| 4张 | 锦上添花 |

**预估总工时**：~10小时（MJ生成~5小时 + 平铺验证+后处理~3小时 + 代码集成~2小时）
