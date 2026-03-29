#!/usr/bin/env python3
"""
第1-3章关卡手工调优脚本 — #422-430
精心设计新手教学章节的地图布局、波次配置、经济参数
覆盖 generate_levels.py 自动生成的数据

设计理念：
- 第1章：纯步兵，教会基本操作（放塔/升级/出售）
- 第2章：引入刺客（快速低HP），教会塔的选择策略  
- 第3章：引入骑士（高甲慢速），教会伤害类型
"""

import json
import os
import copy

# ============================================================
# 地图设计工具函数
# ============================================================

def make_grid(w, h, path_coords, tower_coords, obstacle_coords, spawn, base):
    """从坐标列表构建网格数据"""
    grid = [0] * (w * h)  # 全部初始化为空地
    
    path_set = set(path_coords)
    for (x, y) in path_coords:
        grid[y * w + x] = 1  # Path
    
    for (x, y) in tower_coords:
        if (x, y) not in path_set:
            grid[y * w + x] = 2  # TowerSlot
    
    for (x, y) in obstacle_coords:
        if (x, y) not in path_set and (x, y) not in set(tower_coords):
            grid[y * w + x] = 3  # Obstacle
    
    grid[spawn[1] * w + spawn[0]] = 4  # SpawnPoint
    grid[base[1] * w + base[0]] = 5    # BasePoint
    
    return grid


def make_wave(groups, interval=15, elite=False, boss=False, desc=""):
    """构建波次数据"""
    wave = {"groups": groups, "interval": interval, "desc": desc}
    if elite:
        wave["elite"] = True
    if boss:
        wave["boss"] = True
    return wave


def make_group(enemy_type, count, interval=1.0, hp=1.0, delay=0):
    """构建怪物组"""
    return {
        "type": enemy_type,
        "count": count,
        "interval": round(interval, 2),
        "hp": round(hp, 2),
        "delay": round(delay, 1)
    }


# ============================================================
# 第1章：绿野初阵（纯步兵，教学关卡）
# ============================================================

def chapter1_level1():
    """1-1: 第一步 — 最简单的直线路径，教放箭塔
    地图 10x6，Z形短路径，3波纯步兵，少量怪
    """
    w, h = 10, 6
    # 简单Z形路径：左→右→下→左→下→右
    path = [(0,1),(1,1),(2,1),(3,1),(4,1),(5,1),(6,1),(7,1),
            (7,2),(7,3),
            (6,3),(5,3),(4,3),(3,3),(2,3),(1,3),
            (1,4),
            (2,4),(3,4),(4,4),(5,4),(6,4),(7,4),(8,4),(9,4)]
    spawn = (0, 1)
    base = (9, 4)
    
    # 塔位：路径两侧，新手只需几个关键位置
    towers = [
        (3,0),(5,0),(7,0),  # 第一排上方
        (3,2),(5,2),        # 中间
        (3,5),(5,5),(7,5),  # 下方
        (0,3),(9,3),        # 两侧
    ]
    
    obstacles = [(9,0),(0,5)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 3, 1.5)], interval=20, desc="第1波 - 3个步兵"),
        make_wave([make_group(0, 5, 1.2)], interval=18, desc="第2波 - 5个步兵"),
        make_wave([make_group(0, 8, 1.0)], interval=0, desc="第3波 - 8个步兵"),
    ]
    
    return {
        "levelId": "chapter1_level1",
        "chapter": 1, "level": 1,
        "name": "初次防御",
        "description": "放置箭塔，阻止敌人！",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 200,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 80, "rewardExp": 30, "staminaCost": 5,
    }


def chapter1_level2():
    """1-2: 多点防御 — S形路径更长，教升级塔
    地图 11x7，S形路径，4波，怪物稍多
    """
    w, h = 11, 7
    # S形路径
    path = [(0,1),(1,1),(2,1),(3,1),(4,1),(5,1),(6,1),(7,1),(8,1),(9,1),
            (9,2),(9,3),
            (8,3),(7,3),(6,3),(5,3),(4,3),(3,3),(2,3),(1,3),
            (1,4),(1,5),
            (2,5),(3,5),(4,5),(5,5),(6,5),(7,5),(8,5),(9,5),(10,5)]
    spawn = (0, 1)
    base = (10, 5)
    
    towers = [
        (2,0),(4,0),(6,0),(8,0),    # 第一排上方
        (3,2),(5,2),(7,2),           # 中间上
        (3,4),(5,4),(7,4),           # 中间下
        (2,6),(4,6),(6,6),(8,6),     # 底部
        (0,3),(10,3),                # 两侧
    ]
    
    obstacles = [(10,0),(0,6),(10,1)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 4, 1.3)], interval=20, desc="第1波 - 小队来袭"),
        make_wave([make_group(0, 6, 1.1)], interval=18, desc="第2波 - 步兵增援"),
        make_wave([make_group(0, 5, 1.0), make_group(0, 4, 0.9, delay=3)], interval=16, desc="第3波 - 两路夹击"),
        make_wave([make_group(0, 8, 0.9, hp=1.1)], interval=0, desc="第4波 - 精锐步兵"),
    ]
    
    return {
        "levelId": "chapter1_level2",
        "chapter": 1, "level": 2,
        "name": "蜿蜒小道",
        "description": "路径更长了，试试升级你的塔！",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 220,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 90, "rewardExp": 35, "staminaCost": 5,
    }


