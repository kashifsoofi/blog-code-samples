namespace Customer.Api.Controllers;

using Customer.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("api/[controller]")]
[ApiController]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAsync(long id)
    {
        var customer = await _customerService.GetCustomerAsync(id);
        return Ok(customer);
    }

    [HttpPost]
    public async Task<IActionResult> UpsertCustomerAsync(UpsertCustomerRequest request)
    {
        var customer = new CustomerDto
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            BillingAddressLine1 = request.BillingAddress.Line1,
            BillingAddressLine2 = request.BillingAddress.Line2,
            BillingAddressLine3 = request.BillingAddress.Line3,
            BillingAddressLine4 = request.BillingAddress.Line4,
            BillingAddressCity = request.BillingAddress.City,
            BillingAddressPostCode = request.BillingAddress.PostCode,
            BillingAddressCountry = request.BillingAddress.Country,
            ShippingAddressLine1 = request.ShippingAddress.Line1,
            ShippingAddressLine2 = request.ShippingAddress.Line2,
            ShippingAddressLine3 = request.ShippingAddress.Line3,
            ShippingAddressLine4 = request.ShippingAddress.Line4,
            ShippingAddressCity = request.ShippingAddress.City,
            ShippingAddressPostCode = request.ShippingAddress.PostCode,
            ShippingAddressCountry = request.ShippingAddress.Country,
        };
        var id = request.CustomerId is null
            ? await _customerService.InsertCustomerAsync(customer)
            : await _customerService.UpdateCustomerAsync(request.CustomerId.Value, customer);
        return Ok(id);
    }
}

public record UpsertCustomerRequest(
    long? CustomerId,
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
