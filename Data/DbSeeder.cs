using Drafts.Services;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Drafts.Data;

public static class DbSeeder
{
    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        // Only seed if database doesn't exist (fresh installation)
        if (await db.Database.EnsureCreatedAsync())
        {

        // Database was just created, so we can create the schema directly
        // No need for migration logic since this is a fresh database

        var settings = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1);
        if (settings is null)
        {
            db.Settings.Add(new AppSettings
            {
                Id = 1,
                MaxTimeoutMins = 30,
                ReaperPeriodSeconds = 30,
                LastMoveHighlightColor = "rgba(255,0,0,0.85)",
                EntrapmentMode = true,
                MultiJumpGraceSeconds = 1.5,
                GameInitiatorGoesFirst = true
            });
        }

        // Create users from auth.json
        await CreateUsersFromAuthJson(db);

        //var penny = await db.Users.SingleOrDefaultAsync(x => x.Name == "Penny");

        // Create Groups table if it doesn't exist
        if (!await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Groups'").AnyAsync())
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE ""Groups"" (
                    ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    ""Name"" TEXT NOT NULL,
                    ""Description"" TEXT NULL,
                    ""OwnerUserId"" INTEGER NOT NULL,
                    ""CreatedAtUtc"" TEXT NOT NULL,
                    FOREIGN KEY (""OwnerUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
                )");
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX \"IX_Groups_Name\" ON \"Groups\" (\"Name\")");
        }

        // Create GroupMembers table if it doesn't exist
        if (!await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='GroupMembers'").AnyAsync())
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE ""GroupMembers"" (
                    ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    ""GroupId"" INTEGER NOT NULL,
                    ""UserId"" INTEGER NOT NULL,
                    ""JoinedAtUtc"" TEXT NOT NULL,
                    FOREIGN KEY (""GroupId"") REFERENCES ""Groups"" (""Id"") ON DELETE CASCADE,
                    FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
                )");
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX \"IX_GroupMembers_GroupId_UserId\" ON \"GroupMembers\" (\"GroupId\", \"UserId\")");
        }

        await db.SaveChangesAsync();

        // Create groups and memberships from auth.json after users are saved
        await CreateGroupsAndMembershipsFromAuthJson(db);

        await db.SaveChangesAsync();
        }
    }

    private static async Task CreateUsersFromAuthJson(AppDbContext db)
    {
        var authJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "auth.json");
        
        if (!File.Exists(authJsonPath))
        {
            Console.WriteLine("Warning: auth.json not found, skipping user creation from JSON");
            return;
        }

        var json = await File.ReadAllTextAsync(authJsonPath);
        var authExport = JsonSerializer.Deserialize<AuthExport>(json);
        
        if (authExport?.Users == null)
        {
            Console.WriteLine("Warning: No users found in auth.json");
            return;
        }

        foreach (var authUser in authExport.Users)
        {
            var existingUser = await db.Users.SingleOrDefaultAsync(x => x.Name == authUser.Name);
            if (existingUser is null)
            {
                var (salt, hash) = PinHasher.HashPin(authUser.Pin);
                db.Users.Add(new AppUser
                {
                    Name = authUser.Name,
                    Roles = authUser.Roles,
                    PinSalt = salt,
                    PinHash = hash
                });
            }
        }
    }

    private static async Task CreateGroupsAndMembershipsFromAuthJson(AppDbContext db)
    {
        var authJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "auth.json");
        
        if (!File.Exists(authJsonPath))
        {
            Console.WriteLine("Warning: auth.json not found, skipping group creation from JSON");
            return;
        }

        var json = await File.ReadAllTextAsync(authJsonPath);
        var authExport = JsonSerializer.Deserialize<AuthExport>(json);
        
        if (authExport?.Groups == null)
        {
            Console.WriteLine("Warning: No groups found in auth.json");
            return;
        }

        // Create groups
        foreach (var authGroup in authExport.Groups)
        {
            var existingGroup = await db.Groups.FirstOrDefaultAsync(x => x.Name == authGroup.Name);
            if (existingGroup is null)
            {
                // Find the owner user by name (lookup outside of EF query)
                var ownerUserAuth = authExport.Users.FirstOrDefault(u => u.Id == authGroup.OwnerId);
                if (ownerUserAuth != null)
                {
                    var ownerUser = await db.Users.SingleOrDefaultAsync(x => x.Name == ownerUserAuth.Name);
                    if (ownerUser != null)
                    {
                        var newGroup = new Group
                        {
                            Name = authGroup.Name,
                            Description = authGroup.Description,
                            OwnerUserId = ownerUser.Id,
                            CreatedAtUtc = DateTime.UtcNow
                        };
                        db.Groups.Add(newGroup);
                        await db.SaveChangesAsync(); // Save to get the group ID

                        // Add members
                        foreach (var memberName in authGroup.Members)
                        {
                            var memberUser = await db.Users.SingleOrDefaultAsync(x => x.Name == memberName);
                            if (memberUser != null)
                            {
                                db.GroupMembers.Add(new GroupMember
                                {
                                    GroupId = newGroup.Id,
                                    UserId = memberUser.Id,
                                    JoinedAtUtc = DateTime.UtcNow
                                });
                            }
                        }
                    }
                }
            }
        }
    }
}
