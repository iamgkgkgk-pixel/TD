# #383-395 角色图标 AI Prompt & 后处理方案

> **对应交互**：#383-395
> **生成日期**：2026-03-29
> **依赖**：`正式美术AI生图方案.md`、`美术风格方案.md`

---

## 总览

| # | 任务 | 输出 | 数量 | 状态 |
|---|------|------|------|------|
| 383 | 6种塔 → 列表缩略图标 | `Sprites/UI/icon_tower_*.png` | 6张 | 待生成 |
| 384 | 6种塔 → 图鉴详情大图标 | `Sprites/UI/icon_tower_*_detail.png` | 6张 | 待生成 |
| 385 | 10种怪物 → 图鉴缩略图标 | `Sprites/UI/icon_enemy_*.png` | 10张 | 待生成 |
| 386 | 10种怪物 → 图鉴详情大图标 | `Sprites/UI/icon_enemy_*_detail.png` | 10张 | 待生成 |
| 387 | 2种Boss → 专属警告图标 | `Sprites/UI/icon_boss_*.png` | 2张 | 待生成 |
| 388 | 英雄·希尔薇 3形态 | `Sprites/Heroes/hero_ranger_*.png` | 3张 | 待生成 |
| 389 | 英雄·加尔文 3形态 | `Sprites/Heroes/hero_knight_*.png` | 3张 | 待生成 |
| 390 | 英雄·艾拉 3形态 | `Sprites/Heroes/hero_witch_*.png` | 3张 | 待生成 |
| 391 | 英雄·伊格尼斯 3形态 | `Sprites/Heroes/hero_pyromancer_*.png` | 3张 | 待生成 |
| 392 | 英雄·布隆 3形态 | `Sprites/Heroes/hero_dwarf_*.png` | 3张 | 待生成 |
| 393 | 英雄·阿斯特拉 3形态 | `Sprites/Heroes/hero_chosen_*.png` | 3张 | 待生成 |
| 394 | 全部图标后处理（去背景+统一尺寸+圆角裁切） | 处理脚本 | — | 待执行 |
| 395 | 图标集成验证 | 代码接入 | — | 待执行 |

**共计 49 张图标**

---

## #383 — 塔列表缩略图标 (6张)

用于底部塔栏、图鉴列表、商城展示。尺寸 96×96。

### Prompt模板（Midjourney）

```
/imagine prompt: [Tower Name] icon for mobile tower defense game,
[description], chibi miniature style,
circular icon with dark border ring,
2D hand-painted, vibrant colors, clean outlines,
game UI icon, transparent background,
--ar 1:1 --style raw --v 6.1 --s 200
```

| 文件名 | Tower Name | Description |
|--------|-----------|-------------|
| `icon_tower_archer.png` | Archer Tower | small wooden tower with bow, green theme, arrow quiver |
| `icon_tower_mage.png` | Mage Tower | magical purple crystal on stone pedestal, arcane glow |
| `icon_tower_ice.png` | Ice Tower | frozen ice crystal spire, pale blue frost aura |
| `icon_tower_cannon.png` | Cannon Tower | iron cannon barrel on platform, orange fire spark |
| `icon_tower_poison.png` | Poison Tower | bubbling green cauldron, toxic fumes, skull flask |
| `icon_tower_goldmine.png` | Gold Mine | golden mine cart overflowing with gold, pickaxe |

### 后处理
1. `rembg` 去背景
2. `convert -resize 96x96 -gravity center -extent 96x96`
3. 放入 `Resources/Sprites/UI/`

---

## #384 — 塔图鉴详情大图标 (6张)

用于图鉴详情页、升级面板。尺寸 256×256。

### Prompt模板（Midjourney）

```
/imagine prompt: [Tower Name] defense tower detailed portrait,
[description], centered composition,
2D hand-painted fantasy style, rich details,
dramatic lighting from above, slight glow effects,
transparent background, high quality game art,
--ar 1:1 --style raw --v 6.1 --s 300
```

