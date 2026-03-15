# Group Chat Enhancements

## Overview
Implementation of group-specific lobby chat filtering, personal chat deletion, group-only access control, and admin broadcast functionality, enhancing the chat experience with better privacy, user control, exclusive group communication, and administrative messaging capabilities.

## The Specification:
With the apps Groups can we now: 
- Direct lobby chat to only Groups tha the palyer is memeber of 
- And that when they delete chat is applies only to themselves.
> There was a misinterpretation. AI thought that a player not in any group could send public messages.
- A player not a member of any group is able to send but should not get to anyone
  - Not meant to be public send.  etc
  - It was decided to not allow non-group users to send messages at all.  as it would create confusion and potential privacy issues. 
  - Only users who are members of at least one group can send messages, and those messages will only be visible to members of the relevant groups (or public if no group specified).
- Also Lobby Chat Textareas to be cleared when logging in
- Additional - Allow Admin to broadcast to Lobby Chat to all players logged in

## Phase 1: Enhanced LobbyChatService

### 1.1 Group-Only Access Control
**File:** `Services/LobbyChatService.cs`
- **Enhanced `ChatMessage` record** with new fields:
  - `GroupId` - Optional group ID for message targeting
  - `MessageIndex` - Unique index for personal deletion tracking
- **Updated `GetMessages()` method** with group access enforcement:
  - `userId` - Filter messages for specific user (handles personal deletion)
  - `userGroupIds` - Filter messages by group membership
  - **Group requirement** - Users without groups receive empty message list
- **Group-only logic** - Only users with groups can access chat

### 1.2 Personal Deletion System
- **Added `_userDeletedMessages` dictionary** - Tracks deleted messages per user
- **New methods:**
  - `DeleteMessageForUser(userId, messageIndex)` - Delete specific message for user
  - `ClearChatForUser(userId)` - Clear all messages for user
- **Updated `TrimIfNeeded()`** - Maintains message indexes after trimming
- **Automatic cleanup** - Removes invalid message indexes after trimming

### 1.3 Enhanced Message Operations
- **Updated `AddMessage()`** - Accepts optional `groupId` parameter
- **Updated `AddSystemMessage()`** - Accepts optional `groupId` parameter
- **Added `AddMessageWithGroupCheck()`** - Validates group membership before sending
- **Added `AddAdminBroadcast()`** - Admin broadcast to all logged-in players
- **Added `AddAdminBroadcastWithGroupCheck()`** - Secure admin broadcast with validation
- **Message indexing** - Each message gets unique index for deletion tracking
- **Thread-safe operations** - All operations maintain thread safety
- **Group validation** - Prevents non-group users from sending messages
- **Admin authorization** - Only admins can broadcast messages

## Phase 2: LobbyChat Component Updates

### 2.1 Group Integration
**File:** `Components/LobbyChat.razor`
- **Added `AuthService` injection** - For user group loading
- **Added user data fields:**
  - `_userGroups` - User's group memberships
  - `_currentUserId` - Current authenticated user ID
  - `_isAdmin` - Admin role status
  - `_broadcastMode` - Admin broadcast toggle state
- **Updated `OnInitializedAsync()`** - Loads user groups, ID, and admin status

### 2.2 Admin Broadcast UI
- **Broadcast toggle button** - Only visible to admin users
- **Mode indicator** - Shows current broadcast/normal mode
- **Dynamic placeholders** - Context-aware input placeholders
- **Admin mode styling** - Visual distinction for broadcast mode
- **Chat access for admins** - Admins can chat even without group membership

### 2.3 Group-Only UI Controls
- **Conditional chat interface** - Shows chat for users with groups OR admins
- **Access denied message** - Clear UI for users without group access
- **Helpful guidance** - Directs users to contact admin for group membership
- **Visual distinction** - Styled access requirement message

