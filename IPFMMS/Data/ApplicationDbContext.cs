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

        public DbSet<SharedFinance> SharedFinances { get; set; }
        public DbSet<SharedFinanceMember> SharedFinanceMembers { get; set; }
        public DbSet<SharedBudget> SharedBudgets { get; set; }
        public DbSet<SharedSavingsGoal> SharedSavingsGoals { get; set; }
        public DbSet<SharedFinanceTransaction> SharedFinanceTransactions { get; set; }
        public DbSet<DebtRateChange> DebtRateChanges { get; set; }
        public DbSet<Debt> Debts { get; set; }
        public DbSet<DebtRepayment> DebtRepayments { get; set; }
        public DbSet<RoutineBill> RoutineBills { get; set; }
        public DbSet<RoutineBillPayment> RoutineBillPayments { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ParentStudentLink> ParentStudentLinks { get; set; }
        public DbSet<SpendingPrediction> SpendingPredictions { get; set; }
        public DbSet<FinancialHealthRecord> FinancialHealthRecords { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<AdminAuditEntry> AdminAuditEntries { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }
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
            modelBuilder.Entity<AdminAuditEntry>(e =>
            {
                e.ToTable("AdminAuditEntries");
                e.Property(a => a.AuditId).ValueGeneratedNever();
                e.HasOne(a => a.Admin).WithMany().HasForeignKey(a => a.AdminUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(a => new { a.CreatedDate, a.AuditId });
                e.HasIndex(a => new { a.EntityType, a.EntityId, a.CreatedDate });
            });
            modelBuilder.Entity<UserPreference>(e =>
            {
                e.ToTable("UserPreferences");
                e.HasKey(p => p.UserId);
                e.Property(p => p.UserId).ValueGeneratedNever();
                e.Property(p => p.Theme).HasDefaultValue("Light");
                e.Property(p => p.BillInApp).HasDefaultValue(true);
                e.Property(p => p.BillEmail).HasDefaultValue(true);
                e.Property(p => p.MonitoringInApp).HasDefaultValue(true);
                e.Property(p => p.MonitoringEmail).HasDefaultValue(true);
                e.Property(p => p.InvitationEmail).HasDefaultValue(true);
                e.HasOne(p => p.User).WithOne().HasForeignKey<UserPreference>(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<SharedFinance>(e=>{e.HasOne(g=>g.Owner).WithMany().HasForeignKey(g=>g.ownerId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(g=>new{g.ownerId,g.status});});
            modelBuilder.Entity<SharedFinanceMember>(e=>{e.HasKey(m=>new{m.sharedFinanceId,m.userId});e.HasOne(m=>m.Finance).WithMany().HasForeignKey(m=>m.sharedFinanceId).OnDelete(DeleteBehavior.Restrict);e.HasOne(m=>m.User).WithMany().HasForeignKey(m=>m.userId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(m=>new{m.userId,m.invitationStatus});});
            modelBuilder.Entity<SharedBudget>(e=>{e.HasOne(b=>b.Finance).WithMany().HasForeignKey(b=>b.sharedFinanceId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(b=>new{b.sharedFinanceId,b.budgetMonth}).IsUnique();});
            modelBuilder.Entity<SharedSavingsGoal>(e=>{e.HasOne(g=>g.Finance).WithMany().HasForeignKey(g=>g.sharedFinanceId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(g=>g.sharedFinanceId).IsUnique();});
            modelBuilder.Entity<SharedFinanceTransaction>(e=>{e.HasOne(t=>t.Finance).WithMany().HasForeignKey(t=>t.sharedFinanceId).OnDelete(DeleteBehavior.Restrict);e.HasOne(t=>t.User).WithMany().HasForeignKey(t=>t.userId).OnDelete(DeleteBehavior.Restrict);e.HasOne(t=>t.ExpenseCategory).WithMany().HasForeignKey(t=>t.expenseCategoryId).OnDelete(DeleteBehavior.Restrict);e.HasOne(t=>t.Expense).WithMany().HasForeignKey(t=>t.expenseId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(t=>t.expenseId).IsUnique();e.HasIndex(t=>new{t.userId,t.submissionKey}).IsUnique();e.HasIndex(t=>new{t.sharedFinanceId,t.transactionDate});});
            modelBuilder.Entity<DebtRateChange>(entity =>
            {
                entity.HasOne(c=>c.Debt).WithMany().HasForeignKey(c=>c.debtId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(c=>new{c.debtId,c.effectiveDueDate});
            });
            modelBuilder.Entity<Debt>(entity =>
            {
                entity.Property(d=>d.debtId).ValueGeneratedNever();
                entity.Property(d=>d.calculationMode).HasDefaultValue("Manual");
                entity.Property(d=>d.rateType).HasDefaultValue("Fixed");
                entity.Property(d=>d.loanDueDay).HasDefaultValue(1);
                entity.HasIndex(d=>new{d.userId,d.status,d.dueDate});
                entity.HasOne(d=>d.User).WithMany().HasForeignKey(d=>d.userId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<DebtRepayment>(entity =>
            {
                entity.Property(p=>p.repaymentId).ValueGeneratedNever();
                entity.Property(p=>p.allocationMethod).HasDefaultValue("Manual");
                entity.HasIndex(p=>new{p.debtId,p.repaymentDate});
                entity.HasIndex(p=>p.expenseId).IsUnique();
                entity.HasOne(p=>p.Debt).WithMany().HasForeignKey(p=>p.debtId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p=>p.Expense).WithMany().HasForeignKey(p=>p.expenseId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<RoutineBill>(entity =>
            {
                entity.ToTable("RoutineBills");
                entity.HasKey(b => b.billId);
                entity.Property(b => b.billId).ValueGeneratedNever();
                entity.HasIndex(b => new { b.userId, b.status, b.dueDate });
                entity.HasOne(b => b.User).WithMany().HasForeignKey(b => b.userId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(b => b.ExpenseCategory).WithMany().HasForeignKey(b => b.expenseCategoryId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<RoutineBillPayment>(entity =>
            {
                entity.ToTable("RoutineBillPayments");
                entity.HasKey(p => p.paymentId);
                entity.Property(p => p.paymentId).ValueGeneratedNever();
                entity.HasIndex(p => new { p.billId, p.dueDate }).IsUnique();
                entity.HasIndex(p => p.expenseId).IsUnique();
                entity.HasOne(p => p.Bill).WithMany().HasForeignKey(p => p.billId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.Expense).WithMany().HasForeignKey(p => p.expenseId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("Notifications");
                entity.HasKey(n => n.notificationId);
                entity.Property(n => n.notificationId).ValueGeneratedNever();
                entity.Property(n => n.emailStatus).HasDefaultValue("NotRequested");
                entity.Property(n => n.showInApp).HasDefaultValue(true);
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