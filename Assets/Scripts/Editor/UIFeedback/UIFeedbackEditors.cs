using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(UIFeedbackButton))]
[CanEditMultipleObjects]
public sealed class UIFeedbackButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("点击和导航使用 Button；所有视觉过渡由同节点的 Feedback 效果组件控制。", MessageType.Info);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Interactable"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_TargetGraphic"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Navigation"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("submitFeedbackDuration"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnClick"), true);
        serializedObject.FindProperty("m_Transition").enumValueIndex = (int)Selectable.Transition.None;
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("可选效果", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("勾选自动挂载，取消会移除该类型的效果组件及其参数（可 Undo）。具体参数在对应组件中调整。", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying || UIFeedbackPreview.IsActive))
        {
            EffectToggle<UIScaleEffect>("缩放");
            EffectToggle<UIOffsetEffect>("位移 / 下压");
            EffectToggle<UIOutlineEffect>("描边");
            EffectToggle<UIShadowEffect>("阴影");
            EffectToggle<UIBreathingEffect>("呼吸");
            EffectToggle<UIColorEffect>("颜色");
            EffectToggle<UIRaiseEffect>("悬停提层");
        }
    }

    private void EffectToggle<T>(string label) where T : UIFeedbackEffect
    {
        bool present = ((UIFeedbackButton)target).GetComponent<T>() != null;
        bool mixed = false;
        foreach (var item in targets)
            if ((((UIFeedbackButton)item).GetComponent<T>() != null) != present) mixed = true;

        EditorGUI.showMixedValue = mixed;
        EditorGUI.BeginChangeCheck();
        bool enabled = EditorGUILayout.ToggleLeft(label, present);
        bool changed = EditorGUI.EndChangeCheck();
        EditorGUI.showMixedValue = false;
        if (!changed) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName((enabled ? "Add " : "Remove ") + typeof(T).Name);
        foreach (var item in targets)
        {
            var button = (UIFeedbackButton)item;
            if (enabled)
            {
                if (button.GetComponent<T>() == null)
                    BindTarget(Undo.AddComponent<T>(button.gameObject), button);
            }
            else
            {
                foreach (var effect in button.GetComponents<T>())
                {
                    effect.Restore();
                    Undo.DestroyObjectImmediate(effect);
                }
            }
        }
        Undo.CollapseUndoOperations(group);
    }

    private static void BindTarget(UIFeedbackEffect effect, UIFeedbackButton button)
    {
        // 标准创建菜单的层级可直接绑定；自建层级不猜测目标，保留 Target 供用户指定。
        Object effectTarget = null;
        if (effect is UIScaleEffect) effectTarget = button.transform.Find("ScaleRoot") as RectTransform;
        else if (effect is UIBreathingEffect) effectTarget = button.transform.Find("ScaleRoot/BreatheRoot") as RectTransform;
        else if (effect is UIOffsetEffect) effectTarget = button.transform.Find("ScaleRoot/BreatheRoot/Surface") as RectTransform;
        else if (effect is UIShadowEffect)
        {
            var node = button.transform.Find("ScaleRoot/BreatheRoot/Shadow");
            if (node != null) effectTarget = node.GetComponent<Image>();
        }
        else if (effect is UIColorEffect) effectTarget = button.targetGraphic;
        else if (effect is UIRaiseEffect) effectTarget = button.transform;
        else if (effect is UIOutlineEffect && button.targetGraphic != null)
        {
            var outline = button.targetGraphic.GetComponent<Outline>();
            if (outline == null)
            {
                outline = Undo.AddComponent<Outline>(button.targetGraphic.gameObject);
                outline.effectColor = Color.clear;
                outline.effectDistance = new Vector2(2f, 2f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(outline);
            }
            effectTarget = outline;
        }
        if (effectTarget == null) return;
        var data = new SerializedObject(effect);
        data.FindProperty("target").objectReferenceValue = effectTarget;
        data.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(UIFeedbackController))]
public sealed class UIFeedbackControllerEditor : Editor
{
    private bool previewAttention;

