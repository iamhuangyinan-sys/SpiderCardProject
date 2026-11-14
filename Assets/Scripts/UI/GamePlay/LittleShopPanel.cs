using System;
using System.Collections.Generic;
using Framework.Res;
using Framework.UI;
using UnityEngine;
using UnityEngine.UI;

public partial class LittleShopPanel : NormalPanel, ISceneInputBlockPanel
{
    private const string ItemPath = "Prefab/UI/GamePlay/Item/ShopCardItem";
    private const float ItemScale = 1.15f;
    private readonly List<ShopCardItem> items = new();
    private GameObject itemPrefab;
    private bool choosing;
    public Action OnFinished;
    protected override bool FlyInOnOpen => true;
    protected override bool FlyOutOnClose => true;

    protected override void OnOpen()
    {
        itemPrefab = ResManager.Instance.Load<GameObject>(ItemPath);
        comps.btnSkip.onClick.AddListener(Skip);
        RefreshItems();
    }
    protected override void OnShow() => RefreshItems();
    protected override void OnHide() { }
    protected override void OnClose()
    {
        comps.btnSkip.onClick.RemoveListener(Skip);
        ClearItems();
        OnFinished = null;
        ResManager.Instance.UnloadAsset<GameObject>(ItemPath);
    }
    private void ClearItems()
    {
        foreach (var item in items)
            if (item != null) { item.OnRecycle(); Destroy(item.gameObject); }
        items.Clear();
    }
    private void RefreshItems()
    {
        ClearItems();
        choosing = false;
        var layout = comps.imgLayout.GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = true;
        comps.btnSkip.interactable = true;
        var goods = LittleShopManager.Instance.Goods;
        for (int i = 0; i < goods.Count; i++)
        {
            int index = i;
            var item = Instantiate(itemPrefab, comps.imgLayout.transform).GetComponent<ShopCardItem>();
            var rect = item.RectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = new Vector3(ItemScale, ItemScale, 1f);
            float spacing = rect.sizeDelta.x * ItemScale + (layout != null ? layout.spacing : 80f);
            rect.anchoredPosition = new Vector2((i - (goods.Count - 1) * 0.5f) * spacing, 0f);
            item.OnActivate();
            item.Bind(i, goods[i], () => Buy(index), () => OnFinished?.Invoke());
            items.Add(item);
        }
    }
    private bool Buy(int index)
    {
        if (choosing || !LittleShopManager.Instance.TryBuy(index)) return false;
        choosing = true;
        // 飞牌会脱离布局；冻结布局以保留其余商品的位置。
        var layout = comps.imgLayout.GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = false;
        comps.btnSkip.interactable = false;
        foreach (var item in items)
        {
            var group = item.GetComponent<CanvasGroup>();
            if (group == null) group = item.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
        }
        return true;
    }
    private void Skip()
    {
        if (choosing) return;
        choosing = true;
        comps.btnSkip.interactable = false;
        LittleShopManager.Instance.Skip();
        OnFinished?.Invoke();
    }
}
