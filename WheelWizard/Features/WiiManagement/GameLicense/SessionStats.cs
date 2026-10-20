using Microsoft.Extensions.Logging;
using WheelWizard.Shared.Polling;
using WheelWizard.WiiManagement.GameLicense.Domain;

namespace WheelWizard.WiiManagement.GameLicense;

/// <summary>What changed on a license since the launcher was opened.</summary>
/// <param name="Races">Races played since then.</param>
/// <param name="Wins">Races won since then.</param>
/// <param name="VrChange">VR gained (positive) or lost (negative) since then.</param>
public sealed record SessionStats(int Races, int Wins, int VrChange)
{
    /// <summary>False until a race was played or VR moved, so there is nothing to show before then.</summary>
    public bool HasActivity => Races > 0 || VrChange != 0;
}

/// <summary>
/// Remembers each license as it first appears and reports how far it has moved since. A license is found by its friend
/// code, so switching between licenses (or regions) keeps a separate count for each one.
/// </summary>
public sealed class SessionStatsCalculator
{
    private readonly Dictionary<string, (long Races, long Wins, long Vr)> _baseline = [];

    /// <summary>Records a license that has not been seen yet, and starts over for one whose save was replaced by an older one.</summary>
    public void Observe(string? friendCode, uint races, uint wins, uint vr)
    {
        var code = DigitsOf(friendCode);
        if (code.Length == 0 || code.All(c => c == '0'))
            return;

        // A save that went backwards (restored or imported) would show negative races, so count from that save instead.
        if (!_baseline.TryGetValue(code, out var start) || races < start.Races)
            _baseline[code] = (races, wins, vr);
    }

    /// <summary>The change since the license was first seen, or null for a license that has not been seen.</summary>
    public SessionStats? For(string? friendCode, uint races, uint wins, uint vr)
    {
        if (!_baseline.TryGetValue(DigitsOf(friendCode), out var start))
            return null;

        return new SessionStats(
            (int)Math.Max(0, races - start.Races),
            (int)Math.Max(0, wins - start.Wins),
            (int)Math.Clamp(vr - start.Vr, int.MinValue, int.MaxValue)
        );
    }

    private static string DigitsOf(string? friendCode) => new((friendCode ?? string.Empty).Where(char.IsDigit).ToArray());
}

public interface ISessionStats
{
    /// <summary>The selected license's change since the launcher opened, or null when there is no license to show.</summary>
    SessionStats? Current { get; }

    event Action? Changed;
}

/// <summary>
/// Keeps the session numbers for the selected license. The launcher only reads the game save when something asks it to,
/// so this checks every few seconds whether the save file was written (the game writes it after races) and, when it was,
/// reads it again. It runs on the same UI-thread scheduler as the other live services, so reading the licenses never
/// overlaps a page that is using them.
/// </summary>
public sealed class SessionStatsTracker : ObservablePollingService, ISessionStats
{
    private readonly IGameLicenseSingletonService _licenses;
    private readonly SessionStatsCalculator _calculator = new();
    private DateTime? _lastWrite;
    private bool _seenOnce;

    public SessionStatsTracker(
        IGameLicenseSingletonService licenses,
        IPollingScheduler scheduler,
        TimeProvider timeProvider,
        ILogger<SessionStatsTracker> logger
    )
        : base(5, scheduler, timeProvider, logger)
    {
        _licenses = licenses;
    }

    public SessionStats? Current { get; private set; }

    public event Action? Changed;

    protected override Task ExecuteTaskAsync(CancellationToken cancellationToken)
    {
        Refresh();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Re-reads the save when its file changed since the last check, then updates the numbers. The first check only takes
    /// what the launcher already loaded as the starting point.
    /// </summary>
    public void Refresh()
    {
        var written = _licenses.GetSaveWriteTimeUtc();
        if (_seenOnce && written != _lastWrite)
        {
            _licenses.LoadLicense();
            // A fresh read forgets which players are online, so put that back right away.
            _licenses.RefreshOnlineStatus();
        }

        _lastWrite = written;
        _seenOnce = true;
        Evaluate();
    }

    private void Evaluate()
    {
        LicenseProfile active;
        try
        {
            foreach (var user in _licenses.LicenseCollection.Users)
                _calculator.Observe(user.FriendCode, user.TotalRaceCount, user.TotalWinCount, user.Vr);
            active = _licenses.ActiveUser;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException or NullReferenceException)
        {
            // No license is loaded yet. The next check will have it.
            return;
        }

        var next = _calculator.For(active.FriendCode, active.TotalRaceCount, active.TotalWinCount, active.Vr);
        if (Equals(next, Current))
            return;

        Current = next;
        Changed?.Invoke();
    }
}
