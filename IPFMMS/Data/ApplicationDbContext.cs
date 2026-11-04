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
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<Income> Incomes { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<IncomeCategory> IncomeCategories { get; set; }
        public DbSet<ExpenseCategory> ExpenseCategories { get; set; }

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

            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.ToTable("PasswordResetTokens");

                entity.HasKey(t => t.TokenId);

                entity.Property(t => t.TokenId)
                    .HasMaxLength(36);

                entity.Property(t => t.UserId)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(t => t.TokenHash)
                    .HasMaxLength(64)
                    .IsRequired();

                entity.HasIndex(t => t.TokenHash)
                    .IsUnique();

                entity.HasIndex(t => t.UserId);

                entity.HasOne(t => t.User)
                    .WithMany()
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<IncomeCategory>(entity =>
            {
                entity.ToTable("IncomeCategories");

                entity.HasKey(c => c.IncomeCategoryId);

                entity.Property(c => c.IncomeCategoryId)
                    .ValueGeneratedNever();

                entity.HasIndex(c => c.CategoryName)
                    .IsUnique();
            });

            modelBuilder.Entity<ExpenseCategory>(entity =>
            {
                entity.ToTable("ExpenseCategories");

                entity.HasKey(c => c.ExpenseCategoryId);

                entity.Property(c => c.ExpenseCategoryId)
                    .ValueGeneratedNever();

                entity.HasIndex(c => c.CategoryName)
                    .IsUnique();
            });

            modelBuilder.Entity<Income>(entity =>
            {
                entity.ToTable("Incomes");

                entity.HasKey(i => i.IncomeId);

                entity.Property(i => i.IncomeId)
                    .ValueGeneratedNever();

                entity.Property(i => i.Amount)
                    .HasPrecision(12, 2);

                // Each income belongs to one user.
                entity.HasOne(i => i.User)
                    .WithMany()
                    .HasForeignKey(i => i.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Each income belongs to one income category.
                entity.HasOne<IncomeCategory>()
                    .WithMany(c => c.Incomes)
                    .HasForeignKey(i => i.IncomeCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Expense>(entity =>
            {
                entity.ToTable("Expenses");

                entity.HasKey(e => e.ExpenseId);

                entity.Property(e => e.ExpenseId)
                    .ValueGeneratedNever();

                entity.Property(e => e.Amount)
                    .HasPrecision(12, 2);

                // Each expense belongs to one user.
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Each expense belongs to one expense category.
                entity.HasOne<ExpenseCategory>()
                    .WithMany(c => c.Expenses)
                    .HasForeignKey(e => e.ExpenseCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

        }
    }
}