using Autodesk.Revit.DB;

namespace Apex.Tools.UltimateJoin.Core;

/// <summary>
/// Revit-version compatibility helpers (AGENTS.md §8.5/§9).
/// Revit 2025+ exposes ElementId.Value (long); Revit 2024 only has
/// IntegerValue (int) — net48 builds compile the 2024 API branch,
/// net8.0-windows builds compile the 2025/2026 branch.
/// </summary>
public static class RevitCompat
{
    public static long GetElementIdValue(ElementId id)
    {
#if NETFRAMEWORK
        return id.IntegerValue;   // Revit 2024 (net48)
#else
        return id.Value;          // Revit 2025/2026 (net8.0-windows)
#endif
    }
}
