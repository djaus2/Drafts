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

            app.MapPost("/auth/login", async (
                HttpContext ctx,
                AuthService auth,
                [FromForm] string name,
                [FromForm] string pin,
                [FromForm] string? returnUrl) =>
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
            }).DisableAntiforgery();

            app.Run();
        }
    }
}
