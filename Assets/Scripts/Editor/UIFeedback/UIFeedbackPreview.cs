using System.Globalization;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>使用 AnimationMode 记录原值；选中其它对象、保存、重编译或进入 Play 时自动退出。</summary>
[InitializeOnLoad]
public static class UIFeedbackPreview
{
    private static UIFeedbackController active;
    private static bool ownsAnimationMode;

    public static bool IsActive => ownsAnimationMode;
    public static bool IsPreviewing(UIFeedbackController controller) => ownsAnimationMode && active == controller;

    static UIFeedbackPreview()
    {
        Selection.selectionChanged += Stop;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.quitting += Stop;
        EditorApplication.playModeStateChanged += _ => Stop();
        EditorSceneManager.sceneSaving += (_, __) => Stop();
        PrefabStage.prefabSaving += _ => Stop();
    }

    public static bool Show(UIFeedbackController controller, E_UIFeedbackState state, bool attention)
    {
        Stop();
        if (controller == null || Application.isPlaying || !controller.gameObject.activeInHierarchy) return false;
        if (AnimationMode.InAnimationMode())
        {
            Debug.LogWarning("请先停止 Animation 窗口或其它工具的预览，再预览 UI 效果。", controller);
            return false;
        }

        active = controller;
        AnimationMode.StartAnimationMode();
        ownsAnimationMode = true;
        try
        {
            var effects = controller.GetComponents<UIFeedbackEffect>();
            var bindings = new List<UIFeedbackBinding>();
            foreach (var effect in effects)
                if (effect.enabled) effect.CollectBindings(bindings);

            foreach (var binding in bindings) Record(binding, controller.transform);
            var context = new UIFeedbackContext(state, attention, controller.UseUnscaledTime);
            foreach (var effect in effects) effect.Apply(context, true);
            SceneView.RepaintAll();
            return true;
        }
        catch
        {
            Stop();
            throw;
        }
    }

    private static void Record(UIFeedbackBinding binding, Transform root)
    {
        var serialized = new SerializedObject(binding.Target);
        var property = serialized.FindProperty(binding.PropertyPath);
        if (property == null) throw new System.InvalidOperationException("找不到预览属性：" + binding.PropertyPath);
        string[] channels = property.propertyType switch
        {
            SerializedPropertyType.Vector2 => new[] { "x", "y" },
            SerializedPropertyType.Vector3 => new[] { "x", "y", "z" },
            SerializedPropertyType.Color => new[] { "r", "g", "b", "a" },
            _ => throw new System.InvalidOperationException("不支持的预览属性：" + binding.PropertyPath)
        };
        foreach (string channel in channels)
        {
            var leaf = property.FindPropertyRelative(channel);
            var curve = EditorCurveBinding.FloatCurve(
                AnimationUtility.CalculateTransformPath(binding.Target.transform, root),
                binding.Target.GetType(), leaf.propertyPath);
            AnimationMode.AddPropertyModification(curve, new PropertyModification
            {
                target = binding.Target,
                propertyPath = leaf.propertyPath,
                value = leaf.floatValue.ToString("R", CultureInfo.InvariantCulture)
            }, true);
        }
    }

    public static void Stop()
    {
        if (!ownsAnimationMode) return;
        if (active != null) active.RestoreEffects();
        active = null;
        ownsAnimationMode = false;
        if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        SceneView.RepaintAll();
    }
}

public sealed class UIFeedbackPreviewSaveHook : AssetModificationProcessor
{
    private static string[] OnWillSaveAssets(string[] paths)
    {
        UIFeedbackPreview.Stop();
        return paths;
    }
}
