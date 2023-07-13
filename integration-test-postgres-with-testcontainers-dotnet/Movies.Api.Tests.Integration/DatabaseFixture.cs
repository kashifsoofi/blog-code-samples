using System.Diagnostics;
using TestContainers.Container.Abstractions.Hosting;
using TestContainers.Container.Abstractions.Networks;
using TestContainers.Container.Database.PostgreSql;
using TestContainers.Container.Database.Hosting;

namespace Movies.Api.Tests.Integration;

public class DatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = default!;

    private readonly bool useServiceDatabase;

    private readonly PostgreSqlContainer? databaseContainer;
    private MigrationsContainer? migrationsContainer;

    public DatabaseFixture()
    {
        this.useServiceDatabase = Debugger.IsAttached;
        if (!this.useServiceDatabase)
        {
            Environment.SetEnvironmentVariable("REAPER_DISABLED", true.ToString());
            this.databaseContainer = new ContainerBuilder<PostgreSqlContainer>()
                .ConfigureDockerImageName("postgres:14-alpine")
                .ConfigureDatabaseConfiguration("postgres", "Password123", "moviesdb")
                .Build();
        }
    }

    public async Task InitializeAsync()
    {
        if (this.useServiceDatabase)
        {
            this.ConnectionString = "Host=localhost;Username=postgres;Password=Password123;Database=moviesdb;Integrated Security=false;";
            return;
        }

        await this.databaseContainer!.StartAsync();

        this.migrationsContainer = new ContainerBuilder<MigrationsContainer>()
            .ConfigureContainer((context, container) =>
            {
                var connectionString = $"Host=localhost;Port={this.databaseContainer.GetMappedPort(PostgreSqlContainer.DefaultPort)};Username=postgres;Password=Password123;Database=moviesdb;Integrated Security=false;";
                container.Command = new List<string>
                {
                    connectionString,
                };
            })
            .ConfigureNetwork((hostContext, builderContext) =>
            {
                return new NetworkBuilder<UserDefinedNetwork>()
                .ConfigureNetwork((context, network) => { network.NetworkName = "host"; })
                .Build();
            })
            .Build();

        await this.migrationsContainer.StartAsync();
        var exitCode = await this.migrationsContainer.GetExitCodeAsync();
        if (exitCode > 0)
        {
            throw new Exception("Database migration failed");
        }

        this.ConnectionString = $"Host={this.databaseContainer.GetDockerHostIpAddress()};Port={this.databaseContainer.GetMappedPort(PostgreSqlContainer.DefaultPort)};Username=postgres;Password=Password123;Database=moviesdb;Integrated Security=false;";
    }

    public async Task DisposeAsync()
    {
        if (this.useServiceDatabase)
        {
            return;
        }

        if (this.migrationsContainer != null)
        {
            await this.migrationsContainer.StopAsync();
        }

        if (this.databaseContainer != null)
        {
            await this.databaseContainer.StopAsync();
        }
    }
}

