using System;
using System.Collections.Generic;
using Framework.Res;
using Framework.UI;
using Framework.Event;
using UnityEngine;

public partial class BeginBuildPanel : NormalPanel, ISceneInputBlockPanel
{
    protected override bool FlyInOnOpen => true;
    protected override bool FlyOutOnClose => true;

    private const string ItemPath = "Prefab/UI/GamePlay/Module/BeginBuildItemModule";
    public Func<string, string, int?, bool> OnConfirm;
    private List<DifficultyConfig> difficulties;
    private int difficultyIndex;
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
        comps.btnDifLeft.onClick.AddListener(OnDifficultyLeft);
        comps.btnDifRight.onClick.AddListener(OnDifficultyRight);
        comps.btnCheck.onClick.AddListener(OnCheck);
        comps.btnContinue.onClick.AddListener(OnContinue);
        EventManager.Instance.AddListener(E_EventEnum.OnLanguageChanged, RefreshLanguage);
        itemPrefab = ResManager.Instance.Load<GameObject>(ItemPath);
        builds = ConfigHelper.GetAll<BeginBuildConfig>();
        difficulties = DifficultyHelper.GetAll();
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
        comps.btnDifLeft.onClick.RemoveListener(OnDifficultyLeft);
        comps.btnDifRight.onClick.RemoveListener(OnDifficultyRight);
        comps.btnCheck.onClick.RemoveListener(OnCheck);
        comps.btnContinue.onClick.RemoveListener(OnContinue);
        EventManager.Instance.RemoveListener(E_EventEnum.OnLanguageChanged, RefreshLanguage);
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
    private void OnDifficultyLeft() => SwitchDifficulty(-1);
    private void OnDifficultyRight() => SwitchDifficulty(1);

    private void SwitchDifficulty(int direction)
    {
        if (confirming || difficulties.Count == 0) return;
        difficultyIndex = Mathf.Clamp(difficultyIndex + direction, 0, difficulties.Count - 1);
        RefreshDeckPreview();
        RefreshDifficulty();
    }

    private void RefreshLanguage()
    {
        RefreshBuildName();
        RefreshDifficulty();
    }

    private void RefreshDifficulty()
    {
        var config = difficulties.Count > 0 ? difficulties[difficultyIndex] : null;
        comps.txtDif.text = config != null ? DifficultyHelper.GetName(config.Id) : string.Empty;
        string description = config != null ? DifficultyHelper.GetDescription(config) : string.Empty;
        comps.txtDifDesc.text = description;
        comps.txtDifDesc.gameObject.SetActive(!string.IsNullOrWhiteSpace(description));
        SetArrow(comps.btnDifLeft, difficultyIndex > 0);
        SetArrow(comps.btnDifRight, difficultyIndex < difficulties.Count - 1);
        SetArrow(comps.btnLeft, selectedIndex > 0);
        SetArrow(comps.btnRight, selectedIndex < builds.Count - 1);
        comps.btnContinue.interactable = !confirming && preview != null && config != null;
    }

    private void SetArrow(UnityEngine.UI.Button button, bool visible)
    {
        button.interactable = !confirming && visible;
        button.gameObject.SetActive(visible);
    }

    private void SwitchBuild(int direction)
    {
        if (confirming || builds.Count == 0) return;
        selectedIndex = Mathf.Clamp(selectedIndex + direction, 0, builds.Count - 1);
        RefreshBuild();
    }

    private void RefreshBuild()
    {
        var cfg = builds.Count > 0 ? builds[selectedIndex] : null;
        RefreshBuildName();
        RefreshDeckPreview();
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
        RefreshDifficulty();
    }

    private void RefreshDeckPreview()
    {
        preview = null;
        if (builds.Count > 0 && difficulties.Count > 0)
            BeginBuildHelper.TryBuild(builds[selectedIndex].Id, difficulties[difficultyIndex].Id, out preview);
        comps.btnCheck.interactable = preview != null;
    }

    private void RefreshBuildName()
    {
        var cfg = builds.Count > 0 ? builds[selectedIndex] : null;
        comps.txtName.text = cfg != null ? LanguageHelper.Get(cfg.Name) : LanguageHelper.Get("ui_beginbuild_no_deck");
    }

    private void OnCheck()
    {
        if (preview != null) ShowCardsPanel.ShowCards(preview, LanguageHelper.Get("title_beginbuild_deck_cards"));
    }

    private void OnContinue()
    {
        if (confirming || preview == null || difficulties.Count == 0 || OnConfirm == null) return;
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
        RefreshDifficulty();
        // 回调成功时场景入口会隐藏本面板并播放退出动画。
        if (OnConfirm(builds[selectedIndex].Id, difficulties[difficultyIndex].Id, seed)) return;
        confirming = false;
        RefreshDifficulty();
        TipHelper.Show(LanguageHelper.Get("tip_beginbuild_deck_invalid"));
    }
}
