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
