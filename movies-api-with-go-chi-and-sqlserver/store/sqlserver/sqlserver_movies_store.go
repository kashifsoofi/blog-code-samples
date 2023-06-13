package sqlserver

import (
	"context"
	"database/sql"
	"movies-api/store"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/jmoiron/sqlx"
	_ "github.com/microsoft/go-mssqldb"
)

const driverName = "sqlserver"

type SqlServerMoviesStore struct {
	databaseUrl string
	dbx         *sqlx.DB
}

func NewSqlServerMoviesStore(databaseUrl string) *SqlServerMoviesStore {
	return &SqlServerMoviesStore{
		databaseUrl: databaseUrl,
	}
}

func noOpMapper(s string) string { return s }

func (s *SqlServerMoviesStore) connect(ctx context.Context) error {
	dbx, err := sqlx.ConnectContext(ctx, driverName, s.databaseUrl)
	if err != nil {
		return err
	}

	dbx.MapperFunc(noOpMapper)
	s.dbx = dbx
	return nil
}

func (s *SqlServerMoviesStore) close() error {
	return s.dbx.Close()
}

func (s *SqlServerMoviesStore) GetAll(ctx context.Context) ([]*store.Movie, error) {
	err := s.connect(ctx)
	if err != nil {
		return nil, err
	}
	defer s.close()

	var movies []*store.Movie
	if err := s.dbx.SelectContext(
		ctx,
		&movies,
		`SELECT
			Id, Title, Director, ReleaseDate, TicketPrice, CreatedAt, UpdatedAt
		FROM Movies`); err != nil {
		return nil, err
	}

	return movies, nil
}

func (s *SqlServerMoviesStore) GetById(ctx context.Context, id uuid.UUID) (*store.Movie, error) {
	err := s.connect(ctx)
	if err != nil {
		return nil, err
	}
	defer s.close()

	var movie store.Movie
	if err := s.dbx.GetContext(
		ctx,
		&movie,
		`SELECT
			Id, Title, Director, ReleaseDate, TicketPrice, CreatedAt, UpdatedAt
		FROM Movies
		WHERE Id = @id`,
		sql.Named("id", id)); err != nil {
		if err != sql.ErrNoRows {
			return nil, err
		}

		return nil, &store.RecordNotFoundError{}
	}

	return &movie, nil
}

func (s *SqlServerMoviesStore) Create(ctx context.Context, createMovieParams store.CreateMovieParams) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	movie := store.Movie{
		Id:          createMovieParams.Id,
		Title:       createMovieParams.Title,
		Director:    createMovieParams.Director,
		ReleaseDate: createMovieParams.ReleaseDate,
		TicketPrice: createMovieParams.TicketPrice,
		CreatedAt:   time.Now().UTC(),
		UpdatedAt:   time.Now().UTC(),
	}

	if _, err := s.dbx.NamedExecContext(
		ctx,
		`INSERT INTO Movies
			(Id, Title, Director, ReleaseDate, TicketPrice, CreatedAt, UpdatedAt)
		VALUES
			(:Id, :Title, :Director, :ReleaseDate, :TicketPrice, :CreatedAt, :UpdatedAt)`,
		movie); err != nil {
		if strings.Contains(err.Error(), "Cannot insert duplicate key") {
			return &store.DuplicateIdError{Id: createMovieParams.Id}
		}
		return err
	}

	return nil
}

func (s *SqlServerMoviesStore) Update(ctx context.Context, id uuid.UUID, updateMovieParams store.UpdateMovieParams) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	movie := store.Movie{
		Id:          id,
		Title:       updateMovieParams.Title,
		Director:    updateMovieParams.Director,
		ReleaseDate: updateMovieParams.ReleaseDate,
		TicketPrice: updateMovieParams.TicketPrice,
		UpdatedAt:   time.Now().UTC(),
	}

	if _, err := s.dbx.NamedExecContext(
		ctx,
		`UPDATE Movies
		SET Title = :Title, Director = :Director, ReleaseDate = :ReleaseDate, TicketPrice = :TicketPrice, UpdatedAt = :UpdatedAt
		WHERE Id = :Id`,
		movie); err != nil {
		return err
	}

	return nil
}

func (s *SqlServerMoviesStore) Delete(ctx context.Context, id uuid.UUID) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close()

	if _, err := s.dbx.ExecContext(
		ctx,
		`DELETE FROM Movies
		WHERE id = @id`, sql.Named("id", id)); err != nil {
		return err
	}

	return nil
}
