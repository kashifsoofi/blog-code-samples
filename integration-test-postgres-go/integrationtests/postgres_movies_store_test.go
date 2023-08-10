package integrationtests

import (
	"context"
	"math"
	"testing"
	"time"

	"github.com/google/uuid"
	"github.com/jaswdr/faker"

	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-postgres/config"
	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-postgres/store"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

var dbHelper *databaseHelper
var sut *store.PostgresMoviesStore

var fake faker.Faker

func TestMain(t *testing.T) {
	cfg, err := config.Load()
	require.Nil(t, err)

	dbHelper = newDatabaseHelper(cfg.DatabaseURL)

	sut = store.NewPostgresMoviesStore(cfg.DatabaseURL)

	fake = faker.New()
}

func createMovie() store.Movie {
	m := store.Movie{}
	fake.Struct().Fill(&m)
	m.ReleaseDate = fake.Time().Time(time.Now()).UTC()
	m.TicketPrice = math.Round(m.TicketPrice*100) / 100
	m.CreatedAt = time.Now().UTC()
	m.UpdatedAt = time.Now().UTC()
	return m
}

func createMovies(n int) []store.Movie {
	movies := []store.Movie{}
	for i := 0; i < n; i++ {
		m := createMovie()
		movies = append(movies, m)
	}
	return movies
}

func TestGetAll(t *testing.T) {
	ctx := context.Background()

	t.Run("given no records, should return empty array", func(t *testing.T) {
		storeMovies, err := sut.GetAll(ctx)

		assert.Nil(t, err)
		assert.Empty(t, storeMovies)
		assert.Equal(t, len(storeMovies), 0)
	})

	t.Run("given records exist, should return array", func(t *testing.T) {
		movies := createMovies(3)
		err := dbHelper.AddMovies(ctx, movies)
		assert.Nil(t, err)

		defer func() {
			ids := []uuid.UUID{}
			for _, m := range movies {
				ids = append(ids, m.ID)
			}
			err := dbHelper.CleanupMovies(ctx, ids...)
			assert.Nil(t, err)
		}()

		storeMovies, err := sut.GetAll(ctx)

		assert.Nil(t, err)
		assert.NotEmpty(t, storeMovies)
		assert.GreaterOrEqual(t, len(storeMovies), len(movies))
		for _, m := range movies {
			assert.Contains(t, storeMovies, m)
		}
	})
}

func TestGetByID(t *testing.T) {
	ctx := context.Background()

	t.Run("given record does not exist, should return error", func(t *testing.T) {
		id, err := uuid.Parse(fake.UUID().V4())
		assert.NoError(t, err)

		_, err = sut.GetByID(ctx, id)

		var targetErr *store.RecordNotFoundError
		assert.ErrorAs(t, err, &targetErr)
	})

	t.Run("given record exists, should return record", func(t *testing.T) {
		movie := createMovie()
		err := dbHelper.AddMovie(ctx, movie)
		assert.Nil(t, err)

		defer func() {
			err := dbHelper.DeleteMovie(ctx, movie.ID)
			assert.Nil(t, err)
		}()

		storeMovie, err := sut.GetByID(ctx, movie.ID)

		assert.Nil(t, err)
		assert.Equal(t, movie, storeMovie)
	})
}

func TestCreate(t *testing.T) {
	ctx := context.Background()

	t.Run("given record does not exist, should create record", func(t *testing.T) {
		p := store.CreateMovieParams{}
		fake.Struct().Fill(&p)
		p.TicketPrice = math.Round(p.TicketPrice*100) / 100
		p.ReleaseDate = fake.Time().Time(time.Now()).UTC()

		err := sut.Create(ctx, p)

		assert.Nil(t, err)
		defer func() {
			err := dbHelper.DeleteMovie(ctx, p.ID)
			assert.Nil(t, err)
		}()

		m, err := dbHelper.GetMovie(ctx, p.ID)
		assert.Nil(t, err)

		assert.Equal(t, p.ID, m.ID)
		assert.Equal(t, p.Title, m.Title)
		assert.Equal(t, p.Director, m.Director)
		assert.Equal(t, p.ReleaseDate, m.ReleaseDate)
		assert.Equal(t, p.TicketPrice, m.TicketPrice)
		assert.WithinDuration(t, time.Now().UTC(), m.CreatedAt, 1*time.Second)
		assert.WithinDuration(t, time.Now().UTC(), m.UpdatedAt, 1*time.Second)
	})

	t.Run("given record with id exists, should return DuplicateKeyError", func(t *testing.T) {
		movie := createMovie()
		err := dbHelper.AddMovie(ctx, movie)
		assert.Nil(t, err)
		defer func() {
			err := dbHelper.DeleteMovie(ctx, movie.ID)
			assert.Nil(t, err)
		}()

		p := store.CreateMovieParams{
			ID:          movie.ID,
			Title:       movie.Title,
			Director:    movie.Director,
			ReleaseDate: movie.ReleaseDate,
			TicketPrice: movie.TicketPrice,
		}

		err = sut.Create(ctx, p)

		assert.NotNil(t, err)
		var targetErr *store.DuplicateKeyError
		assert.ErrorAs(t, err, &targetErr)
	})
}

func TestUpdate(t *testing.T) {
	ctx := context.Background()

	t.Run("given record exists, should update record", func(t *testing.T) {
		movie := createMovie()
		err := dbHelper.AddMovie(ctx, movie)
		assert.Nil(t, err)
		defer func() {
			err := dbHelper.DeleteMovie(ctx, movie.ID)
			assert.Nil(t, err)
		}()

		p := store.UpdateMovieParams{
			Title:       fake.RandomStringWithLength(20),
			Director:    fake.Person().Name(),
			ReleaseDate: fake.Time().Time(time.Now()).UTC(),
			TicketPrice: math.Round(fake.RandomFloat(2, 1, 100)*100) / 100,
		}

		err = sut.Update(ctx, movie.ID, p)

		assert.Nil(t, err)

		m, err := dbHelper.GetMovie(ctx, movie.ID)
		assert.Nil(t, err)
		assert.Equal(t, movie.ID, m.ID)
		assert.Equal(t, p.Title, m.Title)
		assert.Equal(t, p.Director, m.Director)
		assert.Equal(t, p.ReleaseDate, m.ReleaseDate)
		assert.Equal(t, p.TicketPrice, m.TicketPrice)
		assert.WithinDuration(t, time.Now().UTC(), m.CreatedAt, 1*time.Second)
		assert.WithinDuration(t, time.Now().UTC(), m.UpdatedAt, 1*time.Second)
	})
}

func TestDelete(t *testing.T) {
	ctx := context.Background()

	t.Run("given record exists, should delete record", func(t *testing.T) {
		movie := createMovie()
		err := dbHelper.AddMovie(ctx, movie)
		assert.Nil(t, err)
		defer func() {
			err := dbHelper.DeleteMovie(ctx, movie.ID)
			assert.Nil(t, err)
		}()

		err = sut.Delete(ctx, movie.ID)

		assert.Nil(t, err)

		_, err = dbHelper.GetMovie(ctx, movie.ID)
		assert.NotNil(t, err)
		assert.ErrorContains(t, err, "sql: no rows in result set")
	})
}
