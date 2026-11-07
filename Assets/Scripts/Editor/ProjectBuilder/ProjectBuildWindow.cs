using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class ProjectBuildWindow : EditorWindow
{
    [SerializeField] private string version;
    [SerializeField] private string lastOutputDirectory;
    private bool buildQueued;
    private string status;
    private MessageType statusType;

    [MenuItem("Tools/项目打包")]
    public static void Open()
    {
        GetWindow<ProjectBuildWindow>("项目打包");
    }

    private void OnEnable()
    {
        minSize = new Vector2(500, 330);
        if (!string.IsNullOrEmpty(version)) return;
        try
        {
            version = ProjectBuilder.ReadVersion();
        }
        catch (Exception exception)
        {
            status = $"读取当前版本失败：{exception.Message}";
            statusType = MessageType.Error;
        }
    }

    private void OnInspectorUpdate() => Repaint();

    private void OnGUI()
    {
        bool busy = buildQueued || ProjectBuilder.IsBuilding || BuildPipeline.isBuildingPlayer;
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("SpiderCard 项目打包", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        using (new EditorGUI.DisabledScope(busy))
            version = EditorGUILayout.TextField("版本号", version);
        EditorGUILayout.LabelField("构建平台", "Windows 64 位");
        EditorGUILayout.LabelField("输出根目录", ProjectBuilder.OutputRoot);

        string outputDirectory = null;
        string validationError = null;
        try
        {
            outputDirectory = ProjectBuildPaths.GetAvailableDirectory(ProjectBuilder.OutputRoot, version);
        }
        catch (Exception exception)
        {
            validationError = exception.Message;
        }
        EditorGUILayout.LabelField("本次输出目录");
        EditorGUILayout.SelectableLabel(outputDirectory ?? "—", EditorStyles.helpBox, GUILayout.Height(38));
        string directoryToOpen = Directory.Exists(lastOutputDirectory) ? lastOutputDirectory : ProjectBuilder.OutputRoot;
        using (new EditorGUI.DisabledScope(busy || !Directory.Exists(directoryToOpen)))
        {
            if (GUILayout.Button("打开输出目录"))
                EditorUtility.RevealInFinder(directoryToOpen);
        }
        EditorGUILayout.HelpBox("重名目录自动追加后缀，游戏版本始终使用填写的版本号。", MessageType.Info);
        if (validationError != null)
            EditorGUILayout.HelpBox(validationError, MessageType.Warning);
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            EditorGUILayout.HelpBox("请退出运行模式，并等待脚本编译和资源导入完成。", MessageType.Warning);

        EditorGUILayout.Space(5);
        using (new EditorGUI.DisabledScope(busy || validationError != null ||
                   EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
        {
            if (GUILayout.Button(busy ? "正在打包…" : "开始打包", GUILayout.Height(32)))
            {
                buildQueued = true;
                // 先让窗口刷新，再开始同步构建。
                EditorApplication.delayCall += StartBuild;
            }
        }

        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, statusType);
    }

    private void StartBuild()
    {
        if (this == null) return;
        try
        {
            version = ProjectBuildPaths.NormalizeVersion(version);
            string result = ProjectBuilder.Build(version);
            if (result == null)
            {
                status = "已取消打包，版本信息未修改。";
                statusType = MessageType.Info;
                return;
            }

            lastOutputDirectory = result;
            status = $"打包成功：{result}";
            statusType = MessageType.Info;
            if (EditorUtility.DisplayDialog("打包完成", $"版本：{version}\n输出目录：{result}", "打开文件夹", "关闭"))
                EditorUtility.RevealInFinder(result);
        }
        catch (Exception exception)
        {
            status = exception.Message;
            statusType = MessageType.Error;
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("打包失败", exception.Message, "关闭");
        }
        finally
        {
            buildQueued = false;
            if (this != null) Repaint();
        }
    }
}
