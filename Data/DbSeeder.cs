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
        try
        {
            // Create database if it doesn't exist
            await db.Database.EnsureCreatedAsync();

            // Always create tables if they don't exist
            await CreateUsersTable(db);
            await CreateSettingsTable(db);
            await CreateGroupsTable(db);
            
            // Check if database has any users
            var hasUsers = await db.Users.AnyAsync();
            
            if (!hasUsers)
            {
                Console.WriteLine("[DbSeeder] Database is empty, running full seeding...");
                
                // Create default settings
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
                        GameInitiatorGoesFirst = true,
                        UseDirectAudioVoiceChat = true
                    });
                }

                // Create users from auth.json
                await CreateUsersFromAuthJson(db);

                // Create groups and memberships from auth.json
                await CreateGroupsAndMembershipsFromAuthJson(db);

                await db.SaveChangesAsync();
                Console.WriteLine("[DbSeeder] Seeding completed successfully");
            }
            else
            {
                Console.WriteLine("[DbSeeder] Database already has users, skipping seeding");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbSeeder] Error during seeding: {ex.Message}");
            throw;
        }
    }

    private static async Task CreateUsersTable(AppDbContext db)
    {
        try
        {
            // Check if Users table exists
            var tableExists = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users'").AnyAsync();
            
            if (!tableExists)
            {
                await db.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE ""Users"" (
                        ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        ""Name"" TEXT NOT NULL,
                        ""Roles"" TEXT NOT NULL,
                        ""PinSalt"" BLOB NOT NULL,
                        ""PinHash"" BLOB NOT NULL,
                        ""PreferredTtsVoice"" TEXT NULL,
                        ""PreferredTtsLanguage"" TEXT NULL,
                        ""PreferredTtsRegion"" TEXT NULL,
                        ""VoiceSettings"" TEXT NULL
                    )");
                Console.WriteLine("[DbSeeder] Created Users table");
                
                // Create unique index on Name
                await db.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX ""IX_Users_Name"" ON ""Users"" (""Name"")");
                Console.WriteLine("[DbSeeder] Created Users table indexes");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbSeeder] Error creating Users table: {ex.Message}");
        }
    }

    private static async Task CreateSettingsTable(AppDbContext db)
    {
        try
        {
            // Check if Settings table exists
            var tableExists = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Settings'").AnyAsync();
            
            if (!tableExists)
            {
                await db.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE ""Settings"" (
                        ""Id"" INTEGER NOT NULL PRIMARY KEY,
                        ""MaxTimeoutMins"" INTEGER NOT NULL,
                        ""ReaperPeriodSeconds"" INTEGER NOT NULL,
                        ""LastMoveHighlightColor"" TEXT NOT NULL,
                        ""EntrapmentMode"" INTEGER NOT NULL,
                        ""MultiJumpGraceSeconds"" REAL NOT NULL,
                        ""GameInitiatorGoesFirst"" INTEGER NOT NULL,
                        ""UseDirectAudioVoiceChat"" INTEGER NOT NULL
                    )");
                Console.WriteLine("[DbSeeder] Created Settings table");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbSeeder] Error creating Settings table: {ex.Message}");
        }
    }

    private static async Task CreateGroupsTable(AppDbContext db)
    {
        try
        {
            // Check if Groups table exists
            var tableExists = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Groups'").AnyAsync();
            
            if (!tableExists)
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
                Console.WriteLine("[DbSeeder] Created Groups table");
                
                // Create GroupMembers table
                await db.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE ""GroupMembers"" (
                        ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        ""GroupId"" INTEGER NOT NULL,
                        ""UserId"" INTEGER NOT NULL,
                        ""JoinedAtUtc"" TEXT NOT NULL,
                        FOREIGN KEY (""GroupId"") REFERENCES ""Groups"" (""Id"") ON DELETE CASCADE,
                        FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
                    )");
                Console.WriteLine("[DbSeeder] Created GroupMembers table");
                
                // Create indexes
                await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX ""IX_GroupMembers_GroupId"" ON ""GroupMembers"" (""GroupId"")");
                await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX ""IX_GroupMembers_UserId"" ON ""GroupMembers"" (""UserId"")");
                await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX ""IX_Groups_OwnerUserId"" ON ""Groups"" (""OwnerUserId"")");
                Console.WriteLine("[DbSeeder] Created table indexes");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbSeeder] Error creating Groups tables: {ex.Message}");
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
