package integrationtests

import (
	"context"
	"log"
	"math"
	"testing"
	"time"

	"github.com/google/uuid"
	"github.com/jaswdr/faker"
	"github.com/testcontainers/testcontainers-go"

	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-postgres/store"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/suite"
)

type postgresMoviesStoreTestSuite struct {
	suite.Suite
	databaseContainer   *postgresContainer
	migrationsContainer testcontainers.Container
	sut                 *store.PostgresMoviesStore
	ctx                 context.Context
	dbHelper            *databaseHelper
	fake                faker.Faker
}

func (suite *postgresMoviesStoreTestSuite) SetupSuite() {
	suite.ctx = context.Background()
	pgContainer, err := createPostgresContainer(suite.ctx)
	if err != nil {
		log.Fatal(err)
	}
	suite.databaseContainer = pgContainer

	migrationsContainer, err := createMigrationsContainer(suite.ctx, suite.databaseContainer.connectionString)
	if err != nil {
		log.Fatal(err)
	}
	suite.migrationsContainer = migrationsContainer

	suite.sut = store.NewPostgresMoviesStore(suite.databaseContainer.connectionString)
	suite.dbHelper = newDatabaseHelper(suite.databaseContainer.connectionString)
	suite.fake = faker.New()
}

func (suite *postgresMoviesStoreTestSuite) TearDownSuite() {
	if err := suite.migrationsContainer.Terminate(suite.ctx); err != nil {
		log.Fatalf("error terminating migrations container: %s", err)
	}
	if err := suite.databaseContainer.Terminate(suite.ctx); err != nil {
		log.Fatalf("error terminating database container: %s", err)
	}
}

func (suite *postgresMoviesStoreTestSuite) createMovie() store.Movie {
	m := store.Movie{}
	suite.fake.Struct().Fill(&m)
	m.ReleaseDate = suite.fake.Time().Time(time.Now()).UTC()
	m.TicketPrice = math.Round(m.TicketPrice*100) / 100
	m.CreatedAt = time.Now().UTC()
	m.UpdatedAt = time.Now().UTC()
	return m
}

func (suite *postgresMoviesStoreTestSuite) createMovies(n int) []store.Movie {
	movies := []store.Movie{}
	for i := 0; i < n; i++ {
		m := suite.createMovie()
		movies = append(movies, m)
	}
	return movies
}

func assertMovieEqual(t *testing.T, expected store.Movie, actual store.Movie) {
	assert.Equal(t, expected.ID, actual.ID)
	assert.Equal(t, expected.Title, actual.Title)
	assert.Equal(t, expected.Director, actual.Director)
	assert.Equal(t, expected.ReleaseDate, actual.ReleaseDate)
	assert.Equal(t, expected.TicketPrice, actual.TicketPrice)
	assert.WithinDuration(t, expected.CreatedAt, actual.CreatedAt, 1*time.Second)
	assert.WithinDuration(t, expected.UpdatedAt, actual.UpdatedAt, 1*time.Second)
}

func (suite *postgresMoviesStoreTestSuite) TestGetAll() {
	t := suite.T()

	t.Run("given no records, should return empty array", func(t *testing.T) {
		storeMovies, err := suite.sut.GetAll(suite.ctx)

		assert.Nil(t, err)
		assert.Empty(t, storeMovies)
		assert.Equal(t, len(storeMovies), 0)
	})

	t.Run("given records exist, should return array", func(t *testing.T) {
		movies := suite.createMovies(3)
		err := suite.dbHelper.AddMovies(suite.ctx, movies)
		assert.Nil(t, err)

		defer func() {
			ids := []uuid.UUID{}
			for _, m := range movies {
				ids = append(ids, m.ID)
			}
			err := suite.dbHelper.CleanupMovies(suite.ctx, ids...)
			assert.Nil(t, err)
		}()

		storeMovies, err := suite.sut.GetAll(suite.ctx)

		assert.Nil(t, err)
		assert.NotEmpty(t, storeMovies)
		assert.GreaterOrEqual(t, len(storeMovies), len(movies))
		for _, m := range movies {
			for _, sm := range storeMovies {
				if m.ID == sm.ID {
					assertMovieEqual(t, m, sm)
					continue
				}
			}
		}
	})
}

