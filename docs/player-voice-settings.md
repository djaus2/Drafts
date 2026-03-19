# Player Voice Settings Documentation

## Overview

The Player Voice Settings system allows individual users to customize their voice chat preferences on a per-session basis. These settings are stored locally in the browser and persist across game sessions, but are cleared when users log out to ensure privacy.

## Architecture

### Storage Strategy
- **Player Settings**: Browser `localStorage` (client-side)
- **Admin Settings**: Database (server-side)
- **Default Fallback**: Admin settings when no player settings exist

### Data Flow
```
Login → Clear localStorage → Load Admin Defaults → Apply Player Settings (if any)
Change Settings → Update localStorage → Apply immediately
Logout → Clear localStorage → Server logout
```

## Implementation Details

### Key Files
- `Components/VoiceSettingsPanel.razor` - Main voice settings UI component
- `Components/Layout/MainLayout.razor` - Logout functionality with localStorage clearing
- `Components/Pages/Login.razor` - Login page with localStorage clearing
- `Components/DraftsGame.razor` - Game interface with voice settings button

### Settings Available

#### Audio Processing Toggles
- **Echo Cancellation** (`echoCancellationEnabled`)
- **Noise Suppression** (`noiseSuppressionEnabled`) 
- **Auto Gain Control** (`autoGainControlEnabled`)

#### Audio Controls
- **Input Sensitivity** (`inputSensitivity`) - Slider (0-100)
- **Adaptive Bitrate** (`adaptiveBitrateEnabled`) - Toggle
- **Quality Priority** (`qualityPriority`) - Dropdown (latency/quality/bandwidth)

#### System Settings
- **Use Enhanced Voice Chat** (`useEnhancedVoiceChat`) - Toggle

## User Experience

### Initial Login
1. User navigates to login page
2. localStorage is cleared automatically
3. User logs in successfully
4. Voice settings load with admin defaults
5. User can customize settings immediately

### In-Game Usage
1. User creates/joins game on `/drafts` page
2. Clicks "⚙️ Voice Settings" button
3. Settings dialog opens with current preferences
4. Changes are applied and saved immediately
5. Dialog can be closed/reopened with settings persisted

### Logout Process
1. User clicks "Logout" button
2. Voice chat connections are disconnected gracefully
3. localStorage is cleared
4. User is redirected to server logout
5. Next user gets fresh admin defaults

## Technical Implementation

### localStorage Structure
```javascript
localStorage.setItem('voiceSettings', JSON.stringify({
    echoCancellationEnabled: true,
    noiseSuppressionEnabled: true,
    autoGainControlEnabled: true,
    inputSensitivity: 75,
    adaptiveBitrateEnabled: true,
    qualityPriority: "quality",
    useEnhancedVoiceChat: false
}));
```

### Key Methods

#### LoadPlayerSettingsWithDefaults()
```csharp
private async Task LoadPlayerSettingsWithDefaults()
{
    // 1. Load admin defaults first
    var adminDefaults = await AdminVoiceSettings.GetAdminVoiceSettingsAsync();
    
    // 2. Try to load player's localStorage settings
    var playerSettingsJson = await JS.InvokeAsync<string>("localStorage.getItem", "voiceSettings");
    
    // 3. Apply player settings or use admin defaults
    if (!string.IsNullOrWhiteSpace(playerSettingsJson))
    {
        var playerSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(playerSettingsJson);
        ApplySettings(playerSettings);
    }
    else
    {
        ApplySettings(adminDefaults);
    }
}
```

#### SavePlayerSettingsToLocalStorage()
```csharp
private async Task SavePlayerSettingsToLocalStorage(Dictionary<string, object> settings)
{
    var settingsJson = JsonSerializer.Serialize(settings);
    await JS.InvokeVoidAsync("localStorage.setItem", "voiceSettings", settingsJson);
    
    // Verify save by reading back
    var savedJson = await JS.InvokeAsync<string>("localStorage.getItem", "voiceSettings");
}
```

#### ApplySettings()
```csharp
private void ApplySettings(Dictionary<string, object>? settings)
{
    // Handles both direct types and JsonElement from JSON deserialization
    foreach (var setting in settings)
    {
        switch (setting.Key)
        {
            case "echoCancellationEnabled":
                if (setting.Value is bool boolVal) echoCancellationEnabled = boolVal;
                else if (setting.Value is JsonElement elem && bool.TryParse(elem.GetRawText(), out var parsedBool))
                    echoCancellationEnabled = parsedBool;
                break;
            // ... similar for other settings
        }
    }
}
```

