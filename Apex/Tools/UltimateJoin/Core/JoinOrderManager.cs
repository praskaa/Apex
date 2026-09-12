using Autodesk.Revit.DB;

namespace Apex.Tools.UltimateJoin.Core;

public enum JoinResult
{
    JoinedAndOrdered,
    OrderedOnly,
    AlreadyCorrect,
    Failed,
    SkippedExcluded,
    UnjoinedExcluded,
    UnjoinFailed,
}

/// <summary>
/// Port of join_order_manager.py. Holds no state of its own beyond the
/// active priority map passed in by the caller — callers (JoinCommand)
/// load config once per run, same as the original script's "live" globals.
/// </summary>
public static class JoinOrderManager
{
    /// get_priority() — returns 99 for categories not in the active priority map.
    public static int GetPriority(Element? element, IReadOnlyDictionary<BuiltInCategory, int> activePriority)
    {
        if (element?.Category == null) return 99;
        return activePriority.TryGetValue(element.Category.BuiltInCategory, out var p) ? p : 99;
    }

    /// get_floor_thickness() — tries compound structure first, falls back to
    /// the instance thickness parameter (handles variable/edited floors).
    public static double? GetFloorThickness(Document doc, Element? element)
    {
        try
        {
            if (element?.Category == null) return null;
            if ((BuiltInCategory)RevitCompat.GetElementIdValue(element.Category.Id) != BuiltInCategory.OST_Floors) return null;

            if (doc.GetElement(element.GetTypeId()) is FloorType floorType)
            {
                var cs = floorType.GetCompoundStructure();
                if (cs != null)
                {
                    var width = cs.GetWidth();
                    if (width > 0) return width;
                }
            }

            var param = element.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
            if (param != null && param.HasValue)
            {
                var value = param.AsDouble();
                if (value > 0) return value;
            }
        }
        catch
        {
            // Same "best-effort, swallow and return null" behavior as the original.
        }
        return null;
    }

    /// get_cutting_element() — same-priority Floors are tie-broken by thickness
    /// (thicker floor cuts), matching real structural intent.
    public static (Element Cutting, Element Cut) GetCuttingElement(
        Element elemA, Element elemB, Document doc, IReadOnlyDictionary<BuiltInCategory, int> activePriority)
    {
        var pa = GetPriority(elemA, activePriority);
        var pb = GetPriority(elemB, activePriority);

        if (pa == pb)
        {
            var catA = elemA.Category?.BuiltInCategory;
            var catB = elemB.Category?.BuiltInCategory;
            if (catA == BuiltInCategory.OST_Floors && catB == BuiltInCategory.OST_Floors)
            {
                var tA = GetFloorThickness(doc, elemA);
                var tB = GetFloorThickness(doc, elemB);
                if (tA is not null && tB is not null && tA != tB)
                    return tA > tB ? (elemA, elemB) : (elemB, elemA);
            }
        }

        return pa <= pb ? (elemA, elemB) : (elemB, elemA);
    }

    /// join_with_order() / join_with_live_order() — join two elements and
    /// enforce correct cutting order, respecting excluded categories.
    public static JoinResult JoinWithOrder(
        Document doc, Element elemA, Element elemB,
        IReadOnlyDictionary<BuiltInCategory, int> activePriority,
        IReadOnlyCollection<string> excludedKeys,
        bool skipIfExcluded = true)
    {
        if (skipIfExcluded && (IsElementExcluded(elemA, excludedKeys) || IsElementExcluded(elemB, excludedKeys)))
        {
            if (JoinGeometryUtils.AreElementsJoined(doc, elemA, elemB))
            {
                try
                {
                    JoinGeometryUtils.UnjoinGeometry(doc, elemA, elemB);
                    return JoinResult.UnjoinedExcluded;
                }
                catch
                {
                    return JoinResult.UnjoinFailed;
                }
            }
            return JoinResult.SkippedExcluded;
        }

        try
        {
            var (cutting, cut) = GetCuttingElement(elemA, elemB, doc, activePriority);

            bool joinedNew = false;
            if (!JoinGeometryUtils.AreElementsJoined(doc, cutting, cut))
            {
                try
                {
                    JoinGeometryUtils.JoinGeometry(doc, cutting, cut);
                    joinedNew = true;
                }
                catch
                {
                    return JoinResult.Failed;
                }
            }

            try
            {
                bool isCutting = JoinGeometryUtils.IsCuttingElementInJoin(doc, cutting, cut);
                if (!isCutting)
                {
                    JoinGeometryUtils.SwitchJoinOrder(doc, cutting, cut);
                    return joinedNew ? JoinResult.JoinedAndOrdered : JoinResult.OrderedOnly;
                }
                return JoinResult.AlreadyCorrect;
            }
            catch
            {
                // IsCuttingElementInJoin unsupported for this pair — force switch.
                try
                {
                    JoinGeometryUtils.SwitchJoinOrder(doc, cutting, cut);
                    return joinedNew ? JoinResult.JoinedAndOrdered : JoinResult.OrderedOnly;
                }
                catch
                {
                    return JoinResult.AlreadyCorrect;
                }
            }
        }
        catch
        {
            return JoinResult.Failed;
        }
    }

