# Initial Data Seeding Configuration

## Overview
The application now includes comprehensive initial data seeding for testing and development purposes. When a fresh database is created, the system automatically creates users, groups, and memberships.

## Seeded Data Structure

### 🔐 **Users Created**
All users use PIN: **9999**

| Name | Role | PIN | Groups |
|------|------|-----|--------|
| **Admin** | Admin,Player | 9999 | All groups (admin access) |
| **Bob** | Player | 9999 | Bob-Carol (Owner) |
| **Carol** | Player | 9999 | Bob-Carol |
| **Tad** | Player | 9999 | Ted-Alice |
| **Alice** | Player | 9999 | Ted-Alice (Owner) |
| **Fred** | Player | 9999 | **No groups** (for testing isolation) |

### 🏷️ **Groups Created**

#### **Bob-Carol Group**
- **Name:** Bob-Carol
- **Owner:** Bob
- **Members:** Bob (owner), Carol
- **Purpose:** Test basic group functionality

#### **Ted-Alice Group** 
- **Name:** Ted-Alice
- **Owner:** Alice
- **Members:** Alice (owner), Tad
- **Purpose:** Test different owner/member dynamics

### 📊 **Group Membership Matrix**

| User | Bob-Carol | Ted-Alice | Chat Access |
|------|------------|-----------|-------------|
| **Admin** | ✅ (admin) | ✅ (admin) | ✅ (all groups) |
| **Bob** | ✅ (owner) | ❌ | ✅ (Bob-Carol only) |
| **Carol** | ✅ (member) | ❌ | ✅ (Bob-Carol only) |
| **Tad** | ❌ | ✅ (member) | ✅ (Ted-Alice only) |
| **Alice** | ❌ | ✅ (owner) | ✅ (Ted-Alice only) |
| **Fred** | ❌ | ❌ | ❌ (no access) |

## Implementation Details

### **Critical Fix: Save Order**
**Issue:** Initial implementation tried to create groups before users were saved to database, causing "Sequence contains no elements" error.

**Solution:** Save users first, then create groups and memberships:
```csharp
await db.SaveChangesAsync(); // Save users first

// Create groups and memberships after users are saved
await CreateGroupsAndMemberships(db);

await db.SaveChangesAsync(); // Save groups and memberships
```

### **DbSeeder Changes**

#### **User Creation**
```csharp
// Create Admin user
var (salt, hash) = PinHasher.HashPin("9999");
db.Users.Add(new AppUser
{
    Name = "Admin",
    Roles = "Admin,Player",
    PinSalt = salt,
    PinHash = hash
});

// Create Player users
var playerNames = new[] { "Bob", "Carol", "Tad", "Alice", "Fred" };
foreach (var playerName in playerNames)
{
    var (salt, hash) = PinHasher.HashPin("9999");
    db.Users.Add(new AppUser
    {
        Name = playerName,
        Roles = "Player",
        PinSalt = salt,
        PinHash = hash
    });
}
```

#### **Group and Membership Creation**
```csharp
private static async Task CreateGroupsAndMemberships(AppDbContext db)
{
    // Get user IDs
    var bob = await db.Users.SingleAsync(x => x.Name == "Bob");
    var carol = await db.Users.SingleAsync(x => x.Name == "Carol");
    var tad = await db.Users.SingleAsync(x => x.Name == "Tad");
    var alice = await db.Users.SingleAsync(x => x.Name == "Alice");
    var fred = await db.Users.SingleAsync(x => x.Name == "Fred");

    // Create Bob-Carol group with Bob as owner
    var bobCarolGroup = new Group
    {
        Name = "Bob-Carol",
        Description = "Bob and Carol's group",
        OwnerUserId = bob.Id,
        CreatedAtUtc = DateTime.UtcNow
    };
    db.Groups.Add(bobCarolGroup);

    // Add members
    db.GroupMembers.Add(new GroupMember
    {
        GroupId = bobCarolGroup.Id,
        UserId = bob.Id,    // Owner
        JoinedAtUtc = DateTime.UtcNow
    });
    db.GroupMembers.Add(new GroupMember
    {
        GroupId = bobCarolGroup.Id,
        UserId = carol.Id,  // Member
        JoinedAtUtc = DateTime.UtcNow
    });

    // Similar logic for Ted-Alice group...
}
```

## Testing Scenarios Enabled

### **1. Group Chat Isolation Testing**
- **Bob/Carol** can chat with each other in Bob-Carol group
- **Alice/Tad** can chat with each other in Ted-Alice group
- **Cross-group messages** are properly isolated
- **Fred** cannot access any group chat

### **2. Group Ownership Testing**
- **Bob** is owner of Bob-Carol group
- **Alice** is owner of Ted-Alice group
- **Owner permissions** can be tested
- **Member vs Owner** functionality

### **3. Admin Access Testing**
- **Admin** can access all groups
- **Admin broadcasts** reach all group members
- **Admin override** capabilities

### **4. Isolation Testing**
- **Fred** (no groups) tests chat access denial
- **Non-member access** properly blocked
- **Group boundary** enforcement

### **5. Game Creation Testing**
- **Group members** can create games within their groups
- **Cross-group games** properly restricted
- **Non-members** cannot create group games

## Usage Instructions

### **Fresh Database Setup**
1. **Delete existing `auth.db`** (if exists)
2. **Run application** → Database created automatically
3. **All users and groups** created instantly
4. **Ready for testing** with complete data structure

### **Login Credentials**
```
Username: Admin    PIN: 9999
Username: Bob      PIN: 9999
Username: Carol    PIN: 9999
Username: Tad      PIN: 9999
Username: Alice    PIN: 9999
Username: Fred     PIN: 9999
```

### **Testing Workflow**
1. **Login as Fred** → Verify no chat access
2. **Login as Bob** → Verify Bob-Carol chat access
3. **Login as Alice** → Verify Ted-Alice chat access
4. **Login as Admin** → Verify admin broadcast functionality
5. **Test cross-group isolation** → Verify messages don't leak

## Benefits for Development

### **Consistent Testing Environment**
- **Same data structure** every time
- **Predictable user roles** and permissions
- **Reliable group memberships** for testing

### **Comprehensive Coverage**
- **All user types** represented
- **Group ownership** scenarios included
- **Isolation edge cases** covered

### **Easy Reset**
- **Delete database** → Fresh start
- **Same structure** recreated automatically
- **No manual setup** required

## Production Considerations

### **Security Notes**
- **Default PIN 9999** should be changed in production
- **Admin user** should have a strong PIN
- **Consider PIN complexity** requirements

### **Data Persistence**
- **Seeded data** only created on fresh database
- **Existing databases** unaffected
- **User data** preserved across restarts

### **Customization**
- **Modify player names** as needed
- **Adjust group structure** for testing
- **Add more test scenarios** easily

## Files Modified

1. **`Data/DbSeeder.cs`**
   - Added user creation for Admin + 5 players
   - Added `CreateGroupsAndMemberships()` method
   - All users use PIN 9999

## Status: ✅ COMPLETE

The initial data seeding is now configured with comprehensive test users, groups, and memberships. Perfect for development, testing, and demonstration purposes.
