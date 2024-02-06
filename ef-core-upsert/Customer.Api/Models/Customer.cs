namespace Customer.Api.Models;

public class Customer
{
    public long Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int CustomerType { get; set; }
    public string BillingAddressLine1 { get; set; }
    public string BillingAddressLine2 { get; set; }
    public string BillingAddressLine3 { get; set; }
    public string BillingAddressLine4 { get; set; }
    public string BillingAddressCity { get; set; }
    public string BillingAddressPostCode { get; set; }
    public string BillingAddressCountry { get; set; }
    public string ShippingAddressLine1 { get; set; }
    public string ShippingAddressLine2 { get; set; }
    public string ShippingAddressLine3 { get; set; }
    public string ShippingAddressLine4 { get; set; }
    public string ShippingAddressCity { get; set; }
    public string ShippingAddressPostCode { get; set; }
    public string ShippingAddressCountry { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
