using System.Security.Claims;
using Drafts.Data;
using Microsoft.EntityFrameworkCore;

namespace Drafts.Services;

public sealed class AuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AppUser?> ValidateLoginAsync(string name, string pin)
    {
        name = (name ?? string.Empty).Trim();
        var lookup = name.ToUpperInvariant();

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Name.ToUpper() == lookup);
        if (user is null) return null;

        if (!PinHasher.VerifyPin(pin, user.PinSalt, user.PinHash)) return null;
        return user;
    }

    public Task<List<AppUser>> ListUsersAsync()
        => _db.Users.OrderBy(x => x.Name).ToListAsync();

    public Task<AppUser?> GetUserByIdAsync(int userId)
        => _db.Users.SingleOrDefaultAsync(x => x.Id == userId);

    public Task<AppUser?> GetUserByNameAsync(string name)
        => _db.Users.SingleOrDefaultAsync(x => x.Name.ToUpper() == name.ToUpper());

    public async Task<string?> GetPreferredTtsVoiceAsync(int userId)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        return user?.PreferredTtsVoice;
    }

    public async Task<bool> UpdatePreferredTtsVoiceAsync(int userId, string? preferredVoice)
    {
        preferredVoice = (preferredVoice ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(preferredVoice)) preferredVoice = null;
        if (preferredVoice is not null && preferredVoice.Length > 1024) return false;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        user.PreferredTtsVoice = preferredVoice;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> GetPreferredTtsLanguageAsync(int userId)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        return user?.PreferredTtsLanguage;
    }

    public async Task<bool> UpdatePreferredTtsLanguageAsync(int userId, string? language)
    {
        language = (language ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(language)) language = null;
        if (language is not null && language.Length > 16) return false;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        user.PreferredTtsLanguage = language;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> GetPreferredTtsRegionAsync(int userId)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        return user?.PreferredTtsRegion;
    }

    public async Task<bool> UpdatePreferredTtsRegionAsync(int userId, string? region)
    {
        region = (region ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(region)) region = null;
        if (region is not null && region.Length > 16) return false;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        user.PreferredTtsRegion = region;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangePinAsync(int userId, string currentPin, string newPin)
    {
        currentPin = (currentPin ?? string.Empty).Trim();
        newPin = (newPin ?? string.Empty).Trim();

        if (!IsValidPin(newPin)) return false;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        if (!PinHasher.VerifyPin(currentPin, user.PinSalt, user.PinHash)) return false;

        var (salt, hash) = PinHasher.HashPin(newPin);
        user.PinSalt = salt;
        user.PinHash = hash;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetUserPinTo9999Async(int targetUserId, int adminUserId, string adminPin)
    {
        adminPin = (adminPin ?? string.Empty).Trim();
        if (!IsValidPin(adminPin)) return false;

        var admin = await _db.Users.SingleOrDefaultAsync(x => x.Id == adminUserId);
        if (admin is null || !HasRole(admin, "Admin")) return false;
        if (!PinHasher.VerifyPin(adminPin, admin.PinSalt, admin.PinHash)) return false;

        var target = await _db.Users.SingleOrDefaultAsync(x => x.Id == targetUserId);
        if (target is null) return false;
        if (HasRole(target, "Admin") || string.Equals(target.Name, "Admin", StringComparison.Ordinal)) return false;

        var (salt, hash) = PinHasher.HashPin("9999");
        target.PinSalt = salt;
        target.PinHash = hash;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteUserAsync(int targetUserId, int adminUserId, string adminPin)
    {
        adminPin = (adminPin ?? string.Empty).Trim();
        if (!IsValidPin(adminPin)) return false;

        var admin = await _db.Users.SingleOrDefaultAsync(x => x.Id == adminUserId);
        if (admin is null || !HasRole(admin, "Admin")) return false;
        if (!PinHasher.VerifyPin(adminPin, admin.PinSalt, admin.PinHash)) return false;

        var target = await _db.Users.SingleOrDefaultAsync(x => x.Id == targetUserId);
        if (target is null) return false;
        if (HasRole(target, "Admin") || string.Equals(target.Name, "Admin", StringComparison.Ordinal)) return false;

        _db.Users.Remove(target);
        await _db.SaveChangesAsync();
        return true;
    }

    private static bool IsValidPin(string pin)
        => pin.Length == 4 && pin.All(char.IsDigit);

    private static bool HasRole(AppUser user, string role)
    {
        var roles = (user.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    }

    public static ClaimsPrincipal BuildPrincipal(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Name),
            new("uid", user.Id.ToString())
        };

        var roles = (user.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "Cookies");
        return new ClaimsPrincipal(identity);
    }

    public async Task<AppUser?> CreateUserAsync(string name, string pin, bool isAdmin, bool isPlayer)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (pin.Length != 4 || !pin.All(char.IsDigit)) return null;

        name = name.Trim();
        var lookup = name.ToUpperInvariant();

        var exists = await _db.Users.AnyAsync(x => x.Name.ToUpper() == lookup);
        if (exists) return null;

        var roles = new List<string>();
        if (isPlayer) roles.Add("Player");
        if (isAdmin) roles.Add("Admin");

        var (salt, hash) = PinHasher.HashPin(pin);

        var user = new AppUser
        {
            Name = name,
            Roles = string.Join(',', roles),
            PinSalt = salt,
            PinHash = hash
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return user;
    }
}
