using Drafts.Services;
using Microsoft.EntityFrameworkCore;
using System.Data;

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

        var admin = await db.Users.SingleOrDefaultAsync(x => x.Name == "Admin");
        if (admin is null)
        {
            var (salt, hash) = PinHasher.HashPin("1371");
            db.Users.Add(new AppUser
            {
                Name = "Admin",
                Roles = "Admin,Player",
                PinSalt = salt,
                PinHash = hash
            });
        }

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
        }
    }
}
