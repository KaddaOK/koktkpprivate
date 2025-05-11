using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

public class KOKTDbContext : DbContext
{
    public DbSet<LocalScanPathEntry> LocalScanPaths { get; set; }
    public DbSet<LocalSongFileEntry> LocalSongFiles { get; set; }
    public DbSet<PerformanceHistory> Performances { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var appDbFilePath = Path.Combine(Utils.GetAppStoragePath(), "appdata.db");
        optionsBuilder.UseSqlite($"Data Source={appDbFilePath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocalSongFileEntry>(entity =>
        {
            entity.HasOne(p => p.ParentPath)
                .WithMany(b => b.Files)
                .HasForeignKey(p => p.ParentPathId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasIndex(p => p.FullPath).IsUnique().HasDatabaseName("IX_LocalSongFiles_FilePath");
        });

        modelBuilder.Entity<LocalScanPathEntry>(entity =>
        {
            entity.HasIndex(p => p.Path).IsUnique().HasDatabaseName("IX_LocalScanPaths_Path");
        });

        modelBuilder.Entity<PerformanceHistory>(entity =>
        {
            entity.HasOne(p => p.LocalSongFileEntry)
                .WithMany(b => b.Performances)
                .HasForeignKey(p => p.LocalSongFileEntryId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        //modelBuilder.Entity<LocalSongFileEntryFTS>().HasNoKey().ToView(null);
        /*modelBuilder.Entity<LocalSongFileEntryFTS>(entity =>
        {
            entity.HasOne(e => e.LocalSongFileEntry)
                .WithOne()
                .HasForeignKey<LocalSongFileEntryFTS>(e => e.RowId);
        });*/
    }

/*
    public override int SaveChanges()
    {
        var changedSongs = this.GetChangedEntities<LocalSongFileEntry>();

        this.ChangeTracker.AutoDetectChangesEnabled = false; // for performance reasons, to avoid calling DetectChanges() again.
        var result = base.SaveChanges();
        this.ChangeTracker.AutoDetectChangesEnabled = true;

        this.UpdateLocalSongFileEntryFTS(changedSongs);
        return result;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        var changedSongs = this.GetChangedEntities<LocalSongFileEntry>();

        this.ChangeTracker.AutoDetectChangesEnabled = false; // for performance reasons, to avoid calling DetectChanges() again.
        var result = await base.SaveChangesAsync(cancellationToken);
        this.ChangeTracker.AutoDetectChangesEnabled = true;

        this.UpdateLocalSongFileEntryFTS(changedSongs);
        return result;
    }

    private static void CreateFtsTables(KOKTDbContext context)
    {
        // For SQLite FTS
        // Note: This can be added to the `protected override void Up(MigrationBuilder migrationBuilder)` method too.
        context.Database.ExecuteSqlRaw(@"CREATE VIRTUAL TABLE IF NOT EXISTS ""LocalScanPathEntries_FTS""
                                USING fts5(""Text"", ""Title"", content=""LocalScanPathEntries"", content_rowid=""Id"");");
    }*/
}