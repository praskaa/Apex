using System.IO;
using System.Text.Json;

namespace UltimateJoinElements.Core;

/// <summary>
/// Replaces pyRevit's script.get_config()/save_config(). A native add-in
/// has no pyRevit config store, so this persists the same three settings
/// (priority_order, excluded_categories, dark_mode) to a small JSON file
/// under %AppData%\UltimateJoinElements\config.json.
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
        "UltimateJoinElements");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static ConfigData Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var data = JsonSerializer.Deserialize<ConfigData>(json);
                if (data != null) return data;
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
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Best-effort persistence, same as the original try/except pass.
        }
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
