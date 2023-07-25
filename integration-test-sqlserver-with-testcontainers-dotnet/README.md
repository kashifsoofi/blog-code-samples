# Integration Testing MySQL with testcontainers-dotnet
This is a continuation of an earlier post [Integration Testing SQL Server Store](https://kashifsoofi.github.io/integrationtest/mysql/dotnet/integration-testing-sqlserver-store/). In this tutorial I will extend the sample to use [testcontainers-dotnet](https://github.com/isen-ng/testcontainers-dotnet) to spin up database container and apply migrations before executing our integration tests.

Prior to this sample, pre-requisite of running integration tests was that database server is running either on machine or in a container and migrations are applied. This step removes that manual step.

## Setup
Lets start by adding nuget packages
```shell
dotnet add package TestContainers.Container.Database.MsSql --version 1.5.4
```

We would need to start 2 containers before running our integration tests.
* Database Container - hosting the database server
* Migrations Container - container to apply database migrations

### MigrationsContainer
We will start by adding a new class `MigrationsContainer` inheriting from `GenericContainer`. We will add a helper method to create an image using `Dockerfile` from our `db` folder.

We will also add a helper method `GetExitCodeAsync`, this will use the docker client to wait for migrations container to exit, we need this so that we only run our integration tests after the migrations have been applied.

`MigrationsContainer` would look like following
```csharp
using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TestContainers.Container.Abstractions;
using TestContainers.Container.Abstractions.Hosting;
using TestContainers.Container.Abstractions.Images;

namespace Movies.Api.Tests.Integration;

public class MigrationsContainer : GenericContainer
{
    [ActivatorUtilitiesConstructor]
    public MigrationsContainer(IDockerClient dockerClient, ILoggerFactory loggerFactory)
        : base(CreateDefaultImage(), dockerClient, loggerFactory)
    {
        this.DockerClient = dockerClient;
    }

    internal IDockerClient DockerClient { get; }

    public async Task<long> GetExitCodeAsync()
    {
        var containerWaitResponse = await this.DockerClient.Containers.WaitContainerAsync(this.ContainerId);
        return containerWaitResponse.StatusCode;
    }

    private static IImage CreateDefaultImage()
    {
        return new ImageBuilder<DockerfileImage>()
            .ConfigureImage((context, image) =>
            {
                image.DockerfilePath = "Dockerfile";
                image.DeleteOnExit = true;
                image.BasePath = "../../../../db";
            })
            .Build();
    }
}
```

### DatabaseFixture
For `DatabaseFixture` we will add following fields
```csharp
private readonly bool useServiceDatabase;

private readonly MsSqlContainer? databaseContainer;
private MigrationsContainer? migrationsContainer;
```

`useServiceDatabase` will be helpful if we want to just use the database server running either on our host or running in a container and debug a test, this would reduce the startup time while debugging and fixing tests or running the red-green-refactor cycle.

Other 2 variables are to hold the references to containers and we will use those to cleanup after the tests are complete.

We will setup databaseContainer in the constructor by setting the Docker image to use using `ConfigureDockerImageName` and configuring username, password and database name using `ConfigureDatabaseConfiguration` extension method. Constructor will look like following
```csharp
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
```

### InitializeAsync
To start with if we are running in debug mode we will just set the connection string to point to database server already running either on our host or in container.

Fun starts if test is not running in debug mode. We start off by starting our database container. After that we will configure and build our migrations container. This needs to be done here becuase we need to pass connection string to database container to migrations container.

After the image is built, we will start migrations container and wait for it to exit, if the exit code is 0 that would indicate migrations are applied successfully. At this point we will setup `ConnectionString` in `DatabaseFixture` that will be used in the integration tests.

Complete code for `InitializeAsync` method is as follows
```csharp
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
```

### DisposeAsync
`DisposeAsync` is simple, if we are not running in debug mode, we double check if database and migration continers are not null we stop those using `StopAsync`.

It would look like as follows
```csharp
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
```

### DatabaseFixture.cs
Complete source of `DatabaseFixture.cs` is as follows
```csharp
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
```

### Test
Now `Run Test` should automatically perform following
* Spin up a Database container
* Create an image for migrations
* Spin up migrations container to execute migrations
* Execute integration tests
* Stop containers

This is all for this post, this automates spining up database and migrations before running integration tests.

## Integration Tests in CI
I have also added [GitHub Actions](https://github.com/features/actions) workflow to run these integration tests as part of the CI when a change is pushed to `main` branch.

We will use the standard steps defined in [Building and testing .NET](https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-net) guide. Running database server and migrations would be taken care by `DatabaseFixture` in `Tests.Integration` project.

Here is the complete listing of the workflow.
```yaml
name: Integration Test MySQL (testcontainers-dotnet)

on:
  push:
    branches: [ "main" ]
    paths:
     - 'integration-test-mysql-with-testcontainers-dotnet/**'
    
jobs:
  build:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: integration-test-mysql-with-testcontainers-dotnet

    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET Core SDK
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 7.0.x
      - name: Install dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --configuration Release --no-restore        
      - name: Run integration tests
        run: dotnet test --configuration Release --no-restore --no-build --verbosity normal
```

## Source
Source code for the demo application is hosted on GitHub in [blog-code-samples](https://github.com/kashifsoofi/blog-code-samples/tree/main/integration-test-mysql-with-testcontainers-dotnet) repository.

Source for `Integration Test MySQL (testcontainers-dotnet)` workflow is in [integration-test-mysql-testcontainers-dotnet.yml](https://github.com/kashifsoofi/blog-code-samples/blob/main/.github/workflows/integration-test-mysql-testcontainers-dotnet.yml).

## References
In no particular order  
* [REST API with ASP.NET Core 7 and MySql](https://kashifsoofi.github.io/aspnetcore/rest/postgres/restapi-with-asp.net-core-7-and-mysql/)
* [Docker](https://www.docker.com/)
* [MySQL Database](https://www.mysql.com/)
* [GitHub Actions](https://github.com/features/actions)
* [Building and testing .NET](https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-net)
* [testcontainers-dotnet](https://github.com/isen-ng/testcontainers-dotnet)
* And many more 