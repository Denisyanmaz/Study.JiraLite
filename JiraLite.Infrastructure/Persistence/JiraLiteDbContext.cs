using JiraLite.Application.Interfaces;
using JiraLite.Domain.Common;
using JiraLite.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraLite.Infrastructure.Persistence
{
    public class JiraLiteDbContext : DbContext
    {
        private readonly ICurrentUserService? _currentUser;

        public JiraLiteDbContext(
            DbContextOptions<JiraLiteDbContext> options,
            ICurrentUserService? currentUser = null)
            : base(options)
        {
            _currentUser = currentUser;
        }

        // DbSets for entities
        public DbSet<User> Users => Set<User>();
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
        public DbSet<TaskItem> Tasks => Set<TaskItem>();
        public DbSet<TaskComment> Comments => Set<TaskComment>();
        public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

        public override int SaveChanges()
        {
            ApplyAudit();
            return base.SaveChanges();
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ✅ Soft delete: hide deleted tasks everywhere by default
            modelBuilder.Entity<TaskItem>().HasQueryFilter(t => !t.IsDeleted);

            // ----------------------------
            // Comments
            // ----------------------------
            modelBuilder.Entity<TaskComment>(entity =>
            {
                entity.Property(c => c.Body)
                      .IsRequired()
                      .HasMaxLength(2000);

                // Relationship: TaskComment -> TaskItem (many-to-one)
                entity.HasOne(c => c.Task)
                      .WithMany() // keep simple; we can add TaskItem.Comments later if you want
                      .HasForeignKey(c => c.TaskId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Helpful indexes
                entity.HasIndex(c => c.TaskId);
                entity.HasIndex(c => c.AuthorId);
                entity.HasIndex(c => c.CreatedAt);
            });

            // ----------------------------
            // Activity Log
            // ----------------------------
            modelBuilder.Entity<ActivityLog>(entity =>
            {
                entity.Property(a => a.ActionType)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(a => a.Message)
                      .IsRequired()
                      .HasMaxLength(2000);

                entity.HasIndex(a => a.ProjectId);
                entity.HasIndex(a => a.TaskId);
                entity.HasIndex(a => a.CreatedAt);
            });
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyAudit();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void ApplyAudit()
        {
            var now = DateTime.UtcNow;
            var userId = _currentUser?.UserId;

            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;

                    // ✅ safe for tests/seeding (no HttpContext => userId null)
                    if (userId.HasValue)
                        entry.Entity.CreatedBy = userId.Value;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = now;

                    // Prevent changing creation fields
                    entry.Property(x => x.CreatedAt).IsModified = false;
                    entry.Property(x => x.CreatedBy).IsModified = false;
                }
            }
        }
    }
}
