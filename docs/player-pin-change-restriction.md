# Player PIN Change Restriction

## Overview
Version 6.4.0 introduces a security feature that allows administrators to control whether regular players can change their own PINs. This is essential for public deployments where you want to maintain control over user credentials.

## Features

### **Admin Control Setting**
- **Setting Name**: `AllowPlayerPinChange`
- **Type**: Boolean (True/False)
- **Default**: `True` (players can change their PINs)
- **Access**: Admin-only configuration

### **Behavior When Enabled (Default)**
- Players can see the "Change My PIN" section on their Player page
- Players can change their own PINs using the current PIN + new PIN form
- Full PIN change functionality available to all users

### **Behavior When Disabled**
- Players cannot see the PIN change form
- Shows a message: "PIN change is currently disabled. Please contact an administrator if you need to change your PIN."
- Only Admin users can still change PINs (Admins are not affected by this restriction)

## Admin Configuration

### **How to Configure**
1. Login as **Admin** (PIN: 9999)
2. Navigate to **Admin** page
3. Scroll to the **Settings** section
4. Find the checkbox: **"Allow players to change their PIN"**
5. Uncheck to disable, check to enable
6. Click **"Save"**

### **Setting Location in Admin Interface**
```
Settings Section:
├── Max move timeout (mins)
├── Max game time (mins)
├── Max game start wait time (mins)
├── Max login hours
├── Reaper period (seconds)
├── Entrapment mode
├── Multi-jump grace period (seconds)
├── Game Initiator goes first
└── Allow players to change their PIN ← NEW SETTING
```

## User Experience

### **When PIN Change is Enabled**
Players see:
```html
<h4>Change My PIN</h4>
<div>
    <input placeholder="Current PIN" />
    <input placeholder="New PIN (4-6 digits)" />
    <button>Change PIN</button>
</div>
```

### **When PIN Change is Disabled**
Players see:
```html
<h4>PIN Change</h4>
<div style="color: #666; font-style: italic;">
    PIN change is currently disabled. Please contact an administrator if you need to change your PIN.
</div>
```

## Technical Implementation

### **Database Schema**
```sql
-- Added to AppSettings table
ALTER TABLE "Settings" ADD COLUMN "AllowPlayerPinChange" INTEGER NOT NULL DEFAULT 1;
```

### **SettingsService Methods**
```csharp
// Get current setting
public async Task<bool> GetAllowPlayerPinChangeAsync()

// Update setting
public async Task<bool> UpdateAllowPlayerPinChangeAsync(bool newValue)

// Direct property access
public bool AllowPlayerPinChange { get; }
```

### **Player Page Logic**
```csharp
// Load setting during initialization
_allowPlayerPinChange = await Settings.GetAllowPlayerPinChangeAsync();

// Conditional UI rendering
@if (_allowPlayerPinChange)
{
    // Show PIN change form
}
else
{
    // Show disabled message
}
```

### **Admin Page Logic**
```csharp
// Load setting
_allowPlayerPinChange = await Settings.GetAllowPlayerPinChangeAsync();

// Save setting
var ok10 = await Settings.UpdateAllowPlayerPinChangeAsync(_allowPlayerPinChange);
```

## Security Considerations

### **Why This Feature Exists**
1. **Public Deployments**: Prevent users from changing PINs in public environments
2. **Account Security**: Maintain control over user credentials
3. **Administrative Control**: Ensure only authorized PIN changes
4. **Compliance**: Meet security requirements for certain deployments

### **Security Benefits**
- **Prevents Unauthorized Changes**: Users can't change PINs without admin approval
- **Maintains Account Integrity**: Reduces risk of account lockouts or forgotten PINs
- **Audit Trail**: All PIN changes require admin intervention when disabled
- **Flexible Control**: Can be enabled/disabled as needed

### **Admin Exemption**
- **Admin users are NOT affected** by this restriction
- Admins can always change their own PINs through the Admin page
- Admins can change any user's PIN through the Admin Users management page

## Use Cases

### **Public Deployment (Recommended: Disabled)**
- Public gaming websites
- School environments
- Corporate environments
- Any deployment where you need tight control over accounts

### **Private/Development (Recommended: Enabled)**
- Development environments
- Private testing
- Personal deployments
- Small trusted groups

### **Hybrid Approach**
- Enable during initial setup/registration
- Disable once accounts are established
- Re-enable temporarily for maintenance windows

## Migration Notes

### **Existing Deployments**
- **Default is ENABLED** - existing behavior is preserved
- No immediate impact on current users
- Admin can disable at any time

### **New Deployments**
- Consider your security requirements
- Disable for public deployments
- Enable for private/trusted environments

### **Database Migration**
- Setting automatically added to existing databases
- Default value ensures no breaking changes
- No manual database updates required

## Troubleshooting

### **Common Issues**
1. **Setting doesn't save**: Ensure Admin has proper permissions
2. **Players still see PIN change**: Refresh browser cache, check setting was saved
3. **Admin can't change PIN**: This restriction doesn't affect Admin users

### **Verification Steps**
1. Login as Admin, check the setting value
2. Login as Player, verify UI matches setting
3. Test PIN change functionality in both states

### **Debug Information**
- Setting is stored in `Settings` table, `AllowPlayerPinChange` column
- Check browser developer tools for any JavaScript errors
- Verify Admin role permissions if issues persist

## Future Enhancements

### **Potential Improvements**
- **Role-based control**: Different settings per user role
- **Time-based restrictions**: Allow PIN changes only during certain hours
- **Approval workflow**: PIN change requests requiring admin approval
- **Audit logging**: Log all PIN change attempts and admin actions

### **Monitoring**
- Track PIN change frequency
- Monitor setting changes by admins
- Alert on unusual PIN change patterns

---

**Version**: 6.4.0  
**Implemented**: 2026-03-26  
**Security Level**: High  
**Default**: Enabled (True)  
**Admin Control**: Full  
**Player Impact**: UI changes based on setting
