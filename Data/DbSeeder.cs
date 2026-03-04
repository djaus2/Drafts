using Drafts.Services;
using Microsoft.EntityFrameworkCore;

namespace Drafts.Data;

public static class DbSeeder
{
    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

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

        var penny = await db.Users.SingleOrDefaultAsync(x => x.Name == "Penny");

        await db.SaveChangesAsync();
    }
}