### JavaScript Integration

#### Logout Handler
```javascript
function clearLocalStorageAndLogout() {
    console.log('[Logout] Disconnecting connections, clearing localStorage and redirecting to logout');
    
    // Disconnect voice chat
    if (window.draftsVoice && typeof window.draftsVoice.disconnect === 'function') {
        window.draftsVoice.disconnect();
    }
    
    // Clear localStorage
    localStorage.removeItem('voiceSettings');
    
    // Delay for cleanup, then logout
    setTimeout(() => {
        window.location.href = '/logout';
    }, 100);
}
```

#### Login Handler
```javascript
// Runs on login page load
console.log('[Login] Clearing localStorage for fresh login');
localStorage.removeItem('voiceSettings');
```

## Persistence Behavior

### What Persists
- ✅ Across dialog close/reopen
- ✅ Across game exit/restart  
- ✅ Across browser refresh
- ✅ Across app restart
- ✅ During active session

### What Clears Settings
- ✅ User logout (intentional)
- ✅ Login page load (safety net)
- ✅ Browser data clearing (user action)

### Privacy & Security
- Settings stored only in user's browser
- No server-side storage of player preferences
- Automatic clearing on logout ensures privacy
- Admin settings remain as system defaults

## Debugging

### Console Messages
The system provides detailed console logging:
- `[VoiceSettings] Loading from localStorage: {...}`
- `[VoiceSettings] Saving to localStorage: {...}`
- `[VoiceSettings] ApplySettings called by MethodName with X settings`
- `[VoiceSettings] SettingName = value (type)`
- `[Logout] Disconnecting connections, clearing localStorage...`
- `[Login] Clearing localStorage for fresh login`

### Common Issues & Solutions

#### Settings Not Persisting
- Check console for JsonElement parsing errors
- Verify localStorage is not being cleared unexpectedly
- Ensure ApplySettings is handling JsonElement types correctly

#### Wrong Settings on Load
- Verify localStorage clearing on login/logout is working
- Check if admin defaults are being applied instead of player settings
- Look for multiple ApplySettings calls in console

#### Logout Errors
- Ensure voice chat disconnection is working
- Check for TaskCanceledException in logs
- Verify setTimeout delay allows proper cleanup

## Testing Checklist

### Basic Functionality
- [ ] Settings load with admin defaults on fresh login
- [ ] Settings persist across dialog close/reopen
- [ ] Settings persist across game restart
- [ ] Settings persist across browser refresh
- [ ] Settings persist across app restart

### Privacy & Security
- [ ] Settings cleared on logout
- [ ] Settings cleared on login page load
- [ ] New user gets admin defaults in same browser
- [ ] New user gets admin defaults in new browser

### Error Handling
- [ ] No exceptions during normal operation
- [ ] Graceful handling of localStorage errors
- [ ] Clean logout without TaskCanceledException
- [ ] Proper voice chat disconnection

## Future Considerations

### Potential Enhancements
- User account-based settings storage (server-side)
- Settings synchronization across devices
- Settings import/export functionality
- Preset configurations (gaming, meeting, etc.)

### Known Limitations
- Settings are browser-specific
- No cross-device synchronization
- Settings lost if browser data is cleared
- Admin settings override during certain edge cases

## Version History

- **V4.5.0** - Initial implementation with localStorage persistence
- **V4.5.0** - Fixed JsonElement boolean parsing issues
- **V4.5.0** - Added login/logout localStorage clearing
- **V4.5.0** - Implemented graceful voice chat disconnection

---

## Admin Voice Settings Documentation

### Overview

Admin Voice Settings provide system-wide default voice chat configurations that are stored in the database and shared across all users. These settings serve as defaults for new players and can be modified by administrators through the admin interface.

### Access & Interface

- **Location**: `/admin` page → "Manage voice settings" button
- **Interface**: Popup dialog with dark theme styling
- **Authorization**: Requires "Admin" role
- **Persistence**: Database storage (SQLite `auth.Users.VoiceSettings` column)

### Settings Available

#### Audio Processing Toggles
- **Echo Cancellation** (`echoCancellationEnabled`) - Global default
- **Noise Suppression** (`noiseSuppressionEnabled`) - Global default  
- **Auto Gain Control** (`autoGainControlEnabled`) - Global default

#### Audio Controls
- **Input Sensitivity** (`inputSensitivity`) - Global default (0-100)
- **Adaptive Bitrate** (`adaptiveBitrateEnabled`) - Global default
- **Quality Priority** (`qualityPriority`) - Global default (latency/quality/bandwidth)

