using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NSubstitute;
using WheelWizard.GameBanana;
using WheelWizard.Models.Mods;
using WheelWizard.Mods.ViewModels;
using WheelWizard.Mods.Views;

namespace WheelWizard.UI.Test;

public class GridModPanelTests
{
    [AvaloniaFact]
    public void Card_ShowsTheOnChipAndAccentOnlyWhileTheModIsEnabled()
    {
        var mod = new Mod
        {
            Title = "Card test mod",
            IsEnabled = true,
            Priority = 3,
        };
        var preview = new ModPreviewViewModel(0, Substitute.For<IGameBananaSingletonService>(), Substitute.For<IGameBananaMediaService>());
        var panel = new GridModPanel { DataContext = new ModListItem(mod, false, false, preview) };
        var window = new Window
        {
            Content = panel,
            Width = 320,
            Height = 220,
        };
        try
        {
            window.Show();
            window.UpdateLayout();

            var border = panel.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "RootCardBorder");
            var onChip = panel.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "On");
            Assert.Contains("Enabled", border.Classes);
            Assert.True(onChip.IsEffectivelyVisible);
            Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Card test mod");

            mod.IsEnabled = false;
            window.UpdateLayout();

            Assert.DoesNotContain("Enabled", border.Classes);
            Assert.False(onChip.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }
}