def chapter1_level3():
    """1-3: 塔的选择 — 解锁法塔和冰塔的选项，教多种塔配合
    地图 12x7，U形路径，5波
    """
    w, h = 12, 7
    # U形路径
    path = [(0,1),(1,1),
            (1,2),(1,3),(1,4),(1,5),
            (2,5),(3,5),(4,5),(5,5),(6,5),(7,5),(8,5),(9,5),(10,5),
            (10,4),(10,3),(10,2),(10,1),
            (11,1)]
    spawn = (0, 1)
    base = (11, 1)
    
    towers = [
        (0,0),(2,0),(4,0),(6,0),(8,0),(11,0),  # 顶行
        (3,1),(5,1),(7,1),                       # 路径上方弯道内
        (0,3),(2,3),(4,3),(6,3),(8,3),           # 中间
        (2,4),(4,4),(6,4),(8,4),                  # 路径上方
        (3,6),(5,6),(7,6),(9,6),(11,6),          # 底行
    ]
    
    obstacles = [(0,6),(11,5),(6,1),(6,2)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 5, 1.2)], interval=20, desc="第1波 - 步兵先锋"),
        make_wave([make_group(0, 7, 1.0)], interval=18, desc="第2波 - 步兵主力"),
        make_wave([make_group(0, 6, 1.0), make_group(0, 4, 0.8, delay=4)], interval=16, desc="第3波 - 分批进攻"),
        make_wave([make_group(0, 8, 0.9, hp=1.2)], interval=15, desc="第4波 - 重装步兵", elite=True),
        make_wave([make_group(0, 10, 0.8, hp=1.0)], interval=0, desc="第5波 - 最后冲锋"),
    ]
    
    return {
        "levelId": "chapter1_level3",
        "chapter": 1, "level": 3,
        "name": "U形拐角",
        "description": "试试不同种类的塔！冰塔可以减速敌人。",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 250,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 100, "rewardExp": 40, "staminaCost": 5,
    }


def chapter1_level4():
    """1-4: 金矿试炼 — 教金矿和经济管理
    地图 12x8，螺旋路径（长路径=更多时间），6波+精英波
    """
    w, h = 12, 8
    # 螺旋路径（外→内）
    path = [(0,1),(1,1),(2,1),(3,1),(4,1),(5,1),(6,1),(7,1),(8,1),(9,1),(10,1),
            (10,2),(10,3),(10,4),(10,5),(10,6),
            (9,6),(8,6),(7,6),(6,6),(5,6),(4,6),(3,6),(2,6),(1,6),
            (1,5),(1,4),(1,3),
            (2,3),(3,3),(4,3),(5,3),(6,3),(7,3),(8,3),
            (8,4),
            (7,4),(6,4),(5,4),(4,4),(3,4)]
    spawn = (0, 1)
    base = (3, 4)
    
    towers = [
        (2,0),(4,0),(6,0),(8,0),         # 顶行
        (3,2),(5,2),(7,2),(9,2),         # 第一排下方
        (0,3),(0,5),(11,3),(11,5),       # 两侧
        (4,5),(6,5),(8,5),               # 路径内圈上方
        (2,7),(4,7),(6,7),(8,7),         # 底行
        (5,3),(5,4),                      # 内圈关键点
    ]
    
    obstacles = [(11,0),(11,7),(0,0),(0,7)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 5, 1.2)], interval=22, desc="第1波 - 侦察兵"),
        make_wave([make_group(0, 7, 1.0)], interval=20, desc="第2波 - 步兵中队"),
        make_wave([make_group(0, 6, 1.0), make_group(0, 5, 0.9, delay=4)], interval=18, desc="第3波 - 双路推进"),
        make_wave([make_group(0, 8, 0.9)], interval=16, desc="第4波 - 大队来袭"),
        make_wave([make_group(0, 6, 0.8, hp=1.4)], interval=15, desc="第5波 - 精锐重甲", elite=True),
        make_wave([make_group(0, 12, 0.7, hp=1.1)], interval=0, desc="第6波 - 人海冲锋"),
    ]
    
    return {
        "levelId": "chapter1_level4",
        "chapter": 1, "level": 4,
        "name": "螺旋围城",
        "description": "试试放置金矿赚取更多金币！",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 250,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 110, "rewardExp": 45, "staminaCost": 5,
    }


