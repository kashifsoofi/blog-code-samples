# REST API with Go, Chi, SQL Server and sqlx
This is a continuation of an earlier post [REST API with Go, Chi and InMemory Store](https://kashifsoofi.github.io/go/rest/restapi-with-go-chi-and-inmemory-store/). In this tutorial I will extend the service to store data in a [Microsoft SQL Server](https://www.microsoft.com/en-gb/sql-server) database. I will be using [Microsoft SQL Server - Ubuntu based images](https://hub.docker.com/_/microsoft-mssql-server) for this sample. I will use [Docker](https://www.docker.com/) to run SQL Server and use the same to run database migrations.

## Setup Database Server
I will be using a docker-compose to run SQL Server in a docker container. This would allow us the add more services that our rest api is depenedent on e.g. redis server for distributed caching.

We will be using a custom image for our instance of SQL Server. Reason for this is that SQL Server container does not have built in funcationality to create a custom application database that MySQL and Postgres provide using environment variables. We have a `setup-db.sql` that we will copy in our custom image and execute as part of the health check in `docker-compose` configuration.

Let's start by adding `Dockerfile.db` under `db` folder.
```Dockerfile
FROM mcr.microsoft.com/mssql/server:2022-latest

WORKDIR /scripts

COPY setup-db.sql /scripts/setup-db.sql

ENTRYPOINT [ "/opt/mssql/bin/sqlservr" ]
```
Here is the `setup-db.sql` file, also located in `db` folder, I am only creating a database but this is a good place to add an application user, role and set permissions.
```sql
IF NOT EXISTS(SELECT * FROM sys.databases WHERE name = 'Movies')
BEGIN
    CREATE DATABASE Movies
    SELECT 'READY'
END
```

Let's add a new file named as `docker-compose.dev-env.yml`, feel free to name it as you like. Add following content to add a database instance for movies rest api.
```yaml
version: '3.7'

services:
  movies.db:
    image: movies.db
    build:
      context: ./db/
      dockerfile: Dockerfile.db
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=Password123
      - MSSQL_PID=Express
    volumes:
      - moviesdbdata:/var/opt/mssql
    ports:
      - "1433:1433"
    healthcheck:
      test: '/opt/mssql-tools/bin/sqlcmd -U sa -P Password123 -i /scripts/setup-db.sql | grep -q "READY"'
      timeout: 20s
      interval: 10s
      retries: 10

volumes:
  moviesdbdata:
```

Open a terminal at the root of the solution where docker-compose file is location and execute following command to start database server.
```shell
docker-compose -f docker-compose.dev-env.yml up -d
```

## Database Migrations
Before we can start using SQL Server we need to create a table to store our data. I will be using excellent [migrate](https://github.com/golang-migrate/migrate) database migrations tool, it can also be imported as a libraray.

For migrations I have created a folder `migrations` under `db` folder. I executed following commands to create migrations.
```shell
migrate create -ext sql -dir db/migrations -seq table_movies_create
```
This would create 2 files, for each migration there would be an `up` and a `down` script, `up` would be executed when applying migration and `down` would be executed when rolling back a change.

- `000001_table_movies_create.up.sql`
```sql
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Movies' and xtype='U')
BEGIN
    CREATE TABLE Movies (
        Id          UNIQUEIDENTIFIER    NOT NULL PRIMARY KEY,
        Title       VARCHAR(100)        NOT NULL,
        Director    VARCHAR(100)        NOT NULL,
        ReleaseDate DateTimeOffset      NOT NULL,
        TicketPrice DECIMAL(12, 4)      NOT NULL,
        CreatedAt   DateTimeOffset      NOT NULL,
        UpdatedAt   DateTimeOffset      NOT NULL
    )
END
```
- `000001_table_movies_create.down.sql`
```sql
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Movies' and xtype='U')
BEGIN
    DROP TABLE Movies
END
```

I usually create a container that has all database migrations and tool to execute those migrations. `Dockerfile.migrations` to run database migrations is as follows
```yaml
FROM migrate/migrate

# Copy all db files
COPY ./migrations /migrations

ENTRYPOINT [ "migrate", "-path", "/migrations", "-database"]
CMD ["sqlserver://sa:Password123@movies.db:1433/Movies up"]
```

Add following in `docker-compose.dev-env.yml` file to add migrations container and run migrations on startup. Please remember if you add new migrations, you would need to delete container and `movies.db.migrations` image to add new migration files in the image.
```yaml
  movies.db.migrations:
    depends_on:
      movies.db:
        condition: service_healthy
    image: movies.db.migrations
    build:
      context: ./db/
      dockerfile: Dockerfile.migrations
    command: "sqlserver://sa:Password123@movies.db:1433/Movies up"
```

Open a terminal at the root of the project where docker-compose file is location and execute following command to start database server and apply migrations to create `Movies` schema and `Movies` table.
```shell
docker-compose -f docker-compose.dev-env.yml up -d
```

## SQL Server Movies Store
I will be using [sqlx](https://github.com/jmoiron/sqlx) to execute queries and map columns to struct fields and vice versa, `sqlx` is a library which provides a set of extensions on go's standard `database/sql` library.

Add a new file named `sqlserver_movies_store.go` under `store` folder. Add a new struct `SqlServerMoviesStore` containing `databaseUrl` and a pointer to `sqlx.DB`, also add helper methods to `connect` to database and `close` connection as well. Also note that I have added a `noOpMapper` method and set as MapperFunc of `sqlx.DB`, reason for this is to use the same casing as the struct field name. Default behaviour for `sqlx` is to map field names to lower case column names.
```go
package sqlserver

import (
	"context"

	"github.com/jmoiron/sqlx"
	_ "github.com/microsoft/go-mssqldb"
)

const driverName = "sqlserver"

type SqlServerMoviesStore struct {
	databaseUrl string
	dbx         *sqlx.DB
}

func NewSqlServerMoviesStore(databaseUrl string) *SqlServerMoviesStore {
	return &SqlServerMoviesStore{
		databaseUrl: databaseUrl,
	}
}

func noOpMapper(s string) string { return s }

func (s *SqlServerMoviesStore) connect(ctx context.Context) error {
	dbx, err := sqlx.ConnectContext(ctx, driverName, s.databaseUrl)
	if err != nil {
		return err
	}

	dbx.MapperFunc(noOpMapper)
	s.dbx = dbx
	return nil
}

func (s *SqlServerMoviesStore) close() error {
	return s.dbx.Close()
}
```

### Add db tag
Update `Movie` struct in `movies_store.go` file to add db tag for `ID` field, this allows sqlx to map `ID` field to correct column. Alternative to this is to use the `AS` in select queries or update the column name in database table as `ID`. All other fields will be mapped correctly by using `noOpMapper` from the above section.

```go
type Movie struct {
	ID          uuid.UUID `db:"Id"`
	...
}
```

### Context
We did not make use of the `Context` in the earlier sample `movies-api-with-go-chi-and-memory-store`, now that we are connecting to an external storage and package we are going to use to run queries support methods accepting `Context` we will update our `store.Interface` to accept `Context` and use that when running queries. `store.Interface` will be updated as follows
```go
type Interface interface {
	GetAll(ctx context.Context) ([]Movie, error)
	GetByID(ctx context.Context, id uuid.UUID) (Movie, error)
	Create(ctx context.Context, createMovieParams CreateMovieParams) error
	Update(ctx context.Context, id uuid.UUID, updateMovieParams UpdateMovieParams) error
	Delete(ctx context.Context, id uuid.UUID) error
}
```
We will also need to update `MemoryMoviesStore` methods to accept `Context` to satisfy `store.Interface` and update methods in `movies_handler` to pass request context using `r.Context()` when calling `store` methods.

### Create
We connect to database using `connect` helper method, create a new instance of `Movie` and execute insert query with `NamedExecContext`. We are handling an `error` and return `DuplicateIdError` if returned error contains text `Cannot insert duplicate key`. If insert is successful then we return `nil`.
Create function looks like
```go
func (s *SqlServerMoviesStore) Create(ctx context.Context, createMovieParams CreateMovieParams) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	movie := Movie{
		ID:          createMovieParams.ID,
		Title:       createMovieParams.Title,
		Director:    createMovieParams.Director,
		ReleaseDate: createMovieParams.ReleaseDate,
		TicketPrice: createMovieParams.TicketPrice,
		CreatedAt:   time.Now().UTC(),
		UpdatedAt:   time.Now().UTC(),
	}

	if _, err := s.dbx.NamedExecContext(
		ctx,
		`INSERT INTO Movies
			(Id, Title, Director, ReleaseDate, TicketPrice, CreatedAt, UpdatedAt)
		VALUES
			(:Id, :Title, :Director, :ReleaseDate, :TicketPrice, :CreatedAt, :UpdatedAt)`,
		movie); err != nil {
		if strings.Contains(err.Error(), "Cannot insert duplicate key") {
			return &DuplicateKeyError{ID: createMovieParams.ID}
		}
		return err
	}

	return nil
}
```

### GetAll
We connect to database using `connect` helper method, then use `SelectContext` method of `sqlx` to execute query, `sqlx` would map the columns to fields. If query is successful then we return the slice of loaded movies.
```go
func (s *SqlServerMoviesStore) GetAll(ctx context.Context) ([]Movie, error) {
	err := s.connect(ctx)
	if err != nil {
		return nil, err
	}
	defer s.close()

	var movies []Movie
	if err := s.dbx.SelectContext(
		ctx,
		&movies,
		`SELECT
			Id, Title, Director, ReleaseDate, TicketPrice, CreatedAt, UpdatedAt
		FROM Movies`); err != nil {
		return nil, err
	}

	return movies, nil
}
```

### GetByID
We connect to database using `connect` helper method, then use `GetContext` method to execute select query, `sqlx` would map the columns to fields. If the driver returns `sql.ErrNoRows` then we return `store.RecordNotFoundError`. If successful loaded `movie` record is returned.
Please note `sql.Named` query paramter, this is needed by the sql server driver to pass named parameters.
```go
func (s *SqlServerMoviesStore) GetByID(ctx context.Context, id uuid.UUID) (Movie, error) {
	err := s.connect(ctx)
	if err != nil {
		return Movie{}, err
	}
	defer s.close()

	var movie Movie
	if err := s.dbx.GetContext(
		ctx,
		&movie,
		`SELECT
			Id, Title, Director, ReleaseDate, TicketPrice, CreatedAt, UpdatedAt
		FROM Movies
		WHERE Id = @id`,
		sql.Named("id", id)); err != nil {
		if err != sql.ErrNoRows {
			return Movie{}, err
		}

		return Movie{}, &RecordNotFoundError{}
	}

	return movie, nil
}
```

### Update
We connect to database using `connect` helper method, then use `NamedExecContext` method to execute query to update an existing record.
```go
func (s *SqlServerMoviesStore) Update(ctx context.Context, id uuid.UUID, updateMovieParams UpdateMovieParams) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	movie := Movie{
		ID:          id,
		Title:       updateMovieParams.Title,
		Director:    updateMovieParams.Director,
		ReleaseDate: updateMovieParams.ReleaseDate,
		TicketPrice: updateMovieParams.TicketPrice,
		UpdatedAt:   time.Now().UTC(),
	}

	if _, err := s.dbx.NamedExecContext(
		ctx,
		`UPDATE Movies
		SET Title = :Title, Director = :Director, ReleaseDate = :ReleaseDate, TicketPrice = :TicketPrice, UpdatedAt = :UpdatedAt
		WHERE Id = :Id`,
		movie); err != nil {
		return err
	}

	return nil
}
```

### Delete
We connect to database using `connect` helper method, then execute query to delete an existing record using `ExecContext`.
```go
func (s *SqlServerMoviesStore) Delete(ctx context.Context, id uuid.UUID) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	if _, err := s.dbx.ExecContext(
		ctx,
		`DELETE FROM Movies
		WHERE id = @id`, sql.Named("id", id)); err != nil {
		return err
	}

	return nil
}
```

## Database Configuration
Add a new struct named `Database` in `config.go` and add that to `Configuration` struct as well.
```go
type Configuration struct {
	HTTPServer
	Database
}
...
type Database struct {
	DatabaseURL        string `envconfig:"DATABASE_URL" required:"true"`
	LogLevel           string `envconfig:"DATABASE_LOG_LEVEL" default:"warn"`
	MaxOpenConnections int    `envconfig:"DATABASE_MAX_OPEN_CONNECTIONS" default:"10"`
}
```

## Dependency Injection
Update `main.go` as follows to create a new instance of `SqlServerMoviesStore`, I have opted to create instance of `SqlServerMoviesStore` instead of `MemoryMoviesStore`, solution can be enhanced to create either one of the dependency based on a configuration.
```go
// store := store.NewMemoryMoviesStore()
store := store.NewSqlServerMoviesStore(cfg.DatabaseURL)
```

## Test
I am not adding any unit or integration tests for this tutorial, perhaps a following tutorial. But all the endpoints can be tested either using Postman for by following test plan from [previous article](https://kashifsoofi.github.io/go/rest/restapi-with-go-chi-and-inmemory-store/#testing).

You can start rest api with SQL Server running in docker by executing following
```shell
DATABASE_URL=sqlserver://sa:Password123@localhost:1433/Movies go run main.go
```

## Source
Source code for the demo application is hosted on GitHub in [blog-code-samples](https://github.com/kashifsoofi/blog-code-samples/tree/main/integration-test-sqlserver-go) repository.


## References
In no particular order
* [What is a REST API?](https://www.ibm.com/topics/rest-apis)
* [Microsoft SQL Server](https://www.microsoft.com/en-gb/sql-server)
* [Microsoft SQL Server - Ubuntu based images](https://hub.docker.com/_/microsoft-mssql-server)
* [Docker](https://www.docker.com/)
* [envconfig](https://github.com/kelseyhightower/envconfig)
* [chi](https://github.com/go-chi/chi)
* [migrate](https://github.com/golang-migrate/migrate)
* [sqlx](https://github.com/jmoiron/sqlx)
* And many more