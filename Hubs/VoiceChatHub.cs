using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Drafts.Hubs;

public class VoiceChatHub : Hub
{
    private static readonly ConcurrentDictionary<string, HashSet<string>> GameParticipants = new();
    private static readonly ConcurrentDictionary<string, string> UserGameMapping = new();
    private static readonly ConcurrentDictionary<string, VoiceChatParticipant> Participants = new();

    public async Task JoinVoiceChat(string gameId, string userName)
    {
        var userId = Context.ConnectionId;
        var participantId = $"{gameId}_{userId}";
        
        // Add to game group
        await Groups.AddToGroupAsync(userId, gameId);
        
        // Track participants
        GameParticipants.AddOrUpdate(gameId, 
            new HashSet<string> { userId }, 
            (key, existing) => { existing.Add(userId); return existing; });
        
        // Map user to game
        UserGameMapping[userId] = gameId;
        
        // Store participant info
        Participants[participantId] = new VoiceChatParticipant
        {
            ConnectionId = userId,
            GameId = gameId,
            UserName = userName,
            IsTalking = false,
            JoinedAt = DateTime.UtcNow
        };
        
        // Notify others about new participant
        await Clients.OthersInGroup(gameId).SendAsync("ParticipantJoined", new
        {
            ConnectionId = userId,
            UserName = userName,
            JoinedAt = DateTime.UtcNow
        });
        
        // Send current participant list to new user
        var currentParticipants = GetParticipantsInGame(gameId);
        await Clients.Caller.SendAsync("ParticipantsList", currentParticipants);
        
        Console.WriteLine($"[VoiceChatHub] {userName} joined voice chat for game {gameId}");
    }

    public async Task LeaveVoiceChat(string gameId)
    {
        var userId = Context.ConnectionId;
        var participantId = $"{gameId}_{userId}";
        
        // Remove from game group
        await Groups.RemoveFromGroupAsync(userId, gameId);
        
        // Remove from tracking
        GameParticipants.TryRemove(gameId, out var participants);
        participants?.Remove(userId);
        
        UserGameMapping.TryRemove(userId, out _);
        Participants.TryRemove(participantId, out _);
        
        // Notify others
        await Clients.OthersInGroup(gameId).SendAsync("ParticipantLeft", new
        {
            ConnectionId = userId,
            LeftAt = DateTime.UtcNow
        });
        
        Console.WriteLine($"[VoiceChatHub] User left voice chat for game {gameId}");
    }

    public async Task StartTalking(string gameId)
    {
        var userId = Context.ConnectionId;
        var participantId = $"{gameId}_{userId}";
        
        if (Participants.TryGetValue(participantId, out var participant))
        {
            participant.IsTalking = true;
            participant.StartedTalkingAt = DateTime.UtcNow;
        }
        
        // Notify others in the game
        await Clients.OthersInGroup(gameId).SendAsync("ParticipantStartedTalking", new
        {
            ConnectionId = userId,
            StartedAt = DateTime.UtcNow
        });
        
        Console.WriteLine($"[VoiceChatHub] {userId} started talking in game {gameId}");
    }

    public async Task StopTalking(string gameId)
    {
        var userId = Context.ConnectionId;
        var participantId = $"{gameId}_{userId}";
        
        if (Participants.TryGetValue(participantId, out var participant))
        {
            participant.IsTalking = false;
            participant.StoppedTalkingAt = DateTime.UtcNow;
        }
        
        // Notify others in the game
        await Clients.OthersInGroup(gameId).SendAsync("ParticipantStoppedTalking", new
        {
            ConnectionId = userId,
            StoppedAt = DateTime.UtcNow
        });
        
        Console.WriteLine($"[VoiceChatHub] {userId} stopped talking in game {gameId}");
    }

    public async Task SendAudioData(string gameId, byte[] audioData)
    {
        var userId = Context.ConnectionId;
        var participantId = $"{gameId}_{userId}";
        
        // Only forward if participant is currently talking
        if (Participants.TryGetValue(participantId, out var participant) && participant.IsTalking)
        {
            // Forward audio data to other participants
            await Clients.OthersInGroup(gameId).SendAsync("ReceiveAudioData", new
            {
                SenderConnectionId = userId,
                AudioData = audioData,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public async Task UpdateMetrics(VoiceChatMetrics metrics)
    {
        var userId = Context.ConnectionId;
        var gameId = UserGameMapping.GetValueOrDefault(userId);
        
        if (!string.IsNullOrEmpty(gameId))
        {
            // Broadcast metrics to all participants in the game
            await Clients.Group(gameId).SendAsync("MetricsUpdate", new
            {
                ConnectionId = userId,
                Metrics = metrics,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.ConnectionId;
        
        if (UserGameMapping.TryRemove(userId, out var gameId))
        {
            await LeaveVoiceChat(gameId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    private List<VoiceChatParticipantInfo> GetParticipantsInGame(string gameId)
    {
        var participants = new List<VoiceChatParticipantInfo>();
        
        foreach (var kvp in Participants)
        {
            if (kvp.Value.GameId == gameId)
            {
                participants.Add(new VoiceChatParticipantInfo
                {
                    ConnectionId = kvp.Value.ConnectionId,
                    UserName = kvp.Value.UserName,
                    IsTalking = kvp.Value.IsTalking,
                    JoinedAt = kvp.Value.JoinedAt
                });
            }
        }
        
        return participants;
    }
}

public class VoiceChatParticipant
{
    public string ConnectionId { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool IsTalking { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? StartedTalkingAt { get; set; }
    public DateTime? StoppedTalkingAt { get; set; }
}

public class VoiceChatParticipantInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool IsTalking { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class VoiceChatMetrics
{
    public double AverageLatency { get; set; }
    public double PacketLossRate { get; set; }
    public double CpuUsage { get; set; }
    public int ActiveParticipants { get; set; }
    public DateTime Timestamp { get; set; }
}