### 2.4 Enhanced Chat Filtering
- **Updated `RefreshChat()` method** - Applies group and personal deletion filters
- **Group membership filtering** - Only shows messages from user's groups + public
- **Personal deletion filtering** - Hides messages deleted by current user
- **Real-time updates** - Chat updates respect user's filters

### 2.5 Personal Chat Controls
- **Updated "Clear" button** - Now "Clear My Chat" (personal only)
- **Confirmation dialog** - Confirms personal chat deletion
- **Updated `ClearMyChat()` method** - Uses `ClearChatForUser()`
- **User-friendly messaging** - Clear indication of personal-only action

### 2.6 Secure Message Sending
- **Updated `SendChat()` method** - Handles both normal and broadcast modes
- **Group membership validation** - Prevents non-group users from sending
- **Admin broadcast routing** - Uses `AddAdminBroadcastWithGroupCheck()` for broadcasts
- **Error handling** - Clear alerts for permission issues
- **Access requirement messaging** - Explains group membership requirement

### 2.7 Admin Broadcast Functionality
- **Broadcast mode toggle** - `ToggleBroadcastMode()` method
- **Admin authorization check** - Validates admin role before broadcasting
- **Message formatting** - Admin messages prefixed with "[ADMIN]"
- **Mode switching** - Clears input when toggling between modes
- **Universal visibility** - Broadcasts visible to all users with group access

### 2.8 Login Chat Clearing
- **Automatic clearing** - Chat textareas cleared on user data load
- **Login refresh** - Clean slate on every login/session
- **Privacy protection** - No residual chat text between sessions
- **Consistent experience** - Predictable clean state

## Phase 3: System Message Integration

### 3.1 Game Creation Messages
**File:** `Services/DraftsService.cs`
- **Updated `CreateGame()` method** - Passes `groupId` to system messages
- **Group-specific announcements** - Game creation messages sent to relevant groups
- **Public game messages** - Games without groups go to public chat

### 3.2 Message Targeting
- **Group games** - System messages only visible to group members
- **Public games** - System messages visible to all users
- **Seamless integration** - No changes to game creation flow

## Technical Implementation Details

### 1. Group-Only Access Control
```csharp
// Only show messages if user is in at least one group
if (userGroupIds != null && !userGroupIds.Any())
{
    // User not in any groups - no chat access
    return new List<ChatMessage>();
}

// Group filtering
if (userGroupIds != null)
{
    var groupIds = userGroupIds.ToList();
    messages = messages.Where(m => !m.GroupId.HasValue || groupIds.Contains(m.GroupId.Value)).ToList();
}
```

### 2. Secure Message Sending
```csharp
public bool AddMessageWithGroupCheck(int senderUserId, string senderName, string text, IEnumerable<int> userGroupIds, int? groupId = null)
{
    // Check if user is in any groups
    if (userGroupIds == null || !userGroupIds.Any())
    {
        return false; // User not in any groups - cannot send messages
    }

    // If groupId specified, check if user is member of that group
    if (groupId.HasValue && !userGroupIds.Contains(groupId.Value))
    {
        return false; // User not in specified group
    }
    
    // ... proceed with message creation
}
```

### 3. Admin Broadcast Implementation
```csharp
public bool AddAdminBroadcastWithGroupCheck(int senderUserId, string senderName, string text, IEnumerable<int> userGroupIds, bool isAdmin)
{
    // Only admins can broadcast
    if (!isAdmin) return false;

    text = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
    if (string.IsNullOrWhiteSpace(text)) return false;

    lock (_lock)
    {
        var messageIndex = _messages.Count;
        // Admin broadcasts use groupId = null to make them visible to all users with group access
        _messages.Add(new ChatMessage(DateTime.UtcNow, senderUserId, $"[ADMIN] {senderName ?? string.Empty}", text, null, messageIndex));
        TrimIfNeeded();
    }

    ChatUpdated?.Invoke();
    return true;
}
```

