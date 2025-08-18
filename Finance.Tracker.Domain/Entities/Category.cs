namespace FinanceTracker.Domain.Entities
{

    public class Category
    {
        public int Id { get; set; }
        public required string CategoryName { get; set; }
        public bool IsDeleted { get; set; }
    }
}
