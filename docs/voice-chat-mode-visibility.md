# Voice Chat Mode Visibility Feature

## Overview

This feature implements an admin-controlled setting to choose between direct audio voice chat and text-to-speech (TTS) voice chat mode. When direct audio mode is enabled, all voice-related UI sections are hidden from players to provide a clean, simplified interface.

## Repository **645af62**

Fix voice chat mode visibility - hide voice sections when UseDirectAudioVoiceChat is true
## Changes Made:

### Player Page (/Player):
- Add debug display showing current UseDirectAudioVoiceChat value
- Wrap entire Voice section (lines 52-110) in conditional @if (!useDirectAudioVoiceChat)
- Voice section now only visible when admin allows TTS mode
- Fix HTML structure issues and remove duplicate content

### VoiceSettingsPanel:
- Hide Audio Processing section when UseDirectAudioVoiceChat is true
- Hide Voice System Choice section when UseDirectAudioVoiceChat is true
- Hide Audio Testing section when UseDirectAudioVoiceChat is true
- Add comprehensive debugging for admin settings loading
- Add OnParametersSetAsync to handle parameter changes

### AdminGroups:
- Fix null reference warnings by adding empty list fallbacks

### DraftsGame:
- Fix unawaited call warning by discarding result with underscore
- Comment out unused _sttActive field

### Admin:
- Comment out unused _voiceSettingsMessage field

## Behavior:
- When UseDirectAudioVoiceChat = true (Direct Audio Mode): All voice-related sections are hidden
- When UseDirectAudioVoiceChat = false (TTS Mode): All voice sections are visible
- Debug display on Player page shows current mode for easy verification
- Clean compilation with no warnings or errors



## Features

### Admin Control
- **Setting Location**: `/Admin` page under voice chat settings
- **Setting Name**: `UseDirectAudioVoiceChat`
- **Default Value**: `true` (Direct Audio Mode - Recommended)
- **Control Type**: Checkbox with descriptive help text

### Player Interface Changes

#### When Direct Audio Mode is Enabled (`UseDirectAudioVoiceChat = true`)
- **Player Page**: Entire "Voice" section is hidden
- **Voice Settings Panel**: All voice-related sections are hidden
  - Audio Processing settings (echo cancellation, noise suppression, etc.)
  - Voice System Choice (Enhanced vs Classic voice chat)
  - Audio Testing (echo test, noise test, recording test)
- **Result**: Clean interface without confusing voice options

#### When TTS Mode is Enabled (`UseDirectAudioVoiceChat = false`)
- **Player Page**: Voice selection UI is visible
- **Voice Settings Panel**: All voice sections are visible and functional
- **Result**: Full voice control options available to players

### Debug Features
- **Player Page Debug Display**: Shows current voice chat mode at the top of the page
  - Green: `TRUE (Direct Audio)` when direct audio mode is enabled
  - Orange: `FALSE (TTS Mode)` when TTS mode is enabled
  - Red: `Loading...` while settings are being loaded
- **Console Logging**: Detailed debug information for troubleshooting

## Technical Implementation

### Database Schema
```sql
-- Settings table includes new column
CREATE TABLE "Settings" (
    "Id" INTEGER NOT NULL PRIMARY KEY,
    -- ... other columns ...
    "UseDirectAudioVoiceChat" INTEGER NOT NULL  -- 1 = true, 0 = false
);
```

### Model Changes
```csharp
// AppSettings.cs
[Required]
public bool UseDirectAudioVoiceChat { get; set; } = true;
```

### Service Changes
```csharp
// SettingsService.cs
public async Task<bool> GetUseDirectAudioVoiceChatAsync()
public async Task<bool> UpdateUseDirectAudioVoiceChatAsync(bool newValue)
```

### UI Conditional Logic

#### Player Page (Player.razor)
```razor
<!-- Debug Display -->
<div style="background: rgba(255,255,255,0.1); ...">
    <strong>🔧 Debug - Voice Chat Mode:</strong><br/>
    @if (useDirectAudioVoiceChatLoaded)
    {
        <span style="color: @(useDirectAudioVoiceChat ? "#4CAF50" : "#FF9800");">
            UseDirectAudioVoiceChat = @(useDirectAudioVoiceChat ? "TRUE (Direct Audio)" : "FALSE (TTS Mode)")
        </span>
    }
    else
    {
        <span style="color: #F44336;">Loading...</span>
    }
</div>

<!-- Voice Section (conditionally hidden) -->
@if (!useDirectAudioVoiceChat)
{
    <h4 class="rainbow-heading rainbow-heading--sm">Voice</h4>
    <!-- Voice selection UI -->
}
```

#### Voice Settings Panel (VoiceSettingsPanel.razor)
```razor
<!-- Audio Processing Settings -->
@if (adminSettings?.UseDirectAudioVoiceChat == false)
{
    <div class="settings-section">
        <div class="section-title">Audio Processing</div>
        <!-- Audio processing controls -->
    </div>
}

<!-- Voice System Choice -->
@if (ShowVoiceSystemChoice)  // Only when admin allows TTS
{
    <div class="settings-section">
        <div class="section-title">Voice System</div>
        <!-- Voice system choice controls -->
    </div>
}

<!-- Audio Testing -->
@if (adminSettings?.UseDirectAudioVoiceChat == false)
{
    <div class="settings-section">
        <div class="section-title">Audio Testing</div>
        <!-- Audio testing controls -->
    </div>
}
```

