using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Drafts.Data;
using Drafts.Services;

public class Program
{
    public static async Task Main(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=auth.db")
            .Options;

        await using var db = new AppDbContext(options);
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var gameLog = new GameLogService(
            new DbContextFactory<AppDbContext>(options),
            loggerFactory.CreateLogger<GameLogService>()
        );

        // Test logging
        await gameLog.LogAsync("Test log entry: Application started");
        await gameLog.LogAsync("Test log entry: User login test");
        await gameLog.LogAsync("Test log entry: Game creation test");

        Console.WriteLine("Test logs created successfully!");

        // Retrieve recent logs
        var logs = await gameLog.GetRecentLogsAsync(10);
        Console.WriteLine($"Found {logs.Count} recent logs:");
        foreach (var log in logs)
        {
            Console.WriteLine($"[{log.Timestamp:yyyy-MM-dd HH:mm:ss}] {log.Message}");
        }
    }
}

public class DbContextFactory<T> : IDbContextFactory<T> where T : DbContext
{
    private readonly DbContextOptions<T> _options;

    public DbContextFactory(DbContextOptions<T> options)
    {
        _options = options;
    }

    public T CreateDbContext()
    {
        return (T)Activator.CreateInstance(typeof(T), _options)!;
    }

    public async ValueTask<T> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return CreateDbContext();
    }
}
