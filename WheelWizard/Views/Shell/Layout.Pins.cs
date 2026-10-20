using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using WheelWizard.Settings;
using WheelWizard.Settings.Views;
using WheelWizard.Views.Shell.Controls;
using WheelWizard.Views.Shell.Views;

namespace WheelWizard.Views.Shell;

/// <summary>
/// Settings tabs pinned to the sidebar: a + and - under the list add and remove them, and each pinned tab opens as a
/// page of its own. They are sidebar buttons like the others, so they follow compact and wide.
/// </summary>
public partial class Layout
{
    private sealed record PinnableTab(
        string Tag,
        Type Page,
        Func<string> Label,
        string Icon,
        Func<ISettingsManager, bool>? Available = null
    );

    private static readonly PinnableTab[] PinnableTabs =
    [
        new("WhWzSettings", typeof(WhWzSettings), () => t("value.wheel_wizard"), "WheelIcon"),
        new("VideoSettings", typeof(VideoSettings), () => t("category.dolphin"), "DolphinIcon", settings => !settings.IsRecompModeActive()),
        new("RecompSettings", typeof(RecompSettings), () => "WiiCompiled", "Gear", settings => settings.IsRecompModeActive()),
        new("AppearanceSettings", typeof(AppearanceSettings), () => t("category.appearance"), "Eye"),
        new("LanguageSettings", typeof(LanguageSettings), () => t("category.language"), "Translation"),
        new("OtherSettings", typeof(OtherSettings), () => t("category.other"), "Gear"),
        new("DiscordSettings", typeof(DiscordSettings), () => "Discord RPC", "DiscordIcon"),
        new("OverlaySettings", typeof(OverlaySettings), () => "VR overlay", "Play"),
        new("FriendCodeSettings", typeof(FriendCodeSettings), () => "Friend codes", "UserCouple"),
        new("BackupSettings", typeof(BackupSettings), () => t("category.backup"), "FileExport"),
        new("GhostSettings", typeof(GhostSettings), () => "Ghosts", "Road"),
    ];

    private readonly List<SidebarRadioButton> _pinButtons = [];
    private readonly Dictionary<SidebarRadioButton, string> _pinKeys = [];
    private Border? _addPinRow;
    private Border? _removePinRow;
    private Border? _editPinRow;
    private StackPanel? _pinActions;

    private bool IsAvailable(PinnableTab tab) => tab.Available?.Invoke(SettingsService) ?? true;

    private List<PinnableTab> PinnedTabs()
    {
        var tags = SettingsService
            .SIDEBAR_PINNED_TABS.Get()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<PinnableTab>();
        foreach (var tag in tags)
        {
            var tab = PinnableTabs.FirstOrDefault(candidate => candidate.Tag == tag);
            if (tab is not null && result.All(existing => existing.Tag != tab.Tag))
                result.Add(tab);
        }

        return result;
    }

    private void SavePinned(IEnumerable<PinnableTab> tabs) =>
        SettingsEditing.Set(SettingsService, SettingsService.SIDEBAR_PINNED_TABS, string.Join(',', tabs.Select(tab => tab.Tag)));

