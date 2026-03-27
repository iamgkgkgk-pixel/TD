// ============================================================
// 文件名：ShopPanel.cs
// 功能描述：商城面板UI — 分页展示、商品列表、购买确认
// 创建时间：2026-03-27
// 所属模块：MetaGame
// 对应交互：阶段四 #254
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Visual;

namespace AetheraSurvivors.MetaGame
{
    /// <summary>
    /// 商城面板
    /// </summary>
    public class ShopPanel : BasePanel
    {
        public override UILayer Layer => UILayer.Normal;
        public override bool IsCached => true;

        private Button _btnBack;
        private Button[] _tabButtons;
        private RectTransform _productListArea;
        private ShopCategory _currentCategory = ShopCategory.Recommend;
        private List<GameObject> _productItems = new List<GameObject>();

        // 购买确认弹窗
        private GameObject _confirmPopup;
        private Text _txtConfirmInfo;
        private Button _btnConfirmBuy;
        private Button _btnConfirmCancel;
        private string _pendingProductId;

        protected override void OnOpen(object param)
        {
            BuildUI();
            RefreshProducts(_currentCategory);
        }

        protected override void OnShow()
        {
            RefreshProducts(_currentCategory);
        }

        private void BuildUI()
        {
            // 背景
            var bgObj = new GameObject("BG");
            bgObj.transform.SetParent(transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            UIStyleKit.CreateGradientPanel(bgRect,
                new Color(0.04f, 0.03f, 0.10f, 1f),
                new Color(0.08f, 0.06f, 0.18f, 1f));

            // 顶部栏
            var topBar = CreateRect("TopBar", transform);
            topBar.anchorMin = new Vector2(0, 0.93f);
            topBar.anchorMax = Vector2.one;
            topBar.offsetMin = Vector2.zero;
            topBar.offsetMax = Vector2.zero;
            UIStyleKit.CreateStyledPanel(topBar, new Color(0.06f, 0.06f, 0.14f, 0.95f));

            _btnBack = CreateButton(topBar, "BtnBack", "← 返回",
                new Vector2(0.01f, 0.1f), new Vector2(0.15f, 0.9f));
            _btnBack.onClick.AddListener(CloseSelf);
            UIStyleKit.StyleGrayButton(_btnBack);

            CreateText("Title", topBar, "🛒 商城", 20,
                UIStyleKit.TextGold, TextAnchor.MiddleCenter,
                new Vector2(0.3f, 0f), new Vector2(0.7f, 1f));

            // 分类Tab
            BuildTabs();

            // 商品列表区
            _productListArea = CreateRect("ProductList", transform);
            _productListArea.anchorMin = new Vector2(0.02f, 0.05f);
            _productListArea.anchorMax = new Vector2(0.98f, 0.85f);
            _productListArea.offsetMin = Vector2.zero;
            _productListArea.offsetMax = Vector2.zero;

            // 购买确认弹窗
            BuildConfirmPopup();
        }

        private void BuildTabs()
        {
            var tabArea = CreateRect("TabArea", transform);
            tabArea.anchorMin = new Vector2(0.02f, 0.86f);
            tabArea.anchorMax = new Vector2(0.98f, 0.93f);
            tabArea.offsetMin = Vector2.zero;
            tabArea.offsetMax = Vector2.zero;

            string[] tabNames = { "推荐", "钻石", "礼包", "道具", "限时" };
            ShopCategory[] categories = {
                ShopCategory.Recommend, ShopCategory.Diamond,
                ShopCategory.Gift, ShopCategory.Item, ShopCategory.Limited
            };
            _tabButtons = new Button[tabNames.Length];
            float tabW = 1f / tabNames.Length;

            for (int i = 0; i < tabNames.Length; i++)
            {
                int idx = i;
                ShopCategory cat = categories[i];
                var btn = CreateButton(tabArea, $"Tab_{i}", tabNames[i],
                    new Vector2(tabW * i + 0.005f, 0.1f),
                    new Vector2(tabW * (i + 1) - 0.005f, 0.9f));
                btn.onClick.AddListener(() => OnTabClick(cat, idx));
                _tabButtons[i] = btn;

                UIStyleKit.StyleButton(btn,
                    i == 0 ? UIStyleKit.BtnBlueNormal : UIStyleKit.BtnGrayNormal,
                    UIStyleKit.BtnBlueHover, UIStyleKit.BtnBluePressed);
            }
        }

        private void BuildConfirmPopup()
        {
            _confirmPopup = new GameObject("ConfirmPopup");
            _confirmPopup.transform.SetParent(transform, false);
            var popRect = _confirmPopup.AddComponent<RectTransform>();
            popRect.anchorMin = new Vector2(0.15f, 0.35f);
            popRect.anchorMax = new Vector2(0.85f, 0.65f);
            popRect.offsetMin = Vector2.zero;
            popRect.offsetMax = Vector2.zero;

            UIStyleKit.CreateStyledPanel(popRect,
                new Color(0.08f, 0.08f, 0.18f, 0.98f),
                UIStyleKit.BorderGold, 16, 3);

            _txtConfirmInfo = CreateText("ConfirmInfo", popRect, "确认购买？", 18,
                UIStyleKit.TextWhite, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.9f));

            _btnConfirmBuy = CreateButton(popRect, "BtnBuy", "✅ 确认",
                new Vector2(0.1f, 0.08f), new Vector2(0.48f, 0.38f));
            _btnConfirmBuy.onClick.AddListener(OnConfirmBuy);
            UIStyleKit.StyleGreenButton(_btnConfirmBuy);

            _btnConfirmCancel = CreateButton(popRect, "BtnCancel", "❌ 取消",
                new Vector2(0.52f, 0.08f), new Vector2(0.9f, 0.38f));
            _btnConfirmCancel.onClick.AddListener(() => _confirmPopup.SetActive(false));
            UIStyleKit.StyleRedButton(_btnConfirmCancel);

            _confirmPopup.SetActive(false);
        }