#### System Settings
- **Use Enhanced Voice Chat** (`useEnhancedVoiceChat`) - Global default

### Technical Implementation

#### Key Components
- `AdminVoiceSettingsService.cs` - Database operations and caching
- `AuthService.cs` - Database CRUD operations for voice settings
- `VoiceSettingsPanel.razor` - UI with `IsAdminMode="true"`
- `Components/Pages/Admin.razor` - Admin interface integration

#### Database Schema
```sql
CREATE TABLE "Users" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Roles" TEXT NOT NULL,
    "PinSalt" BLOB NOT NULL,
    "PinHash" BLOB NOT NULL,
    "PreferredTtsVoice" TEXT NULL,
    "PreferredTtsLanguage" TEXT NULL,
    "PreferredTtsRegion" TEXT NULL,
    "VoiceSettings" TEXT NULL  -- JSON string with admin voice settings
);
```

#### Data Flow
```
Admin opens dialog → Load from database/cache → Apply to UI
Admin changes setting → Save to database → Update cache
Player logs in → Load admin defaults → Apply as baseline
Player changes setting → Save to localStorage (overrides admin defaults)
```

#### Key Methods

##### GetAdminVoiceSettingsAsync()
```csharp
public async Task<Dictionary<string, object>> GetAdminVoiceSettingsAsync()
{
    // 1. Check cache first
    if (_cachedAdminSettings.Count > 0) return _cachedAdminSettings;
    
    // 2. Load from database
    var settingsJson = await _auth.GetAdminVoiceSettingsAsync();
    
    // 3. Parse JSON or return defaults
    if (!string.IsNullOrWhiteSpace(settingsJson))
    {
        var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(settingsJson);
        _cachedAdminSettings = settings;
        return settings;
    }
    
    return GetDefaultVoiceSettings();
}
```

##### SaveAdminVoiceSettingsAsync()
```csharp
public async Task<bool> SaveAdminVoiceSettingsAsync(Dictionary<string, object> settings)
{
    var settingsJson = JsonSerializer.Serialize(settings);
    var success = await _auth.SaveAdminVoiceSettingsAsync(settingsJson);
    
    if (success)
    {
        _cachedAdminSettings = new Dictionary<string, object>(settings);
    }
    
    return success;
}
```

#### Admin Mode UI Handling
```csharp
if (IsAdminMode)
{
    // Load admin settings from database
    var adminSettings = await AdminVoiceSettings.GetAdminVoiceSettingsAsync();
    ApplySettings(adminSettings);
}
else
{
    // Load player settings from localStorage with admin defaults
    await LoadPlayerSettingsWithDefaults();
}
```

### Caching Strategy

- **Memory Cache**: `_cachedAdminSettings` for performance
- **Cache Invalidation**: `ClearCache()` method for manual updates
- **Cache Update**: Automatic on successful save operations
- **Fallback**: Database query if cache is empty

### Database Operations

#### Loading Admin Settings
```sql
SELECT "VoiceSettings" FROM "Users" 
WHERE instr("Roles", 'Admin') > 0 
LIMIT 1
```

#### Saving Admin Settings
```sql
UPDATE "Users" SET "VoiceSettings" = @settingsJson 
WHERE "Id" = @adminUserId
```

### User Experience

#### Admin Workflow
1. Admin navigates to `/admin` page
2. Clicks "Manage voice settings" button
3. Popup dialog opens with current admin settings
4. Admin modifies any setting (toggles, sliders, dropdowns)
5. Changes are saved immediately to database
6. Settings are cached for future loads

#### Player Impact
1. New players receive admin defaults as baseline
2. Players can override admin settings with personal preferences
3. Admin changes affect new sessions, not existing player preferences
4. Admin settings serve as system-wide defaults

### Debugging & Monitoring

#### Console Messages
- `[AdminVoiceSettings] Returning cached settings`
- `[AdminVoiceSettings] Database settings: {...}`
- `[AdminVoiceSettings] Loaded X settings from database`
- `[VoiceSettings] Admin mode - received X settings`
- `[VoiceSettings] Admin mode - saving settings: {...}`

#### Common Issues & Solutions

##### Settings Not Persisting
- Verify database connection and table structure
- Check if admin user exists in database
- Ensure `VoiceSettings` column is not NULL
- Look for database update errors in logs

##### Wrong Settings Displayed
- Clear admin cache: `AdminVoiceSettingsService.ClearCache()`
- Verify JSON parsing in database operations
- Check `ApplySettings` method for JsonElement handling

