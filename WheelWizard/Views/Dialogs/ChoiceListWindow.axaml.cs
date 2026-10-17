using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using WheelWizard.Views.Dialogs.Base;

namespace WheelWizard.Views.Dialogs;

/// <param name="Label">Main text of the row.</param>
/// <param name="Tag">Small muted text on the right of the row.</param>
/// <param name="Highlight">Draws the row bold and slightly lighter, e.g. for a detected default.</param>
public sealed record ChoiceOption(string Label, string Tag, bool Highlight = false);

/// <summary>A flat list popup that resolves to the index of the chosen option, or null when dismissed.</summary>
public partial class ChoiceListWindow : PopupContent
{
    private TaskCompletionSource<int?>? _tcs;

    public ChoiceListWindow(string title, string heading, string description, IReadOnlyList<ChoiceOption> options)
        : base(true, false, true, title)
    {
        InitializeComponent();
        HeadingText.Text = heading;
        DescriptionText.Text = description;

        var labelColor = Application.Current!.FindResource("FormFieldLabelColor") is Color color ? color : Colors.White;
        for (var index = 0; index < options.Count; index++)
        {
            var selectedIndex = index;
            var option = options[index];
            var label = new TextBlock
            {
                Text = option.Label,
                FontWeight = option.Highlight ? FontWeight.Bold : FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = new SolidColorBrush(labelColor),
            };
            var tag = new TextBlock
            {
                Text = option.Tag,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(labelColor, option.Highlight ? 1.0 : 0.55),
            };
            Grid.SetColumn(tag, 1);

            var button = new Avalonia.Controls.Button
            {
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { label, tag } },
            };
            button.Classes.Add("ChoiceRow");
            if (option.Highlight)
                button.Classes.Add("Active");
            button.Click += (_, _) => SubmitSelection(selectedIndex);
            ChoiceList.Children.Add(button);
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
