using Customer.Api;
using Customer.Api.Migrations;
using Customer.Api.Models;
using FluentMigrator.Runner;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var customerConnectionString = builder.Configuration.GetConnectionString("CustomerConnectionString");

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddFluentMigratorCore();
builder.Services.AddTransient<ICustomerService, CustomerService>();
builder.Services.ConfigureRunner(rb => rb
        .AddSQLite()
        .WithGlobalConnectionString(customerConnectionString)
        .ScanIn(typeof(_202402061921_AddCustomerTable).Assembly).For.Migrations());
builder.Services
    .AddDbContext<CustomerContext>(options => 
        options.UseSqlite(customerConnectionString));

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();

// app.UseHttpsRedirection();

using (var scope = app.Services.CreateScope())
{
    CustomerDbMigrator.Run(scope.ServiceProvider);
}


app.Run();
