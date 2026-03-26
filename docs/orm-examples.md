# Entity Framework Core ORM Examples

## Overview
This document provides comprehensive examples of Entity Framework Core ORM operations for the Drafts application database.

## Database Context Setup

### **Using AppDbContext:**
```csharp
// Dependency injection (recommended)
public class SomeService
{
    private readonly AppDbContext _db;
    
    public SomeService(AppDbContext db)
    {
        _db = db;
    }
}

// Manual creation (for testing)
using var db = new AppDbContext();
```

## User Operations

### **Create User:**
```csharp
public async Task<AppUser> CreateUserAsync(string name, string roles, string pin)
{
    var (salt, hash) = PinHasher.HashPin(pin);
    
    var user = new AppUser
    {
        Name = name,
        Roles = roles,
        PinSalt = salt,
        PinHash = hash
    };
    
    _db.Users.Add(user);
    await _db.SaveChangesAsync();
    
    return user;
}
```

### **Get User by ID:**
```csharp
public async Task<AppUser?> GetUserByIdAsync(int id)
{
    return await _db.Users.FindAsync(id);
}
```

### **Get User by Name:**
```csharp
public async Task<AppUser?> GetUserByNameAsync(string name)
{
    return await _db.Users
        .FirstOrDefaultAsync(u => u.Name == name);
}
```

### **Get All Users:**
```csharp
public async Task<List<AppUser>> GetAllUsersAsync()
{
    return await _db.Users
        .OrderBy(u => u.Name)
        .ToListAsync();
}
```

### **Update User:**
```csharp
public async Task<bool> UpdateUserAsync(int userId, string newName)
{
    var user = await _db.Users.FindAsync(userId);
    if (user == null) return false;
    
    user.Name = newName;
    await _db.SaveChangesAsync();
    
    return true;
}
```

### **Delete User:**
```csharp
public async Task<bool> DeleteUserAsync(int userId)
{
    var user = await _db.Users.FindAsync(userId);
    if (user == null) return false;
    
    _db.Users.Remove(user);
    await _db.SaveChangesAsync();
    
    return true;
}
```

### **Verify User PIN:**
```csharp
public async Task<bool> VerifyUserPinAsync(string userName, string pin)
{
    var user = await _db.Users
        .FirstOrDefaultAsync(u => u.Name == userName);
    
    if (user == null) return false;
    
    return PinHasher.VerifyPin(pin, user.PinSalt, user.PinHash);
}
```

## Group Operations

### **Create Group:**
```csharp
public async Task<Group> CreateGroupAsync(string name, string description, int ownerUserId)
{
    var group = new Group
    {
        Name = name,
        Description = description,
        OwnerUserId = ownerUserId,
        CreatedAtUtc = DateTime.UtcNow
    };
    
    _db.Groups.Add(group);
    await _db.SaveChangesAsync();
    
    return group;
}
```

### **Get Group by ID:**
```csharp
public async Task<Group?> GetGroupByIdAsync(int id)
{
    return await _db.Groups
        .Include(g => g.Owner)
        .Include(g => g.Members)
        .ThenInclude(m => m.User)
        .FirstOrDefaultAsync(g => g.Id == id);
}
```

### **Get Groups by Owner:**
```csharp
public async Task<List<Group>> GetGroupsByOwnerAsync(int ownerUserId)
{
    return await _db.Groups
        .Where(g => g.OwnerUserId == ownerUserId)
        .Include(g => g.Members)
        .ThenInclude(m => m.User)
        .OrderBy(g => g.Name)
        .ToListAsync();
}
```

### **Get All Groups:**
```csharp
public async Task<List<Group>> GetAllGroupsAsync()
{
    return await _db.Groups
        .Include(g => g.Owner)
        .Include(g => g.Members)
        .ThenInclude(m => m.User)
        .OrderBy(g => g.Name)
        .ToListAsync();
}
```

### **Update Group:**
```csharp
public async Task<bool> UpdateGroupAsync(int groupId, string newName, string newDescription)
{
    var group = await _db.Groups.FindAsync(groupId);
    if (group == null) return false;
    
    group.Name = newName;
    group.Description = newDescription;
    await _db.SaveChangesAsync();
    
    return true;
}
```

### **Delete Group:**
```csharp
public async Task<bool> DeleteGroupAsync(int groupId)
{
    var group = await _db.Groups.FindAsync(groupId);
    if (group == null) return false;
    
    _db.Groups.Remove(group);
    await _db.SaveChangesAsync();
    
    return true;
}
```

## Group Membership Operations