| 文件名 | Description |
|--------|-------------|
| `icon_tower_archer_detail.png` | elegant archer tower with wooden frame, bowstrings glowing, arrows flying, green vines decoration |
| `icon_tower_mage_detail.png` | mystical mage tower with floating crystal, spell books orbiting, purple energy streams |
| `icon_tower_ice_detail.png` | majestic ice crystal formation, snowflakes swirling, frozen ground, blue light beams |
| `icon_tower_cannon_detail.png` | heavy iron cannon fortress, smoke and sparks, ammunition stacked, brass fittings |
| `icon_tower_poison_detail.png` | dark alchemy laboratory tower, green bubbling cauldrons, vine tentacles, toxic mushrooms |
| `icon_tower_goldmine_detail.png` | dwarven gold mine entrance, treasure piles, gem-studded walls, golden glow |

---

## #385 — 怪物图鉴缩略图标 (10张)

用于波次预告、图鉴列表。尺寸 80×80。

### Prompt模板（Midjourney）

```
/imagine prompt: [Enemy Name] enemy portrait icon,
[description], fierce expression, battle-ready pose,
circular frame with dark rim,
2D hand-painted, game UI icon style,
transparent background,
--ar 1:1 --style raw --v 6.1 --s 200
```

| 文件名 | Enemy | Description |
|--------|-------|-------------|
| `icon_enemy_infantry.png` | Infantry | red armored soldier with sword, angry expression |
| `icon_enemy_assassin.png` | Assassin | dark hooded rogue, glowing purple eyes, dual daggers |
| `icon_enemy_knight.png` | Knight | silver armored knight, blue cape, great sword |
| `icon_enemy_flyer.png` | Flyer | winged imp creature, bat wings spread, red eyes |
| `icon_enemy_healer.png` | Healer | green robed shaman, healing staff with glow |
| `icon_enemy_slime.png` | Slime | cute green slime blob, glowing core, bouncy |
| `icon_enemy_rogue.png` | Rogue | shadowy thief, smoke trail, curved dagger |
| `icon_enemy_shieldmage.png` | Shield Mage | blue robed mage, magic barrier, spell book |
| `icon_enemy_boss_dragon.png` | Dragon Boss | fierce red dragon head, fire breath, golden horns |
| `icon_enemy_boss_giant.png` | Giant Boss | massive stone golem face, glowing rune cracks |

---

## #386 — 怪物图鉴详情大图标 (10张)

用于图鉴详情页、Boss警告。尺寸 256×256。

### Prompt模板

```
/imagine prompt: [Enemy Name] full body portrait,
[description], dynamic action pose,
2D hand-painted fantasy illustration,
dramatic lighting, detailed armor and weapons,
transparent background, high quality game art,
--ar 1:1 --style raw --v 6.1 --s 300
```

| 文件名 | Description |
|--------|-------------|
| `icon_enemy_infantry_detail.png` | charging red armored soldier, sword raised, shield forward, battle cry |
| `icon_enemy_assassin_detail.png` | leaping dark assassin, dual daggers crossed, purple shadow trail |
| `icon_enemy_knight_detail.png` | imposing armored knight, great sword planted, blue cape flowing |
| `icon_enemy_flyer_detail.png` | diving winged demon, bat wings spread wide, claws outstretched |
| `icon_enemy_healer_detail.png` | wise green shaman casting heal, nature magic circle, floating leaves |
| `icon_enemy_slime_detail.png` | giant translucent slime, multiple cores visible, splitting animation |
| `icon_enemy_rogue_detail.png` | shadow rogue emerging from darkness, dual daggers, smoke cloud |
| `icon_enemy_shieldmage_detail.png` | blue mage conjuring massive shield barrier, spell books floating |
| `icon_enemy_boss_dragon_detail.png` | epic fire dragon full body, wings spread, fire breath, massive scale |
| `icon_enemy_boss_giant_detail.png` | colossal stone giant, glowing rune body, fists raised, earth cracking |

---

## #387 — Boss专属警告图标 (2张)

用于Boss波次警告弹窗。尺寸 128×128，红色警告边框。

### Prompt模板

```
/imagine prompt: WARNING [Boss Name] boss alert icon,
[description], menacing close-up face,
red warning glow border, danger symbol,
2D game UI, dramatic dark background,
--ar 1:1 --style raw --v 6.1 --s 250
```

| 文件名 | Description |
|--------|-------------|
| `icon_boss_dragon.png` | dragon head close-up, burning eyes, fire breath, red alert frame |
| `icon_boss_giant.png` | stone giant face, cracked glowing runes, shaking ground effect, red alert frame |

