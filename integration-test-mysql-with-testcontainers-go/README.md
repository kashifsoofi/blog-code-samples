# Integration Test MySQL Store with testcontainers-go
This is a continuation of an earlier post [Integration Test MySQL Store (go)](https://kashifsoofi.github.io/go/rest/mysql/sqlx/This is a continuation of an earlier post [Integration Test MySQL Store (go)](https://kashifsoofi.github.io/go/testing/integrationtest/mysql/integration-test-mysql-go/). In this tutorial I will extend the sample to use [testcontainers-go](https://golang.testcontainers.org) to spin up database container and apply migrations before executing our integration tests.

Prior to this sample, pre-requisite of running integration tests was that database server is running either on machine or in a container and migrations are applied. This update will remove that manual step.

## Setup
We would need to start 2 containers before running our integration tests.
* Database Container - hosting the database server
* Migrations Container - container to apply database migrations

## Database Container
Let's start by adding a new file `containers.go` under `integrationtests` folder. If there are multiple tests feell free to add a separate `testhelper` directory to add common code. When moving common code to separate package remember to make the types and methods public.

We will create a struct `databaseContainer` and embed `mysql.MySQLContainer` from `testcontainers-go/modules/mysql` module. We would also add a `connectionString` for convenience. Then we will add a new method that would start the container and set the `connentionString` before returning to caller.
```go
package integrationtests

import (
	"context"
	"fmt"
	"time"

	"github.com/testcontainers/testcontainers-go"
	"github.com/testcontainers/testcontainers-go/modules/mysql"
	"github.com/testcontainers/testcontainers-go/wait"
)

type databaseContainer struct {
	*mysql.MySQLContainer
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

func createDatabaseContainer(ctx context.Context) (*databaseContainer, error) {
	dbContainer, err := mysql.RunContainer(
		ctx,
		testcontainers.WithImage("mysql:5.7"),
		mysql.WithDatabase("moviesdb"),
		mysql.WithUsername("root"),
		mysql.WithPassword("Password123"),
		WithName("movies.db"),
		WithNetwork("testcontainers-go"),
	)
	if err != nil {
		return nil, err
	}
	connStr, err := dbContainer.ConnectionString(ctx, "parseTime=true")
	if err != nil {
		return nil, err
	}

	return &databaseContainer{
		MySQLContainer:   dbContainer,
		connectionString: connStr,
	}, nil
}
```

### Migrations Cotnainer
We will add another method in `containers.go` file to create and run migrations container by passing database container's IP address. To make this work we would need to run it in the same network we are running our database container i.e. `testcontainers-go`. First step of the method is to create a connection string, we are using the same values except for the database container IP but all these can be made configureable. Instead of keeping migrations container running we will wait until it exits.
```go
func createMigrationsContainer(ctx context.Context, dbHostIP string) (testcontainers.Container, error) {
	connectionString := fmt.Sprintf("mysql://root:Password123@tcp(%s:3306)/moviesdb", dbHostIP)
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

## Test Suite Changes
We will add 2 new fields in our test suite to hold reference to our database container and migrations container that we would start to run our integration tests.

```go
type mysqlMoviesStoreTestSuite struct {
	...
	dbContainer         *databaseContainer
	migrationsContainer testcontainers.Container
	...
}
```

We will update `SetupSuite` method to create both database and migration containers and use the connection string from our newly created database container to initialise `sut` and `dbHelper`. We are no longer loading database url from environment configuration.
```go
func (suite *mysqlMoviesStoreTestSuite) SetupSuite() {
	suite.ctx = context.Background()

	dbContainer, err := createDatabaseContainer(suite.ctx)
	if err != nil {
		log.Fatal(err)
	}
	suite.dbContainer = dbContainer

	dbHostIP, _ := suite.dbContainer.ContainerIP(suite.ctx)
	migrationsContainer, err := createMigrationsContainer(suite.ctx, dbHostIP)
	if err != nil {
		log.Fatal(err)
	}
	suite.migrationsContainer = migrationsContainer

	suite.sut = store.NewMySqlMoviesStore(suite.dbContainer.connectionString)
	suite.dbHelper = newDatabaseHelper(suite.dbContainer.connectionString)
	suite.fake = faker.New()
}
```

We will also update `TearDownSuite` method to terminate both migrations and database containers.
```go
func (suite *mysqlMoviesStoreTestSuite) TearDownSuite() {
	if err := suite.migrationsContainer.Terminate(suite.ctx); err != nil {
		log.Fatalf("error terminating migrations container: %s", err)
	}
	if err := suite.dbContainer.Terminate(suite.ctx); err != nil {
		log.Fatalf("error terminating database container: %s", err)
	}
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
name: Integration Test MySQL (testcontainers-go)

on:
  push:
    branches: [ "main" ]
    paths:
     - 'integration-test-mysql-with-testcontainers-go/**'
    
jobs:
  build:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: integration-test-mysql-with-testcontainers-go

    steps:
      - uses: actions/checkout@v3
      - name: Set up Go
        uses: actions/setup-go@v4
        with:
          go-version: '1.20'
      - name: Build
        run: go build -v ./...
      - name: Run integration tests
        run: TESTCONTAINERS_RYUK_DISABLED=true go test ./integrationtests
```

## Source
Source code for the demo application is hosted on GitHub in [blog-code-samples](https://github.com/kashifsoofi/blog-code-samples/tree/main/integration-test-mysql-with-testcontainers-go) repository.

Source for `Integration Test MySQL (testcontainers-go)` workflow is in [integration-test-mysql-testcontainers-go.yml](https://github.com/kashifsoofi/blog-code-samples/blob/main/.github/workflows/integration-test-mysql-testcontainers-go.yml).


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
* [Testcontainers for Go!](https://golang.testcontainers.org/)
* And many more