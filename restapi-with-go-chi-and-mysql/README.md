# REST API with Go, Chi and MySQL
This is a continuation of an earlier post [REST API with Go, Chi and InMemory Store](https://kashifsoofi.github.io/go/rest/restapi-with-go-chi-and-inmemory-store/). In this tutorial I will extend the service to store data in a [MySQL](https://www.mysql.com/) database. I will use [Docker](https://www.docker.com/) to run MySQL and use the same to run database migrations.

## Setup Database Server
I will be using a docker-compose to run MySQL in a docker container. This would allow us the add more services that our rest api is depenedent on e.g. redis server for distributed caching.

Let's start by adding a new file named as `docker-compose.dev-env.yml`, feel free to name it as you like. Add following content to add a database instance for movies rest api.
```yaml
version: '3.7'

services:
  movies.db:
    image: mysql:5.7
    environment:
      - MYSQL_ROOT_PASSWORD=Password123
      - MYSQL_DATABASE=moviesdb
    volumes:
      - moviesdbdata:/var/lib/mysql
    ports:
      - "3306:3306"
    healthcheck:
      test: "mysql -uroot -pPassword123 moviesdb -e 'select 1'"
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
Before we can start using MySQL we need to create a table to store our data. I will be using excellent [migrate](https://github.com/golang-migrate/migrate) database migrations tool, it can also be imported as a libraray.

For migrations I have created a folder `db` and another folder named `migrations` under db. I executed following commands to create migrations.
```shell
migrate create -ext sql -dir db/migrations -seq schema_movies_create
migrate create -ext sql -dir db/migrations -seq table_movies_create
```
This would create 4 files, for each migration there would be an `up` and a `down` script, `up` would be executed when applying migration and `down` would be executed when rolling back a change.

- `000001_schema_movies_create.up.sql`
```sql
CREATE SCHEMA IF NOT EXISTS Movies;
```
- `000001_schema_movies_create.down.sql`
```sql
DROP SCHEMA IF EXISTS Movies;
```
- `000002_table_movies_create.up.sql`
```sql
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
- `000002_table_movies_create.down.sql`
```sql
DROP TABLE IF EXISTS Movies;
```

I usually create a container that has all database migrations and tool to execute those migrations. `Dockerfile` to run database migrations is as follows
```yaml
FROM migrate/migrate

# Copy all db files
COPY ./migrations /migrations

ENTRYPOINT [ "migrate", "-path", "/migrations", "-database"]
CMD ["mysql://root:Password123@tcp(movies.db:3306)/moviesdb up"]
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
      dockerfile: Dockerfile
    command: "'mysql://root:Password123@tcp(movies.db:3306)/moviesdb' up"
```

Open a terminal at the root of the project where docker-compose file is location and execute following command to start database server and apply migrations to create `Movies` schema and `Movies` table.
```shell
docker-compose -f docker-compose.dev-env.yml up -d
```

## MySQL Movies Store
I will be using [sqlx](https://github.com/jmoiron/sqlx) to execute queries and map columns to struct fields and vice versa, `sqlx` is a library which provides a set of extensions on go's standard `database/sql` library.

Add a new folder named `mysql` under `store` and a new file named `mysql_movies_store.go`. Add a new struct `MySqlMoviesStore` containing `databaseUrl` and a pointer to `sqlx.DB`, also add helper methods to `connect` to database and `close` connection as well. Also note that I have added a `noOpMapper` method and set as MapperFunc of `sqlx.DB`, reason for this is to use the same casing as the struct field name. Default behaviour for `sqlx` is to map field names to lower case column names.
```go
package mysql

import (
	"context"
	"movies-api/store"

	_ "github.com/go-sql-driver/mysql"
	"github.com/jmoiron/sqlx"
)

const driverName = "mysql"

type MySqlMoviesStore struct {
	databaseUrl string
	dbx         *sqlx.DB
}

func NewMySqlMoviesStore(databaseUrl string) *MySqlMoviesStore {
	return &MySqlMoviesStore{
		databaseUrl: databaseUrl,
	}
}

func noOpMapper(s string) string { return s }

func (s *MySqlMoviesStore) connect(ctx context.Context) error {
	dbx, err := sqlx.ConnectContext(ctx, driverName, s.databaseUrl)
	if err != nil {
		return err
	}

	dbx.MapperFunc(noOpMapper)
	s.dbx = dbx
	return nil
}

func (s *MySqlMoviesStore) close() error {
	return s.dbx.Close()
}
```

### Create
We connect to database using `connect` helper method, create a new instance of `Movie` and execute insert query with `NamedExecContext`. We are handling an `error` and return `DuplicateIDError` if returned error contains text `Error 1062`. If insert is successful then we return `nil`.
Create function looks like
```go
func (s *MySqlMoviesStore) Create(ctx context.Context, createMovieParams store.CreateMovieParams) error {
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
		`INSERT INTO Movies
			(Id, Title, Director, ReleaseDate, TicketPrice, CreatedAt, UpdatedAt)
		VALUES
			(:Id, :Title, :Director, :ReleaseDate, :TicketPrice, :CreatedAt, :UpdatedAt)`,
		movie); err != nil {
		if strings.Contains(err.Error(), "Error 1062") {
			return &store.DuplicateIDError{ID: createMovieParams.Id}
		}
		return err
	}

	return nil
}
```

### GetAll
We connect to database using `connect` helper method, then use `SelectContext` method of `sqlx` to execute query, `sqlx` would map the columns to fields. If query is successful then we return the slice of loaded movies.
```go
func (s *MySqlMoviesStore) GetAll(ctx context.Context) ([]*store.Movie, error) {
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
			Id, Title, Director, ReleaseDate, TicketPrice, CreatedAt, UpdatedAt
		FROM Movies`); err != nil {
		return nil, err
	}

	return movies, nil
}
```
If there is an error in parsing `DATETIME` column, remember to add `parseTime=true` parameter to your connection string.

### GetById
We connect to database using `connect` helper method, then use `GetContext` method to execute select query, `sqlx` would map the columns to fields. If the driver returns `sql.ErrNoRows` then we return `store.RecordNotFoundError`. If successful loaded `movie` record is returned.
```go
func (s *MySqlMoviesStore) GetById(ctx context.Context, id uuid.UUID) (*store.Movie, error) {
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
			Id, Title, Director, ReleaseDate, TicketPrice, CreatedAt, UpdatedAt
		FROM Movies
		WHERE Id = ?`,
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
func (s *MySqlMoviesStore) Update(ctx context.Context, id uuid.UUID, updateMovieParams store.UpdateMovieParams) error {
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
func (s *MySqlMoviesStore) Delete(ctx context.Context, id uuid.UUID) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	if _, err := s.dbx.ExecContext(
		ctx,
		`DELETE FROM Movies
		WHERE id = ?`, id); err != nil {
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
Update `main.go` as follows to create a new instance of `MySqlMoviesStore`, I have opted to create instance of `MySqlMoviesStore` instead of `InMemoryMoviesStore`, solution can be enhanced to create either one of the dependency based on a configuration.
```go
// store := in_memory.NewInMemoryMoviesStore()
store := mysql.NewMySqlMoviesStore(cfg.DatabaseURL)
```

## Test
I am not adding any unit or integration tests for this tutorial, perhaps a following tutorial. But all the endpoints can be tested either using Postman for by following test plan from [previous article](https://kashifsoofi.github.io/go/rest/restapi-with-go-chi-and-inmemory-store/#testing).

You can start rest api with mysql running in docker by executing following
```shell
DATABASE_URL=root:Password123@tcp(localhost:3306)/moviesdb?parseTime=true go run main.go
```

## Source
Source code for the demo application is hosted on GitHub in [blog-code-samples](https://github.com/kashifsoofi/blog-code-samples/tree/main/restapi-with-go-chi-and-mysql) repository.


## References
In no particular order
* [What is a REST API?](https://www.ibm.com/topics/rest-apis)
* [MySQL](https://www.mysql.com/)
* [Docker](https://www.docker.com/)
* [envconfig](https://github.com/kelseyhightower/envconfig)
* [chi](https://github.com/go-chi/chi)
* [migrate](https://github.com/golang-migrate/migrate)
* [sqlx](https://github.com/jmoiron/sqlx)
* [parseTime Parameter](https://stackoverflow.com/questions/26617957/how-to-scan-a-mysql-timestamp-value-into-a-time-time-variable)
* And many more