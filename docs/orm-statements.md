# Entity Framework Core ORM Statements

## User Entity Operations

```csharp
// Create User
var user = new AppUser { Name = "Alice", Roles = "Player", PinSalt = salt, PinHash = hash };
_db.Users.Add(user);
await _db.SaveChangesAsync();

// Get User by ID
var user = await _db.Users.FindAsync(1);

// Get User by Name
var user = await _db.Users.FirstOrDefaultAsync(u => u.Name == "Alice");

// Get All Users
var users = await _db.Users.OrderBy(u => u.Name).ToListAsync();

// Update User
user.Name = "Alice Updated";
await _db.SaveChangesAsync();

// Delete User
_db.Users.Remove(user);
await _db.SaveChangesAsync();
```

## Group Entity Operations

```csharp
// Create Group
var group = new Group 
{ 
    Name = "Test Group", 
    Description = "Test Description", 
    OwnerUserId = 1,
    CreatedAtUtc = DateTime.UtcNow 
};
_db.Groups.Add(group);
await _db.SaveChangesAsync();

// Get Group with Owner
var group = await _db.Groups
    .Include(g => g.Owner)
    .FirstOrDefaultAsync(g => g.Id == 1);

// Get Groups by Owner
var groups = await _db.Groups
    .Where(g => g.OwnerUserId == 1)
    .Include(g => g.Owner)
    .Include(g => g.Members)
    .ThenInclude(m => m.User)
    .ToListAsync();

// Update Group
group.Name = "Updated Group";
await _db.SaveChangesAsync();

// Delete Group
_db.Groups.Remove(group);
await _db.SaveChangesAsync();
```

## GroupMember Entity Operations

```csharp
// Add Member to Group
var member = new GroupMember 
{ 
    GroupId = 1, 
    UserId = 2, 
    JoinedAtUtc = DateTime.UtcNow 
};
_db.GroupMembers.Add(member);
await _db.SaveChangesAsync();

// Get User's Groups
var userGroups = await _db.GroupMembers
    .Where(gm => gm.UserId == 2)
    .Select(gm => gm.Group)
    .Include(g => g.Owner)
    .ToListAsync();

// Get Group's Members
var groupMembers = await _db.GroupMembers
    .Where(gm => gm.GroupId == 1)
    .Select(gm => gm.User)
    .ToListAsync();

// Check Membership
var isMember = await _db.GroupMembers
    .AnyAsync(gm => gm.GroupId == 1 && gm.UserId == 2);

// Remove Member
_db.GroupMembers.Remove(member);
await _db.SaveChangesAsync();
```

## Settings Entity Operations

```csharp
// Get Settings
var settings = await _db.Settings.FirstOrDefaultAsync();

// Update Settings
settings.MaxMoveTimeoutMins = 45;
settings.EntrapmentMode = false;
await _db.SaveChangesAsync();

// Create Settings (if not exists)
var newSettings = new AppSettings 
{ 
    Id = 1,
    MaxMoveTimeoutMins = 30,
    ReaperPeriodSeconds = 30,
    LastMoveHighlightColor = "rgba(255,0,0,0.85)",
    EntrapmentMode = true,
    MultiJumpGraceSeconds = 1.5,
    GameInitiatorGoesFirst = true
};
_db.Settings.Add(newSettings);
await _db.SaveChangesAsync();
```

## Relationship Queries

```csharp
// User with their Groups
var userWithGroups = await _db.Users
    .Where(u => u.Id == 1)
    .Select(u => new 
    {
        User = u,
        Groups = _db.GroupMembers
            .Where(gm => gm.UserId == u.Id)
            .Select(gm => gm.Group)
            .ToList()
    })
    .FirstOrDefaultAsync();

// Group with Owner and Members
var groupWithDetails = await _db.Groups
    .Where(g => g.Id == 1)
    .Select(g => new 
    {
        Group = g,
        Owner = _db.Users.FirstOrDefault(u => u.Id == g.OwnerUserId),
        Members = _db.GroupMembers
            .Where(gm => gm.GroupId == g.Id)
            .Select(gm => gm.User)
            .ToList()
    })
    .FirstOrDefaultAsync();

// All Groups with Member Counts
var groupsWithCounts = await _db.Groups
    .Select(g => new 
    {
        g.Id,
        g.Name,
        g.Description,
        MemberCount = _db.GroupMembers.Count(gm => gm.GroupId == g.Id),
        OwnerName = _db.Users.Where(u => u.Id == g.OwnerUserId).Select(u => u.Name).FirstOrDefault()
    })
    .ToListAsync();

// Users with Group Ownership
var usersWithOwnership = await _db.Users
    .Select(u => new 
    {
        u.Id,
        u.Name,
        u.Roles,
        OwnedGroups = _db.Groups.Where(g => g.OwnerUserId == u.Id).ToList(),
        MemberGroups = _db.GroupMembers
            .Where(gm => gm.UserId == u.Id)
            .Select(gm => gm.Group)
            .ToList()
    })
    .ToListAsync();
```

