using Microsoft.EntityFrameworkCore;

namespace Mutex.Api;

public class PaymentsContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase(databaseName: "PaymentsDb");
    }

    public DbSet<Payment> Payments { get; set; }
}

public class Payment
{
    public Guid Id { get; set; }
    public long InvoideId { get; set; }
}