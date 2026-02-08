using JiraLite.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraLite.Infrastructure.Persistence
{
    public class JiraLiteDbContext : DbContext
    {
        public JiraLiteDbContext(DbContextOptions<JiraLiteDbContext> options)
            : base(options)
        {
        }

        // DbSets for entities
        public DbSet<User> Users => Set<User>();
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
        public DbSet<TaskItem> Tasks => Set<TaskItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply configurations here (optional for now)
        }
    }
}
