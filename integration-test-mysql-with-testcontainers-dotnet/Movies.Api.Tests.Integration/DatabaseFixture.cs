using System.Diagnostics;
using TestContainers.Container.Abstractions.Hosting;
using TestContainers.Container.Database.MySql;
using TestContainers.Container.Database.Hosting;
using TestContainers.Container.Abstractions.Networks;

namespace Movies.Api.Tests.Integration;

public class DatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = default!;

    private readonly bool useServiceDatabase;

    private readonly MySqlContainer? databaseContainer;
    private MigrationsContainer? migrationsContainer;

    public DatabaseFixture()
    {
        this.useServiceDatabase = Debugger.IsAttached;
        if (!this.useServiceDatabase)
        {
            Environment.SetEnvironmentVariable("REAPER_DISABLED", true.ToString());
            this.databaseContainer = new ContainerBuilder<MySql57Container>()
                .ConfigureDatabaseConfiguration("root", "Password123", "defaultdb")
                .Build();
        }
    }


    public async Task InitializeAsync()
    {
        if (this.useServiceDatabase)
        {
            this.ConnectionString = "server=localhost;database=Movies;uid=root;password=Password123;SslMode=None;";
            return;
        }

        await this.databaseContainer!.StartAsync();

        this.migrationsContainer = new ContainerBuilder<MigrationsContainer>()
            .ConfigureContainer((context, container) =>
            {
                var connectionString = $"server=localhost;Port={this.databaseContainer.GetMappedPort(MySqlContainer.DefaultPort)};database=defaultdb;uid=root;password=Password123;SslMode=None;";
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

        this.ConnectionString = $"server={this.databaseContainer.GetDockerHostIpAddress()};Port={this.databaseContainer.GetMappedPort(MySqlContainer.DefaultPort)};database=Movies;uid=root;password=Password123;SslMode=None;";
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

