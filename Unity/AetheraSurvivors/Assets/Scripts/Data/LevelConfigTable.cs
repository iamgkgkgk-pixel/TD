// ============================================================
// 文件名：LevelConfigTable.cs
// 功能描述：关卡配置表 — 定义所有关卡的地图+波次数据
//          从 Resources/Configs/Levels/ 加载JSON配置
//          支持程序化生成 + 手动微调
// 创建时间：2026-03-29
// 所属模块：Data
// 对应交互：#421
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Battle.Map;
using AetheraSurvivors.Battle.Wave;
using AetheraSurvivors.Battle.Enemy;

namespace AetheraSurvivors.Data
{
    // ====================================================================
    // 关卡配置数据结构
    // ====================================================================

    /// <summary>
    /// 单个关卡的完整配置（地图+波次+奖励）
    /// </summary>
    [Serializable]
    public class LevelConfig
    {
        /// <summary>关卡ID（格式：chapter{c}_level{l}）</summary>
        public string levelId;
        /// <summary>章节号（1-30）</summary>
        public int chapter;
        /// <summary>关卡序号（1-5）</summary>
        public int level;
        /// <summary>关卡名称</summary>
        public string name;
        /// <summary>关卡描述</summary>
        public string description;

        // ---- 地图配置 ----
        /// <summary>地图宽度</summary>
        public int mapWidth = 12;
        /// <summary>地图高度</summary>
        public int mapHeight = 8;
        /// <summary>地图网格数据（一维数组，行优先）</summary>
        public int[] gridData;
        /// <summary>路径点X坐标列表</summary>
        public int[] pathX;
        /// <summary>路径点Y坐标列表</summary>
        public int[] pathY;
        /// <summary>出生点X</summary>
        public int spawnX;
        /// <summary>出生点Y</summary>
        public int spawnY;
        /// <summary>基地X</summary>
        public int baseX;
        /// <summary>基地Y</summary>
        public int baseY;

        // ---- 波次配置 ----
        /// <summary>初始金币</summary>
        public int startGold = 300;
        /// <summary>基地HP</summary>
        public int baseHP = 50;
        /// <summary>波次列表</summary>
        public List<WaveConfigData> waves;

        // ---- 奖励配置 ----
        /// <summary>通关金币奖励</summary>
        public int rewardGold = 100;
        /// <summary>通关经验奖励</summary>
        public int rewardExp = 50;
        /// <summary>体力消耗</summary>
        public int staminaCost = 6;

        /// <summary>转换为运行时LevelMapData</summary>
        public LevelMapData ToMapData()
        {
            var pathPoints = new List<Vector2Int>();
            if (pathX != null && pathY != null)
            {
                int count = Mathf.Min(pathX.Length, pathY.Length);
                for (int i = 0; i < count; i++)
                    pathPoints.Add(new Vector2Int(pathX[i], pathY[i]));
            }

            return new LevelMapData
            {
                levelId = levelId,
                chapter = chapter,
                levelIndex = level,
                width = mapWidth,
                height = mapHeight,
                gridData = gridData,
                pathPoints = pathPoints,
                spawnPoint = new Vector2Int(spawnX, spawnY),
                basePoint = new Vector2Int(baseX, baseY),
                description = description
            };
        }

        /// <summary>转换为运行时LevelWaveData</summary>
        public LevelWaveData ToWaveData()
        {
            var waveConfigs = new List<WaveConfig>();
            if (waves != null)
            {
                for (int i = 0; i < waves.Count; i++)
                {
                    waveConfigs.Add(waves[i].ToWaveConfig(i + 1));
                }
            }

            return new LevelWaveData
            {
                levelId = levelId,
                startGold = startGold,
                baseHP = baseHP,
                waves = waveConfigs
            };
        }
    }

    /// <summary>
    /// 波次配置数据（JSON序列化用，比WaveConfig更简洁）
    /// </summary>
    [Serializable]
    public class WaveConfigData
    {
        /// <summary>怪物组</summary>
        public List<WaveGroupData> groups;
        /// <summary>波次间等待时间</summary>
        public float interval = 15f;
        /// <summary>是否精英波</summary>
        public bool elite;
        /// <summary>是否Boss波</summary>
        public bool boss;
        /// <summary>描述</summary>
        public string desc = "";

        public WaveConfig ToWaveConfig(int index)
        {
            var wc = new WaveConfig
            {
                waveIndex = index,
                intervalAfterWave = interval,
                isEliteWave = elite,
                isBossWave = boss,
                description = desc,
                groups = new List<WaveGroup>()
            };

            if (groups != null)
            {
                foreach (var g in groups)
                    wc.groups.Add(g.ToWaveGroup());
            }

            return wc;
        }
    }

    /// <summary>
    /// 怪物组数据（JSON序列化用）
    /// </summary>
    [Serializable]
    public class WaveGroupData
    {
        /// <summary>怪物类型ID（0=步兵,1=刺客,2=骑士,3=飞行,10=治疗,11=史莱姆,50=龙Boss）</summary>
        public int type;
        /// <summary>数量</summary>
        public int count = 3;
        /// <summary>生成间隔</summary>
        public float interval = 1f;
        /// <summary>HP倍率</summary>
        public float hp = 1f;
        /// <summary>延迟</summary>
        public float delay;

        public WaveGroup ToWaveGroup()
        {
            return new WaveGroup
            {
                enemyType = (EnemyType)type,
                count = count,
                spawnInterval = interval,
                hpMultiplier = hp,
                startDelay = delay
            };
        }
    }

    /// <summary>
    /// 关卡配置表（包含所有关卡）
    /// </summary>
    [Serializable]
    public class LevelConfigTable
    {
        /// <summary>所有关卡配置</summary>
        public List<LevelConfig> levels;

        /// <summary>根据章节和关卡号查找配置</summary>
        public LevelConfig Find(int chapter, int level)
        {
            if (levels == null) return null;
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].chapter == chapter && levels[i].level == level)
                    return levels[i];
            }
            return null;
        }

        /// <summary>根据levelId查找</summary>
        public LevelConfig FindById(string levelId)
        {
            if (levels == null) return null;
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].levelId == levelId)
                    return levels[i];
            }
            return null;
        }
    }
}