    /// unjoin_elements()
    public static bool UnjoinElements(Document doc, Element elemA, Element elemB)
    {
        if (JoinGeometryUtils.AreElementsJoined(doc, elemA, elemB))
        {
            try
            {
                JoinGeometryUtils.UnjoinGeometry(doc, elemA, elemB);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    /// get_intersecting_structural() — bounding-box filter across ALL_CATEGORIES
    /// (minus any excluded categories), expanded by a small tolerance.
    public static List<Element> GetIntersectingStructural(
        Element element, Document doc,
        double tolerance = 0.001,
        IReadOnlyCollection<BuiltInCategory>? excludeCategories = null)
    {
        var bb = element.get_BoundingBox(null);
        if (bb == null) return new List<Element>();

        var expandedMin = new XYZ(bb.Min.X - tolerance, bb.Min.Y - tolerance, bb.Min.Z - tolerance);
        var expandedMax = new XYZ(bb.Max.X + tolerance, bb.Max.Y + tolerance, bb.Max.Z + tolerance);
        var outline = new Outline(expandedMin, expandedMax);

        var bbFilter = new BoundingBoxIntersectsFilter(outline);
        var excludeSelfFilter = new ExclusionFilter(new List<ElementId> { element.Id });
        var notTypeFilter = new ElementIsElementTypeFilter(true);

        var categoriesToCheck = excludeCategories is { Count: > 0 }
            ? CategoryMap.AllCategories.Where(c => !excludeCategories.Contains(c)).ToList()
            : CategoryMap.AllCategories.ToList();

        var results = new List<Element>();
        foreach (var category in categoriesToCheck)
        {
            var elements = new FilteredElementCollector(doc)
                .OfCategory(category)
                .WherePasses(bbFilter)
                .WherePasses(excludeSelfFilter)
                .WherePasses(notTypeFilter)
                .ToElements();
            results.AddRange(elements);
        }
        return results;
    }

    /// is_element_excluded()
    public static bool IsElementExcluded(Element? element, IReadOnlyCollection<string> excludedKeys)
    {
        if (element?.Category == null || excludedKeys.Count == 0) return false;
        foreach (var key in excludedKeys)
        {
            if (CategoryMap.CatMap.TryGetValue(key, out var entry) && entry.Category == element.Category.BuiltInCategory)
                return true;
        }
        return false;
    }

    /// unjoin_element_from_all() — uses GetJoinedElements() for a single lookup
    /// instead of pairwise checks against every other element.
    public static int UnjoinElementFromAll(Document doc, Element element)
    {
        try
        {
            var joinedIds = JoinGeometryUtils.GetJoinedElements(doc, element);
            if (joinedIds.Count == 0) return 0;

            int count = 0;
            foreach (var otherId in joinedIds)
            {
                var other = doc.GetElement(otherId);
                if (other != null && JoinGeometryUtils.AreElementsJoined(doc, element, other))
                {
                    try
                    {
                        JoinGeometryUtils.UnjoinGeometry(doc, element, other);
                        count++;
                    }
                    catch
                    {
                        // leave this pair joined, keep going
                    }
                }
            }
            return count;
        }
        catch
        {
            return 0;
        }
    }

    /// unjoin_all_excluded() — sweeps every element in each excluded category
    /// and unjoins it from everything. Returns per-category counts for the
    /// summary dialog.
    public static Dictionary<string, int> UnjoinAllExcluded(Document doc, IReadOnlyCollection<string> excludedKeys)
    {
        var results = new Dictionary<string, int>();
        if (excludedKeys.Count == 0) return results;

        foreach (var key in excludedKeys)
        {
            if (!CategoryMap.CatMap.TryGetValue(key, out var entry)) continue;

            var collector = new FilteredElementCollector(doc)
                .OfCategory(entry.Category)
                .WhereElementIsNotElementType()
                .ToElements();

            int totalUnjoined = 0;
            foreach (var element in collector)
                totalUnjoined += UnjoinElementFromAll(doc, element);

            results[key] = totalUnjoined;
        }
        return results;
    }
}
