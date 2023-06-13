package store

import (
	"context"
	"database/sql"
	"strings"
	"time"

	"github.com/google/uuid"
	_ "github.com/jackc/pgx/stdlib"
	"github.com/jmoiron/sqlx"
)

const driverName = "pgx"

type PostgresMoviesStore struct {
	databaseUrl string
	dbx         *sqlx.DB
}

func NewPostgresMoviesStore(databaseUrl string) *PostgresMoviesStore {
	return &PostgresMoviesStore{
		databaseUrl: databaseUrl,
	}
}

func (s *PostgresMoviesStore) connect(ctx context.Context) error {
	dbx, err := sqlx.ConnectContext(ctx, driverName, s.databaseUrl)
	if err != nil {
		return err
	}

	s.dbx = dbx
	return nil
}

func (s *PostgresMoviesStore) close() error {
	return s.dbx.Close()
}

func (s *PostgresMoviesStore) GetAll(ctx context.Context) ([]Movie, error) {
	err := s.connect(ctx)
	if err != nil {
		return nil, err
	}
	defer s.close()

	var movies []Movie
	if err := s.dbx.SelectContext(
		ctx,
		&movies,
		`SELECT
			id, title, director, release_date, ticket_price, created_at, updated_at
		FROM movies`); err != nil {
		return nil, err
	}

	return movies, nil
}

func (s *PostgresMoviesStore) GetByID(ctx context.Context, id uuid.UUID) (Movie, error) {
	err := s.connect(ctx)
	if err != nil {
		return Movie{}, err
	}
	defer s.close()

	var movie Movie
	if err := s.dbx.GetContext(
		ctx,
		&movie,
		`SELECT
			id, title, director, release_date, ticket_price, created_at, updated_at
		FROM movies
		WHERE id = $1`,
		id); err != nil {
		if err != sql.ErrNoRows {
			return Movie{}, err
		}

		return Movie{}, &RecordNotFoundError{}
	}

	return movie, nil
}

func (s *PostgresMoviesStore) Create(ctx context.Context, createMovieParams CreateMovieParams) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	movie := Movie{
		ID:          createMovieParams.ID,
		Title:       createMovieParams.Title,
		Director:    createMovieParams.Director,
		ReleaseDate: createMovieParams.ReleaseDate,
		TicketPrice: createMovieParams.TicketPrice,
		CreatedAt:   time.Now().UTC(),
		UpdatedAt:   time.Now().UTC(),
	}

	if _, err := s.dbx.NamedExecContext(
		ctx,
		`INSERT INTO movies
			(id, title, director, release_date, ticket_price, created_at, updated_at)
		VALUES
			(:id, :title, :director, :release_date, :ticket_price, :created_at, :updated_at)`,
		movie); err != nil {
		if strings.Contains(err.Error(), "SQLSTATE 23505") {
			return &DuplicateKeyError{ID: createMovieParams.ID}
		}
		return err
	}

	return nil
}

func (s *PostgresMoviesStore) Update(ctx context.Context, id uuid.UUID, updateMovieParams UpdateMovieParams) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	movie := Movie{
		ID:          id,
		Title:       updateMovieParams.Title,
		Director:    updateMovieParams.Director,
		ReleaseDate: updateMovieParams.ReleaseDate,
		TicketPrice: updateMovieParams.TicketPrice,
		UpdatedAt:   time.Now().UTC(),
	}

	if _, err := s.dbx.NamedExecContext(
		ctx,
		`UPDATE movies
		SET title = :title, director = :director, release_date = :release_date, ticket_price = :ticket_price, updated_at = :updated_at
		WHERE id = :id`,
		movie); err != nil {
		return err
	}

	return nil
}

func (s *PostgresMoviesStore) Delete(ctx context.Context, id uuid.UUID) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	if _, err := s.dbx.ExecContext(
		ctx,
		`DELETE FROM movies
		WHERE id = $1`, id); err != nil {
		return err
	}

	return nil
}
