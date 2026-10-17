using WheelWizard.Localization;

namespace WheelWizard.Test.Features.Localization;

public class TranslationFunctionsTests
{
    [Theory]
    [InlineData("en", "action.play", "Play")]
    [InlineData("en", "action.save", "Save")]
    [InlineData("en", "action.change", "Change")]
    [InlineData("en", "action.browse", "Browse")]
    [InlineData("en", "action.import", "Import")]
    [InlineData("en", "action.enable_all", "Enable All")]
    [InlineData("en", "action.link.support", "Support us!")]
    [InlineData("en", "action.cancel", "Cancel")]
    [InlineData("en", "action.apply", "Apply")]
    [InlineData("ja", "action.play", "プレイ")]
    [InlineData("ja", "action.save", "保存")]
    [InlineData("ja", "action.change", "変更")]
    [InlineData("ja", "action.browse", "探す")]
    [InlineData("ja", "action.import", "インポート")]
    [InlineData("ja", "action.enable_all", "すべて有効")]
    [InlineData("ja", "action.link.support", "サポート")]
    [InlineData("ja", "action.cancel", "キャンセル")]
    [InlineData("ja", "action.apply", "適用")]
    public void EmbeddedActionTranslationsResolveWithoutExposingRawKeys(string language, string key, string expected)
    {
        var localization = new EmbeddedYamlLocalizationService();

        var result = localization.TranslateForLanguage(key, language);

        Assert.Equal(expected, result);
        Assert.NotEqual(key, result);
    }

    [Theory]
    [InlineData("en", "Do you want to apply the new language settings?")]
    [InlineData("ja", "新しい言語設定を適用しますか？")]
    public void LanguageConfirmationPromptUsesTheRequestedLanguage(string language, string expected)
    {
        var localization = new EmbeddedYamlLocalizationService();

        var result = localization.TranslateForLanguage("question.apply_language_settings.extra", language);

        Assert.Equal(expected, result);
        Assert.NotEqual("question.apply_language_settings.extra", result);
    }

    [Fact]
    public void CountIsReservedEvenWithoutAnExplicitCountArgument()
    {
        Assert.Throws<ArgumentException>(() => TranslationFunctions.tFormat("%{count}", new { count = 99 }));
        Assert.Throws<ArgumentException>(() => TranslationFunctions.t("items", new { count = 99 }));
    }

    [Fact(DisplayName = "Format with no params returns default string")]
    public void FormatWithNoParams_ShouldReturnDefaultString()
    {
        const string value = "Hello, World!";

        var result = TranslationFunctions.tFormat(value);

        Assert.Equal(value, result);
    }

    [Fact(DisplayName = "Format with null object param returns string with empty value")]
    public void FormatWithNullObjectParam_ShouldReturnStringWithEmptyValue()
    {
        const string value = "Hello, %{name}!";

        var result = TranslationFunctions.tFormat(value, new { name = (string?)null });

        Assert.Equal("Hello, !", result);
    }

    [Fact(DisplayName = "Format with object param returns string with object")]
    public void FormatWithObjectParam_ShouldReturnStringWithObject()
    {
        const string value = "Hello, %{name}!";

        var result = TranslationFunctions.tFormat(value, new { name = "World" });

        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void NamedArgumentsRespectTranslationOrderAndDoNotExpandInsertedValues()
    {
        var result = TranslationFunctions.tFormat(
            "%{second} / %{first} / %{second} / %{missing}",
            new { first = "%{second}", second = "Alex" }
        );

        Assert.Equal("Alex / %{second} / Alex / %{missing}", result);
    }
}
