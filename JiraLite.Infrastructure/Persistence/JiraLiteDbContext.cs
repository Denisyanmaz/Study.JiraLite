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
        public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();


        public override int SaveChanges()
        {
            ApplyAudit();
            return base.SaveChanges();
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ✅ Soft delete: hide deleted tasks everywhere by default
            modelBuilder.Entity<TaskItem>()
                .HasQueryFilter(t => !t.IsDeleted);

            // ----------------------------
            // Users
            // ----------------------------
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.Email)
                      .IsRequired()
                      .HasMaxLength(320); // RFC-safe max

                entity.Property(u => u.PasswordHash)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(u => u.Role)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.HasIndex(u => u.Email)
                      .IsUnique();

                entity.Property(u => u.IsActive)
                      .HasDefaultValue(true);
                entity.Property(u => u.IsEmailVerified)
                      .HasDefaultValue(false);

            });
            // ----------------------------
            // Email Verifications
            // ----------------------------
            modelBuilder.Entity<EmailVerification>(entity =>
            {
                entity.Property(v => v.CodeHash)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(v => v.ExpiresAt)
                      .IsRequired();

                entity.Property(v => v.LastSentAt)
                      .IsRequired();

                entity.Property(v => v.SendCount)
                      .HasDefaultValue(1);

                entity.Property(v => v.Attempts)
                      .HasDefaultValue(0);

                entity.Property(v => v.IsUsed)
                      .HasDefaultValue(false);

                entity.HasIndex(v => v.UserId)
                      .IsUnique(); // ✅ one active record per user (we overwrite on resend)

                entity.HasOne(v => v.User)
                      .WithMany() // User has no navigation collection
                      .HasForeignKey(v => v.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(v => v.ExpiresAt);
            });


            // ----------------------------
            // Projects
            // ----------------------------
            modelBuilder.Entity<Project>(entity =>
            {
                entity.Property(p => p.Name)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(p => p.Description)
                      .HasMaxLength(2000);

                // owner is required (domain invariant)
                entity.Property(p => p.OwnerId)
                      .IsRequired();

                entity.Property(p => p.IsArchived)
                      .HasDefaultValue(false);

                // Optional but very useful: allow same name across different owners, but not duplicated for same owner
                entity.HasIndex(p => new { p.OwnerId, p.Name })
                      .IsUnique();

                // Relationship: Project -> Members
                entity.HasMany(p => p.Members)
                      .WithOne(pm => pm.Project)
                      .HasForeignKey(pm => pm.ProjectId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relationship: Project -> Tasks
                entity.HasMany(p => p.Tasks)
                      .WithOne() // TaskItem doesn’t currently have navigation Project
                      .HasForeignKey(t => t.ProjectId)
                      .OnDelete(DeleteBehavior.Restrict); // important: we soft-delete tasks; avoid cascades surprises
            });

            // ----------------------------
            // Project Members
            // ----------------------------
            modelBuilder.Entity<ProjectMember>(entity =>
            {
                entity.Property(pm => pm.Role)
                      .IsRequired()
                      .HasMaxLength(20);

                // DB-level prevention of duplicate membership
                entity.HasIndex(pm => new { pm.ProjectId, pm.UserId })
                      .IsUnique();

                // FK: ProjectMember -> User
                entity.HasOne(pm => pm.User)
                      .WithMany() // User has no navigation collection
                      .HasForeignKey(pm => pm.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // FK: ProjectMember -> Project configured above

                // Postgres check constraint: role is Owner/Member only
                entity.HasCheckConstraint(
                    "CK_ProjectMembers_Role",
                    @"""Role"" IN ('Owner','Member')");
            });

            // ----------------------------
            // Tasks
            // ----------------------------
            modelBuilder.Entity<TaskItem>(entity =>
            {
                entity.Property(t => t.Title)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(t => t.Description)
                      .HasMaxLength(2000);

                entity.Property(t => t.Priority)
                      .IsRequired();

                entity.Property(t => t.AssigneeId)
                      .IsRequired();

                entity.Property(t => t.ProjectId)
                      .IsRequired();

                entity.Property(t => t.IsDeleted)
                      .HasDefaultValue(false);

                // Postgres check constraint: priority range
                entity.HasCheckConstraint(
                    "CK_Tasks_Priority",
                    @"""Priority"" >= 1 AND ""Priority"" <= 5");

                // Helpful indexes for your common queries
                entity.HasIndex(t => t.ProjectId);
                entity.HasIndex(t => new { t.ProjectId, t.Status });
                entity.HasIndex(t => new { t.ProjectId, t.IsDeleted });
                entity.HasIndex(t => new { t.ProjectId, t.AssigneeId });
                entity.HasIndex(t => t.DueDate);
            });

            // ----------------------------
            // Comments
            // ----------------------------
            modelBuilder.Entity<TaskComment>(entity =>
            {
                entity.Property(c => c.Body)
                      .IsRequired()
                      .HasMaxLength(2000);

                entity.Property(c => c.AuthorId)
                      .IsRequired();

                entity.Property(c => c.TaskId)
                      .IsRequired();

                // Relationship: TaskComment -> TaskItem (many-to-one)
                entity.HasOne(c => c.Task)
                      .WithMany() // keep simple; can add TaskItem.Comments later if you want
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
                entity.Property(a => a.ProjectId)
                      .IsRequired();

                entity.Property(a => a.ActorId)
                      .IsRequired();

                entity.Property(a => a.ActionType)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(a => a.Message)
                      .IsRequired()
                      .HasMaxLength(2000);

                // FK: ActivityLog -> Project (activity feed must always belong to project)
                entity.HasOne<Project>()
                      .WithMany()
                      .HasForeignKey(a => a.ProjectId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Optional FK-ish: ActivityLog.TaskId exists but task can be deleted/soft-deleted.
                // Keep it nullable and do NOT enforce cascade relationship to avoid surprises.

                // Indexes for feeds
                entity.HasIndex(a => new { a.ProjectId, a.CreatedAt });
                entity.HasIndex(a => new { a.TaskId, a.CreatedAt });
                entity.HasIndex(a => a.ActorId);
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
