using Microsoft.AspNetCore.SignalR;
using Drafts.Hubs;
using System.Collections.Concurrent;

namespace Drafts.Services;

public class EnhancedVoiceChatService
{
    private readonly IHubContext<VoiceChatHub> _hubContext;
    private readonly ILogger<EnhancedVoiceChatService> _logger;
    private readonly ConcurrentDictionary<string, VoiceChatSession> _sessions = new();

    public EnhancedVoiceChatService(
        IHubContext<VoiceChatHub> hubContext,
        ILogger<EnhancedVoiceChatService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<bool> InitializeSessionAsync(string gameId, string userId, string userName)
    {
        try
        {
            var sessionId = $"{gameId}_{userId}";
            
            if (_sessions.ContainsKey(sessionId))
            {
                _logger.LogWarning($"Voice chat session already exists for {userId} in game {gameId}");
                return false;
            }

            var session = new VoiceChatSession
            {
                SessionId = sessionId,
                GameId = gameId,
                UserId = userId,
                UserName = userName,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _sessions[sessionId] = session;

            _logger.LogInformation($"Initialized voice chat session for {userName} in game {gameId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to initialize voice chat session for {userId} in game {gameId}");
            return false;
        }
    }

    public async Task<bool> JoinVoiceChatAsync(string gameId, string userId, string userName)
    {
        try
        {
            // Initialize session if not exists
            await InitializeSessionAsync(gameId, userId, userName);

            // Notify through SignalR
            await _hubContext.Clients.All.SendAsync("JoinVoiceChat", gameId, userName);

            _logger.LogInformation($"{userName} joined voice chat for game {gameId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to join voice chat for {userId} in game {gameId}");
            return false;
        }
    }

    public async Task<bool> LeaveVoiceChatAsync(string gameId, string userId)
    {
        try
        {
            var sessionId = $"{gameId}_{userId}";
            
            if (_sessions.TryRemove(sessionId, out var session))
            {
                session.IsActive = false;
                session.EndedAt = DateTime.UtcNow;

                // Notify through SignalR
                await _hubContext.Clients.All.SendAsync("LeaveVoiceChat", gameId);

                _logger.LogInformation($"{session.UserName} left voice chat for game {gameId}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to leave voice chat for {userId} in game {gameId}");
            return false;
        }
    }

    public async Task<bool> StartTalkingAsync(string gameId, string userId)
    {
        try
        {
            var sessionId = $"{gameId}_{userId}";
            
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.IsTalking = true;
                session.LastTalkActivity = DateTime.UtcNow;

                // Notify other participants
                await _hubContext.Clients.All.SendAsync("StartTalking", gameId, userId);

                _logger.LogDebug($"{session.UserName} started talking in game {gameId}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to start talking for {userId} in game {gameId}");
            return false;
        }
    }

    public async Task<bool> StopTalkingAsync(string gameId, string userId)
    {
        try
        {
            var sessionId = $"{gameId}_{userId}";
            
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.IsTalking = false;
                session.LastTalkActivity = DateTime.UtcNow;

                // Notify other participants
                await _hubContext.Clients.All.SendAsync("StopTalking", gameId, userId);

                _logger.LogDebug($"{session.UserName} stopped talking in game {gameId}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to stop talking for {userId} in game {gameId}");
            return false;
        }
    }

    public async Task<bool> RouteAudioDataAsync(string gameId, string fromUserId, byte[] audioData)
    {
        try
        {
            var sessionId = $"{gameId}_{fromUserId}";
            
            if (_sessions.TryGetValue(sessionId, out var fromSession) && fromSession.IsTalking)
            {
                // Find all other participants in the same game
                var otherParticipants = _sessions.Values
                    .Where(s => s.GameId == gameId && s.UserId != fromUserId && s.IsActive)
                    .ToList();

                // Route audio data to other participants
                var routingTasks = otherParticipants.Select(async participant =>
                {
                    try
                    {
                        await _hubContext.Clients.All.SendAsync("ReceiveAudioData", gameId, fromUserId, participant.UserId, audioData);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to route audio to {participant.UserId} in game {gameId}");
                    }
                });

                await Task.WhenAll(routingTasks);

                // Update session metrics
                fromSession.AudioBytesSent += audioData.Length;
                fromSession.LastAudioActivity = DateTime.UtcNow;

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to route audio data for {fromUserId} in game {gameId}");
            return false;
        }
    }

    public async Task<bool> UpdateParticipantMetricsAsync(string gameId, string userId, VoiceChatMetrics metrics)
    {
        try
        {
            var sessionId = $"{gameId}_{userId}";
            
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.Metrics = metrics;
                session.LastMetricsUpdate = DateTime.UtcNow;

                // Broadcast metrics to other participants
                await _hubContext.Clients.All.SendAsync("UpdateMetrics", gameId, userId, metrics);

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to update metrics for {userId} in game {gameId}");
            return false;
        }
    }

    public VoiceChatSession? GetSession(string gameId, string userId)
    {
        var sessionId = $"{gameId}_{userId}";
        return _sessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    public List<VoiceChatSession> GetActiveSessions(string gameId)
    {
        return _sessions.Values
            .Where(s => s.GameId == gameId && s.IsActive)
            .ToList();
    }

    public List<VoiceChatSession> GetTalkingParticipants(string gameId)
    {
        return _sessions.Values
            .Where(s => s.GameId == gameId && s.IsActive && s.IsTalking)
            .ToList();
    }

    public async Task CleanupInactiveSessions()
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-30); // Remove sessions inactive for 30 minutes
        var inactiveSessions = _sessions.Values
            .Where(s => s.IsActive && s.LastActivity < cutoffTime)
            .ToList();

        foreach (var session in inactiveSessions)
        {
            session.IsActive = false;
            session.EndedAt = DateTime.UtcNow;
            
            await LeaveVoiceChatAsync(session.GameId, session.UserId);
            
            _logger.LogInformation($"Cleaned up inactive voice chat session for {session.UserName} in game {session.GameId}");
        }
    }

    public VoiceChatStatistics GetStatistics(string gameId)
    {
        var sessions = _sessions.Values.Where(s => s.GameId == gameId).ToList();
        
        return new VoiceChatStatistics
        {
            TotalParticipants = sessions.Count,
            ActiveParticipants = sessions.Count(s => s.IsActive),
            TalkingParticipants = sessions.Count(s => s.IsTalking),
            AverageLatency = sessions.Where(s => s.Metrics != null).Select(s => s.Metrics!.AverageLatency).DefaultIfEmpty(0).Average(),
            AverageCpuUsage = sessions.Where(s => s.Metrics != null).Select(s => s.Metrics!.CpuUsage).DefaultIfEmpty(0).Average(),
            TotalAudioBytesTransmitted = sessions.Sum(s => s.AudioBytesSent),
            LastActivity = sessions.Max(s => s.LastActivity)
        };
    }
}

public class VoiceChatSession
{
    public string SessionId { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsTalking { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public DateTime? LastTalkActivity { get; set; }
    public DateTime? LastAudioActivity { get; set; }
    public DateTime? LastMetricsUpdate { get; set; }
    public VoiceChatMetrics? Metrics { get; set; }
    public long AudioBytesSent { get; set; }
}

public class VoiceChatStatistics
{
    public int TotalParticipants { get; set; }
    public int ActiveParticipants { get; set; }
    public int TalkingParticipants { get; set; }
    public double AverageLatency { get; set; }
    public double AverageCpuUsage { get; set; }
    public long TotalAudioBytesTransmitted { get; set; }
    public DateTime LastActivity { get; set; }
}
