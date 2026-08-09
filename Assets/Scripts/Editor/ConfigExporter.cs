using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 配表导出工具 —— Excel → JSON + C# 数据类（零外部依赖，直接用 .NET 解析 .xlsx）
/// 菜单：Tools/Export All Configs
///
/// Excel 格式约定：
///   第1行：字段名（也是 C# 字段名）
///   第2行：注释（跳过）
///   第3行起：数据（第3行用于推断字段类型）
/// </summary>
public static class ConfigExporter
{
    private const string EXCEL_DIR = "Config/Excel/";
    private const string JSON_DIR = "Assets/Resources/Config/";
    private const string CS_DIR = "Assets/Scripts/Config/";

    [MenuItem("Tools/Export All Configs")]
    public static void ExportAll()
    {
        if (!Directory.Exists(EXCEL_DIR))
        {
            Directory.CreateDirectory(EXCEL_DIR);
            Debug.Log($"[ConfigExporter] 已创建目录: {EXCEL_DIR}，请放入 Excel 文件后重试");
            return;
        }

        if (!Directory.Exists(JSON_DIR)) Directory.CreateDirectory(JSON_DIR);
        if (!Directory.Exists(CS_DIR)) Directory.CreateDirectory(CS_DIR);

        var excelFiles = Directory.GetFiles(EXCEL_DIR, "*.xlsx");
        int total = 0;

        foreach (var filePath in excelFiles)
        {
            string name = Path.GetFileNameWithoutExtension(filePath);
            int count = ExportFile(filePath, name);
            if (count > 0) total += count;
        }

        AssetDatabase.Refresh();
        Debug.Log($"<color=green>[ConfigExporter] 导出完成，{total} 条数据</color>");
        EditorUtility.DisplayDialog("Export Configs", $"完成！{excelFiles.Length} 个文件，{total} 条数据", "OK");
    }

    // ==================== .xlsx 解析（ZIP + XML） ====================