---

## #388-393 — 英雄立绘 (每英雄3张 × 6英雄 = 18张)

### 统一Prompt模板（Midjourney）

#### Full 全身立绘 (1024×1024)
```
/imagine prompt: [Hero Name] hero character,
[description],
full body standing pose, dynamic confident stance,
2D anime game illustration, vivid colors,
detailed armor and weapons, soft dramatic lighting,
transparent background, high quality RPG character art,
--ar 1:1 --style raw --v 6.1 --s 350
```

#### Half 半身像 (512×512)
```
/imagine prompt: [Hero Name] hero character,
[description],
upper body portrait, looking at viewer,
2D anime game illustration, vivid colors,
dramatic close-up lighting, detailed face and armor,
transparent background,
--ar 1:1 --style raw --v 6.1 --s 350
```

#### Avatar 头像 (128×128)
```
/imagine prompt: [Hero Name] hero face portrait,
[description], close-up face,
circular avatar frame, golden border ring,
2D anime game style, expressive eyes,
--ar 1:1 --style raw --v 6.1 --s 250
```

### #388 精灵游侠·希尔薇

| 形态 | 文件名 | Description |
|------|--------|-------------|
| full | `hero_ranger_full.png` | young elven ranger girl, long flowing silver hair, emerald green cloak, crystal longbow, quiver of magical arrows, pointed ears, forest spirit companion, nature magic aura |
| half | `hero_ranger_half.png` | elven ranger girl upper body, silver hair flowing, green eyes, crystal bow held gracefully, confident gentle smile |
| avatar | `hero_ranger_avatar.png` | elf girl face, silver hair, green eyes, pointed ears, leaf crown ornament |

### #389 暗铁骑士·加尔文

| 形态 | 文件名 | Description |
|------|--------|-------------|
| full | `hero_knight_full.png` | imposing dark iron knight, full heavy black plate armor with red runic engravings, massive obsidian greatsword, glowing red visor, dark cape, intimidating stance |
| half | `hero_knight_half.png` | dark knight upper body, black iron helmet with red glowing visor, massive sword on shoulder, battle-scarred armor |
| avatar | `hero_knight_avatar.png` | dark knight helmet close-up, red glowing eyes through visor, iron crown |

### #390 符文女巫·艾拉

| 形态 | 文件名 | Description |
|------|--------|-------------|
| full | `hero_witch_full.png` | mysterious rune witch, flowing purple and midnight blue robes, ancient spell book floating beside her, arcane staff with crystal orb, glowing rune tattoos on arms, mystical purple eyes |
| half | `hero_witch_half.png` | witch upper body, purple robes, floating arcane symbols around her, spell book open, mysterious confident expression |
| avatar | `hero_witch_avatar.png` | witch face, purple eyes with arcane glow, silver hair with purple streaks, rune markings on forehead |

### #391 炎魔法师·伊格尼斯

| 形态 | 文件名 | Description |
|------|--------|-------------|
| full | `hero_pyromancer_full.png` | powerful fire mage, red and gold ornate robes, flame crown hovering above head, hands engulfed in magical fire, phoenix feather cloak, ember particles floating around |
| half | `hero_pyromancer_half.png` | fire mage upper body, flame crown, burning hands raised, confident fierce expression, red robes with gold trim |
| avatar | `hero_pyromancer_avatar.png` | fire mage face, flame crown, golden eyes burning with inner fire, red hair like flames |

### #392 矮人矿工·布隆

| 形态 | 文件名 | Description |
|------|--------|-------------|
| full | `hero_dwarf_full.png` | stout dwarf miner warrior, thick braided red beard, mining helmet with crystal lamp, golden enchanted pickaxe, barrel chest with leather apron, gem-studded belt, standing on pile of gold |
| half | `hero_dwarf_half.png` | dwarf upper body, big smile, red braided beard, mining helmet, golden pickaxe resting on shoulder, jolly expression |
| avatar | `hero_dwarf_avatar.png` | dwarf face, big red beard, mining helmet with lamp, friendly gruff expression, rosy cheeks |

### #393 天选者·阿斯特拉

