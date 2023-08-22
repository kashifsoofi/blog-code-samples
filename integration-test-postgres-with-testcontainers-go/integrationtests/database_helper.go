package integrationtests

import (
	"context"

	"github.com/google/uuid"
	_ "github.com/jackc/pgx/stdlib"
	"github.com/jmoiron/sqlx"
	"github.com/kashifsoofi/blog-code-samples/integration-test-postgres-with-testcontainers-go/store"
)

const driverName = "pgx"

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

func (s *databaseHelper) connect(ctx context.Context) error {
	dbx, err := sqlx.ConnectContext(ctx, driverName, s.databaseUrl)
	if err != nil {
		return err
	}

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
			id, title, director, release_date, ticket_price, created_at, updated_at
		FROM movies
		WHERE id = $1`,
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
		`INSERT INTO movies
			(id, title, director, release_date, ticket_price, created_at, updated_at)
		VALUES
			(:id, :title, :director, :release_date, :ticket_price, :created_at, :updated_at)`,
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
	_, err := s.dbx.ExecContext(ctx, `DELETE FROM movies WHERE id = $1`, id)
	if err != nil {
		return err
	}

	delete(s.trackedIDs, id)
	return nil
}
