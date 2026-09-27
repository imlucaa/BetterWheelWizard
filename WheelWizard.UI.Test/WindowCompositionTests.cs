using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WheelWizard.CustomCharacters;
using WheelWizard.RrRooms;
using WheelWizard.Shared.Services;
using WheelWizard.Views;
using WheelWizard.Views.Components;
using WheelWizard.Views.Patterns;
using WheelWizard.Views.Popups;
using WheelWizard.Views.Popups.Base;
using WheelWizard.Views.Popups.Generic;
using WizardButton = WheelWizard.Views.Components.Button;

namespace WheelWizard.UI.Test;

public class WindowCompositionTests
{
    [AvaloniaFact]
    public void MiiCustomCharacterButtons_UseCtmkfFontFirst()
    {
        var characters = new CustomCharactersService().GetCustomCharacters();
        var popup = new TextInputWindow().SetCustomCharacters(characters, initiallyOpen: true);
        var symbolButton = popup.GetLogicalDescendants().OfType<WizardButton>().First(button => button.Text == "\ue000");
        var typeface = new Typeface(symbolButton.FontFamily, symbolButton.FontStyle, symbolButton.FontWeight, symbolButton.FontStretch);

        Assert.StartsWith("CTMKF", symbolButton.FontFamily.Name, StringComparison.OrdinalIgnoreCase);
        Assert.True(FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface));
        Assert.Equal("CTMKF", glyphTypeface.FamilyName, ignoreCase: true);
        Assert.All(
            characters,
            character =>
            {
                Assert.True(glyphTypeface.CharacterToGlyphMap.TryGetGlyph(character, out var glyph), $"Missing U+{(int)character:X4}");
                Assert.NotEqual(0, glyph);
            }
        );
    }

    [AvaloniaFact]
    public void DefaultUiFont_LoadsBundledRobotoTypeface()
    {
        var family = Assert.IsType<FontFamily>(Application.Current!.FindResource("DefaultFont"));
        var typeface = new Typeface(family);

        Assert.True(FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface));
        Assert.Equal("Roboto", glyphTypeface.FamilyName, ignoreCase: true);
    }

    [AvaloniaFact]
    public void VrChange_UsesSameTextColorAsStartAndEndValues()
    {
        var caller = Substitute.For<IApiCaller<IRwfcApi>>();
        var graph = new VrHistoryGraph(new VrHistoryViewModel(caller)) { TotalChange = 8532 };
        var window = new Window
        {
            Content = graph,
            Width = 560,
            Height = 390,
        };
        try
        {
            window.Show();
            window.UpdateLayout();
            var gainText = graph.FindControl<SelectableTextBlock>("TotalChangeGainText")!;
            var gainLabel = graph.FindControl<FormFieldLabel>("TotalChangeLabel")!;
            var startingLabel = graph.FindControl<FormFieldLabel>("StartingVrLabel")!;
            var startingValue = graph.FindControl<SelectableTextBlock>("StartingVrValue")!;
            Assert.True(gainText.IsVisible);
            Assert.InRange(gainText.Bounds.Top - gainLabel.Bounds.Bottom, -2, 1);
            Assert.InRange(startingValue.Bounds.Top - startingLabel.Bounds.Bottom, -2, 1);
            Assert.Equal(
                Assert.IsAssignableFrom<ISolidColorBrush>(startingValue.Foreground).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(gainText.Foreground).Color
            );
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ThemeLibraryFactory_PassesThemeNamesAsOneConstructorArgument()
    {
        var factory = new PopupFactory(new ServiceCollection().BuildServiceProvider());

        var popup = factory.CreateThemeLibrary(["Blue", "Purple"]);

        Assert.NotNull(popup);
    }

    [AvaloniaFact]
    public void ThemePaletteChange_UpdatesExistingStyledControls()
    {
        var resources = Application.Current!.Resources;
        var original = resources["Neutral700"];
        var button = new WizardButton { Variant = WizardButton.ButtonsVariantType.Default };
        var window = new Window { Content = button };
        try
        {
            window.Show();
            window.UpdateLayout();
            resources["Neutral700"] = Color.Parse("#123456");
            window.UpdateLayout();

            Assert.Equal(Color.Parse("#123456"), Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color);
        }
        finally
        {
            resources["Neutral700"] = original;
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PopupScale_UsesApplicationResourcesWithoutAServiceProvider()
    {
        Application.Current!.Resources[WindowAppearance.ScaleResourceKey] = 1.5;
        var popup = new PopupWindow(true, false, false, "Scale test");
        try
        {
            popup.Show();
            popup.UpdateLayout();
            Assert.Equal(1.5, popup.RequestedWindowScale);
            Application.Current.Resources[WindowAppearance.ScaleResourceKey] = 1.25;
            Assert.Equal(1.25, popup.RequestedWindowScale);
        }
        finally
        {
            popup.Close();
            Application.Current.Resources.Remove(WindowAppearance.ScaleResourceKey);
        }
    }

    [AvaloniaFact]
    public void OwnedPopups_DisableOnlyTheirWindowGroup_AndRestoreInteraction()
    {
        var first = new TestWindow();
        var second = new TestWindow();
        var popup = new TestWindow();
        var nested = new TestWindow();
        try
        {
            first.Show();
            second.Show();
            Assert.True(first.CanInteract);
            Assert.True(second.CanInteract);
            popup.Show(first);
            Assert.False(first.CanInteract);
            Assert.True(second.CanInteract);
            nested.Show(popup);
            Assert.False(popup.CanInteract);
            nested.Close();
            Assert.True(popup.CanInteract);
            Assert.False(first.CanInteract);
            popup.Close();
            Assert.True(first.CanInteract);
        }
        finally
        {
            nested.Close();
            popup.Close();
            first.Close();
            second.Close();
        }
    }

    [AvaloniaFact]
    public void ClosingOwner_ClosesItsNonmodalPopups()
    {
        var owner = new TestWindow();
        var popup = new TestWindow(allowParentInteraction: true);
        owner.Show();
        popup.Show(owner);
        Assert.True(owner.CanInteract);
        owner.Close();
        Assert.False(popup.IsVisible);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PopupShow_PreservesOwnershipAndClosesWithItsOwner(bool allowParentInteraction)
    {
        var owner = new TestWindow();
        owner.Show();
        var popup = new OwnedPopup(owner, allowParentInteraction);
        try
        {
            popup.Show();
            Assert.Same(owner, popup.Owner);
            Assert.Equal(allowParentInteraction, owner.CanInteract);
            owner.Close();
            Assert.False(popup.IsVisible);
        }
        finally
        {
            popup.Close();
            owner.Close();
        }
    }

    private sealed class OwnedPopup : PopupWindow
    {
        public OwnedPopup(Window owner, bool allowParentInteraction)
            : base(true, allowParentInteraction, false, "Owned popup")
        {
            Owner = owner;
        }
    }

    private sealed class TestWindow : BaseWindow
    {
        private readonly Border _overlay = new();
        private readonly Border _content = new();
        protected override Control InteractionOverlay => _overlay;
        protected override Control InteractionContent => _content;
        public bool CanInteract => _content.IsEnabled;

        public TestWindow(bool allowParentInteraction = false)
        {
            AllowParentInteraction = allowParentInteraction;
            Content = _content;
        }
    }
}
