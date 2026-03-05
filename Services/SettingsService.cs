using Drafts.Data;
using Microsoft.EntityFrameworkCore;

namespace Drafts.Services;

public sealed class SettingsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lock = new();
    private int _maxTimeoutMins = 30;
    private int _reaperPeriodSeconds = 30;
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
                _loaded = true;
            }
        }
        else
        {
            lock (_lock)
            {
                _maxTimeoutMins = 30;
                _reaperPeriodSeconds = 30;
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
}
