using Avalonia.Controls;
using WheelWizard.Settings;
using WheelWizard.Settings.Types;
using WheelWizard.Views.Dialogs;

namespace WheelWizard.Settings.Views;

public partial class LanguageSettings : UserControl
{
    private readonly ISettingsManager _settings;
    private readonly ISettingsLocalizationService _localization;
    private bool _updatingLanguageDropdown;

    private sealed record LanguageDropdownItem(string Key, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    public LanguageSettings(ISettingsManager settings, ISettingsLocalizationService localization)
    {
        _settings = settings;
        _localization = localization;
        InitializeComponent();
        RefreshLanguageDropdown();
        RefreshTranslationCredit();
        WhWzLanguageDropdown.SelectionChanged += WhWzLanguageDropdown_OnSelectionChanged;
    }

    private void RefreshLanguageDropdown()
    {
        var currentLanguage = _settings.WW_LANGUAGE.Get();
        _updatingLanguageDropdown = true;
        try
        {
            WhWzLanguageDropdown.Items.Clear();
            foreach (var (key, displayNameFactory) in SettingValues.WhWzLanguages)
                WhWzLanguageDropdown.Items.Add(new LanguageDropdownItem(key, displayNameFactory()));

            WhWzLanguageDropdown.SelectedItem = WhWzLanguageDropdown
                .Items.OfType<LanguageDropdownItem>()
                .FirstOrDefault(item => item.Key == currentLanguage);
        }
        finally
        {
            _updatingLanguageDropdown = false;
        }
    }

    private void RefreshTranslationCredit()
    {
        TranslationsPercentageText.Text = t("text.language_translated_by", new { translators = t("value.language.z_translators") });
        TranslationsPercentageText.IsVisible = t("value.language.z_translators") != "-";
    }

    private async void WhWzLanguageDropdown_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingLanguageDropdown || WhWzLanguageDropdown.SelectedItem is not LanguageDropdownItem selectedLanguage)
            return;

        var currentLanguage = _settings.WW_LANGUAGE.Get();
        if (selectedLanguage.Key == currentLanguage)
            return;

        var promptCurrent = t($"{currentLanguage}.question.apply_language_settings.extra");
        var promptTarget = t($"{selectedLanguage.Key}.question.apply_language_settings.extra");
        var apply = await new YesNoWindow()
            .SetMainText($"{promptCurrent}\n\n{promptTarget}")
            .SetButtonText(t("action.apply"), t("action.cancel"))
            .AwaitAnswer();

        if (!apply)
        {
            RefreshLanguageDropdown();
            return;
        }

        if (SettingsEditing.Set(_settings, _settings.WW_LANGUAGE, selectedLanguage.Key))
        {
            _localization.ApplyCurrentLanguage();
            RefreshLanguageDropdown();
            RefreshTranslationCredit();
        }
    }
}
