using FluentMigrator;

namespace Customer.Api.Migrations;

[Migration(202402061921)]
public class _202402061921_AddCustomerTable : Migration
{
    public override void Up()
    {
        Create.Table("Customer")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("FirstName").AsString(50)
            .WithColumn("LastName").AsString(50)
            .WithColumn("CustomerType").AsInt32()
            .WithColumn("BillingAddressLine1").AsString(50)
            .WithColumn("BillingAddressLine2").AsString(50)
            .WithColumn("BillingAddressLine3").AsString(50)
            .WithColumn("BillingAddressLine4").AsString(50)
            .WithColumn("BillingAddressCity").AsString(50)
            .WithColumn("BillingAddressPostCode").AsString(50)
            .WithColumn("BillingAddressCountry").AsString(50)
            .WithColumn("ShippingAddressLine1").AsString(50)
            .WithColumn("ShippingAddressLine2").AsString(50)
            .WithColumn("ShippingAddressLine3").AsString(50)
            .WithColumn("ShippingAddressLine4").AsString(50)
            .WithColumn("ShippingAddressCity").AsString(50)
            .WithColumn("ShippingAddressPostCode").AsString(50)
            .WithColumn("ShippingAddressCountry").AsString(50)
            .WithColumn("CreatedAt").AsDateTime()
            .WithColumn("UpdatedAt").AsDateTime();
    }

    public override void Down()
    {
        Delete.Table("Customer");
    }
}
