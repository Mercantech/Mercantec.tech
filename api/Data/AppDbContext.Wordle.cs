using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<WordleSchedule> WordleSchedules => Set<WordleSchedule>();
    public DbSet<WordlePuzzle> WordlePuzzles => Set<WordlePuzzle>();
    public DbSet<WordlePlay> WordlePlays => Set<WordlePlay>();

    private static void ConfigureWordle(ModelBuilder model)
    {
        model.Entity<WordleSchedule>(entity =>
        {
            entity.ToTable("wordle_schedule");
            entity.HasKey(schedule => schedule.Id);
            entity.Property(schedule => schedule.Id).ValueGeneratedNever();
            entity.Property(schedule => schedule.Key).HasMaxLength(64).IsRequired();
        });

        model.Entity<WordlePuzzle>(entity =>
        {
            entity.ToTable("wordle_puzzles");
            entity.HasKey(puzzle => puzzle.Date);
            entity.Property(puzzle => puzzle.HostSlug).HasMaxLength(40).IsRequired();
            entity.Property(puzzle => puzzle.Theme).HasMaxLength(120).IsRequired();
            entity.Property(puzzle => puzzle.Answer).HasMaxLength(5).IsRequired();
            entity.Property(puzzle => puzzle.Explanation).HasMaxLength(500).IsRequired();
        });

        model.Entity<WordlePlay>(entity =>
        {
            entity.ToTable("wordle_plays");
            entity.HasKey(play => new { play.UserSub, play.Date });
            entity.Property(play => play.Attempts).IsConcurrencyToken();
            entity.HasOne<WordlePuzzle>().WithMany().HasForeignKey(play => play.Date);
        });
    }
}
