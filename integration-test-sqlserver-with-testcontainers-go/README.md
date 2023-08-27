# Integration Test SQL Server with testcontainers-go
This is a continuation of an earlier post [Integration Test SQL Server Store (go)](https://kashifsoofi.github.io/go/testing/integrationtest/sqlserver/integration-test-sqlserver-go/). In this tutorial I will extend the sample to use [testcontainers-go](https://golang.testcontainers.org) to spin up database container and apply migrations before executing our integration tests.

Prior to this sample, pre-requisite of running integration tests was that database server is running either on machine or in a container and migrations are applied. This update will remove that manual step.

## Setup
We would need to start 2 containers before running our integration tests.
* Database Container - hosting the database server
* Migrations Container - container to apply database migrations

## Database Container
Let's start by adding a new file `containers.go` under `integrationtests` folder. If there are multiple tests feel free to add a separate `testhelper` directory to add common code. When moving common code to separate package remember to make the types and methods public.

We will create a struct `databaseContainer` and embed `testcontainers.Container` from `testcontainers-go`. We will also add 2 fields for `password` and `database`. We are going to set default values for these fields in this tutorial.

We will add a helper method to return the connection string for `sa` default user.

```go
package integrationtests

import (
	"context"
	"fmt"
	"io"
	"net"
	"strings"
	"time"

	"github.com/testcontainers/testcontainers-go"
	"github.com/testcontainers/testcontainers-go/wait"
)

const (
	defaultPassword     = "Password123"
	defaultDatabaseName = "Movies"
)

type databaseContainer struct {
	testcontainers.Container
	password string
	database string
}

func (c *databaseContainer) ConnectionString(ctx context.Context) (string, error) {
	containerPort, err := c.MappedPort(ctx, "1433/tcp")
	if err != nil {
		return "", err
	}

	host, err := c.Host(ctx)
	if err != nil {
		return "", err
	}

	connectionString := fmt.Sprintf("sqlserver://sa:%s@%s/%s", c.password, net.JoinHostPort(host, containerPort.Port()), c.database)
	return connectionString, nil
}
```

### Database Container
Next we will add a method `createDatabaseContainer` in `containers.go` file. This is setting up `ContainerRequest` to create our custom SQL Server image, wait for log message to ensure database is running and then execute `setup-db.sql` script using `sqlcmd` and verify if the script was successful.

Next we start the container and return our custom `databaseContainer` struct if successful.
```go
func createDatabaseContainer(ctx context.Context) (*databaseContainer, error) {
	req := testcontainers.ContainerRequest{
		FromDockerfile: testcontainers.FromDockerfile{
			Context:    "../db",
			Dockerfile: "Dockerfile.db",
		},
		ExposedPorts: []string{"1433/tcp"},
		Env: map[string]string{
			"ACCEPT_EULA":       "Y",
			"MSSQL_SA_PASSWORD": defaultPassword,
			"MSSQL_PID":         "Express",
		},
		Networks: []string{"testcontainers-go"},
		Name:     "movies.db",
		WaitingFor: wait.ForAll(
			wait.ForLog("SQL Server is now ready for client connections"),
			wait.ForExec([]string{
				"/opt/mssql-tools/bin/sqlcmd",
				"-U",
				"sa",
				"-P",
				defaultPassword,
				"-i",
				"/scripts/setup-db.sql",
			}).
				WithResponseMatcher(func(body io.Reader) bool {
					data, _ := io.ReadAll(body)
					return strings.Contains(string(data), "READY")
				}),
		).
			WithStartupTimeoutDefault(time.Minute * 3).
			WithDeadline(time.Minute * 5),
	}

	dbContainer, err := testcontainers.GenericContainer(ctx, testcontainers.GenericContainerRequest{
		ContainerRequest: req,
		Started:          true,
	})
	if err != nil {
		return nil, err
	}

	return &databaseContainer{
		Container: dbContainer,
		password:  defaultPassword,
		database:  defaultDatabaseName,
	}, nil
}
```

### Migrations Container
We will add another method in `containers.go` file to create and run migrations container by passing database container's IP address. To make this work we would need to run it in the same network we are running our database container i.e. `testcontainers-go`. First step of the method is to create a connection string, we are using the same values except for the database container IP but all these can be made configureable. Instead of keeping migrations container running we will wait until it exits.
```go
func createMigrationsContainer(ctx context.Context, dbHostIP string) (testcontainers.Container, error) {
	connectionString := fmt.Sprintf("sqlserver://sa:Password123@%s:1433/Movies", dbHostIP)
	req := testcontainers.ContainerRequest{
		FromDockerfile: testcontainers.FromDockerfile{
			Context:    "../db",
			Dockerfile: "Dockerfile.migrations",
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
type sqlServerMoviesStoreTestSuite struct {
	...
	dbContainer         *databaseContainer
	migrationsContainer testcontainers.Container
	...
}
```

We will update `SetupSuite` method to create both database and migration containers and use the connection string from our newly created database container to initialise `sut` and `dbHelper`. We are no longer loading database url from environment configuration.
```go
func (suite *sqlServerMoviesStoreTestSuite) SetupSuite() {
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

	connectionString, err := suite.dbContainer.ConnectionString(suite.ctx)
	if err != nil {
		log.Fatal(err)
	}

	suite.sut = store.NewSqlServerMoviesStore(connectionString)
	suite.dbHelper = newDatabaseHelper(connectionString)
	suite.fake = faker.New()
}
```

We will also update `TearDownSuite` method to terminate both migrations and database containers.
```go
func (suite *sqlServerMoviesStoreTestSuite) TearDownSuite() {
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
name: Integration Test SQL Server (testcontainers-go)

on:
  push:
    branches: [ "main" ]
    paths:
     - 'integration-test-sqlserver-with-testcontainers-go/**'
    
jobs:
  build:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: integration-test-sqlserver-with-testcontainers-go

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
Source code for the demo application is hosted on GitHub in [blog-code-samples](https://github.com/kashifsoofi/blog-code-samples/tree/main/integration-test-sqlserver-with-testcontainers-go) repository.

Source for `Integration Test SQL Server (testcontainers-go)` workflow is in [integration-test-sqlserver-testcontainers-go.yml](https://github.com/kashifsoofi/blog-code-samples/blob/main/.github/workflows/integration-test-sqlserver-testcontainers-go.yml).

## References
In no particular order
* [REST API with Go, Chi, SQL Server and sqlx](https://kashifsoofi.github.io/go/rest/sqlserver/sqlx/restapi-with-go-chi-sqlserver-and-sqlx/)
* [Microsoft SQL Server](https://www.microsoft.com/en-gb/sql-server)
* [Microsoft SQL Server - Ubuntu based images](https://hub.docker.com/_/microsoft-mssql-server)
* [Docker](https://www.docker.com/)
* [envconfig](https://github.com/kelseyhightower/envconfig)
* [chi](https://github.com/go-chi/chi)
* [migrate](https://github.com/golang-migrate/migrate)
* [sqlx](https://github.com/jmoiron/sqlx)
* [faker](https://github.com/jaswdr/faker)
* [testify](https://github.com/stretchr/testify)
* [GitHub Actions](https://github.com/features/actions)
* [About service containers](https://docs.github.com/en/actions/using-containerized-services/about-service-containers)
* [Building and testing Go](https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-go)
* [Testcontainers for Go!](https://golang.testcontainers.org/)
* And many more