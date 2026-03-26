# Game Timeout System

## Overview

The Drafts game implements a comprehensive timeout system to manage game lifecycle and prevent stale games from consuming resources. The system uses a background reaper service that periodically checks and enforces four different timeout types.

**Version:** V6.2.0  
**Implementation Date:** March 2026

## Timeout Types

### 1. Max Move Timeout (Idle Timeout)
**Setting:** `MaxMoveTimeoutMins`  
**Default:** 5 minutes  
**Purpose:** Prevents games from remaining active when players stop making moves.

**Behavior:**
- Monitors time since last player action (`LastTimeUtc`)
- Only applies to games that have started (second player connected)
- Warning sent at 80% of timeout threshold
- Grace period of 1 second after final warning before removal

**Messages:**
- Warning: `"Warning: this game will time out after X minutes of inactivity."`
- Closure: `"Game closed: Maximum move timeout - too much inactivity."`

### 2. Max Game Time Timeout
**Setting:** `MaxGameTimeMins`  
**Default:** 30 minutes  
**Purpose:** Limits total game duration to prevent excessively long games.

**Behavior:**
- Monitors total game time from `StartTimeUtc`
- Only applies to games that have started (second player connected)
- Warning sent at 80% of timeout threshold
- Immediate removal when threshold exceeded

**Messages:**
- Warning: `"Warning: this game will close after X minutes of total game time."`
- Closure: `"Game closed: Maximum game time (X minutes) exceeded."`

### 3. Max Game Start Wait Timeout
**Setting:** `MaxGameStartWaitTimeMins`  
**Default:** 30 minutes  
**Purpose:** Removes games waiting for a second player to join.

**Behavior:**
- Monitors time since game creation (`CreatedUtc`)
- Only applies to games waiting for second player
- Warning sent at 80% of timeout threshold with countdown timer
- Immediate removal when threshold exceeded

**Messages:**
- Warning: `"Warning: waiting for second player. Game will close in M:SS."`
- Closure: `"Game closed: Maximum start wait time (X minutes) exceeded - no second player joined."`

### 4. Max Login Session Timeout
**Setting:** `MaxLoginHrs`  
**Default:** 4 hours  
**Purpose:** Expires user login sessions for security.

**Behavior:**
- Managed by authentication system
- Forces re-login after specified hours
- Separate from game timeout mechanisms

## Architecture

### Components

#### 1. GameTimeoutReaper (Background Service)
**File:** `Services/GameTimeoutReaper.cs`

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        var period = await _settings.GetReaperPeriodSecondsAsync();
        var maxMoveTimeout = await _settings.GetMaxMoveTimeoutMinsAsync();
        var maxGameTime = await _settings.GetMaxGameTimeMinsAsync();
        var maxStartWait = await _settings.GetMaxGameStartWaitTimeMinsAsync();
        
        // Process all timeout types
        _drafts.ProcessIdleTimeouts(TimeSpan.FromMinutes(maxMoveTimeout), TimeSpan.FromSeconds(1));
        _drafts.ProcessGameTimeTimeouts(TimeSpan.FromMinutes(maxGameTime));
        _drafts.ProcessGameStartWaitTimeouts(TimeSpan.FromMinutes(maxStartWait));
        
        await Task.Delay(TimeSpan.FromSeconds(period), stoppingToken);
    }
}
```

**Configuration:**
- `ReaperPeriodSeconds`: How often the reaper runs (default: 30 seconds)
- Runs continuously as a hosted background service
- Gracefully handles cancellation on shutdown

#### 2. DraftsService (Timeout Processing)
**File:** `Services/DraftsService.cs`

**Key Methods:**
- `ProcessIdleTimeouts()`: Handles move timeout with grace period
- `ProcessGameTimeTimeouts()`: Handles total game time limits
- `ProcessGameStartWaitTimeouts()`: Handles connection wait limits

**Thread Safety:**
- All game state modifications use locks
- Concurrent dictionary for game storage
- Atomic flag checks prevent duplicate warnings

#### 3. DraftsGame (State Tracking)
**File:** `Services/DraftsService.cs` (nested class)

**Timeout-Related Properties:**
```csharp
public DateTime CreatedUtc { get; }
public DateTime StartTimeUtc { get; private set; }
public DateTime LastTimeUtc { get; private set; }
public bool IdleWarningSent { get; set; }
public bool IdleKillMessageSent { get; set; }
public bool GameTimeWarningSent { get; set; }
public bool StartWaitWarningSent { get; set; }
public DateTime? KillAfterUtc { get; set; }
public bool HadSecondPlayerConnected { get; set; }
```

#### 4. UI Components

**Home Page Display:**
`Components/Pages/Home.razor` displays all timeout settings:
```razor
<div>
    <strong>Move timeout:</strong> @_maxMoveTimeoutMins minutes
    <strong>Max game time:</strong> @_maxGameTimeMins minutes
    <strong>Max game start wait:</strong> @_maxGameStartWaitTimeMins minutes
    <strong>Max login session:</strong> @_maxLoginHrs hours
