# REST API with Go, Chi and Postgres
This is a continuation of an earlier post [REST API with Go, Chi and InMemory Store](https://kashifsoofi.github.io/go/rest/restapi-with-go-chi-and-inmemory-store/). In this tutorial I will extend the service to store data in a [Postgres](https://www.postgresql.org/) database. I will use [Docker](https://www.docker.com/) to run Postgres and use the same to run database migrations.

## Setup Database Server
I will be using a docker-compose to run Postgres in a docker container. This would allow us the add more services that our rest api is depenedent on e.g. redis server for distributed caching.

Let's start by adding a new file named as `docker-compose.dev-env.yml`, feel free to name it as you like. Add following content to add a database instance for movies rest api.
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
      test: [ “CMD-SHELL”, “pg_isready -q -d moviesdb -U Password123“]
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
Before we can start using Postgres we need to create a table to store our data. I will be using excellent [migrate](https://github.com/golang-migrate/migrate) database migrations tool, it can also be imported as a libraray.

For migrations I have created a folder `db` and another folder named `migrations` under db. I executed following commands to create migrations.
```shell
migrate create -ext sql -dir db/migrations -seq extension_uuid_ossp_create
migrate create -ext sql -dir db/migrations -seq table_movies_create
```
This would create 4 files, for each migration there would be an `up` and a `down` script, `up` would be executed when applying migration and `down` would be executed when rolling back a change.

- `000001_extension_uuid_ossp_create.up.sql`
```sql
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
```
- `000001_extension_uuid_ossp_create.down.sql`
```sql
DROP EXTENSION IF EXISTS "uuid-ossp";
```
- `000002_table_movies_create.up.sql`
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
- `000002_table_movies_create.down.sql`
```sql
DROP TABLE IF EXISTS movies;
```

I usually create a container that has all database migrations and tool to execute those migrations. Dockerfile to run database migrations is as follows
```yaml
FROM migrate/migrate

# Copy all db files
COPY ./migrations /migrations

ENTRYPOINT [ "migrate", "-path", "/migrations", "-database"]
CMD ["postgresql://postgres:Password123@movies.db:5432/moviesdb?sslmode=disable up"]
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
    command: "postgresql://postgres:Password123@movies.db:5432/moviesdb?sslmode=disable up"
```

Open a terminal at the root of the project where docker-compose file is location and execute following command to start database server and apply migrations to create `uuid-ossp` extension and `movies` table.
```shell
docker-compose -f docker-compose.dev-env.yml up -d
```

## Postgres Movies Store
I will be using [sqlx](https://github.com/jmoiron/sqlx) to execute queries and map columns to struct fields and vice versa, `sqlx` is a library which provides a set of extensions on go's standard `database/sql` library.

Add a new folder named `postgres` under `store` and a new file named `postgres_movies_store.go`. Add a new struct `PostgresMoviesStore` containing `databaseUrl` and a pointer to `sqlx.DB`, also add helper methods to `connect` to database and `close` connection as well.
```go
package postgres

import (
	"context"
	"movies-api/store"

	_ "github.com/jackc/pgx/stdlib"
	"github.com/jmoiron/sqlx"
)

const driverName = "pgx"

type PostgresMoviesStore struct {
	databaseUrl string
	dbx         *sqlx.DB
}

func NewInMemoryMoviesStore(databaseUrl string) *PostgresMoviesStore {
	return &PostgresMoviesStore{
		databaseUrl: databaseUrl,
	}
}

func (s *PostgresMoviesStore) connect(ctx context.Context) error {
	dbx, err := sqlx.ConnectContext(ctx, driverName, s.databaseUrl)
	if err != nil {
		return err
	}

	s.dbx = dbx
	return nil
}

func (s *PostgresMoviesStore) close() error {
	return s.dbx.Close()
}
```
### Add db tags
Update `Movie` struct in `movies_store.go` file to add db tags, this allows sqlx to map struct members to column names.
```go
type Movie struct {
	Id          uuid.UUID
	Title       string
	Director    string
	ReleaseDate time.Time `db:"release_date"`
	TicketPrice float64   `db:"ticket_price"`
	CreatedAt   time.Time `db:"created_at"`
	UpdatedAt   time.Time `db:"updated_at"`
}
```

### Create
We connect to database using `connect` helper method, create a new instance of `Movie` and execute insert query with `NamedExecContext`. We are handling an `error` and return `DuplicateIdError` if `SqlState` of exception is `23505`. If insert is successful then we return `nil`.
Create function looks like
```go
func (s *PostgresMoviesStore) Create(ctx context.Context, createMovieParams store.CreateMovieParams) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	movie := store.Movie{
		Id:          createMovieParams.Id,
		Title:       createMovieParams.Title,
		Director:    createMovieParams.Director,
		ReleaseDate: createMovieParams.ReleaseDate,
		TicketPrice: createMovieParams.TicketPrice,
		CreatedAt:   time.Now().UTC(),
		UpdatedAt:   time.Now().UTC(),
	}

	if _, err := s.dbx.NamedExecContext(
		ctx,
		`INSERT INTO movies
			(id, title, director, release_date, ticket_price, created_at, updated_at)
		VALUES
			(:id, :title, :director, :release_date, :ticket_price, :created_at, :updated_at)`,
		movie); err != nil {
		if strings.Contains(err.Error(), "SQLSTATE 23505") {
			return &store.DuplicateIdError{Id: createMovieParams.Id}
		}
		return err
	}

	return nil
}
```

### GetAll
We connect to database using `connect` helper method, then use `SelectContext` method of `sqlx` to execute query, `sqlx` would map the columns to fields. If query is successful then we return the slice of loaded movies.
```go
func (s *PostgresMoviesStore) GetAll(ctx context.Context) ([]*store.Movie, error) {
	err := s.connect(ctx)
	if err != nil {
		return nil, err
	}
	defer s.close()

	var movies []*store.Movie
	if err := s.dbx.SelectContext(
		ctx,
		&movies,
		`SELECT
			id, title, director, release_date, ticket_price, created_at, updated_at
		FROM movies`); err != nil {
		return nil, err
	}

	return movies, nil
}
```

### GetById
We connect to database using `connect` helper method, then use `GetContext` method to execute select query, `sqlx` would map the columns to fields. If the driver returns `sql.ErrNoRows` then we return `store.RecordNotFoundError`. If successful loaded `movie` record is returned.
```go
func (s *PostgresMoviesStore) GetById(ctx context.Context, id uuid.UUID) (*store.Movie, error) {
	err := s.connect(ctx)
	if err != nil {
		return nil, err
	}
	defer s.close()

	var movie store.Movie
	if err := s.dbx.GetContext(
		ctx,
		&movie,
		`SELECT
			id, title, director, release_date, ticket_price, created_at, updated_at
		FROM movies
		WHERE id = $1`,
		id); err != nil {
		if err != sql.ErrNoRows {
			return nil, err
		}

		return nil, &store.RecordNotFoundError{}
	}

	return &movie, nil
}
```

### Update
We connect to database using `connect` helper method, then use `NamedExecContext` method to execute query to update an existing record.
```go
func (s *PostgresMoviesStore) Update(ctx context.Context, id uuid.UUID, updateMovieParams store.UpdateMovieParams) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	movie := store.Movie{
		Id:          id,
		Title:       updateMovieParams.Title,
		Director:    updateMovieParams.Director,
		ReleaseDate: updateMovieParams.ReleaseDate,
		TicketPrice: updateMovieParams.TicketPrice,
		UpdatedAt:   time.Now().UTC(),
	}

	if _, err := s.dbx.NamedExecContext(
		ctx,
		`UPDATE movies
		SET title = :title, director = :director, release_date = :release_date, ticket_price = :ticket_price, updated_at = :updated_at
		WHERE id = :id`,
		movie); err != nil {
		return err
	}

	return nil
}
```

### Delete
We connect to database using `connect` helper method, then execute query to delete an existing record using `ExecContext`.
```go
func (s *PostgresMoviesStore) Delete(ctx context.Context, id uuid.UUID) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	if _, err := s.dbx.ExecContext(
		ctx,
		`DELETE FROM movies
		WHERE id = $1`, id); err != nil {
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
Update `main.go` as follows to create a new instance of `PostgresMoviesStore`, I have opted to create instance of `PostgresMoviesStore` instead of `InMemoryMoviesStore`, solution can be enhanced to create either one of the dependency based on a configuration.
```go
// store := in_memory.NewInMemoryMoviesStore()
store := postgres.NewPostgresMoviesStore(cfg.DatabaseURL)
```

## Test
I am not adding any unit or integration tests for this tutorial, perhaps a following tutorial. But all the endpoints can be tested either using Postman for by following test plan from [previous article](https://kashifsoofi.github.io/go/rest/restapi-with-go-chi-and-inmemory-store/#testing).

You can start rest api with postgres by executing following
```shell
DATABASE_URL=postgresql://postgres:Password123@localhost:5432/moviesdb?sslmode=disable go run main.go
```

## Source
Source code for the demo application is hosted on GitHub in [blog-code-samples](https://github.com/kashifsoofi/blog-code-samples/tree/main/restapi-with-go-chi-and-postgres) repository.


## References
In no particular order
* [What is a REST API?](https://www.ibm.com/topics/rest-apis)
* [Postgres](https://www.postgresql.org/)
* [Docker](https://www.docker.com/)
* [envconfig](https://github.com/kelseyhightower/envconfig)
* [chi](https://github.com/go-chi/chi)
* [migrate](https://github.com/golang-migrate/migrate)
* [sqlx](https://github.com/jmoiron/sqlx)
* And many more