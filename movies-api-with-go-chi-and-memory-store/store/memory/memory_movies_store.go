package memory

import (
	"sync"
	"time"

	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-memory-store/store"

	"github.com/google/uuid"
)

type MemoryMoviesStore struct {
	movies map[uuid.UUID]store.Movie
	mu     sync.RWMutex
}

func NewMemoryMoviesStore() *MemoryMoviesStore {
	return &MemoryMoviesStore{
		movies: map[uuid.UUID]store.Movie{},
	}
}

func (s *MemoryMoviesStore) GetAll() ([]store.Movie, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()

	var movies []store.Movie
	for _, m := range s.movies {
		movies = append(movies, m)
	}
	return movies, nil
}

func (s *MemoryMoviesStore) GetByID(id uuid.UUID) (store.Movie, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()

	m, ok := s.movies[id]
	if !ok {
		return store.Movie{}, &store.RecordNotFoundError{}
	}

	return m, nil
}

func (s *MemoryMoviesStore) Create(createMovieParams store.CreateMovieParams) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	if _, ok := s.movies[createMovieParams.ID]; ok {
		return &store.DuplicateKeyError{ID: createMovieParams.ID}
	}

	movie := store.Movie{
		ID:          createMovieParams.ID,
		Title:       createMovieParams.Title,
		Director:    createMovieParams.Director,
		ReleaseDate: createMovieParams.ReleaseDate,
		TicketPrice: createMovieParams.TicketPrice,
		CreatedAt:   time.Now().UTC(),
		UpdatedAt:   time.Now().UTC(),
	}

	s.movies[movie.ID] = movie
	return nil
}

func (s *MemoryMoviesStore) Update(id uuid.UUID, updateMovieParams store.UpdateMovieParams) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	m, ok := s.movies[id]
	if !ok {
		return &store.RecordNotFoundError{}
	}

	m.Title = updateMovieParams.Title
	m.Director = updateMovieParams.Director
	m.ReleaseDate = updateMovieParams.ReleaseDate
	m.TicketPrice = updateMovieParams.TicketPrice
	m.UpdatedAt = time.Now().UTC()

	s.movies[id] = m
	return nil
}

func (s *MemoryMoviesStore) Delete(id uuid.UUID) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	delete(s.movies, id)
	return nil
}
