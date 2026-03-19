using Drafts.Components;
using Drafts.Data;
using Drafts.Services;
using Drafts.Hubs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace Drafts
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddSignalR(options =>
            {
                options.MaximumReceiveMessageSize = 1024 * 1024;
            });

            builder.Services.AddHttpContextAccessor();

            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                // Always use local SQLite database in development
                options.UseSqlite("Data Source=auth.db");
            });

            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<AdminVoiceSettingsService>();

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/login";
                    options.LogoutPath = "/logout";
                    options.AccessDeniedPath = "/login";
                });

            builder.Services.AddAuthorization();

            // Register the game service so multiple components can join the same game.
            builder.Services.AddSingleton<DraftsService>();

            builder.Services.AddSingleton<LobbyChatService>();

            builder.Services.AddSingleton<SettingsService>();
            builder.Services.AddHostedService<GameTimeoutReaper>();

            // Enhanced Voice Chat Services
            builder.Services.AddSingleton<EnhancedVoiceChatService>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Create database if it doesn't exist
                await db.Database.EnsureCreatedAsync();

                // Create tables if they don't exist before querying
                await CreateDatabaseTables(db);

                // Check if database has users, if not, run seeding
                var hasUsers = await db.Users.AnyAsync();
                if (!hasUsers)
                {
                    Console.WriteLine("[Program] Database is empty, running seeding...");
                    await DbSeeder.EnsureSeededAsync(db);
                    Console.WriteLine("[Program] Seeding completed");
                }
                else
                {
                    Console.WriteLine("[Program] Database already has users, skipping seeding");
                }
            }

            using (var scope = app.Services.CreateScope())
            {
                var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
                settings.LoadAsync().GetAwaiter().GetResult();
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.MapGet("/api/admin/download-database", (HttpContext ctx) =>
            {
                Console.WriteLine($"Download request received. User: {ctx.User.Identity?.Name}, Authenticated: {ctx.User.Identity?.IsAuthenticated}, IsAdmin: {ctx.User.IsInRole("Admin")}");

                if (!ctx.User.IsInRole("Admin"))
                {
                    Console.WriteLine("User not in Admin role - forbidding access");
                    return Results.Forbid();
                }

                try
                {
                    var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "auth.db");
                    Console.WriteLine($"Database path: {dbPath}");

                    if (!System.IO.File.Exists(dbPath))
                    {
                        Console.WriteLine("Database file not found");
                        return Results.NotFound("Database file not found");
                    }

                    // Create a temporary copy to avoid file locking issues
                    var tempPath = Path.Combine(Path.GetTempPath(), $"auth_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db");
                    Console.WriteLine($"Creating temporary copy: {tempPath}");

                    try
                    {
                        System.IO.File.Copy(dbPath, tempPath, true);
                        var fileBytes = System.IO.File.ReadAllBytes(tempPath);
                        var fileName = $"auth_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db";
                        Console.WriteLine($"Returning file: {fileName}, size: {fileBytes.Length} bytes");

                        return Results.File(fileBytes, "application/octet-stream", fileName);
                    }
                    finally
                    {
                        // Clean up temporary file
                        if (System.IO.File.Exists(tempPath))
                        {
                            try
                            {
                                System.IO.File.Delete(tempPath);
                                Console.WriteLine($"Cleaned up temporary file: {tempPath}");
                            }
                            catch (Exception cleanupEx)
                            {
                                Console.WriteLine($"Failed to cleanup temporary file: {cleanupEx}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Database download error: {ex}");
                    return Results.Problem($"Error downloading database: {ex.Message}");
                }
            }).RequireAuthorization(builder => builder.RequireRole("Admin"));

            app.MapGet("/debug/admin-check", async (AppDbContext db) =>
            {
                var admin = await db.Users.SingleOrDefaultAsync(x => x.Name == "Admin");
                if (admin == null)
                {
                    return Results.Json(new { adminExists = false, message = "Admin user not found" });
                }
                return Results.Json(new
                {
                    adminExists = true,
                    adminName = admin.Name,
                    adminRoles = admin.Roles,
                    hasPinSalt = admin.PinSalt != null,
                    hasPinHash = admin.PinHash != null
                });
            });

            app.MapGet("/debug/login-test", async (AuthService auth, string name = "Admin", string pin = "1371") =>
            {
                var user = await auth.ValidateLoginAsync(name, pin);
                if (user == null)
                {
                    return Results.Json(new { success = false, message = "Login validation failed" });
                }
                return Results.Json(new
                {
                    success = true,
                    userName = user.Name,
                    userRoles = user.Roles,
                    userId = user.Id
                });
            });

            app.MapGet("/logout", async (HttpContext ctx, DraftsService drafts) =>
            {
                var raw = ctx.User?.FindFirst("uid")?.Value;
                if (int.TryParse(raw, out var uid) && uid > 0)
                {
                    drafts.RemoveGamesForUser(uid);
                }
                await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.Redirect("/login", permanent: false);
            });

            app.MapGet("/debug/auth-test", async (AuthService auth, HttpContext ctx) =>
            {
                try
                {
                    var user = await auth.ValidateLoginAsync("Admin", "1371");
                    if (user == null)
                    {
                        return Results.Json(new { success = false, message = "User validation failed" });
                    }

                    var principal = AuthService.BuildPrincipal(user);
                    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                    return Results.Json(new
                    {
                        success = true,
                        message = "Authentication test successful",
                        userName = user.Name,
                        userRoles = user.Roles,
                        isAuthenticated = ctx.User.Identity?.IsAuthenticated ?? false
                    });
                }
                catch (Exception ex)
                {
                    return Results.Json(new { success = false, message = $"Auth test failed: {ex.Message}" });
                }
            });

            app.MapPost("/auth/login", async (
                HttpContext ctx,
                AuthService auth,
                [FromForm] string name,
                [FromForm] string pin,
                [FromForm] string? returnUrl) =>
            {
                try
                {
                    name = (name ?? string.Empty).Trim();
                    pin = (pin ?? string.Empty).Trim();

                    if (string.IsNullOrWhiteSpace(name) || pin.Length != 4 || !pin.All(char.IsDigit))
                    {
                        return Results.Redirect("/login?error=1", permanent: false);
                    }

                    var user = await auth.ValidateLoginAsync(name, pin);
                    if (user is null)
                    {
                        return Results.Redirect("/login?error=1", permanent: false);
                    }

                    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, AuthService.BuildPrincipal(user));

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
                    {
                        return Results.Redirect(returnUrl, permanent: false);
                    }

                    var roles = (user.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var isAdmin = roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
                    return Results.Redirect(isAdmin ? "/admin" : "/player", permanent: false);
                }
                catch (Exception ex)
                {
                    // Log the error for debugging
                    Console.WriteLine($"Login error: {ex}");
                    return Results.Redirect("/login?error=1", permanent: false);
                }
            }).DisableAntiforgery();

            // Map SignalR hubs
            app.MapHub<VoiceChatHub>("/voiceChatHub");

            app.Run();
        }


        private static async Task CreateDatabaseTables(AppDbContext db)
        {
            try
            {
                // Create Users table if needed
                var usersTableExists = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users'").AnyAsync();
                if (!usersTableExists)
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
                    await db.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX ""IX_Users_Name"" ON ""Users"" (""Name"")");
                    Console.WriteLine("[Program] Created Users table");
                }

                // Create Settings table if needed
                var settingsTableExists = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Settings'").AnyAsync();
                if (!settingsTableExists)
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
                    Console.WriteLine("[Program] Created Settings table");
                }

                // Create Groups table if needed
                var groupsTableExists = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Groups'").AnyAsync();
                if (!groupsTableExists)
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
                    Console.WriteLine("[Program] Created Groups table");
                }

                // Create GroupMembers table if needed
                var groupMembersTableExists = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='GroupMembers'").AnyAsync();
                if (!groupMembersTableExists)
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
                    Console.WriteLine("[Program] Created GroupMembers table");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Program] Error creating database tables: {ex.Message}");
                throw;
            }
        }
    }
}
