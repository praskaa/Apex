using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Apex.Tools.UltimateJoin.Core;

namespace Apex.Tools.UltimateJoin.UI;

/// <summary>
/// Port of the pyRevit JoinOrderDialog WPF window. Same palette,
/// same borderless/custom-titlebar/dark-light-toggle shell, same
/// "one row per category, checkbox + position both live on that row"
/// design the original settled on.
///
/// Reordering is full drag-and-drop, ported from the pyRevit original:
/// press the ⋮⋮ grip on a row's right edge and drop onto another row
/// (DragDrop.DoDragDrop + AllowDrop/Drop per row, pop+insert move).
/// </summary>
public partial class JoinOrderDialog : Window
{
    private class RowItem
    {
        public string Key = "";
        public bool Excluded;
    }

    private readonly List<RowItem> _items;
    private bool _darkMode;
    private DispatcherTimer? _helpCloseTimer;
    private readonly List<Border> _rowBorders = new();
    private int? _dragSourceIndex;
    private int? _dropTargetIndex;
    private bool _dropEdgeTop;

    public List<string>? ResultOrder { get; private set; }
    public List<string>? ResultExcluded { get; private set; }
    public bool ResultDarkMode { get; private set; }

    private static readonly Dictionary<string, string> LightPalette = new()
    {
        ["BgBrush"] = "#F2EDDC",
        ["PanelBrush"] = "#FFFFFF",
        ["BorderBrush1"] = "#D8D2BE",
        ["TextBrush"] = "#0C3A52",
        ["SubTextBrush"] = "#6E93A0",
        ["AccentBrush"] = "#0C4A63",
        ["AccentTextBrush"] = "#F2ECDC",
        ["SelectedBrush"] = "#D9E3E6",
        ["HeaderBrush"] = "#E8E2CF",
        ["RemoveBrush"] = "#EF4444",
    };

    private static readonly Dictionary<string, string> DarkPalette = new()
    {
        ["BgBrush"] = "#0A3346",
        ["PanelBrush"] = "#0F5A78",
        ["BorderBrush1"] = "#1E6884",
        ["TextBrush"] = "#F2ECDC",
        ["SubTextBrush"] = "#9FC3D1",
        ["AccentBrush"] = "#2E93C7",
        ["AccentTextBrush"] = "#F2ECDC",
        ["SelectedBrush"] = "#1B5C78",
        ["HeaderBrush"] = "#0A3F55",
        ["RemoveBrush"] = "#F87171",
    };

    public JoinOrderDialog(List<string> currentOrder, List<string> currentExcluded, bool darkMode = false)
    {
        InitializeComponent();

        _darkMode = darkMode;
        var excludedSet = new HashSet<string>(currentExcluded);
        _items = currentOrder.Select(k => new RowItem { Key = k, Excluded = excludedSet.Contains(k) }).ToList();

        HelpPopup.PlacementTarget = HelpButton;
        ItemsList.ItemContainerStyle = BuildFlatItemContainerStyle();

        RefreshList();
        ApplyPalette();
    }

