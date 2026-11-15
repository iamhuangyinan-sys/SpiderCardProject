using Framework.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public static class ScrollListMenu
{
    private const string UndoName = "Create Scroll List";

    [MenuItem("GameObject/UI/Scroll List", false, 2032)]
    private static void CreateFromMenu(MenuCommand command)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);

        GameObject context = command.context as GameObject;
        if (context == null) context = Selection.activeGameObject;
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        Transform parent = FindParent(context, prefabStage);
        var root = CreateList(parent);
        Undo.RegisterCreatedObjectUndo(root, UndoName);

        if (prefabStage == null && Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystem.AddComponent<InputSystemUIInputModule>();
#else
            eventSystem.AddComponent<StandaloneInputModule>();
#endif
            Undo.RegisterCreatedObjectUndo(eventSystem, UndoName);
        }

        Selection.activeGameObject = root;
        Undo.CollapseUndoOperations(group);
    }

    private static Transform FindParent(GameObject context, PrefabStage prefabStage)
    {
        if (prefabStage != null)
        {
            if (context != null && context.scene == prefabStage.scene && context.transform is RectTransform)
                return context.transform;

            var prefabRoot = prefabStage.prefabContentsRoot;
            var prefabCanvas = prefabRoot.GetComponentInChildren<Canvas>();
            return prefabCanvas != null ? prefabCanvas.transform : prefabRoot.transform;
        }

        if (context != null && context.transform is RectTransform && context.GetComponentInParent<Canvas>() != null)
            return context.transform;

        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null) return canvas.transform;

        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = LayerMask.NameToLayer("UI");
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        Undo.RegisterCreatedObjectUndo(canvasObject, UndoName);
        return canvasObject.transform;
    }

    private static GameObject CreateList(Transform parent)
    {
        var root = DefaultControls.CreateScrollView(new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
        });
        root.name = GameObjectUtility.GetUniqueNameForSibling(parent, "list_Scroll");
        root.transform.SetParent(parent, false);
        int uiLayer = LayerMask.NameToLayer("UI");
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = uiLayer;

        var scrollRect = root.GetComponent<ScrollRect>();
        var horizontalScrollbar = scrollRect.horizontalScrollbar;
        scrollRect.horizontalScrollbar = null;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        Object.DestroyImmediate(horizontalScrollbar.gameObject);

        var content = scrollRect.content;
        content.anchorMin = Vector2.up;
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;

        var list = root.AddComponent<ScrollList>();
        using var serializedList = new SerializedObject(list);
        serializedList.FindProperty("_scrollRect").objectReferenceValue = scrollRect;
        serializedList.FindProperty("_content").objectReferenceValue = content;
        serializedList.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }
}
