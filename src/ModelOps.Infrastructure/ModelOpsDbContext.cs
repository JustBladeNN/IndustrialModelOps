using Microsoft.EntityFrameworkCore;
using ModelOps.Domain;

namespace ModelOps.Infrastructure;

public sealed class ModelOpsDbContext(DbContextOptions<ModelOpsDbContext> options)
    : DbContext(options)
{
    public DbSet<ModelProject> Projects => Set<ModelProject>();
    public DbSet<ModelElement> Elements => Set<ModelElement>();
    public DbSet<ValidationRun> ValidationRuns => Set<ValidationRun>();
    public DbSet<ValidationIssue> ValidationIssues => Set<ValidationIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ModelProject>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<ModelElement>(entity =>
        {
            entity.ToTable("model_elements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(300).IsRequired();
            entity.Property(x => x.ParametersJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.ProjectId, x.ExternalId });
            entity.HasIndex(x => new { x.ProjectId, x.Category });
            entity.HasOne(x => x.Project)
                .WithMany(x => x.Elements)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ValidationRun>(entity =>
        {
            entity.ToTable("validation_runs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Error).HasMaxLength(2_000);
            entity.HasIndex(x => new { x.ProjectId, x.CreatedAt });
        });

        modelBuilder.Entity<ValidationIssue>(entity =>
        {
            entity.ToTable("validation_issues");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RuleCode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Message).HasMaxLength(1_000).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasIndex(x => new { x.RunId, x.Severity, x.IsResolved });
            entity.HasOne(x => x.Run)
                .WithMany(x => x.Issues)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
