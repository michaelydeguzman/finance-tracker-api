namespace FinanceTracker.Domain.Categories
{
    public class Category
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string CategoryType { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        public bool IsActive { get; private set; }
        public Recurrence Recurrence { get; private set; }
    }
}

public record Recurrence(string Frequency, DateTime StartDate, DateTime EndDate);