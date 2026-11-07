using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>项目窗口构建入口：先检查，再写版本、创建目录，最后构建。</summary>
public static class ProjectBuilder
{
    public const string OutputRoot = @"F:\Workspace\Build";
    public const string PlayerFileName = "SpiderCard.exe";
    public const BuildTarget Target = BuildTarget.StandaloneWindows64;

    public static bool IsBuilding { get; private set; }

    private static string BuildInfoPath => Path.GetFullPath(
        Path.Combine(Application.dataPath, "..", BuildInfoHelper.AssetPath));

    public static string ReadVersion()
    {
        return BuildInfoHelper.Parse(File.ReadAllText(BuildInfoPath)).version;
    }

    /// <returns>成功的产物目录；用户取消保存场景时返回 null。</returns>
    public static string Build(string version)
    {
        version = ProjectBuildPaths.NormalizeVersion(version);
        if (IsBuilding || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("已有构建正在进行，请等待完成。");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("请退出运行模式，并等待脚本编译和资源导入完成后再打包。");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, Target))
            throw new InvalidOperationException("当前 Editor 缺少 Windows 构建支持模块。");
        if (EditorUserBuildSettings.activeBuildTarget != Target)
            throw new InvalidOperationException("请先在 File → Build Profiles 中切换到 Windows 64 位平台。");

        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (scenes.Length == 0 || scenes.Any(path => string.IsNullOrEmpty(path) || !File.Exists(path)))
            throw new InvalidOperationException("请检查构建场景列表：至少启用一个场景，且所有已启用的场景文件都必须存在。");

        // 在修改版本前确认原 JSON 可读、可恢复，且版本设置文件可写。
        byte[] previousJson = File.ReadAllBytes(BuildInfoPath);
        BuildInfoHelper.Parse(File.ReadAllText(BuildInfoPath));
        EnsureWritableFile(BuildInfoPath);
        EnsureWritableFile(Path.GetFullPath(Path.Combine(Application.dataPath, "../ProjectSettings/ProjectSettings.asset")));

        IsBuilding = true;
        try
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return null;

            Directory.CreateDirectory(OutputRoot);
            // 同一输出根目录的工具构建互斥；关闭句柄时自动删除锁文件，覆盖成功、失败和取消路径。
            using var outputLock = new FileStream(Path.Combine(OutputRoot, ".spidercard-build.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            string outputDirectory = ProjectBuildPaths.GetAvailableDirectory(OutputRoot, version);
            return BuildIntoDirectory(version, scenes, previousJson, outputDirectory);
        }
        finally
        {
            IsBuilding = false;
        }
    }

    private static string BuildIntoDirectory(string version, string[] scenes, byte[] previousJson, string outputDirectory)
    {
        string previousVersion = PlayerSettings.bundleVersion;
        bool versionTouched = false;
        bool directoryCreated = false;
        bool capturingLog = false;
        var log = new StringBuilder();
        log.AppendLine($"Version: {version}\nTarget: {Target}\nOutput: {outputDirectory}\nStarted: {DateTime.Now:O}");
        Application.LogCallback capture = (message, stackTrace, type) =>
        {
            log.AppendLine($"[{type}] {message}");
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                log.AppendLine(stackTrace);
        };

        try
        {
            // 先标记，确保即使写入中途失败也尝试恢复原文件。
            versionTouched = true;
            string json = JsonUtility.ToJson(new BuildInfoData { version = version }, true) + "\n";
            File.WriteAllText(BuildInfoPath, json, new UTF8Encoding(false));
            ImportBuildInfo();
            var importedInfo = AssetDatabase.LoadAssetAtPath<TextAsset>(BuildInfoHelper.AssetPath);
            if (importedInfo == null || BuildInfoHelper.Parse(importedInfo.text).version != version)
                throw new InvalidOperationException("新的打包信息导入失败，已停止构建。");

            PlayerSettings.bundleVersion = version;
            AssetDatabase.SaveAssets();

            // 锁内再次检查，绝不复用已有目录或文件。
            if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory))
                throw new IOException($"输出路径已被占用：{outputDirectory}");
            Directory.CreateDirectory(outputDirectory);
            directoryCreated = true;
            Application.logMessageReceived += capture;
            capturingLog = true;

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(outputDirectory, PlayerFileName),
                target = Target,
                options = BuildOptions.None
            });

            if (report == null)
                throw new InvalidOperationException("Unity 未返回构建报告。");
            var summary = report.summary;
            log.AppendLine($"Result: {summary.result}\nSize: {summary.totalSize} bytes\n" +
                $"Duration: {summary.totalTime}\nErrors: {summary.totalErrors}\nWarnings: {summary.totalWarnings}");
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"构建结果：{summary.result}，错误数：{summary.totalErrors}。请查看 Console 和 Build.log。");
            if (!File.Exists(Path.Combine(outputDirectory, PlayerFileName)))
                throw new IOException("构建报告成功，但没有找到 SpiderCard.exe。");

            Debug.Log($"[ProjectBuild] 打包成功，版本 {version}，输出：{outputDirectory}");
            return outputDirectory;
        }
        catch (Exception exception)
        {
            log.AppendLine(exception.ToString());
            string rollbackErrors = versionTouched ? RestoreVersion(previousJson, previousVersion) : "";
            string message = exception.Message + "\n\n" +
                (string.IsNullOrEmpty(rollbackErrors) ? "项目版本信息已恢复。" : $"版本信息恢复异常：\n{rollbackErrors}");
            if (directoryCreated)
            {
                message += $"\n失败产物保留在：{outputDirectory}";
                TryWriteDiagnostic(Path.Combine(outputDirectory, "BUILD_FAILED.txt"), message);
            }
            throw new InvalidOperationException(message, exception);
        }
        finally
        {
            if (capturingLog)
                Application.logMessageReceived -= capture;
            if (directoryCreated)
            {
                log.AppendLine($"Finished: {DateTime.Now:O}");
                TryWriteDiagnostic(Path.Combine(outputDirectory, "Build.log"), log.ToString());
            }
        }
    }

    private static void EnsureWritableFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
    }

    private static void ImportBuildInfo()
    {
        AssetDatabase.ImportAsset(BuildInfoHelper.AssetPath,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    private static string RestoreVersion(byte[] previousJson, string previousVersion)
    {
        var errors = new StringBuilder();
        try
        {
            File.WriteAllBytes(BuildInfoPath, previousJson);
            ImportBuildInfo();
        }
        catch (Exception exception)
        {
            errors.AppendLine($"JSON：{exception.Message}");
        }
        try
        {
            PlayerSettings.bundleVersion = previousVersion;
            AssetDatabase.SaveAssets();
        }
        catch (Exception exception)
        {
            errors.AppendLine($"Unity 版本：{exception.Message}");
        }
        return errors.ToString();
    }

    private static void TryWriteDiagnostic(string path, string content)
    {
        try
        {
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[ProjectBuild] 无法写入日志 {path}：{exception.Message}");
        }
    }
}
