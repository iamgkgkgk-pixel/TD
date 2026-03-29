# 功能图标 AI Prompt 方案（#396-405）

> **涵盖范围**：技能图标、Buff图标、商品图标、货币图标、系统功能图标、成就/任务图标
> **总计**：61张功能图标
> **统一风格**：Kingdom Rush + Arknights chibi，手绘卡通，描边清晰，透明底

---

## 一、统一 Prompt 后缀（所有图标共用）

```
hand-painted 2D game icon, thick black outline, vibrant colors,
clean design, transparent background, game-ready asset,
Kingdom Rush art style, chibi fantasy theme,
--style raw --v 6.1 --s 250
```

---

## 二、图标分类详细 Prompt

### #396 — 英雄主动技能图标（6张）

> 路径：`Resources/Sprites/UI/skill_active_{id}.png`
> 尺寸：96×96px | Prompt 比例：`--ar 1:1`

| # | 文件名 | 英雄 | 技能名 | Prompt 描述 |
|---|--------|------|--------|-------------|
| S01 | `skill_active_knight.png` | 铁壁骑士 | 无敌护盾 | golden divine shield with holy light rays, glowing barrier, white and gold energy, defensive stance icon |
| S02 | `skill_active_archer.png` | 精灵射手 | 万箭齐发 | volley of glowing green arrows raining from sky, multiple arrows in fan pattern, dynamic motion lines |
| S03 | `skill_active_ice_witch.png` | 霜雪女巫 | 暴风雪 | swirling blizzard vortex with ice crystals, snowflakes, pale blue frozen wind, frost magic circle |
| S04 | `skill_active_fire_mage.png` | 炎魔法师 | 陨石术 | flaming meteor falling from sky with fire trail, impact explosion, red and orange flames, destruction |
| S05 | `skill_active_dwarf.png` | 矮人矿工 | 淘金热 | pile of golden coins erupting upward, gold rush explosion, sparkling treasure, pickaxe crossed with gold |
| S06 | `skill_active_chosen.png` | 天选者 | 神之裁决 | divine judgment beam from heavens, holy cross light, golden celestial energy, angelic wings silhouette |

---

### #397 — 英雄被动技能图标（6张）

> 路径：`Resources/Sprites/UI/skill_passive_{id}.png`
> 尺寸：96×96px | Prompt 比例：`--ar 1:1`

| # | 文件名 | 英雄 | 被动名 | Prompt 描述 |
|---|--------|------|--------|-------------|
| P01 | `skill_passive_knight.png` | 铁壁骑士 | 坚韧意志 | heart-shaped iron shield with plus symbol, HP boost, sturdy willpower aura, red and silver |
| P02 | `skill_passive_archer.png` | 精灵射手 | 鹰眼 | eagle eye with targeting reticle, expanded vision circle, green scope, precision aiming |
| P03 | `skill_passive_ice_witch.png` | 霜雪女巫 | 寒冰之力 | frozen ice crystal with power aura, blue ice enchantment, snowflake pattern, cold energy boost |
| P04 | `skill_passive_fire_mage.png` | 炎魔法师 | 火焰亲和 | flame rune circle with fire affinity symbol, red arcane pattern, burning enchantment |
| P05 | `skill_passive_dwarf.png` | 矮人矿工 | 矿脉感知 | glowing underground vein of gold ore, mining sense, earth tremor lines, golden ore detection |
| P06 | `skill_passive_chosen.png` | 天选者 | 命运垂青 | four-leaf clover with golden glow, fortune star, extra card symbol, lucky charm |

---

### #398 — Buff/状态图标（10张）

> 路径：`Resources/Sprites/UI/buff_{id}.png`
> 尺寸：48×48px | Prompt 比例：`--ar 1:1`

| # | 文件名 | Buff名 | Prompt 描述 |
|---|--------|--------|-------------|
| B01 | `buff_slow.png` | 减速 | blue snail shell with slow motion lines, speed reduction, cyan debuff |
| B02 | `buff_poison.png` | 中毒 | green skull with toxic drip bubbles, poison DOT, sickly green |
| B03 | `buff_freeze.png` | 冰冻 | solid ice crystal block, frozen solid, pale blue frost, snowflake center |
| B04 | `buff_burn.png` | 灼烧 | orange flame engulfing, burning fire DOT, hot ember particles |
| B05 | `buff_armor_reduce.png` | 护甲降低 | cracked broken shield with down arrow, defense reduction, grey and red |
| B06 | `buff_stun.png` | 眩晕 | spinning yellow stars circling, dizzy spiral, stun effect, cartoon stars |
| B07 | `buff_speed_up.png` | 加速 | blue speed arrow with motion trail, fast forward, velocity boost |
| B08 | `buff_heal.png` | 持续治疗 | green healing cross with sparkles, regeneration, heart pulse, nature magic |
| B09 | `buff_shield.png` | 护盾 | blue magical barrier dome, protective shield bubble, shimmering defense |
| B10 | `buff_armor_up.png` | 护甲提升 | reinforced steel shield with up arrow, defense boost, silver and blue |

