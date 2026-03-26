namespace Drafts.Data;

public class GameLog
{
    public int Id { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public string Message { get; set; } = string.Empty;
}
