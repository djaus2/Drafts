using Drafts.Components;
using Drafts.Data;
using Drafts.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Security.Claims;

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
                        // For Azure App Service, use the temporary directory for better reliability
                        var basePath = builder.Environment.IsProduction() 
                            ? Path.GetTempPath() 
                            : builder.Environment.ContentRootPath;
                        
                        var abs = Path.Combine(basePath, src);
                        cs = dataSourcePrefix + abs;
                        
                        // Log the database path for debugging
                        Console.WriteLine($"Database path: {abs} (Environment: {builder.Environment.EnvironmentName})");
                    }
                }

                options.UseSqlite(cs);
            });

            builder.Services.AddServerSideBlazor()
        .AddHubOptions(options =>
        {
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            options.HandshakeTimeout = TimeSpan.FromSeconds(15);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.EnableDetailedErrors = true;
        });

            builder.Services.AddScoped<AuthService>();

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/login";
                    options.LogoutPath = "/logout";
                    options.AccessDeniedPath = "/login";
                    
                    // Azure-specific cookie settings
                    if (builder.Environment.IsProduction())
                    {
                        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                        options.Cookie.SameSite = SameSiteMode.Lax;
                        options.Cookie.HttpOnly = true;
                    }
                    
                    // Add sliding expiration for better reliability
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;
                    
                    // Add logging for debugging
                    options.Events = new CookieAuthenticationEvents
                    {
                        OnSigningIn = context =>
                        {
                            Console.WriteLine($"Cookie signing in: {context.Principal?.Identity?.Name}");
                            return Task.CompletedTask;
                        },
                        OnSignedIn = context =>
                        {
                            Console.WriteLine($"Cookie signed in: {context.Principal?.Identity?.Name}");
                            return Task.CompletedTask;
                        },
                        OnValidatePrincipal = context =>
                        {
                            Console.WriteLine($"Cookie validating principal: {context.Principal?.Identity?.Name}");
                            return Task.CompletedTask;
                        }
                    };
                });

            builder.Services.AddAuthorization();

            // Register the game service so multiple components can join the same game.
            builder.Services.AddSingleton<DraftsService>();

            builder.Services.AddSingleton<LobbyChatService>();

            builder.Services.AddSingleton<SettingsService>();
            builder.Services.AddSingleton<UsableMsVoiceService>();
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

            using (var scope = app.Services.CreateScope())
            {
                var usableMsVoices = scope.ServiceProvider.GetRequiredService<UsableMsVoiceService>();
                usableMsVoices.LoadAsync().GetAwaiter().GetResult();
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            //app.UseHttpsRedirection();

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

            app.MapGet("/admin/change-pin-test", async (HttpContext ctx, AuthService auth) =>
            {
                try
                {
                    Console.WriteLine("Admin PIN change test: Simple test endpoint");
                    
                    var user = ctx.User;
                    Console.WriteLine($"Admin PIN change test: User null? {user == null}, IsAuthenticated? {user?.Identity?.IsAuthenticated}");
                    Console.WriteLine($"Admin PIN change test: User name: {user?.Identity?.Name}");
                    
                    var raw = user?.FindFirst("uid")?.Value;
                    Console.WriteLine($"Admin PIN change test: UID claim: '{raw}'");
                    
                    var result = $"User null? {user == null}, IsAuthenticated? {user?.Identity?.IsAuthenticated}, Name: {user?.Identity?.Name}, UID: '{raw}'";
                    return Results.Text(result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Admin PIN change test: Exception - {ex.Message}");
                    return Results.Text($"Error: {ex.Message}");
                }
            });

            app.MapGet("/test-endpoint", () => Results.Text("Test endpoint is working"));

            app.MapPost("/admin/change-pin", async (HttpContext ctx, AuthService auth) =>
            {
                try
                {
                    Console.WriteLine("Admin PIN change API: Starting request");
                    
                    // Read form data manually
                    var form = await ctx.Request.ReadFormAsync();
                    var currentPin = form["currentPin"].ToString();
                    var newPin = form["newPin"].ToString();
                    
                    Console.WriteLine($"Admin PIN change API: Current PIN='{currentPin}', New PIN='{newPin}'");
                    
                    // Use the exact same logic as Admin.razor
                    var user = ctx.User;
                    int? userId = null;
                    
                    Console.WriteLine($"Admin PIN change API: User null? {user == null}, IsAuthenticated? {user?.Identity?.IsAuthenticated}");
                    
                    if (user == null || !user.Identity?.IsAuthenticated == true)
                    {
                        Console.WriteLine("Admin PIN change API: User not authenticated");
                        return Results.Json(new { success = false, message = "No player id." });
                    }

                    var raw = user?.FindFirst("uid")?.Value;
                    Console.WriteLine($"Admin PIN change API: UID claim value: '{raw}'");
                    
                    if (int.TryParse(raw, out var id))
                    {
                        userId = id;
                        Console.WriteLine($"Admin PIN change API: UID claim parsed successfully: {userId}");
                    }
                    else
                    {
                        var name = user?.Identity?.Name;
                        Console.WriteLine($"Admin PIN change API: Attempting username fallback for '{name}'");
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            var appUser = await auth.GetUserByNameAsync(name);
                            userId = appUser?.Id;
                            Console.WriteLine($"Admin PIN change API: Username lookup result: {userId}");
                        }
                    }

                    if (!userId.HasValue)
                    {
                        Console.WriteLine("Admin PIN change API: No user ID found");
                        return Results.Json(new { success = false, message = "No player id." });
                    }

                    Console.WriteLine($"Admin PIN change API: About to call ChangePinAsync for user {userId.Value}");
                    var ok = await auth.ChangePinAsync(userId.Value, (currentPin ?? "").Trim(), (newPin ?? "").Trim());
                    Console.WriteLine($"Admin PIN change API: ChangePinAsync returned: {ok}");
                    
                    var message = ok ? "PIN changed." : "Failed (check current PIN and new PIN format).";
                    Console.WriteLine($"Admin PIN change API: Final result: {message}");
                    
                    return Results.Json(new { success = ok, message = message });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Admin PIN change API: Exception - {ex.GetType().Name}: {ex.Message}");
                    Console.WriteLine($"Admin PIN change API: Stack trace: {ex.StackTrace}");
                    return Results.Json(new { success = false, message = $"Error: {ex.Message}" });
                }
            });

            app.MapGet("/debug/admin-pin-test", async (HttpContext ctx, AuthService auth) =>
            {
                try
                {
                    // Replicate the exact Admin.razor OnChangePin logic
                    var result = new System.Text.StringBuilder();
                    result.AppendLine("=== Admin PIN Change Debug ===");
                    
                    // Simulate the PIN values from the form
                    var currentPin = "9999"; // Current PIN
                    var newPin = "1234"; // New PIN to test
                    
                    result.AppendLine($"Starting with currentPin='{currentPin}', newPin='{newPin}'");

                    // Replicate GetCurrentUserIdAsync logic
                    var user = ctx.User;
                    int? userId = null;
                    
                    result.AppendLine($"HttpContext null? {ctx == null}");
                    result.AppendLine($"User null? {user == null}");
                    result.AppendLine($"IsAuthenticated? {user?.Identity?.IsAuthenticated}");
                    result.AppendLine($"Name? {user?.Identity?.Name}");
                    
                    if (user == null || !user.Identity?.IsAuthenticated == true)
                    {
                        result.AppendLine("User not authenticated - would return 'No player id.'");
                        return Results.Text(result.ToString());
                    }

                    var raw = user?.FindFirst("uid")?.Value;
                    result.AppendLine($"UID claim value: '{raw}'");
                    
                    if (int.TryParse(raw, out var id))
                    {
                        userId = id;
                        result.AppendLine($"UID claim success, userId={userId}");
                    }
                    else
                    {
                        var name = user?.Identity?.Name;
                        result.AppendLine($"Attempting username fallback for '{name}'");
                        
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            try
                            {
                                var appUser = await auth.GetUserByNameAsync(name);
                                if (appUser != null)
                                {
                                    userId = appUser.Id;
                                    result.AppendLine($"Username fallback success, userId={userId}");
                                }
                                else
                                {
                                    result.AppendLine("Username lookup failed - user not found");
                                }
                            }
                            catch (Exception ex)
                            {
                                result.AppendLine($"Username lookup error: {ex.Message}");
                            }
                        }
                    }

                    if (!userId.HasValue)
                    {
                        result.AppendLine("No user ID found - would return 'No player id.'");
                        return Results.Text(result.ToString());
                    }

                    result.AppendLine($"User ID found: {userId.Value}");
                    result.AppendLine("About to call Auth.ChangePinAsync");

                    var ok = await auth.ChangePinAsync(userId.Value, currentPin.Trim(), newPin.Trim());
                    
                    result.AppendLine($"ChangePinAsync returned: {ok}");
                    result.AppendLine($"Final message: {(ok ? "PIN changed." : "Failed (check current PIN and new PIN format).")}");
                    
                    return Results.Text(result.ToString());
                }
                catch (Exception ex)
                {
                    return Results.Text($"Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                }
            });

            app.MapPost("/debug/ui-pin-test", async (HttpContext ctx, AuthService auth) =>
            {
                try
                {
                    // Replicate the EXACT logic from Player.razor OnChangePin() method
                    Console.WriteLine($"UI PIN test: Starting - HttpContext={ctx != null}, User={ctx.User != null}");
                    
                    var httpContext = ctx;
                    var user = httpContext?.User;
                    
                    if (user == null || !user.Identity?.IsAuthenticated == true)
                    {
                        Console.WriteLine($"UI PIN test: User not authenticated");
                        return Results.Json(new { status = "User not authenticated", message = "No player id." });
                    }

                    // Use hardcoded values to test
                    var currentPin = "123456"; // Current PIN from successful Test 4
                    var newPin = "9999"; // Change back to 9999 for testing
                    
                    Console.WriteLine($"UI PIN test: Attempting PIN change from '{currentPin}' to '{newPin}'");

                    // Get user ID using the exact same logic as UI components
                    var raw = user?.FindFirst("uid")?.Value;
                    int? userId = null;
                    
                    if (int.TryParse(raw, out var id))
                    {
                        userId = id;
                        Console.WriteLine($"UI PIN test: UID claim success, userId={userId}");
                    }
                    else
                    {
                        var name = user?.Identity?.Name;
                        Console.WriteLine($"UI PIN test: Attempting username fallback for '{name}'");
                        
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            var appUser = await auth.GetUserByNameAsync(name);
                            if (appUser != null)
                            {
                                userId = appUser.Id;
                                Console.WriteLine($"UI PIN test: Username fallback success, userId={userId}");
                            }
                        }
                    }

                    if (!userId.HasValue)
                    {
                        Console.WriteLine($"UI PIN test: No user ID found");
                        return Results.Json(new { status = "User ID lookup failed", message = "No player id." });
                    }

                    Console.WriteLine($"UI PIN test: About to call ChangePinAsync with userId={userId}");
                    
                    // Call the exact same method as UI
                    var success = await auth.ChangePinAsync(userId.Value, currentPin.Trim(), newPin.Trim());
                    
                    Console.WriteLine($"UI PIN test: ChangePinAsync returned {success}");
                    
                    var message = success ? "PIN changed." : "Failed (check current PIN and new PIN format).";
                    
                    return Results.Json(new { 
                        status = success ? "Success" : "Failed",
                        message = message,
                        userId = userId,
                        currentPin = currentPin,
                        newPin = newPin,
                        changePinResult = success
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"UI PIN test: Exception - {ex.GetType().Name}: {ex.Message}");
                    return Results.Json(new { 
                        status = "Exception", 
                        error = ex.Message, 
                        errorType = ex.GetType().Name,
                        stackTrace = ex.StackTrace
                    });
                }
            });

            app.MapGet("/debug/simple-pin-test", async (AuthService auth, HttpContext ctx) =>
            {
                try
                {
                    Console.WriteLine($"Simple PIN test: Starting - HttpContext={ctx != null}, User={ctx.User != null}");
                    Console.WriteLine($"Simple PIN test: User.Identity={ctx.User?.Identity}, IsAuthenticated={ctx.User?.Identity?.IsAuthenticated}");
                    
                    // Get the current user ID using the same logic as the components
                    var user = ctx.User;
                    int? userId = null;
                    
                    var raw = user?.FindFirst("uid")?.Value;
                    Console.WriteLine($"Simple PIN test: UID claim value: '{raw}'");
                    
                    if (int.TryParse(raw, out var id))
                    {
                        userId = id;
                        Console.WriteLine($"Simple PIN test: UID claim success, userId={userId}");
                    }
                    else
                    {
                        var name = user?.Identity?.Name;
                        Console.WriteLine($"Simple PIN test: Attempting username fallback for '{name}'");
                        
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            var appUser = await auth.GetUserByNameAsync(name);
                            userId = appUser?.Id;
                            Console.WriteLine($"Simple PIN test: Username fallback result: {userId}");
                        }
                    }

                    if (!userId.HasValue)
                    {
                        return Results.Json(new { 
                            status = "No user ID found",
                            uidClaim = raw,
                            userName = user?.Identity?.Name,
                            isAuthenticated = user?.Identity?.IsAuthenticated,
                            allClaims = user?.Claims.Select(c => new { type = c.Type, value = c.Value }).ToList()
                        });
                    }

                    // Test with the current PIN (1371) to change to 123456
                    var success = await auth.ChangePinAsync(userId.Value, "1371", "123456");
                    
                    return Results.Json(new { 
                        status = success ? "Success" : "Failed",
                        userId = userId,
                        currentPinTest = "1371",
                        newPinTest = "123456",
                        changePinResult = success
                    });
                }
                catch (Exception ex)
                {
                    return Results.Json(new { 
                        status = "Exception", 
                        error = ex.Message, 
                        errorType = ex.GetType().Name,
                        stackTrace = ex.StackTrace
                    });
                }
            });

            app.MapPost("/debug/test-pin-change", async (HttpContext ctx, AuthService auth, [FromForm] string currentPin, [FromForm] string newPin) =>
            {
                try
                {
                    Console.WriteLine($"PIN change test: Starting with currentPin='{currentPin}', newPin='{newPin}'");
                    
                    // Replicate the exact logic from Player.razor OnChangePin()
                    var httpContext = ctx;
                    var user = httpContext?.User;
                    
                    var initialResult = new
                    {
                        step = "Initial check",
                        httpContextNull = httpContext == null,
                        userNull = user == null,
                        isAuthenticated = user?.Identity?.IsAuthenticated,
                        userName = user?.Identity?.Name
                    };

                    Console.WriteLine($"PIN change test: HttpContext={httpContext != null}, User={user != null}, Authenticated={user?.Identity?.IsAuthenticated}");

                    if (user == null || !user.Identity?.IsAuthenticated == true)
                    {
                        var response1 = new { result = initialResult, status = "User not authenticated", message = "No player id." };
                        Console.WriteLine($"PIN change test: Returning 'User not authenticated'");
                        return Results.Json(response1);
                    }

                    var raw = user?.FindFirst("uid")?.Value;
                    int? userId = null;
                    object stepResult;
                    
                    Console.WriteLine($"PIN change test: UID claim value: '{raw}'");
                    
                    if (int.TryParse(raw, out var id))
                    {
                        userId = id;
                        stepResult = new
                        {
                            step = "UID claim success",
                            uidClaimValue = raw,
                            userId = userId
                        };
                        Console.WriteLine($"PIN change test: UID claim success, userId={userId}");
                    }
                    else
                    {
                        // Fallback: lookup by username
                        var name = user?.Identity?.Name;
                        Console.WriteLine($"PIN change test: Attempting username fallback for '{name}'");
                        
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            try
                            {
                                var appUser = await auth.GetUserByNameAsync(name);
                                if (appUser != null)
                                {
                                    userId = appUser.Id;
                                    stepResult = new
                                    {
                                        step = "Username fallback success",
                                        userName = name,
                                        userId = userId
                                    };
                                    Console.WriteLine($"PIN change test: Username fallback success, userId={userId}");
                                }
                                else
                                {
                                    stepResult = new { step = "Username lookup failed", userName = name };
                                    Console.WriteLine($"PIN change test: Username lookup failed - user not found");
                                }
                            }
                            catch (Exception ex)
                            {
                                stepResult = new { step = "Username lookup failed", userName = name, error = ex.Message };
                                Console.WriteLine($"PIN change test: Username lookup exception: {ex.Message}");
                                var response2 = new { result = stepResult, status = "Username lookup failed", error = ex.Message };
                                return Results.Json(response2);
                            }
                        }
                        else
                        {
                            stepResult = new { step = "No username available" };
                            Console.WriteLine($"PIN change test: No username available");
                        }
                    }

                    if (!userId.HasValue)
                    {
                        var response3 = new { result = stepResult, status = "User ID lookup failed", message = "No player id." };
                        Console.WriteLine($"PIN change test: Returning 'User ID lookup failed'");
                        return Results.Json(response3);
                    }

                    Console.WriteLine($"PIN change test: About to call ChangePinAsync with userId={userId}");
                    
                    // Now test the actual PIN change
                    var success = await auth.ChangePinAsync(userId.Value, (currentPin ?? "").Trim(), (newPin ?? "").Trim());
                    
                    Console.WriteLine($"PIN change test: ChangePinAsync returned {success}");
                    
                    var response4 = new { 
                        result = stepResult, 
                        status = success ? "Success" : "PIN change failed",
                        message = success ? "PIN changed." : "Failed (check current PIN and new PIN format)."
                    };
                    
                    Console.WriteLine($"PIN change test: Returning final response");
                    return Results.Json(response4);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PIN change test: Exception - {ex.GetType().Name}: {ex.Message}");
                    Console.WriteLine($"PIN change test: Stack trace: {ex.StackTrace}");
                    
                    var errorResponse = new { 
                        status = "Exception", 
                        error = ex.Message, 
                        errorType = ex.GetType().Name,
                        stackTrace = ex.StackTrace
                    };
                    
                    return Results.Json(errorResponse);
                }
            });

            app.MapGet("/debug/user-id-test", async (HttpContext ctx, AuthService auth) =>
            {
                try
                {
                    // Replicate the exact logic from Player.razor GetCurrentUserId()
                    var httpContext = ctx;
                    var user = httpContext?.User;
                    
                    var initialResult = new
                    {
                        step = "Initial check",
                        httpContextNull = httpContext == null,
                        userNull = user == null,
                        isAuthenticated = user?.Identity?.IsAuthenticated,
                        userName = user?.Identity?.Name
                    };

                    if (user == null || !user.Identity?.IsAuthenticated == true)
                    {
                        return Results.Json(new { result = initialResult, status = "User not authenticated" });
                    }

                    var raw = user?.FindFirst("uid")?.Value;
                    var uidCheckResult = new
                    {
                        step = "After UID claim check",
                        uidClaimValue = raw,
                        canParseToInt = int.TryParse(raw, out var id),
                        parsedId = id
                    };

                    if (int.TryParse(raw, out var userId))
                    {
                        return Results.Json(new { result = uidCheckResult, status = "Success", userId = userId });
                    }

                    // Fallback: lookup by username
                    var name = user?.Identity?.Name;
                    AppUser? appUser = null;
                    
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        try
                        {
                            appUser = await auth.GetUserByNameAsync(name);
                        }
                        catch (Exception ex)
                        {
                            return Results.Json(new { result = uidCheckResult, status = "Username lookup failed", error = ex.Message });
                        }
                    }

                    if (appUser != null)
                    {
                        var fallbackResult = new
                        {
                            step = "Username fallback success",
                            userId = appUser.Id,
                            userName = appUser.Name
                        };
                        return Results.Json(new { result = fallbackResult, status = "Username fallback success", userId = appUser.Id });
                    }

                    return Results.Json(new { result = uidCheckResult, status = "All methods failed" });
                }
                catch (Exception ex)
                {
                    return Results.Json(new { status = "Exception", error = ex.Message, errorType = ex.GetType().Name });
                }
            });

            app.MapGet("/debug/test-auth", async (AuthService auth, HttpContext ctx) =>
            {
                try
                {
                    // Test user lookup and principal creation
                    var testUser = await auth.GetUserByNameAsync("Admin");
                    if (testUser == null)
                    {
                        return Results.Json(new { success = false, message = "Admin user not found" });
                    }

                    var principal = AuthService.BuildPrincipal(testUser);
                    
                    // Test claim retrieval
                    var uid = principal.FindFirst("uid")?.Value;
                    var name = principal.FindFirst(ClaimTypes.Name)?.Value;
                    
                    return Results.Json(new { 
                        success = true,
                        userId = testUser.Id,
                        userName = testUser.Name,
                        principalUid = uid,
                        principalName = name,
                        isAuthenticated = principal.Identity?.IsAuthenticated,
                        allClaims = principal.Claims.Select(c => new { type = c.Type, value = c.Value }).ToList()
                    });
                }
                catch (Exception ex)
                {
                    return Results.Json(new { 
                        success = false, 
                        error = ex.GetType().Name + ": " + ex.Message 
                    });
                }
            });

            app.MapGet("/debug/auth-info", (HttpContext ctx) =>
            {
                try
                {
                    var user = ctx.User;
                    var result = new
                    {
                        isAuthenticated = user?.Identity?.IsAuthenticated ?? false,
                        name = user?.Identity?.Name,
                        authenticationType = user?.Identity?.AuthenticationType,
                        claims = user?.Claims.Select(c => new { type = c.Type, value = c.Value }).ToList(),
                        uidClaim = user?.FindFirst("uid")?.Value,
                        nameClaim = user?.FindFirst(ClaimTypes.Name)?.Value,
                        environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
                    };
                    
                    Console.WriteLine($"Auth info debug: IsAuthenticated={result.isAuthenticated}, Name={result.name}, UID={result.uidClaim}");
                    
                    return Results.Json(result);
                }
                catch (Exception ex)
                {
                    var error = new { 
                        error = ex.Message,
                        errorType = ex.GetType().Name,
                        environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
                    };
                    
                    Console.WriteLine($"Auth info debug failed: {ex.GetType().Name}: {ex.Message}");
                    
                    return Results.Json(error, statusCode: 500);
                }
            });

            app.MapGet("/health/db", async (AppDbContext db) =>
            {
                try
                {
                    // Test database connectivity
                    var canConnect = await db.Database.CanConnectAsync();
                    
                    // Test basic query
                    var userCount = await db.Users.CountAsync();
                    var admin = await db.Users.FirstOrDefaultAsync(x => x.Name == "Admin");
                    
                    // Test settings table
                    var settings = await db.Settings.FirstOrDefaultAsync();
                    
                    var result = new { 
                        databaseConnected = canConnect,
                        userCount = userCount,
                        hasAdmin = admin != null,
                        adminName = admin?.Name,
                        hasSettings = settings != null,
                        databasePath = db.Database.GetDbConnection().ConnectionString,
                        environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
                    };
                    
                    Console.WriteLine($"Database health check: Connected={canConnect}, Users={userCount}, Admin={admin?.Name}");
                    
                    return Results.Json(result);
                }
                catch (Exception ex)
                {
                    var error = new { 
                        databaseConnected = false,
                        error = ex.Message,
                        errorType = ex.GetType().Name,
                        databasePath = db.Database.GetDbConnection().ConnectionString,
                        environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
                    };
                    
                    Console.WriteLine($"Database health check failed: {ex.GetType().Name}: {ex.Message}");
                    
                    return Results.Json(error, statusCode: 500);
                }
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

                    if (string.IsNullOrWhiteSpace(name) || (pin.Length != 4 && pin.Length != 6) || !pin.All(char.IsDigit))
                    {
                        return Results.Redirect("/login?error=1", permanent: false);
                    }

                    var user = await auth.ValidateLoginAsync(name, pin);
                    if (user is null)
                    {
                        Console.WriteLine($"Login debug: Authentication failed for user '{name}'");
                        return Results.Redirect("/login?error=1", permanent: false);
                    }

                    Console.WriteLine($"Login debug: Authentication successful for user '{user.Name}' (ID: {user.Id})");
                    
                    var principal = AuthService.BuildPrincipal(user);
                    Console.WriteLine($"Login debug: About to sign in principal");
                    
                    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                    
                    Console.WriteLine($"Login debug: Sign-in completed for user '{user.Name}'");

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
