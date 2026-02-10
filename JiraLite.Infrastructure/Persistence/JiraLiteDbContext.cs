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
