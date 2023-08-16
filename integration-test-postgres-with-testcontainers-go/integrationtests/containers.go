package integrationtests

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

func createMigrationsContainer(ctx context.Context, connStr string) (testcontainers.Container, error) {
	connectionString := strings.Replace(connStr, "localhost", "host.docker.internal", 1)
	req := testcontainers.ContainerRequest{
		FromDockerfile: testcontainers.FromDockerfile{
			Context:    "../db",
			Dockerfile: "Dockerfile",
		},
		WaitingFor: wait.ForExit(),
		Cmd:        []string{connectionString, "up"},
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
