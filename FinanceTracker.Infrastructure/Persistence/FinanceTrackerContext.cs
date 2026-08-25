using Microsoft.EntityFrameworkCore;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Services;
using System.Reflection;

namespace FinanceTracker.Infrastructure.Persistence
{
    public class FinanceTrackerContext : DbContext
    {
        private readonly ICurrentUserAccessor? _currentUser;

        public FinanceTrackerContext(DbContextOptions<FinanceTrackerContext> options)
            : this(options, currentUser: null) { }

        public FinanceTrackerContext(DbContextOptions<FinanceTrackerContext> options, ICurrentUserAccessor? currentUser)
            : base(options) => _currentUser = currentUser;

        /// <summary>
        /// Read by the tenancy query filters below. Null when there is no user context, and
        /// a null never equals a non-nullable column, so the filters then match nothing.
        ///
        /// That direction is deliberate. A caller who has lost its identity sees an empty
        /// result — noticeable and harmless — rather than every tenant's records. The worker,
        /// which legitimately sweeps across tenants, opts out explicitly with
        /// <c>IgnoreQueryFilters()</c>.
        ///
        /// Referenced as an instance member so EF re-evaluates it per query rather than
        /// baking one tenant into the cached model.
        /// </summary>
        public Guid? CurrentUserId => _currentUser?.UserId;

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

            // Tenancy backstop. The repositories could filter by hand, but one forgotten
            // Where() would leak another person's finances, so the filter is applied at the
            // model where it cannot be omitted by accident.
            //
            // Frequency is reference data shared by everyone and is deliberately not scoped.
            modelBuilder.Entity<Category>().HasQueryFilter(e => e.UserId == CurrentUserId);
            modelBuilder.Entity<Transaction>().HasQueryFilter(e => e.UserId == CurrentUserId);
            modelBuilder.Entity<RecurringTransaction>().HasQueryFilter(e => e.UserId == CurrentUserId);
        }
    }
}
