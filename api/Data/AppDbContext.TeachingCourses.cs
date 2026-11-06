using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<TeachingCourse> TeachingCourses => Set<TeachingCourse>();
    public DbSet<TeachingCourseResource> TeachingCourseResources => Set<TeachingCourseResource>();

    private static void ConfigureTeachingCourses(ModelBuilder model)
    {
        model.Entity<TeachingCourse>(e =>
        {
            e.ToTable("teaching_courses"); e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.LearningObjectives).HasMaxLength(8000).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.HoldId, x.Archived });
            e.HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<TeachingCourseResource>(e =>
        {
            e.ToTable("teaching_course_resources");
            e.HasKey(x => new { x.CourseId, x.Kind, x.ResourceId });
            e.Property(x => x.Kind).HasMaxLength(12).IsRequired();
            e.HasIndex(x => new { x.Kind, x.ResourceId }).IsUnique();
            e.HasOne(x => x.Course).WithMany(x => x.Resources).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
