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

        System.Diagnostics.Debug.WriteLine($"Login attempt: Name='{name}', PIN='{pin}'");

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Name.ToUpper() == lookup);
        if (user is null) 
        {
            System.Diagnostics.Debug.WriteLine($"Login debug: User '{name}' not found");
            return null;
        }

        var pinValid = PinHasher.VerifyPin(pin, user.PinSalt, user.PinHash);
        System.Diagnostics.Debug.WriteLine($"Login debug: User '{name}' (ID: {user.Id}) found, PIN valid: {pinValid}");
        
        if (!pinValid) 
        {
            // Debug: Test if PIN format is valid
            var pinFormatValid = IsValidPin(pin);
            System.Diagnostics.Debug.WriteLine($"Login debug: PIN format valid: {pinFormatValid} (length: {pin?.Length})");
            return null;
        }
        return user;
    }

    public Task<List<AppUser>> ListUsersAsync()
        => _db.Users.OrderBy(x => x.Name).ToListAsync();

    public async Task<List<AppUser>> ListAdminUsersAsync()
    {
        return await _db.Users
            .Where(x => x.Roles != null && x.Roles.Split(',').Contains("Admin"))
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

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

    public async Task<string?> GetAdminDesktopFallbackTtsVoiceAsync(int userId)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        return user?.AdminDesktopFallbackTtsVoice;
    }

    public async Task<bool> UpdateAdminDesktopFallbackTtsVoiceAsync(int userId, string? preferredVoice)
    {
        preferredVoice = (preferredVoice ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(preferredVoice)) preferredVoice = null;
        if (preferredVoice is not null && preferredVoice.Length > 1024) return false;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        user.AdminDesktopFallbackTtsVoice = preferredVoice;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> GetAdminDesktopFallbackTtsLanguageAsync(int userId)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        return user?.AdminDesktopFallbackTtsLanguage;
    }

    public async Task<bool> UpdateAdminDesktopFallbackTtsLanguageAsync(int userId, string? language)
    {
        language = (language ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(language)) language = null;
        if (language is not null && language.Length > 16) return false;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        user.AdminDesktopFallbackTtsLanguage = language;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> GetAdminDesktopFallbackTtsRegionAsync(int userId)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        return user?.AdminDesktopFallbackTtsRegion;
    }

    public async Task<bool> UpdateAdminDesktopFallbackTtsRegionAsync(int userId, string? region)
    {
        region = (region ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(region)) region = null;
        if (region is not null && region.Length > 16) return false;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        user.AdminDesktopFallbackTtsRegion = region;
        await _db.SaveChangesAsync();
        return true;
    }

    // Group management methods
    public async Task<List<Group>> ListGroupsAsync()
        => await _db.Groups.Include(g => g.OwnerUser).Include(g => g.Members).ThenInclude(m => m.User).OrderBy(g => g.Name).ToListAsync();

    public async Task<List<AppUser>> ListPlayersAsync()
    {
        var users = await _db.Users.Where(u => !string.IsNullOrWhiteSpace(u.Roles)).ToListAsync();
        return users.Where(u => u.Roles.Contains("Player", StringComparison.OrdinalIgnoreCase) && !u.Roles.Contains("Admin", StringComparison.OrdinalIgnoreCase)).OrderBy(u => u.Name).ToList();
    }

    public async Task<List<Group>> GetUserGroupsAsync(int userId)
        => await _db.Groups.Include(g => g.OwnerUser).Include(g => g.Members).Where(g => g.Members.Any(m => m.UserId == userId)).OrderBy(g => g.Name).ToListAsync();

    public async Task<Group?> CreateGroupAsync(string name, string? description, int ownerUserId)
    {
        // Check if group name already exists
        var existing = await _db.Groups.FirstOrDefaultAsync(g => g.Name == name);
        if (existing != null) return null;

        var group = new Group
        {
            Name = name,
            Description = description,
            OwnerUserId = ownerUserId
        };

        _db.Groups.Add(group);
        await _db.SaveChangesAsync();

        // Add owner as a member
        await AddGroupMemberAsync(group.Id, ownerUserId);

        return group;
    }

    public async Task<bool> DeleteGroupAsync(int groupId, int adminUserId, string adminPin)
    {
        var admin = await _db.Users.SingleOrDefaultAsync(x => x.Id == adminUserId);
        if (admin is null || !HasRole(admin, "Admin")) return false;
        if (!PinHasher.VerifyPin(adminPin, admin.PinSalt, admin.PinHash)) return false;

        var group = await _db.Groups.FindAsync(groupId);
        if (group == null) return false;

        // Remove all members first
        var members = await _db.GroupMembers.Where(m => m.GroupId == groupId).ToListAsync();
        _db.GroupMembers.RemoveRange(members);

        // Delete the group
        _db.Groups.Remove(group);
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<List<GroupMember>> GetGroupMembersAsync(int groupId)
        => await _db.GroupMembers.Include(m => m.User).Where(m => m.GroupId == groupId).OrderBy(m => m.User != null ? m.User.Name : "").ToListAsync();

    public async Task<bool> AddGroupMemberAsync(int groupId, int userId)
    {
        // Check if already a member
        var existing = await _db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (existing != null) return false;

        var member = new GroupMember
        {
            GroupId = groupId,
            UserId = userId
        };

        _db.GroupMembers.Add(member);
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RemoveGroupMemberAsync(int groupId, int userId)
    {
        var member = await _db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (member == null) return false;

        _db.GroupMembers.Remove(member);
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ChangePinAsync(int userId, string currentPin, string newPin)
    {
        currentPin = (currentPin ?? string.Empty).Trim();
        newPin = (newPin ?? string.Empty).Trim();

        if (!IsValidPin(newPin)) 
        {
            System.Diagnostics.Debug.WriteLine($"PIN change debug: Invalid new PIN format: '{newPin}'");
            return false;
        }

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) 
        {
            System.Diagnostics.Debug.WriteLine($"PIN change debug: User {userId} not found");
            return false;
        }

        System.Diagnostics.Debug.WriteLine($"PIN change debug: Changing PIN for user '{user.Name}' (ID: {user.Id})");

        // Debug: Verify current PIN
        var currentPinValid = PinHasher.VerifyPin(currentPin, user.PinSalt, user.PinHash);
        if (!currentPinValid) 
        {
            System.Diagnostics.Debug.WriteLine($"PIN change debug: Current PIN verification failed for user '{user.Name}'");
            return false;
        }

        // Debug: Hash new PIN
        var (salt, hash) = PinHasher.HashPin(newPin);
        
        // Debug: Verify new PIN hash immediately
        var newPinValidAfterHash = PinHasher.VerifyPin(newPin, salt, hash);
        if (!newPinValidAfterHash)
        {
            // This should never happen, but let's catch it
            System.Diagnostics.Debug.WriteLine("ERROR: New PIN hash verification failed immediately after hashing!");
            return false;
        }

        try
        {
            user.PinSalt = salt;
            user.PinHash = hash;
            var changes = await _db.SaveChangesAsync();
            
            System.Diagnostics.Debug.WriteLine($"PIN change debug: Database save completed. Changes saved: {changes}");
            
            // Debug: Verify saved PIN can be validated
            var savedUser = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
            if (savedUser != null)
            {
                var savedPinValid = PinHasher.VerifyPin(newPin, savedUser.PinSalt, savedUser.PinHash);
                System.Diagnostics.Debug.WriteLine($"PIN change debug: Current PIN valid: {currentPinValid}, New PIN valid after save: {savedPinValid}");
                
                if (!savedPinValid)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: PIN verification failed after database save - this indicates a database issue");
                    return false;
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PIN change debug: Database save failed: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"PIN change error for user '{user.Name}': {ex.GetType().Name}: {ex.Message}");
            return false;
        }
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
        => (pin.Length == 4 || pin.Length == 6) && pin.All(char.IsDigit);

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
        var principal = new ClaimsPrincipal(identity);
        
        // Debug logging for Azure
        Console.WriteLine($"BuildPrincipal debug: Building principal for user '{user.Name}' (ID: {user.Id})");
        Console.WriteLine($"BuildPrincipal debug: Claims created:");
        foreach (var claim in claims)
        {
            Console.WriteLine($"  - {claim.Type}: {claim.Value}");
        }
        Console.WriteLine($"BuildPrincipal debug: Authentication type: {identity.AuthenticationType}");
        Console.WriteLine($"BuildPrincipal debug: IsAuthenticated: {principal.Identity?.IsAuthenticated}");
        Console.WriteLine($"BuildPrincipal debug: Principal name: {principal.Identity?.Name}");
        
        return principal;
    }

    public async Task<AppUser?> CreateUserAsync(string name, string pin, bool isAdmin, bool isPlayer)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (!IsValidPin(pin)) return null;

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
