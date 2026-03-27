// ============================================================
// 文件名：GachaSystem.cs
// 功能描述：抽卡/召唤系统 — 单抽/十连、保底机制、概率展示
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #261-262
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Data;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>
    /// 抽卡结果
    /// </summary>
    [Serializable]
    public class GachaResult
    {
        public string HeroId;
        public HeroRarity Rarity;
        public bool IsNew; // 是否新获得
        public int FragmentCount; // 如果重复，转化为碎片数
    }

    /// <summary>
    /// 抽卡系统管理器
    /// 
    /// 规则（来自GDD）：
    /// - 单抽消耗150钻石或1召唤券
    /// - 十连消耗1500钻石或10召唤券
    /// - 概率：R=85%, SR=12%, SSR=3%
    /// - 保底：50次必出SSR
    /// - 十连保底：至少1个SR
    /// - 重复英雄转化为碎片
    /// </summary>
    public class GachaSystem : Singleton<GachaSystem>
    {
        // ========== 常量 ==========
        public const int SingleCostDiamond = 150;
        public const int TenCostDiamond = 1500;
        public const int PityCount = 50; // SSR保底次数

        // 概率（百分比）
        public const float RateR = 85f;
        public const float RateSR = 12f;
        public const float RateSSR = 3f;

        // 重复英雄碎片转化
        private const int DuplicateR_Fragments = 5;
        private const int DuplicateSR_Fragments = 15;
        private const int DuplicateSSR_Fragments = 50;

        // ========== 私有字段 ==========
        private int _pityCounter; // 保底计数器
        private int _totalPulls; // 总抽卡次数

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            LoadState();
            Debug.Log("[GachaSystem] 初始化完成");
        }

        protected override void OnDispose()
        {
            SaveState();
        }

        // ========== 公共方法 ==========

        /// <summary>单抽（消耗钻石）</summary>
        public GachaResult PullSingle()
        {
            if (!PlayerDataManager.HasInstance) return null;

            if (!PlayerDataManager.Instance.SpendDiamonds(SingleCostDiamond))
            {
                Debug.LogWarning("[Gacha] 钻石不足");
                return null;
            }

            var result = DoSinglePull();
            ProcessResult(result);

            SaveState();

            // 发布事件
            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new GachaResultEvent
                {
                    Count = 1,
                    SSRCount = result.Rarity == HeroRarity.SSR ? 1 : 0,
                    SRCount = result.Rarity == HeroRarity.SR ? 1 : 0,
                    RCount = result.Rarity == HeroRarity.R ? 1 : 0,
                    HitPity = _pityCounter == 0
                });
            }

            return result;
        }

        /// <summary>十连抽</summary>
        public List<GachaResult> PullTen()
        {
            if (!PlayerDataManager.HasInstance) return null;

            if (!PlayerDataManager.Instance.SpendDiamonds(TenCostDiamond))
            {
                Debug.LogWarning("[Gacha] 钻石不足");
                return null;
            }

            var results = new List<GachaResult>();
            bool hasSR = false;

            for (int i = 0; i < 10; i++)
            {
                var result = DoSinglePull();

                // 十连保底：最后一抽如果没有SR以上，强制出SR
                if (i == 9 && !hasSR)
                {
                    if (result.Rarity == HeroRarity.R)
                    {
                        result = ForcePullRarity(HeroRarity.SR);
                    }
                }

                if (result.Rarity >= HeroRarity.SR) hasSR = true;

                ProcessResult(result);
                results.Add(result);
            }

            SaveState();

            // 统计
            int ssrCount = 0, srCount = 0, rCount = 0;
            bool hitPity = false;
            for (int i = 0; i < results.Count; i++)
            {
                switch (results[i].Rarity)
                {
                    case HeroRarity.SSR: ssrCount++; break;
                    case HeroRarity.SR: srCount++; break;
                    case HeroRarity.R: rCount++; break;
                }
            }

            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new GachaResultEvent
                {
                    Count = 10,
                    SSRCount = ssrCount,
                    SRCount = srCount,
                    RCount = rCount,
                    HitPity = hitPity
                });
            }

            return results;
        }

        /// <summary>获取当前保底计数</summary>
        public int GetPityCounter() => _pityCounter;

        /// <summary>获取距离保底还需多少次</summary>
        public int GetPityRemaining() => PityCount - _pityCounter;

        /// <summary>获取总抽卡次数</summary>
        public int GetTotalPulls() => _totalPulls;

        /// <summary>获取概率展示文本（合规要求）</summary>
        public string GetRateDisplayText()
        {
            return $"概率公示：\n" +
                   $"  R  (普通): {RateR:F1}%\n" +
                   $"  SR (稀有): {RateSR:F1}%\n" +
                   $"  SSR(传说): {RateSSR:F1}%\n" +
                   $"  保底: {PityCount}次必出SSR\n" +
                   $"  十连保底: 至少1个SR";
        }

        // ========== 私有方法 ==========

        private GachaResult DoSinglePull()
        {
            _pityCounter++;
            _totalPulls++;

            HeroRarity rarity;

            // 保底检查
            if (_pityCounter >= PityCount)
            {
                rarity = HeroRarity.SSR;
                _pityCounter = 0;
            }
            else
            {
                // 随机抽取
                float roll = UnityEngine.Random.Range(0f, 100f);
                if (roll < RateSSR)
                {
                    rarity = HeroRarity.SSR;
                    _pityCounter = 0; // 出SSR重置保底
                }
                else if (roll < RateSSR + RateSR)
                {
                    rarity = HeroRarity.SR;
                }
                else
                {
                    rarity = HeroRarity.R;
                }
            }

            // 从对应稀有度池中随机选择英雄
            var heroes = HeroConfigTable.GetHeroesByRarity(rarity);
            if (heroes.Count == 0)
            {
                // 降级
                heroes = HeroConfigTable.GetHeroesByRarity(HeroRarity.R);
            }

            var selectedHero = heroes[UnityEngine.Random.Range(0, heroes.Count)];

            return new GachaResult
            {
                HeroId = selectedHero.Id,
                Rarity = rarity,
                IsNew = !HeroSystem.Instance.IsHeroUnlocked(selectedHero.Id),
                FragmentCount = 0
            };
        }

        private GachaResult ForcePullRarity(HeroRarity rarity)
        {
            var heroes = HeroConfigTable.GetHeroesByRarity(rarity);
            if (heroes.Count == 0) return DoSinglePull();

            var selectedHero = heroes[UnityEngine.Random.Range(0, heroes.Count)];
            return new GachaResult
            {
                HeroId = selectedHero.Id,
                Rarity = rarity,
                IsNew = !HeroSystem.Instance.IsHeroUnlocked(selectedHero.Id),
                FragmentCount = 0
            };
        }

        private void ProcessResult(GachaResult result)
        {
            if (result.IsNew)
            {
                // 新英雄，解锁
                HeroSystem.Instance.UnlockHero(result.HeroId);
            }
            else
            {
                // 重复英雄，转化为碎片
                int fragments = result.Rarity == HeroRarity.SSR ? DuplicateSSR_Fragments :
                                result.Rarity == HeroRarity.SR ? DuplicateSR_Fragments :
                                DuplicateR_Fragments;
                result.FragmentCount = fragments;
                HeroSystem.Instance.AddFragments(result.HeroId, fragments);
            }
        }

        private void LoadState()
        {
            if (SaveManager.HasInstance)
            {
                var state = SaveManager.Instance.Load<GachaSaveState>("gacha_state");
                if (state != null)
                {
                    _pityCounter = state.PityCounter;
                    _totalPulls = state.TotalPulls;
                }
            }
        }

        private void SaveState()
        {
            if (SaveManager.HasInstance)
            {
                SaveManager.Instance.Save("gacha_state", new GachaSaveState
                {
                    PityCounter = _pityCounter,
                    TotalPulls = _totalPulls
                });
            }
        }
    }

    [Serializable]
    public class GachaSaveState
    {
        public int PityCounter;
        public int TotalPulls;
    }
}
