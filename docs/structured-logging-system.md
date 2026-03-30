# Structured Logging System

## Overview
Version 6.5.0 introduces a comprehensive structured logging system with GroupId support that replaces the previous simple message-based logging. This new system provides powerful search capabilities, better data organization, and detailed tracking of game events, user actions, and system events with group-aware context.

## **🎯 Key Features**

### **Structured Data Storage**
- **LogType Enumeration**: Categorized log entries for better filtering
- **Player ID Tracking**: Mandatory field linking events to specific users
- **Game ID Association**: Optional field for game-related events
- **GroupId Support**: Optional field for group-based event organization
- **Opponent Tracking**: For wins/losses, stores both winner and loser IDs
- **Detailed Context**: Rich field structure instead of simple text messages

### **Advanced Search Capabilities**
- **Type-based Filtering**: Filter by specific log categories
- **Player-based Search**: Find all events for a specific player
- **Game-based Search**: View complete game history
- **Group-based Search**: Filter events by specific groups
- **Date Range Filtering**: Search within specific time periods
- **Combined Filters**: Multiple search criteria simultaneously

### **Enhanced Admin Interface**
- **Color-coded Log Types**: Visual distinction between event types
- **Structured Table Display**: Organized columns for better readability
- **Real-time Search**: Instant filtering as you type
- **User-friendly Names**: Shows player names instead of just IDs

## **📊 LogType Enumeration**

### **Player Actions**
```csharp
PlayerLogin = 1,      // User successfully logs in
PlayerLogout = 2,     // User logs out (session expires)
PlayerPinChange = 3   // User changes their PIN
```

### **Game Lifecycle**
```csharp
GameCreated = 10,     // New game created
GameStarted = 11,     // Game begins (both players joined)
GameEnded = 12,       // Game finishes (win/loss/draw)
GameJoined = 13        // Player joins an existing game
```

### **Game Events**
```csharp
MoveMade = 20,        // Player makes a move
TurnTimeout = 21,      // Player exceeds move time limit
GameTimeout = 22       // Game exceeds total time limit
```

### **System Events**
```csharp
SystemStartup = 30,    // Application starts
SystemShutdown = 31,  // Application shuts down
Error = 32,           // Error events
Warning = 33          // Warning events
```

### **Admin Actions**
```csharp
AdminAction = 40,      // General admin actions
UserManagement = 41,   // User creation/modification
SettingsChanged = 42   // Configuration changes
```

## **🗄️ Database Schema**

### **New GameLog Structure**
```sql
CREATE TABLE GameLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LogType INTEGER NOT NULL,           -- LogType enum value
    PlayerId INTEGER NOT NULL,          -- Who triggered the event
    GameId INTEGER NULL,                -- Related game (if applicable)
    GroupId INTEGER NULL,               -- Group association (if applicable)
    OpponentPlayerId INTEGER NULL,      -- For wins/losses
    Details TEXT NULL                   -- Additional context
);
```

### **Migration Notes**
- **Backward Compatible**: Old `Message` field still works
- **Automatic Migration**: New fields added to existing databases
- **Data Preservation**: Existing logs remain accessible
- **Default Values**: Sensible defaults for new fields

## **🔍 Search Examples**

### **Find All Player Activity**
```csharp
// Get all events for player ID 5
var logs = await GameLog.GetLogsByPlayerAsync(5);
```

### **Track Game History**
```csharp
// Get complete timeline for game 12345
var gameHistory = await GameLog.GetLogsByGameAsync(12345);
```

### **Filter by Event Type**
```csharp
// Get all game endings
var endings = await GameLog.GetLogsByTypeAsync(LogType.GameEnded);
```

### **Advanced Search**
```csharp
// Find all timeouts for player 3 in the last week
var timeouts = await GameLog.SearchLogsAsync(
    playerId: 3,
    logType: LogType.GameTimeout,
    startDate: DateTime.UtcNow.AddDays(-7),
    count: 50
);
```

## **🎨 Admin Interface Features**