    // The palette entries are colours (or brushes), so both are accepted.
    private IBrush? PinBrush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var found)
            ? found switch
            {
                IBrush brush => brush,
                Color color => new SolidColorBrush(color),
                _ => null,
            }
            : null;

    private Geometry? PinIcon(string key) => this.TryFindResource(key, ActualThemeVariant, out var found) ? found as Geometry : null;

    private void InitializePins()
    {
        _addPinRow = CreatePinAction("M 5 0 L 5 10 M 0 5 L 10 5", "Add a settings tab", add: true);
        _removePinRow = CreatePinAction("M 0 5 L 10 5", "Remove a settings tab", add: false);
        _editPinRow = CreatePinAction(null, "Arrange the sidebar", add: null);
        _pinActions = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { _editPinRow },
        };
        RebuildPins();
    }

    /// <summary>Makes a button for every pinned tab, then lays the whole sidebar out again.</summary>
    private void RebuildPins()
    {
        if (_addPinRow is null || _removePinRow is null || _pinActions is null)
            return;

        foreach (var button in _pinButtons)
            SidePanelButtons.Children.Remove(button);
        _pinButtons.Clear();
        _pinKeys.Clear();

        var pinned = PinnedTabs();
        foreach (var tab in pinned)
        {
            var button = new SidebarRadioButton
            {
                IconData = PinIcon(tab.Icon) ?? PinIcon("Settings")!,
                Text = tab.Label(),
                PageType = tab.Page,
            };
            button.Classes.Set("compact", _sidebarCollapsed);
            var page = tab.Page;
            button.NavigationRequested += (_, _) => NavigateFromPin(page);
            _pinButtons.Add(button);
            _pinKeys[button] = PinKey(tab);
        }

        _addPinRow.IsVisible = PinnableTabs.Any(tab => IsAvailable(tab) && pinned.All(existing => existing.Tag != tab.Tag));
        _removePinRow.IsVisible = pinned.Count > 0;
        ArrangeSidebar();
        ApplyPinActionsCompact(_sidebarCollapsed);
        UpdatePinSelection(ContentArea.Content as UserControl);
    }

    private static string PinKey(PinnableTab tab) => "pin:" + tab.Tag;

    // A small round button like the sidebar toggle, so the sidebar keeps one look.
    private Border CreatePinAction(string? glyph, string tip, bool? add)
    {
        var muted = PinBrush("Neutral300");
        Control icon = glyph is not null
            ? new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(glyph),
                Stroke = muted,
                StrokeThickness = 1.8,
                StrokeLineCap = PenLineCap.Round,
                Width = 10,
                Height = 10,
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }
            : new PathIcon
            {
                Data = PinIcon("Pen"),
                Width = 11,
                Height = 11,
                Foreground = muted,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        void Paint(IBrush? brush)
        {
            switch (icon)
            {
                case Avalonia.Controls.Shapes.Path path:
                    path.Stroke = brush;
                    break;
                case PathIcon pathIcon:
                    pathIcon.Foreground = brush;
                    break;
            }
        }

        var circle = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(13),
            BorderThickness = new Thickness(1),
            BorderBrush = PinBrush("Neutral600"),
            Background = PinBrush("FrameColor") ?? Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = icon,
        };
        ToolTip.SetTip(circle, tip);
        ToolTip.SetPlacement(circle, PlacementMode.Right);
        ToolTip.SetShowDelay(circle, 300);
        circle.PointerEntered += (_, _) =>
        {
            circle.Background = PinBrush("Neutral700");
            Paint(PinBrush("Neutral100"));
        };
        circle.PointerExited += (_, _) =>
        {
            circle.Background = PinBrush("FrameColor") ?? Brushes.Transparent;
            Paint(muted);
        };
        circle.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(circle).Properties.IsLeftButtonPressed)
                return;
            if (add is null)
                OpenSidebarEditor();
            else
                ShowPinMenu(circle, add.Value);
        };
        return circle;
    }

    private void ShowPinMenu(Control anchor, bool add)
    {
        var pinned = PinnedTabs();
        var items = new List<Control>();
        var choices = add ? PinnableTabs.Where(tab => IsAvailable(tab) && pinned.All(existing => existing.Tag != tab.Tag)) : pinned;
        foreach (var tab in choices)
        {
            var item = new MenuItem { Header = tab.Label() };
            var chosen = tab;
            item.Click += (_, _) =>
            {
                SavePinned(add ? [.. pinned, chosen] : pinned.Where(existing => existing.Tag != chosen.Tag));
                RebuildPins();
            };
            items.Add(item);
        }

        if (items.Count == 0)
            return;
        var menu = new ContextMenu { Placement = PlacementMode.Right, ItemsSource = items };
        menu.Open(anchor);
    }

    private void NavigateFromPin(Type tab)
    {
        Navigation.NavigateTo<PinnedTabPage>(tab);
        // If the current page asked to stay (unsaved work), keep highlighting where you really are.
        if (Navigation.CurrentPage is { } current && !(current is PinnedTabPage pinned && pinned.Tab == tab))
            UpdateSidebarSelection(current);
    }

    /// <summary>A pinned tab is highlighted while its page is open.</summary>
    private void UpdatePinSelection(UserControl? page)
    {
        var tab = (page as PinnedTabPage)?.Tab;
        foreach (var button in _pinButtons)
            button.IsChecked = tab is not null && button.PageType == tab;
    }

    private void ApplyPinActionsCompact(bool compact)
    {
        if (_pinActions is null)
            return;
        // Wide: the round buttons sit side by side under the list. Compact: stacked, centred in the narrow bar.
        _pinActions.Orientation = compact ? Orientation.Vertical : Orientation.Horizontal;
        _pinActions.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _pinActions.Margin = compact ? new Thickness(0, 8, 0, 8) : new Thickness(19, 8, 0, 8);
    }
}
