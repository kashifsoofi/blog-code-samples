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

        builder.OwnsOne(p => p.BillingAddress, p =>
        {
            p.Property(pp => pp.Line1).HasColumnName("BillingAddressLine1");
            p.Property(pp => pp.Line2).HasColumnName("BillingAddressLine2");
            p.Property(pp => pp.Line3).HasColumnName("BillingAddressLine3");
            p.Property(pp => pp.Line4).HasColumnName("BillingAddressLine4");
            p.Property(pp => pp.City).HasColumnName("BillingAddressCity");
            p.Property(pp => pp.PostCode).HasColumnName("BillingAddressPostCode");
            p.Property(pp => pp.Country).HasColumnName("BillingAddressCountry");
        });
        builder.OwnsOne(p => p.ShippingAddress, p =>
        {
            p.Property(pp => pp.Line1).HasColumnName("ShippingAddressLine1");
            p.Property(pp => pp.Line2).HasColumnName("ShippingAddressLine2");
            p.Property(pp => pp.Line3).HasColumnName("ShippingAddressLine3");
            p.Property(pp => pp.Line4).HasColumnName("ShippingAddressLine4");
            p.Property(pp => pp.City).HasColumnName("ShippingAddressCity");
            p.Property(pp => pp.PostCode).HasColumnName("ShippingAddressPostCode");
            p.Property(pp => pp.Country).HasColumnName("ShippingAddressCountry");
        });
    }
}