---

### #399 — 货币 & 资源图标（5张）

> 路径：`Resources/Sprites/UI/ui_currency_{id}.png`
> 尺寸：64×64px | Prompt 比例：`--ar 1:1`

| # | 文件名 | 名称 | Prompt 描述 |
|---|--------|------|-------------|
| C01 | `ui_currency_diamond.png` | 钻石 | brilliant blue diamond gemstone, faceted crystal, sparkling highlight, precious gem |
| C02 | `ui_currency_gold.png` | 金币 | shiny golden coin with embossed crown, stack of gold coins, treasure | 
| C03 | `ui_currency_stamina.png` | 体力 | green lightning bolt energy icon, stamina power, electric charge, vitality |
| C04 | `ui_currency_exp_book.png` | 经验书 | ancient leather-bound spell book with glowing pages, experience tome, magical knowledge |
| C05 | `ui_currency_summon_ticket.png` | 召唤券 | golden mystical scroll with seal, summon ticket, arcane invitation, purple wax stamp |

---

### #400 — 商城商品图标（10张）

> 路径：`Resources/Sprites/UI/shop_{id}.png`
> 尺寸：96×96px | Prompt 比例：`--ar 1:1`

| # | 文件名 | 商品 | Prompt 描述 |
|---|--------|------|-------------|
| SH01 | `shop_first_pay.png` | 首充礼包 | ornate treasure chest overflowing with gems and gold, first purchase gift, red ribbon bow, premium |
| SH02 | `shop_month_card.png` | 月卡 | golden calendar card with crescent moon emblem, 30-day subscription, premium monthly pass |
| SH03 | `shop_week_card.png` | 周卡 | silver calendar card with 7-star emblem, weekly subscription, 7-day pass |
| SH04 | `shop_diamond_small.png` | 少量钻石 | single small blue diamond, basic gem purchase, simple crystal |
| SH05 | `shop_diamond_medium.png` | 中量钻石 | cluster of 3 blue diamonds, medium gem bundle, sparkling crystals |
| SH06 | `shop_diamond_large.png` | 大量钻石 | pile of blue diamonds overflowing from pouch, large gem bundle, treasure bag |
| SH07 | `shop_stamina.png` | 体力药水 | green glowing potion bottle, stamina recovery elixir, bubbling liquid, cork stopper |
| SH08 | `shop_gold_bag.png` | 金币袋 | bulging cloth sack of gold coins, money bag with coin symbol, wealthy |
| SH09 | `shop_newbie_gift.png` | 新手礼包 | colorful gift box with pink ribbon, newbie starter pack, welcome present, sparkles |
| SH10 | `shop_growth_gift.png` | 成长礼包 | ascending staircase of treasure chests, growth package, leveling up rewards, golden ladder |

---

### #401 — 设置界面图标（5张）

> 路径：`Resources/Sprites/UI/ui_setting_{id}.png`
> 尺寸：48×48px | Prompt 比例：`--ar 1:1`

| # | 文件名 | 功能 | Prompt 描述 |
|---|--------|------|-------------|
| ST01 | `ui_setting_gear.png` | 设置 | metallic gear cog wheel icon, settings symbol, silver mechanical, precision |
| ST02 | `ui_setting_music.png` | BGM音量 | musical note with speaker, BGM volume control, purple melody, sound wave |
| ST03 | `ui_setting_sfx.png` | SFX音量 | speaker cone with sound waves, SFX volume control, blue audio, vibration lines |
| ST04 | `ui_setting_language.png` | 语言 | globe earth icon with speech bubble, language selection, international, blue planet |
| ST05 | `ui_setting_feedback.png` | 反馈 | pencil writing on paper note, feedback form, green notepad, comment bubble |

---

### #402 — 系统功能入口图标（9张）

> 路径：`Resources/Sprites/UI/ui_nav_{id}.png`
> 尺寸：64×64px | Prompt 比例：`--ar 1:1`

