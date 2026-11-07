using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>只在用户执行菜单时创建新对象，不转换或修改已有按钮。</summary>
public static class UIFeedbackButtonMenu
{
    [MenuItem("GameObject/UI/Feedback Button", false, 2031)]
    private static void CreateFromMenu(MenuCommand command)
    {
        UIFeedbackPreview.Stop();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Feedback Button");
        var context = command.context as GameObject;
        if (context == null) context = Selection.activeGameObject;
        Transform parent = context != null && context.transform is RectTransform ? context.transform : null;
        if (parent == null)
        {
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                go.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                go.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            }
            parent = canvas.transform;
        }

        var button = Create(parent);
        Undo.RegisterCreatedObjectUndo(button, "Create Feedback Button");
        if (PrefabStageUtility.GetCurrentPrefabStage() == null && Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var input = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            input.AddComponent<InputSystemUIInputModule>();
#else
            input.AddComponent<StandaloneInputModule>();
#endif
            Undo.RegisterCreatedObjectUndo(input, "Create EventSystem");
        }
        Selection.activeGameObject = button;
        Undo.CollapseUndoOperations(group);
    }

    /// <summary>编辑器创建工具和验证共用的工厂；不写入任何预制体资产。</summary>
    public static GameObject Create(Transform parent)
    {
        var root = new GameObject("btn_Feedback", typeof(RectTransform));
        root.SetActive(false);
        root.transform.SetParent(parent, false);
        root.layer = LayerMask.NameToLayer("UI");
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(280f, 72f);
        var hitArea = root.AddComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;

        var scale = Child("ScaleRoot", root.transform);
        var breathe = Child("BreatheRoot", scale);
        var shadowRect = Child("Shadow", breathe);
        shadowRect.anchoredPosition = new Vector2(4f, -4f);
        var surface = Child("Surface", breathe);
        var backgroundRect = Child("Background", surface);
        var content = Child("Content", surface);
        content.offsetMin = new Vector2(20f, 8f);
        content.offsetMax = new Vector2(-20f, -8f);

        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var shadow = shadowRect.gameObject.AddComponent<Image>();
        shadow.sprite = sprite;
        shadow.type = Image.Type.Sliced;
        shadow.color = Color.clear; // 未选择阴影效果时不显示。
        shadow.raycastTarget = false;
        var background = backgroundRect.gameObject.AddComponent<Image>();
        background.sprite = sprite;
        background.type = Image.Type.Sliced;
        background.color = new Color(0.22f, 0.6f, 0.215f);
        background.raycastTarget = false;

        var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        layout.spacing = 12f;
        var labelRect = Child("Label", content);
        var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = "Button";
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 32f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        // RequireComponent 会随 Button 自动添加 Controller；默认不挂载任何效果。
        var button = root.AddComponent<UIFeedbackButton>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;
        root.SetActive(true);
        return root;
    }

    private static RectTransform Child(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

}
