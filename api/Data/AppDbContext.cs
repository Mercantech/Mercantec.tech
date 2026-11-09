using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Forloeb> Forloeb => Set<Forloeb>();
    public DbSet<Hold> Hold => Set<Hold>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<HoldLectioSource> HoldLectioSources => Set<HoldLectioSource>();

    public DbSet<ChristmasGift> ChristmasGifts => Set<ChristmasGift>();
    public DbSet<GiftContact> GiftContacts => Set<GiftContact>();
    public DbSet<PollRound> PollRounds => Set<PollRound>();
    public DbSet<PollResponse> PollResponses => Set<PollResponse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureAvatarProfiles(modelBuilder);
        ConfigureActionCenter(modelBuilder);
        ConfigureLearning(modelBuilder);
        ConfigureGroupSets(modelBuilder);
        ConfigureHoldCalendar(modelBuilder);
        ConfigureTeachingCourses(modelBuilder);
        ConfigureQuestAudience(modelBuilder);
        ConfigureNotion(modelBuilder);
        ConfigureNotionMcp(modelBuilder);
        ConfigureEditorAi(modelBuilder);
        ConfigureEditorAiContext(modelBuilder);
        ConfigureNotebooks(modelBuilder);
        ConfigureReferences(modelBuilder);
        ConfigureDrawings(modelBuilder);
        ConfigureNotionGroupsMedia(modelBuilder);
        ConfigurePreviews(modelBuilder);
        ConfigureEmojis(modelBuilder);
        ConfigureExtensionUploads(modelBuilder);
        modelBuilder.Entity<PollRound>(e =>
        {
            e.ToTable("poll_rounds");
            e.HasKey(x => x.Id);
            e.Property(x => x.Question).HasMaxLength(200).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(12).IsRequired();
            e.Property(x => x.State).HasMaxLength(12).IsRequired();
            e.Property(x => x.ResultsMode).HasMaxLength(16).IsRequired();
            e.Property(x => x.OptionsJson).IsRequired();
            e.HasIndex(x => new { x.HoldId, x.CreatedAtUtc });
            e.HasOne(x => x.Hold).WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PollResponse>(e =>
        {
            e.ToTable("poll_responses");
            e.HasKey(x => new { x.PollId, x.UserSub });
            e.Property(x => x.Text).HasMaxLength(280);
            e.HasIndex(x => x.UserSub);
            e.HasOne(x => x.Poll).WithMany(x => x.Responses).HasForeignKey(x => x.PollId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<GiftContact>(e =>
        {
            e.ToTable("gift_contacts");
            e.HasKey(x => x.UserSub);
            e.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
        });
        modelBuilder.Entity<ChristmasGift>(e =>
        {
            e.ToTable("christmas_gifts", table => table.HasCheckConstraint("CK_gifts_different_people", "\"SenderSub\" <> \"RecipientSub\""));
            e.HasKey(x => x.Id);
            e.Property(x => x.SenderName).HasMaxLength(120).IsRequired();
            e.Property(x => x.RecipientName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            e.Property(x => x.Wrapping).HasMaxLength(16).IsRequired();
            e.Property(x => x.Theme).HasMaxLength(16).IsRequired().HasDefaultValue("unknown");
            e.Property(x => x.Message).HasMaxLength(240).IsRequired();
            e.HasIndex(x => new { x.RecipientSub, x.CreatedAtUtc });
            e.HasIndex(x => new { x.SenderSub, x.CreatedAtUtc });
            e.HasIndex(x => new { x.SenderSub, x.Theme, x.RecipientSub });
        });
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

        modelBuilder.Entity<HoldLectioSource>(e =>
        {
            e.ToTable("hold_lectio_sources");
            e.HasKey(x => x.Id);
            e.Property(x => x.LectioHoldKey).HasMaxLength(128).IsRequired();
            e.Property(x => x.LectioHoldName).HasMaxLength(200);
            e.HasIndex(x => x.LectioHoldKey).IsUnique();
            e.HasIndex(x => x.HoldId);
            e.HasOne(x => x.Hold)
                .WithMany(x => x.LectioSources)
                .HasForeignKey(x => x.HoldId)
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
