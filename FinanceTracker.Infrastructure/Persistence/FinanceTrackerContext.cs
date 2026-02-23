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

        protected override void OnModelCreating(ModelBuilder modelBuilder)  
        {
            base.OnModelCreating(modelBuilder);

            // Apply all entity configurations from the assembly
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