</div>
```

**Game Closure Dialog:**
`Components/DraftsGame.razor` captures and displays specific timeout reasons in popup alert.

## Message Flow

### 1. Warning Phase (80% Threshold)
```
Reaper runs → Check timeout → 80% exceeded → Add warning to chat → Set warning flag → OnGameUpdated → UI displays warning
```

### 2. Closure Phase (100% Threshold)
```
Reaper runs → Check timeout → 100% exceeded → Add closure message to chat → OnGameUpdated (game still exists) → UI captures message → Remove game → OnGameUpdated (game null) → UI shows popup with captured message
```

**Critical Timing:**
- `OnGameUpdated` is called **before** game removal so UI can capture the closure message
- Second `OnGameUpdated` call after removal triggers the popup dialog
- This ensures specific timeout reason appears in both chat and popup

## Configuration

### Database Settings
**Table:** `Settings`  
**File:** `Data/AppSettings.cs`

```csharp
public class AppSettings
{
    public int MaxMoveTimeoutMins { get; set; } = 5;
    public int MaxGameTimeMins { get; set; } = 30;
    public int MaxGameStartWaitTimeMins { get; set; } = 30;
    public int MaxLoginHrs { get; set; } = 4;
    public int ReaperPeriodSeconds { get; set; } = 30;
}
```

### Admin Configuration
**Page:** `Components/Pages/Admin.razor`

Administrators can modify all timeout values through the Admin interface:
- Changes saved to database
- SettingsService caches values in memory
- Reaper picks up new values on next cycle

### Default Seeding
**File:** `Data/DbSeeder.cs`

Default values are seeded when database is created:
```csharp
db.Settings.Add(new AppSettings
{
    MaxMoveTimeoutMins = 5,
    MaxGameTimeMins = 30,
    MaxGameStartWaitTimeMins = 30,
    MaxLoginHrs = 4,
    ReaperPeriodSeconds = 30
});
```

## User Experience

### Warning Messages
All timeout warnings appear in the Game Chat:
- **Move timeout:** Shows total timeout duration
- **Game time:** Shows total game time limit
- **Start wait:** Shows remaining time as countdown (MM:SS)

### Closure Messages
When a game is closed by timeout:
1. **Game Chat** displays specific reason
2. **Popup Dialog** shows the same reason
3. **Navigation** redirects to player/admin page

### Example User Flow

**Scenario: Waiting for Second Player**

1. Player creates game (t=0:00)
2. At t=24:00 (80% of 30 min): Warning appears in chat
   - "Warning: waiting for second player. Game will close in 6:00."
3. At t=30:00: Game closes
   - Chat: "Game closed: Maximum start wait time (30 minutes) exceeded - no second player joined."
   - Popup: Same message
   - Redirect to player page

## Testing

### Verified Scenarios
✅ **Max move timeout** - Tested with idle games  
✅ **Max game time** - Tested with long-running games  
✅ **Max start wait** - Tested with single-player games  
⏳ **Max login session** - Deferred (requires 4+ hour wait)

### Test Configuration
For faster testing, temporarily reduce timeout values in Admin settings:
- MaxMoveTimeoutMins: 1-2 minutes
- MaxGameTimeMins: 3-5 minutes
- MaxGameStartWaitTimeMins: 2-3 minutes
- ReaperPeriodSeconds: 10-30 seconds

## Troubleshooting

### Issue: Generic "Game was closed" message
**Cause:** UI receiving game removal notification before capturing closure message  
**Solution:** Ensure `OnGameUpdated` is called before `_games.TryRemove()`

### Issue: Warning messages not appearing
**Cause:** Warning flag already set from previous cycle  
**Solution:** Flags are reset on game activity via `Touch()` method

### Issue: Timeouts not triggering
**Cause:** Reaper not running or incorrect time calculations  
**Solution:** Check background service registration in `Program.cs` and verify datetime comparisons

## Performance Considerations

- Reaper iterates all active games every cycle
- Lock contention minimized by quick flag checks
- Chat message list grows with warnings (cleared on game removal)
- Concurrent dictionary provides thread-safe game access
- No database queries during timeout checks (uses in-memory state)

## Future Enhancements

Potential improvements:
- Configurable warning threshold percentage (currently 80%)
- Multiple warning levels (e.g., 50%, 80%, 95%)
- Per-game timeout overrides
- Timeout statistics and analytics
- Email/notification on timeout events
- Timeout exemptions for specific users/groups

## Related Documentation

- [Game Reaper System](game-reaper-system.md)
- [Authentication System](auth.md)
- [Database Schema](entity-relationships.md)

## Version History

- **V6.2.0** (March 2026): Complete timeout system implementation
  - Added MaxGameTimeMins and MaxGameStartWaitTimeMins
  - Implemented warning messages at 80% threshold
  - Fixed message capture timing for popup dialogs
  - Added countdown timer for start wait warnings
  - Displayed all timeout settings on Home page
