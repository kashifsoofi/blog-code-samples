using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Mutex.Api;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private PaymentsContext _paymentsContext;
    private IMemoryCache _memoryCache;

    public PaymentsController(PaymentsContext paymentsContext, IMemoryCache memoryCache)
    {
        _paymentsContext = paymentsContext;
        _memoryCache = memoryCache;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreatePaymentRequest request)
    {
        var invoiceId = request.InvoiceId;
        Console.WriteLine($"{DateTime.Now:u} - Create Payment for InvoideId: {invoiceId}");
        
        // Can work in parallel
        Console.WriteLine($"{DateTime.Now:u} - Section 1 start for InvoideId: {invoiceId}");
        await Task.Delay(TimeSpan.FromSeconds(1));
        Console.WriteLine($"{DateTime.Now:u} - Section 1 end for InvoideId: {invoiceId}");

        // Critical section
        var (paymentId, createdNew) = await CriticalSectionAsync(invoiceId);
        if (createdNew)
        {
            // Can work in parallel
            Console.WriteLine($"{DateTime.Now:u} - Section 3 start for InvoideId: {invoiceId}");
            await Task.Delay(TimeSpan.FromSeconds(10));
            Console.WriteLine($"{DateTime.Now:u} - Section 3 end for InvoideId: {invoiceId}");
        }
        Console.WriteLine($"{DateTime.Now:U} - Created Payment for InvoideId: {invoiceId}");
        return Ok(paymentId);
    }

    private static readonly ConcurrentDictionary<long, object> _locks = new ConcurrentDictionary<long, object>();

    private (Payment, bool) GetOrCreatePayment(long invoiceId)
    {
        var paymentId = Guid.NewGuid();

        lock(_locks.GetOrAdd(invoiceId, new object()))
        {
            var payment = _paymentsContext.Payments.FirstOrDefault(x => x.InvoideId == invoiceId);
            if (payment != null)
            {
                return (payment, false);
            }

            payment = new Payment { Id = Guid.NewGuid(), InvoideId = invoiceId };
            _paymentsContext.Payments.Add(payment);
            _paymentsContext.SaveChanges();

            return (payment, true);
        }
    }

    private async Task<(Guid, bool)> CriticalSectionAsync(long invoiceId)
    {
        Console.WriteLine($"{DateTime.Now:u} - Critical section start for InvoideId: {invoiceId}");

        var (payment, createdNew) = GetOrCreatePayment(invoiceId);
        if (!createdNew)
        {
            Console.WriteLine($"{DateTime.Now:u} - Skipped critical section InvoideId: {invoiceId} found {payment.Id}");
            return (payment.Id, createdNew);
        }

        await Task.Delay(TimeSpan.FromSeconds(10));

        payment.InvoideId = invoiceId;
        await _paymentsContext.SaveChangesAsync();

        Console.WriteLine($"{DateTime.Now:u} - Critical section end for InvoideId: {invoiceId}, PaymentId: {payment.Id}");

        return (payment.Id, false);
    }
}

public record CreatePaymentRequest(long InvoiceId);