| # | 文件名 | 功能 | 当前占位 | Prompt 描述 |
|---|--------|------|---------|-------------|
| N01 | `ui_nav_battle.png` | 战斗 | 文字"开始战斗" | crossed swords with fire, battle entrance, combat challenge, red and gold |
| N02 | `ui_nav_hero.png` | 英雄 | 文字"英雄" | hero silhouette with cape, character roster, blue hero emblem, warrior stance |
| N03 | `ui_nav_shop.png` | 商城 | 文字"商城" | treasure chest with price tag, shop entrance, gold and purple, merchant |
| N04 | `ui_nav_gacha.png` | 召唤 | 文字"抽卡" | mystical summoning portal with stars, gacha entrance, purple vortex, magical |
| N05 | `ui_nav_social.png` | 社交 | 文字"好友" | two people silhouette with handshake, social connection, friendly, blue and green |
| N06 | `ui_nav_quest.png` | 任务 | 文字"任务" | checklist clipboard with checkmark, daily quest, mission board, green tick |
| N07 | `ui_nav_mail.png` | 邮件 | 文字"邮件" | sealed envelope with wax stamp, mailbox, letter icon, red notification dot |
| N08 | `ui_nav_battlepass.png` | 战令 | 文字"战令" | military badge with star emblem, battle pass, ranking tier, gold and red |
| N09 | `ui_nav_rank.png` | 排行 | 文字"排行" | trophy cup with number 1, leaderboard, champion crown, golden podium |

---

### #403 — 任务 & 成就图标（6张）

> 路径：`Resources/Sprites/UI/ui_quest_{id}.png`
> 尺寸：48×48px | Prompt 比例：`--ar 1:1`

| # | 文件名 | 类别 | Prompt 描述 |
|---|--------|------|-------------|
| Q01 | `ui_quest_battle.png` | 战斗类任务 | sword crossing shield, battle quest, combat mission, red and silver |
| Q02 | `ui_quest_build.png` | 建造类任务 | hammer and tower construction, build quest, crafting mission, orange |
| Q03 | `ui_quest_hero.png` | 英雄类任务 | hero star ascending, hero upgrade quest, leveling mission, blue star |
| Q04 | `ui_quest_social.png` | 社交类任务 | speech bubble with heart, social quest, sharing mission, pink and blue |
| Q05 | `ui_quest_daily.png` | 每日任务标题 | calendar with sunrise, daily reset, new day mission board, warm orange |
| Q06 | `ui_quest_achievement.png` | 成就标题 | golden trophy star with laurel wreath, achievement unlocked, champion emblem |

---

### #404 — 塔图鉴图标补充（6张，用于TowerCollectionSystem面板）

> 路径：`Resources/Sprites/UI/codex_tower_{id}.png`
> 尺寸：80×80px | Prompt 比例：`--ar 1:1`

| # | 文件名 | 塔 | Prompt 描述 |
|---|--------|-----|-------------|
| CT01 | `codex_tower_archer.png` | 箭塔图鉴 | wooden guard tower with archer, green canopy, bow and arrows rack |
| CT02 | `codex_tower_mage.png` | 法塔图鉴 | mystical crystal tower, purple arcane energy, floating rune stones |
| CT03 | `codex_tower_ice.png` | 冰塔图鉴 | frozen ice spire tower, frost crystal pinnacle, pale blue aurora |
| CT04 | `codex_tower_cannon.png` | 炮塔图鉴 | iron fortress cannon tower, military siege engine, smoke and gunpowder |
| CT05 | `codex_tower_poison.png` | 毒塔图鉴 | toxic swamp tower with green fumes, bubbling cauldron top, skull ornament |
| CT06 | `codex_tower_goldmine.png` | 金矿图鉴 | golden mine shaft tower, mining cart with gems, pickaxe and lantern |

---

### #405 — 通用 UI 装饰素材（3张）

> 路径：`Resources/Sprites/UI/ui_deco_{id}.png`

| # | 文件名 | 尺寸 | Prompt 描述 |
|---|--------|------|-------------|
| D01 | `ui_deco_red_dot.png` | 24×24 | small red notification circle dot, alert badge, bright red, subtle glow, `--ar 1:1` |
| D02 | `ui_deco_lock.png` | 48×48 | padlock icon, locked feature, grey iron lock, keyhole, `--ar 1:1` |
| D03 | `ui_deco_new_tag.png` | 64×32 | "NEW" ribbon banner tag, red and gold, small label, announcement badge, `--ar 2:1` |

---

## 三、Prompt 完整模板

### Midjourney 标准模板（所有图标通用）

```
/imagine prompt: [Description from table above],
hand-painted 2D game icon, thick black outline, vibrant colors,
clean design, transparent background, game-ready asset,
Kingdom Rush art style, chibi fantasy theme,
--ar 1:1 --style raw --v 6.1 --s 250
```

