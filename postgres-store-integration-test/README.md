# REST API using ASP.NET Core 7 with Postgres
This is a continuation of an earlier post [REST API with ASP.NET Core 7 and InMemory Store](https://kashifsoofi.github.io/aspnetcore/rest/restapi-with-asp.net-core-7-and-inmemory-store/). In this tutorial I will extend the service to store data in a [Postgres Database](https://www.postgresql.org/). I will use [Docker](https://www.docker.com/) to run Postgres and use the same to run database migrations.

## Setup Database Server
I will be using a docker-compose to run Postgres in a docker container. This would allow us the add more services that our rest api is depenedent on e.g. redis server for distributed caching.

Let's start by adding a new file by right clicking on Solution name in Visual Studio and Add New File. I like to name file as `docker-compose.dev-env.yml`, feel free to name it as you like. Add following content to add a database instance for movies rest api.
```yaml
version: '3.7'

services:
  movies.db:
    image: postgres:14-alpine
    environment:
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=Password123
      - POSTGRES_DB=moviesdb
    volumes:
      - moviesdbdata:/var/lib/postgresql/data/
    ports:
      - “5432:5432”
    restart: on-failure
    healthcheck:
      test: [ “CMD-SHELL”, “pg_isready -q -d $${POSTGRES_DB} -U $${POSTGRES_USER}“]
      timeout: 10s
      interval: 5s
      retries: 10

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
- `20230518_1800_extension_uuid_ossp_create.sql`
```sql
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
```
- `20230518_1801_table_movies_create.sql`
```sql
CREATE TABLE IF NOT EXISTS movies (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    title VARCHAR(100) NOT NULL,
    director VARCHAR(100) NOT NULL,
    release_date TIMESTAMP NOT NULL,
    ticket_price DECIMAL(12, 2) NOT NULL,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT (now() AT TIME ZONE 'utc') NOT NULL,
    updated_at TIMESTAMP WITHOUT TIME ZONE DEFAULT (now() AT TIME ZONE 'utc') NOT NULL
)
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
    command: '"Host=movies.db;Username=postgres;Password=Password123;Database=moviesdb;Integrated Security=false;"'
```

Open a terminal at the root of the solution where docker-compose file is location and execute following command to start database server and apply migrations to create `uuid-ossp` extension and `movies` table.
```shell
docker-compose -f docker-compose.dev-env.yml up -d
```

## Postgres Movies Store
I will be using [Dapper](https://github.com/DapperLib/Dapper) - a simple object mapper for .Net along with [Npgsql](https://www.npgsql.org/doc/index.html).

### Setup
* Lets start by adding nuget packages
```shell
dotnet add package Npgsql --version 8.0.0-preview.4
dotnet add package Dapper --version 2.0.123
```
* Update `IMovieStore` and make all methods `async`.
* Update `Controller` to make methods `async` and `await` calls to store methods
* Update `InMemoryMoviesStore` to make methods `async`

### SqlHelper
I have added a helper class under `Store` folder named `SqlHelper`. It loads embedded resources under the `Sql` folder with extension `.sql` where the class containing the instance of thhe helper is. Reason for this is I like to have each `SQL` query in its own file. Feel free to put the query directly in the methods.

### Class and Constructor
Add a new folder under `Store`, I named it as `Postgres` and add a file named `PostgresMoviesStore.cs`. This class would accept an `IConfiguration` as parameter that we would use to load postgres connection string from .NET configuration. We would initialize `connectionString` and `sqlHelper` member variables in constructor.
```csharp
public PostgresMoviesStore(IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("MoviesDb");
    if (connectionString == null)
    {
        throw new InvalidOperationException("Missing [MoviesDb] connection string.");
    }

    this.connectionString = connectionString;
    sqlHelper = new SqlHelper<PostgresMoviesStore>();
}
```

I have specified this in `appsettings.json` configuration file. This is acceptable for development but NEVER put a production/stagging connection string in a configuration file. This can be put in secure vault e.g. AWS Parameter Store or Azure KeyVault and can be accessed from the application. CD pipeline can also be configured to load this value from a secure location and set as an environment variable for the container running the application.

### Create
We create a new instance of `NpgsqlConnection`, setup parameters for create and execute the query using `Dapper` to insert a new record, we are handling a `NpgsqlException` and throw our custom `DuplicateKeyException` if `SqlState` of exception is `23505`.
Create function looks like
```csharp
public async Task Create(CreateMovieParams createMovieParams)
{
    await using var connection = new NpgsqlConnection(connectionString);
    {
        var parameters = new
        {
            id = createMovieParams.Id,
            title = createMovieParams.Title,
            director = createMovieParams.Director,
            release_date = createMovieParams.ReleaseDate,
            ticket_price = createMovieParams.TicketPrice,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
        };

        try
        {
            await connection.ExecuteAsync(
                this.sqlHelper.GetSqlFromEmbeddedResource("Create"),
                parameters,
                commandType: CommandType.Text);
        }
        catch (NpgsqlException ex)
        {
            if (ex.SqlState == "23505")
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
)
```

Please note the column names and parameter names to conform to postgresql conventions.

### GetAll
We create a new instance of `NpgsqlConnection`, use `Dapper` to execute query, dapper would map the columns to properties.
```csharp
public async Task<IEnumerable<Movie>> GetAll()
{
    await using var connection = new NpgsqlConnection(connectionString);
    return await connection.QueryAsync<Movie>(
        sqlHelper.GetSqlFromEmbeddedResource("GetAll"),
        commandType: CommandType.Text
        );
}
```
And corresponding sql query from `GetAll.sql` file
```sql
SELECT
    id,
    title,
    director,
    ticket_price as TicketPrice,
    release_date as ReleaseDate,
    created_at as CreatedAt,
    updated_at as UpdatedAt
FROM movies
```
Please note the column alias in the `SELECT` this is required as `Dapper` at the time of reading does not support mapping snake_case columns to camelCase/PascalCase property names.

### GetById
We create a new instance of `NpgsqlConnection`, use `Dapper` to execute query by passing the id, dapper would map the columns to properties.
```csharp
public async Task<Movie?> GetById(Guid id)
{
    await using var connection = new NpgsqlConnection(connectionString);
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
    id,
    title,
    director,
    ticket_price as TicketPrice,
    release_date as ReleaseDate,
    created_at as CreatedAt,
    updated_at as UpdatedAt
FROM movies
WHERE id = @id
```

### Update
We create a new instance of `NpgsqlConnection`, setup parameters for query and execute the query using `Dapper` to update an existing record.

Create function looks like
```csharp
public async Task Update(Guid id, UpdateMovieParams updateMovieParams)
{
    await using var connection = new NpgsqlConnection(connectionString);
    {
        var parameters = new
        {
            id = id,
            title = updateMovieParams.Title,
            director = updateMovieParams.Director,
            release_date = updateMovieParams.ReleaseDate,
            ticket_price = updateMovieParams.TicketPrice,
            updated_at = DateTime.UtcNow,
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
UPDATE movies
SET
    title = @title,
    director = @director,
    release_date = @release_date,
    ticket_price = @ticket_price,
    updated_at = @updated_at
WHERE id = @id
```

### Delete
We create a new instance of `NpgsqlConnection`, use `Dapper` to execute query by passing the id, dapper would map the columns to properties.
```csharp
public async Task Delete(Guid id)
{
    await using var connection = new NpgsqlConnection(connectionString);
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
FROM movies
WHERE id = @id
```

Please note we don't throw `RecordNotFoundException` exception as we were doing in `InMemoryMoviesStore`, reason for that is trying to delete a record with a non existent key is not considered an error in Postgres.

## Setup Dependency Injection
Final step is to setup the Dependency Injection container to wireup the new created store. Update `Program.cs` as shown below
```csharp
// builder.Services.AddSingleton<IMoviesStore, InMemoryMoviesStore>();
builder.Services.AddScoped<IMoviesStore, PostgresMoviesStore>();
```

For simplicity I have disabled `InMemoryMoviesStore`, we can add a configuration and based on that decide which service to use at runtime. That can be a good exercise however we don't do that practically. However for traffic heavy services InMemory or Distributed Cache is used to cache results to improve performance.

## Test
I am not adding any unit or integration tests for this tutorial, perhaps a following tutorial. But all the endpoints can be tested either by the Swagger UI by running the application or using Postman.

## References
In no particular order  
* [REST API using C# .NET 7 with InMemory Store](https://kashifsoofi.github.io/aspnetcore/rest/aspnetcore-restapi/)
* [Postgres Database](https://www.postgresql.org/)
* [roundhouse](https://github.com/chucknorris/roundhouse)
* [dotnet-script](https://github.com/dotnet-script/dotnet-script)
* [Dapper](https://github.com/DapperLib/Dapper)
* [Npgsql](https://www.npgsql.org/doc/index.html)
* [SqlState 23505](https://stackoverflow.com/questions/24390820/postgresql-error-23505-duplicate-key-value-violates-unique-constraint-foo-col)
* And many more