### 3. Personal Deletion Filtering
```csharp
// Personal deletion filtering
if (userId.HasValue)
{
    if (_userDeletedMessages.TryGetValue(userId.Value, out var deletedIndexes))
    {
        messages = messages.Where(m => !deletedIndexes.Contains(m.MessageIndex)).ToList();
    }
}
```

### 4. Login Chat Clearing
```csharp
private async Task LoadUserData()
{
    // Clear chat display when loading user data (login/refresh)
    _chatInText = "";
    _chatOutText = "";
    _lastChatCount = 0;
    _scrollPending = false;
    
    // Get current user ID and admin status
    var user = HttpContextAccessor.HttpContext?.User;
    var rawUid = user?.FindFirst("uid")?.Value;
    _isAdmin = user?.IsInRole("Admin") ?? false;
    
    // ... load user groups and ID
}
```

### 5. Admin Broadcast UI Logic
```csharp
private void ToggleBroadcastMode()
{
    _broadcastMode = !_broadcastMode;
    _chatOutText = ""; // Clear input when switching modes
    _ = InvokeAsync(StateHasChanged);
}

// Handle admin broadcast in SendChat()
if (_broadcastMode && _isAdmin)
{
    var broadcastOk = LobbyChatSvc.AddAdminBroadcastWithGroupCheck(senderId, senderName, text, userGroupIds, _isAdmin);
    // ... handle broadcast result
}
```

### 6. Message Index Management
```csharp
// Update indexes after trimming
for (int i = 0; i < _messages.Count; i++)
{
    var message = _messages[i];
    _messages[i] = message with { MessageIndex = i };
}

// Clean up invalid deletion references
foreach (var deletedSet in _userDeletedMessages.Values)
{
    deletedSet.RemoveWhere(index => index >= _messages.Count);
}
```

## Files Modified

### Core Services:
- `Services/LobbyChatService.cs` - Enhanced with group filtering and personal deletion
- `Services/DraftsService.cs` - Group-aware system messages

### UI Components:
- `Components/LobbyChat.razor` - Group integration and personal controls

### Project Configuration:
- `Drafts.csproj` - Removed markdown compilation (build fix)

## Database Schema

No database changes required. All enhancements use in-memory storage:
- Message filtering based on existing group relationships
- Personal deletion tracking in memory
- No persistence needed for chat messages

## API Changes

### LobbyChatService Methods:
- `GetMessages(userId?, userGroupIds?)` - Enhanced filtering with group-only access
- `AddMessage(senderUserId, senderName, text, groupId?)` - Group support
- `AddMessageWithGroupCheck(senderUserId, senderName, text, userGroupIds, groupId?)` - Group validation
- `AddAdminBroadcast(senderUserId, senderName, text)` - Admin broadcast to all
- `AddAdminBroadcastWithGroupCheck(senderUserId, senderName, text, userGroupIds, isAdmin)` - Secure admin broadcast
- `AddSystemMessage(text, groupId?)` - Group support
- `DeleteMessageForUser(userId, messageIndex)` - Personal deletion
- `ClearChatForUser(userId)` - Personal clear

## User Experience

### Before:
- All users see all lobby messages
- Clear chat affects everyone
- No group-based privacy
- No access restrictions

### After:
- Users only see messages from their groups + public messages
- Clear chat only affects individual user
- Better privacy and control
- Group-only access - users without groups cannot participate
- Admin broadcast capability - admins can message all logged-in players
- Clean chat interface on every login
- Visual feedback for access requirements
- Admin-only broadcast mode with visual indicators

## Usage Examples

### Group-Filtered Chat:
```csharp
// User in Group A and B sees:
// - Public messages (no group)
// - Messages from Group A
// - Messages from Group B
// - NOT messages from Group C (not a member)
```

### Personal Deletion:
```csharp
// User deletes message for themselves only
LobbyChatSvc.DeleteMessageForUser(userId, messageIndex);

// Other users still see the message
```

