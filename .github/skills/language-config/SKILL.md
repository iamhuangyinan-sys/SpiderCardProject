---
name: language-config
description: 查询和修改 SpiderCard 的 LanguageConfig.xlsx。用于按 ID 或关键词查询、新增、删除和修改中英文文案。
---

# LanguageConfig

从项目根目录运行 `python tools/language_config.py --help`。工具只需 Python 3 标准库。

1. 用 `show --id <Id>` 查单条，或用 `show --contains <关键词>` 在 ID 和中英文中查找上下文。无参数的 `show` 输出全表 JSON。
2. 将变更写成 UTF-8 JSON 数组。每项含 `Id` 和 `op`。`update` 只改补丁提供的字段。`add` 追加新 ID，需同时提供 `Chinese` 和 `English`。`delete` 只提供现有 `Id`，会删除整行。参考附近文案统一术语，保留格式占位符、富文本标签和有意义的换行。
3. 用 `edit <patch.json>` 预览，核对 ID、语言和前后文本，再用 `edit <patch.json> --write` 写回。补丁文件可放在临时目录。
4. 用 `check` 校验表结构和 ID，并检查 Unity 运行时 JSON 是否与 Excel 一致。需要同步时可在 Unity 选择 **Tools > Export All Configs**，随后再次检查。该菜单会导出所有配表，执行前检查是否有其他未完成的配表改动。若本次只修改 Excel，报告运行时 JSON 的同步状态。

```json
[
  {"op": "update", "Id": "tip_discard_empty", "English": "The discard pile is empty"},
  {"op": "add", "Id": "ui_example_title", "Chinese": "示例", "English": "Example"},
  {"op": "delete", "Id": "ui_obsolete_title"}
]
```