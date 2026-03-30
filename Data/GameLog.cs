namespace Draughts.Data;

public enum LogType
{
    // Player actions
    PlayerLogin = 1,
    PlayerLogout = 2,
    PlayerPinChange = 3,
    
    // Game lifecycle
    GameCreated = 10,
    GameStarted = 11,
    GameEnded = 12,
    GameJoined = 13,
    GameLeft = 14,
    
    // Game events
    MoveMade = 20,
    TurnTimeout = 21,
    GameTimeout = 22,
    
    // System events
    SystemStartup = 30,
    SystemShutdown = 31,
    Error = 32,
    Warning = 33,
    
    // Admin actions
    AdminAction = 40,
    UserManagement = 41,
    SettingsChanged = 42
}

public class GameLog
{
    public int Id { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public LogType LogType { get; set; }
    
    public int PlayerId { get; set; }  // Mandatory - player who triggered the action, or Admin ID for system events
    
    public int? GameId { get; set; }   // Optional - only for game-related events
    
    public int? GroupId { get; set; }  // Optional - group associated with the event
    
    public int? OpponentPlayerId { get; set; }  // Optional - for wins (opponent who lost)
    
    public string? Details { get; set; }  // Optional - additional context or old message content
    
    // Computed property for backward compatibility
    public string Message 
    { 
        get 
        {
            var parts = new List<string>();
            
            // Add log type description
            parts.Add($"[{LogType}]");
            
            // Add player name if available
            if (PlayerId > 0)
            {
                parts.Add($"Player {PlayerId}");
            }
            
            // Add game info if available
            if (GameId.HasValue)
            {
                parts.Add($"Game {GameId.Value}");
            }
            
            // Add group info if available
            if (GroupId.HasValue)
            {
                parts.Add($"Group {GroupId.Value}");
            }
            
            // Add opponent info if available
            if (OpponentPlayerId.HasValue)
            {
                parts.Add($"vs Player {OpponentPlayerId.Value}");
            }
            
            // Add details if available
            if (!string.IsNullOrWhiteSpace(Details))
            {
                parts.Add(Details);
            }
            
            return string.Join(" ", parts);
        }
        set => Details = value;  // For backward compatibility
    }
}

public class PlayerWinsLosses
{
    public int? GroupId { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    
    public int TotalGames => Wins + Losses;
    public double WinRate => TotalGames > 0 ? (double)Wins / TotalGames * 100 : 0;
}
