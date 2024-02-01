
using System.Net.Http.Json;
using Flurl.Http;

var invoiceId = 1;
var invoiceId2 = 2;
var task1 = CreatePaymentAsync(invoiceId);
var task2 = CreatePaymentAsync(invoiceId);
var task3 = CreatePaymentAsync(invoiceId2);
var task4 = CreatePaymentAsync(invoiceId2);
//var task5 = CreatePaymentAsync(invoiceId);

Task.WaitAll(task1, task2, task3, task4);

Console.WriteLine(task1.Result);
Console.WriteLine(task2.Result);
Console.WriteLine(task3.Result);
Console.WriteLine(task4.Result);
// Console.WriteLine(task5.Result);

static async Task<Guid> CreatePaymentAsync(long invoiceId)
{
    var result = await "http://localhost:5151/api/payments"
        .PostJsonAsync(new {invoiceId});
    var response = result.ResponseMessage;
    var content =  await response.Content.ReadFromJsonAsync<Guid>();
    return content;
}