    private static int ExportFile(string filePath, string fileName)
    {
        try
        {
            using var zip = ZipFile.OpenRead(filePath);
            return ProcessZip(zip, filePath, fileName);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ConfigExporter] {fileName}.xlsx 读取失败: {e.Message}");
            Debug.LogError("[ConfigExporter] 请确认：1) Excel 文件已关闭  2) 文件格式为 .xlsx（不是 .xls 改后缀）");
            return 0;
        }
    }

    private static int ProcessZip(ZipArchive zip, string filePath, string fileName)
    {

        // 读取共享字符串表
        var sst = ReadSharedStrings(zip);

        // 找到所有 sheet
        var sheets = ReadSheets(zip);
        if (sheets.Count == 0) return 0;

        int total = 0;

        foreach (var (sheetName, sheetPath) in sheets)
        {
            var entry = zip.GetEntry(sheetPath);
            if (entry == null) continue;

            using var stream = entry.Open();
            var doc = XDocument.Load(stream);
            var ns = doc.Root.Name.Namespace;
            var sheetData = doc.Root.Element(ns + "sheetData");
            if (sheetData == null) continue;
            var rows = sheetData.Elements(ns + "row");

            // 读取第1行 → 字段名
            var headers = new List<string>();
            var firstRow = rows.FirstOrDefault();
            if (firstRow == null) continue;
            foreach (var cell in firstRow.Elements(ns + "c"))
            {
                string val = GetCellValue(cell, ns, sst)?.ToString();
                if (string.IsNullOrEmpty(val)) break;
                headers.Add(val);
            }
            if (headers.Count == 0) continue;

            // 读取第3行起的数据（跳过第2行注释）
            var data = new List<Dictionary<string, object>>();
            var dataRows = rows.Skip(2); // 跳过第1行标题+第2行注释
            bool firstDataRow = true;
            Dictionary<string, object> firstRowData = null;

            foreach (var row in dataRows)
            {
                var dict = new Dictionary<string, object>();
                bool hasData = false;
                int colIdx = 0;

                foreach (var cell in row.Elements(ns + "c"))
                {
                    int c = GetColIndex(cell.Attribute("r")?.Value);
                    while (colIdx < c && colIdx < headers.Count) { dict[headers[colIdx]] = ""; colIdx++; }
                    if (colIdx < headers.Count)
                    {
                        object val = GetCellValue(cell, ns, sst);
                        dict[headers[colIdx]] = val;
                        if (val != null && !string.IsNullOrEmpty(val.ToString())) hasData = true;
                        colIdx++;
                    }
                }
                while (colIdx < headers.Count) { dict[headers[colIdx]] = ""; colIdx++; }

                if (hasData)
                {
                    data.Add(dict);
                    if (firstDataRow) { firstRowData = dict; firstDataRow = false; }
                }
            }

            if (data.Count == 0) continue;

            string jsonName = (sheets.Count == 1) ? fileName : $"{fileName}_{sheetName}";

            // 生成 JSON
            string json = BuildJson(data);
            File.WriteAllText(Path.Combine(JSON_DIR, jsonName + ".json"), json, Encoding.UTF8);

            // 生成 C# 类
            if (firstRowData != null)
                GenerateCSharpClass(jsonName, headers, firstRowData);

            total += data.Count;
            Debug.Log($"  [ConfigExporter] {jsonName}.json + .cs  ({data.Count} 条)");
        }

        return total;
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var sst = new List<string>();
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry == null) return sst;

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        var ns = doc.Root.Name.Namespace;

        foreach (var si in doc.Root.Elements(ns + "si"))
        {
            var t = si.Element(ns + "t");
            sst.Add(t?.Value ?? "");
        }
        return sst;
    }

    private static List<(string name, string path)> ReadSheets(ZipArchive zip)
    {
        var sheets = new List<(string, string)>();
        var entry = zip.GetEntry("xl/workbook.xml");
        if (entry == null) return sheets;

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        var ns = doc.Root.Name.Namespace;
        var sheetNs = ns + "sheet";

        int idx = 1;
        foreach (var sheet in doc.Root.Descendants(sheetNs))
        {
            string name = sheet.Attribute("name")?.Value ?? $"Sheet{idx}";
            sheets.Add((name, $"xl/worksheets/sheet{idx}.xml"));
            idx++;
        }
        return sheets;
    }

    private static object GetCellValue(XElement cell, XNamespace ns, List<string> sst)
    {
        var type = cell.Attribute("t")?.Value;
        var v = cell.Element(ns + "v")?.Value;

        if (type == "s") // 共享字符串
        {
            if (int.TryParse(v, out int idx) && idx < sst.Count)
                return sst[idx];
            return "";
        }

        if (type == "b") return v == "1";

        // 数字或空
        if (double.TryParse(v, out double num))
        {
            if (num == Math.Floor(num) && num <= int.MaxValue && num >= int.MinValue)
                return (int)num;
            return (float)num;
        }

        return v ?? "";
    }

    private static int GetColIndex(string cellRef)
    {
        if (string.IsNullOrEmpty(cellRef)) return 0;
        int col = 0;
        foreach (char c in cellRef)
        {
            if (c >= 'A' && c <= 'Z') col = col * 26 + (c - 'A' + 1);
            else break;
        }
        return col - 1;
    }

    // ==================== JSON 生成 ====================

    private static string BuildJson(List<Dictionary<string, object>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"items\": [");

        for (int i = 0; i < rows.Count; i++)
        {
            sb.Append("    {");
            var row = rows[i];
            int j = 0;
            foreach (var kv in row)
            {
                sb.Append($"\"{kv.Key}\": ");
                sb.Append(ToJsonValue(kv.Value));
                if (++j < row.Count) sb.Append(", ");
            }
            sb.Append("}");
            if (i < rows.Count - 1) sb.Append(",");
            sb.AppendLine();
        }

        sb.AppendLine("  ]");
        sb.Append("}");
        return sb.ToString();
    }

    private static string ToJsonValue(object val)
    {
        if (val == null) return "null";
        if (val is int i) return i.ToString();
        if (val is float f) return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (val is double d) return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (val is long l) return l.ToString();
        if (val is bool b) return b ? "true" : "false";
        string s = val.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{s}\"";
    }

    // ==================== C# 类生成 ====================

    private static void GenerateCSharpClass(string className, List<string> headers, Dictionary<string, object> firstRow)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Auto-generated by ConfigExporter");
        sb.AppendLine();
        sb.AppendLine("[System.Serializable]");
        sb.AppendLine($"public class {className}");
        sb.AppendLine("{");

        foreach (var header in headers)
        {
            string type = InferType(firstRow.TryGetValue(header, out var v) ? v : null);
            sb.AppendLine($"    public {type} {header};");
        }

        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(CS_DIR, className + ".cs"), sb.ToString(), Encoding.UTF8);
    }

    private static string InferType(object val)
    {
        if (val == null) return "string";
        if (val is int) return "int";
        if (val is float) return "float";
        if (val is double) return "float";
        if (val is long) return "int";
        if (val is bool) return "bool";
        return "string";
    }
}
