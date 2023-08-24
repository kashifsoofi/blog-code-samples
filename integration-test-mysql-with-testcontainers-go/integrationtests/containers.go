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
