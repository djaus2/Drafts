# PIN Management Fixes

## Issues Fixed

### 1. Admin PIN Change - "Id not found" Error
**Problem:** When trying to change PIN on /Admin page, it showed "Id not found" error.

**Root Cause:** The `GetCurrentUserId()` method in Admin.razor was missing a fallback to look up the user by name when the `uid` claim was not available.

**Solution:** Added fallback logic to get user ID by username if `uid` claim is missing.

```csharp
private int? GetCurrentUserId()
{
    var user = HttpContextAccessor.HttpContext?.User;
    var raw = user?.FindFirst("uid")?.Value;
    if (int.TryParse(raw, out var id)) return id;
    
    // Fallback: lookup by username if uid claim not found
    var name = user?.Identity?.Name;
    if (!string.IsNullOrWhiteSpace(name))
    {
        var appUser = Auth.GetUserByNameAsync(name).GetAwaiter().GetResult();
        if (appUser != null) return appUser.Id;
    }
    
    return null;
}
```

### 2. Player Page - Missing "Change My PIN" Functionality
**Problem:** Player page had no option for users to change their own PIN.

**Solution:** Added "Change My PIN" section with current PIN and new PIN fields.

**UI Added:**
```html
<h4>Change My PIN</h4>
<div style="max-width:360px">
    <div style="display:flex;flex-direction:column;gap:8px">
        <input @bind="_currentPin" placeholder="Current PIN" inputmode="numeric" maxlength="4" />
        <input @bind="_newPin" placeholder="New PIN" inputmode="numeric" maxlength="4" />
        <button @onclick="OnChangePin">Change PIN</button>
        @if (!string.IsNullOrWhiteSpace(_pinMessage))
        {
            <div>@_pinMessage</div>
        }
    </div>
</div>
```

**Code Added:**
```csharp
private string _currentPin = "";
private string _newPin = "";
private string _pinMessage = "";

private async Task OnChangePin()
{
    _pinMessage = "";
    var userId = GetCurrentUserId();
    if (!userId.HasValue)
    {
        _pinMessage = "No player id.";
        return;
    }

    var ok = await Auth.ChangePinAsync(userId.Value, (_currentPin ?? "").Trim(), (_newPin ?? "").Trim());
    _pinMessage = ok ? "PIN changed." : "Failed (check current PIN and new PIN format).";
    if (ok)
    {
        _currentPin = "";
        _newPin = "";
    }
}
```

### 3. Admin-Only Reset PIN Functionality
**Status:** ✅ Maintained as Admin-only

**Existing Functionality:** The `ResetUserPinTo9999Async` method in AuthService.cs remains Admin-only and requires Admin PIN verification.

**Security:** Only Admin users can reset other users' PINs to 9999, and Admin users themselves cannot be reset.

### 4. Player Page Login Display Issue
**Problem:** Player page showed only a dot instead of the username in "You are logged in as" display.

**Root Cause:** Direct access to `HttpContextAccessor.HttpContext?.User?.Identity?.Name` was not reliable during initial render.

**Solution:** Added username caching during initialization and used stored value for display.

**Code Changes:**
```csharp
private string _currentUserName = "";

protected override async Task OnInitializedAsync()
{
    await InvokeAsync(async () =>
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        var userName = user?.Identity?.Name;
        
        // Store username for display
        _currentUserName = userName ?? "Unknown";
        // ... rest of initialization
    });
}
```

**UI Change:**
```html
<!-- Before -->
<p>You are logged in as <b>@(HttpContextAccessor.HttpContext?.User?.Identity?.Name)</b>.</p>

<!-- After -->
<p>You are logged in as <b>@_currentUserName</b>.</p>
```

## Features Summary

### Admin Page (/Admin)
- ✅ **Change Own PIN** - Fixed with proper user ID resolution
- ✅ **Reset Other Users' PIN** - Admin-only functionality maintained
- ✅ **Admin PIN Verification** - Required for admin operations

### Player Page (/Player)
- ✅ **Change Own PIN** - New functionality added
- ✅ **Current PIN Required** - Security verification
- ✅ **New PIN Validation** - 4-digit numeric validation
- ✅ **Username Display** - Fixed login display issue

### Security Features
- ✅ **PIN Validation** - 4-digit numeric format only
- ✅ **Current PIN Verification** - Required for PIN changes
- ✅ **Admin Privileges** - Reset functionality remains Admin-only
- ✅ **User Isolation** - Users can only change their own PIN

### User Experience
- ✅ **Clear Feedback** - Success/error messages for all operations
- ✅ **Form Validation** - Input constraints and validation
- ✅ **Responsive Design** - Consistent styling with existing UI
- ✅ **Error Handling** - Graceful error handling with user feedback

## Testing Scenarios

### Admin PIN Change
1. Login as Admin
2. Navigate to /Admin
3. Enter current PIN (9999)
4. Enter new PIN (4 digits)
5. Click "Change PIN"
6. Verify success message

### Player PIN Change
1. Login as any Player (Bob, Carol, etc.)
2. Navigate to /Player
3. Enter current PIN (9999)
4. Enter new PIN (4 digits)
5. Click "Change PIN"
6. Verify success message

### Admin Reset PIN
1. Login as Admin
2. Navigate to /Admin/Users
3. Select a player
4. Enter Admin PIN
5. Click "Reset PIN to 9999"
6. Verify success message

### Error Scenarios
- **Wrong Current PIN** - Should show error message
- **Invalid New PIN** - Should show validation error
- **Missing User ID** - Should show "No player id" error
- **Network Issues** - Should handle gracefully

## Files Modified

### Core Files
- `Components/Pages/Admin.razor` - Fixed GetCurrentUserId method
- `Components/Pages/Player.razor` - Added PIN change functionality and username display
- `Services/AuthService.cs` - No changes (existing methods used)

### Supporting Files
- `docs/pin-management-fixes.md` - This documentation

## Status: ✅ COMPLETE

All requested PIN management issues have been resolved:
1. ✅ Admin PIN change fixed
2. ✅ Player PIN change functionality added
3. ✅ Admin-only reset PIN maintained
4. ✅ Player page login display fixed

The application now provides complete PIN management functionality with proper security controls and user feedback.