### **Visual Log Types**
Each log type has a distinct color:
- 🟢 **Green**: Positive events (logins, game starts)
- 🔴 **Red**: Negative events (errors, timeouts, game ends)
- 🔵 **Blue**: Informational events (game creation, joins)
- 🟡 **Orange**: Warnings and timeouts
- 🟣 **Purple**: Admin actions

### **Search Interface**
- **Type Dropdown**: Select specific log categories
- **Player ID Field**: Search by player number
- **Game ID Field**: Search by game identifier
- **Clear Button**: Reset all filters instantly
- **Refresh Button**: Reload latest logs

### **Table Display**
```
| Timestamp           | Type       | Player      | Game | Details                     |
|---------------------|------------|-------------|------|-----------------------------|
| 2026-03-27 12:00:00 | GameStarted | Alice vs Bob| 1234 | Game started - Alice joined |
| 2026-03-27 12:05:00 | GameEnded   | Alice vs Bob| 1234 | Winner: Alice, Loser: Bob   |
| 2026-03-27 12:10:00 | PlayerLogin | Carol      | -    | Player Carol logged in       |
```

## **📝 Code Implementation**

### **Structured Logging Methods**
```csharp
// Player actions
await GameLog.LogPlayerActionAsync(LogType.PlayerLogin, userId, "Player logged in", null, null);

// Game events with GroupId
await GameLog.LogGameEventAsync(LogType.GameCreated, userId, gameId, "Game created", null, groupId);

// Wins with opponent tracking and GroupId
await GameLog.LogWinAsync(winnerId, loserId, gameId, "Winner: Alice, Loser: Bob", groupId);

// System events (no GroupId)
await GameLog.LogSystemEventAsync(LogType.Error, adminId, "System error occurred");
```

### **Enhanced Search Methods**
```csharp
// Search with GroupId filtering
var logs = await GameLog.SearchLogsAsync(
    playerId: 5,
    gameId: 12345,
    groupId: 789,        // NEW: Group-based filtering
    logType: LogType.GameEnded,
    count: 100
);

// Group-specific analysis
var groupLogs = await GameLog.SearchLogsAsync(groupId: targetGroupId);
```

### **Backward Compatibility**
```csharp
// Old method still works (treated as system event)
await GameLog.LogAsync("Simple message logging");

// New structured approach preferred
await GameLog.LogPlayerActionAsync(LogType.PlayerLogin, userId, "Player logged in");
```

## **🔧 Integration Points**

### **AuthService.cs**
```csharp
// Login events (no GroupId for user actions)
_ = _gameLog.LogPlayerActionAsync(LogType.PlayerLogin, user.Id, $"Player {user.Name} logged in", null, null);
```

### **DraughtsService.cs**
```csharp
// Game creation with GroupId
_ = _gameLog.LogGameEventAsync(LogType.GameCreated, userId, gameId, "Game created", null, groupId);

// Game endings with winner/loser tracking and GroupId
_ = _gameLog.LogWinAsync(winnerUserId, loserUserId, gameId, details, game.GroupId);

// Player joining with GroupId
_ = _gameLog.LogGameEventAsync(LogType.GameJoined, userId, gameId, "Player joined", null, game.GroupId);
```

## **🎯 GroupId Features (V6.5.0)**

### **Group-Aware Logging**
- **Context Association**: Links events to specific groups
- **Enhanced Search**: Filter logs by GroupId
- **Performance Analytics**: Group-based activity tracking
- **User Experience**: Group names displayed in interfaces

### **GroupId Use Cases**
```csharp
// Group activity monitoring
var groupActivity = await GameLog.SearchLogsAsync(groupId: targetGroupId);

// Multi-group comparison
var allGroups = await GameLog.SearchLogsAsync();
var groupedByGroup = allLogs.GroupBy(x => x.GroupId);

// Group-specific troubleshooting
var groupTimeouts = await GameLog.SearchLogsAsync(
    groupId: problemGroupId,
    logType: LogType.GameTimeout
);
```