def chapter1_level5():
    """1-5: 章末Boss — 第一次遇到龙Boss
    地图 12x8，Z形长路径，7波+精英+Boss
    """
    w, h = 12, 8
    path = [(0,1),(1,1),(2,1),(3,1),(4,1),(5,1),(6,1),(7,1),(8,1),(9,1),(10,1),
            (10,2),(10,3),
            (9,3),(8,3),(7,3),(6,3),(5,3),(4,3),(3,3),(2,3),(1,3),
            (1,4),(1,5),
            (2,5),(3,5),(4,5),(5,5),(6,5),(7,5),(8,5),(9,5),(10,5),
            (10,6),
            (11,6)]
    spawn = (0, 1)
    base = (11, 6)
    
    towers = [
        (2,0),(4,0),(6,0),(8,0),         # 顶行
        (0,2),(3,2),(5,2),(7,2),         # 中上
        (3,4),(5,4),(7,4),(9,4),         # 中下
        (2,6),(4,6),(6,6),(8,6),         # 底行
        (0,5),(11,3),(11,5),             # 两侧
        (11,1),(0,4),                     # 边角
    ]
    
    obstacles = [(0,0),(11,0),(0,7),(6,2),(6,4)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 5, 1.2)], interval=20, desc="第1波 - 前锋"),
        make_wave([make_group(0, 8, 1.0)], interval=18, desc="第2波 - 步兵团"),
        make_wave([make_group(0, 6, 0.9), make_group(0, 5, 0.8, delay=4)], interval=16, desc="第3波 - 双路攻势"),
        make_wave([make_group(0, 10, 0.8)], interval=16, desc="第4波 - 大规模进攻"),
        make_wave([make_group(0, 8, 0.7, hp=1.3)], interval=15, desc="第5波 - 精锐先锋", elite=True),
        make_wave([make_group(0, 6, 0.8)], interval=15, desc="第6波 - 最后的步兵"),
        make_wave([make_group(0, 4, 1.0, hp=1.2), make_group(50, 1, 0, hp=1.0, delay=5)],
                  interval=0, desc="Boss来袭！炎龙降临！", boss=True),
    ]
    
    return {
        "levelId": "chapter1_level5",
        "chapter": 1, "level": 5,
        "name": "炎龙初现",
        "description": "小心！Boss正在接近基地！",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 280,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 130, "rewardExp": 55, "staminaCost": 5,
    }


# ============================================================
# 第2章：暗影入侵（引入刺客 type=1）
# ============================================================

def chapter2_level1():
    """2-1: 刺客登场 — 快速低HP敌人，教速射塔的重要性"""
    w, h = 11, 7
    path = [(0,3),(1,3),(2,3),(3,3),(4,3),(5,3),(6,3),(7,3),(8,3),(9,3),(10,3)]
    spawn = (0, 3)
    base = (10, 3)
    
    towers = [
        (2,1),(4,1),(6,1),(8,1),
        (1,2),(3,2),(5,2),(7,2),(9,2),
        (1,4),(3,4),(5,4),(7,4),(9,4),
        (2,5),(4,5),(6,5),(8,5),
    ]
    
    obstacles = [(0,0),(10,0),(0,6),(10,6)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 6, 1.1)], interval=20, desc="第1波 - 步兵巡逻"),
        make_wave([make_group(1, 4, 0.8)], interval=18, desc="第2波 - 刺客突袭！速度很快！"),
        make_wave([make_group(0, 5, 1.0), make_group(1, 3, 0.7, delay=3)], interval=16, desc="第3波 - 步兵掩护刺客"),
        make_wave([make_group(1, 6, 0.7, hp=1.1)], interval=15, desc="第4波 - 刺客小队"),
        make_wave([make_group(0, 8, 0.9), make_group(1, 5, 0.6, delay=5)], interval=0, desc="第5波 - 全面进攻"),
    ]
    
    return {
        "levelId": "chapter2_level1",
        "chapter": 2, "level": 1,
        "name": "暗影前哨",
        "description": "刺客速度很快！箭塔是你的好帮手。",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 240,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 100, "rewardExp": 40, "staminaCost": 6,
    }