#### Admin Page (Admin.razor)
```razor
<label style="display:flex;align-items:center;gap:8px">
    <input type="checkbox" @bind="_useDirectAudioVoiceChat" />
    Use Direct Audio Voice Chat (Recommended)
    <br />
    <small style="color:#666;">
        @if (_useDirectAudioVoiceChat)
        {
            <text>Players will use direct audio transmission (natural voice). Voice selection options will be hidden.</text>
        }
        else
        {
            <text>Players can choose between direct audio and text-to-speech. Voice selection options will be available.</text>
        }
    </small>
</label>
```

## User Experience

### For Administrators
1. Navigate to `/Admin`
2. Find "Use Direct Audio Voice Chat (Recommended)" checkbox
3. Check the box to enable direct audio mode (default)
4. Uncheck the box to enable TTS mode with voice selection
5. Save settings to apply changes

### For Players
#### Direct Audio Mode (Default)
- **Simple Interface**: No voice-related settings visible
- **Natural Voice**: Players just talk normally - no configuration needed
- **Clean Experience**: Focus on game, not technical settings

#### TTS Mode
- **Voice Selection**: Choose from available system voices
- **Audio Controls**: Configure echo cancellation, noise suppression, etc.
- **Testing Options**: Test audio quality and settings
- **System Choice**: Switch between enhanced and classic voice chat

## Behavior Details

### Voice Chat Transmission
- **Direct Audio Mode**: Real-time audio transmission between players (WebM with Opus codec)
- **TTS Mode**: Text-to-speech synthesis for voice output

### Settings Persistence
- **Admin Settings**: Stored in SQLite database `Settings` table
- **Player Settings**: Stored in browser localStorage (only relevant for TTS mode)
- **Default Values**: Direct audio mode (`true`) for new installations

### Error Handling
- **Graceful Degradation**: Falls back to direct audio mode if settings can't be loaded
- **Debug Logging**: Comprehensive console logging for troubleshooting
- **UI Feedback**: Loading states and error messages

## Migration Notes

### For Existing Installations
The `UseDirectAudioVoiceChat` column will be added automatically by the database seeder, but existing databases may need manual column addition:

```sql
ALTER TABLE "Settings" ADD COLUMN "UseDirectAudioVoiceChat" INTEGER NOT NULL DEFAULT 1;
```

### For New Installations
The setting is automatically created with the default value of `true` (direct audio mode).

## Troubleshooting

### Debug Information
Check the browser console (F12) for debug messages:
- `[Player Debug] UseDirectAudioVoiceChat loaded: true/false`
- `[VoiceSettings] Loaded admin settings - UseDirectAudioVoiceChat: true/false`
- `[VoiceSettings] UI Check - adminSettings null: false, UseDirectAudioVoiceChat: true, ShowVoiceSystemChoice: false`

### Common Issues

#### Voice Section Still Visible
1. Check the debug display on the Player page
2. Verify admin setting is saved correctly
3. Check browser console for error messages
4. Ensure the application has been restarted after changes

#### Settings Not Loading
1. Check database connection
2. Verify Settings table exists and has the `UseDirectAudioVoiceChat` column
3. Check for database migration issues

#### Performance Issues
1. Debug display has minimal performance impact
2. Console logging can be disabled in production by removing debug statements
3. Settings are cached after initial load

## Future Enhancements

### Potential Improvements
- **Per-Game Settings**: Allow different voice modes per game
- **User Preferences**: Remember individual player preferences when allowed
- **Voice Quality Settings**: Advanced audio quality controls
- **Real-time Switching**: Allow changing voice mode during active games

### Related Features
- **Voice Activity Detection**: Automatic voice activation
- **Audio Quality Metrics**: Real-time quality monitoring
- **Voice Recording**: Voice message recording and playback

## Security Considerations

### Settings Access
- **Admin Only**: Only administrators can change the global voice chat mode
- **Player Isolation**: Players cannot override admin settings
- **Database Security**: Settings are stored securely in SQLite database

### Privacy
- **Local Storage**: Player settings stored only in browser localStorage
- **No Tracking**: No voice data is stored or transmitted unnecessarily
- **User Control**: Players have full control over their local audio settings

## Version History

### v4.6.0 - Current Version
- **Added**: Voice chat mode visibility control
- **Added**: Debug display for current mode
- **Fixed**: All voice sections properly hidden in direct audio mode
- **Improved**: Clean compilation with no warnings
- **Enhanced**: Better user experience with simplified interface

### Previous Versions
- Voice selection was always visible regardless of mode
- No admin control over voice chat mode
- Audio settings always shown even when not relevant

## Support

For issues or questions about this feature:
1. Check the debug display on the Player page
2. Review browser console for error messages
3. Verify admin settings are correctly configured
4. Consult the troubleshooting section above

---

**Last Updated**: March 19, 2026  
**Version**: 4.6.0  
**Author**: Development Team
