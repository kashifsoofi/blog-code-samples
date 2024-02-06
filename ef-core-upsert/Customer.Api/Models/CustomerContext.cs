using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Customer.Api.Models;

public class CustomerContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        new CustomerEntityTypeConfiguration().Configure(modelBuilder.Entity<Customer>());
    }

    public CustomerContext(DbContextOptions<CustomerContext> options)
        : base(options)
    { }
}

public class CustomerEntityTypeConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customer");

        builder.Property(x => x.CustomerType)
            .HasDefaultValue(1);
        builder.Property(x => x.CreatedAt)
            .HasDefaultValue(DateTime.Now);
        builder.Property(x => x.UpdatedAt)
            .HasDefaultValue(DateTime.Now);
    }
}