def chapter2_level2():
    """2-2: 交叉火力 — L形路径，教集中火力点"""
    w, h = 12, 8
    path = [(0,6),(1,6),(2,6),(3,6),(4,6),(5,6),(6,6),(7,6),(8,6),(9,6),(10,6),
            (10,5),(10,4),(10,3),(10,2),(10,1),
            (11,1)]
    spawn = (0, 6)
    base = (11, 1)
    
    towers = [
        (2,5),(4,5),(6,5),(8,5),           # 路径上方
        (2,7),(4,7),(6,7),(8,7),           # 路径下方（仅底行）
        (9,2),(9,4),(11,3),(11,5),         # 拐角处集中
        (9,0),(11,0),                       # 顶部
        (0,5),(0,7),                        # 左侧
    ]
    
    obstacles = [(0,0),(5,0),(5,3)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 6, 1.1)], interval=20, desc="第1波 - 步兵纵队"),
        make_wave([make_group(1, 5, 0.7)], interval=18, desc="第2波 - 刺客渗透"),
        make_wave([make_group(0, 8, 1.0), make_group(1, 3, 0.6, delay=5)], interval=16, desc="第3波 - 混合编队"),
        make_wave([make_group(0, 6, 0.9, hp=1.2), make_group(1, 4, 0.7, hp=1.1)], interval=15, desc="第4波 - 强化混编"),
        make_wave([make_group(1, 8, 0.6, hp=1.2)], interval=14, desc="第5波 - 刺客突击队", elite=True),
        make_wave([make_group(0, 10, 0.8), make_group(1, 6, 0.6, delay=5)], interval=0, desc="第6波 - 最终冲锋"),
    ]
    
    return {
        "levelId": "chapter2_level2",
        "chapter": 2, "level": 2,
        "name": "L形走廊",
        "description": "在拐角处集中火力效果最好！",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 260,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 110, "rewardExp": 45, "staminaCost": 6,
    }


def chapter2_level3():
    """2-3: 迂回战术 — S形长路径，拉长战线"""
    w, h = 12, 8
    path = [(0,1),(1,1),(2,1),(3,1),(4,1),(5,1),(6,1),(7,1),(8,1),(9,1),(10,1),
            (10,2),(10,3),
            (9,3),(8,3),(7,3),(6,3),(5,3),(4,3),(3,3),(2,3),(1,3),
            (1,4),(1,5),
            (2,5),(3,5),(4,5),(5,5),(6,5),(7,5),(8,5),(9,5),(10,5),(11,5)]
    spawn = (0, 1)
    base = (11, 5)
    
    towers = [
        (2,0),(4,0),(6,0),(8,0),
        (3,2),(5,2),(7,2),(9,2),
        (0,3),(11,3),
        (3,4),(5,4),(7,4),(9,4),
        (2,6),(4,6),(6,6),(8,6),(10,6),
        (0,5),(11,4),
    ]
    
    obstacles = [(0,0),(11,0),(0,7),(11,7),(6,2)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 7, 1.0)], interval=20, desc="第1波 - 步兵大队"),
        make_wave([make_group(1, 5, 0.7), make_group(0, 4, 1.0, delay=4)], interval=18, desc="第2波 - 刺客先行"),
        make_wave([make_group(0, 8, 0.9)], interval=16, desc="第3波 - 步兵洪流"),
        make_wave([make_group(1, 7, 0.6, hp=1.1)], interval=16, desc="第4波 - 暗影刺客"),
        make_wave([make_group(0, 6, 0.8, hp=1.3), make_group(1, 4, 0.6, hp=1.2, delay=3)],
                  interval=15, desc="第5波 - 精锐混编", elite=True),
        make_wave([make_group(0, 10, 0.7), make_group(1, 6, 0.5, delay=6)], interval=0, desc="第6波 - 全线出击"),
    ]
    
    return {
        "levelId": "chapter2_level3",
        "chapter": 2, "level": 3,
        "name": "蜿蜒长途",
        "description": "利用长路径，布置多层防线！",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 270,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 120, "rewardExp": 50, "staminaCost": 6,
    }


