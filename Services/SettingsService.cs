using Drafts.Data;
using Microsoft.EntityFrameworkCore;

namespace Drafts.Services;

public sealed class SettingsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lock = new();
    private int _maxMoveTimeoutMins = 5;
    private int _maxGameTimeMins = 30;
    private int _maxGameStartWaitTimeMins = 30;
    private int _maxLoginHrs = 4;
    private int _reaperPeriodSeconds = 30;
    private string _lastMoveHighlightColor = "rgba(255,0,0,0.85)";
    private bool _entrapmentMode = true;
    private double _multiJumpGraceSeconds = 1.5;
    private bool _gameInitiatorGoesFirst = true;
    private bool _allowPlayerPinChange = true;
    private bool _loaded;

    public SettingsService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public int MaxMoveTimeout
    {
        get
        {
            lock (_lock)
            {
                return _maxMoveTimeoutMins;
            }
        }
    }

    public int MaxGameTimeMins
    {
        get
        {
            lock (_lock)
            {
                return _maxGameTimeMins;
            }
        }
    }

    public int MaxGameStartWaitTimeMins
    {
        get
        {
            lock (_lock)
            {
                return _maxGameStartWaitTimeMins;
            }
        }
    }

    public int MaxLoginHrs
    {
        get
        {
            lock (_lock)
            {
                return _maxLoginHrs;
            }
        }
    }

    public int ReaperPeriodSeconds
    {
        get
        {
            lock (_lock)
            {
                return _reaperPeriodSeconds;
            }
        }
    }

    public string LastMoveHighlightColor
    {
        get
        {
            lock (_lock)
            {
                return _lastMoveHighlightColor;
            }
        }
    }

    public bool EntrapmentMode
    {
        get
        {
            lock (_lock)
            {
                return _entrapmentMode;
            }
        }
    }

    public double MultiJumpGraceSeconds
    {
        get
        {
            lock (_lock)
            {
                return _multiJumpGraceSeconds;
            }
        }
    }

    public bool GameInitiatorGoesFirst
    {
        get
        {
            lock (_lock)
            {
                return _gameInitiatorGoesFirst;
            }
        }
    }

    public bool AllowPlayerPinChange
    {
        get
        {
            lock (_lock)
            {
                return _allowPlayerPinChange;
            }
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is not null)
        {
            lock (_lock)
            {
                _maxMoveTimeoutMins = s.MaxMoveTimeoutMins;
                _maxGameTimeMins = s.MaxGameTimeMins;
                _maxGameStartWaitTimeMins = s.MaxGameStartWaitTimeMins;
                _maxLoginHrs = s.MaxLoginHrs;
                _reaperPeriodSeconds = s.ReaperPeriodSeconds;
                _lastMoveHighlightColor = string.IsNullOrWhiteSpace(s.LastMoveHighlightColor)
                    ? "rgba(255,0,0,0.85)"
                    : s.LastMoveHighlightColor;
                _entrapmentMode = s.EntrapmentMode;
                _multiJumpGraceSeconds = s.MultiJumpGraceSeconds;
                _gameInitiatorGoesFirst = s.GameInitiatorGoesFirst;
                _allowPlayerPinChange = s.AllowPlayerPinChange;
                _loaded = true;
            }
        }
        else
        {
            lock (_lock)
            {
                _maxMoveTimeoutMins = 5;
                _maxGameTimeMins = 30;
                _maxGameStartWaitTimeMins = 30;
                _maxLoginHrs = 4;
                _reaperPeriodSeconds = 30;
                _lastMoveHighlightColor = "rgba(255,0,0,0.85)";
                _entrapmentMode = true;
                _multiJumpGraceSeconds = 1.5;
                _gameInitiatorGoesFirst = true;
                _allowPlayerPinChange = true;
                _loaded = true;
            }
        }
    }

    public async Task<int> GetMaxMoveTimeoutMinsAsync(CancellationToken cancellationToken = default)
    {
        var needLoad = false;
        lock (_lock)
        {
            needLoad = !_loaded;
        }

        if (needLoad)
        {
            await LoadAsync(cancellationToken);
        }

        lock (_lock)
        {
            return _maxMoveTimeoutMins;
        }
    }

    public async Task<int> GetMaxGameTimeMinsAsync(CancellationToken cancellationToken = default)
    {
        var needLoad = false;
        lock (_lock)
        {
            needLoad = !_loaded;
        }

        if (needLoad)
        {
            await LoadAsync(cancellationToken);
        }

        lock (_lock)
        {
            return _maxGameTimeMins;
        }
    }

    public async Task<int> GetMaxGameStartWaitTimeMinsAsync(CancellationToken cancellationToken = default)
    {
        var needLoad = false;
        lock (_lock)
        {
            needLoad = !_loaded;
        }

        if (needLoad)
        {
            await LoadAsync(cancellationToken);
        }

        lock (_lock)
        {
            return _maxGameStartWaitTimeMins;
        }
    }

    public async Task<int> GetMaxLoginHrsAsync(CancellationToken cancellationToken = default)
    {
        var needLoad = false;
        lock (_lock)
        {
            needLoad = !_loaded;
        }

        if (needLoad)
        {
            await LoadAsync(cancellationToken);
        }

        lock (_lock)
        {
            return _maxLoginHrs;
        }
    }

    public async Task<int> GetReaperPeriodSecondsAsync(CancellationToken cancellationToken = default)
    {
        var needLoad = false;
        lock (_lock)
        {
            needLoad = !_loaded;
        }

        if (needLoad)
        {
            await LoadAsync(cancellationToken);
        }

        lock (_lock)
        {
            return _reaperPeriodSeconds;
        }
    }

    public async Task<string> GetLastMoveHighlightColorAsync(CancellationToken cancellationToken = default)
    {
        var needLoad = false;
        lock (_lock)
        {
            needLoad = !_loaded;
        }

        if (needLoad)
        {
            await LoadAsync(cancellationToken);
        }

        lock (_lock)
        {
            return _lastMoveHighlightColor;
        }
    }

    public async Task<bool> GetEntrapmentModeAsync(CancellationToken cancellationToken = default)
    {
        var needLoad = false;
        lock (_lock)
        {
            needLoad = !_loaded;
        }

        if (needLoad)
        {
            await LoadAsync(cancellationToken);
        }

        lock (_lock)
        {
            return _entrapmentMode;
        }
    }

    public async Task<double> GetMultiJumpGraceSecondsAsync(CancellationToken cancellationToken = default)
    {
        var needLoad = false;
        lock (_lock)
        {
            needLoad = !_loaded;
        }

        if (needLoad)
        {
            await LoadAsync(cancellationToken);
        }

        lock (_lock)
        {
            return _multiJumpGraceSeconds;
        }
    }

    public async Task<bool> GetGameInitiatorGoesFirstAsync(CancellationToken cancellationToken = default)
    {
        var needLoad = false;
        lock (_lock)
        {
            needLoad = !_loaded;
        }

        if (needLoad)
        {
            await LoadAsync(cancellationToken);
        }

        lock (_lock)
        {
            return _gameInitiatorGoesFirst;
        }
    }

    public async Task<bool> GetAllowPlayerPinChangeAsync(CancellationToken cancellationToken = default)
    {
        var needLoad = false;
        lock (_lock)
        {
            needLoad = !_loaded;
        }

        if (needLoad)
        {
            await LoadAsync(cancellationToken);
        }

        lock (_lock)
        {
            return _allowPlayerPinChange;
        }
    }

    public async Task<bool> UpdateMaxMoveTimeoutMinsAsync(int newValue, CancellationToken cancellationToken = default)
    {
        if (newValue <= 0) return false;
        if (newValue > 24 * 60) return false;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is null)
        {
            s = new AppSettings { Id = 1, MaxMoveTimeoutMins = newValue };
            db.Settings.Add(s);
        }
        else
        {
            s.MaxMoveTimeoutMins = newValue;
        }

        await db.SaveChangesAsync(cancellationToken);

        lock (_lock)
        {
            _maxMoveTimeoutMins = newValue;
            _loaded = true;
        }

        return true;
    }

    public async Task<bool> UpdateMaxGameTimeMinsAsync(int newValue, CancellationToken cancellationToken = default)
    {
        if (newValue <= 0) return false;
        if (newValue > 24 * 60) return false;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is null)
        {
            s = new AppSettings { Id = 1, MaxGameTimeMins = newValue };
            db.Settings.Add(s);
        }
        else
        {
            s.MaxGameTimeMins = newValue;
        }

        await db.SaveChangesAsync(cancellationToken);

        lock (_lock)
        {
            _maxGameTimeMins = newValue;
            _loaded = true;
        }

        return true;
    }

    public async Task<bool> UpdateMaxGameStartWaitTimeMinsAsync(int newValue, CancellationToken cancellationToken = default)
    {
        if (newValue <= 0) return false;
        if (newValue > 24 * 60) return false;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is null)
        {
            s = new AppSettings { Id = 1, MaxGameStartWaitTimeMins = newValue };
            db.Settings.Add(s);
        }
        else
        {
            s.MaxGameStartWaitTimeMins = newValue;
        }

        await db.SaveChangesAsync(cancellationToken);

        lock (_lock)
        {
            _maxGameStartWaitTimeMins = newValue;
            _loaded = true;
        }

        return true;
    }

    public async Task<bool> UpdateMaxLoginHrsAsync(int newValue, CancellationToken cancellationToken = default)
    {
        if (newValue <= 0) return false;
        if (newValue > 24 * 7) return false; // Max 1 week

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is null)
        {
            s = new AppSettings { Id = 1, MaxLoginHrs = newValue };
            db.Settings.Add(s);
        }
        else
        {
            s.MaxLoginHrs = newValue;
        }

        await db.SaveChangesAsync(cancellationToken);

        lock (_lock)
        {
            _maxLoginHrs = newValue;
            _loaded = true;
        }

        return true;
    }

    public async Task<bool> UpdateReaperPeriodSecondsAsync(int newValue, CancellationToken cancellationToken = default)
    {
        if (newValue < 5) return false;
        if (newValue > 10 * 60) return false;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is null)
        {
            s = new AppSettings { Id = 1, MaxMoveTimeoutMins = 30, MaxGameTimeMins = 30, MaxGameStartWaitTimeMins = 30, MaxLoginHrs = 4, ReaperPeriodSeconds = newValue };
            db.Settings.Add(s);
        }
        else
        {
            s.ReaperPeriodSeconds = newValue;
        }

        await db.SaveChangesAsync(cancellationToken);

        lock (_lock)
        {
            _reaperPeriodSeconds = newValue;
            _loaded = true;
        }

        return true;
    }

    public async Task<bool> UpdateLastMoveHighlightColorAsync(string? newValue, CancellationToken cancellationToken = default)
    {
        newValue = (newValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(newValue)) return false;
        if (newValue.Length > 64) return false;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is null)
        {
            s = new AppSettings
            {
                Id = 1,
                MaxMoveTimeoutMins = 5,
                MaxGameTimeMins = 30,
                MaxGameStartWaitTimeMins = 30,
                MaxLoginHrs = 4,
                ReaperPeriodSeconds = 30,
                LastMoveHighlightColor = newValue
            };
            db.Settings.Add(s);
        }
        else
        {
            s.LastMoveHighlightColor = newValue;
        }

        await db.SaveChangesAsync(cancellationToken);

        lock (_lock)
        {
            _lastMoveHighlightColor = newValue;
            _loaded = true;
        }

        return true;
    }

    public async Task<bool> UpdateEntrapmentModeAsync(bool newValue, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is null)
        {
            s = new AppSettings
            {
                Id = 1,
                MaxMoveTimeoutMins = 5,
                MaxGameTimeMins = 30,
                MaxGameStartWaitTimeMins = 30,
                MaxLoginHrs = 4,
                ReaperPeriodSeconds = 30,
                LastMoveHighlightColor = "rgba(255,0,0,0.85)",
                EntrapmentMode = newValue
            };
            db.Settings.Add(s);
        }
        else
        {
            s.EntrapmentMode = newValue;
        }

        await db.SaveChangesAsync(cancellationToken);

        lock (_lock)
        {
            _entrapmentMode = newValue;
            _loaded = true;
        }

        return true;
    }

    public async Task<bool> UpdateMultiJumpGraceSecondsAsync(double newValue, CancellationToken cancellationToken = default)
    {
        if (newValue < 0) return false;
        if (newValue > 60) return false;

        // Must be a multiple of 0.5 seconds.
        var halfSteps = newValue * 2.0;
        if (Math.Abs(halfSteps - Math.Round(halfSteps)) > 0.0000001) return false;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is null)
        {
            s = new AppSettings
            {
                Id = 1,
                MaxMoveTimeoutMins = 5,
                MaxGameTimeMins = 30,
                MaxGameStartWaitTimeMins = 30,
                MaxLoginHrs = 4,
                ReaperPeriodSeconds = 30,
                LastMoveHighlightColor = "rgba(255,0,0,0.85)",
                EntrapmentMode = true,
                MultiJumpGraceSeconds = newValue
            };
            db.Settings.Add(s);
        }
        else
        {
            s.MultiJumpGraceSeconds = newValue;
        }

        await db.SaveChangesAsync(cancellationToken);

        lock (_lock)
        {
            _multiJumpGraceSeconds = newValue;
            _loaded = true;
        }

        return true;
    }

    public async Task<bool> UpdateGameInitiatorGoesFirstAsync(bool newValue, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is null)
        {
            s = new AppSettings
            {
                Id = 1,
                MaxMoveTimeoutMins = 5,
                MaxGameTimeMins = 30,
                MaxGameStartWaitTimeMins = 30,
                MaxLoginHrs = 4,
                ReaperPeriodSeconds = 30,
                LastMoveHighlightColor = "rgba(255,0,0,0.85)",
                EntrapmentMode = true,
                MultiJumpGraceSeconds = 1.5,
                GameInitiatorGoesFirst = newValue
            };
            db.Settings.Add(s);
        }
        else
        {
            s.GameInitiatorGoesFirst = newValue;
        }

        await db.SaveChangesAsync(cancellationToken);

        lock (_lock)
        {
            _gameInitiatorGoesFirst = newValue;
            _loaded = true;
        }

        return true;
    }

    public async Task<bool> UpdateAllowPlayerPinChangeAsync(bool newValue, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is null)
        {
            s = new AppSettings
            {
                Id = 1,
                MaxMoveTimeoutMins = 5,
                MaxGameTimeMins = 30,
                MaxGameStartWaitTimeMins = 30,
                MaxLoginHrs = 4,
                ReaperPeriodSeconds = 30,
                LastMoveHighlightColor = "rgba(255,0,0,0.85)",
                EntrapmentMode = true,
                MultiJumpGraceSeconds = 1.5,
                GameInitiatorGoesFirst = true,
                AllowPlayerPinChange = newValue
            };
            db.Settings.Add(s);
        }
        else
        {
            s.AllowPlayerPinChange = newValue;
        }

        await db.SaveChangesAsync(cancellationToken);

        lock (_lock)
        {
            _allowPlayerPinChange = newValue;
            _loaded = true;
        }

        return true;
    }
}
