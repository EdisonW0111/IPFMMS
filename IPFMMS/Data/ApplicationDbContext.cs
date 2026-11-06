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

        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ParentStudentLink> ParentStudentLinks { get; set; }
        public DbSet<SpendingPrediction> SpendingPredictions { get; set; }
        public DbSet<FinancialHealthRecord> FinancialHealthRecords { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<EmailOTP> EmailOTPs { get; set; }
        public DbSet<IdCounter> IdCounters { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<Income> Incomes { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<IncomeCategory> IncomeCategories { get; set; }
        public DbSet<ExpenseCategory> ExpenseCategories { get; set; }
        public DbSet<BudgetRepeatSkip> BudgetRepeatSkips { get; set; }
        public DbSet<Budget> Budgets { get; set; }
        public DbSet<BudgetSchedule> BudgetSchedules { get; set; }
        public DbSet<SavingsAutomaticAttempt> SavingsAutomaticAttempts { get; set; }
        public DbSet<SavingsGoal> SavingsGoals { get; set; }
        public DbSet<SavingsContribution> SavingsContributions { get; set; }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("Notifications");
                entity.HasKey(n => n.notificationId);
                entity.Property(n => n.notificationId).ValueGeneratedNever();
                entity.Property(n => n.emailStatus).HasDefaultValue("NotRequested");
                entity.HasIndex(n => n.eventKey).IsUnique();
                entity.HasIndex(n => new { n.userId, n.isRead, n.createdDateTime });
                entity.HasOne(n => n.Receiver).WithMany().HasForeignKey(n => n.userId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(n => n.RelatedUser).WithMany().HasForeignKey(n => n.relatedUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(n => n.Link).WithMany().HasForeignKey(n => n.linkId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<ParentStudentLink>(entity =>
            {
                entity.ToTable("ParentStudentLinks");
                entity.HasKey(l => l.linkId);
                entity.Property(l => l.linkId).ValueGeneratedNever();
                entity.HasIndex(l => new { l.parentId, l.userId }).IsUnique();
                entity.HasOne(l => l.Parent).WithMany().HasForeignKey(l => l.parentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(l => l.Student).WithMany().HasForeignKey(l => l.userId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<SpendingPrediction>(entity =>
            {
                entity.ToTable("SpendingPredictions");
                entity.HasKey(p => p.predictionId);
                entity.Property(p => p.predictionId).ValueGeneratedNever();
                entity.HasIndex(p => new { p.userId, p.predictionMonth }).IsUnique();
                entity.HasOne(p => p.User).WithMany().HasForeignKey(p => p.userId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<FinancialHealthRecord>(entity =>
            {
                entity.ToTable("FinancialHealthRecords");
                entity.HasKey(r => r.healthRecordId);
                entity.Property(r => r.healthRecordId).ValueGeneratedNever();
                entity.HasIndex(r => new { r.userId, r.recordMonth }).IsUnique();
                entity.HasOne(r => r.User).WithMany().HasForeignKey(r => r.userId).OnDelete(DeleteBehavior.Restrict);
            });

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

                entity.Property(c => c.DisplayOrder)
                    .HasDefaultValue(CategoryDefaults.UnassignedOrder);
            });

            modelBuilder.Entity<ExpenseCategory>(entity =>
            {
                entity.ToTable("ExpenseCategories");

                entity.HasKey(c => c.ExpenseCategoryId);

                entity.Property(c => c.ExpenseCategoryId)
                    .ValueGeneratedNever();

                entity.HasIndex(c => c.CategoryName)
                    .IsUnique();

                entity.Property(c => c.DisplayOrder)
                    .HasDefaultValue(CategoryDefaults.UnassignedOrder);
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

            modelBuilder.Entity<BudgetRepeatSkip>(entity =>
            {
                entity.ToTable("BudgetRepeatSkips");
                entity.HasKey(s => new { s.BudgetScheduleId, s.SeriesVersion, s.BudgetMonth });
                entity.HasOne(s => s.BudgetSchedule).WithMany().HasForeignKey(s => s.BudgetScheduleId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Budget>(entity =>
            {
                entity.HasKey(b => b.BudgetId);

                // One budget per user, category and month.
                entity.HasIndex(b => new
                {
                    b.UserId,
                    b.ExpenseCategoryId,
                    b.BudgetMonth
                }).IsUnique();

                entity.HasOne(b => b.User)
                    .WithMany()
                    .HasForeignKey(b => b.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(b => b.ExpenseCategory)
                    .WithMany()
                    .HasForeignKey(b => b.ExpenseCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(b => b.BudgetAmount)
                    .HasPrecision(12, 2);

                entity.Property(b => b.BudgetPercentage)
                    .HasPrecision(5, 2);
            });

            modelBuilder.Entity<Budget>().HasOne(b => b.BudgetSchedule).WithMany()
                .HasForeignKey(b => b.BudgetScheduleId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Budget>().HasIndex(b => new { b.BudgetScheduleId, b.RepeatSeriesVersion, b.BudgetMonth });
            modelBuilder.Entity<BudgetSchedule>().Property(s => s.SeriesVersion).HasDefaultValue(1);
            modelBuilder.Entity<BudgetSchedule>(entity =>
            {
                entity.ToTable("BudgetSchedules");

                entity.HasKey(s => s.BudgetScheduleId);
                entity.Property(s => s.BudgetScheduleId)
                    .ValueGeneratedNever();

                // Reuse the same schedule when repetition is re-enabled.
                entity.HasIndex(s => new { s.UserId, s.ExpenseCategoryId })
                    .IsUnique();

                entity.HasOne(s => s.User)
                    .WithMany()
                    .HasForeignKey(s => s.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.ExpenseCategory)
                    .WithMany()
                    .HasForeignKey(s => s.ExpenseCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(s => s.NextBudgetMonth)
                    .HasColumnType("date");

                entity.Property(s => s.EndMonth)
                    .HasColumnType("date");

                entity.Property(s => s.BudgetAmount)
                    .HasPrecision(12, 2);

                entity.Property(s => s.BudgetPercentage)
                    .HasPrecision(5, 2);
            });

            modelBuilder.Entity<SavingsAutomaticAttempt>(entity =>
            {
                entity.ToTable("SavingsAutomaticAttempts");
                entity.HasIndex(a => new { a.SavingsGoalId, a.DueDate }).IsUnique();
                entity.HasOne(a => a.SavingsGoal).WithMany().HasForeignKey(a => a.SavingsGoalId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<SavingsGoal>(entity =>
            {
                entity.ToTable("SavingsGoals");

                entity.HasKey(g => g.SavingsGoalId);
                entity.Property(g => g.SavingsGoalId)
                    .ValueGeneratedNever();

                entity.HasOne(g => g.User)
                    .WithMany()
                    .HasForeignKey(g => g.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(g => g.TargetAmount)
                    .HasPrecision(12, 2);

                entity.Property(g => g.MonthlyTargetAmount)
                    .HasPrecision(12, 2);

                entity.Property(g => g.TargetDate)
                    .HasColumnType("date");
            });

            modelBuilder.Entity<SavingsContribution>(entity =>
            {
                entity.ToTable("SavingsContributions");

                entity.HasKey(c => c.ContributionId);
                entity.Property(c => c.ContributionId)
                    .ValueGeneratedNever();

                entity.HasOne(c => c.SavingsGoal)
                    .WithMany(g => g.SavingsContributions)
                    .HasForeignKey(c => c.SavingsGoalId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(c => c.Amount)
                    .HasPrecision(12, 2);

                entity.Property(c => c.ContributionDate)
                    .HasColumnType("date");
            });

        }
    }
}