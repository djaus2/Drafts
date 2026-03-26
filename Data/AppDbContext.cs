using Microsoft.EntityFrameworkCore;

namespace Drafts.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppSettings> Settings => Set<AppSettings>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<UsableMsVoice> UsableMsVoices => Set<UsableMsVoice>();
    public DbSet<GameLog> GameLogs => Set<GameLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(b =>
        {
            b.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<UsableMsVoice>(b =>
        {
            b.HasIndex(x => new { x.BrowserFamily, x.Token }).IsUnique();
        });
    }
}
