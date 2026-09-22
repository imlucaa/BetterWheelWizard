using Avalonia.Interactivity;
using WheelWizard.Shared.DependencyInjection;
using WheelWizard.Views.Popups.Base;
using WheelWizard.WiiManagement.MiiManagement;
using WheelWizard.WiiManagement.MiiManagement.Domain.Mii;

namespace WheelWizard.Views.Popups.MiiManagement;

public partial class MiiCarouselWindow : PopupContent
{
    [Inject]
    private IMiiDbService MiiDbService { get; set; } = null!;

    private Mii? _mii;

    public MiiCarouselWindow()
        : base(true, true, false, t("popup_title.mii_carousel"))
    {
        InitializeComponent();
    }

    public MiiCarouselWindow SetMii(Mii newMii)
    {
        _mii = newMii;
        Window.WindowTitle = newMii.Name.ToString();
        Carousel.MiiImageLoaded += DisableLoadingIcon;
        Carousel.Mii = newMii;
        return this;
    }

    private void SaveMii_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_mii == null)
            return;

        var serialized = MiiSerializer.Serialize(_mii);
        if (serialized.IsFailure)
        {
            ViewUtils.ShowSnackbar(serialized.Error.Message, ViewUtils.SnackbarType.Danger);
            return;
        }

        var copy = MiiSerializer.Deserialize(serialized.Value);
        if (copy.IsFailure)
        {
            ViewUtils.ShowSnackbar(copy.Error.Message, ViewUtils.SnackbarType.Danger);
            return;
        }

        var save = MiiDbService.AddToDatabase(copy.Value, "02:11:11:11:11:11");
        if (save.IsFailure)
        {
            ViewUtils.ShowSnackbar(save.Error.Message, ViewUtils.SnackbarType.Danger);
            return;
        }

        SaveMiiButton.IsEnabled = false;
        SaveMiiButton.Text = "Saved to My Miis";
        ViewUtils.ShowSnackbar($"Saved {_mii.Name} to My Miis.");
    }

    private void DisableLoadingIcon(object? sender, EventArgs e)
    {
        MiiLoadingIcon.IsVisible = false;
        Carousel.MiiImageLoaded -= DisableLoadingIcon;
    }
}
