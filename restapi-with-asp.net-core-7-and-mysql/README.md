# REST API using ASP.NET Core 7 and MySql
This is a continuation of an earlier post [REST API with ASP.NET Core 7 and InMemory Store](https://kashifsoofi.github.io/aspnetcore/rest/restapi-with-asp.net-core-7-and-inmemory-store/). In this tutorial I will extend the service to store data in a [MySQL Database](https://www.mysql.com/). I will use [Docker](https://www.docker.com/) to run MySQL and use the same to run database migrations.

## Setup Database Server
I will be using a docker-compose to run MySQL in a docker container. This would allow us the add more services that our rest api is depenedent on e.g. redis server for distributed caching.

Let's start by adding a new file by right clicking on Solution name in Visual Studio and Add New File. I like to name file as `docker-compose.dev-env.yml`, feel free to name it as you like. Add following content to add a database instance for movies rest api.
```yaml
version: '3.7'

services:
  movies.db:
    image: mysql:5.7
    environment:
      - MYSQL_DATABASE=defaultdb
      - MYSQL_ROOT_PASSWORD=Password123
    volumes:
      - moviesdbdata:/var/lib/postgresql/data/
    ports:
      - "33060:3306"

volumes:
  moviesdbdata:
```

Open a terminal at the root of the solution where docker-compose file is location and execute following command to start database server.
```shell
docker-compose -f docker-compose.dev-env.yml up -d
```

## Database Migrations
Before we can start using Postgres we need to create a table to store our data. I will be using excellent [roundhouse](https://github.com/chucknorris/roundhouse) database deployment system to execute database migrations.

I usually create a container that has all database migrations and tool to execute those migrations. I name migrations as [yyyyMMdd-HHmm-migration-name.sql] but please feel free to use any naming scheme, keep in mind how the tool would order multiple files to run those migrations. I have also added a `wait-for-db.csx` file that I would use as the entry point for database migrations container. This is a `dotnet-script` file and would be run using [dotnet-script](https://github.com/dotnet-script/dotnet-script). I have pinned the versions that are compatible with .net sdk 3.1 as this the version `roundhouse` is build against at the time of writing.

Dockerfile to run database migrations
```yaml
FROM mcr.microsoft.com/dotnet/sdk:3.1-alpine

ENV PATH="$PATH:/root/.dotnet/tools"

RUN dotnet tool install -g dotnet-script --version 1.1.0
RUN dotnet tool install -g dotnet-roundhouse --version 1.3.1

WORKDIR /db

# Copy all db files
COPY . .

ENTRYPOINT ["dotnet-script", "wait-for-db.csx", "--", "rh", "--silent", "--dt", "postgres", "-cs"]
CMD ["Host=movies.db;Username=postgres;Password=Password123;Database=moviesdb;Integrated Security=false;"]
```

For migration, I have added following under `db\up` folder.
- `20230523_1800_schema_create.sql`
```sql
CREATE SCHEMA IF NOT EXISTS Movies;
```
- `20230523_1801_table_movies_create.sql`
```sql
USE Movies;

CREATE TABLE IF NOT EXISTS Movies (
    Id          CHAR(36)        NOT NULL UNIQUE,
    Title       VARCHAR(100)    NOT NULL,
    Director    VARCHAR(100)    NOT NULL,
    ReleaseDate DATETIME        NOT NULL,
    TicketPrice DECIMAL(12, 4)  NOT NULL,
    CreatedAt   DATETIME        NOT NULL,
    UpdatedAt   DATETIME        NOT NULL,
    PRIMARY KEY (Id)
) ENGINE=INNODB;
```

Add following in `docker-compose.dev-env.yml` file to add migrations container and run migrations on startup. Please remember if you add new migrations, you would need to delete container and `movies.db.migrations` image to add new migration files in the container.
```yaml
  movies.db.migrations:
    depends_on:
      - movies.db
    image: movies.db.migrations
    build:
      context: ./db/
      dockerfile: Dockerfile
    command: '"server=movies.db;database=defaultdb;uid=root;password=Password123;SslMode=None;"'
```

Open a terminal at the root of the solution where docker-compose file is location and execute following command to start database server and apply migrations to create schema and `movies` table.
```shell
docker-compose -f docker-compose.dev-env.yml up -d
```

## MySql Movies Store
I will be using [Dapper](https://github.com/DapperLib/Dapper) - a simple object mapper for .Net along with [MySqlConnector](https://mysqlconnector.net/).

### Setup
* Lets start by adding nuget packages
```shell
dotnet add package MySqlConnector --version 2.2.6
dotnet add package Dapper --version 2.0.123
```
* Update `IMovieStore` and make all methods `async`.
* Update `Controller` to make methods `async` and `await` calls to store methods
* Update `InMemoryMoviesStore` to make methods `async`

### SqlHelper
I have added a helper class under `Store` folder named `SqlHelper`. It loads embedded resources under the `Sql` folder with extension `.sql` where the class containing the instance of thhe helper is. Reason for this is I like to have each `SQL` query in its own file. Feel free to put the query directly in the methods.

### Class and Constructor
Add a new folder under `Store`, I named it as `MySql` and add a file named `MySqlMoviesStore.cs`. This class would accept an `IConfiguration` as parameter that we would use to load MySql connection string from .NET configuration. We would initialize `connectionString` and `sqlHelper` member variables in constructor.
```csharp
public MySqlMoviesStore(IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("MoviesDb");
    if (connectionString == null)
    {
        throw new InvalidOperationException("Missing [MoviesDb] connection string.");
    }

    this.connectionString = connectionString;
    sqlHelper = new SqlHelper<MySqlMoviesStore>();
}
```

I have specified this in `appsettings.json` configuration file. This is acceptable for development but NEVER put a production/stagging connection string in a configuration file. This can be put in secure vault e.g. AWS Parameter Store or Azure KeyVault and can be accessed from the application. CD pipeline can also be configured to load this value from a secure location and set as an environment variable for the container running the application.

### Create
We create a new instance of `MySqlConnection`, setup parameters for create and execute the query using `Dapper` to insert a new record, we are handling a `NpgsqlException` and throw our custom `DuplicateKeyException` if `ErrorCode` of exception is `DuplicateKeyEntry`.
Create function looks like
```csharp
public async Task Create(CreateMovieParams createMovieParams)
{
    await using var connection = new MySqlConnection(this.connectionString);
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
        catch (MySqlException ex)
        {
            if (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
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
We create a new instance of `MySqlConnection`, use `Dapper` to execute query, dapper would map the columns to properties.
```csharp
public async Task<IEnumerable<Movie>> GetAll()
{
    await using var connection = new MySqlConnection(this.connectionString);
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
    TicketPrice,
    ReleaseDate,
    CreatedAt,
    UpdatedAt
FROM Movies
```

### GetById
We create a new instance of `MySqlConnection`, use `Dapper` to execute query by passing the id, dapper would map the columns to properties.
```csharp
public async Task<Movie?> GetById(Guid id)
{
    await using var connection = new MySqlConnection(this.connectionString);
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
    TicketPrice,
    ReleaseDate,
    CreatedAt,
    UpdatedAt
FROM Movies
WHERE Id = @Id
```

### Update
We create a new instance of `MySqlConnection`, setup parameters for query and execute the query using `Dapper` to update an existing record.

Update function looks like
```csharp
public async Task Update(Guid id, UpdateMovieParams updateMovieParams)
{
    await using var connection = new MySqlConnection(this.connectionString);
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
We create a new instance of `MySqlConnection`, use `Dapper` to execute query by passing the id.
```csharp
public async Task Delete(Guid id)
{
    await using var connection = new MySqlConnection(this.connectionString);
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
builder.Services.AddScoped<IMoviesStore, MySqlMoviesStore>();
```

For simplicity I have disabled `InMemoryMoviesStore`, we can add a configuration and based on that decide which service to use at runtime. That can be a good exercise however we don't do that practically. However for traffic heavy services InMemory or Distributed Cache is used to cache results to improve performance.

## Test
I am not adding any unit or integration tests for this tutorial, perhaps a following tutorial. But all the endpoints can be tested either by the Swagger UI by running the application or using Postman.

## References
In no particular order
* [REST API with ASP.NET Core 7 and InMemory Store](https://kashifsoofi.github.io/aspnetcore/rest/restapi-with-asp.net-core-7-and-inmemory-store/)
* [MySQL Database](https://www.mysql.com/)
* [roundhouse](https://github.com/chucknorris/roundhouse)
* [dotnet-script](https://github.com/dotnet-script/dotnet-script)
* [Dapper](https://github.com/DapperLib/Dapper)
* [MySqlConnector](https://mysqlconnector.net/)
* And many more