using Drafts.Components;
using Drafts.Data;
using Drafts.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace Drafts
{
    public class Program
    {
        public static void Main(string[] args)
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
                var cs = builder.Configuration.GetConnectionString("AuthDb") ?? "Data Source=auth.db";

                // SQLite relative paths are relative to the *process working directory*, which can vary
                // depending on how the app is launched. Resolve to the content root for stability.
                const string dataSourcePrefix = "Data Source=";
                if (cs.StartsWith(dataSourcePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var src = cs.Substring(dataSourcePrefix.Length).Trim().Trim('"');
                    if (!string.IsNullOrWhiteSpace(src)
                        && !Path.IsPathRooted(src)
                        && !src.Contains(";")
                        && !src.Contains("://", StringComparison.OrdinalIgnoreCase))
                    {
                        var abs = Path.Combine(builder.Environment.ContentRootPath, src);
                        cs = dataSourcePrefix + abs;
                    }
                }

                options.UseSqlite(cs);
            });

            builder.Services.AddScoped<AuthService>();

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

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                DbSeeder.EnsureSeededAsync(db).GetAwaiter().GetResult();
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
                return Results.Json(new { 
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
                return Results.Json(new { 
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

                    return Results.Json(new { 
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

            app.Run();
        }
    }
}
