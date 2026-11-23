using System;
using System.Collections.Generic;
using Framework.UI;

public partial class AddCardItem : BaseListItem
{
#if UNITY_EDITOR || SPIDERCARD_TEST_BUILD
    private Action<int> onChanged;
    private Action onDelete;

    public override void OnActivate()
    {
        comps.drpItem.onValueChanged.AddListener(OnChanged);
        comps.btnDelete.onClick.AddListener(OnDelete);
    }

    public override void OnRecycle()
    {
        comps.drpItem.Hide();
        comps.drpItem.onValueChanged.RemoveListener(OnChanged);
        comps.btnDelete.onClick.RemoveListener(OnDelete);
        onChanged = null;
        onDelete = null;
    }

    public void Render(List<string> options, int selection, Action<int> changed, Action delete)
    {
        onChanged = null;
        onDelete = null;
        comps.drpItem.Hide();
        comps.drpItem.ClearOptions();
        comps.drpItem.AddOptions(options);
        comps.drpItem.SetValueWithoutNotify(selection);
        comps.drpItem.RefreshShownValue();
        onChanged = changed;
        onDelete = delete;
    }

    private void OnChanged(int value) => onChanged?.Invoke(value);
    private void OnDelete() => onDelete?.Invoke();
#else
    public override void OnActivate() { }
    public override void OnRecycle() { }
#endif
}