def chapter2_level4():
    """2-4: 毒塔初试 — 教DOT伤害，对付密集敌人"""
    w, h = 12, 8
    # 螺旋路径
    path = [(0,1),(1,1),(2,1),(3,1),(4,1),(5,1),(6,1),(7,1),(8,1),(9,1),(10,1),
            (10,2),(10,3),(10,4),(10,5),(10,6),
            (9,6),(8,6),(7,6),(6,6),(5,6),(4,6),(3,6),(2,6),(1,6),
            (1,5),(1,4),(1,3),
            (2,3),(3,3),(4,3),(5,3),(6,3),(7,3),(8,3),
            (8,4),(7,4),(6,4)]
    spawn = (0, 1)
    base = (6, 4)
    
    towers = [
        (2,0),(4,0),(6,0),(8,0),
        (3,2),(5,2),(7,2),(9,2),(11,2),
        (0,3),(0,5),(11,4),(11,6),
        (3,5),(5,5),(7,5),(9,5),
        (2,7),(4,7),(6,7),(8,7),
        (4,4),(5,3),
    ]
    
    obstacles = [(0,0),(11,0),(0,7),(11,7)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 6, 1.0)], interval=20, desc="第1波 - 步兵"),
        make_wave([make_group(1, 5, 0.7)], interval=18, desc="第2波 - 刺客"),
        make_wave([make_group(0, 8, 0.8), make_group(1, 4, 0.6, delay=4)], interval=16, desc="第3波 - 混编"),
        make_wave([make_group(0, 10, 0.7)], interval=15, desc="第4波 - 密集步兵"),
        make_wave([make_group(0, 8, 0.7, hp=1.3), make_group(1, 5, 0.6, hp=1.2)],
                  interval=14, desc="第5波 - 精锐混编", elite=True),
        make_wave([make_group(1, 8, 0.5, hp=1.1), make_group(0, 6, 0.8, delay=5)], interval=13, desc="第6波 - 暗影大军"),
        make_wave([make_group(0, 5, 0.9, hp=1.2), make_group(50, 1, 0, hp=1.0, delay=6)],
                  interval=0, desc="Boss来袭！", boss=True),
    ]
    
    return {
        "levelId": "chapter2_level4",
        "chapter": 2, "level": 4,
        "name": "毒雾迷宫",
        "description": "毒塔对密集敌人非常有效！",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 280,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 130, "rewardExp": 55, "staminaCost": 6,
    }


def chapter2_level5():
    """2-5: 暗影之主 — Boss关，刺客大军+龙Boss"""
    w, h = 13, 8
    path = [(0,1),(1,1),(2,1),(3,1),(4,1),(5,1),(6,1),(7,1),(8,1),(9,1),(10,1),(11,1),
            (11,2),(11,3),
            (10,3),(9,3),(8,3),(7,3),(6,3),(5,3),(4,3),(3,3),(2,3),(1,3),
            (1,4),(1,5),
            (2,5),(3,5),(4,5),(5,5),(6,5),(7,5),(8,5),(9,5),(10,5),(11,5),
            (11,6),
            (12,6)]
    spawn = (0, 1)
    base = (12, 6)
    
    towers = [
        (2,0),(4,0),(6,0),(8,0),(10,0),
        (3,2),(5,2),(7,2),(9,2),
        (0,3),(12,3),(12,5),
        (3,4),(5,4),(7,4),(9,4),
        (2,6),(4,6),(6,6),(8,6),(10,6),
        (0,5),(12,4),
    ]
    
    obstacles = [(0,0),(12,0),(0,7),(6,2),(6,4)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 6, 1.0)], interval=20, desc="第1波 - 步兵前锋"),
        make_wave([make_group(1, 5, 0.7)], interval=18, desc="第2波 - 刺客侦察"),
        make_wave([make_group(0, 8, 0.9), make_group(1, 4, 0.6, delay=4)], interval=16, desc="第3波 - 混合攻势"),
        make_wave([make_group(1, 8, 0.6)], interval=15, desc="第4波 - 暗影潮"),
        make_wave([make_group(0, 10, 0.8), make_group(1, 6, 0.5, delay=5)], interval=14, desc="第5波 - 全面进攻"),
        make_wave([make_group(0, 8, 0.7, hp=1.3), make_group(1, 6, 0.5, hp=1.2)],
                  interval=13, desc="第6波 - 精锐突击", elite=True),
        make_wave([make_group(1, 6, 0.6, hp=1.1), make_group(0, 4, 1.0, hp=1.2, delay=3)], interval=12, desc="第7波 - 暗影卫队"),
        make_wave([make_group(0, 5, 1.0, hp=1.3), make_group(1, 3, 0.7, hp=1.2, delay=2),
                   make_group(50, 1, 0, hp=1.2, delay=8)],
                  interval=0, desc="暗影之主降临！", boss=True),
    ]
    
    return {
        "levelId": "chapter2_level5",
        "chapter": 2, "level": 5,
        "name": "暗影之主",
        "description": "暗影军团的首领来了！全力迎战！",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 300,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 150, "rewardExp": 65, "staminaCost": 6,
    }


# ============================================================
# 第3章：铁甲军团（引入骑士 type=2，高甲慢速）
# ============================================================

