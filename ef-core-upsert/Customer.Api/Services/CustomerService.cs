using Customer.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Customer.Api;

public interface ICustomerService
{
    Task<Models.Customer?> GetCustomerAsync(long id);
    Task<long> InsertCustomerAsync(CustomerDto customerDto);
    Task<long> UpdateCustomerAsync(long id, CustomerDto customerDto);
}

public class CustomerService : ICustomerService
{
    private readonly CustomerContext _customerContext;

    public CustomerService(CustomerContext customerContext)
    {
        _customerContext = customerContext;
    }

    public async Task<Models.Customer?> GetCustomerAsync(long id)
    {
        return await _customerContext.Customers.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<long> InsertCustomerAsync(CustomerDto customerDto)
    {
        var customer = FromCustomerDto(customerDto);
        customer.CustomerType = 1;
        customer.CreatedAt = DateTime.Now;
        customer.UpdatedAt = DateTime.Now;
        await _customerContext.Customers.AddAsync(customer);
        await _customerContext.SaveChangesAsync();
        return customer.Id;
    }

    public async Task<long> UpdateCustomerAsync(long id, CustomerDto customerDto)
    {
        var customer = await _customerContext.Customers.FirstOrDefaultAsync(x => x.Id == id);
        if (customer == null)
        {
            return 0;
        }

        customer.FirstName = customerDto.FirstName;
        customer.LastName = customerDto.LastName;
        customer.BillingAddressLine1 = customerDto.BillingAddressLine1;
        customer.BillingAddressLine2 = customerDto.BillingAddressLine2;
        customer.BillingAddressLine3 = customerDto.BillingAddressLine3;
        customer.BillingAddressLine4 = customerDto.BillingAddressLine4;
        customer.BillingAddressCity = customerDto.BillingAddressCity;
        customer.BillingAddressPostCode = customerDto.BillingAddressPostCode;
        customer.BillingAddressCountry = customerDto.BillingAddressCountry;
        customer.ShippingAddressLine1 = customerDto.ShippingAddressLine1;
        customer.ShippingAddressLine2 = customerDto.ShippingAddressLine2;
        customer.ShippingAddressLine3 = customerDto.ShippingAddressLine3;
        customer.ShippingAddressLine4 = customerDto.ShippingAddressLine4;
        customer.ShippingAddressCity = customerDto.ShippingAddressCity;
        customer.ShippingAddressPostCode = customerDto.ShippingAddressPostCode;
        customer.ShippingAddressCountry = customerDto.ShippingAddressCountry;
        customer.UpdatedAt = DateTime.Now;

        await _customerContext.SaveChangesAsync();
        return customer.Id;
    }

    private Models.Customer FromCustomerDto(CustomerDto customer)
    {
        return new Models.Customer
        {
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            BillingAddressLine1 = customer.BillingAddressLine1,
            BillingAddressLine2 = customer.BillingAddressLine2,
            BillingAddressLine3 = customer.BillingAddressLine3,
            BillingAddressLine4 = customer.BillingAddressLine4,
            BillingAddressCity = customer.BillingAddressCity,
            BillingAddressPostCode = customer.BillingAddressPostCode,
            BillingAddressCountry = customer.BillingAddressCountry,
            ShippingAddressLine1 = customer.ShippingAddressLine1,
            ShippingAddressLine2 = customer.ShippingAddressLine2,
            ShippingAddressLine3 = customer.ShippingAddressLine3,
            ShippingAddressLine4 = customer.ShippingAddressLine4,
            ShippingAddressCity = customer.ShippingAddressCity,
            ShippingAddressPostCode = customer.ShippingAddressPostCode,
            ShippingAddressCountry = customer.ShippingAddressCountry,
        };
    }
}

public record CustomerDto
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
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
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