func (suite *postgresMoviesStoreTestSuite) TestGetByID() {
	t := suite.T()

	t.Run("given record does not exist, should return error", func(t *testing.T) {
		id, err := uuid.Parse(suite.fake.UUID().V4())
		assert.NoError(t, err)

		_, err = suite.sut.GetByID(suite.ctx, id)

		var targetErr *store.RecordNotFoundError
		assert.ErrorAs(t, err, &targetErr)
	})

	t.Run("given record exists, should return record", func(t *testing.T) {
		movie := suite.createMovie()
		err := suite.dbHelper.AddMovie(suite.ctx, movie)
		assert.Nil(t, err)

		defer func() {
			err := suite.dbHelper.DeleteMovie(suite.ctx, movie.ID)
			assert.Nil(t, err)
		}()

		storeMovie, err := suite.sut.GetByID(suite.ctx, movie.ID)

		assert.Nil(t, err)
		assertMovieEqual(t, movie, storeMovie)
	})
}

func (suite *postgresMoviesStoreTestSuite) TestCreate() {
	t := suite.T()

	t.Run("given record does not exist, should create record", func(t *testing.T) {
		p := store.CreateMovieParams{}
		suite.fake.Struct().Fill(&p)
		p.TicketPrice = math.Round(p.TicketPrice*100) / 100
		p.ReleaseDate = suite.fake.Time().Time(time.Now()).UTC()

		err := suite.sut.Create(suite.ctx, p)

		assert.Nil(t, err)
		defer func() {
			err := suite.dbHelper.DeleteMovie(suite.ctx, p.ID)
			assert.Nil(t, err)
		}()

		m, err := suite.dbHelper.GetMovie(suite.ctx, p.ID)
		assert.Nil(t, err)
		expected := store.Movie{
			ID:          p.ID,
			Title:       p.Title,
			Director:    p.Director,
			ReleaseDate: p.ReleaseDate,
			TicketPrice: p.TicketPrice,
			CreatedAt:   time.Now().UTC(),
			UpdatedAt:   time.Now().UTC(),
		}
		assertMovieEqual(t, expected, m)
	})

	t.Run("given record with id exists, should return DuplicateKeyError", func(t *testing.T) {
		movie := suite.createMovie()
		err := suite.dbHelper.AddMovie(suite.ctx, movie)
		assert.Nil(t, err)
		defer func() {
			err := suite.dbHelper.DeleteMovie(suite.ctx, movie.ID)
			assert.Nil(t, err)
		}()

		p := store.CreateMovieParams{
			ID:          movie.ID,
			Title:       movie.Title,
			Director:    movie.Director,
			ReleaseDate: movie.ReleaseDate,
			TicketPrice: movie.TicketPrice,
		}

		err = suite.sut.Create(suite.ctx, p)

		assert.NotNil(t, err)
		var targetErr *store.DuplicateKeyError
		assert.ErrorAs(t, err, &targetErr)
	})
}

func (suite *postgresMoviesStoreTestSuite) TestUpdate() {
	t := suite.T()

	t.Run("given record exists, should update record", func(t *testing.T) {
		movie := suite.createMovie()
		err := suite.dbHelper.AddMovie(suite.ctx, movie)
		assert.Nil(t, err)
		defer func() {
			err := suite.dbHelper.DeleteMovie(suite.ctx, movie.ID)
			assert.Nil(t, err)
		}()

		p := store.UpdateMovieParams{
			Title:       suite.fake.RandomStringWithLength(20),
			Director:    suite.fake.Person().Name(),
			ReleaseDate: suite.fake.Time().Time(time.Now()).UTC(),
			TicketPrice: math.Round(suite.fake.RandomFloat(2, 1, 100)*100) / 100,
		}

		err = suite.sut.Update(suite.ctx, movie.ID, p)

		assert.Nil(t, err)

		m, err := suite.dbHelper.GetMovie(suite.ctx, movie.ID)
		assert.Nil(t, err)
		expected := store.Movie{
			ID:          movie.ID,
			Title:       p.Title,
			Director:    p.Director,
			ReleaseDate: p.ReleaseDate,
			TicketPrice: p.TicketPrice,
			CreatedAt:   movie.CreatedAt,
			UpdatedAt:   time.Now().UTC(),
		}
		assertMovieEqual(t, expected, m)
	})
}

func (suite *postgresMoviesStoreTestSuite) TestDelete() {
	t := suite.T()

	t.Run("given record exists, should delete record", func(t *testing.T) {
		movie := suite.createMovie()
		err := suite.dbHelper.AddMovie(suite.ctx, movie)
		assert.Nil(t, err)
		defer func() {
			err := suite.dbHelper.DeleteMovie(suite.ctx, movie.ID)
			assert.Nil(t, err)
		}()

		err = suite.sut.Delete(suite.ctx, movie.ID)

		assert.Nil(t, err)

		_, err = suite.dbHelper.GetMovie(suite.ctx, movie.ID)
		assert.NotNil(t, err)
		assert.ErrorContains(t, err, "sql: no rows in result set")
	})
}

func TestPostgresMoviesStoreTestSuite(t *testing.T) {
	suite.Run(t, new(postgresMoviesStoreTestSuite))
}
