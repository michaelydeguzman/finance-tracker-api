namespace FinanceTracker.Domain.Categories
{
    public class Recurrence
    {
        public Guid Id { get; private set; }

        public string Frequency { get; set; } = string.Empty;
    }
}

public record Frequency (string frequency, DateTime startDate, DateTime endDate);