def chapter3_level1():
    """3-1: 铁甲先锋 — 骑士登场，教法塔打高甲"""
    w, h = 12, 7
    path = [(0,3),(1,3),(2,3),(3,3),(4,3),(5,3),(6,3),(7,3),(8,3),(9,3),(10,3),(11,3)]
    spawn = (0, 3)
    base = (11, 3)
    
    towers = [
        (1,1),(3,1),(5,1),(7,1),(9,1),
        (2,2),(4,2),(6,2),(8,2),(10,2),
        (2,4),(4,4),(6,4),(8,4),(10,4),
        (1,5),(3,5),(5,5),(7,5),(9,5),
    ]
    
    obstacles = [(0,0),(11,0),(0,6),(11,6)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 6, 1.0)], interval=20, desc="第1波 - 步兵"),
        make_wave([make_group(2, 3, 1.5)], interval=20, desc="第2波 - 铁甲骑士！物理攻击效果较差！"),
        make_wave([make_group(0, 6, 0.9), make_group(2, 2, 1.3, delay=4)], interval=18, desc="第3波 - 步兵+骑士"),
        make_wave([make_group(1, 5, 0.7), make_group(2, 3, 1.2)], interval=16, desc="第4波 - 刺客+骑士"),
        make_wave([make_group(2, 5, 1.2, hp=1.2)], interval=0, desc="第5波 - 铁甲纵队"),
    ]
    
    return {
        "levelId": "chapter3_level1",
        "chapter": 3, "level": 1,
        "name": "铁甲先锋",
        "description": "骑士护甲很高！法塔的魔法伤害更有效！",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 260,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 110, "rewardExp": 45, "staminaCost": 7,
    }


def chapter3_level2():
    """3-2: 三线协同 — 步兵/刺客/骑士混编"""
    w, h = 12, 8
    path = [(0,1),(1,1),(2,1),(3,1),(4,1),(5,1),(6,1),(7,1),(8,1),(9,1),(10,1),
            (10,2),(10,3),
            (9,3),(8,3),(7,3),(6,3),(5,3),(4,3),(3,3),(2,3),(1,3),
            (1,4),(1,5),
            (2,5),(3,5),(4,5),(5,5),(6,5),(7,5),(8,5),(9,5),(10,5),(11,5)]
    spawn = (0, 1)
    base = (11, 5)
    
    towers = [
        (2,0),(4,0),(6,0),(8,0),
        (3,2),(5,2),(7,2),(9,2),(11,2),
        (0,3),(11,4),
        (3,4),(5,4),(7,4),(9,4),
        (2,6),(4,6),(6,6),(8,6),(10,6),
    ]
    
    obstacles = [(0,0),(11,0),(0,7),(11,7),(6,2)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 7, 1.0)], interval=20, desc="第1波 - 步兵中队"),
        make_wave([make_group(1, 5, 0.7), make_group(2, 2, 1.5, delay=3)], interval=18, desc="第2波 - 刺客+骑士"),
        make_wave([make_group(0, 8, 0.9), make_group(1, 4, 0.6, delay=5)], interval=16, desc="第3波 - 步兵+刺客"),
        make_wave([make_group(2, 4, 1.2, hp=1.2)], interval=16, desc="第4波 - 重甲方阵"),
        make_wave([make_group(0, 6, 0.8, hp=1.2), make_group(1, 4, 0.6, hp=1.1), make_group(2, 3, 1.2, hp=1.2, delay=5)],
                  interval=14, desc="第5波 - 三兵种联合", elite=True),
        make_wave([make_group(0, 10, 0.7), make_group(2, 4, 1.0, delay=6)], interval=0, desc="第6波 - 铁壁冲锋"),
    ]
    
    return {
        "levelId": "chapter3_level2",
        "chapter": 3, "level": 2,
        "name": "三线协同",
        "description": "面对多种敌人，你需要搭配不同的塔！",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 280,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 120, "rewardExp": 50, "staminaCost": 7,
    }


def chapter3_level3():
    """3-3: 炮塔登场 — 教AOE对付密集敌群"""
    w, h = 13, 8
    # 长S形
    path = [(0,1),(1,1),(2,1),(3,1),(4,1),(5,1),(6,1),(7,1),(8,1),(9,1),(10,1),(11,1),
            (11,2),(11,3),
            (10,3),(9,3),(8,3),(7,3),(6,3),(5,3),(4,3),(3,3),(2,3),(1,3),
            (1,4),(1,5),
            (2,5),(3,5),(4,5),(5,5),(6,5),(7,5),(8,5),(9,5),(10,5),(11,5),
            (11,6),
            (12,6)]
    spawn = (0, 1)
    base = (12, 6)
    
    towers = [
        (2,0),(4,0),(6,0),(8,0),(10,0),
        (3,2),(5,2),(7,2),(9,2),(12,2),
        (0,3),(12,4),
        (3,4),(5,4),(7,4),(9,4),
        (2,6),(4,6),(6,6),(8,6),(10,6),
        (0,5),
    ]
    
    obstacles = [(0,0),(12,0),(0,7),(6,2)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 8, 0.9)], interval=20, desc="第1波 - 步兵大队"),
        make_wave([make_group(1, 6, 0.6), make_group(0, 5, 0.9, delay=4)], interval=18, desc="第2波 - 快慢夹击"),
        make_wave([make_group(2, 4, 1.2)], interval=18, desc="第3波 - 铁甲方阵"),
        make_wave([make_group(0, 12, 0.6)], interval=16, desc="第4波 - 密集步兵，试试炮塔！"),
        make_wave([make_group(0, 8, 0.7, hp=1.2), make_group(2, 3, 1.1, hp=1.2, delay=4)],
                  interval=15, desc="第5波 - 精锐联合", elite=True),
        make_wave([make_group(1, 8, 0.5, hp=1.1), make_group(2, 4, 1.0, hp=1.1, delay=5)],
                  interval=14, desc="第6波 - 闪电突袭"),
        make_wave([make_group(0, 10, 0.7), make_group(1, 5, 0.5, delay=4), make_group(2, 3, 1.0, delay=8)],
                  interval=0, desc="第7波 - 全军出击"),
    ]
    
    return {
        "levelId": "chapter3_level3",
        "chapter": 3, "level": 3,
        "name": "密集阵线",
        "description": "敌人挤在一起？炮塔的范围伤害正合适！",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 300,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 130, "rewardExp": 55, "staminaCost": 7,
    }


