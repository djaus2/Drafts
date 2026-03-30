# Game Reaper System Documentation

## Overview

The Game Reaper is a background cleanup process that automatically removes abandoned or expired games from the system to prevent resource waste and keep the game list clean.

## Configuration

### Reaper Period Seconds
- **Setting**: `_reaperPeriodSeconds`
- **Default**: 30 seconds
- **Purpose**: How frequently the reaper runs to check for expired games
- **Location**: Admin.razor settings section

### Game Timeout Settings
- **Max Move Timeout**: Time a player has to make a move after opponent moves
- **Max Game Time**: Maximum duration a game can run after starting
- **Max Game Start Wait Time**: Maximum time a waiting game can wait for players

## How It Works

1. **Periodic Execution**: The reaper runs every `_reaperPeriodSeconds` (default: 30 seconds)
2. **Game Evaluation**: Checks all active games against timeout criteria
3. **Automatic Cleanup**: Removes games that have exceeded their time limits
4. **Resource Management**: Frees up server resources and maintains clean game list

## Timeout Conditions

Games are removed when they exceed any of these limits:

1. **Move Timeout**: A player takes too long to make their move
2. **Game Duration**: Total game time exceeds maximum allowed duration
3. **Start Wait Time**: Game waits too long for all players to join

## Implementation Details

### Admin Configuration
Admins can adjust reaper settings in the Admin panel:
- Navigate to `/admin`
- Modify "Reaper period (seconds)" in the Settings section
- Click "Save" to apply changes

### Technical Notes
- The reaper runs as part of the `DraughtsService` background processing
- Uses efficient database queries to identify expired games
- Gracefully handles edge cases and prevents race conditions

## Best Practices

- **Default Setting**: 30 seconds is suitable for most scenarios
- **High Traffic**: Consider shorter intervals (15-20 seconds) for busy servers
- **Low Traffic**: Longer intervals (60+ seconds) are fine for quiet servers
- **Monitoring**: Watch system performance when adjusting reaper frequency

## Troubleshooting

### Games Not Being Cleaned Up
- Check if reaper period is set appropriately
- Verify timeout settings are not too generous
- Review game creation timestamps for accuracy

### Performance Issues
- Reduce reaper frequency if server is under high load
- Monitor database query performance during reaper runs
- Consider optimizing timeout logic for large game volumes

## Related Components

- `DraughtsService` - Core game management service
- `SettingsService` - Manages reaper configuration
- `Admin.razor` - Admin interface for reaper settings
- Game timeout validation logic in game creation/joining flows

---

*Last Updated: March 2026*
