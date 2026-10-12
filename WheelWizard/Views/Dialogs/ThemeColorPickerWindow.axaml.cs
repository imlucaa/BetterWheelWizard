using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using WheelWizard.Views.Popups.Base;

namespace WheelWizard.Views.Popups.Generic;

public partial class ThemeColorPickerWindow : PopupContent
{
    private TaskCompletionSource<Color?>? _completion;
    private Color _selectedColor;
    private bool _isUpdatingHex;

    public ThemeColorPickerWindow(string title, Color initialColor)
        : base(true, false, true, "Theme color")
    {
        InitializeComponent();
        PickerTitle.Text = title;
        _selectedColor = initialColor;
        Picker.Color = initialColor;
        HexTextBox.Text = ToHex(initialColor);
    }

    public async Task<Color?> AwaitColor()
    {
        _completion = new();
        Show();
        return await _completion.Task;
    }

    private void Picker_OnColorChanged(object? sender, ColorChangedEventArgs e)
    {
        _selectedColor = e.NewColor;
        _isUpdatingHex = true;
        HexTextBox.Text = ToHex(_selectedColor);
        _isUpdatingHex = false;
    }

    private void HexTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdatingHex)
            return;

        var text = HexTextBox.Text?.Trim() ?? string.Empty;
        if (text.Length == 6 && !text.StartsWith('#'))
            text = $"#{text}";
        if (!WheelWizard.Themes.LauncherThemeService.IsValidHexColor(text))
            return;

        _selectedColor = Color.Parse(text);
        Picker.Color = _selectedColor;
    }

    private void UseColor_OnClick(object? sender, RoutedEventArgs e)
    {
        _completion?.TrySetResult(_selectedColor);
        Close();
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close();

    protected override void BeforeClose() => _completion?.TrySetResult(null);

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