        private void OnTabClick(ShopCategory category, int tabIndex)
        {
            _currentCategory = category;
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                UIStyleKit.StyleButton(_tabButtons[i],
                    i == tabIndex ? UIStyleKit.BtnBlueNormal : UIStyleKit.BtnGrayNormal,
                    UIStyleKit.BtnBlueHover, UIStyleKit.BtnBluePressed);
            }
            RefreshProducts(category);
        }

        private void RefreshProducts(ShopCategory category)
        {
            // 清除旧商品
            foreach (var item in _productItems)
            {
                if (item != null) Destroy(item);
            }
            _productItems.Clear();

            var products = ShopSystem.Instance.GetProducts(category);
            int cols = 2;
            float itemW = 1f / cols;
            float itemH = 0.22f;

            for (int i = 0; i < products.Count; i++)
            {
                var product = products[i];
                int col = i % cols;
                int row = i / cols;

                var itemObj = new GameObject($"Product_{product.ProductId}");
                itemObj.transform.SetParent(_productListArea, false);
                var itemRect = itemObj.AddComponent<RectTransform>();
                itemRect.anchorMin = new Vector2(col * itemW + 0.01f, 1f - (row + 1) * itemH - 0.01f);
                itemRect.anchorMax = new Vector2((col + 1) * itemW - 0.01f, 1f - row * itemH - 0.01f);
                itemRect.offsetMin = Vector2.zero;
                itemRect.offsetMax = Vector2.zero;

                UIStyleKit.CreateStyledPanel(itemRect,
                    new Color(0.10f, 0.10f, 0.22f, 0.9f),
                    UIStyleKit.BorderSilver, 8, 1);

                // 商品名
                CreateText("Name", itemRect, $"{product.Icon} {product.Name}", 16,
                    UIStyleKit.TextWhite, TextAnchor.MiddleCenter,
                    new Vector2(0.05f, 0.65f), new Vector2(0.95f, 0.95f));

                // 描述
                CreateText("Desc", itemRect, product.Description, 12,
                    UIStyleKit.TextGray, TextAnchor.MiddleCenter,
                    new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.65f));

                // 价格
                string priceStr = product.PriceType == PriceType.RMB ? $"¥{product.Price}" :
                    product.PriceType == PriceType.Diamond ? $"💎{product.Price}" :
                    product.PriceType == PriceType.Gold ? $"🪙{product.Price}" :
                    product.PriceType == PriceType.Free ? "免费" : "看广告";

                // 购买按钮
                string pid = product.ProductId;
                var buyBtn = CreateButton(itemRect, "BtnBuy", priceStr,
                    new Vector2(0.15f, 0.05f), new Vector2(0.85f, 0.35f));
                buyBtn.onClick.AddListener(() => OnProductClick(pid));

                // 检查是否已达购买上限
                if (product.PurchaseLimit > 0)
                {
                    int count = ShopSystem.Instance.GetPurchaseCount(product.ProductId);
                    if (count >= product.PurchaseLimit)
                    {
                        buyBtn.interactable = false;
                        UIStyleKit.StyleGrayButton(buyBtn);
                    }
                    else
                    {
                        UIStyleKit.StyleGreenButton(buyBtn);
                    }
                }
                else
                {
                    UIStyleKit.StyleGreenButton(buyBtn);
                }

                _productItems.Add(itemObj);
            }
        }

        private void OnProductClick(string productId)
        {
            _pendingProductId = productId;
            var products = ShopSystem.Instance.GetAllProducts();
            ShopProduct product = null;
            for (int i = 0; i < products.Count; i++)
            {
                if (products[i].ProductId == productId)
                {
                    product = products[i];
                    break;
                }
            }

            if (product == null) return;

            string priceStr = product.PriceType == PriceType.RMB ? $"¥{product.Price}" :
                product.PriceType == PriceType.Diamond ? $"💎{product.Price}" :
                $"🪙{product.Price}";

            _txtConfirmInfo.text = $"确认购买 {product.Name}？\n价格: {priceStr}";
            _confirmPopup.SetActive(true);
        }

        private void OnConfirmBuy()
        {
            _confirmPopup.SetActive(false);
            if (!string.IsNullOrEmpty(_pendingProductId))
            {
                if (ShopSystem.Instance.Purchase(_pendingProductId))
                {
                    RefreshProducts(_currentCategory);
                }
            }
        }

        // ========== 工具方法 ==========

        private RectTransform CreateRect(string name, Transform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj.AddComponent<RectTransform>();
        }

        private Text CreateText(string name, RectTransform parent, string text, int fontSize,
            Color color, TextAnchor alignment, Vector2 anchorMin = default, Vector2 anchorMax = default)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin == default ? Vector2.zero : anchorMin;
            rect.anchorMax = anchorMax == default ? Vector2.one : anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = alignment;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            txt.raycastTarget = false;
            return txt;
        }

        private Button CreateButton(RectTransform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = obj.AddComponent<Image>();
            var btn = obj.AddComponent<Button>();

            var txtObj = new GameObject("Label");
            txtObj.transform.SetParent(obj.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            var txt = txtObj.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = 14;
            txt.color = UIStyleKit.TextWhite;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            txt.raycastTarget = false;

            return btn;
        }
    }
}
