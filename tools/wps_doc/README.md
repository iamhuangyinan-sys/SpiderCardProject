# WPS 文档模板填充工具

用 Python + pywin32 通过 **COM** 驱动本地已安装的 **WPS 文字**(`KWPS.Application`)，
把数据填进 `.doc` / `.docx` 模板并另存为新文件。

> 注意:驱动的是 **WPS 本体**,不是 Microsoft Word。(本机同时装了 MS Office，
> `Word.Application` 指向的是 `WINWORD.EXE`，所以必须用 `KWPS.Application`。)

## 环境

| 项 | 值 |
|----|----|
| Python | `C:\Users\asus\AppData\Local\Programs\Python\Python311\python.exe` (3.11.3) |
| 依赖 | `pywin32` (已安装) |
| WPS | `C:\Users\asus\AppData\Local\Kingsoft\WPS Office\12.1.0.28505\office6` |

> 不要用 MSYS 的 `F:/MSYS/mingw64/bin/python.exe`，它装不了 pywin32。

## 两种填充方式(可同时用)

### 1. 占位符

在模板正文里写占位符，工具会自动替换:

```
姓名: {{姓名}}
公司: ${公司}
```

支持两种写法: `{{key}}` 和 `${key}`。

### 2. 书签(Bookmark)

在 WPS 里选中一段位置 → 插入 → 书签，书签名就是数据 key。
工具会往同名书签里写入内容。适合"填空格"式的模板。

## 用法

生成的文档统一放到 `private_doc/`(已在 `.gitignore` 中,不进版本库)。

```powershell
# 用 JSON 数据填充
python wps_doc_fill.py -t 模板.doc -o private_doc\输出.doc -d data.json

# 命令行直接给值(可重复)
python wps_doc_fill.py -t 模板.doc -o private_doc\输出.doc --set 姓名=张三 --set 日期=2026-09-13

# 输出 .docx
python wps_doc_fill.py -t 模板.docx -o private_doc\输出.docx -d data.json
```

参数:

| 参数 | 说明 |
|------|------|
| `-t, --template` | 模板文件路径(必填) |
| `-o, --output` | 输出文件路径(必填，按扩展名决定格式) |
| `-d, --data` | JSON 数据文件(UTF-8) |
| `--set KEY=VALUE` | 直接指定键值，可重复；会覆盖 JSON 中的同名项 |
| `--visible` | 显示 WPS 窗口(调试用) |
| `--keep-open` | 结束后不关闭 WPS |

## 输出格式

由 `-o` 的扩展名决定:

| 扩展名 | 格式 |
|--------|------|
| `.doc` | Word 97-2003 |
| `.docx` | Word 2007+ |
| `.rtf` | RTF |
| `.txt` | 纯文本 |
| `.pdf` | PDF |

## 数据文件格式

UTF-8 编码的 JSON,键 = 占位符/书签名:

```json
{
  "姓名": "张三",
  "公司": "某某科技有限公司",
  "日期": "2026-09-13"
}
```

## 自检

```powershell
python _e2e_test.py
```

会自动在 `private_doc/` 下生成 `demo_template.doc` → 填充 → 读回验证，输出类似:

```
[2] 填充结果: {... 'bookmarks_filled': 1, 'placeholders_replaced': 3 ...}
[3] 输出文档内容:
========= 入职登记表 =========
姓名: 张三
公司: 某某科技有限公司
日期: 2026-09-13
书签填充的内容
```

## 已知坑

- **WPS 的 `Find.Execute` 只认位置参数**，用命名参数(`FindText=`/`ReplaceWith=`)会静默不替换。
  脚本里已经改用位置参数。
- 书签名建议用 ASCII 或常用汉字；特殊符号可能不被接受。
- 替换文本里若包含占位符本身，可能被反复匹配；如需要请自行加转义。
- 首次运行会启动 WPS 进程，跑完会自动 `Quit()`。
