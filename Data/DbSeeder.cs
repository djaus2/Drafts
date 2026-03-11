using Drafts.Services;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Drafts.Data;

public static class DbSeeder
{
    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"Settings\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Settings\" PRIMARY KEY, \"MaxTimeoutMins\" INTEGER NOT NULL, \"ReaperPeriodSeconds\" INTEGER NOT NULL, \"LastMoveHighlightColor\" TEXT NOT NULL DEFAULT 'rgba(255,0,0,0.85)', \"EntrapmentMode\" INTEGER NOT NULL DEFAULT 1, \"MultiJumpGraceSeconds\" REAL NOT NULL DEFAULT 1.5);");

        static async Task<bool> HasColumnAsync(AppDbContext db, string tableName, string columnName)
        {
            try
            {
                var conn = db.Database.GetDbConnection();
                var wasClosed = conn.State == ConnectionState.Closed;
                if (wasClosed)
                {
                    await conn.OpenAsync();
                }

                try
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
                    await using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        // PRAGMA table_info columns: cid, name, type, notnull, dflt_value, pk
                        var nameObj = reader["name"];
                        var name = nameObj?.ToString() ?? string.Empty;
                        if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    return false;
                }
                finally
                {
                    if (wasClosed)
                    {
                        await conn.CloseAsync();
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        try
        {
            if (!await HasColumnAsync(db, "Settings", "ReaperPeriodSeconds"))
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"Settings\" ADD COLUMN \"ReaperPeriodSeconds\" INTEGER NOT NULL DEFAULT 30;");
            }
        }
        catch
        {
        }

        try
        {
            if (!await HasColumnAsync(db, "Settings", "LastMoveHighlightColor"))
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"Settings\" ADD COLUMN \"LastMoveHighlightColor\" TEXT NOT NULL DEFAULT 'rgba(255,0,0,0.85)';");
            }
        }
        catch
        {
        }

        try
        {
            if (!await HasColumnAsync(db, "Settings", "EntrapmentMode"))
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"Settings\" ADD COLUMN \"EntrapmentMode\" INTEGER NOT NULL DEFAULT 1;");
            }
        }
        catch
        {
        }

        try
        {
            if (!await HasColumnAsync(db, "Settings", "MultiJumpGraceSeconds"))
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"Settings\" ADD COLUMN \"MultiJumpGraceSeconds\" REAL NOT NULL DEFAULT 1.5;");
            }
        }
        catch
        {
        }

        // Force add voice preference columns - retry multiple times
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (!await HasColumnAsync(db, "Users", "PreferredTtsVoice"))
                {
                    await db.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE \"Users\" ADD COLUMN \"PreferredTtsVoice\" TEXT NULL;");
                }
                break;
            }
            catch
            {
                await Task.Delay(100);
            }
        }

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (!await HasColumnAsync(db, "Users", "PreferredTtsLanguage"))
                {
                    await db.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE \"Users\" ADD COLUMN \"PreferredTtsLanguage\" TEXT NULL;");
                }
                break;
            }
            catch
            {
                await Task.Delay(100);
            }
        }

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (!await HasColumnAsync(db, "Users", "PreferredTtsRegion"))
                {
                    await db.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE \"Users\" ADD COLUMN \"PreferredTtsRegion\" TEXT NULL;");
                }
                break;
            }
            catch
            {
                await Task.Delay(100);
            }
        }

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
                MultiJumpGraceSeconds = 1.5
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
