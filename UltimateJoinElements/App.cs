using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace UltimateJoinElements;

public class App : IExternalApplication
{
    private const string TabName = "Apex";
    private const string PanelName = "Ultimate Join";

    public Result OnStartup(UIControlledApplication application)
    {
        // First line of defence: prove whether Revit actually invoked us and,
        // if not, leave a breadcrumb under %AppData%\UltimateJoinElements\.
        Log("OnStartup entered. Assembly=" + Assembly.GetExecutingAssembly().Location);

        try
        {
            // Creating a tab that already exists throws — same tab can host
            // future native add-ins you build after this one.
            try
            {
                application.CreateRibbonTab(TabName);
                Log("CreateRibbonTab('" + TabName + "') succeeded.");
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Tab already exists (e.g. another add-in created it first) — fine.
                Log("CreateRibbonTab('" + TabName + "') reported the tab already exists.");
            }

            var panel = application.CreateRibbonPanel(TabName, PanelName);
            Log("CreateRibbonPanel('" + PanelName + "') succeeded.");
            var assemblyPath = Assembly.GetExecutingAssembly().Location;

            var joinButtonData = new PushButtonData(
                "UltimateJoinElements_Join",
                "Join",
                assemblyPath,
                "UltimateJoinElements.Commands.JoinCommand")
            {
                ToolTip = "Join selected elements (or pick if nothing selected) using the configured priority order.",
                Image = PackIcon("join.small.png"),
                LargeImage = PackIcon("join.large.png"),
            };

            var configureButtonData = new PushButtonData(
                "UltimateJoinElements_Configure",
                "Configure",
                assemblyPath,
                "UltimateJoinElements.Commands.ConfigureCommand")
            {
                ToolTip = "Set join priority order and excluded categories.",
                Image = PackIcon("configure.small.png"),
                LargeImage = PackIcon("configure.large.png"),
            };

            panel.AddItem(joinButtonData);
            panel.AddItem(configureButtonData);
            Log("Added Join + Configure buttons.");

            Log("OnStartup succeeded.");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Log("OnStartup FAILED: " + ex);
            throw;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        return Result.Succeeded;
    }

    /// <summary>Loads a PNG embedded as <Resource> via pack URI.</summary>
    private static BitmapImage PackIcon(string fileName)
    {
        return new BitmapImage(new Uri(
            "pack://application:,,,/UltimateJoinElements;component/Icons/" + fileName,
            UriKind.Absolute));
    }

    private static void Log(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UltimateJoinElements");
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
