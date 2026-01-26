namespace FinanceTracker.Domain.Entities
{
    public class Recurring
    {
        public string Frequency { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class Category
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public string CategoryType { get; private set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public Recurring RecurringProps { get; set; }

        public Category(string name, bool isIncome)
        {
            Id = Guid.NewGuid();
            Name = name ?? throw new ArgumentNullException(nameof(name));
            IsIncome = isIncome;
        }
    }
}
