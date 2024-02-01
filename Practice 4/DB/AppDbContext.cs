using System.Configuration;
using Microsoft.EntityFrameworkCore;
using Practice_4.Models.Entities;

namespace Practice_4.DB;

public sealed class AppDbContext : DbContext
{
    public AppDbContext()
    {
        this.Database.SetCommandTimeout(600);
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        
    }

    public DbSet<Score> Scores => Set<Score>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<SchoolYear> SchoolYears => Set<SchoolYear>();
    public DbSet<Subject> Subjects => Set<Subject>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(ConfigurationManager.ConnectionStrings["NationalExamDB"].ConnectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Score>(entity =>
        {
            entity.HasOne(e => e.Student)
                .WithMany(e => e.Scores)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Subject)
                .WithMany(e => e.Scores)
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasOne(e => e.SchoolYear)
                .WithMany(e => e.Students)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}