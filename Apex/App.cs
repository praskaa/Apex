using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace Apex;

/// <summary>
/// Hub add-in Apex — owns the "Apex" ribbon tab; one panel per tool.
/// Adding a tool = create Tools/&lt;NamaTool&gt;/ + register its panel and
/// buttons here (see AGENTS.md §12.2).
/// </summary>
public class App : IExternalApplication
{
    private const string TabName = "Apex";
    private const string PanelName = "Ultimate Join";

    public Result OnStartup(UIControlledApplication application)
    {
        // First line of defence: prove whether Revit actually invoked us
        // and, if not, leave a breadcrumb under %AppData%\Apex\.
        Log("[UltimateJoin] OnStartup entered. Assembly=" + Assembly.GetExecutingAssembly().Location);

        try
        {
            // Creating a tab that already exists throws — the try/catch
            // keeps Apex loadable even if another add-in made the tab first.
            try
            {
                application.CreateRibbonTab(TabName);
                Log("[UltimateJoin] CreateRibbonTab('" + TabName + "') succeeded.");
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                Log("[UltimateJoin] CreateRibbonTab('" + TabName + "') reported the tab already exists.");
            }

            var panel = application.CreateRibbonPanel(TabName, PanelName);
            var assemblyPath = Assembly.GetExecutingAssembly().Location;

            var joinButtonData = new PushButtonData(
                "Apex_UltimateJoin_Join",
                "Join",
                assemblyPath,
                "Apex.Tools.UltimateJoin.Commands.JoinCommand")
            {
                ToolTip = "Join selected elements (or pick if nothing selected) using the configured priority order.",
                Image = PackIcon("join.small.png"),
                LargeImage = PackIcon("join.large.png"),
            };

            var configureButtonData = new PushButtonData(
                "Apex_UltimateJoin_Configure",
                "Configure",
                assemblyPath,
                "Apex.Tools.UltimateJoin.Commands.ConfigureCommand")
            {
                ToolTip = "Set join priority order and excluded categories.",
                Image = PackIcon("configure.small.png"),
                LargeImage = PackIcon("configure.large.png"),
            };

            panel.AddItem(joinButtonData);
            panel.AddItem(configureButtonData);
            Log("[UltimateJoin] Added Join + Configure buttons.");

            Log("[UltimateJoin] OnStartup succeeded.");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Log("[UltimateJoin] OnStartup FAILED: " + ex);
            throw;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        return Result.Succeeded;
    }

    /// <summary>Loads a PNG embedded as &lt;Resource&gt; via pack URI.</summary>
    private static BitmapImage PackIcon(string fileName)
    {
        return new BitmapImage(new Uri(
            "pack://application:,,,/Apex;component/Icons/" + fileName,
            UriKind.Absolute));
    }

    /// <summary>
    /// One shared startup log for the whole hub; every tool prefixes its
    /// lines with its own tag (see AGENTS.md §12.2 item 5).
    /// </summary>
    private static void Log(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Apex");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "startup.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
        }
        catch
        {
            // Logging must never take the add-in down.
        }
    }
}
