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
        public DbSet<EmailOTP> EmailOTPs { get; set; }
        public DbSet<IdCounter> IdCounters { get; set; }

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

            modelBuilder.Entity<EmailOTP>(entity =>
            {
                entity.HasKey(e => e.OtpId);

                entity.Property(e => e.OtpId)
                    .ValueGeneratedNever();

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<IdCounter>(entity =>
            {
                entity.HasKey(c => c.Prefix);

                entity.Property(c => c.Prefix)
                    .ValueGeneratedNever();

                entity.Property(c => c.LastNumber)
                    .IsRequired();
            });

        }
    }
}