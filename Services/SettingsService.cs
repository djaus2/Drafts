using Drafts.Data;
using Microsoft.EntityFrameworkCore;

namespace Drafts.Services;

public sealed class SettingsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lock = new();
    private int _maxTimeoutMins = 30;
    private int _reaperPeriodSeconds = 30;
    private string _lastMoveHighlightColor = "rgba(255,0,0,0.85)";
    private bool _entrapmentMode = true;
    private double _multiJumpGraceSeconds = 1.5;
    private bool _gameInitiatorGoesFirst = true;
    private bool _loaded;

    public SettingsService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public int MaxTimeoutMins
    {
        get
        {
            lock (_lock)
            {
                return _maxTimeoutMins;
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

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is not null)
        {
            lock (_lock)
            {
                _maxTimeoutMins = s.MaxTimeoutMins;
                _reaperPeriodSeconds = s.ReaperPeriodSeconds;
                _lastMoveHighlightColor = string.IsNullOrWhiteSpace(s.LastMoveHighlightColor)
                    ? "rgba(255,0,0,0.85)"
                    : s.LastMoveHighlightColor;
                _entrapmentMode = s.EntrapmentMode;
                _multiJumpGraceSeconds = s.MultiJumpGraceSeconds;
                _gameInitiatorGoesFirst = s.GameInitiatorGoesFirst;
                _loaded = true;
            }
        }
        else
        {
            lock (_lock)
            {
                _maxTimeoutMins = 30;
                _reaperPeriodSeconds = 30;
                _lastMoveHighlightColor = "rgba(255,0,0,0.85)";
                _entrapmentMode = true;
                _multiJumpGraceSeconds = 1.5;
                _gameInitiatorGoesFirst = true;
                _loaded = true;
            }
        }
    }

    public async Task<int> GetMaxTimeoutMinsAsync(CancellationToken cancellationToken = default)
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
            return _maxTimeoutMins;
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

    public async Task<bool> UpdateMaxTimeoutMinsAsync(int newValue, CancellationToken cancellationToken = default)
    {
        if (newValue <= 0) return false;
        if (newValue > 24 * 60) return false;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is null)
        {
            s = new AppSettings { Id = 1, MaxTimeoutMins = newValue };
            db.Settings.Add(s);
        }
        else
        {
            s.MaxTimeoutMins = newValue;
        }

        await db.SaveChangesAsync(cancellationToken);

        lock (_lock)
        {
            _maxTimeoutMins = newValue;
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
            s = new AppSettings { Id = 1, MaxTimeoutMins = 30, ReaperPeriodSeconds = newValue };
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
                MaxTimeoutMins = 30,
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
                MaxTimeoutMins = 30,
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
                MaxTimeoutMins = 30,
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
                MaxTimeoutMins = 30,
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
}
