# Integration Testing Postgres Store
This is a continuation of an earlier post [REST API with ASP.NET Core 7 and Postgres](https://kashifsoofi.github.io/aspnetcore/rest/postgres/restapi-with-asp.net-core-7-and-postgres/). In this tutorial I will extend the sample to add integration tests to verify our implementation of `PostgresMoviesStore`.

## Why Integration Test
As per definition from [Wikipedia](https://en.wikipedia.org/wiki/Integration_testing) integration testing is the phase in which individual software modules are combined and tested as a group.

This is important in our case as we are using an external system to store our data and before we can declare that it is ready to use we need to make sure that it is working as intended. It would also help us verify if our Dapper mapping is correct especially if we are using any custom mapping.

Our options are
* One way would be to run the database server and our api project and invoke the endpoints either from the Swagger UI, curl or Postman with defined data and then verify if our service is storing and retrieving the data correctly.This is tedious to do everytime we make a change, add or remove a property to our domain model, add a new endpoint for new use case.
* Add set of integration tests to our source code and run everytime we make a change, this would ensure that any change we have made has not broken any existing funcationality and scenario. Important thing to remember is this is not set in stone and these should be updated as the funcationality evolves, new functionality would lead to adding new test cases.

Focus of this article would be to implement automated integration tests for `PostgresMoviesStore` we implemented earlier.

## Test Project
Let's start by adding a new test project.
* Right click on Solution name -> Add -> New Project -> Tests -> xUnit Test Project
<figure>
  <a href="images/001-new-project.png"><img src="images/001-new-project.png"></a>
  <figcaption>New xUnit Test Project</figcaption>
</figure>
* Select Target Framework, I have selected `.NET 7.0` as we are targeting `.NET 7.0` in this sample
* Name your test project, I like to name as Project I am testing followed by `.Tests` and followed by test types `.Integration`
<figure>
  <a href="images/002-name-project.png"><img src="images/002-name-project.png"></a>
  <figcaption>Name Test Project</figcaption>
</figure>
* Click create to finish creating test project.

### Setup
Start by adding nuget reference to our project `Movies.Api` in `Movies.Api.Tests.Integration` project and following nuget packages
```
dotnet add pacakge AutoFixture.Xunit2 --version 4.18.0
dotnet add pacakge FluentAssertions --version 6.11.0
dotnet add package Dapper.Contrib --version 2.0.78
```

In order to test funcationality provided by `PostgresMoviesStore` we would need a way to access database without going through our store. This is to ensure that e.g. `Create` funcationlity works independent of `GetById` of the store. To accomplish that I would add a couple of helper classes under `Helper` folder.

#### DatabaseHelper.cs
```csharp
using System;
using System.Data;
using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;

namespace Movies.Api.Tests.Integration.Helpers;

public class DatabaseHelper<TId, TRecord>
    where TRecord : class
    where TId : notnull
{
    protected readonly string connectionString;
	private readonly string tableName;
	private readonly string idColumnName;
    protected readonly Func<TRecord, TId> idSelector;

    public DatabaseHelper(
        string connectionString,
        string tableName,
        Func<TRecord, TId> idSelector,
        string idColumnName = "Id")
	{
        this.connectionString = connectionString;
		this.tableName = tableName;
		this.idColumnName = idColumnName;
		this.idSelector = idSelector;
	}

    public Dictionary<TId, TRecord> AddedRecords { get; } = new Dictionary<TId, TRecord>();

    public virtual async Task<TRecord> GetRecordAsync(TId id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        return await connection.QueryFirstOrDefaultAsync<TRecord>(
            $"SELECT * FROM {tableName} WHERE {idColumnName} = @Id",
            new { Id = id },
            commandType: CommandType.Text);
    }

    public virtual async Task AddRecordAsync(TRecord record)
    {
        this.AddedRecords.Add(idSelector(record), record);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.InsertAsync<TRecord>(record);
    }

    public async Task AddRecordsAsync(IEnumerable<TRecord> records)
    {
        foreach (var record in records)
        {
            await AddRecordAsync(record);
        }
    }

    public void TrackId(TId id) => AddedRecords.Add(id, default!);

    public virtual async Task DeleteRecordAsync(TId id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.ExecuteAsync(
            $"DELETE FROM {tableName} WHERE {idColumnName} = @Id",
            new { Id = id },
            commandType: CommandType.Text);
    }

    public async Task CleanTableAsync()
    {
        foreach (var addedRecord in AddedRecords)
        {
            await DeleteRecordAsync(addedRecord.Key);
        }
    }
}
```
#### MoviesDatabaseHelper.cs
```csharp
using System;
using System.Data;
using Dapper;
using Movies.Api.Store;
using Npgsql;
using Xunit;

namespace Movies.Api.Tests.Integration.Helpers;

public class MoviesDatabaseHelper : DatabaseHelper<Guid, Movie>
{
	public MoviesDatabaseHelper(string connectionString)
		: base(connectionString, "movies", x => x.Id, "id")
    { }

    public async override Task AddRecordAsync(Movie record)
    {
        this.AddedRecords.Add(idSelector(record), record);

        var parameters = new
        {
            id = record.Id,
            title = record.Title,
            director = record.Director,
            release_date = record.ReleaseDate,
            ticket_price = record.TicketPrice,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
        };

        var query = @"
            INSERT INTO movies(
                id,
                title,
                director,
                release_date,
                ticket_price,
                created_at,
                updated_at
            )
            VALUES (
                @id,
                @title,
                @director,
                @release_date,
                @ticket_price,
                @created_at,
                @updated_at
            )";

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.ExecuteAsync(query, parameters, commandType: CommandType.Text);
    }

    public async override Task<Movie> GetRecordAsync(Guid id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        return await connection.QueryFirstOrDefaultAsync<Movie>(
            $"SELECT id as Id, title as Title, director as Director, release_date as ReleaseDate, ticket_price as TicketPrice, created_at as CreatedAt, updated_at as UpdatedAt FROM movies WHERE id = @id",
            new { id },
            commandType: CommandType.Text);
    }
}
```
Next add a `DatabaseFixture` class to pass shared database context between test classes testing database, in our case it is `ConnectionString` that we would initialise in `InitializeAsync` method.
```csharp
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
```
Next add a `DatabaseCollection.cs` file, this is so that we can pass the same `DatabaseContext` to multiple files.  This is here as an example as we only have a single class to test.
```csharp
namespace Movies.Api.Tests.Integration;

[CollectionDefinition("DatabaseCollection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
```
Let's also delete default `UnitTest1.cs` file.

### PostgresMoviesStoreTests
I like to mirror the structure in the source project but please feel free to place your test file where you like. I will add a `Store` and under that a `Postgres` folder and then create `PostgresMoviesStoreTests.cs` file.

I will start by adding an instance of `moviesDatabaseHelper` and `sut` (system under test). In constructor, `moviesDatabaseHelper` is initialised using `ConnectionString` from `DatabaseFixture` then initialised an in memory configuration object and passed that to `PostgresMoviesStore`.

I am calling `CleanTableAsync` in `DisposeAsync` method that will run after each test to cleanup any data inserted by the test.

#### PostgresMoviesStoreTests.cs
```csharp
using System;
using Microsoft.Extensions.Configuration;
using Movies.Api.Store.Postgres;
using Movies.Api.Tests.Integration.Helpers;

namespace Movies.Api.Tests.Integration.Store.Postgres;

[Collection("DatabaseCollection")]
public class PostgresMoviesStoreTests : IAsyncLifetime
{
    private readonly MoviesDatabaseHelper moviesDatabaseHelper;

    private readonly PostgresMoviesStore sut;

    public PostgresMoviesStoreTests(DatabaseFixture databaseFixture)
	{
        moviesDatabaseHelper = new MoviesDatabaseHelper(databaseFixture.ConnectionString);

        var myConfiguration = new Dictionary<string, string?>
        {
            {"ConnectionStrings:MoviesDb", databaseFixture.ConnectionString},
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration)
            .Build();

        sut = new PostgresMoviesStore(configuration);
	}

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await moviesDatabaseHelper.CleanTableAsync();
    }
}
```

### GetById Tests
Time to add tests. Please note to run these test first we would need to start our database server and apply the migrations. Lets go ahead and do that before adding our test.
```shell
docker-compose -f docker-compose.dev-env.yml up -d
```

Our first test is very easy, I like to name my tests as `MethodName_GivenCondition_ShouldExpectedResult` to follow the pattern, I have added `GetById_GivenRecordDoesNotExist_ShouldReturnNull` and I am going to leavarage excellent [AutoFixture](https://github.com/AutoFixture/AutoFixture) to get a new Guid as parameter. For this test we don't need arrange part, we would skip to the act and then assert. For Assertion I am going to use [FluentAssertions](https://fluentassertions.com/). For this test we need to assert the returned result is null.
```csharp
[Theory]
[AutoData]
public async void GetById_GivenRecordDoesNotExist_ShouldReturnNull(Guid id)
{
    // Arrange

    // Act
    var result = await sut.GetById(id);

    // Assert
    result.Should().BeNull();
}
```
Go ahead and run the test, it should be green.

Let's add our second test `GetById_GivenRecordExists_ShouldReturnRecord`, in this test we would first insert a new record using the helper we added earlier. In assertion step we would compare the result with instance passed by `AutoFixture` excluding `CreatedAt` and `UpdatedAt` properties as we know these would be set to current time when inserting. Instead we would test those are set to within 1 second of current time. I have also excluded `ReleasedAt` property and will match it within 1 second of passed value, in my opinion this is acceptable as this time being within 1 second of inserted time is accurate enough for this use case, however if more accuracy is needed we would look for an appropriate column type that provides that accuracy. Newly inserted record will be cleared after the test from `DisposeAsync` method.
```csharp
[Theory]
[AutoData]
public async void GetById_GivenRecordExists_ShouldReturnRecord(Movie movie)
{
    // Arrange
    await moviesDatabaseHelper.AddRecordAsync(movie);

    // Act
    var result = await sut.GetById(movie.Id);

    // Assert
    result.Should().NotBeNull();
    result.Should().BeEquivalentTo(
        movie,
        x => x.Excluding(p => p.ReleaseDate).Excluding(p => p.CreatedAt).Excluding(p => p.UpdatedAt));
    result.ReleaseDate.Should().BeCloseTo(movie.ReleaseDate, TimeSpan.FromSeconds(1));
    result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
}
```

### GetAll Tests
First test is easy, we would just test if there are no records, `GetAll` return an empty collection.
```csharp
[Fact]
public async void GetAll_GivenNoRecords_ShouldReturnEmptyCollection()
{
    // Arrange
    // Act
    var result = await this.sut.GetAll();

    // Assert
    result.Should().BeEmpty();
}
```

Next test is to insert some records and executing `GetAll` and comparing the results. I have chosen not to assert the `DateTime` field values as we are doing that in `GetById` but those should be asserted if this method is in a separate class e.g. if we choose to have separate Command and Query classes.
```csharp
[Theory]
[AutoData]
public async void GetAll_GivenRecordsExist_ShouldReturnCollection(List<Movie> movies)
{
    // Arrange
    await moviesDatabaseHelper.AddRecordsAsync(movies);

    // Act
    var result = await this.sut.GetAll();

    // Assert
    result.Should().BeEquivalentTo(movies, x => x.Excluding(p => p.ReleaseDate).Excluding(p => p.CreatedAt).Excluding(p => p.UpdatedAt));
}
```

### Create Tests
First test for `Create` is straight forward, its going to call `Create` to create a record and then load that record using `moviesDatabaseHelper` and compare it with passed parameter.
```csharp
[Theory]
[AutoData]
public async void Create_GivenRecordDoesNotExist_ShouldCreateRecord(CreateMovieParams createMovieParams)
{
    // Arrange
    // Act
    await sut.Create(createMovieParams);
    moviesDatabaseHelper.TrackId(createMovieParams.Id);

    // Assert
    var createdMovie = await moviesDatabaseHelper.GetRecordAsync(createMovieParams.Id);

    createdMovie.Should().BeEquivalentTo(createMovieParams, x => x.Excluding(p => p.ReleaseDate));
    createdMovie.ReleaseDate.Should().BeCloseTo(createMovieParams.ReleaseDate, TimeSpan.FromSeconds(1));
    createdMovie.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    createdMovie.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
}
```
2nd test is to check if the method thorws an exeption if the id already exists. We will use `moviesDatabaseHelper` to add a new record first and then try to create a new record.
```csharp
[Theory]
[AutoData]
public async void Create_GivenRecordWithIdExists_ShouldThrowDuplicateKeyException(Movie movie)
{
    // Arrange
    await moviesDatabaseHelper.AddRecordAsync(movie);

    var createMovieParams = new CreateMovieParams(movie.Id, movie.Title, movie.Director, movie.TicketPrice, movie.ReleaseDate);

    // Act & Assert
    await Assert.ThrowsAsync<DuplicateKeyException>(async () => await sut.Create(createMovieParams));
}
```

### Update Tests
To test update, first we will create a record and then call the `Update` method of store to update the record. After updating we will use the `moviesDatabaseHelper` to load the saved record and verify if the saved record has expected values.
```csharp
[Theory]
[AutoData]
public async void Update_GivenRecordExists_ShouldUpdateRecord(Movie movie, UpdateMovieParams updateMovieParams)
{
    // Arrange
    await moviesDatabaseHelper.AddRecordAsync(movie);

    // Act
    await sut.Update(movie.Id, updateMovieParams);

    // Assert
    var saved = await moviesDatabaseHelper.GetRecordAsync(movie.Id);

    saved.Should().BeEquivalentTo(updateMovieParams, x => x.Excluding(p => p.ReleaseDate));
    saved.ReleaseDate.Should().BeCloseTo(updateMovieParams.ReleaseDate, TimeSpan.FromSeconds(1));
    saved.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
}
```

### Delete Tests
To test delete, first we will add a new record using `moviesDatabaseHelper`, then call `Delete` method on store. To verify we will load the record and then assert the loaded values is `null`.
```csharp
[Theory]
[AutoData]
public async void Delete_GivenRecordExists_ShouldDeleteRecord(Movie movie)
{
    // Arrange
    await moviesDatabaseHelper.AddRecordAsync(movie);

    // Act
    await sut.Delete(movie.Id);

    // Assert
    var loaded = await moviesDatabaseHelper.GetRecordAsync(movie.Id);
    loaded.Should().BeNull();
}
```

This concludes the integration tests. Running these tests does need we start the databaes server prior to running the tests and run the migrations before running the tests. If the database is not running then the tests would not run.

## Integration Tests in CI
Next step would be to run these integration tests in continuous integration pipeline. First step for that would be to extract the connection string used in our integration tests as a configuration that can be passed through environment variables.

Running integration tests in CI is dependent on having access to a database server as these tests are testing the integration of our service boundary with the database server.

We can have a dedicated database instance we use for integration tests in CI pipeline. Our tests need to be tolerant of the presence of data inserted by other CI runs. However it would incur some costs to keep that instance running. Another option can be to use a `dev` instance that is also used in CI pipeline, again tests need to be tolerant of the presence of other tests and dev data.

In the age of containerization we can leverage [Docker](https://www.docker.com/) to spin up a clean database container for each CI run,
apply migrations and then execute tests. We have 2 options here
1. We use our CI pipeline to manage containers and as pre step start up database container and execute migrations before running integration tests.
2. We bake pre step in our integration tests to start up database container and apply migrations before executing integration tests.

I will touch on these 2 in next posts.

## References
In no particular order  
* [REST API with ASP.NET Core 7 and Postgres](https://kashifsoofi.github.io/aspnetcore/rest/postgres/restapi-with-asp.net-core-7-and-postgres/)
* [Postgres Database](https://www.postgresql.org/)
* [Integration Testing](https://en.wikipedia.org/wiki/Integration_testing)
* [Dapper](https://github.com/DapperLib/Dapper)
* [Npgsql](https://www.npgsql.org/doc/index.html)
* [AutoFixture](https://github.com/AutoFixture/AutoFixture)
* [FluentAssertions](https://fluentassertions.com/)
* [Docker](https://www.docker.com/)
* And many more