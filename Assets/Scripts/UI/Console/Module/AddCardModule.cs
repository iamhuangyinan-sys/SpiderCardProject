using Framework.UI;
using UnityEngine;

public partial class AddCardModule : BaseModule
{
    protected override void OnBind()
    {
        comps.btnAdd.onClick.AddListener(OnClickAdd);
        comps.btnAddBuild.onClick.AddListener(OnClickAddBuild);
    }

    protected override void OnUnBind()
    {
        comps.btnAdd?.onClick.RemoveListener(OnClickAdd);
        comps.btnAddBuild?.onClick.RemoveListener(OnClickAddBuild);
    }

    private void OnClickAdd() => Submit("add");

    private void OnClickAddBuild() => Submit("add_build");

    private void Submit(string command)
    {
        string[] fields =
        {
            comps.iptSuit.text.Trim(),
            comps.iptRank.text.Trim(),
            comps.iptPlaceSkill.text.Trim(),
            comps.iptFlipSkill.text.Trim(),
            comps.iptAttribute.text.Trim(),
        };

        foreach (string field in fields)
        {
            foreach (char character in field)
            {
                if (character != '_' && !char.IsWhiteSpace(character)) continue;

                Debug.LogWarning("[Console] 添加失败：表单字段内不能包含空白或下划线");
                return;
            }
        }

        ConsolePanel.ExecuteCommand($"{command} {string.Join("_", fields)}");
    }
}