### **Add Member to Group:**
```csharp
public async Task<bool> AddMemberToGroupAsync(int groupId, int userId)
{
    // Check if already a member
    var existing = await _db.GroupMembers
        .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
    
    if (existing != null) return false; // Already a member
    
    var membership = new GroupMember
    {
        GroupId = groupId,
        UserId = userId,
        JoinedAtUtc = DateTime.UtcNow
    };
    
    _db.GroupMembers.Add(membership);
    await _db.SaveChangesAsync();
    
    return true;
}
```

### **Remove Member from Group:**
```csharp
public async Task<bool> RemoveMemberFromGroupAsync(int groupId, int userId)
{
    var membership = await _db.GroupMembers
        .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
    
    if (membership == null) return false;
    
    _db.GroupMembers.Remove(membership);
    await _db.SaveChangesAsync();
    
    return true;
}
```

### **Get User Groups:**
```csharp
public async Task<List<Group>> GetUserGroupsAsync(int userId)
{
    return await _db.GroupMembers
        .Where(gm => gm.UserId == userId)
        .Select(gm => gm.Group)
        .Include(g => g.Owner)
        .Include(g => g.Members)
        .ThenInclude(m => m.User)
        .OrderBy(g => g.Name)
        .ToListAsync();
}
```

### **Get Group Members:**
```csharp
public async Task<List<AppUser>> GetGroupMembersAsync(int groupId)
{
    return await _db.GroupMembers
        .Where(gm => gm.GroupId == groupId)
        .Select(gm => gm.User)
        .OrderBy(u => u.Name)
        .ToListAsync();
}
```

### **Check if User is Group Member:**
```csharp
public async Task<bool> IsUserInGroupAsync(int userId, int groupId)
{
    return await _db.GroupMembers
        .AnyAsync(gm => gm.UserId == userId && gm.GroupId == groupId);
}
```

### **Check if User is Group Owner:**
```csharp
public async Task<bool> IsUserGroupOwnerAsync(int userId, int groupId)
{
    return await _db.Groups
        .AnyAsync(g => g.Id == groupId && g.OwnerUserId == userId);
}
```

## Settings Operations

### **Get Settings:**
```csharp
public async Task<AppSettings?> GetSettingsAsync()
{
    return await _db.Settings
        .FirstOrDefaultAsync();
}
```

### **Update Settings:**
```csharp
public async Task<bool> UpdateSettingsAsync(AppSettings settings)
{
    var existing = await _db.Settings.FirstOrDefaultAsync();
    if (existing == null)
    {
        _db.Settings.Add(settings);
    }
    else
    {
        existing.MaxMoveTimeoutMins = settings.MaxTimeoutMins;
        existing.ReaperPeriodSeconds = settings.ReaperPeriodSeconds;
        existing.LastMoveHighlightColor = settings.LastMoveHighlightColor;
        existing.EntrapmentMode = settings.EntrapmentMode;
        existing.MultiJumpGraceSeconds = settings.MultiJumpGraceSeconds;
        existing.GameInitiatorGoesFirst = settings.GameInitiatorGoesFirst;
    }
    
    await _db.SaveChangesAsync();
    return true;
}
```

## Advanced Query Examples

### **Complex Join - Users with Group Info:**
```csharp
public async Task<List<object>> GetUsersWithGroupInfoAsync()
{
    return await _db.Users
        .Select(u => new
        {
            User = u,
            GroupCount = _db.GroupMembers.Count(gm => gm.UserId == u.Id),
            OwnedGroups = _db.Groups.Where(g => g.OwnerUserId == u.Id).ToList(),
            IsAdmin = u.Roles.Contains("Admin")
        })
        .OrderBy(u => u.User.Name)
        .ToListAsync();
}
```

### **Group Statistics:**
```csharp
public async Task<List<object>> GetGroupStatisticsAsync()
{
    return await _db.Groups
        .Select(g => new
        {
            Group = g,
            MemberCount = _db.GroupMembers.Count(gm => gm.GroupId == g.Id),
            OwnerName = _db.Users.Where(u => u.Id == g.OwnerUserId).Select(u => u.Name).FirstOrDefault(),
            HasMembers = _db.GroupMembers.Any(gm => gm.GroupId == g.Id)
        })
        .OrderBy(g => g.Group.Name)
        .ToListAsync();
}
```

### **Search Users by Name:**
```csharp
public async Task<List<AppUser>> SearchUsersAsync(string searchTerm)
{
    return await _db.Users
        .Where(u => u.Name.Contains(searchTerm))
        .OrderBy(u => u.Name)
        .ToListAsync();
}
```

