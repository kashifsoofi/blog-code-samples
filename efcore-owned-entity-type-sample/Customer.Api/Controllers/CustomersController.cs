namespace Customer.Api.Controllers;

using Customer.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Customer = Models.Customer;

[Route("api/[controller]")]
[ApiController]
public class CustomersController : ControllerBase
{
    private readonly CustomerContext _customerContext;

    public CustomersController(CustomerContext customerContext)
    {
        _customerContext = customerContext;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAsync(long id)
    {
        var customer = await _customerContext.Customers.FirstOrDefaultAsync(x => x.Id == id);
        return Ok(customer);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CustomerRequest request)
    {
        var customer = new Customer
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            BillingAddress = new Address
            {
                Line1 = request.BillingAddress.Line1,
                Line2 = request.BillingAddress.Line2,
                Line3 = request.BillingAddress.Line3,
                Line4 = request.BillingAddress.Line4,
                City = request.BillingAddress.City,
                PostCode = request.BillingAddress.PostCode,
                Country = request.BillingAddress.Country,
            },
            ShippingAddress = new Address
            {
                Line1 = request.ShippingAddress.Line1,
                Line2 = request.ShippingAddress.Line2,
                Line3 = request.ShippingAddress.Line3,
                Line4 = request.ShippingAddress.Line4,
                City = request.ShippingAddress.City,
                PostCode = request.ShippingAddress.PostCode,
                Country = request.ShippingAddress.Country,
            },
        };
        await _customerContext.Customers.AddAsync(customer);
        await _customerContext.SaveChangesAsync();
        return Ok(customer.Id);
    }
}

public record CustomerRequest(
    string FirstName,
    string LastName,
    CustomerRequestAddress BillingAddress,
    CustomerRequestAddress ShippingAddress);

public record CustomerRequestAddress(
    string Line1,
    string Line2,
    string Line3,
    string Line4,
    string City,
    string PostCode,
    string Country);
