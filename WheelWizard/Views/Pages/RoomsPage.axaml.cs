using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WheelWizard.Models.RRInfo;
using WheelWizard.Services.LiveData;
using WheelWizard.Utilities.RepeatedTasks;

namespace WheelWizard.Views.Pages;

public partial class RoomsPage : UserControlBase, INotifyPropertyChanged, IRepeatedTaskListener
{
    private string? _searchQuery;
    private int? _minimumAverageVr;
    private int? _maximumAverageVr;
    private int _averageVrSort;
    private bool _isPageReady;

    private readonly ObservableCollection<RrRoom> _rooms = [];
    private readonly DispatcherTimer _raceTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public ObservableCollection<RrRoom> Rooms
    {
        get => _rooms;
        init
        {
            _rooms = value;
            OnPropertyChanged(nameof(Rooms));
        }
    }

    private readonly ObservableCollection<RrPlayer> _players = [];

    public ObservableCollection<RrPlayer> Players
    {
        get => _players;
        init
        {
            _players = value;
            OnPropertyChanged(nameof(Players));
        }
    }

    public RoomsPage()
    {
        InitializeComponent();
        DataContext = this;
        _isPageReady = true;
        RRLiveRooms.Instance.Subscribe(this);

        _raceTimer.Tick += RaceTimer_OnTick;
        _raceTimer.Start();

        OnUpdate(RRLiveRooms.Instance);
        Unloaded += RoomsPage_Unloaded;
    }

    public void OnUpdate(RepeatedTaskManager sender)
    {
        if (sender is not RRLiveRooms liveRooms)
            return;

        var count = liveRooms.RoomCount;
        EmptyRoomsView.IsVisible = count == 0;
        if (count == 0)
        {
            Rooms.Clear();
            Players.Clear();
            RoomsListViewContainer.IsVisible = false;
            PlayerListViewContainer.IsVisible = false;
            return;
        }

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var query = _searchQuery?.Trim();
        IEnumerable<RrRoom> filteredRooms = RRLiveRooms
            .Instance.CurrentRooms.Where(room => room.PlayerCount > 0)
            .Where(room => !_minimumAverageVr.HasValue || room.AverageVr >= _minimumAverageVr.Value)
            .Where(room => !_maximumAverageVr.HasValue || room.AverageVr <= _maximumAverageVr.Value)
            .Where(room =>
                string.IsNullOrWhiteSpace(query)
                || room.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || room.GameMode.Contains(query, StringComparison.OrdinalIgnoreCase)
                || room.GameModeAbbrev.Contains(query, StringComparison.OrdinalIgnoreCase)
                || room.Players.Any(player =>
                    player.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || player.FriendCode.Contains(query, StringComparison.OrdinalIgnoreCase)
                )
            );

        filteredRooms = _averageVrSort switch
        {
            1 => filteredRooms.OrderBy(room => room.AverageVr),
            2 => filteredRooms.OrderByDescending(room => room.AverageVr),
            3 => filteredRooms.OrderBy(room => room.AverageVr).Take(1),
            4 => filteredRooms.OrderByDescending(room => room.AverageVr).Take(1),
            _ => filteredRooms,
        };

        var visibleRooms = filteredRooms.ToList();

        Rooms.Clear();
        foreach (var room in visibleRooms)
            Rooms.Add(room);

        RoomsListItemCount.Text = visibleRooms.Count.ToString();
        RoomsListViewContainer.IsVisible = visibleRooms.Count != 0;
        PlayerListViewContainer.IsVisible = false;
        EmptyRoomsView.IsVisible = visibleRooms.Count == 0;
    }

    private void RoomsPage_Unloaded(object? sender, RoutedEventArgs e)
    {
        _raceTimer.Stop();
        RRLiveRooms.Instance.Unsubscribe(this);
    }

    private void RaceTimer_OnTick(object? sender, EventArgs e)
    {
        foreach (var room in Rooms)
            room.RefreshRaceTime();
    }

    private void PlayerSearchField_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (e.Source is not TextBox textBox)
            return;
        _searchQuery = textBox.Text;
        ApplyFilters();
    }

    private void VrFilter_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        _minimumAverageVr = ParseVrFilter(MinimumVrField.Text);
        _maximumAverageVr = ParseVrFilter(MaximumVrField.Text);
        ApplyFilters();
    }

    private void AverageVrSortDropdown_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // SelectedIndex is applied while InitializeComponent is still constructing the page.
        // Do not touch the remaining named controls until the page has finished loading.
        if (!_isPageReady || sender is not ComboBox comboBox)
            return;

        _averageVrSort = comboBox.SelectedIndex;
        ApplyFilters();
    }

    internal static int? ParseVrFilter(string? text) =>
        int.TryParse(text, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var value) && value >= 0
            ? value
            : null;

    private void RoomsView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not ListBox listBox)
            return;
        if (listBox.SelectedItem is not RrRoom selectedRoom)
            return;

        NavigationManager.NavigateTo<RoomDetailsPage>(selectedRoom);
        listBox.SelectedItem = null;
        // Deselect the item immediately after navigating. This is important
        // for a good user experience. Otherwise, the item stays selected,
        // and if you navigate back, you can't re-select the same item.
    }

    private void PlayerView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not ListBox listBox)
            return;
        if (listBox.SelectedItem is not RrPlayer selectedRoom)
            return;

        var room = Rooms.FirstOrDefault(r => r.Players.Any(p => p.Equals(selectedRoom)));

        NavigationManager.NavigateTo<RoomDetailsPage>(room);
        listBox.SelectedItem = null;
        // Deselect the item immediately after navigating. This is important
        // for a good user experience. Otherwise, the item stays selected,
        // and if you navigate back, you can't re-select the same item.
    }

    #region PropertyChanged

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new(propertyName));
    }

    #endregion
}
