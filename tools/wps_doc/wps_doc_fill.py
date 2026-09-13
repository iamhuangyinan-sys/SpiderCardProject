# -*- coding: utf-8 -*-
"""
用 WPS 文字(KWPS.Application)通过 COM 填充本地 .doc / .docx 模板。

驱动的是 WPS 本体,不是 Microsoft Word。

支持的填充方式(可同时使用):
  1. 书签(Bookmark) —— 模板里插入书签,书签名 = 数据 key
  2. 占位符        —— 模板里写 {{key}} 或 ${key}

用法示例:
  # 用 JSON 数据填充
  python wps_doc_fill.py -t 模板.doc -o 输出.doc -d data.json

  # 命令行直接给值(可重复)
  python wps_doc_fill.py -t 模板.doc -o 输出.doc --set 姓名=张三 --set 日期=2026-09-13

  # 输出 .docx
  python wps_doc_fill.py -t 模板.docx -o 输出.docx -d data.json

data.json 格式(UTF-8):
  { "姓名": "张三", "公司": "某某科技", "日期": "2026-09-13" }
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import traceback

import win32com.client as win32

# ---- Word 兼容常量 ----
WD_REPLACE_ALL = 2          # 替换全部
WD_FIND_CONTINUE = 1        # 到文档末尾后继续(从头开始)
WD_FORMAT = {
    ".doc": 0,      # wdFormatDocument
    ".docx": 12,    # wdFormatXMLDocument
    ".rtf": 6,      # wdFormatRTF
    ".txt": 2,      # wdFormatText
    ".pdf": 17,     # wdFormatPDF
}
WD_DO_NOT_SAVE = 0          # 关闭时不保存


def _log(msg: str) -> None:
    print(msg)


def get_wps_app(visible: bool = False):
    """连接(或新建)一个 WPS 文字 COM 实例。"""
    app = None
    # 优先 DispatchEx:新开独立进程,不打扰用户已打开的 WPS 窗口
    for progid in ("KWPS.Application", "Kwps.Application", "wps.Application"):
        try:
            app = win32.DispatchEx(progid)
            break
        except Exception:
            app = None
    if app is None:
        # 退回普通 Dispatch
        for progid in ("KWPS.Application", "Kwps.Application", "wps.Application"):
            try:
                app = win32.Dispatch(progid)
                break
            except Exception:
                app = None
    if app is None:
        raise RuntimeError("无法启动 WPS 文字(KWPS.Application),请确认已安装 WPS Office。")
    app.Visible = visible
    app.DisplayAlerts = False
    return app


def _replace_in_range(rng, find_text: str, replace_text: str) -> int:
    """在一个 Range 内逐个替换,返回替换次数。

    注意:WPS 的 Find.Execute 对「命名参数」支持不稳定,必须用位置参数。
    签名与 Word 一致:
      Execute(FindText, MatchCase, MatchWholeWord, MatchWildcards, MatchSoundsLike,
              MatchAllWordForms, Forward, Wrap, Format, ReplaceWith, Replace, ...)
      Wrap:    0 = wdFindStop(不折返)   1 = wdFindContinue
      Replace: 0 = 不替换  1 = 替换单个  2 = 全部替换
    """
    if not find_text:
        return 0
    find = rng.Find
    find.ClearFormatting()
    find.Replacement.ClearFormatting()

    count = 0
    # Replace=1 逐个替换;替换后匹配位置自动前移,循环即可覆盖全部出现
    while count < 100000:
        found = find.Execute(
            find_text, False, False, False, False, False,
            True, 0, False, replace_text, 1,
        )
        if not found:
            break
        count += 1
    return count


def replace_placeholders(doc, values: dict) -> int:
    """在整个文档(正文 + 页眉页脚 + 表格等所有 story)里替换占位符。"""
    total = 0
    tokens: list[tuple[str, str]] = []
    for k, v in values.items():
        text = "" if v is None else str(v)
        tokens.append((f"{{{{{k}}}}}", text))  # {{key}}
        tokens.append((f"${{{k}}}", text))     # ${key}

    try:
        stories = doc.StoryRanges
    except Exception:
        stories = None

    ranges = []
    if stories is not None:
        try:
            for i in range(1, stories.Count + 1):
                rng = stories.Item(i)
                while rng is not None:
                    ranges.append(rng)
                    rng = rng.NextStoryRange
        except Exception:
            ranges = []
    if not ranges:
        ranges = [doc.Content]

    for find_text, replace_text in tokens:
        for rng in ranges:
            try:
                total += _replace_in_range(rng, find_text, replace_text)
            except Exception:
                pass
    return total


def fill_bookmarks(doc, values: dict) -> int:
    """按书签名填充内容。"""
    filled = 0
    bookmarks = doc.Bookmarks
    for k, v in values.items():
        try:
            if bookmarks.Exists(k):
                bookmarks(k).Range.Text = "" if v is None else str(v)
                filled += 1
        except Exception:
            pass
    return filled


def _file_format_for(path: str) -> int:
    ext = os.path.splitext(path)[1].lower()
    return WD_FORMAT.get(ext, 0)


def fill_template(template: str, output: str, values: dict,
                  visible: bool = False, keep_open: bool = False) -> dict:
    """核心:打开模板 -> 填充 -> 另存为 output。返回统计信息。"""
    template = os.path.abspath(template)
    output = os.path.abspath(output)
    if not os.path.isfile(template):
        raise FileNotFoundError(f"模板不存在: {template}")

    app = get_wps_app(visible=visible)
    doc = None
    try:
        doc = app.Documents.Open(template)
        n_bookmark = fill_bookmarks(doc, values)
        n_text = replace_placeholders(doc, values)

        if os.path.exists(output):
            os.remove(output)
        doc.SaveAs(FileName=output, FileFormat=_file_format_for(output))

        return {
            "template": template,
            "output": output,
            "bookmarks_filled": n_bookmark,
            "placeholders_replaced": n_text,
            "keys": list(values.keys()),
        }
    finally:
        if doc is not None:
            try:
                doc.Close(WD_DO_NOT_SAVE)
            except Exception:
                pass
        if not keep_open:
            try:
                app.Quit()
            except Exception:
                pass


def load_values(data_file: str | None, sets: list[str]) -> dict:
    values: dict = {}
    if data_file:
        with open(data_file, "r", encoding="utf-8-sig") as f:
            values.update(json.load(f))
    for item in sets or []:
        if "=" not in item:
            raise ValueError(f"--set 参数格式应为 key=value,收到: {item}")
        k, v = item.split("=", 1)
        values[k.strip()] = v
    return values


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(
        description="用 WPS 文字 COM 填充本地 .doc/.docx 模板",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("-t", "--template", required=True, help="模板文件路径(.doc/.docx)")
    parser.add_argument("-o", "--output", required=True, help="输出文件路径(按扩展名决定格式)")
    parser.add_argument("-d", "--data", help="JSON 数据文件(UTF-8)")
    parser.add_argument("--set", action="append", default=[], metavar="KEY=VALUE",
                        help="直接指定键值,可重复")
    parser.add_argument("--visible", action="store_true", help="显示 WPS 窗口(调试用)")
    parser.add_argument("--keep-open", action="store_true", help="结束后不关闭 WPS")
    args = parser.parse_args(argv)

    values = load_values(args.data, args.set)
    if not values:
        print("警告:没有任何数据(--data 或 --set 都为空)", file=sys.stderr)
    try:
        result = fill_template(args.template, args.output, values,
                               visible=args.visible, keep_open=args.keep_open)
    except Exception:
        traceback.print_exc()
        return 1

    _log("填充完成:")
    _log(f"  模板      : {result['template']}")
    _log(f"  输出      : {result['output']}")
    _log(f"  书签填充  : {result['bookmarks_filled']} 个")
    _log(f"  占位符替换: {result['placeholders_replaced']} 处")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
