using Autodesk.Revit.DB;

namespace UltimateJoinElements.Core;

/// <summary>
/// Single source of truth for the join-order category list.
/// Ported 1:1 from the pyRevit join_order_manager.py CATEGORY_DEFS.
/// If you add/remove a category, do it here only — everything else
/// (default order, key lookup, priority map) derives from this list.
/// </summary>
public static class CategoryMap
{
    public sealed record CategoryDef(BuiltInCategory Category, string Key, string DisplayName);

    public static readonly IReadOnlyList<CategoryDef> CategoryDefs = new List<CategoryDef>
    {
        new(BuiltInCategory.OST_Floors,                "OST_Floors",               "Floors"),
        new(BuiltInCategory.OST_StructuralColumns,     "OST_StructuralColumns",    "Structural Columns"),
        new(BuiltInCategory.OST_StructuralFraming,     "OST_StructuralFraming",    "Structural Framing"),
        new(BuiltInCategory.OST_StructuralFoundation,  "OST_StructuralFoundation", "Structural Foundations"),
        new(BuiltInCategory.OST_Walls,                 "OST_Walls",                "Walls"),
        new(BuiltInCategory.OST_IOSModelGroups,        "OST_IOSModelGroups",       "Model Groups"),
        new(BuiltInCategory.OST_EdgeSlab,              "OST_EdgeSlab",             "Slab Edges"),
    };

    /// Default priority order, as a list of keys (index 0 = highest priority).
    public static readonly IReadOnlyList<string> DefaultOrder =
        CategoryDefs.Select(d => d.Key).ToList();

    /// key -> (BuiltInCategory, DisplayName)
    public static readonly IReadOnlyDictionary<string, (BuiltInCategory Category, string DisplayName)> CatMap =
        CategoryDefs.ToDictionary(d => d.Key, d => (d.Category, d.DisplayName));

    public static readonly IReadOnlyList<BuiltInCategory> AllCategories =
        CategoryDefs.Select(d => d.Category).ToList();

    /// Default priority map built from DefaultOrder (lower number = higher priority / cuts).
    public static readonly IReadOnlyDictionary<BuiltInCategory, int> DefaultJoinPriority =
        BuildPriorityFromOrder(DefaultOrder);

    /// Build a {BuiltInCategory: priority} map from an ordered list of keys.
    /// Equivalent to build_priority_from_order() in the original script.
    public static Dictionary<BuiltInCategory, int> BuildPriorityFromOrder(IReadOnlyList<string> order)
    {
        var priority = new Dictionary<BuiltInCategory, int>();
        for (int i = 0; i < order.Count; i++)
        {
            if (CatMap.TryGetValue(order[i], out var entry))
                priority[entry.Category] = i + 1;
        }
        return priority;
    }

    public static string DisplayNameFor(string key) =>
        CatMap.TryGetValue(key, out var entry) ? entry.DisplayName : key;
}