### Admin Broadcast:
```csharp
// Admin broadcasts to all logged-in players
LobbyChatSvc.AddAdminBroadcastWithGroupCheck(adminId, "AdminName", "Server maintenance in 5 minutes", userGroups, isAdmin: true);

// All users with group access see: [ADMIN] AdminName: Server maintenance in 5 minutes
```

## Performance Considerations

### Filtering Efficiency:
- **Group filtering** - O(n) where n = message count
- **Personal deletion** - O(1) lookup per message
- **Memory usage** - Minimal overhead for deletion tracking

### Scalability:
- **Message limit** - 200 messages (existing)
- **User tracking** - Only tracks deletions for active users
- **Group loading** - Cached per component instance

## Security Features

### Privacy:
- **Group isolation** - Messages only visible to group members
- **Personal deletion** - Users control their own chat view
- **No cross-group leakage** - Strict filtering enforcement

### Data Protection:
- **In-memory storage** - No persistent chat data
- **User authentication** - All operations require authenticated user
- **Group verification** - Uses existing group membership system

## Testing Checklist

- [ ] Users only see messages from their groups + public
- [ ] Personal deletion works correctly
- [ ] Clear chat only affects individual user
- [ ] Group system messages work
- [ ] Public system messages work
- [ ] Message indexing handles trimming correctly
- [ ] Multiple users have independent chat views
- [ ] Group membership changes affect chat visibility
- [ ] Performance acceptable with many users
- [ ] Error handling for invalid operations
- [ ] Users without groups cannot see any chat messages
- [ ] Users without groups cannot send chat messages
- [ ] Access denied UI displays correctly for non-group users
- [ ] Alert messages show for permission violations
- [ ] Chat textareas clear on login/session refresh
- [ ] No residual chat text between user sessions
- [ ] Admin broadcast toggle works correctly
- [ ] Admin messages show [ADMIN] prefix
- [ ] Broadcast mode indicator displays properly
- [ ] Admin can chat even without group membership
- [ ] Non-admin users cannot see broadcast toggle
- [ ] Broadcast messages visible to all users with group access

## Future Enhancements

### Planned Features:
- **Group selection UI** - Allow users to choose target group for messages
- **Message history** - Persistent chat storage with deletion tracking
- **Chat moderation** - Admin controls for group chat management
- **Message search** - Search within filtered chat history
- **Typing indicators** - Show who's typing in group chat
- **File sharing** - Share files within group chats

### Technical Improvements:
- **SignalR integration** - Real-time group chat updates
- **Message encryption** - End-to-end encryption for sensitive groups
- **Offline support** - Queue messages when offline
- **Analytics** - Chat usage statistics and insights

## Troubleshooting

### Common Issues:
1. **Messages not appearing** - Check group membership and user authentication
2. **Personal deletion not working** - Verify user ID is correctly loaded
3. **Group filtering not working** - Ensure user groups are loaded correctly
4. **Performance issues** - Check message count and user activity

### Debug Tools:
- **Console logging** - Add debug output for filtering operations
- **User inspection** - Verify user groups and ID loading
- **Message tracking** - Log message creation and deletion operations

**Status:** ✅ Complete implementation of group-specific lobby chat filtering, personal deletion, group-only access control, and admin broadcast functionality

## Migration Notes

### Breaking Changes:
- **LobbyChatService.GetMessages()** - Now enforces group-only access
- **LobbyChat component** - Updated to async initialization
- **Clear chat behavior** - Now personal instead of global
- **Chat access** - Restricted to group members only

### Compatibility:
- **Existing group chat** - Works with enhanced filtering
- **Group games** - Now have proper chat isolation
- **User experience** - Enhanced privacy and control
- **Non-group users** - No longer have chat access (breaking change)
- **Admin functionality** - New broadcast capabilities for admins

### New Requirements:
- **Group membership** - Required for chat participation (except admins)
- **Admin setup** - Users must be added to groups to chat
- **User guidance** - Clear messaging for access requirements
- **Admin training** - Admins need to understand broadcast functionality
