# Chat Clearing on Login Fix

## Problem
Lobby chat was not being cleared when users logged back in, causing old messages to persist across login sessions.

## Root Cause Analysis

### **Issue 1: Display-Only Clearing**
The original implementation only cleared the UI display variables:
```csharp
_chatInText = "";
_chatOutText = "";
_lastChatCount = 0;
```

However, when `RefreshChat()` was called after data loading, it would repopulate the display with existing messages from the `LobbyChatService`, effectively undoing the clear.

### **Issue 2: Wrong Clearing Method**
The code was using display-level clearing instead of service-level clearing:
- **Display clearing**: Clears only what the user sees
- **Service clearing**: Clears the user's personal chat view permanently

## Solution Implemented

### **Fixed LoadUserData Method**
```csharp
private async Task LoadUserData()
{
    // Clear chat display when loading user data (login/refresh)
    _chatInText = "";
    _chatOutText = "";
    _lastChatCount = 0;
    _scrollPending = false;
    _dataLoaded = false;
    
    // Get current user ID and admin status
    var user = HttpContextAccessor.HttpContext?.User;
    var rawUid = user?.FindFirst("uid")?.Value;
    _isAdmin = user?.IsInRole("Admin") ?? false;
    
    if (int.TryParse(rawUid, out var userId))
    {
        _currentUserId = userId;
        
        // Clear user's personal chat view to ensure fresh start on login
        LobbyChatSvc.ClearChatForUser(userId);
        
        // Load user groups
        try
        {
            _userGroups = await Auth.GetUserGroupsAsync(userId);
        }
        catch
        {
            _userGroups = new List<Draughts.Data.Group>();
        }
    }
    
    _dataLoaded = true;
    
    // Refresh chat after data is loaded to apply proper filtering
    RefreshChat();
    await InvokeAsync(StateHasChanged);
}
```

## Key Changes

### **1. Proper Service-Level Clearing**
- **Before**: Only cleared display variables
- **After**: Uses `LobbyChatSvc.ClearChatForUser(userId)` to clear the user's personal chat view

### **2. Correct Execution Order**
- **Step 1**: Get user ID
- **Step 2**: Clear user's chat view in service
- **Step 3**: Load user groups
- **Step 4**: Refresh chat with proper filtering

### **3. Persistent Clearing**
The `ClearChatForUser` method adds all current message indexes to the user's deleted messages set, ensuring they won't appear in future `RefreshChat()` calls.

## How ClearChatForUser Works

```csharp
public void ClearChatForUser(int userId)
{
    lock (_lock)
    {
        var allMessageIndexes = _messages.Select(m => m.MessageIndex).ToHashSet();
        _userDeletedMessages[userId] = allMessageIndexes;
    }

    ChatUpdated?.Invoke();
}
```

This method:
1. **Gets all current message indexes** from the chat service
2. **Adds them to user's deleted set** to hide all messages
3. **Triggers chat update** to refresh UI

## Expected Behavior After Fix

### **Login Process:**
1. **User logs in** → `LoadUserData()` called
2. **Display cleared** → `_chatInText`, `_chatOutText` reset
3. **User ID obtained** → Current user identified
4. **Service chat cleared** → `ClearChatForUser()` hides all messages
5. **Groups loaded** → User's group memberships retrieved
6. **Chat refreshed** → Only new messages after login appear

### **User Experience:**
- **Fresh chat start** on every login ✅
- **No old message persistence** ✅
- **Proper group filtering** maintained ✅
- **Loading state** prevents flicker ✅

## Testing Verification

### **Test Scenario:**
1. **User logs in** → Chat should be empty
2. **Messages sent** → Appear in chat
3. **User logs out** → Messages persist in service
4. **User logs back in** → Chat should be empty again
5. **New messages** → Only new messages appear

### **Expected Results:**
- ✅ Chat cleared on login
- ✅ No old messages visible after login
- ✅ New messages appear correctly
- ✅ Group filtering still works

## Technical Benefits

### **1. Proper State Management**
- **Service-level clearing** ensures persistence
- **User-specific clearing** doesn't affect other users
- **Message index tracking** prevents message re-appearance

### **2. Improved User Experience**
- **Clean login experience** - no confusing old messages
- **Predictable behavior** - same result every time
- **Privacy maintained** - users don't see previous session messages

### **3. System Stability**
- **Thread-safe operations** with proper locking
- **Memory efficient** - doesn't delete actual messages
- **Scalable** - works with multiple concurrent users

## Files Modified

1. **`Components/LobbyChat.razor`**
   - Fixed `LoadUserData()` method
   - Added proper service-level chat clearing
   - Maintained correct execution order

## Status: ✅ COMPLETE

The chat clearing issue has been resolved. Users will now see a clean, empty chat interface when they log back in, with only new messages appearing after login.
