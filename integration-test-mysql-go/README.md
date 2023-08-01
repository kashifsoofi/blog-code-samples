# Integration Test MySQL Store (go)
This is a continuation of an earlier post [REST API with Go, Chi, MySQL and sqlx](https://kashifsoofi.github.io/go/rest/mysql/sqlx/restapi-with-go-chi-mysql-and-sqlx/). In this tutorial I will extend the sample to add integration tests to verify our implementation of `MySqlMoviesStore`.

## Why Integration Test
As per definition from [Wikipedia](https://en.wikipedia.org/wiki/Integration_testing) integration testing is the phase in which individual software modules are combined and tested as a group.

This is important in our case as we are using an external system to store our data and before we can declare that it is ready to use we need to make sure that it is working as intended.

Our options are
* One way would be to run the database server and our api project and invoke the endpoints either from the Swagger UI, curl or Postman with defined data and then verify if our service is storing and retrieving the data correctly.This is tedious to do everytime we make a change, add or remove a property to our domain model, add a new endpoint for new use case.
* Add set of integration tests to our source code and run everytime we make a change, this would ensure that any change we have made has not broken any existing funcationality and scenario. Important thing to remember is this is not set in stone and these should be updated as the funcationality evolves, new functionality would lead to adding new test cases.

Focus of this article would be to implement automated integration tests for `MySqlMoviesStore` we implemented earlier.

## Test Setup
Let's start by adding a new folder `integrationtests`.

### `database_helper.go`
I will start by adding `database_helper.go`, this will closely match `mysql_movies_store.go` but will provide its own methods for CRUD operations and it will keep track of the created records to clean up after the test finishes.

Here is the complete listing
```go
package integrationtests

import (
	"context"

	_ "github.com/go-sql-driver/mysql"
	"github.com/google/uuid"
	"github.com/jmoiron/sqlx"
	"github.com/kashifsoofi/blog-code-samples/integration-test-mysql-go/store"
)

const driverName = "mysql"

type databaseHelper struct {
	databaseUrl string
	dbx         *sqlx.DB
	trackedIDs  map[uuid.UUID]any
}

func newDatabaseHelper(databaseUrl string) *databaseHelper {
	return &databaseHelper{
		databaseUrl: databaseUrl,
		trackedIDs:  map[uuid.UUID]any{},
	}
}

func noOpMapper(s string) string { return s }

func (s *databaseHelper) connect(ctx context.Context) error {
	dbx, err := sqlx.ConnectContext(ctx, driverName, s.databaseUrl)
	if err != nil {
		return err
	}

	dbx.MapperFunc(noOpMapper)
	s.dbx = dbx
	return nil
}

func (s *databaseHelper) close() error {
	return s.dbx.Close()
}

func (s *databaseHelper) GetMovie(ctx context.Context, id uuid.UUID) (store.Movie, error) {
	err := s.connect(ctx)
	if err != nil {
		return store.Movie{}, err
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
		return store.Movie{}, err
	}

	return movie, nil
}

func (s *databaseHelper) AddMovie(ctx context.Context, movie store.Movie) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	if _, err := s.dbx.NamedExecContext(
		ctx,
		`INSERT INTO Movies
			(Id, Title, Director, ReleaseDate, TicketPrice, CreatedAt, UpdatedAt)
		VALUES
			(:Id, :Title, :Director, :ReleaseDate, :TicketPrice, :CreatedAt, :UpdatedAt)`,
		movie); err != nil {
		return err
	}

	s.trackedIDs[movie.ID] = movie.ID
	return nil
}

func (s *databaseHelper) AddMovies(ctx context.Context, movies []store.Movie) error {
	for _, movie := range movies {
		if err := s.AddMovie(ctx, movie); err != nil {
			return err
		}
	}

	return nil
}

func (s *databaseHelper) DeleteMovie(ctx context.Context, id uuid.UUID) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	return s.deleteMovie(ctx, id)
}

func (s *databaseHelper) CleanupAllMovies(ctx context.Context) error {
	ids := []uuid.UUID{}
	for id := range s.trackedIDs {
		ids = append(ids, id)
	}
	return s.CleanupMovies(ctx, ids...)
}

func (s *databaseHelper) CleanupMovies(ctx context.Context, ids ...uuid.UUID) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	for _, id := range ids {
		if err := s.deleteMovie(ctx, id); err != nil {
			return err
		}
	}

	return nil
}

func (s *databaseHelper) deleteMovie(ctx context.Context, id uuid.UUID) error {
	_, err := s.dbx.ExecContext(ctx, `DELETE FROM Movies WHERE id = ?`, id)
	if err != nil {
		return err
	}

	delete(s.trackedIDs, id)
	return nil
}
```

### `mysql_movies_store_test.go`
This file will contain the tests for each of the methods provided by `MySqlMoviesStore`. But first lets start by adding a `TestSuite`. We will add a test suite using `testify/suite` to test our `MySqlMoviesStore`. This will allow us to use the common setup before running any of the tests.
In `SetupSuite` we will load the configuration from environment and initilise `MySqlMoviesStore` as well as `dbHelper` with database connection string.
In `TearDownSuite` we can perform any clean up operations, it will remain empty in our case.

```go
type mysqlMoviesStoreTestSuite struct {
	suite.Suite
	sut      *store.MySqlMoviesStore
	ctx      context.Context
	dbHelper *databaseHelper
	fake     faker.Faker
}

func (suite *mysqlMoviesStoreTestSuite) SetupSuite() {
	suite.ctx = context.Background()

	cfg, err := config.Load()
	require.Nil(suite.T(), err)

	suite.sut = store.NewMySqlMoviesStore(cfg.DatabaseURL)
	suite.dbHelper = newDatabaseHelper(cfg.DatabaseURL)
	suite.fake = faker.New()
}

func (suite *mysqlMoviesStoreTestSuite) TearDownSuite() {
}

...

func TestMySqlMoviesStoreTestSuite(t *testing.T) {
	suite.Run(t, new(mysqlMoviesStoreTestSuite))
}
```

### Helper Methods
We will add 2 helper methods to generate test data using faker and a helper method to assert 2 instancs of `store.Movie` are equal, we will compare time fields to nearest second.
```go
func (suite *mysqlMoviesStoreTestSuite) createMovie() store.Movie {
	m := store.Movie{}
	suite.fake.Struct().Fill(&m)
	m.ReleaseDate = suite.fake.Time().Time(time.Now()).UTC()
	m.TicketPrice = math.Round(m.TicketPrice*100) / 100
	m.CreatedAt = time.Now().UTC()
	m.UpdatedAt = time.Now().UTC()
	return m
}

func (suite *mysqlMoviesStoreTestSuite) createMovies(n int) []store.Movie {
	movies := []store.Movie{}
	for i := 0; i < n; i++ {
		m := suite.createMovie()
		movies = append(movies, m)
	}
	return movies
}

func assertMovieEqual(t *testing.T, expected store.Movie, actual store.Movie) {
	assert.Equal(t, expected.ID, actual.ID)
	assert.Equal(t, expected.Title, actual.Title)
	assert.Equal(t, expected.Director, actual.Director)
	assert.Equal(t, expected.ReleaseDate, actual.ReleaseDate)
	assert.Equal(t, expected.TicketPrice, actual.TicketPrice)
	assert.WithinDuration(t, expected.CreatedAt, actual.CreatedAt, 1*time.Second)
	assert.WithinDuration(t, expected.UpdatedAt, actual.UpdatedAt, 1*time.Second)
}
```

## Tests
I am going to group tests by the method and then use `t.Run` within test mehtod to run an individual scenario. We can also use table based tests to run individual scenarios. e.g. if there are 2 tests for `GetAll`, those would be in `TestGetAll` method and then I would run individual test with `t.Run` within that method.

Also before running tests, we would need to start the database server and apply migrations. Run following command to do that.
```shell
docker-compose -f docker-compose.dev-env.yml up -d
```

### GetAll Tests
For `GetAll`, we are going to implement 2 test. First test is simple i.e. given there is no record in database for movies, `GetAll` should return an empty array. It would look like following
```go
func (suite *mysqlMoviesStoreTestSuite) TestGetAll() {
	t := suite.T()

	t.Run("given no records, should return empty array", func(t *testing.T) {
		storeMovies, err := suite.sut.GetAll(suite.ctx)

		assert.Nil(t, err)
		assert.Empty(t, storeMovies)
		assert.Equal(t, len(storeMovies), 0)
	})
```

For second test, we would start by creating test movies and then using the `dbHelper` to insert those records to the database before calling the `GetAll` method on `MySqlMoviesStore`. After getting the result we will verify if each record we added earlier using `dbHelper` is present in the `GetAll` method result of `MySqlMoviesStore`. We will also call a `defer` function to delete test data from the database.
```go
func (suite *mysqlMoviesStoreTestSuite) TestGetAll() {
	...
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
```

### GetByID Tests
First test for `GetByID` is to try to get a record with a random id and verify that it return a `RecordNotFoundError`.
```go
func (suite *mysqlMoviesStoreTestSuite) TestGetByID() {
	t := suite.T()

	t.Run("given record does not exist, should return error", func(t *testing.T) {
		id, err := uuid.Parse(suite.fake.UUID().V4())
		assert.NoError(t, err)

		_, err = suite.sut.GetByID(suite.ctx, id)

		var targetErr *store.RecordNotFoundError
		assert.ErrorAs(t, err, &targetErr)
	})
}
```

In our next test, we would first insert a record using `dbHelper` and then use our `sut`(system under test) to load the record and then finally verify that the inserted record is same as loaded record.
```go
func (suite *mysqlMoviesStoreTestSuite) TestGetByID() {
	...
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
```

### Create Tests
First test for `Create` is straight forward, we are going to generate some fake data for `createMovieParam`, create a new record using `sut` and then we would use our helper to load the record from database and assert the record was saved correctly.
```go
func (suite *mysqlMoviesStoreTestSuite) TestCreate() {
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
}
```

2nd test is to check if the method returns an error if the id already exists. We will use `dbHelper` to add a new record first and then try to create a new record using `MySqlMoviesStore`.
```go
func (suite *mysqlMoviesStoreTestSuite) TestCreate() {
	...
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
```

### Update Tests
To test update, first we will create a record and then call the `Update` method of store to update the recrod. After updating the record we will use the `dbHelper` to load the saved record and assert the saved record has updated values.
```go
func (suite *mysqlMoviesStoreTestSuite) TestUpdate() {
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
```

### Delete Tests
To test delete, first we will add a new record using `dbHelper`, then call `Delete` method on our `sut`. To verify the record was successfully deleted we would again use `dbHelper` to load the record and assert it returns error with string `no rows in result set`.
```go
func (suite *mysqlMoviesStoreTestSuite) TestDelete() {
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
Run following `go test` command to run integration tests. Please remember pre-requisit of running these tests is to start database server and apply migrations.
```shell
DATABASE_URL=root:Password123@tcp(localhost:3306)/moviesdb?parseTime=true go test ./integrationtests
```

## Integration Tests in CI
I am also adding 2 [GitHub Actions](https://github.com/features/actions) workflows to run these integration tests as part of CI.

### Setting up MySQL using GitHub Service Container
In this workflow we would make use of the [GitHub service containers](https://docs.github.com/en/actions/using-containerized-services/about-service-containers) to start a MySQL server. We will build migrations container and run it as part of the build process to apply migrations before running integration tests. Here is the full listing.
```yaml
name: Integration Test MySQL (Go)

on:
  push:
    branches: [ "main" ]
    paths:
     - 'integration-test-mysql-go/**'

jobs:
  build:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: integration-test-mysql-go

    services:
      movies.db.test:
        image: mysql:5.7
        env:
          MYSQL_DATABASE: defaultdb
          MYSQL_ROOT_PASSWORD: Password123
        options: >-
          --health-cmd="mysqladmin ping"
          --health-interval=10s
          --health-timeout=5s
          --health-retries=3
        ports:
          - 3306:3306

    steps:
      - uses: actions/checkout@v3
      - name: Set up Go
        uses: actions/setup-go@v4
        with:
          go-version: '1.20'
      - name: Build
        run: go build -v ./...
      - name: Build migratinos Docker image
        run: docker build --file ./db/Dockerfile -t movies.db.migrations ./db
      - name: Run migrations
        run: docker run --add-host=host.docker.internal:host-gateway movies.db.migrations "mysql://root:Password123@tcp(movies.db:3306)/moviesdb" up
      - name: Run integration tests
        run: DATABASE_URL=root:Password123@tcp(localhost:3306)/moviesdb?parseTime=true go test ./integrationtests
```

### Setting up MySQL using docker-compose
In this workflow we will use the docker-compose.dev-env.yml to start MySQL and apply migrations as a first step of the workflow after checking out the code. Here is the full listing.
```yaml
name: Integration Test MySQL (Go) with docker-compose

on:
  push:
    branches: [ "main" ]
    paths:
     - 'integration-test-mysql-go/**'

jobs:
  build:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: integration-test-mysql-go

    steps:
      - uses: actions/checkout@v3
      - name: Start container and apply migrations
        run: docker compose -f "docker-compose.dev-env.yml" up -d --build
      - name: Set up Go
        uses: actions/setup-go@v4
        with:
          go-version: '1.20'
      - name: Build
        run: go build -v ./...
      - name: Run integration tests
        run: DATABASE_URL=root:Password123@tcp(localhost:3306)/moviesdb?parseTime=true go test ./integrationtests
      - name: Stop containers
        run: docker compose -f "docker-compose.dev-env.yml" down --remove-orphans --rmi all --volumes
```

## Source
Source code for the demo application is hosted on GitHub in [blog-code-samples](https://github.com/kashifsoofi/blog-code-samples/tree/main/integration-test-mysql-go) repository.

Source for `Integration Test MySQL (Go)` workflow is in [integration-test-mysql-go.yml](https://github.com/kashifsoofi/blog-code-samples/blob/main/.github/workflows/integration-test-mysql-go.yml).

Source for `Integration Test MySQL (Go) with docker-compose` workflow is in [integration-test-mysql-go-docker-compose.yml](https://github.com/kashifsoofi/blog-code-samples/blob/main/.github/workflows/integration-test-mysql-go-docker-compose.yml).


## References
In no particular order
* [REST API with Go, Chi, MySQL and sqlx](https://kashifsoofi.github.io/go/rest/mysql/sqlx/restapi-with-go-chi-mysql-and-sqlx/)
* [MySQL](https://www.mysql.com/)
* [Docker](https://www.docker.com/)
* [envconfig](https://github.com/kelseyhightower/envconfig)
* [chi](https://github.com/go-chi/chi)
* [migrate](https://github.com/golang-migrate/migrate)
* [sqlx](https://github.com/jmoiron/sqlx)
* [parseTime Parameter](https://stackoverflow.com/questions/26617957/how-to-scan-a-mysql-timestamp-value-into-a-time-time-variable)
* [sqlx](https://github.com/jmoiron/sqlx)
* [faker](https://github.com/jaswdr/faker)
* [testify](https://github.com/stretchr/testify)
* [GitHub Actions](https://github.com/features/actions)
* [About service containers](https://docs.github.com/en/actions/using-containerized-services/about-service-containers)
* [Building and testing Go](https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-go)
* And many more