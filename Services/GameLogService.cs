using Drafts.Data;
using Microsoft.EntityFrameworkCore;

namespace Drafts.Services;

public class GameLogService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<GameLogService> _logger;

    public GameLogService(IDbContextFactory<AppDbContext> dbFactory, ILogger<GameLogService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task LogAsync(string message)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            db.GameLogs.Add(new GameLog
            {
                Timestamp = DateTime.UtcNow,
                Message = message
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write game log: {Message}", message);
        }
    }

    public async Task<List<GameLog>> GetRecentLogsAsync(int count = 100)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.GameLogs
                .OrderByDescending(x => x.Timestamp)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve game logs");
            return new List<GameLog>();
        }
    }

    public async Task<List<GameLog>> GetLogsSinceAsync(DateTime since)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.GameLogs
                .Where(x => x.Timestamp >= since)
                .OrderByDescending(x => x.Timestamp)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve game logs since {Since}", since);
            return new List<GameLog>();
        }
    }

    public async Task ClearOldLogsAsync(int keepDays = 30)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var cutoff = DateTime.UtcNow.AddDays(-keepDays);
            var oldLogs = await db.GameLogs
                .Where(x => x.Timestamp < cutoff)
                .ToListAsync();
            
            if (oldLogs.Any())
            {
                db.GameLogs.RemoveRange(oldLogs);
                await db.SaveChangesAsync();
                _logger.LogInformation("Cleared {Count} old game logs older than {Days} days", oldLogs.Count, keepDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear old game logs");
        }
    }
}
