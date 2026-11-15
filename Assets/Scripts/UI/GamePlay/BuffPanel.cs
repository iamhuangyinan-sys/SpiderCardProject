using System.Collections.Generic;
using Framework.Event;
using Framework.UI;

public partial class BuffPanel : BasePanel
{
    private IReadOnlyList<KeyValuePair<E_BuffEnum, BuffState>> _buffs;

    protected override void OnOpen()
    {
        comps.listBuff.OnItemRender = RenderItem;
        OnShow();
    }

    protected override void OnShow()
    {
        EventManager.Instance.AddListener(E_EventEnum.OnBuffChanged, RefreshBuffs);
        RefreshBuffs();
    }

    protected override void OnHide()
    {
        EventManager.Instance.RemoveListener(E_EventEnum.OnBuffChanged, RefreshBuffs);
    }

    protected override void OnClose()
    {
        OnHide();
        comps.listBuff.OnItemRender = null;
        _buffs = null;
    }

    private void RefreshBuffs()
    {
        var previous = _buffs;
        _buffs = BuffManager.Instance.GetActiveBuffs();
        if (previous == null || previous.Count != _buffs.Count)
        {
            comps.listBuff.ItemCount = _buffs.Count;
            return;
        }

        for (int i = 0; i < _buffs.Count; i++)
        {
            if (previous[i].Key != _buffs[i].Key)
            {
                comps.listBuff.ItemCount = _buffs.Count;
                return;
            }
        }

        for (int i = 0; i < _buffs.Count; i++)
        {
            var before = previous[i].Value;
            var after = _buffs[i].Value;
            if (before.Count != after.Count || before.IsActivated != after.IsActivated)
                comps.listBuff.RefreshItem(i);
        }
    }

    private void RenderItem(int index, BaseListItem item)
    {
        if (item is BuffItem buffItem)
        {
            var buff = _buffs[index];
            buffItem.Bind(buff.Key, buff.Value);
        }
    }
}
