using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Drafts.Services;

public sealed class GameTimeoutReaper : BackgroundService
{
    private readonly DraftsService _drafts;
    private readonly SettingsService _settings;
    private readonly ILogger<GameTimeoutReaper> _logger;

    public GameTimeoutReaper(DraftsService drafts, SettingsService settings, ILogger<GameTimeoutReaper> logger)
    {
        _drafts = drafts;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var mins = await _settings.GetMaxTimeoutMinsAsync(stoppingToken);
                var seconds = await _settings.GetReaperPeriodSecondsAsync(stoppingToken);
                var killGrace = TimeSpan.FromSeconds(Math.Max(1, seconds));
                var (removed, warnings) = _drafts.ProcessIdleTimeouts(TimeSpan.FromMinutes(mins), killGrace, warningFraction: 0.8);
                if (warnings > 0)
                {
                    _logger.LogInformation("GameTimeoutReaper sent {Count} idle warning(s)", warnings);
                }
                if (removed > 0)
                {
                    _logger.LogInformation("GameTimeoutReaper removed {Count} game(s) after {Mins} mins idle", removed, mins);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GameTimeoutReaper failed");
            }

            try
            {
                var seconds = await _settings.GetReaperPeriodSecondsAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
