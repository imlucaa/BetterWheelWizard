using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace WheelWizard.UI.Test;

public class ColorPickerStyleTests
{
    [AvaloniaFact]
    public void ThemeColorPicker_RendersASpectrumWithRealSize()
    {
        var picker = new Border
        {
            Height = 290,
            ClipToBounds = true,
            Child = new ColorView
            {
                Width = 340,
                Margin = new Avalonia.Thickness(0, -48, 0, 0),
                Color = Color.Parse("#F04444"),
                ColorModel = ColorModel.Hsva,
                ColorSpectrumComponents = ColorSpectrumComponents.SaturationValue,
                ColorSpectrumShape = ColorSpectrumShape.Box,
                IsAlphaEnabled = false,
                IsAlphaVisible = false,
                IsAccentColorsVisible = false,
                IsColorPaletteVisible = false,
                IsColorPreviewVisible = false,
                IsColorSpectrumVisible = true,
                IsColorSpectrumSliderVisible = true,
                IsColorComponentsVisible = false,
                IsComponentSliderVisible = false,
                IsComponentTextInputVisible = false,
                IsColorModelVisible = false,
                IsHexInputVisible = false,
            },
        };
        var window = new Window
        {
            Content = picker,
            Width = 420,
            Height = 520,
        };
        try
        {
            window.Show();
            window.UpdateLayout();

            var view = picker.GetVisualDescendants().OfType<ColorView>().Single();
            var spectrum = view.GetVisualDescendants().OfType<ColorSpectrum>().FirstOrDefault();
            var report = string.Join(
                "; ",
                view.GetVisualDescendants()
                    .Select(v => $"{v.GetType().Name}[{(v as Control)?.Bounds.Width:0}x{(v as Control)?.Bounds.Height:0}]")
                    .Take(40)
            );
            Assert.True(
                spectrum is { Bounds.Width: > 50, Bounds.Height: > 50 },
                $"view {view.Bounds}; spectrum {spectrum?.Bounds}; tree: {report}"
            );
        }
        finally
        {
            window.Close();
        }
    }
}