### **Get Groups with Member Count:**
```csharp
public async Task<List<object>> GetGroupsMemberCountAsync()
{
    return await _db.Groups
        .Select(g => new
        {
            g.Id,
            g.Name,
            g.Description,
            MemberCount = _db.GroupMembers.Count(gm => gm.GroupId == g.Id),
            OwnerName = _db.Users.Where(u => u.Id == g.OwnerUserId).Select(u => u.Name).FirstOrDefault()
        })
        .OrderBy(g => g.Name)
        .ToListAsync();
}
```

## Raw SQL Operations

### **Execute Raw SQL:**
```csharp
public async Task<List<AppUser>> GetUsersWithRawSqlAsync()
{
    return await _db.Users
        .FromSqlRaw("SELECT * FROM Users WHERE Roles LIKE '%Admin%'")
        .ToListAsync();
}
```

### **Execute Non-Query SQL:**
```csharp
public async Task<int> DeleteInactiveUsersAsync()
{
    return await _db.Database
        .ExecuteSqlRawAsync("DELETE FROM Users WHERE Name LIKE 'Test%'");
}
```

### **Execute Scalar Query:**
```csharp
public async Task<int> GetUserCountAsync()
{
    return await _db.Users
        .CountAsync();
}
```

## Transaction Operations

### **Transaction Example:**
```csharp
public async Task<bool> TransferGroupOwnershipAsync(int groupId, int newOwnerId)
{
    using var transaction = await _db.Database.BeginTransactionAsync();
    try
    {
        var group = await _db.Groups.FindAsync(groupId);
        if (group == null) return false;
        
        // Verify new owner exists
        var newOwner = await _db.Users.FindAsync(newOwnerId);
        if (newOwner == null) return false;
        
        // Transfer ownership
        group.OwnerUserId = newOwnerId;
        
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        
        return true;
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

## Error Handling

### **Safe Database Operations:**
```csharp
public async Task<OperationResult> SafeCreateUserAsync(string name, string roles, string pin)
{
    try
    {
        // Check if user already exists
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Name == name);
        if (existing != null)
        {
            return OperationResult.Failure("User already exists");
        }
        
        var (salt, hash) = PinHasher.HashPin(pin);
        
        var user = new AppUser
        {
            Name = name,
            Roles = roles,
            PinSalt = salt,
            PinHash = hash
        };
        
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        
        return OperationResult.Success($"User '{name}' created successfully");
    }
    catch (Exception ex)
    {
        return OperationResult.Failure($"Error creating user: {ex.Message}");
    }
}
```

## Performance Tips

### **Efficient Queries:**
```csharp
// GOOD: Use specific projections
public async Task<List<string>> GetUserNamesAsync()
{
    return await _db.Users
        .Select(u => u.Name)
        .OrderBy(name => name)
        .ToListAsync();
}

// AVOID: Loading unnecessary data
public async Task<List<string>> GetUserNamesBadAsync()
{
    var users = await _db.Users.ToListAsync(); // Loads all user data
    return users.Select(u => u.Name).ToList();  // Then filters in memory
}
```

### **Batch Operations:**
```csharp
public async Task UpdateUserRolesAsync(Dictionary<int, string> roleUpdates)
{
    var userIds = roleUpdates.Keys.ToList();
    var users = await _db.Users
        .Where(u => userIds.Contains(u.Id))
        .ToListAsync();
    
    foreach (var user in users)
    {
        user.Roles = roleUpdates[user.Id];
    }
    
    await _db.SaveChangesAsync();
}
```

## Testing Examples

### **Mock DbContext for Testing:**
```csharp
public class UserServiceTests
{
    private readonly Mock<AppDbContext> _mockDb;
    private readonly Mock<DbSet<AppUser>> _mockUsers;
    
    public UserServiceTests()
    {
        _mockDb = new Mock<AppDbContext>();
        _mockUsers = new Mock<DbSet<AppUser>>();
        
        _mockDb.Setup(db => db.Users).Returns(_mockUsers.Object);
    }
    
    [Fact]
    public async Task CreateUser_ShouldAddUser()
    {
        // Arrange
        var service = new UserService(_mockDb.Object);
        
        // Act
        var result = await service.CreateUserAsync("TestUser", "Player", "1234");
        
        // Assert
        _mockUsers.Verify(m => m.Add(It.IsAny<AppUser>()), Times.Once);
        _mockDb.Verify(m => m.SaveChangesAsync(), Times.Once);
    }
}
```

## Status: ✅ COMPLETE

These ORM examples provide comprehensive coverage of Entity Framework Core operations for the Drafts application database, including basic CRUD operations, complex queries, transactions, and best practices.
