using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Apex.Tools.UltimateJoin.Core;

namespace Apex.Tools.UltimateJoin.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class JoinCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uidoc = commandData.Application.ActiveUIDocument;
        var doc = uidoc.Document;

        // Load config once for this run (equivalent of the script's
        // "live" globals loaded at the top of the normal-click branch).
        var priorityOrder = ConfigStore.LoadPriorityOrder();
        var excludedKeys = ConfigStore.LoadExcludedKeys();
        var activePriority = CategoryMap.BuildPriorityFromOrder(priorityOrder);

        var excludedCategoryIds = excludedKeys
            .Where(k => CategoryMap.CatMap.ContainsKey(k))
            .Select(k => CategoryMap.CatMap[k].Category)
            .ToList();

        // --- Get selection: current selection filtered to join-able
        //     categories, or prompt to pick if nothing valid is selected ---
        var selected = new List<Element>();
        var currentSelectionIds = uidoc.Selection.GetElementIds();
        foreach (var id in currentSelectionIds)
        {
            var elem = doc.GetElement(id);
            if (elem?.Category != null && activePriority.ContainsKey(elem.Category.BuiltInCategory))
                selected.Add(elem);
        }

        if (selected.Count == 0)
        {
            try
            {
                var filter = new CategorySelectionFilter(activePriority.Keys.ToHashSet());
                var refs = uidoc.Selection.PickObjects(
                    ObjectType.Element, filter,
                    "Select elements to join (Esc to cancel), then click Finish");
                foreach (var r in refs)
                {
                    var elem = doc.GetElement(r);
                    if (elem != null) selected.Add(elem);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
        }

        if (selected.Count == 0)
        {
            TaskDialog.Show("Ultimate Join Elements", "Nothing selected — nothing to do.");
            return Result.Cancelled;
        }

        // --- Process ---
        var processedPairs = new HashSet<(long, long)>();

        using (var t = new Transaction(doc, "Join with Order"))
        {
            t.Start();
            try
            {
                // Step 1: unjoin all excluded elements globally (first pass)
                if (excludedKeys.Count > 0)
                    JoinOrderManager.UnjoinAllExcluded(doc, excludedKeys);

                // Step 2: process each selected element against its
                //         intersecting structural neighbors
                foreach (var elem in selected)
                {
                    ProcessElement(doc, elem, activePriority, excludedKeys, excludedCategoryIds, processedPairs);
                }

                // Step 3: final cleanup pass, same as the original script
                if (excludedKeys.Count > 0)
                    JoinOrderManager.UnjoinAllExcluded(doc, excludedKeys);

                t.Commit();
            }
            catch
            {
                if (t.HasStarted() && !t.HasEnded())
                    t.RollBack();
                throw;
            }
        }

        if (excludedKeys.Count > 0)
        {
            var msg = "Join operation completed.\n\nExcluded Categories:\n";
            foreach (var key in excludedKeys)
                msg += $"  \u2022 {CategoryMap.DisplayNameFor(key)}\n";
            msg += "\nThese categories were unjoined from all other elements.";
            TaskDialog.Show("Ultimate Join Elements - Complete", msg);
        }

        return Result.Succeeded;
    }

    /// process_element() — join the element with every intersecting
    /// structural neighbor (or unjoin-from-all if the element itself is excluded).
    private static void ProcessElement(
        Document doc, Element element,
        IReadOnlyDictionary<BuiltInCategory, int> activePriority,
        List<string> excludedKeys,
        List<BuiltInCategory> excludedCategoryIds,
        HashSet<(long, long)> processedPairs)
    {
        if (JoinOrderManager.IsElementExcluded(element, excludedKeys))
        {
            JoinOrderManager.UnjoinElementFromAll(doc, element);
            return;
        }

        var intersecting = JoinOrderManager.GetIntersectingStructural(
            element, doc, excludeCategories: excludedCategoryIds.Count > 0 ? excludedCategoryIds : null);

        foreach (var other in intersecting)
        {
            var idA = element.Id.Value;
            var idB = other.Id.Value;
            var pairKey = idA < idB ? (idA, idB) : (idB, idA);
            if (!processedPairs.Add(pairKey)) continue;

            JoinOrderManager.JoinWithOrder(doc, element, other, activePriority, excludedKeys);
        }
    }

    /// Restricts PickObjects to the categories currently in the priority map,
    /// same intent as the original script's pick_by_category() fallback.
    private class CategorySelectionFilter : ISelectionFilter
    {
        private readonly HashSet<BuiltInCategory> _allowed;
        public CategorySelectionFilter(HashSet<BuiltInCategory> allowed) => _allowed = allowed;

        public bool AllowElement(Element elem) =>
            elem.Category != null && _allowed.Contains(elem.Category.BuiltInCategory);

        public bool AllowReference(Reference reference, XYZ position) => true;
    }
}
