# REST API using ASP.NET Core 7 and MongoDB
This is a continuation of an earlier post [REST API with ASP.NET Core 7 and InMemory Store](https://kashifsoofi.github.io/aspnetcore/rest/restapi-with-asp.net-core-7-and-inmemory-store/). In this tutorial I will extend the service to store data in [MongoDB](https://www.mongodb.com/), I will be using [MongoDB Community Server Docker Image](https://hub.docker.com/r/mongodb/mongodb-community-server) for this sample. I will use [Docker](https://www.docker.com/) to run MongoDB.

## Setup Database Server
I will be using a docker-compose to run SQL Server in a docker container. This would allow us the add more services that our rest api is depenedent on e.g. redis server for distributed caching.

Let's start by adding a new file by right clicking on Solution name in Visual Studio and Add New File. I like to name file as `docker-compose.dev-env.yml`, feel free to name it as you like. Add following content to add a database instance for movies rest api.
```yaml
version: '3.7'

services:
  movies.db:
    image: mongodb/mongodb-community-server:6.0-ubi8
    environment:
      - MONGODB_INITDB_ROOT_USERNAME=root
      - MONGODB_INITDB_ROOT_PASSWORD=Password123
    volumes:
      - moviesdbdata:/data/db
    ports:
      - "27017:27017"

volumes:
  moviesdbdata:
```

Open a terminal at the root of the solution where docker-compose file is location and execute following command to start database server.
```shell
docker-compose -f docker-compose.dev-env.yml up -d
```

## Database Migrations
We would not do any database/schema migrations for MongoDB as its a NoSQL database, [here](https://stackoverflow.com/a/49446108) is an excellent discussion on Stackoverflow on this topic. We don't need any migration for this sample however if the need arise and there is no strong use case of a schema migration script I would prefer to opt the route of supporting multiple schemas conconcurrently and update when required.

## MongoDB Movies Store

### Setup - TODO
* Lets start by adding nuget packages
```shell
dotnet add package Microsoft.Data.SqlClient --version 5.1.1
dotnet add package Dapper --version 2.0.123
```
* Update `IMovieStore` and make all methods `async`.
* Update `Controller` to make methods `async` and `await` calls to store methods
* Update `InMemoryMoviesStore` to make methods `async`

### Class and Constructor
Add a new folder under `Store`, I named it as `SqlServer` and add a file named `SqlServerMoviesStore.cs`. This class would accept an `IConfiguration` as parameter that we would use to load SQL Server connection string from .NET configuration. We would initialize `connectionString` and `sqlHelper` member variables in constructor.
```csharp
public SqlServerMoviesStore(IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("MoviesDb");
    if (connectionString == null)
    {
        throw new InvalidOperationException("Missing [MoviesDb] connection string.");
    }

    this.connectionString = connectionString;
    sqlHelper = new SqlHelper<SqlServerMoviesStore>();
}
```

I have specified this in `appsettings.json` configuration file. This is acceptable for development but NEVER put a production/stagging connection string in a configuration file. This can be put in secure vault e.g. AWS Parameter Store or Azure KeyVault and can be accessed from the application. CD pipeline can also be configured to load this value from a secure location and set as an environment variable for the container running the application.

### Create
We create a new instance of `SqlConnection`, setup parameters for create and execute the query using `Dapper` to insert a new record, we are handling a `SqlException` and throw our custom `DuplicateKeyException` if `Number` of exception is `2627`.

Create function looks like
```csharp
public async Task Create(CreateMovieParams createMovieParams)
{
    await using var connection = new SqlConnection(this.connectionString);
    {
        var parameters = new
        {
            createMovieParams.Id,
            createMovieParams.Title,
            createMovieParams.Director,
            createMovieParams.ReleaseDate,
            createMovieParams.TicketPrice,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        try
        {
            await connection.ExecuteAsync(
                this.sqlHelper.GetSqlFromEmbeddedResource("Create"),
                parameters,
                commandType: CommandType.Text);
        }
        catch (SqlException ex)
        {
            if (ex.Number == 2627)
            {
                throw new DuplicateKeyException();
            }

            throw;
        }
    }
}
```
And corresponding sql query from `Create.sql` file
```sql
INSERT INTO Movies(
    Id,
    Title,
    Director,
    ReleaseDate,
    TicketPrice,
    CreatedAt,
    UpdatedAt
)
VALUES (
    @Id,
    @Title,
    @Director,
    @ReleaseDate,
    @TicketPrice,
    @CreatedAt,
    @UpdatedAt
)
```

Please note the column names and parameter names should match casing as defined in database `up` scripts.

### GetAll
We create a new instance of `SqlConnection`, use `Dapper` to execute query, dapper would map the columns to properties.
```csharp
public async Task<IEnumerable<Movie>> GetAll()
{
    await using var connection = new SqlConnection(this.connectionString);
    return await connection.QueryAsync<Movie>(
        sqlHelper.GetSqlFromEmbeddedResource("GetAll"),
        commandType: CommandType.Text
        );
}
```
And corresponding sql query from `GetAll.sql` file
```sql
SELECT
    Id,
    Title,
    Director,
    ReleaseDate,
    TicketPrice,
    CreatedAt,
    UpdatedAt
FROM Movies
```

### GetById
We create a new instance of `SqlConnection`, use `Dapper` to execute query by passing the id, dapper would map the columns to properties.
```csharp
public async Task<Movie?> GetById(Guid id)
{
    await using var connection = new SqlConnection(this.connectionString);
    return await connection.QueryFirstOrDefaultAsync<Movie?>(
        sqlHelper.GetSqlFromEmbeddedResource("GetById"),
        new { id },
        commandType: System.Data.CommandType.Text
        );
}
```
And coresponding sql from `GetById.sql` file
```sql
SELECT
    Id,
    Title,
    Director,
    ReleaseDate,
    TicketPrice,
    CreatedAt,
    UpdatedAt
FROM Movies
WHERE Id = @Id
```

### Update
We create a new instance of `SqlConnection`, setup parameters for query and execute the query using `Dapper` to update an existing record.

Update function looks like
```csharp
public async Task Update(Guid id, UpdateMovieParams updateMovieParams)
{
    await using var connection = new SqlConnection(this.connectionString);
    {
        var parameters = new
        {
            Id = id,
            updateMovieParams.Title,
            updateMovieParams.Director,
            updateMovieParams.ReleaseDate,
            updateMovieParams.TicketPrice,
            UpdatedAt = DateTime.UtcNow,
        };

        await connection.ExecuteAsync(
            this.sqlHelper.GetSqlFromEmbeddedResource("Update"),
            parameters,
            commandType: CommandType.Text);
    }
}
```
And corresponding sql query from `Update.sql` file
```sql
UPDATE Movies
SET
    Title = @Title,
    Director = @Director,
    ReleaseDate = @ReleaseDate,
    TicketPrice = @TicketPrice,
    UpdatedAt = @UpdatedAt
WHERE id = @id
```

### Delete
We create a new instance of `SqlConnection`, use `Dapper` to execute query by passing the id.
```csharp
public async Task Delete(Guid id)
{
    await using var connection = new SqlConnection(this.connectionString);
    await connection.ExecuteAsync(
        sqlHelper.GetSqlFromEmbeddedResource("Delete"),
        new { id },
        commandType: CommandType.Text
        );
}
```
And corresponding sql query from `Delete.sql` file
```sql
DELETE
FROM Movies
WHERE Id = @Id
```

Please note we don't throw `RecordNotFoundException` exception as we were doing in `InMemoryMoviesStore`, reason for that is trying to delete a record with a non existent key is not considered an error in Postgres.

## Setup Dependency Injection
Final step is to setup the Dependency Injection container to wireup the new created store. Update `Program.cs` as shown below
```csharp
// builder.Services.AddSingleton<IMoviesStore, InMemoryMoviesStore>();
builder.Services.AddScoped<IMoviesStore, SqlServerMoviesStore>();
```

For simplicity I have disabled `InMemoryMoviesStore`, we can add a configuration and based on that decide which service to use at runtime. That can be a good exercise however we don't do that practically. However for traffic heavy services InMemory or Distributed Cache is used to cache results to improve performance.

## Test
I am not adding any unit or integration tests for this tutorial, perhaps a following tutorial. But all the endpoints can be tested either by the Swagger UI by running the application or using Postman.

## References
In no particular order
* [What is a REST API?](https://www.ibm.com/topics/rest-apis)
* [REST API with ASP.NET Core 7 and InMemory Store](https://kashifsoofi.github.io/aspnetcore/rest/restapi-with-asp.net-core-7-and-inmemory-store/)
* [Docker](https://www.docker.com/)
* [MongoDB](https://www.mongodb.com/)
* [MongoDB Community Server Docker Image](https://hub.docker.com/r/mongodb/mongodb-community-server)
* [Install MongoDB Community with Docker](https://www.mongodb.com/docs/manual/tutorial/install-mongodb-community-with-docker/)
* [Schema Migration Scripts in NoSQL Databases](https://stackoverflow.com/a/49446108)
* And many more