##### Performance Issues
- Admin settings are cached after first load
- Database queries only happen on cache miss
- Consider cache size for large deployments

### Integration with Player Settings

#### Priority System
1. **Player localStorage** (highest priority) - User's personal preferences
2. **Admin database settings** (medium priority) - System defaults
3. **Hardcoded defaults** (lowest priority) - Fallback values

#### Loading Sequence
```csharp
// Player mode loading sequence
var adminDefaults = await AdminVoiceSettings.GetAdminVoiceSettingsAsync();
var playerSettings = LoadFromLocalStorage();
ApplySettings(playerSettings ?? adminDefaults);
```

#### Storage Separation
- **Admin**: Database (shared, persistent)
- **Player**: localStorage (personal, session-based)
- **Privacy**: Player settings never stored in database

### Security Considerations

#### Access Control
- Only users with "Admin" role can modify settings
- Database operations require proper authentication
- Settings validation prevents malicious values

#### Data Integrity
- JSON schema validation for settings structure
- Type checking in ApplySettings method
- Graceful fallback for corrupted data

### Testing Checklist

#### Admin Functionality
- [ ] Admin can access voice settings dialog
- [ ] All settings load correctly from database
- [ ] Settings persist across dialog close/reopen
- [ ] Settings persist across admin logout/login
- [ ] Database updates execute successfully
- [ ] Cache invalidation works properly

#### Player Integration
- [ ] New players receive admin defaults
- [ ] Player overrides don't affect admin settings
- [ ] Admin changes don't override existing player preferences
- [ ] Settings priority system works correctly

#### Error Handling
- [ ] Database connection failures handled gracefully
- [ ] JSON parsing errors don't crash application
- [ ] Invalid setting types are logged and skipped
- [ ] Cache corruption falls back to database

### Future Enhancements

#### Potential Features
- Multiple admin profiles (different default sets)
- Settings versioning and migration
- Real-time admin setting updates for active players
- Settings audit trail and change history
- Group-specific admin settings

#### Performance Optimizations
- Distributed cache for multi-server deployments
- Lazy loading of admin settings
- Background cache warming
- Database connection pooling

---

## UI Improvements Addendum (V4.5.0)

### Overview

Significant UI/UX improvements were implemented to provide clearer user intent and better control over voice settings management. The confusing "✕" close button was replaced with explicit action buttons.

### Key Changes

#### Button Layout Redesign
- **Removed**: "✕" close button (ambiguous exit without save)
- **Added**: Explicit **Save**, **Cancel**, and **Restore** buttons
- **Layout**: Restore buttons (left) + Cancel/Save buttons (right)

#### Mode-Specific Functionality

##### Admin Mode Buttons
- **Save** (Green): Saves current settings to database and closes dialog
- **Cancel** (Gray): Discards changes, reloads from database, closes dialog
- **Restore Defaults** (Orange): Applies hardcoded defaults and saves to database

##### Player Mode Buttons
- **Save** (Green): Saves current settings to localStorage and closes dialog
- **Cancel** (Gray): Discards changes, reloads from localStorage, closes dialog
- **Restore to Admin** (Orange): Applies current admin defaults to localStorage

#### Behavior Changes
- **No Auto-Save**: Toggle/slider changes no longer auto-save
- **Explicit Save Required**: Users must click Save to persist changes
- **Cancel Discards**: Cancel button reloads original settings
- **Restore Immediate**: Restore buttons apply defaults immediately

### Technical Implementation

#### UI Structure
```html
<!-- Action Buttons -->
<div class="settings-actions">
    <div class="actions-left">
        @if (IsAdminMode)
        {
            <button class="action-btn restore-btn" @onclick="RestoreDefaults">
                Restore Defaults
            </button>
        }
        else
        {
            <button class="action-btn restore-btn" @onclick="RestoreToAdmin">
                Restore to Admin
            </button>
        }
    </div>
    
    <div class="actions-right">
        <button class="action-btn cancel-btn" @onclick="CancelChanges">
            Cancel
        </button>
        <button class="action-btn save-btn" @onclick="SaveAndClose">
            Save
        </button>
    </div>
</div>
```

#### Button Styling
```css
.save-btn {
    background: #4CAF50;
    color: white;
}

.cancel-btn {
    background: rgba(255, 255, 255, 0.2);
    color: white;
    border: 1px solid rgba(255, 255, 255, 0.3);
}

.restore-btn {
    background: rgba(255, 152, 0, 0.8);
    color: white;
    border: 1px solid rgba(255, 152, 0, 0.5);
}
```

