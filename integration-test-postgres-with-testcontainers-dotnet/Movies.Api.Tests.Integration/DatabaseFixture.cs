using System;
namespace Movies.Api.Tests.Integration;

public class DatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        this.ConnectionString = "Host=localhost;Username=postgres;Password=Password123;Database=moviesdb;Integrated Security=false;";
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }
}

