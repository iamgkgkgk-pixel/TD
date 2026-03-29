#!/usr/bin/env python3
"""
关卡批量生成工具 — #421
基于模板+随机参数生成所有章节关卡的JSON配置
支持：30章×5关 = 150关

设计原则：
1. 章节越高，地图越大、路径越长、塔位越多
2. 每章5关递进：关1简单 → 关5困难（怪物多+精英+Boss）
3. 路径形状多样：Z形、S形、U形、L形、螺旋
4. 每章有主题地形（通过chapter字段关联）

输出：Resources/Configs/Levels/level_config.json
"""

import json
import random
import os
import sys

# 路径形状模板
# 每个模板定义路径点的生成规则（相对于地图尺寸的百分比）
PATH_TEMPLATES = {
    "Z_shape": lambda w, h: generate_z_path(w, h),
    "S_shape": lambda w, h: generate_s_path(w, h),
    "U_shape": lambda w, h: generate_u_path(w, h),
    "L_shape": lambda w, h: generate_l_path(w, h),
    "spiral":  lambda w, h: generate_spiral_path(w, h),
}

def generate_z_path(w, h):
    """Z形路径：左→右→左→右"""
    path = []
    y1 = 1
    y2 = h // 2
    y3 = h - 2
    
    # 行1：左→右
    for x in range(0, w - 1):
        path.append((x, y1))
    # 连接列：右侧下行
    for y in range(y1, y2 + 1):
        path.append((w - 2, y))
    # 行2：右→左
    for x in range(w - 2, 1, -1):
        path.append((x, y2))
    # 连接列：左侧下行
    for y in range(y2, y3 + 1):
        path.append((1, y))
    # 行3：左→右
    for x in range(1, w):
        path.append((x, y3))
    
    return path

