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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(b =>
        {
            b.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Group>(b =>
        {
            b.HasIndex(x => x.Name).IsUnique();
            b.HasOne(x => x.OwnerUser)
             .WithMany()
             .HasForeignKey(x => x.OwnerUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GroupMember>(b =>
        {
            b.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
            b.HasOne(x => x.Group)
             .WithMany(x => x.Members)
             .HasForeignKey(x => x.GroupId)
             .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
