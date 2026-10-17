using Avalonia;

namespace WheelWizard.Views.Components;

// Compatibility adapter for BetterWheelWizard views that still use the old property names.
public sealed class Button : ActionButton
{
    public Button()
    {
        // Legacy defaults must also be reflected by the modern ActionButton properties.
        // Avalonia does not raise a property change when XAML explicitly assigns the
        // same value as a styled property's default (for example Variant="Default").
        base.Variant = ActionButtonVariant.Button;
        Tone = ActionButtonTone.Secondary;
        Size = WheelWizard.Views.Components.ButtonSize.Regular;
    }

    public static readonly StyledProperty<ButtonsVariantType> LegacyVariantProperty = AvaloniaProperty.Register<Button, ButtonsVariantType>(
        nameof(Variant),
        ButtonsVariantType.Default
    );
    public static readonly StyledProperty<ButtonsSizeType> LegacyButtonSizeProperty = AvaloniaProperty.Register<Button, ButtonsSizeType>(
        nameof(ButtonSize),
        ButtonsSizeType.Regular
    );
    public new ButtonsVariantType Variant
    {
        get => GetValue(LegacyVariantProperty);
        set => SetValue(LegacyVariantProperty, value);
    }
    public ButtonsSizeType ButtonSize
    {
        get => GetValue(LegacyButtonSizeProperty);
        set => SetValue(LegacyButtonSizeProperty, value);
    }

    public enum ButtonsVariantType
    {
        Primary,
        Warning,
        Default,
        Danger,
        UglyLight,
    }

    public enum ButtonsSizeType
    {
        Regular,
        Compact,
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LegacyVariantProperty)
        {
            base.Variant = ActionButtonVariant.Button;
            Tone = Variant switch
            {
                ButtonsVariantType.Primary => ActionButtonTone.Brand,
                ButtonsVariantType.Danger or ButtonsVariantType.Warning => ActionButtonTone.Danger,
                ButtonsVariantType.UglyLight => ActionButtonTone.Primary,
                _ => ActionButtonTone.Secondary,
            };
        }

        if (change.Property == LegacyButtonSizeProperty)
            Size =
                ButtonSize == ButtonsSizeType.Compact
                    ? WheelWizard.Views.Components.ButtonSize.Compact
                    : WheelWizard.Views.Components.ButtonSize.Regular;
    }
}