### 示例（技能图标 S01）

```
/imagine prompt: golden divine shield with holy light rays,
glowing barrier, white and gold energy, defensive stance icon,
hand-painted 2D game icon, thick black outline, vibrant colors,
clean design, transparent background, game-ready asset,
Kingdom Rush art style, chibi fantasy theme,
--ar 1:1 --style raw --v 6.1 --s 250
```

---

## 四、后处理规范

### 步骤

1. **去背景**：用 rembg 或 Photoshop 去除非透明背景
2. **统一尺寸**：按表格中的尺寸缩放（ImageMagick `convert -resize 96x96`）
3. **描边强化**：可选，用 Photoshop 描边效果加强轮廓
4. **PNG压缩**：`pngquant --quality=85-95 --strip *.png`
5. **文件命名**：严格按表格中的文件名
6. **放入目录**：全部放入 `Resources/Sprites/UI/` 目录

### 批处理脚本

```bash
#!/bin/bash
# process_func_icons.sh — 功能图标后处理

INPUT_DIR="./raw_icons"
OUTPUT_DIR="./Unity/AetheraSurvivors/Assets/Resources/Sprites/UI"

for img in "$INPUT_DIR"/*.png; do
    filename=$(basename "$img")
    
    # 去背景
    rembg i "$img" "/tmp/nobg_$filename"
    
    # 判断尺寸类别
    case "$filename" in
        skill_*|shop_*|codex_*)  SIZE="96x96" ;;
        ui_nav_*|ui_currency_*|roguelike_*) SIZE="64x64" ;;
        buff_*|ui_setting_*|ui_quest_*|ui_deco_lock*) SIZE="48x48" ;;
        ui_deco_red_dot*) SIZE="24x24" ;;
        ui_deco_new_tag*) SIZE="64x32" ;;
        *) SIZE="64x64" ;;
    esac
    
    # 缩放
    convert "/tmp/nobg_$filename" -resize "$SIZE" -gravity center -extent "$SIZE" "$OUTPUT_DIR/$filename"
    
    echo "✅ $filename → $SIZE"
done

# PNG压缩
cd "$OUTPUT_DIR"
pngquant --quality=85-95 --strip --ext .png --force *.png 2>/dev/null
echo "🎉 处理完成！共 $(ls -1 *.png | wc -l) 张图标"
```

---

## 五、资源目录放置总览

```
Resources/Sprites/UI/
├── 已有 ──────────────────
│   ├── ui_icon_coin.png            ← ✅ 已有
│   ├── ui_icon_heart.png           ← ✅ 已有
│   ├── ui_icon_wave_flag.png       ← ✅ 已有
│   ├── ui_btn_tower_*.png (×6)     ← ✅ 已有
│   ├── ui_icon_upgrade.png         ← ✅ 已有
│   ├── ui_icon_sell.png            ← ✅ 已有
│   ├── ui_icon_speedup.png         ← ✅ 已有
│   └── roguelike_*.png (×8)        ← ✅ 已有
│
├── #396 技能图标 ─────────
│   ├── skill_active_knight.png     ← 新增
│   ├── skill_active_archer.png
│   ├── skill_active_ice_witch.png
│   ├── skill_active_fire_mage.png
│   ├── skill_active_dwarf.png
│   ├── skill_active_chosen.png
│   ├── skill_passive_knight.png
│   ├── skill_passive_archer.png
│   ├── skill_passive_ice_witch.png
│   ├── skill_passive_fire_mage.png
│   ├── skill_passive_dwarf.png
│   └── skill_passive_chosen.png
│
├── #398 Buff图标 ─────────
│   ├── buff_slow.png               ← 新增
│   ├── buff_poison.png
│   ├── buff_freeze.png
│   ├── buff_burn.png
│   ├── buff_armor_reduce.png
│   ├── buff_stun.png
│   ├── buff_speed_up.png
│   ├── buff_heal.png
│   ├── buff_shield.png
│   └── buff_armor_up.png
│
├── #399 货币图标 ─────────
│   ├── ui_currency_diamond.png     ← 新增
│   ├── ui_currency_gold.png
│   ├── ui_currency_stamina.png
│   ├── ui_currency_exp_book.png
│   └── ui_currency_summon_ticket.png
│
├── #400 商品图标 ─────────
│   ├── shop_first_pay.png          ← 新增
│   ├── shop_month_card.png
│   ├── shop_week_card.png
│   ├── shop_diamond_small.png
│   ├── shop_diamond_medium.png
│   ├── shop_diamond_large.png
│   ├── shop_stamina.png
│   ├── shop_gold_bag.png
│   ├── shop_newbie_gift.png
│   └── shop_growth_gift.png
│
├── #401 设置图标 ─────────
│   ├── ui_setting_gear.png         ← 新增
│   ├── ui_setting_music.png
│   ├── ui_setting_sfx.png
│   ├── ui_setting_language.png
│   └── ui_setting_feedback.png
│
├── #402 导航图标 ─────────
│   ├── ui_nav_battle.png           ← 新增
│   ├── ui_nav_hero.png
│   ├── ui_nav_shop.png
│   ├── ui_nav_gacha.png
│   ├── ui_nav_social.png
│   ├── ui_nav_quest.png
│   ├── ui_nav_mail.png
│   ├── ui_nav_battlepass.png
│   └── ui_nav_rank.png
│
├── #403 任务&成就图标 ────
│   ├── ui_quest_battle.png         ← 新增
│   ├── ui_quest_build.png
│   ├── ui_quest_hero.png
│   ├── ui_quest_social.png
│   ├── ui_quest_daily.png
│   └── ui_quest_achievement.png
│
├── #404 塔图鉴图标 ───────
│   ├── codex_tower_archer.png      ← 新增
│   ├── codex_tower_mage.png
│   ├── codex_tower_ice.png
│   ├── codex_tower_cannon.png
│   ├── codex_tower_poison.png
│   └── codex_tower_goldmine.png
│
└── #405 通用装饰 ─────────
    ├── ui_deco_red_dot.png         ← 新增
    ├── ui_deco_lock.png
    └── ui_deco_new_tag.png
```

