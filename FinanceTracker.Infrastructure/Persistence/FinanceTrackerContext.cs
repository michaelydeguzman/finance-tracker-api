using Microsoft.EntityFrameworkCore;
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Infrastructure.Persistence
{
    public class FinanceTrackerContext : DbContext
    {
        public FinanceTrackerContext(DbContextOptions<FinanceTrackerContext> options)
            : base(options) { }

        public DbSet<Category> Categories { get; set; }
    }
}
