using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using UltimateJoinElements.Core;
using UltimateJoinElements.UI;

namespace UltimateJoinElements.Commands;

[Transaction(TransactionMode.ReadOnly)]
[Regeneration(RegenerationOption.Manual)]
public class ConfigureCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var currentOrder = ConfigStore.LoadPriorityOrder();
        var currentExcluded = ConfigStore.LoadExcludedKeys();
        var darkMode = ConfigStore.LoadDarkMode();

        var dlg = new JoinOrderDialog(currentOrder, currentExcluded, darkMode);
        dlg.ShowDialog();

        if (dlg.ResultOrder != null)
        {
            ConfigStore.SavePriorityOrder(dlg.ResultOrder);
            ConfigStore.SaveExcludedKeys(dlg.ResultExcluded ?? new List<string>());
            ConfigStore.SaveDarkMode(dlg.ResultDarkMode);

            var msg = "Configuration Saved!\n\nPriority Order:\n";
            int i = 1;
            foreach (var key in dlg.ResultOrder)
            {
                var display = CategoryMap.DisplayNameFor(key);
                var tag = (dlg.ResultExcluded?.Contains(key) ?? false) ? "  (always unjoined)" : "";
                msg += $"  {i}. {display}{tag}\n";
                i++;
            }
            TaskDialog.Show("Ultimate Join Elements", msg);
        }

        return Result.Succeeded;
    }
}
