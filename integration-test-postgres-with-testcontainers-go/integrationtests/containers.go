package integrationtests

import (
	"context"
	"fmt"
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
