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
///   第2行：类型（int / string / float / bool，或 list<int> / list<string> / list<list<int>> …）
///          列表字段的单元格值写成 [值1;值2]，可嵌套：[[1;2];[3;4]]
///          （方括号可省；; 分隔，中文 ；也行；连续 / 尾随分号产生的空项跳过）
///   第3行：注释（可空，跳过）
///   第4行起：数据
///
/// 列表语法说明：
///   - 支持嵌套：list<list<int>> 的值就写成 [[1;2];[3;4]]，只按最外层的 ; 切分
///   - 分隔符是 ;，所以字符串元素里不能含分号
///   - 连续 / 尾随分号产生的空项会被跳过，空的嵌套列表要写成 []
///   - 前提约定：字符串元素里不含 [ ]，所以方括号可以直接当结构符号用，不需要转义
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

        var excelFiles = Directory.GetFiles(EXCEL_DIR, "*.xlsx")
            // Excel 打开时会生成 ~$xxx.xlsx 临时文件，别把它当配置表读
            .Where(f => !Path.GetFileName(f).StartsWith("~$"))
            .ToArray();
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

            // 读取第1行 → 字段名（字段名为空的列视为注释列，跳过）
            var headers = new List<string>();
            var colMap = new Dictionary<int, int>(); // Excel列索引 → headers索引
            var firstRow = rows.FirstOrDefault();
            if (firstRow == null) continue;
            foreach (var cell in firstRow.Elements(ns + "c"))
            {
                int c = GetColIndex(cell.Attribute("r")?.Value);
                string val = GetCellValue(cell, ns, sst)?.ToString();
                if (string.IsNullOrEmpty(val)) continue;
                colMap[c] = headers.Count;
                headers.Add(val);
            }
            if (headers.Count == 0) continue;

            // 读取第2行 → 类型（只读有效列）
            var types = new List<string>();
            for (int i = 0; i < headers.Count; i++) types.Add("string");
            var typeRow = rows.Skip(1).FirstOrDefault();
            if (typeRow != null)
            {
                foreach (var cell in typeRow.Elements(ns + "c"))
                {
                    int c = GetColIndex(cell.Attribute("r")?.Value);
                    string val = GetCellValue(cell, ns, sst)?.ToString();
                    if (string.IsNullOrEmpty(val)) continue;
                    if (colMap.TryGetValue(c, out int idx))
                        types[idx] = val;
                }
            }

            // 读取第4行起的数据（跳过第1行字段名+第2行类型+第3行注释）
            var data = new List<Dictionary<string, object>>();
            var dataRows = rows.Skip(3);

            foreach (var row in dataRows)
            {
                var dict = new Dictionary<string, object>();
                bool hasData = false;

                foreach (var cell in row.Elements(ns + "c"))
                {
                    int c = GetColIndex(cell.Attribute("r")?.Value);
                    if (!colMap.TryGetValue(c, out int idx)) continue; // 注释列跳过

                    object val = GetCellValue(cell, ns, sst);
                    string type = idx < types.Count ? types[idx] : "string";
                    val = ConvertByType(val, type);
                    dict[headers[idx]] = val;
                    if (HasValue(val)) hasData = true;
                }

                if (hasData)
                {
                    FillMissingValues(dict, headers, types);
                    data.Add(dict);
                }
            }

            if (data.Count == 0) continue;

            string jsonName = (sheets.Count == 1) ? fileName : $"{fileName}_{sheetName}";

            // 生成 JSON
            string json = BuildJson(data);
            File.WriteAllText(Path.Combine(JSON_DIR, jsonName + ".json"), json, Encoding.UTF8);

            // 生成 C# 类
            GenerateCSharpClass(jsonName, headers, types);

            total += data.Count;
            Debug.Log($"  [ConfigExporter] {jsonName}.json + .cs  ({data.Count} 条)");
        }

        return total;
    }

    /// <summary>
    /// 补齐整行缺失的列：Excel 里的空单元格可能整个不写进 XML → 该字段会从 JSON 里消失，
    /// JsonUtility 读出来就是 null（List 字段直接 NRE）。按类型补默认值，保证字段总是在
    /// </summary>
    private static void FillMissingValues(Dictionary<string, object> row, List<string> headers, List<string> types)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            string name = headers[i];
            if (row.ContainsKey(name)) continue;

            row[name] = GetDefaultValue(i < types.Count ? types[i] : "string");
        }
    }

    /// <summary>各类型的空值默认值（列表是空列表，不是 null）</summary>
    private static object GetDefaultValue(string type)
    {
        if (GetListElementType(type) != null) return new List<object>();

        switch (type?.Trim().ToLower())
        {
            case "int": return 0;
            case "float":
            case "double": return 0f;
            case "bool": return false;
            default: return "";
        }
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

        // 列表 → JSON 数组（List<T> 实现非泛型 IList；string 不是 IList，不会误判）
        if (val is System.Collections.IList list)
        {
            var items = new List<string>(list.Count);
            foreach (var item in list) items.Add(ToJsonValue(item));
            return "[" + string.Join(", ", items) + "]";
        }

        if (val is int i) return i.ToString();
        if (val is float f) return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (val is double d) return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (val is long l) return l.ToString();
        if (val is bool b) return b ? "true" : "false";
        string s = val.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{s}\"";
    }

    // ==================== C# 类生成 ====================

    private static void GenerateCSharpClass(string className, List<string> headers, List<string> types)
    {
        // 先归一化类型：列表字段需要 using System.Collections.Generic
        var normalized = new List<string>(headers.Count);
        bool needGenericUsing = false;

        for (int i = 0; i < headers.Count; i++)
        {
            string type = NormalizeType(i < types.Count ? types[i] : "string");
            if (type.Contains("List<")) needGenericUsing = true;
            normalized.Add(type);
        }

        var sb = new StringBuilder();
        sb.AppendLine("// Auto-generated by ConfigExporter");
        if (needGenericUsing) sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("[System.Serializable]");
        sb.AppendLine($"public class {className}");
        sb.AppendLine("{");

        for (int i = 0; i < headers.Count; i++)
        {
            sb.AppendLine($"    public {normalized[i]} {headers[i]};");
        }

        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(CS_DIR, className + ".cs"), sb.ToString(), Encoding.UTF8);
    }

    /// <summary>该值算不算“有数据”（空列表不算，避免空行被当成数据导出去）</summary>
    private static bool HasValue(object val)
    {
        if (val == null) return false;
        if (val is string s) return !string.IsNullOrEmpty(s);
        if (val is System.Collections.IList list) return list.Count > 0;
        return true;
    }

    /// <summary>归一化类型名（第2行写的类型 → C# 类型）</summary>
    private static string NormalizeType(string type)
    {
        // list<T> → List<T>（元素类型同样归一化）
        string elementType = GetListElementType(type);
        if (elementType != null) return $"List<{NormalizeType(elementType)}>";

        switch (type?.Trim().ToLower())
        {
            case "int": return "int";
            case "float": return "float";
            case "double": return "float";
            case "bool": return "bool";
            case "string": return "string";
            default: return "string";
        }
    }

    /// <summary>
    /// 是 list&lt;...&gt; 就返回尖括号里的元素类型名，否则返回 null。
    /// 支持嵌套：list&lt;list&lt;int&gt;&gt; → 返回 "list&lt;int&gt;"（交给递归继续解析）
    /// </summary>
    private static string GetListElementType(string type)
    {
        if (string.IsNullOrEmpty(type)) return null;

        string t = type.Trim();
        if (t.Length < 6) return null;                                       // 最短也就 "list<>"
        if (!t.StartsWith("list<", StringComparison.OrdinalIgnoreCase)) return null;
        if (!t.EndsWith(">", StringComparison.Ordinal)) return null;

        // 取尖括号里那层：最外层括号一定是最前 / 最后各一个，直接去掉即可（内部可能还有 <>）
        string element = t.Substring(5, t.Length - 6).Trim();
        if (element.Length == 0) return null;

        return element;
    }

    /// <summary>按声明类型转换单元格值</summary>
    private static object ConvertByType(object val, string type)
    {
        // list<...>：单元格值写成 [a;b;c]
        string elementType = GetListElementType(type);
        if (elementType != null) return ConvertToList(val, elementType);

        switch (type?.Trim().ToLower())
        {
            case "int":
                if (val is int i) return i;
                return int.TryParse(val?.ToString(), out int iv) ? iv : 0;
            case "float":
                if (val is float f) return f;
                return float.TryParse(val?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fv) ? fv : 0f;
            case "bool":
                if (val is bool b) return b;
                return bool.TryParse(val?.ToString(), out bool bv) && bv;
            case "string":
            default:
                return val?.ToString() ?? "";
        }
    }

    /// <summary>
    /// 把 [值1;值2] 形式的单元格值解析成 List（每个元素按 elementType 转，可嵌套）。
    /// 外层方括号可省；只按「最外层」的 ; 切分；英文 ; 与中文 ；都当分隔符；空项跳过
    /// </summary>
    private static object ConvertToList(object val, string elementType)
    {
        var list = new List<object>();
        string text = val?.ToString()?.Trim() ?? "";

        // 只有整串确实被一对配对括号包住时才剥（否则 [1;2];[3] 这种会被误剥开头那个 [）
        if (HasWrappingBrackets(text)) text = text.Substring(1, text.Length - 2);

        foreach (string part in SplitTopLevel(text))
        {
            string item = part.Trim();
            if (item.Length == 0) continue;      // 空项跳过（空的嵌套列表请写成 []）

            list.Add(ConvertByType(item, elementType));
        }

        return list;
    }

    /// <summary>整串是否被一对配对括号包住：开头的 [ 对应的 ] 正好是最后一个字符</summary>
    private static bool HasWrappingBrackets(string text)
    {
        if (text.Length < 2) return false;
        if (text[0] != '[' || text[text.Length - 1] != ']') return false;

        int depth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '[')
            {
                depth++;
            }
            else if (text[i] == ']')
            {
                depth--;

                // 开头那个 [ 已经闭合 → 看是不是正好收尾；不是的话说明外层没包整串
                if (depth == 0) return i == text.Length - 1;
            }
        }

        return false;
    }

    /// <summary>
    /// 只按「最外层」的 ; 切分：嵌套列表里的分号（在 [ ] 里面）不当分隔符。
    /// 字符串元素里不含 [ ] 是这套语法的前提，所以数括号深度就够
    /// </summary>
    private static IEnumerable<string> SplitTopLevel(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        int depth = 0;
        int start = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '[')
            {
                depth++;
            }
            else if (c == ']')
            {
                if (depth > 0) depth--;
            }
            else if ((c == ';' || c == '；') && depth == 0)
            {
                yield return text.Substring(start, i - start);
                start = i + 1;
            }
        }

        yield return text.Substring(start);
    }
}
