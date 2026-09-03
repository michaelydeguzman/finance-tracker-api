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

        /// <summary>
        /// The household the caller shares records with, or null. Read by the tenancy filters
        /// alongside <see cref="CurrentUserId"/>, and an instance member for the same reason:
        /// EF must re-evaluate it per query rather than bake one household into the model.
        ///
        /// Null is the safe value here too. Every comparison against it is guarded by an
        /// explicit null check, so a caller with no household matches on ownership alone
        /// and a row with no household is never shared by accident.
        /// </summary>
        public Guid? CurrentHouseholdId => _currentUser?.HouseholdId;

        public DbSet<Category> Categories { get; set; }
        public DbSet<Frequency> Frequencies { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<RecurringTransaction> RecurringTransactions { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserIdentity> UserIdentities { get; set; }
        public DbSet<UserCredential> UserCredentials { get; set; }
        public DbSet<UserToken> UserTokens { get; set; }
        public DbSet<Household> Households { get; set; }
        public DbSet<HouseholdInvitation> HouseholdInvitations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply all entity configurations from the assembly
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            // Tenancy backstop. The repositories could filter by hand, but one forgotten
            // Where() would leak another person's finances, so the filter is applied at the
            // model where it cannot be omitted by accident.
            //
            // Two ways in, not one: a record is visible to the person who owns it, and to
            // anyone in the household it was stamped with. The household half is guarded by
            // an explicit null check on *the caller's* household, because a null never equals
            // a null in SQL — without the guard a caller outside any household would still
            // read as "no match", but the intent would rest on that accident rather than on
            // the code saying so.
            //
            // Frequency is reference data shared by everyone and is deliberately not scoped.
            modelBuilder.Entity<Category>().HasQueryFilter(e =>
                e.UserId == CurrentUserId
                || (CurrentHouseholdId != null && e.HouseholdId == CurrentHouseholdId));

            modelBuilder.Entity<Transaction>().HasQueryFilter(e =>
                e.UserId == CurrentUserId
                || (CurrentHouseholdId != null && e.HouseholdId == CurrentHouseholdId));

            modelBuilder.Entity<RecurringTransaction>().HasQueryFilter(e =>
                e.UserId == CurrentUserId
                || (CurrentHouseholdId != null && e.HouseholdId == CurrentHouseholdId));
        }
    }
}