#### Method Changes
- **Toggle Methods**: Removed `await SaveSettings()` calls
- **Slider Methods**: Removed `await SaveSettings()` calls
- **New Methods**: `SaveAndClose()`, `CancelChanges()`, `RestoreDefaults()`, `RestoreToAdmin()`

### User Experience Improvements

#### Clear Intent
- **Save**: Explicitly confirms changes
- **Cancel**: Clearly discards changes
- **Restore**: Provides reset functionality

#### Reduced Confusion
- **No accidental data loss** from X button
- **Clear visual hierarchy** with colored buttons
- **Mode-specific labeling** (Restore Defaults vs Restore to Admin)

#### Better Workflow
1. **Make changes** → Settings update in UI only
2. **Review changes** → All modifications visible
3. **Choose action** → Save/Cancel/Restore
4. **Confirm intent** → Explicit button click

### Method Documentation

#### SaveAndClose()
```csharp
private async Task SaveAndClose()
{
    // Save current settings (admin: database, player: localStorage)
    await SaveSettings();
    // Close the panel
    await OnClose.InvokeAsync();
}
```

#### CancelChanges()
```csharp
private async Task CancelChanges()
{
    // Reload settings to discard changes
    await LoadSettings();
    // Close the panel
    await OnClose.InvokeAsync();
}
```

#### RestoreDefaults() - Admin Only
```csharp
private async Task RestoreDefaults()
{
    // Apply hardcoded defaults
    ApplyDefaultSettings();
    // Save defaults to database
    if (IsAdminMode)
    {
        var defaultSettings = CreateSettingsDictionary();
        await AdminVoiceSettings.SaveAdminVoiceSettingsAsync(defaultSettings);
    }
}
```

#### RestoreToAdmin() - Player Only
```csharp
private async Task RestoreToAdmin()
{
    // Load admin defaults from database
    var adminDefaults = await AdminVoiceSettings.GetAdminVoiceSettingsAsync();
    ApplySettings(adminDefaults);
    // Save admin defaults to localStorage
    if (!IsAdminMode)
    {
        await SavePlayerSettingsToLocalStorage(adminDefaults);
    }
}
```

### Visual Design

#### Button Placement
```
┌─────────────────────────────────────────────────┐
│                Voice Settings                  │
├─────────────────────────────────────────────────┤
│                                                 │
│              [Settings Content]                 │
│                                                 │
├─────────────────────────────────────────────────┤
│ [Restore Defaults]           [Cancel] [Save]   │
└─────────────────────────────────────────────────┘
```

#### Color Coding
- **Green (Save)**: Positive action, confirms changes
- **Gray (Cancel)**: Neutral action, discards changes
- **Orange (Restore)**: Warning action, resets to defaults

### Benefits

#### User Experience
- **Clear intent** - No ambiguous X button
- **Explicit control** - Users decide when to save
- **Easy recovery** - Restore buttons provide quick reset
- **Mode awareness** - Different restore options per mode

#### Data Integrity
- **Prevented accidental loss** - No more X button mistakes
- **Explicit confirmation** - Save requires deliberate action
- **Clean state management** - Cancel properly reloads state
- **Consistent behavior** - Same pattern across admin/player

### Testing Checklist

#### Button Functionality
- [ ] Save button saves and closes dialog
- [ ] Cancel button discards changes and closes
- [ ] Restore Defaults works in admin mode
- [ ] Restore to Admin works in player mode
- [ ] Button styling displays correctly

#### State Management
- [ ] Changes not auto-saved
- [ ] Cancel reloads original settings
- [ ] Save persists to correct storage
- [ ] Restore applies correct defaults

#### Cross-Mode Behavior
- [ ] Admin shows "Restore Defaults"
- [ ] Player shows "Restore to Admin"
- [ ] Save goes to correct destination (DB vs localStorage)
- [ ] Cancel reloads from correct source

### Future Considerations

#### Potential Enhancements
- **Unsaved changes indicator** - Visual cue for modified settings
- **Keyboard shortcuts** - ESC for cancel, Enter for save
- **Confirmation dialogs** - For restore operations
- **Tooltip help** - Explain what each restore button does

#### Accessibility
- **Button labels** - Clear text for screen readers
- **Keyboard navigation** - Tab order and focus management
- **Color contrast** - Meet WCAG guidelines
- **Touch targets** - Appropriate button sizes

---

*Last Updated: 2026-03-19*
*Version: 4.5.0*