---

## 六、代码集成映射表

图标生成后需要在以下代码位置接入：

| 图标类别 | 代码文件 | 接入位置 | 当前占位 | 接入方式 |
|---------|---------|---------|---------|---------|
| 技能图标 | `HeroPanel.cs` L348-349 | 技能描述前的 emoji `🔴🔵` | `🔴 {name}: {desc}` | 新增 Image + `SpriteLoader.LoadUI()` |
| 技能图标 | `BattleUI.cs` | 战斗中英雄技能按钮（**尚未实现**） | 无按钮 | 需新增技能按钮区域 |
| Buff图标 | `EnemyVisualAnimator.cs` | 怪物头顶Buff指示器 | 纯颜色染色 | 可选：新增Buff图标悬浮显示 |
| 货币图标 | `MainMenuUI.cs` L189-198 | 顶部货币显示 | `◇` `G` `!` 文字 | 新增 Image + `SpriteLoader.LoadUI()` |
| 货币图标 | `MetaGamePanels.cs` 各面板 | 奖励显示 | `G{amount}` `◇{amount}` | 同上 |
| 商品图标 | `ShopPanel.cs` L202 | 商品名前 | `[礼] 📅 ◇ ! T G 🎀 📦` | 新增 Image + `ShopProduct.IconSprite` |
| 设置图标 | `MetaGamePanels.cs` L624-651 | 设置项前 | `⚙️ 🎵 🔊 🌐 📝` | 新增 Image |
| 导航图标 | `MainMenuUI.cs` 底部栏+功能按钮 | 按钮内 | 纯文字 | 新增 Image |
| 任务图标 | `MetaGamePanels.cs` QuestPanel | 任务列表标题 | `📋` | 新增 Image |
| 成就图标 | `CheckInPanel.cs` | 成就项前 | `☆ ■` | 新增 Image |
| 塔图鉴图标 | `TowerCollectionSystem.cs` L735 | 塔列表项 | `[弓] [法]` 文字 | 新增 Image |
| 装饰素材 | `RedDotManager.cs` | 红点标记 | 程序化红色圆 | 替换为真实PNG |

---

## 七、生产优先级

| 优先级 | 图标类别 | 数量 | 理由 |
|--------|---------|------|------|
| **P0** | 导航图标(#402) + 货币图标(#399) | 14张 | 主界面最显眼，纯文字最影响品质感 |
| **P1** | 商品图标(#400) + 设置图标(#401) | 15张 | 商城和设置是高频访问页面 |
| **P2** | 技能图标(#396-397) | 12张 | 英雄面板重要但不是首屏 |
| **P3** | Buff图标(#398) + 任务成就(#403) | 16张 | 战斗中视觉提升，但当前程序化方案可用 |
| **P4** | 塔图鉴(#404) + 装饰(#405) | 9张 | 优先级最低，图鉴非核心功能 |

**预估总工时**：~16小时（MJ生成~8小时 + 后处理~4小时 + 代码集成~4小时）