    public override void OnInspectorGUI()
    {
        var controller = (UIFeedbackController)target;
        using (new EditorGUI.DisabledScope(UIFeedbackPreview.IsActive)) DrawDefaultInspector();
        DrawWarnings(controller);
        EditorGUILayout.Space();
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("当前状态", controller.State.ToString());
            bool attention = EditorGUILayout.Toggle("需要提醒 / 呼吸", controller.Attention);
            if (attention != controller.Attention) controller.SetAttention(attention);
            if (GUILayout.Button("重新应用参数")) controller.RefreshEffects();
            return;
        }

        EditorGUILayout.LabelField("状态预览（不播放过渡）", EditorStyles.boldLabel);
        previewAttention = EditorGUILayout.Toggle("普通状态预览呼吸峰值", previewAttention);
        using (new EditorGUI.DisabledScope(!controller.gameObject.activeInHierarchy))
        {
            EditorGUILayout.BeginHorizontal();
            PreviewButton(controller, E_UIFeedbackState.Normal, "普通");
            PreviewButton(controller, E_UIFeedbackState.Hovered, "悬停");
            PreviewButton(controller, E_UIFeedbackState.Pressed, "按下");
            PreviewButton(controller, E_UIFeedbackState.Focused, "焦点");
            PreviewButton(controller, E_UIFeedbackState.Disabled, "禁用");
            EditorGUILayout.EndHorizontal();
        }
        if (UIFeedbackPreview.IsPreviewing(controller))
        {
            EditorGUILayout.HelpBox("正在预览。停止预览后可修改参数；切换选择或保存时自动还原。", MessageType.Info);
            if (GUILayout.Button("停止预览并还原")) UIFeedbackPreview.Stop();
        }
    }

    private void PreviewButton(UIFeedbackController controller, E_UIFeedbackState state, string label)
    {
        if (GUILayout.Button(label)) UIFeedbackPreview.Show(controller, state, previewAttention);
    }

    internal static void DrawWarnings(UIFeedbackController controller)
    {
        if (controller.GetComponent<UIFeedbackButton>() != null && controller.GetComponent<UIFeedbackHoverInput>() != null)
            EditorGUILayout.HelpBox("FeedbackButton 已提供输入，请移除 HoverInput，避免多个输入源。", MessageType.Warning);
        var selectable = controller.GetComponent<Selectable>();
        if (selectable != null && !(selectable is UIFeedbackButton))
            EditorGUILayout.HelpBox("普通 Button 不会驱动 Feedback。试验时请新建 FeedbackButton，或由宿主手动传入状态。", MessageType.Warning);

        var owners = new Dictionary<(Component target, string property), UIFeedbackEffect>();
        foreach (var effect in controller.GetComponents<UIFeedbackEffect>())
        {
            if (!effect.enabled) continue;
            var bindings = new List<UIFeedbackBinding>();
            effect.CollectBindings(bindings);
            if (bindings.Count == 0 && !(effect is UIRaiseEffect))
                EditorGUILayout.HelpBox(effect.GetType().Name + " 尚未指定 Target。", MessageType.Warning);
            foreach (var binding in bindings)
            {
                var key = (binding.Target, binding.PropertyPath);
                if (owners.TryGetValue(key, out var owner))
                    EditorGUILayout.HelpBox($"{owner.GetType().Name} 和 {effect.GetType().Name} 同时写入 {binding.Target.name} 的 {binding.PropertyPath}。请分开目标节点。", MessageType.Warning);
                else owners.Add(key, effect);
            }
        }
    }
}

[CustomEditor(typeof(UIFeedbackEffect), true)]
[CanEditMultipleObjects]
public sealed class UIFeedbackEffectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (UIFeedbackPreview.IsActive)
        {
            EditorGUILayout.HelpBox("先停止状态预览，再修改参数。", MessageType.Info);
            if (GUILayout.Button("停止预览")) UIFeedbackPreview.Stop();
        }

        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledScope(UIFeedbackPreview.IsActive)) DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck() && Application.isPlaying)
        {
            // 先恢复旧目标，再捕获新目标，避免把已经放大的值当作新的基准。
            foreach (var item in targets)
            {
                var effect = (UIFeedbackEffect)item;
                effect.Restore();
                effect.GetComponent<UIFeedbackController>().RefreshEffects();
            }
        }

        if (targets.Length == 1)
            UIFeedbackControllerEditor.DrawWarnings(((UIFeedbackEffect)target).GetComponent<UIFeedbackController>());
    }
}
