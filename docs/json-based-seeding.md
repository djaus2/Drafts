# JSON-Based Database Seeding

## Overview
The database seeding has been refactored to read user and group data from `auth.json` instead of hardcoded values. This makes the seeding process more maintainable and allows easy modification of the initial data structure.

## Architecture

### **What Stays the Same:**
- ✅ **Database schema creation** - Tables created as before
- ✅ **Settings seeding** - Default application settings
- ✅ **Fresh database logic** - Only runs on new database creation

### **What Changed:**
- 🔄 **User creation** - Reads from `auth.json` instead of hardcoded list
- 🔄 **Group creation** - Reads from `auth.json` instead of hardcoded groups
- 🔄 **Membership creation** - Reads from `auth.json` instead of hardcoded assignments

## File Structure

### **Core Files:**
```
Data/
├── DbSeeder.cs          # Main seeding logic (updated)
├── AuthModels.cs        # JSON data models (new)
└── ...

auth.json               # User and group data source
```

### **JSON Data Models (`AuthModels.cs`):**
```csharp
public class AuthExport
{
    public DateTime ExportDate { get; set; }
    public List<AuthUser> Users { get; set; }
    public List<AuthGroup> Groups { get; set; }
}

public class AuthUser
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Roles { get; set; }
    public string Pin { get; set; }
    public List<string> Groups { get; set; }
}

public class AuthGroup
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public int OwnerId { get; set; }
    public List<string> Members { get; set; }
}
```

## Seeding Process

### **1. Database Creation (Unchanged)**
```csharp
if (await db.Database.EnsureCreatedAsync())
{
    // Create schema tables (Settings, Users, Groups, GroupMembers)
    // Create default settings
}
```

### **2. User Creation (JSON-Based)**
```csharp
await CreateUsersFromAuthJson(db);
```

**Process:**
- Read `auth.json` file
- Parse JSON into `AuthExport` object
- Create each user with their PIN and roles
- Handle duplicate user names gracefully

### **3. Group Creation (JSON-Based)**
```csharp
await CreateGroupsAndMembershipsFromAuthJson(db);
```

**Process:**
- Read `auth.json` file
- Create each group with proper ownership
- Add all members to each group
- Maintain group ownership relationships

## JSON File Structure

### **Current `auth.json`:**
```json
{
  "exportDate": "2026-03-16T03:26:12.0962439Z",
  "users": [
    {
      "id": 1,
      "name": "Admin",
      "roles": "Admin,Player",
      "pin": "9999",
      "groups": []
    },
    {
      "id": 5,
      "name": "Alice",
      "roles": "Player",
      "pin": "9999",
      "groups": ["Ted-Alice"]
    }
  ],
  "groups": [
    {
      "id": 2,
      "name": "Ted-Alice",
      "description": "Ted and Alice's group",
      "ownerId": 5,
      "members": ["Alice", "Tad"]
    }
  ]
}
```

## Benefits

### **🔧 Maintainability:**
- **Single source of truth** - User data in one place
- **Easy updates** - Modify JSON instead of code
- **Version control friendly** - JSON changes are clear
- **No recompilation** - Change data without rebuilding

### **🎯 Flexibility:**
- **Add users** - Just add to JSON array
- **Modify groups** - Update group definitions
- **Change PINs** - Update pin values
- **Adjust roles** - Modify role strings

### **🛡️ Safety:**
- **Graceful fallback** - Works without auth.json
- **Error handling** - Warnings for missing data
- **Duplicate prevention** - Checks existing users
- **Validation** - Validates JSON structure

## Error Handling

### **Missing File:**
```
Warning: auth.json not found, skipping user creation from JSON
```
- Seeding continues without JSON data
- Database still created with schema
- No users or groups created

### **Invalid JSON:**
- JSON parsing errors caught
- Warning messages logged
- Seeding continues gracefully

### **Missing Data:**
```
Warning: No users found in auth.json
Warning: No groups found in auth.json
```
- Partial data handled gracefully
- Existing data preserved

## Usage Scenarios

### **Development:**
- **Rapid prototyping** - Test different user setups
- **Feature testing** - Create specific test scenarios
- **Team collaboration** - Share user configurations

### **Production:**
- **Environment-specific data** - Different auth.json per environment
- **Initial setup** - Deploy with predefined users
- **Configuration management** - Version control user data

### **Testing:**
- **Test data management** - Create test scenarios
- **CI/CD pipelines** - Automated database setup
- **Integration testing** - Consistent test data

## Migration from Hardcoded

### **Before (Hardcoded):**
```csharp
var playerNames = new[] { "Bob", "Carol", "Tad", "Alice", "Fred" };
foreach (var playerName in playerNames)
{
    // Create user with hardcoded PIN 9999
}
```

### **After (JSON-Based):**
```csharp
foreach (var authUser in authExport.Users)
{
    // Create user with authUser.Pin and authUser.Roles
}
```

## Customization Examples

### **Add New User:**
```json
{
  "id": 7,
  "name": "George",
  "roles": "Player",
  "pin": "1234",
  "groups": ["NewGroup"]
}
```

### **Create New Group:**
```json
{
  "id": 3,
  "name": "NewGroup",
  "description": "A new test group",
  "ownerId": 7,
  "members": ["George", "Alice"]
}
```

### **Change PINs:**
```json
{
  "id": 1,
  "name": "Admin",
  "pin": "secure123",
  // ...
}
```

## File Location Resolution

### **Path Logic:**
```csharp
var authJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "auth.json");
```

### **Deployment Locations:**
- **Development:** Project root directory
- **Production:** Application directory
- **Docker:** Container application directory
- **Azure:** App Service wwwroot or specific path

## Testing the Seeding

### **Fresh Database Test:**
1. **Delete `auth.db`** file
2. **Run application**
3. **Verify users created** from auth.json
4. **Verify groups created** from auth.json
5. **Test login** with JSON-defined PINs

### **JSON Modification Test:**
1. **Modify auth.json** (add user, change PIN)
2. **Delete `auth.db`** file
3. **Run application**
4. **Verify changes** applied correctly

## Future Enhancements

### **Potential Improvements:**
- **JSON validation schema** - Ensure data quality
- **Multiple environment support** - auth.dev.json, auth.prod.json
- **Dynamic PIN generation** - Generate secure PINs automatically
- **Group permission system** - Add role-based group permissions
- **User profile data** - Add more user attributes

## Status: ✅ COMPLETE

The JSON-based seeding system is fully implemented and provides a flexible, maintainable approach to database initialization while preserving all existing database schema creation logic.
