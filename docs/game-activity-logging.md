# Game Activity Logging System

## Overview
Version 6.4.0 introduces a comprehensive game activity logging system that captures important game events and makes them viewable to administrators through the web interface.

## Features

### **Logged Events**
The system automatically captures the following game events:

#### **Authentication Events**
- Player logins: `"Player login: {username} (ID: {userId})"`

#### **Game Lifecycle Events**
- Game creation: `"Game created: {gameId} by {displayName} (ID: {userId})"`
- Player joins: `"Player joined: {username} (ID: {userId}) joined game {gameId} as Player{1|2}"`
- Game starts: `"Game started: {gameId} - {username} joined as Player{1|2}, game now has both players"`
- Game endings: `"Game ended: {gameId} - Winner: {winnerName} (Player{winner}), Loser: {loserName} (Player{loser})"`

#### **System Events**
- Idle timeouts: `"Game timeout: {gameId} - Maximum move timeout (idle/inactivity) - System closed game"`
- Game time timeouts: `"Game timeout: {gameId} - Maximum game time exceeded - System closed game"`
- Start wait timeouts: `"Game timeout: {gameId} - Maximum start wait time exceeded (no second player) - System closed game"`

### **Database Storage**
- **Table**: `GameLogs`
- **Schema**:
  - `Id` (INTEGER PRIMARY KEY)
  - `Timestamp` (TEXT) - UTC timestamp of the event
  - `Message` (TEXT) - Human-readable log message
- **Indexing**: Timestamp indexed for efficient querying
- **Retention**: Automatic cleanup of logs older than 30 days (configurable)

## Access Control

### **Who Can View Logs**
- **Admin users only** - Users with "Admin" role can access the log viewer
- **Authorization**: Protected by `[Authorize(Roles = "Admin")]` attribute
- **URL**: `/admin` - Main admin dashboard contains the log viewer

### **Access Levels**
| User Type | Can View Logs | Can Access Admin Pages |
|-----------|---------------|------------------------|
| Admin     | ✅ Yes        | ✅ Yes                 |
| Player    | ❌ No         | ❌ No                  |

## How to View Logs

### **Step 1: Login as Admin**
1. Navigate to the application
2. Login with Admin credentials:
   - Username: `Admin`
   - PIN: `9999`

### **Step 2: Access Log Viewer**
1. From the main menu, go to **Admin** page
2. Click the **"View game logs"** button
3. The log viewer will open in a modal overlay

### **Step 3: Configure Log Display**
The log viewer provides several options:
- **Show last**: Choose between 50, 100, 200, or 500 recent entries
- **Auto-refresh**: Logs automatically refresh every 30 seconds when open
- **Manual refresh**: Click "Refresh" button to update immediately
- **Close**: Click "Close" button or click outside modal to dismiss

### **Log Entry Format**
Each log entry displays:
```
[YYYY-MM-DD HH:MM:SS] Event message
```
Example:
```
[2026-03-26 11:15:32] Player login: Alice (ID: 5)
[2026-03-26 11:15:45] Game created: abc123 by Alice (ID: 5)
[2026-03-26 11:16:02] Player joined: Bob (ID: 2) joined game abc123 as Player2
[2026-03-26 11:16:02] Game started: abc123 - Bob joined as Player2, game now has both players
```

## Technical Implementation

### **Service Architecture**
- **GameLogService**: Handles database operations for logging
- **Dependency Injection**: Registered as singleton in `Program.cs`
- **Integration**: Integrated with `DraughtsService` and `AuthService`

### **Key Methods**
```csharp
// Log an event
await GameLogService.LogAsync("Event message");

// Get recent logs
var logs = await GameLogService.GetRecentLogsAsync(100);

// Get logs since specific time
var logs = await GameLogService.GetLogsSinceAsync(sinceDateTime);

// Clean up old logs
await GameLogService.ClearOldLogsAsync(30); // 30 days
```

### **Database Schema**
```sql
CREATE TABLE [GameLogs] (
    [Id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    [Timestamp] TEXT NOT NULL,
    [Message] TEXT NOT NULL
);

CREATE INDEX [IX_GameLogs_Timestamp] ON [GameLogs] ([Timestamp] ASC);
```

## Security Considerations

### **Access Protection**
- All admin pages require Admin role authorization
- Non-admin users are automatically redirected to login
- Even authenticated players cannot access admin functions
- Log viewer is only accessible through the admin dashboard

### **Data Privacy**
- Logs contain user IDs and usernames for accountability
- System events are logged with Admin ID (0) for system actions
- No sensitive data like PINs or passwords is logged
- Automatic cleanup prevents unlimited data retention

### **Audit Trail**
- All significant game events are captured
- Timestamps provide chronological audit capability
- User identification enables accountability
- System actions are clearly distinguished from user actions

## Maintenance

### **Automatic Cleanup**
- Old logs (older than 30 days) are automatically cleaned up
- Cleanup runs as part of the logging service
- Retention period is configurable in `GameLogService.ClearOldLogsAsync()`

### **Performance**
- Timestamp indexing ensures fast queries
- Pagination limits prevent memory issues
- Asynchronous logging doesn't impact game performance
- Efficient database operations with proper connection management

## Troubleshooting

### **Common Issues**
1. **Logs not appearing**: Check that the GameLogs table exists in the database
2. **Access denied**: Ensure user has Admin role in the Users table
3. **Missing events**: Verify GameLogService is properly injected and called

### **Database Verification**
```sql
-- Check if table exists
SELECT name FROM sqlite_master WHERE type='table' AND name='GameLogs';

-- Check recent logs
SELECT * FROM GameLogs ORDER BY Timestamp DESC LIMIT 10;
```

## Future Enhancements

### **Potential Improvements**
- Log filtering by event type
- Export logs to CSV/JSON
- Advanced search capabilities
- Log level categorization
- Real-time log streaming
- Integration with external logging systems

### **Monitoring**
- Consider adding metrics for log volume
- Monitor database size growth
- Alert on unusual activity patterns

---

**Version**: 6.4.0  
**Implemented**: 2026-03-26  
**Access Level**: Admin only  
**Database**: SQLite (GameLogs table)
