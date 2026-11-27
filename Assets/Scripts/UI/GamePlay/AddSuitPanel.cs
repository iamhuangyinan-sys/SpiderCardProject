using System;
using System.Collections.Generic;
using DG.Tweening;
using Framework.Res;
using Framework.UI;
using UnityEngine;
using UnityEngine.UI;

public partial class AddSuitPanel : NormalPanel, ISceneInputBlockPanel
{
    private const string ItemPath = "Prefab/UI/GamePlay/Module/AddSuitItemModule";
    private const float ItemScale = 1.15f;
    private readonly List<AddSuitItemModule> _items = new();
    private GameObject _itemPrefab;
    private Tween _fly;
    private bool _choosing;

    public Func<E_CardSuitEnum, bool> OnChoose;
    public Action OnFinished;

    protected override bool FlyInOnOpen => true;
    protected override bool FlyOutOnClose => true;

    protected override void OnOpen()
    {
        _itemPrefab = ResManager.Instance.Load<GameObject>(ItemPath);
        BuildItems();
    }

    protected override void OnShow() => BuildItems();

    protected override void OnHide() => ClearItems();

    protected override void OnClose()
    {
        ClearItems();
        OnChoose = null;
        OnFinished = null;
        ResManager.Instance.UnloadAsset<GameObject>(ItemPath);
    }

    private void BuildItems()
    {
        ClearItems();
        _choosing = false;
        var layout = comps.imgLayout.GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = true;

        foreach (var suit in RunManager.Instance.GetAvailableMainSuits())
        {
            var item = Instantiate(_itemPrefab, comps.imgLayout.transform).GetComponent<AddSuitItemModule>();
            item.transform.localScale = new Vector3(ItemScale, ItemScale, 1f);
            item.Bind(suit, Choose);
            _items.Add(item);
        }
    }

    private void Choose(AddSuitItemModule item)
    {
        if (_choosing || OnChoose?.Invoke(item.Suit) != true) return;
        _choosing = true;
        foreach (var candidate in _items) candidate.SetSelectable(false);

        var anchor = UIManager.Instance.GetPanel<MainTopPanel>()?.CardBuildAnchor;
        if (anchor == null)
        {
            OnFinished?.Invoke();
            return;
        }

        var layout = comps.imgLayout.GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = false;
        var canvas = item.GetComponentInParent<Canvas>();
        _fly = AnimationHelper.FlyAndVanish((RectTransform)item.transform, anchor.position,
            canvas != null ? canvas.rootCanvas.transform : null);
        _fly.OnComplete(() =>
        {
            _fly = null;
            item.gameObject.SetActive(false);
            OnFinished?.Invoke();
        });
    }

    private void ClearItems()
    {
        _fly?.Kill();
        _fly = null;
        foreach (var item in _items)
        {
            if (item == null) continue;
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
        }
        _items.Clear();
    }
}
