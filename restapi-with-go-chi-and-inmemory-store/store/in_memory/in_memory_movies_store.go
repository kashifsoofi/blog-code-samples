package in_memory

import (
	"context"
	"movies-api/store"
	"sync"
	"time"

	"github.com/google/uuid"
)

type InMemoryMoviesStore struct {
	movies map[uuid.UUID]*store.Movie
	mu     sync.RWMutex
}

func NewInMemoryMoviesStore() *InMemoryMoviesStore {
	return &InMemoryMoviesStore{
		movies: map[uuid.UUID]*store.Movie{},
	}
}

func (s *InMemoryMoviesStore) GetAll(ctx context.Context) ([]*store.Movie, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()

	movies := make([]*store.Movie, 0)
	for _, m := range s.movies {
		movies = append(movies, m)
	}
	return movies, nil
}

func (s *InMemoryMoviesStore) GetById(ctx context.Context, id uuid.UUID) (*store.Movie, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()

	m, ok := s.movies[id]
	if !ok {
		return nil, &store.RecordNotFoundError{}
	}

	return m, nil
}

func (s *InMemoryMoviesStore) Create(ctx context.Context, createMovieParams store.CreateMovieParams) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	if _, ok := s.movies[createMovieParams.Id]; ok {
		return &store.DuplicateIdError{Id: createMovieParams.Id}
	}

	movie := &store.Movie{
		Id:          createMovieParams.Id,
		Title:       createMovieParams.Title,
		Director:    createMovieParams.Director,
		ReleaseDate: createMovieParams.ReleaseDate,
		TicketPrice: createMovieParams.TicketPrice,
		CreatedAt:   time.Now().UTC(),
		UpdatedAt:   time.Now().UTC(),
	}

	s.movies[movie.Id] = movie
	return nil
}

func (s *InMemoryMoviesStore) Update(ctx context.Context, id uuid.UUID, updateMovieParams store.UpdateMovieParams) error {
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

func (s *InMemoryMoviesStore) Delete(ctx context.Context, id uuid.UUID) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	delete(s.movies, id)
	return nil
}
