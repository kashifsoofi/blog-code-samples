package store

import (
	"context"
	"sync"
	"time"

	"github.com/google/uuid"
)

type MemoryMoviesStore struct {
	movies map[uuid.UUID]Movie
	mu     sync.RWMutex
}

func NewMemoryMoviesStore() *MemoryMoviesStore {
	return &MemoryMoviesStore{
		movies: map[uuid.UUID]Movie{},
	}
}

func (s *MemoryMoviesStore) GetAll(ctx context.Context) ([]Movie, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()

	var movies []Movie
	for _, m := range s.movies {
		movies = append(movies, m)
	}
	return movies, nil
}

func (s *MemoryMoviesStore) GetByID(ctx context.Context, id uuid.UUID) (Movie, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()

	m, ok := s.movies[id]
	if !ok {
		return Movie{}, &RecordNotFoundError{}
	}

	return m, nil
}

func (s *MemoryMoviesStore) Create(ctx context.Context, createMovieParams CreateMovieParams) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	if _, ok := s.movies[createMovieParams.ID]; ok {
		return &DuplicateKeyError{ID: createMovieParams.ID}
	}

	movie := Movie{
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

func (s *MemoryMoviesStore) Update(ctx context.Context, id uuid.UUID, updateMovieParams UpdateMovieParams) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	m, ok := s.movies[id]
	if !ok {
		return &RecordNotFoundError{}
	}

	m.Title = updateMovieParams.Title
	m.Director = updateMovieParams.Director
	m.ReleaseDate = updateMovieParams.ReleaseDate
	m.TicketPrice = updateMovieParams.TicketPrice
	m.UpdatedAt = time.Now().UTC()

	s.movies[id] = m
	return nil
}

func (s *MemoryMoviesStore) Delete(ctx context.Context, id uuid.UUID) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	delete(s.movies, id)
	return nil
}
