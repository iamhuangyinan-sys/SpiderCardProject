# -*- coding: utf-8 -*-
"""端到端测试:自动生成模板 -> 填充 -> 读回验证。仅用于开发调试。"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import win32com.client as win32  # noqa: E402

import wps_doc_fill  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
PRIVATE = os.path.join(HERE, "private_doc")
os.makedirs(PRIVATE, exist_ok=True)
TEMPLATE = os.path.join(PRIVATE, "demo_template.doc")
OUTPUT = os.path.join(PRIVATE, "demo_output.doc")


def make_template() -> None:
    app = win32.Dispatch("KWPS.Application")
    app.Visible = False
    app.DisplayAlerts = False
    doc = app.Documents.Add()
    doc.Content.Text = (
        "========= 入职登记表 =========\r"
        "姓名: {{姓名}}\r"
        "公司: ${公司}\r"
        "日期: {{日期}}\r"
        "备注: \r"
    )
    # 加一个书签:备注(第 5 段)
    doc.Bookmarks.Add("备注", doc.Paragraphs(5).Range)
    if os.path.exists(TEMPLATE):
        os.remove(TEMPLATE)
    doc.SaveAs(FileName=TEMPLATE, FileFormat=0)
    doc.Close(0)
    app.Quit()
    print("[1] 模板已生成:", TEMPLATE)


def fill() -> dict:
    values = {"姓名": "张三", "公司": "某某科技有限公司", "日期": "2026-09-13", "备注": "书签填充的内容"}
    result = wps_doc_fill.fill_template(TEMPLATE, OUTPUT, values)
    print("[2] 填充结果:", result)
    return result


def read_back() -> None:
    app = win32.Dispatch("KWPS.Application")
    app.Visible = False
    app.DisplayAlerts = False
    doc = app.Documents.Open(OUTPUT, ReadOnly=True)
    text = doc.Content.Text
    doc.Close(0)
    app.Quit()
    print("[3] 输出文档内容:")
    print("-" * 40)
    print(text.replace("\r", "\n"))
    print("-" * 40)


if __name__ == "__main__":
    make_template()
    fill()
    read_back()
    print("[4] 端到端测试完成")