def generate_s_path(w, h):
    """S形路径：蜿蜒3排"""
    path = []
    rows = [1, h // 2, h - 2]
    
    for i, y in enumerate(rows):
        if i % 2 == 0:
            xs = range(1, w - 1)
        else:
            xs = range(w - 2, 0, -1)
        for x in xs:
            path.append((x, y))
        if i < len(rows) - 1:
            # 连接到下一行
            next_y = rows[i + 1]
            step = 1 if next_y > y else -1
            conn_x = (w - 2) if i % 2 == 0 else 1
            for cy in range(y + step, next_y, step):
                path.append((conn_x, cy))
    return path

def generate_u_path(w, h):
    """U形路径：左下→上→右→下"""
    path = []
    # 左列上行
    for y in range(1, h - 1):
        path.append((1, y))
    # 顶部横行
    for x in range(1, w - 1):
        path.append((x, h - 2))
    # 右列下行
    for y in range(h - 2, 0, -1):
        path.append((w - 2, y))
    return path

def generate_l_path(w, h):
    """L形路径：上→下→右"""
    path = []
    # 左列下行
    for y in range(h - 2, 1, -1):
        path.append((1, y))
    # 底部横行
    for x in range(1, w - 1):
        path.append((x, 1))
    return path

def generate_spiral_path(w, h):
    """螺旋路径：外→内，每步只走1格确保连续"""
    path = []
    x1, y1 = 1, 1
    x2, y2 = w - 2, h - 2
    
    while x1 <= x2 and y1 <= y2:
        # 右
        for x in range(x1, x2 + 1):
            path.append((x, y1))
        y1 += 1
        # 上
        for y in range(y1, y2 + 1):
            path.append((x2, y))
        x2 -= 1
        if y1 <= y2:
            # 左
            for x in range(x2, x1 - 1, -1):
                path.append((x, y2))
            y2 -= 1
        if x1 <= x2:
            # 下
            for y in range(y2, y1 - 1, -1):
                path.append((x1, y))
            x1 += 1
    return path


def fix_path_breaks(path):
    """修复路径中的断点：在不相邻的两点之间插入直线连接"""
    if len(path) < 2:
        return path
    
    fixed = [path[0]]
    for i in range(1, len(path)):
        prev = fixed[-1]
        curr = path[i]
        dx = curr[0] - prev[0]
        dy = curr[1] - prev[1]
        
        # 如果曼哈顿距离>1，需要插入中间点
        if abs(dx) + abs(dy) > 1:
            # 先走x方向，再走y方向
            step_x = 1 if dx > 0 else -1 if dx < 0 else 0
            step_y = 1 if dy > 0 else -1 if dy < 0 else 0
            
            cx, cy = prev
            # 先水平
            while cx != curr[0]:
                cx += step_x
                if (cx, cy) != curr:
                    fixed.append((cx, cy))
            # 再垂直
            while cy != curr[1]:
                cy += step_y
                if (cx, cy) != curr:
                    fixed.append((cx, cy))
        
        fixed.append(curr)
    
    return fixed


def place_tower_slots(grid, w, h, path_set, num_slots, rng):
    """在路径两侧放置塔位"""
    candidates = []
    directions = [(0, 1), (0, -1), (1, 0), (-1, 0)]
    
    for (px, py) in path_set:
        for dx, dy in directions:
            nx, ny = px + dx, py + dy
            if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in path_set:
                idx = ny * w + nx
                if grid[idx] == 0:  # Empty
                    candidates.append((nx, ny))
    
    # 去重
    candidates = list(set(candidates))
    rng.shuffle(candidates)
    
    placed = 0
    for (tx, ty) in candidates[:num_slots]:
        idx = ty * w + tx
        grid[idx] = 2  # TowerSlot
        placed += 1
    
    return placed


def generate_waves(chapter, level, rng):
    """根据章节和关卡生成波次配置"""
    # 基础参数随章节递增
    base_waves = min(5 + (chapter - 1) // 3, 12)  # 5~12波
    num_waves = base_waves + (level - 1)  # 关卡内递增
    num_waves = min(num_waves, 15)
    
    # 难度系数
    difficulty = chapter * 0.15 + level * 0.05
    hp_mult = 1.0 + (chapter - 1) * 0.12 + (level - 1) * 0.03
    
    # 可用怪物类型（随章节解锁）
    enemy_pool = [0]  # 步兵始终可用
    if chapter >= 2: enemy_pool.append(1)   # 刺客
    if chapter >= 3: enemy_pool.append(2)   # 骑士
    if chapter >= 4: enemy_pool.append(3)   # 飞行
    if chapter >= 5: enemy_pool.append(10)  # 治疗
    if chapter >= 6: enemy_pool.append(11)  # 史莱姆
    if chapter >= 8: enemy_pool.append(12)  # 盗贼
    if chapter >= 10: enemy_pool.append(13) # 盾法师
    
    boss_pool = [50]  # 龙Boss
    if chapter >= 15: boss_pool.append(51)  # 巨人Boss
    
    waves = []
    for wi in range(num_waves):
        is_last = (wi == num_waves - 1)
        is_elite = (wi == num_waves - 2) and num_waves > 3
        is_boss = is_last and level >= 4  # 每章第4-5关有Boss
        
        groups = []
        
        if is_boss:
            # Boss波：少量护卫 + Boss
            guard_type = rng.choice(enemy_pool)
            guard_count = 2 + chapter // 5
            groups.append({
                "type": guard_type,
                "count": guard_count,
                "interval": 0.8,
                "hp": round(hp_mult, 2),
                "delay": 0
            })
            groups.append({
                "type": rng.choice(boss_pool),
                "count": 1,
                "interval": 0,
                "hp": round(hp_mult * 1.5, 2),
                "delay": 3.0
            })
        elif is_elite:
            # 精英波：混合小队，HP加成
            num_groups = rng.randint(2, 3)
            for gi in range(num_groups):
                etype = rng.choice(enemy_pool)
                count = rng.randint(2, 4 + chapter // 3)
                groups.append({
                    "type": etype,
                    "count": count,
                    "interval": round(rng.uniform(0.7, 1.2), 2),
                    "hp": round(hp_mult * 1.3, 2),
                    "delay": round(gi * 2.0, 1)
                })
        else:
            # 普通波
            num_groups = rng.randint(1, 2)
            for gi in range(num_groups):
                etype = rng.choice(enemy_pool)
                base_count = 3 + wi + chapter // 2
                count = rng.randint(max(2, base_count - 2), base_count + 1)
                groups.append({
                    "type": etype,
                    "count": count,
                    "interval": round(rng.uniform(0.8, 1.5), 2),
                    "hp": round(hp_mult, 2),
                    "delay": round(gi * 1.5, 1)
                })
        
        wave = {
            "groups": groups,
            "interval": 15 if not is_last else 0,
            "desc": f"Wave {wi + 1}"
        }
        if is_elite:
            wave["elite"] = True
            wave["desc"] = f"精英波 {wi + 1}!"
        if is_boss:
            wave["boss"] = True
            wave["desc"] = f"BOSS来袭!"
            wave["interval"] = 0
        
        waves.append(wave)
    
    return waves


def generate_level(chapter, level, rng):
    """生成单个关卡配置"""
    # 地图尺寸随章节增长
    base_w = 12 + min((chapter - 1) // 5, 4) * 2  # 12~20
    base_h = 8 + min((chapter - 1) // 5, 3) * 2   # 8~14
    w = base_w + rng.choice([-1, 0, 0, 1])
    h = base_h + rng.choice([-1, 0, 0, 1])
    
    # 选择路径模板
    templates = list(PATH_TEMPLATES.keys())
    template_name = templates[(chapter * 7 + level * 3) % len(templates)]
    
    # 生成路径
    raw_path = PATH_TEMPLATES[template_name](w, h)
    
    # 确保路径合法
    path = []
    seen = set()
    for (px, py) in raw_path:
        px = max(0, min(w - 1, px))
        py = max(0, min(h - 1, py))
        if (px, py) not in seen:
            path.append((px, py))
            seen.add((px, py))
    
    # 修复路径断点（插入中间连接点）
    path = fix_path_breaks(path)
    # 再次去重
    deduped = []
    seen2 = set()
    for p in path:
        if p not in seen2:
            deduped.append(p)
            seen2.add(p)
    path = deduped
    
    if len(path) < 5:
        # 回退到简单直线
        path = [(x, h // 2) for x in range(w)]
        seen = set(path)
    
    # 生成网格
    grid = [0] * (w * h)
    
    # 放路径
    path_set = set()
    for (px, py) in path:
        idx = py * w + px
        if 0 <= idx < len(grid):
            grid[idx] = 1  # Path
            path_set.add((px, py))
    
    # 出生点和基地
    spawn = path[0]
    base = path[-1]
    grid[spawn[1] * w + spawn[0]] = 4  # SpawnPoint
    grid[base[1] * w + base[0]] = 5    # BasePoint
    
    # 塔位数量
    num_slots = 10 + chapter // 2 + level
    placed = place_tower_slots(grid, w, h, path_set, num_slots, rng)
    
    # 添加一些障碍物（空地的5%）
    for idx in range(len(grid)):
        if grid[idx] == 0 and rng.random() < 0.05:
            grid[idx] = 3  # Obstacle
    
    # 波次
    waves = generate_waves(chapter, level, rng)
    
    # 经济参数
    start_gold = 300 + chapter * 20 + level * 10
    base_hp = max(30, 60 - chapter + (5 - level) * 2)
    stamina_cost = 6 + (chapter - 1)
    reward_gold = 80 + chapter * 15 + level * 5
    reward_exp = 40 + chapter * 10 + level * 5
    
    return {
        "levelId": f"chapter{chapter}_level{level}",
        "chapter": chapter,
        "level": level,
        "name": f"第{chapter}章-{level}",
        "description": f"章节{chapter} 关卡{level}",
        "mapWidth": w,
        "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0],
        "spawnY": spawn[1],
        "baseX": base[0],
        "baseY": base[1],
        "startGold": start_gold,
        "baseHP": base_hp,
        "waves": waves,
        "rewardGold": reward_gold,
        "rewardExp": reward_exp,
        "staminaCost": stamina_cost,
    }


def main():
    output_dir = os.path.join(os.path.dirname(__file__), "..",
        "Unity", "AetheraSurvivors", "Assets", "Resources", "Configs", "Levels")
    os.makedirs(output_dir, exist_ok=True)
    
    seed = 42  # 固定种子确保可重现
    rng = random.Random(seed)
    
    max_chapters = 30
    levels_per_chapter = 5
    
    # 如果命令行传了参数，只生成前N章
    if len(sys.argv) > 1:
        max_chapters = int(sys.argv[1])
    
    all_levels = []
    
    for chapter in range(1, max_chapters + 1):
        for level in range(1, levels_per_chapter + 1):
            config = generate_level(chapter, level, rng)
            all_levels.append(config)
    
    # 写入JSON
    output = {"levels": all_levels}
    output_path = os.path.join(output_dir, "level_config.json")
    
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(output, f, ensure_ascii=False, indent=2)
    
    print(f"✅ 关卡配置生成完成！")
    print(f"   章节数: {max_chapters}")
    print(f"   每章关卡: {levels_per_chapter}")
    print(f"   总关卡数: {len(all_levels)}")
    print(f"   输出路径: {output_path}")
    print(f"   文件大小: {os.path.getsize(output_path) / 1024:.1f} KB")
    
    # 打印前3关摘要
    for lv in all_levels[:3]:
        w_count = len(lv["waves"])
        path_len = len(lv["pathX"])
        print(f"   [{lv['levelId']}] {lv['mapWidth']}x{lv['mapHeight']} "
              f"路径{path_len}格 {w_count}波 金币{lv['startGold']} HP{lv['baseHP']}")


if __name__ == "__main__":
    main()