| 形态 | 文件名 | Description |
|------|--------|-------------|
| full | `hero_chosen_full.png` | divine celestial chosen one, brilliant white and gold armor with ethereal glow, luminous angel wings spread wide, holy sword radiating light, divine halo, golden hair flowing, majestic heroic pose |
| half | `hero_chosen_half.png` | celestial warrior upper body, white gold armor, angel wings, holy sword, serene powerful expression, divine light from above |
| avatar | `hero_chosen_avatar.png` | celestial face, golden eyes glowing with divine light, white hair, golden halo, serene ethereal beauty |

---

## #394 — 后处理脚本

```bash
#!/bin/bash
# process_character_icons.sh

SRC="./ai_raw_icons"
DST="./Assets/Resources/Sprites"

echo "=== 处理塔图标 ==="
for f in $SRC/tower_icons/*.png; do
  name=$(basename "$f")
  # 去背景
  rembg i "$f" "/tmp/nobg_$name"
  # 缩放到96x96（列表图标）
  if [[ "$name" == *detail* ]]; then
    convert "/tmp/nobg_$name" -resize 256x256 -gravity center -extent 256x256 "$DST/UI/$name"
  else
    convert "/tmp/nobg_$name" -resize 96x96 -gravity center -extent 96x96 "$DST/UI/$name"
  fi
  pngquant --quality=80-95 --force --output "$DST/UI/$name" "$DST/UI/$name"
done

echo "=== 处理怪物图标 ==="
for f in $SRC/enemy_icons/*.png; do
  name=$(basename "$f")
  rembg i "$f" "/tmp/nobg_$name"
  if [[ "$name" == *detail* ]]; then
    convert "/tmp/nobg_$name" -resize 256x256 -gravity center -extent 256x256 "$DST/UI/$name"
  else
    convert "/tmp/nobg_$name" -resize 80x80 -gravity center -extent 80x80 "$DST/UI/$name"
  fi
  pngquant --quality=80-95 --force --output "$DST/UI/$name" "$DST/UI/$name"
done

echo "=== 处理Boss图标 ==="
for f in $SRC/boss_icons/*.png; do
  name=$(basename "$f")
  rembg i "$f" "/tmp/nobg_$name"
  convert "/tmp/nobg_$name" -resize 128x128 -gravity center -extent 128x128 "$DST/UI/$name"
  pngquant --quality=85-95 --force --output "$DST/UI/$name" "$DST/UI/$name"
done

echo "=== 处理英雄立绘 ==="
for f in $SRC/heroes/*.png; do
  name=$(basename "$f")
  rembg i "$f" "/tmp/nobg_$name"

  if [[ "$name" == *_full.* ]]; then
    convert "/tmp/nobg_$name" -resize 1024x1024 -gravity center -extent 1024x1024 "$DST/Heroes/$name"
  elif [[ "$name" == *_half.* ]]; then
    convert "/tmp/nobg_$name" -resize 512x512 -gravity center -extent 512x512 "$DST/Heroes/$name"
  elif [[ "$name" == *_avatar.* ]]; then
    convert "/tmp/nobg_$name" -resize 128x128 -gravity center -extent 128x128 "$DST/Heroes/$name"
  fi
  pngquant --quality=85-95 --force --output "$DST/Heroes/$name" "$DST/Heroes/$name"
done

echo "✅ 全部处理完成"
```

---

## #395 — 集成验证清单

| 验证项 | 检查方式 | 预期 |
|--------|---------|------|
| 塔图标在图鉴面板显示 | 打开塔图鉴 | 6种塔都有缩略图和详情图 |
| 怪物图标在波次预告显示 | 开始波次 | 波次预告显示怪物图标 |
| Boss警告图标显示 | Boss波次 | 全屏Boss警告用专属图标 |
| 英雄全身立绘在主界面显示 | 打开主界面 | 中央英雄展示区显示立绘 |
| 英雄半身像在英雄面板显示 | 打开英雄面板 | 详情页显示半身像 |
| 英雄头像在抽卡结果显示 | 抽卡 | 结果展示用头像图标 |
| 所有图标无白边/锯齿 | 目视检查 | 去背景干净 |
| 图标尺寸符合规格 | 文件属性检查 | 各尺寸正确 |
