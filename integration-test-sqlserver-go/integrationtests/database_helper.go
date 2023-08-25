package integrationtests

import (
	"context"
	"database/sql"

	"github.com/google/uuid"
	"github.com/jmoiron/sqlx"
	"github.com/kashifsoofi/blog-code-samples/integration-test-sqlserver-go/store"
	_ "github.com/microsoft/go-mssqldb"
)

const driverName = "sqlserver"

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
			CAST(Id AS CHAR(36)) AS Id, Title, Director, ReleaseDate, TicketPrice, CreatedAt, UpdatedAt
		FROM Movies
		WHERE Id = @id`,
		sql.Named("id", id)); err != nil {
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
	_, err := s.dbx.ExecContext(ctx, `DELETE FROM Movies WHERE id = @id`, sql.Named("id", id))
	if err != nil {
		return err
	}

	delete(s.trackedIDs, id)
	return nil
}
