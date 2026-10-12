using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;

namespace IPFMMS.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.UserId);

                entity.Property(u => u.UserId)
                    .ValueGeneratedNever();

                entity.HasIndex(u => u.Email)
                    .IsUnique();
            });
        }
    }
}