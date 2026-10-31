using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Forloeb> Forloeb => Set<Forloeb>();
    public DbSet<Hold> Hold => Set<Hold>();
    public DbSet<Membership> Memberships => Set<Membership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Forloeb>(e =>
        {
            e.ToTable("forloeb");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasIndex(x => x.CreatedBySub);
        });

        modelBuilder.Entity<Hold>(e =>
        {
            e.ToTable("hold");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.LectioHoldKey).HasMaxLength(128);
            e.HasIndex(x => x.ForloebId).IsUnique();
            e.HasIndex(x => x.LectioHoldKey);
            e.HasOne(x => x.Forloeb)
                .WithOne(x => x.Hold)
                .HasForeignKey<Hold>(x => x.ForloebId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Membership>(e =>
        {
            e.ToTable("memberships");
            e.HasKey(x => x.Id);
            e.Property(x => x.Source).HasMaxLength(32).IsRequired();
            e.Property(x => x.LectioId).HasMaxLength(64);
            e.Property(x => x.DisplayName).HasMaxLength(200);
            // Claimed: én række pr. Auth-bruger på holdet
            e.HasIndex(x => new { x.HoldId, x.UserSub })
                .IsUnique()
                .HasFilter("\"UserSub\" IS NOT NULL");
            // Shadow/lectio: én række pr. Lectio-id på holdet
            e.HasIndex(x => new { x.HoldId, x.LectioId })
                .IsUnique()
                .HasFilter("\"LectioId\" IS NOT NULL");
            e.HasIndex(x => x.UserSub);
            e.HasIndex(x => x.LectioId);
            e.HasOne(x => x.Hold)
                .WithMany(x => x.Memberships)
                .HasForeignKey(x => x.HoldId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