def chapter3_level4():
    """3-4: 铁壁防线 — 大量骑士+精英波"""
    w, h = 13, 8
    path = [(0,1),(1,1),(2,1),(3,1),(4,1),(5,1),(6,1),(7,1),(8,1),(9,1),(10,1),(11,1),
            (11,2),(11,3),(11,4),(11,5),(11,6),
            (10,6),(9,6),(8,6),(7,6),(6,6),(5,6),(4,6),(3,6),(2,6),(1,6),
            (1,5),(1,4),(1,3),
            (2,3),(3,3),(4,3),(5,3),(6,3),(7,3),(8,3),(9,3),
            (9,4),(8,4),(7,4),(6,4)]
    spawn = (0, 1)
    base = (6, 4)
    
    towers = [
        (2,0),(4,0),(6,0),(8,0),(10,0),
        (3,2),(5,2),(7,2),(9,2),(12,2),
        (0,3),(12,4),(0,5),
        (3,5),(5,5),(7,5),(9,5),
        (2,7),(4,7),(6,7),(8,7),(10,7),
        (4,4),(5,4),(5,3),
    ]
    
    obstacles = [(0,0),(12,0),(0,7),(12,7)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 7, 1.0)], interval=20, desc="第1波 - 步兵"),
        make_wave([make_group(2, 4, 1.3), make_group(1, 3, 0.7, delay=4)], interval=18, desc="第2波 - 骑士+刺客"),
        make_wave([make_group(0, 10, 0.7)], interval=16, desc="第3波 - 步兵洪流"),
        make_wave([make_group(2, 6, 1.1, hp=1.2)], interval=16, desc="第4波 - 重甲军团"),
        make_wave([make_group(1, 8, 0.5, hp=1.1)], interval=15, desc="第5波 - 暗影突袭"),
        make_wave([make_group(2, 5, 1.0, hp=1.3), make_group(0, 6, 0.8, hp=1.2, delay=4)],
                  interval=14, desc="第6波 - 精锐铁壁", elite=True),
        make_wave([make_group(0, 8, 0.7), make_group(1, 5, 0.5, delay=3), make_group(2, 4, 1.0, delay=6)],
                  interval=13, desc="第7波 - 三线合攻"),
        make_wave([make_group(2, 4, 1.2, hp=1.3), make_group(0, 5, 0.9, hp=1.2, delay=3),
                   make_group(50, 1, 0, hp=1.3, delay=8)],
                  interval=0, desc="铁甲Boss来袭！", boss=True),
    ]
    
    return {
        "levelId": "chapter3_level4",
        "chapter": 3, "level": 4,
        "name": "铁壁防线",
        "description": "大量骑士来袭！法塔和炮塔是关键！",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 320,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 140, "rewardExp": 60, "staminaCost": 7,
    }


