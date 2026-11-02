using System;
using System.Collections.Generic;
using Framework.Res;
using Framework.UI;
using UnityEngine;

public partial class BeginBuildPanel : NormalPanel, ISceneInputBlockPanel
{
    protected override bool FlyInOnOpen => true;
    protected override bool FlyOutOnClose => true;

    private const string ItemPath = "Prefab/UI/GamePlay/Module/BeginBuildItemModule";
    public Func<string, int?, bool> OnConfirm;
    private List<BeginBuildConfig> builds;
    private readonly List<BeginBuildItemModule> items = new();
    private GameObject itemPrefab;
    private List<CardDef> preview;
    private int selectedIndex;
    private bool confirming;

    protected override void OnOpen()
    {
        comps.btnLeft.onClick.AddListener(OnLeft);
        comps.btnRight.onClick.AddListener(OnRight);
        comps.btnCheck.onClick.AddListener(OnCheck);
        comps.btnContinue.onClick.AddListener(OnContinue);
        itemPrefab = ResManager.Instance.Load<GameObject>(ItemPath);
        builds = ConfigHelper.GetAll<BeginBuildConfig>();
        // TODO: 难度选择，当前沿用预制体中隐藏的 dif 节点。
        RefreshBuild();
    }

    protected override void OnShow()
    {
        confirming = false;
        comps.iptSeed.text = string.Empty;
        RefreshBuild();
    }
    protected override void OnHide() { }

    protected override void OnClose()
    {
        comps.btnLeft.onClick.RemoveListener(OnLeft);
        comps.btnRight.onClick.RemoveListener(OnRight);
        comps.btnCheck.onClick.RemoveListener(OnCheck);
        comps.btnContinue.onClick.RemoveListener(OnContinue);
        OnConfirm = null;
        foreach (var item in items)
            if (item != null) Destroy(item.gameObject);
        items.Clear();
        if (itemPrefab != null) ResManager.Instance.UnloadAsset<GameObject>(ItemPath);
        itemPrefab = null;
        preview = null;
    }

    private void OnLeft() => SwitchBuild(-1);
    private void OnRight() => SwitchBuild(1);

    private void SwitchBuild(int direction)
    {
        if (confirming || builds.Count == 0) return;
        selectedIndex = (selectedIndex + direction + builds.Count) % builds.Count;
        RefreshBuild();
    }

    private void RefreshBuild()
    {
        preview = null;
        var cfg = builds.Count > 0 ? builds[selectedIndex] : null;
        comps.txtName.text = cfg != null ? LanguageHelper.Get(cfg.Name) : LanguageHelper.Get("ui_beginbuild_no_deck");
        bool valid = cfg != null && BeginBuildHelper.TryBuild(cfg.Id, out preview);
        comps.btnCheck.interactable = valid;
        comps.btnContinue.interactable = valid;
        comps.btnLeft.interactable = builds.Count > 1;
        comps.btnRight.interactable = builds.Count > 1;
        int count = cfg?.Image?.Count ?? 0;
        while (items.Count < count && itemPrefab != null)
        {
            var go = Instantiate(itemPrefab, comps.imgLayout.transform, false);
            items.Add(go.GetComponent<BeginBuildItemModule>());
        }
        for (int i = 0; i < items.Count; i++)
        {
            bool visible = i < count;
            items[i].gameObject.SetActive(visible);
            items[i].SetImage(visible ? cfg.Image[i] : null);
        }
    }

    private void OnCheck()
    {
        if (preview != null) ShowCardsPanel.ShowCards(preview, LanguageHelper.Get("title_beginbuild_deck_cards"));
    }

    private void OnContinue()
    {
        if (confirming || preview == null || OnConfirm == null) return;
        string seedText = comps.iptSeed.text.Trim();
        int? seed = null;
        if (seedText.Length > 0)
        {
            if (!int.TryParse(seedText, System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out int parsedSeed))
            {
                comps.iptSeed.text = string.Empty;
                TipHelper.Show(LanguageHelper.Get("tip_beginbuild_seed_invalid"));
                return;
            }
            seed = parsedSeed;
        }
        confirming = true;
        comps.btnContinue.interactable = false;
        // 回调成功时场景入口会隐藏本面板并播放退出动画。
        if (OnConfirm(builds[selectedIndex].Id, seed)) return;
        confirming = false;
        comps.btnContinue.interactable = true;
        TipHelper.Show(LanguageHelper.Get("tip_beginbuild_deck_invalid"));
    }
}
