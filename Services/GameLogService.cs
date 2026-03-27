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

    // Structured logging methods
    public async Task LogPlayerActionAsync(LogType logType, int playerId, string? details = null, int? gameId = null, int? groupId = null)
    {
        await LogStructuredAsync(logType, playerId, details, gameId, null, groupId);
    }

    public async Task LogGameEventAsync(LogType logType, int playerId, int gameId, string? details = null, int? opponentPlayerId = null, int? groupId = null)
    {
        await LogStructuredAsync(logType, playerId, details, gameId, opponentPlayerId, groupId);
    }

    public async Task LogSystemEventAsync(LogType logType, int adminPlayerId, string? details = null, int? gameId = null, int? groupId = null)
    {
        await LogStructuredAsync(logType, adminPlayerId, details, gameId, null, groupId);
    }

    public async Task LogWinAsync(int winnerPlayerId, int loserPlayerId, int gameId, string? details = null, int? groupId = null)
    {
        await LogStructuredAsync(LogType.GameEnded, winnerPlayerId, details, gameId, loserPlayerId, groupId);
    }

    private async Task LogStructuredAsync(LogType logType, int playerId, string? details = null, int? gameId = null, int? opponentPlayerId = null, int? groupId = null)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            db.GameLogs.Add(new GameLog
            {
                Timestamp = DateTime.UtcNow,
                LogType = logType,
                PlayerId = playerId,
                GameId = gameId,
                GroupId = groupId,
                OpponentPlayerId = opponentPlayerId,
                Details = details
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write structured game log: {LogType}, PlayerId: {PlayerId}, GameId: {GameId}", 
                logType, playerId, gameId);
        }
    }

    // Backward compatibility method
    public async Task LogAsync(string message)
    {
        // For backward compatibility, treat this as a system event with admin ID (assuming admin is ID 1)
        await LogSystemEventAsync(LogType.SystemStartup, 1, message);
    }

    // Enhanced query methods
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

    // New search methods
    public async Task<List<GameLog>> GetLogsByPlayerAsync(int playerId, int count = 100)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.GameLogs
                .Where(x => x.PlayerId == playerId || x.OpponentPlayerId == playerId)
                .OrderByDescending(x => x.Timestamp)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve game logs for player {PlayerId}", playerId);
            return new List<GameLog>();
        }
    }

    public async Task<List<GameLog>> GetLogsByGameAsync(int gameId)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.GameLogs
                .Where(x => x.GameId == gameId)
                .OrderBy(x => x.Timestamp)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve game logs for game {GameId}", gameId);
            return new List<GameLog>();
        }
    }

    public async Task<List<GameLog>> GetLogsByTypeAsync(LogType logType, int count = 100)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.GameLogs
                .Where(x => x.LogType == logType)
                .OrderByDescending(x => x.Timestamp)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve game logs for type {LogType}", logType);
            return new List<GameLog>();
        }
    }

    public async Task<List<GameLog>> SearchLogsAsync(int? playerId = null, int? gameId = null, int? groupId = null, LogType? logType = null, DateTime? startDate = null, DateTime? endDate = null, int count = 100)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var query = db.GameLogs.AsQueryable();

            if (playerId.HasValue)
            {
                query = query.Where(x => x.PlayerId == playerId.Value || x.OpponentPlayerId == playerId.Value);
            }

            if (gameId.HasValue)
            {
                query = query.Where(x => x.GameId == gameId.Value);
            }

            if (groupId.HasValue)
            {
                query = query.Where(x => x.GroupId == groupId.Value);
            }

            if (logType.HasValue)
            {
                query = query.Where(x => x.LogType == logType.Value);
            }

            if (startDate.HasValue)
            {
                query = query.Where(x => x.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(x => x.Timestamp <= endDate.Value);
            }

            return await query
                .OrderByDescending(x => x.Timestamp)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search game logs");
            return new List<GameLog>();
        }
    }

    public async Task<List<PlayerWinsLosses>> GetPlayerWinsLossesAsync(int playerId)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            
            // Get all game ended events where this player was involved
            var gameEndLogs = await db.GameLogs
                .Where(x => x.LogType == LogType.GameEnded && 
                           (x.PlayerId == playerId || x.OpponentPlayerId == playerId))
                .OrderByDescending(x => x.Timestamp)
                .ToListAsync();

            var results = new List<PlayerWinsLosses>();

            foreach (var log in gameEndLogs)
            {
                // Skip games without groups (public games are no longer allowed)
                if (!log.GroupId.HasValue)
                    continue;

                var isWinner = log.PlayerId == playerId;
                var opponentId = isWinner ? log.OpponentPlayerId : log.PlayerId;
                var gameId = log.GameId ?? 0;
                var groupId = log.GroupId;

                // Create or update the player's record
                var existingRecord = results.FirstOrDefault(r => r.GroupId == groupId);
                if (existingRecord == null)
                {
                    existingRecord = new PlayerWinsLosses
                    {
                        GroupId = groupId
                    };
                    results.Add(existingRecord);
                }

                if (isWinner)
                {
                    existingRecord.Wins++;
                }
                else
                {
                    existingRecord.Losses++;
                }
            }

            // Sort by total games (wins + losses) descending
            return results.OrderByDescending(r => r.Wins + r.Losses).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get player wins/losses for player {PlayerId}", playerId);
            return new List<PlayerWinsLosses>();
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
