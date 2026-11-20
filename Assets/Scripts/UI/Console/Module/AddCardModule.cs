using System;
using System.Collections.Generic;
using System.Linq;
using Framework.UI;
using TMPro;
using UnityEngine;

public partial class AddCardModule : BaseModule
{
#if UNITY_EDITOR
    // 列表保存选项索引，实际花色 / 点数 / 枚举值由选项表映射。
    private readonly List<int> suits = new() { 0 };
    private readonly List<int> ranks = new() { 0 };
    private readonly List<int> attributes = new();
    private readonly List<string> suitNames = new() { "红桃", "方块", "黑桃", "梅花" };
    private readonly List<string> rankNames = new()
    {
        "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "蜘蛛", "任意放下"
    };
    private readonly List<int> rankValues = new()
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, CardRankConst.Ignore, CardRankConst.Wild
    };
    private readonly List<string> placeNames = new() { "无" };
    private readonly List<int> placeValues = new() { 0 };
    private readonly List<string> flipNames = new() { "无" };
    private readonly List<int> flipValues = new() { 0 };
    private readonly List<string> attributeNames = new();
    private readonly List<int> attributeValues = new();

    protected override void OnBind()
    {
        foreach (var cfg in ConfigHelper.GetAll<PlaceSkillConfig>().OrderBy(cfg => cfg.Id, StringComparer.Ordinal))
            AddConfigOption(typeof(E_PlaceSkillEnum), cfg.Id, cfg.Name, placeNames, placeValues);
        foreach (var cfg in ConfigHelper.GetAll<FlipSkillConfig>().OrderBy(cfg => cfg.Id, StringComparer.Ordinal))
            AddConfigOption(typeof(E_FlipSkillEnum), cfg.Id, cfg.Name, flipNames, flipValues);
        foreach (var cfg in ConfigHelper.GetAll<CardAttributeConfig>().OrderBy(cfg => cfg.Id, StringComparer.Ordinal))
            AddConfigOption(typeof(E_CardAttributeEnum), cfg.Id, cfg.Name, attributeNames, attributeValues);

        SetOptions(comps.drpPlaceSkill, placeNames);
        SetOptions(comps.drpFlipSkill, flipNames);
        BindList(comps.listSuit, suits, suitNames);
        BindList(comps.listRank, ranks, rankNames);
        BindList(comps.listAttribute, attributes, attributeNames);
        comps.btnAddDrawPile.onClick.AddListener(OnClickAddDrawPile);
        comps.btnAddDiscardPile.onClick.AddListener(OnClickAddDiscardPile);
        comps.btnAddPocket.onClick.AddListener(OnClickAddPocket);
        comps.btnAddBuild.onClick.AddListener(OnClickAddBuild);
        comps.btnAddSuit.onClick.AddListener(OnClickAddSuit);
        comps.btnAddRank.onClick.AddListener(OnClickAddRank);
        comps.btnAddAttribute.onClick.AddListener(OnClickAddAttribute);
    }

    // 等子节点 Awake 完成后再创建列表项；隐藏再显示不会重置选择。
    private void Start() => RefreshLists();

    protected override void OnUnBind()
    {
        comps.btnAddDrawPile?.onClick.RemoveListener(OnClickAddDrawPile);
        comps.btnAddDiscardPile?.onClick.RemoveListener(OnClickAddDiscardPile);
        comps.btnAddPocket?.onClick.RemoveListener(OnClickAddPocket);
        comps.btnAddBuild?.onClick.RemoveListener(OnClickAddBuild);
        comps.btnAddSuit?.onClick.RemoveListener(OnClickAddSuit);
        comps.btnAddRank?.onClick.RemoveListener(OnClickAddRank);
        comps.btnAddAttribute?.onClick.RemoveListener(OnClickAddAttribute);
        if (comps.listSuit != null) comps.listSuit.OnItemRender = null;
        if (comps.listRank != null) comps.listRank.OnItemRender = null;
        if (comps.listAttribute != null) comps.listAttribute.OnItemRender = null;
    }

    private static void AddConfigOption(Type enumType, string id, string name,
        List<string> names, List<int> values)
    {
        if (!int.TryParse(id, out int value) || !Enum.IsDefined(enumType, value) || value == 0)
        {
            Debug.LogWarning($"[Console] 跳过未实现的选项：{enumType.Name} / {id}");
            return;
        }

        var language = ConfigHelper.Get<LanguageConfig>(name);
        names.Add(string.IsNullOrEmpty(language?.Chinese) ? name : language.Chinese);
        values.Add(value);
    }

    private static void SetOptions(TMP_Dropdown dropdown, List<string> names)
    {
        dropdown.ClearOptions();
        dropdown.AddOptions(names);
        dropdown.SetValueWithoutNotify(0);
        dropdown.RefreshShownValue();
    }

    private void BindList(ScrollList list, List<int> selections, List<string> names)
    {
        list.OnItemRender = (index, item) =>
        {
            ((AddCardItem)item).Render(names, selections[index],
                value => selections[index] = value,
                () =>
                {
                    selections.RemoveAt(index);
                    RefreshLists();
                });
        };
    }

    private void RefreshLists()
    {
        comps.listSuit.ItemCount = suits.Count;
        comps.listRank.ItemCount = ranks.Count;
        comps.listAttribute.ItemCount = attributes.Count;
        comps.btnAddSuit.interactable = suits.Count < 4;
        comps.btnAddRank.interactable = ranks.Count < CardRankConst.MaxCount;
        comps.btnAddAttribute.interactable = attributeNames.Count > 0;
    }

    private void OnClickAddSuit()
    {
        if (suits.Count >= 4) return;
        suits.Add(0);
        RefreshLists();
        comps.listSuit.ScrollTo(suits.Count - 1);
    }

    private void OnClickAddRank()
    {
        if (ranks.Count >= CardRankConst.MaxCount) return;
        ranks.Add(0);
        RefreshLists();
        comps.listRank.ScrollTo(ranks.Count - 1);
    }

    private void OnClickAddAttribute()
    {
        if (attributeNames.Count == 0) return;
        attributes.Add(0);
        RefreshLists();
        comps.listAttribute.ScrollTo(attributes.Count - 1);
    }

    private void OnClickAddDrawPile() => Submit(E_ConsoleCardTarget.DrawPile);
    private void OnClickAddBuild() => Submit(E_ConsoleCardTarget.Deck);
    private void OnClickAddDiscardPile() => Submit(E_ConsoleCardTarget.DiscardPile);
    private void OnClickAddPocket() => Submit(E_ConsoleCardTarget.Pocket);

    private void Submit(E_ConsoleCardTarget target)
    {
        var selectedRanks = ranks.Select(index => rankValues[index]).ToList();
        var selectedAttributes = attributes.Select(index => (E_CardAttributeEnum)attributeValues[index]).Distinct().ToList();
        if (!CardDef.TryCreate(suits, selectedRanks, (E_PlaceSkillEnum)placeValues[comps.drpPlaceSkill.value],
                selectedAttributes, out var def, out string error,
                (E_FlipSkillEnum)flipValues[comps.drpFlipSkill.value]))
        {
            Debug.LogWarning($"[Console] 添加失败：{error}");
            return;
        }

        string output = ConsoleCommandRegistry.AddCard(def, target);
        Debug.Log($"[Console] {output}");
    }
#else
    protected override void OnBind() { }
    protected override void OnUnBind() { }
#endif
}
