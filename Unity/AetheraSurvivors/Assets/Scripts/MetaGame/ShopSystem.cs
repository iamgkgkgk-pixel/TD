// ============================================================
// 文件名：ShopSystem.cs
// 功能描述：商城系统 — 商品配置、购买逻辑、首充/月卡/周卡
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #254-256
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Data;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>商品类型</summary>
    public enum ShopCategory { Recommend, Diamond, Gift, Item, Limited }

    /// <summary>货币类型</summary>
    public enum PriceType { Gold, Diamond, RMB, Free, Ad }

    /// <summary>
    /// 商品配置
    /// </summary>
    [Serializable]
    public class ShopProduct
    {
        public string ProductId;
        public string Name;
        public string Description;
        public string Icon;
        public ShopCategory Category;
        public PriceType PriceType;
        public int Price;
        public int OriginalPrice; // 原价（用于显示折扣）
        public string RewardType; // "diamonds", "gold", "hero_fragment", "summon_ticket", "stamina"
        public int RewardAmount;
        public string RewardHeroId; // 如果奖励是英雄碎片
        public int PurchaseLimit; // 购买次数限制（0=无限）
        public bool IsLimited; // 是否限时
        public long ExpireTime; // 过期时间戳
    }

    /// <summary>
    /// 商城系统管理器
    /// </summary>
    public class ShopSystem : Singleton<ShopSystem>
    {
        private List<ShopProduct> _products;
        private Dictionary<string, int> _purchaseCounts; // 商品购买次数记录

        // ========== 首充/月卡状态 ==========
        private bool _firstPayClaimed;
        private long _monthCardExpireTime;
        private long _weekCardExpireTime;

        protected override void OnInit()
        {
            _purchaseCounts = new Dictionary<string, int>();
            InitProducts();
            LoadPurchaseState();
            ClaimDailyCardRewards();
            Debug.Log("[ShopSystem] 初始化完成");
        }

        protected override void OnDispose()
        {
            SavePurchaseState();
        }

        /// <summary>初始化商品列表</summary>
        private void InitProducts()
        {
            _products = new List<ShopProduct>
            {
                // ===== 推荐 =====
                new ShopProduct
                {
                    ProductId = "first_pay", Name = "首充礼包", Description = "SSR碎片×30 + 500钻 + 3召唤券",
Icon = "[礼]", Category = ShopCategory.Recommend, PriceType = PriceType.RMB,

                    Price = 6, OriginalPrice = 30, RewardType = "first_pay_bundle",
                    RewardAmount = 1, PurchaseLimit = 1
                },
                new ShopProduct
                {
                    ProductId = "month_card", Name = "月卡", Description = "每日100钻+20体力（30天）",
                    Icon = "📅", Category = ShopCategory.Recommend, PriceType = PriceType.RMB,
                    Price = 30, RewardType = "month_card", RewardAmount = 1, PurchaseLimit = 0
                },
                new ShopProduct
                {
                    ProductId = "week_card", Name = "周卡", Description = "每日50钻+10体力（7天）",
                    Icon = "📆", Category = ShopCategory.Recommend, PriceType = PriceType.RMB,
                    Price = 12, RewardType = "week_card", RewardAmount = 1, PurchaseLimit = 0
                },

                // ===== 钻石 =====
                new ShopProduct
                {
                    ProductId = "diamond_60", Name = "60钻石", Description = "少量钻石",
                    Icon = "◇", Category = ShopCategory.Diamond, PriceType = PriceType.RMB,
                    Price = 6, RewardType = "diamonds", RewardAmount = 60
                },
                new ShopProduct
                {
                    ProductId = "diamond_300", Name = "300钻石", Description = "中量钻石",
                    Icon = "◇", Category = ShopCategory.Diamond, PriceType = PriceType.RMB,
                    Price = 30, RewardType = "diamonds", RewardAmount = 300
                },
                new ShopProduct
                {
                    ProductId = "diamond_680", Name = "680钻石", Description = "大量钻石",
                    Icon = "◇", Category = ShopCategory.Diamond, PriceType = PriceType.RMB,
                    Price = 68, RewardType = "diamonds", RewardAmount = 680
                },
                new ShopProduct
                {
                    ProductId = "diamond_1280", Name = "1280钻石", Description = "巨量钻石",
                    Icon = "◇", Category = ShopCategory.Diamond, PriceType = PriceType.RMB,
                    Price = 128, RewardType = "diamonds", RewardAmount = 1280
                },
                new ShopProduct
                {
                    ProductId = "diamond_3280", Name = "3280钻石", Description = "超值钻石",
                    Icon = "◇", Category = ShopCategory.Diamond, PriceType = PriceType.RMB,
                    Price = 328, RewardType = "diamonds", RewardAmount = 3280
                },

                // ===== 道具 =====
                new ShopProduct
                {
                    ProductId = "stamina_60", Name = "体力×60", Description = "恢复60点体力",
                    Icon = "!", Category = ShopCategory.Item, PriceType = PriceType.Diamond,
                    Price = 50, RewardType = "stamina", RewardAmount = 60
                },
                new ShopProduct
                {
                    ProductId = "summon_ticket_1", Name = "召唤券×1", Description = "1张召唤券",
                    Icon = "T", Category = ShopCategory.Item, PriceType = PriceType.Diamond,
                    Price = 150, RewardType = "summon_ticket", RewardAmount = 1
                },
                new ShopProduct
                {
                    ProductId = "gold_10000", Name = "金币×10000", Description = "10000金币",
                    Icon = "G", Category = ShopCategory.Item, PriceType = PriceType.Diamond,
                    Price = 100, RewardType = "gold", RewardAmount = 10000
                },

                // ===== 礼包 =====
                new ShopProduct
                {
                    ProductId = "newbie_gift", Name = "新手礼包", Description = "300钻+5000金+3召唤券",
                    Icon = "🎀", Category = ShopCategory.Gift, PriceType = PriceType.RMB,
                    Price = 6, OriginalPrice = 30, RewardType = "newbie_bundle",
                    RewardAmount = 1, PurchaseLimit = 1
                },
                new ShopProduct
                {
                    ProductId = "growth_gift", Name = "成长礼包", Description = "500钻+10000金+5召唤券",
                    Icon = "📦", Category = ShopCategory.Gift, PriceType = PriceType.RMB,
                    Price = 30, OriginalPrice = 98, RewardType = "growth_bundle",
                    RewardAmount = 1, PurchaseLimit = 1
                }
            };
        }

        // ========== 公共方法 ==========

        /// <summary>获取指定分类的商品列表</summary>
        public List<ShopProduct> GetProducts(ShopCategory category)
        {
            var result = new List<ShopProduct>();
            for (int i = 0; i < _products.Count; i++)
            {
                if (_products[i].Category == category)
                    result.Add(_products[i]);
            }
            return result;
        }

        /// <summary>获取所有商品</summary>
        public List<ShopProduct> GetAllProducts() => _products;

        /// <summary>购买商品</summary>
        public bool Purchase(string productId)
        {
            var product = GetProduct(productId);
            if (product == null)
            {
                Debug.LogError($"[ShopSystem] 商品不存在: {productId}");
                return false;
            }

            // 检查购买次数限制
            if (product.PurchaseLimit > 0)
            {
                int count = GetPurchaseCount(productId);
                if (count >= product.PurchaseLimit)
                {
                    Debug.LogWarning($"[ShopSystem] 已达购买上限: {productId}");
                    return false;
                }
            }

            // 扣费
            bool paySuccess = false;
            switch (product.PriceType)
            {
                case PriceType.Gold:
                    paySuccess = PlayerDataManager.Instance.SpendGold(product.Price);
                    break;
                case PriceType.Diamond:
                    paySuccess = PlayerDataManager.Instance.SpendDiamonds(product.Price);
                    break;
                case PriceType.RMB:
                    // RMB支付走微信支付流程（此处模拟成功）
                    paySuccess = true;
                    Debug.Log($"[ShopSystem] 模拟RMB支付: ¥{product.Price}");
                    break;
                case PriceType.Free:
                case PriceType.Ad:
                    paySuccess = true;
                    break;
            }

            if (!paySuccess)
            {
                Debug.LogWarning($"[ShopSystem] 支付失败: {productId}");
                return false;
            }

            // 发放奖励
            DeliverReward(product);

            // 记录购买次数
            IncrementPurchaseCount(productId);

            // 发布事件
            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new ShopPurchaseEvent
                {
                    ProductId = productId,
                    ProductName = product.Name,
                    Price = product.Price,
                    CurrencyType = product.PriceType.ToString()
                });
            }

            Debug.Log($"[ShopSystem] 购买成功: {product.Name}");
            return true;
        }

        /// <summary>获取商品已购买次数</summary>
        public int GetPurchaseCount(string productId)
        {
            _purchaseCounts.TryGetValue(productId, out int count);
            return count;
        }

        /// <summary>是否已首充</summary>
        public bool IsFirstPayClaimed => _firstPayClaimed;

        /// <summary>月卡是否有效</summary>
        public bool IsMonthCardActive =>
            _monthCardExpireTime > DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>周卡是否有效</summary>
        public bool IsWeekCardActive =>
            _weekCardExpireTime > DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // ========== 私有方法 ==========

        private ShopProduct GetProduct(string productId)
        {
            for (int i = 0; i < _products.Count; i++)
            {
                if (_products[i].ProductId == productId)
                    return _products[i];
            }
            return null;
        }

        private void DeliverReward(ShopProduct product)
        {
            if (!PlayerDataManager.HasInstance) return;

            switch (product.RewardType)
            {
                case "diamonds":
                    PlayerDataManager.Instance.AddDiamonds(product.RewardAmount);
                    break;
                case "gold":
                    PlayerDataManager.Instance.AddGold(product.RewardAmount);
                    break;
                case "stamina":
                    var data = PlayerDataManager.Instance.Data;
                    data.Stamina = Mathf.Min(data.Stamina + product.RewardAmount, data.MaxStamina * 2);
                    PlayerDataManager.Instance.MarkDirty();
                    break;
                case "first_pay_bundle":
                    PlayerDataManager.Instance.AddDiamonds(500);
                    HeroSystem.Instance.AddFragments("hero_chosen_one", 30);
                    // 发放3张召唤券
                    var fpData = PlayerDataManager.Instance.Data;
                    fpData.SummonTickets = (fpData.SummonTickets) + 3;
                    PlayerDataManager.Instance.MarkDirty();
                    _firstPayClaimed = true;
                    break;
                case "month_card":
                    _monthCardExpireTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30 * 86400;
                    PlayerDataManager.Instance.AddDiamonds(100); // 立即发放当日
                    break;
                case "week_card":
                    _weekCardExpireTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 7 * 86400;
                    PlayerDataManager.Instance.AddDiamonds(50);
                    break;
                case "newbie_bundle":
                    PlayerDataManager.Instance.AddDiamonds(300);
                    PlayerDataManager.Instance.AddGold(5000);
                    break;
                case "growth_bundle":
                    PlayerDataManager.Instance.AddDiamonds(500);
                    PlayerDataManager.Instance.AddGold(10000);
                    break;
            }

            PlayerDataManager.Instance.Save();
        }

        /// <summary>每日登录时发放月卡/周卡奖励</summary>
        public void ClaimDailyCardRewards()
        {
            if (!PlayerDataManager.HasInstance) return;

            // 检查是否今天已领取过（用LastCardClaimDate判断）
            var data = PlayerDataManager.Instance.Data;
            string today = System.DateTime.UtcNow.ToString("yyyyMMdd");
            if (data.LastCardClaimDate == today) return;

            // 无论是否有卡，都标记今日已检查（防止购卡后重启重复领取）
            data.LastCardClaimDate = today;

            bool anyReward = false;

            // 月卡每日奖励：100钻石 + 20体力
            if (IsMonthCardActive)
            {
                PlayerDataManager.Instance.AddDiamonds(100);
                data.Stamina = Mathf.Min(data.Stamina + 20, data.MaxStamina * 2);
                anyReward = true;
                Debug.Log("[ShopSystem] 月卡每日奖励发放: 100钻石 + 20体力");
            }

            // 周卡每日奖励：50钻石 + 10体力
            if (IsWeekCardActive)
            {
                PlayerDataManager.Instance.AddDiamonds(50);
                data.Stamina = Mathf.Min(data.Stamina + 10, data.MaxStamina * 2);
                anyReward = true;
                Debug.Log("[ShopSystem] 周卡每日奖励发放: 50钻石 + 10体力");
            }

            PlayerDataManager.Instance.MarkDirty();
            PlayerDataManager.Instance.Save();
        }

        private void IncrementPurchaseCount(string productId)
        {
            if (!_purchaseCounts.ContainsKey(productId))
                _purchaseCounts[productId] = 0;
            _purchaseCounts[productId]++;
            SavePurchaseState();
        }

        private void LoadPurchaseState()
        {
            // 从SaveManager加载购买记录
            if (SaveManager.HasInstance)
            {
                var state = SaveManager.Instance.Load<ShopSaveState>("shop_state");
                if (state != null)
                {
                    _firstPayClaimed = state.FirstPayClaimed;
                    _monthCardExpireTime = state.MonthCardExpire;
                    _weekCardExpireTime = state.WeekCardExpire;
                    if (state.PurchaseCounts != null)
                    {
                        for (int i = 0; i < state.PurchaseCounts.Count; i++)
                        {
                            _purchaseCounts[state.PurchaseCounts[i].Key] = state.PurchaseCounts[i].Value;
                        }
                    }
                }
            }
        }

        private void SavePurchaseState()
        {
            if (!SaveManager.HasInstance) return;

            var state = new ShopSaveState
            {
                FirstPayClaimed = _firstPayClaimed,
                MonthCardExpire = _monthCardExpireTime,
                WeekCardExpire = _weekCardExpireTime,
                PurchaseCounts = new List<KVPair>()
            };

            foreach (var pair in _purchaseCounts)
            {
                state.PurchaseCounts.Add(new KVPair { Key = pair.Key, Value = pair.Value });
            }

            SaveManager.Instance.Save("shop_state", state);
        }
    }

    /// <summary>商城存档数据</summary>
    [Serializable]
    public class ShopSaveState
    {
        public bool FirstPayClaimed;
        public long MonthCardExpire;
        public long WeekCardExpire;
        public List<KVPair> PurchaseCounts;
    }

    /// <summary>键值对（用于序列化Dictionary）</summary>
    [Serializable]
    public class KVPair
    {
        public string Key;
        public int Value;
    }
}
