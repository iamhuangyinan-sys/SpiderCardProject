using TMPro;
using UnityEngine;

/// <summary>
/// 多语言文本 —— 挂在某个 TMP 的同一节点上，填一个 LanguageConfig 的 Id，
/// 运行时把 TMP 的文字换成当前语言的文案（预制体里写的中文只是编辑器预览，会被覆盖）。
///
/// 用法：
///   预制体里给 TMP 节点加本组件，Lang Id 填 ui_xxx（留空 = 不改文字）
///   代码里也可以 SetId("ui_xxx") 动态换；切语言后重新打开面板会自动刷新
///
/// 只负责「Id → 文案」，取词逻辑在 LanguageHelper。
/// </summary>
[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    /// <summary>多语言 Id（语言表主键；留空 = 保留预制体里的默认文字）</summary>
    [SerializeField] private string langId = string.Empty;

    /// <summary>每次启用都重新取一次文案（对象池复用 / 切语言后重新打开需要）</summary>
    [SerializeField] private bool refreshOnEnable = true;

    /// <summary>当前配置的多语言 Id</summary>
    public string LangId => langId;

    private TMP_Text _text;

    private void OnEnable()
    {
        if (refreshOnEnable) Refresh();
    }

    /// <summary>换一个 Id 并立刻生效</summary>
    public void SetId(string id)
    {
        langId = id;
        Refresh();
    }

    /// <summary>按当前 Id 重新取文案（Id 为空时什么都不做）</summary>
    public void Refresh()
    {
        if (string.IsNullOrEmpty(langId)) return;

        if (_text == null) _text = GetComponent<TMP_Text>();
        if (_text == null)
        {
            Debug.LogWarning($"[LocalizedText] {name} 上没有 TMP 组件，无法显示文案 {langId}", this);
            return;
        }

        _text.text = LanguageHelper.Get(langId);
    }
}
