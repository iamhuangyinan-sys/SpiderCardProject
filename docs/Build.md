# 项目打包

Unity 菜单选择 **Tools → 项目打包**，填写版本号后点击 **开始打包**。
窗口默认读取当前打包信息，构建平台固定为 Windows 64 位。

## 输出目录

输出根目录固定为 `F:\Workspace\Build`，可执行文件为 `SpiderCard.exe`。
例如填写 `0.3.0`，完整构建产物放在 `F:\Workspace\Build\0.3.0`。
如果同名文件或目录已存在，依次尝试 `0.3.0.1`、`0.3.0.2`，使用第一个空闲名称。
后缀只用于目录去重，JSON、游戏界面和 Unity 项目版本仍为填写的 `0.3.0`。

## 打包信息

`Assets/Resources/BuildInfo.json` 是游戏显示版本的来源：

```json
{
  "version": "0.3.0"
}
```

`BuildInfoHelper` 统一读取该资源；开始界面打开、重新显示时刷新版本文字。
打包成功后，JSON 和 `PlayerSettings.bundleVersion` 保留本次填写的版本，需随项目一起提交。

## 执行顺序

1. 校验版本号、Editor 状态、Windows 构建模块、当前平台、场景和版本文件。
2. 提示保存修改过的场景；取消保存则终止，不修改版本。
3. 检查输出目录可写，并通过根目录的 `.spidercard-build.lock` 阻止工具并发构建；构建结束后自动删除锁文件，失败或取消时也会清理。
4. 确定空闲的版本目录，备份旧版本信息。
5. 写入并同步导入 JSON，更新并保存 Unity 项目版本。
6. 创建本次输出目录，按 Build Settings 中启用的场景及其顺序执行构建。
7. 检查 BuildReport 和可执行文件，成功后显示输出目录。

构建过程中禁止重复点击。运行模式、脚本编译和资源导入期间不能开始构建。
当前平台需在 **File → Build Profiles** 中切换到 Windows 64 位。

## 失败处理

修改版本后的可捕获失败或取消会恢复原 JSON 内容及 Unity 项目版本。
已创建的产物目录保留 `BUILD_FAILED.txt` 和 `Build.log`，以后构建会避开该目录。
成功构建也会保存 `Build.log`。若磁盘本身不可写，错误会显示在 Console 中。
Editor 被强制终止或操作系统崩溃时无法执行回滚，应检查项目版本和残留输出目录。

当前工具直接调用 Unity Editor 构建 API，不依赖 Unity CLI 或 Pipeline 包。
