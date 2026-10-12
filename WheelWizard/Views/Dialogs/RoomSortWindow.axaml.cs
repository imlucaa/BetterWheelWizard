using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WheelWizard.Views.Popups.Base;

namespace WheelWizard.Views.Popups.Generic;

public partial class RoomSortWindow : PopupContent
{
    private TaskCompletionSource<int?>? _tcs;

    public RoomSortWindow()
        : base(true, false, true, "Room sort")
    {
        InitializeComponent();
    }

    public async Task<int?> AwaitAnswer()
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return await Dispatcher.UIThread.InvokeAsync(() => AwaitAnswer());

        _tcs = new();
        Show();
        return await _tcs.Task;
    }

    protected override void BeforeClose()
    {
        _tcs?.TrySetResult(null);
    }

    private void SubmitSelection(int value)
    {
        _tcs?.TrySetResult(value);
        Close();
    }

    private void AllRoomsButton_OnClick(object? sender, RoutedEventArgs e) => SubmitSelection(0);

    private void LowestToHighestButton_OnClick(object? sender, RoutedEventArgs e) => SubmitSelection(1);

    private void HighestToLowestButton_OnClick(object? sender, RoutedEventArgs e) => SubmitSelection(2);

    private void LowestRoomOnlyButton_OnClick(object? sender, RoutedEventArgs e) => SubmitSelection(3);

    private void HighestRoomOnlyButton_OnClick(object? sender, RoutedEventArgs e) => SubmitSelection(4);
}
