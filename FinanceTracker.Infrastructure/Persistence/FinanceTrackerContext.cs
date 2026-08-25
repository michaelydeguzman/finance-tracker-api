using Microsoft.EntityFrameworkCore;
using FinanceTracker.Domain.Entities;
using System.Reflection;

namespace FinanceTracker.Infrastructure.Persistence
{
    public class FinanceTrackerContext : DbContext
    {
        public FinanceTrackerContext(DbContextOptions<FinanceTrackerContext> options)
            : base(options) { }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Frequency> Frequencies { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<RecurringTransaction> RecurringTransactions { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserIdentity> UserIdentities { get; set; }
        public DbSet<UserCredential> UserCredentials { get; set; }
        public DbSet<UserToken> UserTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply all entity configurations from the assembly
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
