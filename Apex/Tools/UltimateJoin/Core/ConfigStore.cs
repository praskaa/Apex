using System.IO;
using System.Text;

namespace Apex.Tools.UltimateJoin.Core;

/// <summary>
/// Replaces pyRevit's script.get_config()/save_config(). A native add-in
/// has no pyRevit config store, so this persists the same three settings
/// (priority_order, excluded_categories, dark_mode) to a small JSON file
/// under %AppData%\Apex\UltimateJoin\config.json.
///
/// JSON is hand-rolled (System.Text.Json is not in-box on net48, so one
/// dependency-free code path serves every TFM). File format is identical
/// to what the previous System.Text.Json version wrote — old config.json
/// files stay readable.
/// </summary>
public static class ConfigStore
{
    private sealed class ConfigData
    {
        public List<string> PriorityOrder { get; set; } = new();
        public List<string> ExcludedCategories { get; set; } = new();
        public bool DarkMode { get; set; }
    }

    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Apex", "UltimateJoin");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static ConfigData Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return Deserialize(json);
            }
        }
        catch
        {
            // Corrupt/unreadable config: fall through to a fresh default.
        }
        return new ConfigData();
    }

    private static void Save(ConfigData data)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, Serialize(data));
        }
        catch
        {
            // Best-effort persistence, same as the original try/except pass.
        }
    }

    // ------------------------------------------------------------------
    // Mini JSON — just enough for the fixed config schema
    // { "priority_order": [string], "excluded_categories": [string],
    //   "dark_mode": bool }
    // ------------------------------------------------------------------

    private static string Serialize(ConfigData data)
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"priority_order\": ").Append(StringArray(data.PriorityOrder)).Append(",\n");
        sb.Append("  \"excluded_categories\": ").Append(StringArray(data.ExcludedCategories)).Append(",\n");
        sb.Append("  \"dark_mode\": ").Append(data.DarkMode ? "true" : "false").Append("\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    private static string StringArray(List<string> items)
    {
        if (items.Count == 0) return "[]";
        var sb = new StringBuilder("[\n");
        for (int i = 0; i < items.Count; i++)
        {
            sb.Append("    ").Append(Quote(items[i]));
            if (i < items.Count - 1) sb.Append(',');
            sb.Append('\n');
        }
        sb.Append("  ]");
        return sb.ToString();
    }

    private static string Quote(string s)
    {
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static ConfigData Deserialize(string json)
    {
        var data = new ConfigData();
        int i = 0;

        SkipWs(json, ref i);
        Expect(json, ref i, '{');
        SkipWs(json, ref i);
        if (Peek(json, i) == '}') return data;

        while (true)
        {
            SkipWs(json, ref i);
            var key = ReadString(json, ref i);
            SkipWs(json, ref i);
            Expect(json, ref i, ':');
            SkipWs(json, ref i);

            if (key == "priority_order") data.PriorityOrder = ReadStringArray(json, ref i);
            else if (key == "excluded_categories") data.ExcludedCategories = ReadStringArray(json, ref i);
            else if (key == "dark_mode") data.DarkMode = ReadBool(json, ref i);
            else SkipValue(json, ref i); // tolerate unknown keys

            SkipWs(json, ref i);
            var c = Peek(json, i);
            if (c == ',') { i++; continue; }
            if (c == '}') break;
            throw new FormatException("Expected ',' or '}' at " + i);
        }
        return data;
    }

    private static List<string> ReadStringArray(string json, ref int i)
    {
        var items = new List<string>();
        Expect(json, ref i, '[');
        SkipWs(json, ref i);
        if (Peek(json, i) == ']') { i++; return items; }

        while (true)
        {
            SkipWs(json, ref i);
            items.Add(ReadString(json, ref i));
            SkipWs(json, ref i);
            var c = Peek(json, i);
            if (c == ',') { i++; continue; }
            if (c == ']') { i++; break; }
            throw new FormatException("Expected ',' or ']' at " + i);
        }
        return items;
    }

    private static bool ReadBool(string json, ref int i)
    {
        if (json.Length - i >= 4 && string.CompareOrdinal(json, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (json.Length - i >= 5 && string.CompareOrdinal(json, i, "false", 0, 5) == 0) { i += 5; return false; }
        throw new FormatException("Expected true/false at " + i);
    }

    private static string ReadString(string json, ref int i)
    {
        Expect(json, ref i, '"');
        var sb = new StringBuilder();
        while (i < json.Length)
        {
            var c = json[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }

            if (i >= json.Length) break;
            var esc = json[i++];
            switch (esc)
            {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if (i + 4 > json.Length) throw new FormatException("Bad \\u escape");
                    sb.Append((char)Convert.ToInt32(json.Substring(i, 4), 16));
                    i += 4;
                    break;
                default:
                    throw new FormatException("Bad escape '\\" + esc + "'");
            }
        }
        throw new FormatException("Unterminated string");
    }

    /// Skips any JSON value (string, number, bool, null, object, array) —
    /// used to ignore unknown keys without breaking the parse.
    private static void SkipValue(string json, ref int i)
    {
        SkipWs(json, ref i);
        var c = Peek(json, i);
        if (c == '"') { ReadString(json, ref i); return; }
        if (c == '{' || c == '[')
        {
            int depth = 0;
            while (i < json.Length)
            {
                var d = json[i];
                if (d == '"') { ReadString(json, ref i); continue; }
                if (d == '{' || d == '[') depth++;
                else if (d == '}' || d == ']') { depth--; i++; if (depth == 0) return; continue; }
                i++;
            }
            throw new FormatException("Unterminated container");
        }
        while (i < json.Length && !char.IsWhiteSpace(json[i]) && json[i] != ',' && json[i] != '}') i++;
    }

    private static char Peek(string json, int i)
    {
        if (i >= json.Length) throw new FormatException("Unexpected end of JSON");
        return json[i];
    }

    private static void SkipWs(string json, ref int i)
    {
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
    }

    private static void Expect(string json, ref int i, char c)
    {
        if (i >= json.Length || json[i] != c) throw new FormatException("Expected '" + c + "' at " + i);
        i++;
    }

    /// Load saved priority order, falling back to CategoryMap.DefaultOrder
    /// if nothing saved yet or the saved keys don't all validate.
    public static List<string> LoadPriorityOrder()
    {
        var data = Load();
        if (data.PriorityOrder.Count > 0 && data.PriorityOrder.All(k => CategoryMap.CatMap.ContainsKey(k)))
            return data.PriorityOrder;
        return CategoryMap.DefaultOrder.ToList();
    }

    public static void SavePriorityOrder(List<string> order)
    {
        var data = Load();
        data.PriorityOrder = order;
        Save(data);
    }

    public static List<string> LoadExcludedKeys()
    {
        var data = Load();
        if (data.ExcludedCategories.All(k => CategoryMap.CatMap.ContainsKey(k)))
            return data.ExcludedCategories;
        return new List<string>();
    }

    public static void SaveExcludedKeys(List<string> keys)
    {
        var data = Load();
        data.ExcludedCategories = keys;
        Save(data);
    }

    public static bool LoadDarkMode()
    {
        return Load().DarkMode;
    }

    public static void SaveDarkMode(bool value)
    {
        var data = Load();
        data.DarkMode = value;
        Save(data);
    }
}
