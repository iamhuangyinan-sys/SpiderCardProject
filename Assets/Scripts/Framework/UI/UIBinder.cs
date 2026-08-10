using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Framework.UI
{
    /// <summary>
    /// UI 控件绑定条目
    /// </summary>
    [Serializable]
    public class UICompEntry
    {
        /// <summary> 前缀类型：btn / txt / img / mod </summary>
        public string prefix;

        /// <summary> C# 字段名（btn_Start → btnStart） </summary>
        public string fieldName;

        /// <summary> 组件类型名（mod 类型时使用，如 "Mod_Button"） </summary>
        public string typeName;

        /// <summary> 实际控件组件（Button / TMP_Text / Image / BaseModule） </summary>
        public Component component;
    }

    /// <summary>
    /// UI 控件绑定器 —— 挂载到面板根节点上
    ///
    /// 命名约定（以 GameObject 名前缀识别）：
    ///   btn_xxx → Button
    ///   txt_xxx → TMP_Text
    ///   img_xxx → Image
    ///   mod_xxx → BaseModule（模组，含自己的 comps）
    ///   txt_xxx → TMP_Text（TextMeshPro）
    ///   img_xxx → Image
    ///
    /// 使用方式：
    ///   1. 挂载到预制体根节点
    ///   2. Inspector 中点击 "Refresh" 自动扫描
    ///   3. 点击 "Export Code" 生成 comps 代码
    ///   4. 业务代码中用 comps.btnStart 等访问控件
    /// </summary>
    public class UIBinder : MonoBehaviour
    {
        /// <summary> 绑定的控件列表（Refresh 自动填充） </summary>
        public List<UICompEntry> uiComps = new();

        private BasePanel _panel;
        private bool _bound;

        private void Awake()
        {
            _panel = GetComponent<BasePanel>();
        }

        /// <summary>
        /// 扫描子物体，按前缀自动填充 uiComps（由编辑器 Refresh 按钮调用）
        /// </summary>
        public void Refresh()
        {
            uiComps.Clear();
            ScanChildren(transform);
        }

        private void ScanChildren(Transform parent)
        {
            foreach (Transform child in parent)
            {
                string name = child.name;

                UICompEntry entry = null;

                if (name.StartsWith("btn_"))
                {
                    var comp = child.GetComponent<Button>();
                    if (comp != null)
                        entry = new UICompEntry { prefix = "btn", component = comp };
                }
                else if (name.StartsWith("txt_"))
                {
                    var comp = child.GetComponent<TMP_Text>();
                    if (comp != null)
                        entry = new UICompEntry { prefix = "txt", component = comp };
                }
                else if (name.StartsWith("img_"))
                {
                    var comp = child.GetComponent<Image>();
                    if (comp != null)
                        entry = new UICompEntry { prefix = "img", component = comp };
                }
                else if (name.StartsWith("mod_"))
                {
                    var comp = child.GetComponent<BaseModule>();
                    if (comp != null)
                        entry = new UICompEntry { prefix = "mod", component = comp, typeName = comp.GetType().Name };
                }

                if (entry != null)
                {
                    entry.fieldName = PrefixToCamel(name);
                    uiComps.Add(entry);
                }

                // 模组不递归扫描（模组自己的 UIBinder 负责）
                if (entry == null || entry.prefix != "mod")
                    ScanChildren(child);
            }
        }

        /// <summary>
        /// 将 uiComps 中的控件赋值到面板 comps 字段中（由 BasePanel 在 Open 时调用）
        /// </summary>
        public void Bind()
        {
            if (_bound) return;
            _bound = true;

            var target = GetComponent<BasePanel>() ?? (Component)GetComponent<BaseModule>();
            if (target == null)
            {
                Debug.LogWarning("[UIBinder] 未找到 BasePanel 或 BaseModule");
                return;
            }

            // 从 target 的具体类型获取 comps（.gen.cs 中 new 覆盖了基类的 object comps）
            var targetType = target.GetType();
            var compsField = targetType.GetField("comps", BindingFlags.Public | BindingFlags.Instance);
            if (compsField == null)
            {
                Debug.LogWarning($"[UIBinder] {targetType.Name} 没有 comps 字段，请先点击 Export Code");
                return;
            }

            var compsObj = compsField.GetValue(target);
            if (compsObj == null) return;

            var compsType = compsObj.GetType();

            foreach (var entry in uiComps)
            {
                if (entry.component == null) continue;

                var field = compsType.GetField(entry.fieldName, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(compsObj, entry.component);
                }
                else
                {
                    Debug.LogWarning($"[UIBinder] comps 中找不到字段 '{entry.fieldName}'，请重新 Export Code");
                }
            }
        }

        /// <summary>
        /// btn_Start → btnStart
        /// </summary>
        public static string PrefixToCamel(string name)
        {
            int underscore = name.IndexOf('_');
            if (underscore < 0 || underscore >= name.Length - 1) return name;

            string prefix = name.Substring(0, underscore);       // "btn"
            string rest = name.Substring(underscore + 1);        // "Start"
            return prefix + rest;                                 // "btnStart"
        }

        private void OnDestroy()
        {
            _bound = false;
        }
    }
}