### **Admin Interface Enhancements**
- **Group Column**: Shows group names instead of IDs
- **Group Filtering**: Search logs by specific groups
- **Visual Consistency**: Matches group naming throughout app
- **Fallback Handling**: Graceful display for missing groups

// System timeouts
_ = _gameLog.LogSystemEventAsync(LogType.GameTimeout, 1, details, gameId);
```

### **Admin.razor**
```csharp
// Enhanced search interface
_logs = await GameLog.SearchLogsAsync(
    playerId: _searchPlayerId,
    gameId: gameId,
    logType: logType,
    count: _logCount
);
```

## **📈 Performance Benefits**

### **Database Efficiency**
- **Indexed Fields**: PlayerId, GameId, LogType, Timestamp
- **Targeted Queries**: Only retrieve needed data
- **Reduced Payload**: No more parsing text messages
- **Scalable Design**: Handles high-volume logging

### **Search Performance**
- **Fast Filtering**: Database-level filtering
- **Lazy Loading**: Load only requested amounts
- **Cached Results**: User names cached for display
- **Optimized Queries**: Efficient SQL generation

## **🔍 Use Cases**

### **Debugging Game Issues**
1. **Player Reports Problem**: Search by player ID
2. **Game Investigation**: Filter by specific game
3. **System Errors**: Filter by error/warning types
4. **Performance Analysis**: Look for timeout patterns

### **User Behavior Analysis**
1. **Login Patterns**: Track player activity over time
2. **Game Preferences**: Most popular game types
3. **Session Duration**: How long players stay active
4. **Peak Usage Times**: Busiest periods

### **Security Monitoring**
1. **Failed Logins**: Monitor authentication attempts
2. **Admin Actions**: Track configuration changes
3. **Suspicious Activity**: Unusual player behavior
4. **System Health**: Error and warning patterns

## **🚀 Future Enhancements**

### **Potential Improvements**
- **Real-time Notifications**: Live log streaming
- **Export Capabilities**: Download log data as CSV/JSON
- **Advanced Analytics**: Charts and statistics
- **Log Retention Policies**: Automatic cleanup rules
- **Integration with Monitoring**: External logging services

### **Scalability Considerations**
- **Database Partitioning**: Separate tables by time period
- **Archive Strategy**: Move old logs to cold storage
- **Index Optimization**: Additional performance indexes
- **Caching Layer**: Redis for frequent queries

## **📚 Migration Guide**

### **For Existing Applications**
1. **Deploy New Version**: Automatic database migration
2. **Update Code**: Replace old logging calls gradually
3. **Test Functionality**: Verify search features work
4. **Train Admins**: Show new interface features

### **For New Development**
1. **Use Structured Methods**: Prefer new logging API
2. **Plan Search Needs**: Design with filtering in mind
3. **Consider Log Types**: Choose appropriate LogType values
4. **Document Events**: Maintain clear logging standards

## **🎯 Best Practices**

### **Logging Guidelines**
- **Be Specific**: Use detailed messages for context
- **Use Correct Types**: Choose appropriate LogType values
- **Include Context**: Add relevant IDs and details
- **Log Consistently**: Similar events should use similar patterns

### **Search Optimization**
- **Use Filters**: Narrow results before loading
- **Limit Results**: Don't load more data than needed
- **Cache User Names**: Avoid repeated database lookups
- **Index Appropriately**: Ensure search fields are indexed

### **Performance Considerations**
- **Async Logging**: Don't block main application flow
- **Batch Operations**: Group related log entries
- **Error Handling**: Ensure logging failures don't crash app
- **Monitor Performance**: Track logging impact on system

---

**Version**: 6.5.0  
**Implemented**: 2026-03-27  
**Type**: Major Enhancement  
**Backward Compatible**: ✅ Yes  
**Search Enabled**: ✅ Yes  
**Admin Interface**: ✅ Enhanced  
**GroupId Support**: ✅ Yes  
**Group Names**: ✅ Displayed
