using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using WheelWizard.Views.Components;
using WheelWizard.Views.Dialogs.Base;
using Button = WheelWizard.Views.Components.Button;

namespace WheelWizard.Views.Dialogs;

public partial class ThemeLibraryWindow : PopupContent
{
    private TaskCompletionSource<int?>? _tcs;

    public ThemeLibraryWindow(IReadOnlyList<string> themeNames)
        : base(true, false, true, "Theme library")
    {
        InitializeComponent();

        var icon = (Avalonia.Media.Geometry)Application.Current!.FindResource("ThemePalette")!;
        for (var index = 0; index < themeNames.Count; index++)
        {
            var selectedIndex = index;
            var button = new Button
            {
                Text = themeNames[index],
                IconData = icon,
                Variant = Button.ButtonsVariantType.Default,
                Tone = ActionButtonTone.Brand,
                ButtonSize = Button.ButtonsSizeType.Regular,
                MinHeight = 42,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            button.Click += (_, _) => SubmitSelection(selectedIndex);
            ThemeList.Children.Add(button);
        }
    }

    public async Task<int?> AwaitAnswer()
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return await Dispatcher.UIThread.InvokeAsync(AwaitAnswer);

        _tcs = new();
        Show();
        return await _tcs.Task;
    }

    protected override void BeforeClose() => _tcs?.TrySetResult(null);

    private void SubmitSelection(int index)
    {
        _tcs?.TrySetResult(index);
        Close();
    }
}
