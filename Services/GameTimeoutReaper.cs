using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Draughts.Services;

public sealed class GameTimeoutReaper : BackgroundService
{
    private readonly DraughtsService _Draughts;
    private readonly SettingsService _settings;
    private readonly ILogger<GameTimeoutReaper> _logger;

    public GameTimeoutReaper(DraughtsService Draughts, SettingsService settings, ILogger<GameTimeoutReaper> logger)
    {
        _Draughts = Draughts;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Process idle timeouts (MaxMoveTimeoutMins)
                var mins = await _settings.GetMaxMoveTimeoutMinsAsync(stoppingToken);
                var seconds = await _settings.GetReaperPeriodSecondsAsync(stoppingToken);
                var killGrace = TimeSpan.FromSeconds(Math.Max(1, seconds));
                var (removed, warnings) = _Draughts.ProcessIdleTimeouts(TimeSpan.FromMinutes(mins), killGrace, warningFraction: 0.8);
                if (warnings > 0)
                {
                    _logger.LogInformation("GameTimeoutReaper sent {Count} idle warning(s)", warnings);
                }
                if (removed > 0)
                {
                    _logger.LogInformation("GameTimeoutReaper removed {Count} game(s) after {Mins} mins idle", removed, mins);
                }

                // Process total game time timeouts (MaxGameTimeMins)
                var maxGameTimeMins = await _settings.GetMaxGameTimeMinsAsync(stoppingToken);
                var gameTimeRemoved = _Draughts.ProcessGameTimeTimeouts(TimeSpan.FromMinutes(maxGameTimeMins));
                if (gameTimeRemoved > 0)
                {
                    _logger.LogInformation("GameTimeoutReaper removed {Count} game(s) after {Mins} mins total game time", gameTimeRemoved, maxGameTimeMins);
                }

                // Process game start wait timeouts (MaxGameStartWaitTimeMins)
                var maxStartWaitMins = await _settings.GetMaxGameStartWaitTimeMinsAsync(stoppingToken);
                var startWaitRemoved = _Draughts.ProcessGameStartWaitTimeouts(TimeSpan.FromMinutes(maxStartWaitMins));
                if (startWaitRemoved > 0)
                {
                    _logger.LogInformation("GameTimeoutReaper removed {Count} game(s) after {Mins} mins waiting for players", startWaitRemoved, maxStartWaitMins);
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
