# Integration Test Postgres Store with testcontainers-go
This is a continuation of an earlier post [Integration Test Postgres Store (go)](https://kashifsoofi.github.io/go/testing/integrationtest/postgres/integration-test-postgres-go/). In this tutorial I will extend the sample to use [testcontainers-go](https://golang.testcontainers.org) to spin up database container and apply migrations before executing our integration tests.

Prior to this sample, pre-requisite of running integration tests was that database server is running either on machine or in a container and migrations are applied. This update will remove that manual step.

## Setup
We would need to start 2 containers before running our integration tests.
* Database Container - hosting the database server
* Migrations Container - container to apply database migrations

## Database Container
Let's start by adding a new file `containers.go` under `integrationtests` folder. If there are multiple tests feell free to add a separate `testhelper` directory to add common code. When moving common code to separate package remember to make the types and methods public.

We will create a struct and embed `postgres.PostgresContainer` from `testcontainers-go/modules/postgres` module. We would also add a `connectionString` for convenience. Then we will add a new method that would start the container and set the `connentionString` before returning to caller.
```go
import (
	"context"
	"strings"
	"time"

	"github.com/testcontainers/testcontainers-go"
	"github.com/testcontainers/testcontainers-go/modules/postgres"
	"github.com/testcontainers/testcontainers-go/wait"
)

type postgresContainer struct {
	*postgres.PostgresContainer
	connectionString string
}

func WithNetwork(network string) testcontainers.CustomizeRequestOption {
	return func(req *testcontainers.GenericContainerRequest) {
		req.Networks = []string{
			network,
		}
	}
}

func WithName(name string) testcontainers.CustomizeRequestOption {
	return func(req *testcontainers.GenericContainerRequest) {
		req.Name = name
	}
}

func createPostgresContainer(ctx context.Context) (*postgresContainer, error) {
	pgContainer, err := postgres.RunContainer(
		ctx,
		testcontainers.WithImage("postgres:14-alpine"),
		postgres.WithDatabase("moviesdb"),
		postgres.WithUsername("postgres"),
		postgres.WithPassword("Password123"),
		testcontainers.WithWaitStrategy(
			wait.ForLog("database system is ready to accept connections").
				WithOccurrence(2).WithStartupTimeout(5*time.Second)),
		WithName("movies.db"),
		WithNetwork("testcontainers-go"),
	)
	if err != nil {
		return nil, err
	}
	connStr, err := pgContainer.ConnectionString(ctx, "sslmode=disable")
	if err != nil {
		return nil, err
	}

	return &postgresContainer{
		PostgresContainer: pgContainer,
		connectionString:  connStr,
	}, nil
}
```

### Migrations Cotnainer
We will add another method in `containers.go` file to create and run migrations container by passing database container's IP address. To make this work we would need to run it in the same network we are running our database container i.e. `testcontainers-go`. First step of the method is to create a connection string, we are using the same values except for the database container IP but all these can be made configureable. Instead of keeping migrations container running we will wait until it exits.
```go
func createMigrationsContainer(ctx context.Context, dbHostIP string) (testcontainers.Container, error) {
	connectionString := fmt.Sprintf("postgresql://postgres:Password123@%s:5432/moviesdb?sslmode=disable", dbHostIP)
	req := testcontainers.ContainerRequest{
		FromDockerfile: testcontainers.FromDockerfile{
			Context:    "../db",
			Dockerfile: "Dockerfile",
		},
		WaitingFor: wait.ForExit().WithExitTimeout(10 * time.Second),
		Cmd:        []string{connectionString, "up"},
		Networks:   []string{"testcontainers-go"},
		Name:       "movies.db.migrations",
	}

	migrationsContainer, err := testcontainers.GenericContainer(ctx, testcontainers.GenericContainerRequest{
		ContainerRequest: req,
		Started:          true,
	})
	if err != nil {
		return nil, err
	}

	return migrationsContainer, nil
}
```

## Test Suite
We will add a test suite using `testify/suite` to test our `PostgresMoviesStore`. This would allow us the use the common setup of starting up database container and applying migrations before running all tests and terminating the running containers after running the tests.
In `SetupSuite` we will start database container. Then apply migrations using migrations container by passing connection string to started database server. And finally initialise our `sut` (system under test) and `dbHelper` using the same connection string.

In `TearDownSuite` we simply terminate both `migrations` and `database` containers that we started in `SetupSuite`.

```go
type postgresMoviesStoreTestSuite struct {
	suite.Suite
	databaseContainer   *postgresContainer
	migrationsContainer testcontainers.Container
	sut                 *store.PostgresMoviesStore
	ctx                 context.Context
	dbHelper            *databaseHelper
	fake                faker.Faker
}

func (suite *postgresMoviesStoreTestSuite) SetupSuite() {
	suite.ctx = context.Background()
	pgContainer, err := createPostgresContainer(suite.ctx)
	if err != nil {
		log.Fatal(err)
	}
	suite.databaseContainer = pgContainer

	dbHostIP, _ := suite.databaseContainer.ContainerIP(suite.ctx)
	migrationsContainer, err := createMigrationsContainer(suite.ctx, dbHostIP)
	if err != nil {
		log.Fatal(err)
	}
	suite.migrationsContainer = migrationsContainer

	suite.sut = store.NewPostgresMoviesStore(suite.databaseContainer.connectionString)
	suite.dbHelper = newDatabaseHelper(suite.databaseContainer.connectionString)
	suite.fake = faker.New()
}

func (suite *postgresMoviesStoreTestSuite) TearDownSuite() {
	if err := suite.migrationsContainer.Terminate(suite.ctx); err != nil {
		log.Fatalf("error terminating migrations container: %s", err)
	}
	if err := suite.databaseContainer.Terminate(suite.ctx); err != nil {
		log.Fatalf("error terminating database container: %s", err)
	}
}

...

func TestPostgresMoviesStoreTestSuite(t *testing.T) {
	suite.Run(t, new(postgresMoviesStoreTestSuite))
}
```

### Helper Methods
Helper method to create random data are updated to hang off the `postgresMoviesStoreTestSuite`.
```go
func (suite *postgresMoviesStoreTestSuite) createMovie() store.Movie {
	m := store.Movie{}
	suite.fake.Struct().Fill(&m)
	m.ReleaseDate = suite.fake.Time().Time(time.Now()).UTC()
	m.TicketPrice = math.Round(m.TicketPrice*100) / 100
	m.CreatedAt = time.Now().UTC()
	m.UpdatedAt = time.Now().UTC()
	return m
}

func (suite *postgresMoviesStoreTestSuite) createMovies(n int) []store.Movie {
	movies := []store.Movie{}
	for i := 0; i < n; i++ {
		m := suite.createMovie()
		movies = append(movies, m)
	}
	return movies
}
```

### Tests
Tests remain the same as previous post. Only difference is now they hang off the `postgresMoviesStoreTestSuite` and use suite's fields. I am adding these here for easy visibility.
```go
func (suite *postgresMoviesStoreTestSuite) TestGetAll() {
	t := suite.T()

	t.Run("given no records, should return empty array", func(t *testing.T) {
		storeMovies, err := suite.sut.GetAll(suite.ctx)

		assert.Nil(t, err)
		assert.Empty(t, storeMovies)
		assert.Equal(t, len(storeMovies), 0)
	})

	t.Run("given records exist, should return array", func(t *testing.T) {
		movies := suite.createMovies(3)
		err := suite.dbHelper.AddMovies(suite.ctx, movies)
		assert.Nil(t, err)

		defer func() {
			ids := []uuid.UUID{}
			for _, m := range movies {
				ids = append(ids, m.ID)
			}
			err := suite.dbHelper.CleanupMovies(suite.ctx, ids...)
			assert.Nil(t, err)
		}()

		storeMovies, err := suite.sut.GetAll(suite.ctx)

		assert.Nil(t, err)
		assert.NotEmpty(t, storeMovies)
		assert.GreaterOrEqual(t, len(storeMovies), len(movies))
		for _, m := range movies {
			for _, sm := range storeMovies {
				if m.ID == sm.ID {
					assertMovieEqual(t, m, sm)
					continue
				}
			}
		}
	})
}

func (suite *postgresMoviesStoreTestSuite) TestGetByID() {
	t := suite.T()

	t.Run("given record does not exist, should return error", func(t *testing.T) {
		id, err := uuid.Parse(suite.fake.UUID().V4())
		assert.NoError(t, err)

		_, err = suite.sut.GetByID(suite.ctx, id)

		var targetErr *store.RecordNotFoundError
		assert.ErrorAs(t, err, &targetErr)
	})

	t.Run("given record exists, should return record", func(t *testing.T) {
		movie := suite.createMovie()
		err := suite.dbHelper.AddMovie(suite.ctx, movie)
		assert.Nil(t, err)

		defer func() {
			err := suite.dbHelper.DeleteMovie(suite.ctx, movie.ID)
			assert.Nil(t, err)
		}()

		storeMovie, err := suite.sut.GetByID(suite.ctx, movie.ID)

		assert.Nil(t, err)
		assertMovieEqual(t, movie, storeMovie)
	})
}

func (suite *postgresMoviesStoreTestSuite) TestCreate() {
	t := suite.T()

	t.Run("given record does not exist, should create record", func(t *testing.T) {
		p := store.CreateMovieParams{}
		suite.fake.Struct().Fill(&p)
		p.TicketPrice = math.Round(p.TicketPrice*100) / 100
		p.ReleaseDate = suite.fake.Time().Time(time.Now()).UTC()

		err := suite.sut.Create(suite.ctx, p)

		assert.Nil(t, err)
		defer func() {
			err := suite.dbHelper.DeleteMovie(suite.ctx, p.ID)
			assert.Nil(t, err)
		}()

		m, err := suite.dbHelper.GetMovie(suite.ctx, p.ID)
		assert.Nil(t, err)
		expected := store.Movie{
			ID:          p.ID,
			Title:       p.Title,
			Director:    p.Director,
			ReleaseDate: p.ReleaseDate,
			TicketPrice: p.TicketPrice,
			CreatedAt:   time.Now().UTC(),
			UpdatedAt:   time.Now().UTC(),
		}
		assertMovieEqual(t, expected, m)
	})

	t.Run("given record with id exists, should return DuplicateKeyError", func(t *testing.T) {
		movie := suite.createMovie()
		err := suite.dbHelper.AddMovie(suite.ctx, movie)
		assert.Nil(t, err)
		defer func() {
			err := suite.dbHelper.DeleteMovie(suite.ctx, movie.ID)
			assert.Nil(t, err)
		}()

		p := store.CreateMovieParams{
			ID:          movie.ID,
			Title:       movie.Title,
			Director:    movie.Director,
			ReleaseDate: movie.ReleaseDate,
			TicketPrice: movie.TicketPrice,
		}

		err = suite.sut.Create(suite.ctx, p)

		assert.NotNil(t, err)
		var targetErr *store.DuplicateKeyError
		assert.ErrorAs(t, err, &targetErr)
	})
}

func (suite *postgresMoviesStoreTestSuite) TestUpdate() {
	t := suite.T()

	t.Run("given record exists, should update record", func(t *testing.T) {
		movie := suite.createMovie()
		err := suite.dbHelper.AddMovie(suite.ctx, movie)
		assert.Nil(t, err)
		defer func() {
			err := suite.dbHelper.DeleteMovie(suite.ctx, movie.ID)
			assert.Nil(t, err)
		}()

		p := store.UpdateMovieParams{
			Title:       suite.fake.RandomStringWithLength(20),
			Director:    suite.fake.Person().Name(),
			ReleaseDate: suite.fake.Time().Time(time.Now()).UTC(),
			TicketPrice: math.Round(suite.fake.RandomFloat(2, 1, 100)*100) / 100,
		}

		err = suite.sut.Update(suite.ctx, movie.ID, p)

		assert.Nil(t, err)

		m, err := suite.dbHelper.GetMovie(suite.ctx, movie.ID)
		assert.Nil(t, err)
		expected := store.Movie{
			ID:          movie.ID,
			Title:       p.Title,
			Director:    p.Director,
			ReleaseDate: p.ReleaseDate,
			TicketPrice: p.TicketPrice,
			CreatedAt:   movie.CreatedAt,
			UpdatedAt:   time.Now().UTC(),
		}
		assertMovieEqual(t, expected, m)
	})
}

func (suite *postgresMoviesStoreTestSuite) TestDelete() {
	t := suite.T()

	t.Run("given record exists, should delete record", func(t *testing.T) {
		movie := suite.createMovie()
		err := suite.dbHelper.AddMovie(suite.ctx, movie)
		assert.Nil(t, err)
		defer func() {
			err := suite.dbHelper.DeleteMovie(suite.ctx, movie.ID)
			assert.Nil(t, err)
		}()

		err = suite.sut.Delete(suite.ctx, movie.ID)

		assert.Nil(t, err)

		_, err = suite.dbHelper.GetMovie(suite.ctx, movie.ID)
		assert.NotNil(t, err)
		assert.ErrorContains(t, err, "sql: no rows in result set")
	})
}
```

## Run Integration Tests
Run following `go test` command to run integration tests. Please note now we don't need to setup `DATABASE_URL` environment variable before running the tests as our setup takes care of starting up database server and getting connection string for that server and passing on to `migrationsContainer`, `sut` and `dbHelper`.
```shell
go test ./integrationtests
```

## Integration Tests in CI
I have also added [GitHub Actions](https://github.com/features/actions) workflow to run these integration tests as part of the CI when a change is pushed to `main` branch.

We will use the standard steps defined in [Building and testing Go](https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-go) guide. Running database server and migrations would be taken care by `SetupSuite`.

Here is the complete listing of the workflow.
```yaml
name: Integration Test Postgres (testcontainers-go)

on:
  push:
    branches: [ "main" ]
    paths:
     - 'integration-test-postgres-with-testcontainers-go/**'
    
jobs:
  build:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: integration-test-postgres-with-testcontainers-go

    steps:
      - uses: actions/checkout@v3
      - name: Set up Go
        uses: actions/setup-go@v4
        with:
          go-version: '1.20'
      - name: Build
        run: go build -v ./...
      - name: Run integration tests
        run: go test ./integrationtests
```

## Source
Source code for the demo application is hosted on GitHub in [blog-code-samples](https://github.com/kashifsoofi/blog-code-samples/tree/main/integration-test-postgres-with-testcontainers-go) repository.

Source for `Integration Test Postgres (testcontainers-go)` workflow is in [integration-test-postgres-testcontainers-go.yml](https://github.com/kashifsoofi/blog-code-samples/blob/main/.github/workflows/integration-test-postgres-testcontainers-go.yml).

## References
In no particular order
* [Integration Test Postgres Store (go)](https://kashifsoofi.github.io/go/testing/integrationtest/postgres/integration-test-postgres-go/)
* [Postgres](https://www.postgresql.org/)
* [Docker](https://www.docker.com/)
* [envconfig](https://github.com/kelseyhightower/envconfig)
* [chi](https://github.com/go-chi/chi)
* [migrate](https://github.com/golang-migrate/migrate)
* [sqlx](https://github.com/jmoiron/sqlx)
* [faker](https://github.com/jaswdr/faker)
* [testify](https://github.com/stretchr/testify)
* [Testcontainers for Go!](https://golang.testcontainers.org/)
* [Getting started with Testcontainers for Go](https://testcontainers.com/guides/getting-started-with-testcontainers-for-go/)
* [GitHub Actions](https://github.com/features/actions)
* [Building and testing Go](https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-go)
* And many more