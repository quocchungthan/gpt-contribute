using Microsoft.EntityFrameworkCore;

namespace Listener.Data;

public sealed class ListenerDbContext(DbContextOptions<ListenerDbContext> options) : DbContext(options)
{
    public DbSet<Recording> Recordings => Set<Recording>();
    public DbSet<RecordingTag> RecordingTags => Set<RecordingTag>();
    public DbSet<AppPreference> Preferences => Set<AppPreference>();
    public DbSet<OperationalEvent> OperationalEvents => Set<OperationalEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppPreference>().HasKey(x => x.Id);
        modelBuilder.Entity<Recording>().HasMany(x => x.Tags).WithOne(x => x.Recording).HasForeignKey(x => x.RecordingId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RecordingTag>().HasIndex(x => new { x.RecordingId, x.Value }).IsUnique();
        modelBuilder.Entity<OperationalEvent>().HasIndex(x => x.OccurredAt);
    }
}
