using System.Diagnostics;
using TestContainers.Container.Abstractions.Hosting;
using TestContainers.Container.Abstractions.Networks;
using TestContainers.Container.Database.Hosting;
using TestContainers.Container.Database.MsSql;

namespace Movies.Api.Tests.Integration;

public class DatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = default!;

    private readonly bool useServiceDatabase;

    private readonly MsSqlContainer? databaseContainer;
    private MigrationsContainer? migrationsContainer;

    public DatabaseFixture()
    {
        this.useServiceDatabase = Debugger.IsAttached;
        if (!this.useServiceDatabase)
        {
            Environment.SetEnvironmentVariable("REAPER_DISABLED", true.ToString());
            this.databaseContainer = new ContainerBuilder<MsSqlContainer>()
                .ConfigureDockerImageName("mcr.microsoft.com/mssql/server:2022-latest")
                .ConfigureDatabaseConfiguration("sa", "Password123", "Movies")
                .ConfigureContainer((contex, container) =>
                {
                    container.Env.Add("MSSQL_PID", "Express");
                })
                .Build();
        }
    }

    public async Task InitializeAsync()
    {
        if (this.useServiceDatabase)
        {
            this.ConnectionString = "Server=localhost;Database=Movies;User ID=sa;Password=Password123;Encrypt=False";
            return;
        }

        await this.databaseContainer!.StartAsync();

        this.migrationsContainer = new ContainerBuilder<MigrationsContainer>()
            .ConfigureContainer((context, container) =>
            {
                var connectionString = $"Server=localhost;Port={this.databaseContainer.GetMappedPort(MsSqlContainer.DefaultPort)};User ID=sa;Password=Password123;Database=master;Encrypt=False";
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

        this.ConnectionString = $"Server={this.databaseContainer.GetDockerHostIpAddress()};Port={this.databaseContainer.GetMappedPort(MsSqlContainer.DefaultPort)};User ID=sa;Password=Password123;Database=Movies;Encrypt=False";
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
