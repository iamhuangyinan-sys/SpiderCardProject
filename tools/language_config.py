"""Read and patch SpiderCard's LanguageConfig workbook by Id."""

import argparse
from io import BytesIO
import json
from pathlib import Path
import re
import sys
from tempfile import NamedTemporaryFile
from xml.dom.minidom import parseString
from xml.parsers.expat import ExpatError
from zipfile import BadZipFile, ZipFile


ROOT = Path(__file__).resolve().parents[1]
WORKBOOK = ROOT / "Config/Excel/LanguageConfig.xlsx"
RUNTIME = ROOT / "Assets/Resources/Config/LanguageConfig.json"
SHEET = "xl/worksheets/sheet1.xml"
STRINGS = "xl/sharedStrings.xml"
BOOK = "xl/workbook.xml"
COLUMNS = {"Id": "A", "Chinese": "C", "English": "D"}
EDITABLE = set(COLUMNS) - {"Id"}


def children(node, name):
    return [child for child in node.childNodes if child.nodeName == name]


def content(node):
    return "".join(child.data for child in node.childNodes if child.nodeType in (3, 4))


def unique_fields(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON field: {key}")
        result[key] = value
    return result


class LanguageTable:
    def __init__(self, raw):
        self.raw = raw
        with ZipFile(BytesIO(raw)) as archive:
            self.sheet = parseString(archive.read(SHEET))
            self.strings = parseString(archive.read(STRINGS))
            self.book = parseString(archive.read(BOOK))
        sheets = self.book.getElementsByTagName("sheet")
        if len(sheets) != 1:
            raise ValueError("Expected a single LanguageConfig worksheet")
        self.data = self.sheet.getElementsByTagName("sheetData")[0]
        self.values = [
            "".join(content(t) for t in item.getElementsByTagName("t")
                    if t.parentNode.nodeName in ("si", "r"))
            for item in children(self.strings.documentElement, "si")
        ]
        self.plain_indices = {
            self.values[index]: index
            for index, item in enumerate(children(self.strings.documentElement, "si"))
            if len(children(item, "t")) == 1 and not children(item, "r")
        }
        rows = children(self.data, "row")
        headers = [self.read_row(row) for row in rows[:3]]
        if len(headers) != 3 or any(
            headers[index][key] != expected
            for index, expected_fields in enumerate((
                {"Id": "Id", "Chinese": "Chinese", "English": "English"},
                {"Id": "string", "Chinese": "string", "English": "string"},
            ))
            for key, expected in expected_fields.items()
        ) or [row.getAttribute("r") for row in rows[:3]] != ["1", "2", "3"]:
            raise ValueError("Unexpected LanguageConfig headers")
        self.rows = {}
        self.records = {}
        self.last_row = rows[-1]
        self.last_number = int(self.last_row.getAttribute("r"))
        self.appended = False
        for row in rows[3:]:
            record = self.read_row(row)
            key = record["Id"]
            if not any(record.values()):
                continue
            if not key.strip() or key in self.rows:
                raise ValueError(f"Missing or duplicate Id in row {row.getAttribute('r')}: {key!r}")
            self.rows[key] = row
            self.records[key] = record

    def cells(self, row):
        return {cell.getAttribute("r").rstrip("0123456789"): cell
                for cell in children(row, "c")}

    def read_row(self, row):
        cells = self.cells(row)
        record = {}
        for field, column in COLUMNS.items():
            cell = cells.get(column)
            values = children(cell, "v") if cell else []
            cell_type = cell.getAttribute("t") if cell else ""
            if cell and (children(cell, "f") or cell_type not in ("", "s", "n", "str")):
                raise ValueError(f"Unsupported cell in LanguageConfig: {cell.getAttribute('r')}")
            value = content(values[0]) if values else ""
            record[field] = self.values[int(value)] if cell_type == "s" and value else value
        return record

    def set_text(self, row, column, value):
        cells = self.cells(row)
        cell = cells.get(column)
        if cell is None:
            if not value:
                return
            cell = self.sheet.createElement("c")
            cell.setAttribute("r", column + row.getAttribute("r"))
            if row is not self.last_row and column in self.cells(self.last_row):
                style = self.cells(self.last_row)[column].getAttribute("s")
                if style:
                    cell.setAttribute("s", style)
            row.insertBefore(cell, next((c for col, c in cells.items() if col > column), None))
        for old in children(cell, "v"):
            cell.removeChild(old)
        if not value:
            if cell.hasAttribute("t"):
                cell.removeAttribute("t")
            return
        if value not in self.plain_indices:
            item = self.strings.createElement("si")
            string = self.strings.createElement("t")
            string.setAttribute("xml:space", "preserve")
            string.appendChild(self.strings.createTextNode(value))
            item.appendChild(string)
            self.strings.documentElement.insertBefore(
                item, next(iter(children(self.strings.documentElement, "extLst")), None))
            self.plain_indices[value] = len(self.values)
            self.values.append(value)
        cell.setAttribute("t", "s")
        index = self.sheet.createElement("v")
        index.appendChild(self.sheet.createTextNode(str(self.plain_indices[value])))
        cell.appendChild(index)

    def edit(self, patch):
        if not isinstance(patch, list):
            raise ValueError("Patch must be a JSON array")
        seen = set()
        changes = []
        for entry in patch:
            if not isinstance(entry, dict) or entry.keys() - (EDITABLE | {"op", "Id"}):
                raise ValueError("Patch entries accept op, Id, Chinese and English")
            if any(not isinstance(value, str) for value in entry.values()):
                raise ValueError("Patch fields must be strings")
            key = entry.get("Id", "")
            if not key or key != key.strip() or key in seen:
                raise ValueError(f"Invalid or repeated patch Id: {key!r}")
            seen.add(key)
            before = self.records.get(key)
            fields = entry.keys() & EDITABLE
            operation = entry.get("op")
            if operation == "add":
                if before is not None or not all(entry.get(lang, "").strip()
                                                for lang in ("Chinese", "English")):
                    raise ValueError(f"add requires a new Id and both translations: {key}")
            elif operation == "update":
                if before is None or not fields:
                    raise ValueError(f"update requires an existing Id and editable fields: {key}")
            elif operation == "delete":
                if before is None or fields:
                    raise ValueError(f"delete requires only an existing Id: {key}")
                self.data.removeChild(self.rows[key])
                del self.records[key]
                changes.append({"op": operation, "Id": key, "before": before, "after": None})
                continue
            else:
                raise ValueError(f"Use op add, update or delete for {key}")
            for field in fields | {"Id"}:
                value = entry[field]
                if re.search(r"[\x00-\x08\x0b\x0c\x0e-\x1f\ud800-\udfff\ufffe\uffff]", value):
                    raise ValueError(f"Invalid XML character in {key}.{field}")
                if len(value.encode("utf-16-le")) > 32767 * 2:
                    raise ValueError(f"Excel text limit exceeded in {key}.{field}")
            after = dict(before or dict.fromkeys(COLUMNS, ""), Id=key)
            after.update({field: entry[field] for field in fields})
            if before == after:
                continue
            row = self.rows[key] if before is not None else self.append_row()
            for field, column in COLUMNS.items():
                if before is None or before[field] != after[field]:
                    self.set_text(row, column, after[field])
            self.records[key] = after
            changes.append({"op": operation, "Id": key, "before": before, "after": after})
        return changes

    def append_row(self):
        old = self.last_number
        new = old + 1
        if new > 1048576:
            raise ValueError("Excel row limit exceeded")
        row = self.sheet.createElement("row")
        row.setAttribute("r", str(new))
        row.setAttribute("spans", "1:4")
        self.data.appendChild(row)
        self.last_number = new
        self.appended = True
        for tag in ("dimension", "autoFilter"):
            for node in children(self.sheet.documentElement, tag):
                ref = node.getAttribute("ref")
                match = re.fullmatch(r"(A1:[A-Z]+)(\d+)", ref)
                if match and int(match[2]) == old:
                    node.setAttribute("ref", match[1] + str(new))
        for node in self.book.getElementsByTagName("definedName"):
            if node.getAttribute("name") == "_xlnm._FilterDatabase":
                for child in node.childNodes:
                    if child.nodeType == 3:
                        child.data = re.sub(rf"(\$[A-Z]+\$){old}$", rf"\g<1>{new}", child.data)
        return row

    def write(self, path):
        root = self.strings.documentElement
        root.setAttribute("uniqueCount", str(len(self.values)))
        root.setAttribute("count", str(sum(
            cell.getAttribute("t") == "s" for cell in self.data.getElementsByTagName("c")
        )))
        documents = {SHEET: self.sheet, STRINGS: self.strings}
        if self.appended:
            documents[BOOK] = self.book
        replacements = {
            name: document.toxml(encoding="utf-8").replace(b"\r", b"&#13;")
            for name, document in documents.items()
        }
        with NamedTemporaryFile(dir=path.parent, suffix=".xlsx", delete=False) as temp:
            staged = Path(temp.name)
        try:
            with ZipFile(BytesIO(self.raw)) as source, ZipFile(staged, "w") as target:
                for info in source.infolist():
                    data = replacements[info.filename] if info.filename in replacements else source.read(info)
                    target.writestr(info, data)
            if LanguageTable(staged.read_bytes()).records != self.records:
                raise ValueError("Workbook round-trip validation failed")
            if path.read_bytes() != self.raw:
                raise ValueError("Workbook changed while editing. Re-run the preview.")
            staged.replace(path)
        finally:
            staged.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    show = commands.add_parser("show", help="Look up entries as JSON")
    filters = show.add_mutually_exclusive_group()
    filters.add_argument("--id")
    filters.add_argument("--contains")
    edit = commands.add_parser("edit", help="Preview or write a UTF-8 JSON patch")
    edit.add_argument("patch", type=Path)
    edit.add_argument("--write", action="store_true")
    commands.add_parser("check", help="Validate the workbook and compare it with the runtime JSON")
    args = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")
    try:
        table = LanguageTable(WORKBOOK.read_bytes())
        if args.command == "show":
            if args.id is not None:
                if args.id not in table.records:
                    raise ValueError(f"Unknown Id: {args.id}")
                result = table.records[args.id]
            else:
                query = (args.contains or "").casefold()
                result = [record for record in table.records.values()
                          if any(query in value.casefold() for value in record.values())]
        elif args.command == "edit":
            patch = json.loads(args.patch.read_text(encoding="utf-8-sig"),
                               object_pairs_hook=unique_fields)
            changes = table.edit(patch)
            if changes and args.write:
                table.write(WORKBOOK)
            result = {"written": bool(changes and args.write), "changes": changes}
        else:
            runtime = json.loads(RUNTIME.read_text(encoding="utf-8-sig"))["items"]
            excel = list(table.records.values())
            expected = {row["Id"]: row for row in excel}
            actual = {row["Id"]: row for row in runtime}
            result = {
                "rows": len(excel),
                "runtime_in_sync": runtime == excel,
                "missing_in_runtime": sorted(expected.keys() - actual.keys()),
                "extra_in_runtime": sorted(actual.keys() - expected.keys()),
                "different_text": sorted(key for key in expected.keys() & actual.keys()
                                         if expected[key] != actual[key]),
            }
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return int(args.command == "check" and not result["runtime_in_sync"])
    except (OSError, ValueError, BadZipFile, ExpatError, KeyError, IndexError) as error:
        print(f"LanguageConfig: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
