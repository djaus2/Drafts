using Drafts.Services;
using Microsoft.EntityFrameworkCore;

namespace Drafts.Data;

public static class DbSeeder
{
    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"Settings\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Settings\" PRIMARY KEY, \"MaxTimeoutMins\" INTEGER NOT NULL, \"ReaperPeriodSeconds\" INTEGER NOT NULL, \"LastMoveHighlightColor\" TEXT NOT NULL DEFAULT 'rgba(255,0,0,0.85)');");

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Settings\" ADD COLUMN \"ReaperPeriodSeconds\" INTEGER NOT NULL DEFAULT 30;");
        }
        catch
        {
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Settings\" ADD COLUMN \"LastMoveHighlightColor\" TEXT NOT NULL DEFAULT 'rgba(255,0,0,0.85)';");
        }
        catch
        {
        }

        var settings = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1);
        if (settings is null)
        {
            db.Settings.Add(new AppSettings
            {
                Id = 1,
                MaxTimeoutMins = 30,
                ReaperPeriodSeconds = 30,
                LastMoveHighlightColor = "rgba(255,0,0,0.85)"
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

        await db.SaveChangesAsync();
    }
}
