using Movies.Api.Store;
using Movies.Api.Store.InMemory;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

// Add services to the container.
builder.Services.AddSingleton<IMoviesStore, InMemoryMoviesStore>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapHealthChecks("/healthz");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();