## Filter and Search Operations

```csharp
// Users by Role
var adminUsers = await _db.Users
    .Where(u => u.Roles.Contains("Admin"))
    .ToListAsync();

// Groups by Name
var searchGroups = await _db.Groups
    .Where(g => g.Name.Contains("Test"))
    .ToListAsync();

// Users not in any Group
var usersWithoutGroups = await _db.Users
    .Where(u => !_db.GroupMembers.Any(gm => gm.UserId == u.Id))
    .ToListAsync();

// Groups with specific Member
var groupsWithUser = await _db.Groups
    .Where(g => _db.GroupMembers.Any(gm => gm.GroupId == g.Id && gm.UserId == 1))
    .ToListAsync();

// Groups owned by specific User
var ownedGroups = await _db.Groups
    .Where(g => g.OwnerUserId == 1)
    .ToListAsync();
```

## Aggregate Operations

```csharp
// Count Users
var userCount = await _db.Users.CountAsync();

// Count Groups
var groupCount = await _db.Groups.CountAsync();

// Count Members in Group
var memberCount = await _db.GroupMembers
    .Where(gm => gm.GroupId == 1)
    .CountAsync();

// Groups with Member Count
var groupStats = await _db.Groups
    .Select(g => new 
    {
        g.Name,
        MemberCount = _db.GroupMembers.Count(gm => gm.GroupId == g.Id)
    })
    .ToListAsync();

// Users by Group Count
var userStats = await _db.Users
    .Select(u => new 
    {
        u.Name,
        GroupCount = _db.GroupMembers.Count(gm => gm.UserId == u.Id),
        OwnedGroupCount = _db.Groups.Count(g => g.OwnerUserId == u.Id)
    })
    .ToListAsync();
```

## Transaction Operations

```csharp
using var transaction = await _db.Database.BeginTransactionAsync();
try
{
    // Create User
    var user = new AppUser { Name = "New User", Roles = "Player", PinSalt = salt, PinHash = hash };
    _db.Users.Add(user);
    await _db.SaveChangesAsync();

    // Create Group owned by new User
    var group = new Group 
    { 
        Name = "New Group", 
        Description = "Owned by new user", 
        OwnerUserId = user.Id,
        CreatedAtUtc = DateTime.UtcNow 
    };
    _db.Groups.Add(group);
    await _db.SaveChangesAsync();

    // Add User as Member
    var member = new GroupMember 
    { 
        GroupId = group.Id, 
        UserId = user.Id, 
        JoinedAtUtc = DateTime.UtcNow 
    };
    _db.GroupMembers.Add(member);
    await _db.SaveChangesAsync();

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

## Raw SQL Operations

```csharp
// Raw SQL Query
var users = await _db.Users
    .FromSqlRaw("SELECT * FROM Users WHERE Name LIKE '%Admin%'")
    .ToListAsync();

// Execute Raw SQL Command
var rowsAffected = await _db.Database
    .ExecuteSqlRawAsync("UPDATE Settings SET MaxMoveTimeoutMins = 45");

// Scalar Query
var count = await _db.Database
    .SqlQueryRaw<int>("SELECT COUNT(*) FROM Users")
    .FirstOrDefaultAsync();
```

## Bulk Operations

```csharp
// Bulk Update User Roles
var usersToUpdate = await _db.Users
    .Where(u => u.Roles == "Player")
    .ToListAsync();

foreach (var user in usersToUpdate)
{
    user.Roles = "Player,Member";
}
await _db.SaveChangesAsync();

// Bulk Delete Test Users
var testUsers = await _db.Users
    .Where(u => u.Name.StartsWith("Test"))
    .ToListAsync();

_db.Users.RemoveRange(testUsers);
await _db.SaveChangesAsync();
```

## Validation Operations

```csharp
// Check if User Exists
var userExists = await _db.Users
    .AnyAsync(u => u.Name == "Alice");

// Check if Group Name is Available
var nameAvailable = !await _db.Groups
    .AnyAsync(g => g.Name == "New Group");

// Verify User can Join Group
var canJoin = await _db.Groups
    .Where(g => g.Id == 1)
    .AnyAsync(g => !_db.GroupMembers.Any(gm => gm.GroupId == g.Id && gm.UserId == 2));

// Check if User is Group Owner
var isOwner = await _db.Groups
    .AnyAsync(g => g.Id == 1 && g.OwnerUserId == 2);
```