def chapter3_level5():
    """3-5: 钢铁风暴 — 章末Boss，全兵种+强化龙Boss"""
    w, h = 14, 8
    path = [(0,1),(1,1),(2,1),(3,1),(4,1),(5,1),(6,1),(7,1),(8,1),(9,1),(10,1),(11,1),(12,1),
            (12,2),(12,3),
            (11,3),(10,3),(9,3),(8,3),(7,3),(6,3),(5,3),(4,3),(3,3),(2,3),(1,3),
            (1,4),(1,5),
            (2,5),(3,5),(4,5),(5,5),(6,5),(7,5),(8,5),(9,5),(10,5),(11,5),(12,5),
            (12,6),
            (13,6)]
    spawn = (0, 1)
    base = (13, 6)
    
    towers = [
        (2,0),(4,0),(6,0),(8,0),(10,0),(12,0),
        (3,2),(5,2),(7,2),(9,2),(11,2),
        (0,3),(13,3),(13,5),
        (3,4),(5,4),(7,4),(9,4),(11,4),
        (2,6),(4,6),(6,6),(8,6),(10,6),
        (0,5),(13,4),
    ]
    
    obstacles = [(0,0),(13,0),(0,7),(13,7),(7,2)]
    
    grid = make_grid(w, h, path, towers, obstacles, spawn, base)
    
    waves = [
        make_wave([make_group(0, 8, 1.0)], interval=20, desc="第1波 - 步兵先锋"),
        make_wave([make_group(1, 6, 0.6)], interval=18, desc="第2波 - 刺客渗透"),
        make_wave([make_group(2, 4, 1.2), make_group(0, 6, 0.9, delay=4)], interval=16, desc="第3波 - 铁甲前军"),
        make_wave([make_group(0, 10, 0.7), make_group(1, 6, 0.5, delay=5)], interval=15, desc="第4波 - 人海战术"),
        make_wave([make_group(2, 6, 1.0, hp=1.2)], interval=15, desc="第5波 - 重甲推进"),
        make_wave([make_group(0, 8, 0.7, hp=1.2), make_group(1, 5, 0.5, hp=1.1, delay=3),
                   make_group(2, 3, 1.0, hp=1.3, delay=6)],
                  interval=14, desc="第6波 - 精锐联军", elite=True),
        make_wave([make_group(1, 8, 0.5, hp=1.2), make_group(2, 5, 1.0, hp=1.2, delay=5)],
                  interval=13, desc="第7波 - 暗影铁壁"),
        make_wave([make_group(0, 6, 0.8), make_group(2, 3, 1.0, hp=1.2, delay=3)], interval=12, desc="第8波 - 最后的步兵"),
        make_wave([make_group(2, 4, 1.2, hp=1.4), make_group(1, 4, 0.6, hp=1.2, delay=3),
                   make_group(50, 1, 0, hp=1.5, delay=10)],
                  interval=0, desc="钢铁风暴！炎龙咆哮！", boss=True),
    ]
    
    return {
        "levelId": "chapter3_level5",
        "chapter": 3, "level": 5,
        "name": "钢铁风暴",
        "description": "三大军团齐聚！这是最终考验！",
        "mapWidth": w, "mapHeight": h,
        "gridData": grid,
        "pathX": [p[0] for p in path],
        "pathY": [p[1] for p in path],
        "spawnX": spawn[0], "spawnY": spawn[1],
        "baseX": base[0], "baseY": base[1],
        "startGold": 340,
        "baseHP": 20,
        "waves": waves,
        "rewardGold": 160, "rewardExp": 70, "staminaCost": 7,
    }


# ============================================================
# 主程序：替换 level_config.json 中前3章的数据
# ============================================================

def main():
    config_path = os.path.join(os.path.dirname(__file__), "..",
        "Unity", "AetheraSurvivors", "Assets", "Resources", "Configs", "Levels", "level_config.json")
    
    # 读取现有配置
    with open(config_path, "r", encoding="utf-8") as f:
        data = json.load(f)
    
    # 生成手工调优的前3章数据
    tuned_levels = [
        chapter1_level1(), chapter1_level2(), chapter1_level3(), chapter1_level4(), chapter1_level5(),
        chapter2_level1(), chapter2_level2(), chapter2_level3(), chapter2_level4(), chapter2_level5(),
        chapter3_level1(), chapter3_level2(), chapter3_level3(), chapter3_level4(), chapter3_level5(),
    ]
    
    # 建立索引
    tuned_map = {lv["levelId"]: lv for lv in tuned_levels}
    
    # 替换
    replaced = 0
    for i, lv in enumerate(data["levels"]):
        if lv["levelId"] in tuned_map:
            data["levels"][i] = tuned_map[lv["levelId"]]
            replaced += 1
    
    # 写回
    with open(config_path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    
    print(f"✅ 第1-3章关卡调优完成！替换了 {replaced} 个关卡")
    print()
    
    # 打印摘要
    for lv in tuned_levels:
        w_count = len(lv["waves"])
        path_len = len(lv["pathX"])
        total_enemies = sum(g["count"] for w in lv["waves"] for g in w["groups"])
        types = sorted(set(g["type"] for w in lv["waves"] for g in w["groups"]))
        has_boss = any(w.get("boss") for w in lv["waves"])
        has_elite = any(w.get("elite") for w in lv["waves"])
        flags = ""
        if has_elite: flags += " [精英]"
        if has_boss: flags += " [Boss]"
        print(f"  {lv['levelId']:20s} {lv['mapWidth']:2d}x{lv['mapHeight']} "
              f"路径{path_len:2d}格 {w_count}波 怪{total_enemies:3d}只 "
              f"金{lv['startGold']} HP{lv['baseHP']} "
              f"种={types}{flags}")
        print(f"    «{lv['name']}» {lv['description']}")


if __name__ == "__main__":
    main()
