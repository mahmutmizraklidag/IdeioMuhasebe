using IdeioMuhasebe.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace IdeioMuhasebe.Data
{
    public class DatabaseContext : DbContext
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
        {
        }
        public DbSet<Debt> Debts { get; set; }
        public DbSet<DebtType> DebtTypes { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<RecurringDebt> RecurringDebts => Set<RecurringDebt>();
        public DbSet<IncomeType> IncomeTypes => Set<IncomeType>();
        public DbSet<Income> Incomes => Set<Income>();
        public DbSet<RecurringIncome> RecurringIncomes => Set<RecurringIncome>();
        public DbSet<RecurringDebtSkip> RecurringDebtSkips { get; set; }
        public DbSet<RecurringIncomeSkip> RecurringIncomeSkips { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<DebtType>()
               .HasMany(x => x.Debts)
               .WithOne(x => x.DebtType)
               .HasForeignKey(x => x.DebtTypeId)
               .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Income>()
    .HasOne(x => x.IncomeType)
    .WithMany(x => x.Incomes)
    .HasForeignKey(x => x.IncomeTypeId)
    .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
               .HasIndex(x => x.Username)
               .IsUnique();

            modelBuilder.Entity<Debt>()
                .HasIndex(x => x.DueDate);

            modelBuilder.Entity<Debt>()
                .HasIndex(x => new { x.DebtTypeId, x.IsPaid, x.DueDate });

            modelBuilder.Entity<RecurringDebt>()
    .HasOne(x => x.DebtType)
    .WithMany() // DebtType içinde collection şart değil
    .HasForeignKey(x => x.DebtTypeId)
    .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RecurringDebt>()
                .HasIndex(x => new { x.IsActive, x.DebtTypeId });

            modelBuilder.Entity<RecurringIncome>()
    .HasOne(x => x.IncomeType)
    .WithMany()
    .HasForeignKey(x => x.IncomeTypeId)
    .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RecurringDebtSkip>()
    .HasIndex(x => new { x.RecurringDebtId, x.Month })
    .IsUnique();

            modelBuilder.Entity<RecurringIncomeSkip>()
                .HasIndex(x => new { x.RecurringIncomeId, x.Month })
                .IsUnique();

            modelBuilder.Entity<Income>()
                .HasOne(x => x.RecurringIncome)
                .WithMany()
                .HasForeignKey(x => x.RecurringIncomeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<User>().HasData(new User
            {
                Id = 1,
                Username = "admin",
                password = "xyz123456"
            });
        }
    }
}