    // ------------------------------------------------------------------
    // Shell / chrome
    // ------------------------------------------------------------------
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { /* ignore drag races on click */ }
        }
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _darkMode = !_darkMode;
        ApplyPalette();
    }

    private static SolidColorBrush Brush(string hex) =>
        (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    private void ApplyPalette()
    {
        var p = _darkMode ? DarkPalette : LightPalette;

        Shell.Background = Brush(p["BgBrush"]);
        Shell.BorderBrush = Brush(p["BorderBrush1"]);
        TitleBar.Background = Brush(p["HeaderBrush"]);
        TitleText.Foreground = Brush(p["TextBrush"]);
        ThemeButton.Content = _darkMode ? "\u2600" : "\u263D"; // sun / moon glyph
        ThemeButton.Background = Brush(p["PanelBrush"]);
        ThemeButton.Foreground = Brush(p["TextBrush"]);
        CloseButton.Background = Brush(p["PanelBrush"]);
        CloseButton.Foreground = Brush(p["TextBrush"]);

        SubtitleText.Foreground = Brush(p["TextBrush"]);
        HelpButton.Background = Brush(p["AccentBrush"]);
        HelpButton.Foreground = Brush(p["AccentTextBrush"]);
        HelpBorder.Background = Brush(p["PanelBrush"]);
        HelpBorder.BorderBrush = Brush(p["BorderBrush1"]);
        HelpText.Foreground = Brush(p["TextBrush"]);

        ListBorder.Background = Brush(p["PanelBrush"]);
        ListBorder.BorderBrush = Brush(p["BorderBrush1"]);
        ItemsList.Background = Brush(p["PanelBrush"]); // otherwise the ListBox's default white shows in dark mode

        foreach (var btn in new[] { DefaultButton, CancelButtonFooter })
        {
            btn.Background = Brush(p["PanelBrush"]);
            btn.Foreground = Brush(p["TextBrush"]);
        }
        SaveButton.Background = Brush(p["AccentBrush"]);
        SaveButton.Foreground = Brush(p["AccentTextBrush"]);

        RefreshList(); // rows carry their own palette-dependent brushes too
    }

    // ------------------------------------------------------------------
    // Help popup — opens on hover of the "?" button AND the popup itself,
    // closes on a short delay so moving between the two doesn't flicker it.
    // ------------------------------------------------------------------
    private void CancelHelpCloseTimer() => _helpCloseTimer?.Stop();

    private void HelpButton_MouseEnter(object sender, MouseEventArgs e)
    {
        CancelHelpCloseTimer();
        HelpPopup.IsOpen = true;
    }

    private void HelpButton_MouseLeave(object sender, MouseEventArgs e)
    {
        CancelHelpCloseTimer();
        _helpCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _helpCloseTimer.Tick += (s, args) =>
        {
            _helpCloseTimer!.Stop();
            HelpPopup.IsOpen = false;
        };
        _helpCloseTimer.Start();
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e) => HelpPopup.IsOpen = !HelpPopup.IsOpen;

    // ------------------------------------------------------------------
    // Row list
    // ------------------------------------------------------------------
    // Replaces the default ListBoxItem chrome (blue hover/selection tint,
    // focus rectangle) with a plain presenter so the rounded row Borders
    // are the only visible "blocks" in the list.
    private static Style BuildFlatItemContainerStyle()
    {
        var template = new ControlTemplate(typeof(ListBoxItem));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        template.VisualTree = presenter;

        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(TemplateProperty, template));
        style.Setters.Add(new Setter(FocusableProperty, false));
        return style;
    }

    private void RefreshList()
    {
        var p = _darkMode ? DarkPalette : LightPalette;
        ItemsList.Items.Clear();
        _rowBorders.Clear();

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            int rowIndex = i;

            // Each category is one rounded "block" — the visual unit you
            // lift out and slot somewhere else while drag-reordering.
            var rowBorder = new Border
            {
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = item.Excluded ? Brush(p["HeaderBrush"]) : Brush(p["SelectedBrush"]),
                Opacity = item.Excluded ? 0.45 : 1.0,
                SnapsToDevicePixels = true,
            };

            var row = new Grid { Margin = new Thickness(4, 3, 4, 3) }; // minimalist padding inside the block
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) }); // position #
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); // checkbox
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // name
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); // grip

            var posText = new TextBlock
            {
                Text = (i + 1).ToString() + ".",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                Foreground = Brush(p["SubTextBrush"]),
            };
            Grid.SetColumn(posText, 0);

            var checkBox = new CheckBox
            {
                IsChecked = !item.Excluded,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Uncheck to always unjoin this category from everything else",
            };
            checkBox.Checked += (s, a) => ToggleExcluded(rowIndex, false);
            checkBox.Unchecked += (s, a) => ToggleExcluded(rowIndex, true);
            Grid.SetColumn(checkBox, 1);

            var nameText = new TextBlock
            {
                Text = CategoryMap.DisplayNameFor(item.Key) + (item.Excluded ? "  \u2014 always unjoined" : ""),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                FontWeight = item.Excluded ? FontWeights.Normal : FontWeights.SemiBold,
                Foreground = item.Excluded ? Brush(p["SubTextBrush"]) : Brush(p["TextBrush"]),
            };
            Grid.SetColumn(nameText, 2);

            // Drag handle — port of the pyRevit grip (⋮⋮) on the row's right
            // edge. Press-and-drag it to move the row to any other position.
            var grip = new Border
            {
                Width = 26,
                Height = 24,
                CornerRadius = new CornerRadius(3),
                Cursor = Cursors.SizeAll,
                Background = Brushes.Transparent,
                ToolTip = "Drag to reorder",
            };
            grip.Child = new TextBlock
            {
                Text = "\u22EE\u22EE",
                FontSize = 13,
                Foreground = Brush(p["SubTextBrush"]),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            grip.MouseLeftButtonDown += (s, a) =>
            {
                if (a.LeftButton != MouseButtonState.Pressed) return;
                _dragSourceIndex = rowIndex;
                _rowBorders[rowIndex].Opacity = 0.35; // block lifted out of its slot
                try { DragDrop.DoDragDrop(ItemsList, rowIndex.ToString(), DragDropEffects.Move); }
                catch { /* ignore drag races on click */ }
                _dragSourceIndex = null;
                _dropTargetIndex = null;
                RefreshList(); // restore visuals once the drag is over
            };
            Grid.SetColumn(grip, 3);

            row.Children.Add(posText);
            row.Children.Add(checkBox);
            row.Children.Add(nameText);
            row.Children.Add(grip);

            rowBorder.Child = row;

            // Whole block is a drop target — same as row_border.AllowDrop in
            // the pyRevit script, with the target index captured per row.
            rowBorder.AllowDrop = true;
            rowBorder.DragOver += (s, a) =>
            {
                a.Effects = DragDropEffects.Move;
                a.Handled = true;
                var pos = a.GetPosition(rowBorder);
                ShowDropIndicator(rowIndex, pos.Y < rowBorder.ActualHeight / 2);
            };
            rowBorder.DragLeave += (s, a) => ClearDropIndicator(rowIndex);
            rowBorder.Drop += (s, a) =>
            {
                ClearDropIndicator(rowIndex);
                try
                {
                    var data = a.Data.GetData(DataFormats.StringFormat) as string;
                    if (data == null) return;
                    MoveTo(int.Parse(data), rowIndex);
                }
                catch { /* ignore malformed drag data */ }
            };

            _rowBorders.Add(rowBorder);
            ItemsList.Items.Add(new ListBoxItem { Content = rowBorder });
        }
    }

    // --- drag "bongkar pasang" feedback -----------------------------------
    // Source row ghosts at low opacity (block lifted out); the block under
    // the cursor shows where the dragged one will snap in via an accent
    // edge on the half (top/bottom) the pointer is currently in.
    private void ShowDropIndicator(int index, bool topEdge)
    {
        if (_dropTargetIndex == index && _dropEdgeTop == topEdge) return;
        ResetAllDropIndicators();
        if (_dragSourceIndex == index) return; // no slot marker on the ghost itself
        _dropTargetIndex = index;
        _dropEdgeTop = topEdge;
        var b = _rowBorders[index];
        b.BorderBrush = Brush(_darkMode ? DarkPalette["AccentBrush"] : LightPalette["AccentBrush"]);
        b.BorderThickness = topEdge ? new Thickness(0, 3, 0, 0) : new Thickness(0, 0, 0, 3);
    }

    private void ClearDropIndicator(int index)
    {
        if (_dropTargetIndex != index) return;
        _dropTargetIndex = null;
        ResetAllDropIndicators();
    }

    private void ResetAllDropIndicators()
    {
        foreach (var b in _rowBorders)
        {
            b.BorderBrush = null;
            b.BorderThickness = new Thickness(0);
        }
    }

    private void MoveTo(int sourceIndex, int targetIndex)
    {
        if (sourceIndex == targetIndex) return;
        if (sourceIndex < 0 || sourceIndex >= _items.Count) return;
        if (targetIndex < 0 || targetIndex >= _items.Count) return;
        var moved = _items[sourceIndex];
        _items.RemoveAt(sourceIndex);
        _items.Insert(targetIndex, moved);
        RefreshList();
    }

    private void ToggleExcluded(int index, bool excluded)
    {
        _items[index].Excluded = excluded;
        RefreshList();
    }

    // ------------------------------------------------------------------
    // Footer actions
    // ------------------------------------------------------------------
    private void ToggleAllButton_Click(object sender, RoutedEventArgs e)
    {
        // Everything on → unjoin the whole list; mixed or everything off →
        // turn the whole list back on.
        bool allIncluded = _items.All(it => !it.Excluded);
        foreach (var it in _items)
            it.Excluded = allIncluded;
        RefreshList();
    }

    private void DefaultButton_Click(object sender, RoutedEventArgs e)
    {
        _items.Clear();
        foreach (var key in CategoryMap.DefaultOrder)
            _items.Add(new RowItem { Key = key, Excluded = false });
        RefreshList();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ResultOrder = null;
        ResultExcluded = null;
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ResultOrder = _items.Select(it => it.Key).ToList();
        ResultExcluded = _items.Where(it => it.Excluded).Select(it => it.Key).ToList();
        ResultDarkMode = _darkMode;
        Close();
    }
}
