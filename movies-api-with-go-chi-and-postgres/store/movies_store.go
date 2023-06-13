package store

import (
	"time"

	"github.com/google/uuid"
)

type Movie struct {
	ID          uuid.UUID
	Title       string
	Director    string
	ReleaseDate time.Time
	TicketPrice float64
	CreatedAt   time.Time
	UpdatedAt   time.Time
}

type CreateMovieParams struct {
	ID          uuid.UUID
	Title       string
	Director    string
	ReleaseDate time.Time
	TicketPrice float64
}

type UpdateMovieParams struct {
	Title       string
	Director    string
	ReleaseDate time.Time
	TicketPrice float64
}

type Interface interface {
	GetAll() ([]Movie, error)
	GetByID(id uuid.UUID) (Movie, error)
	Create(createMovieParams CreateMovieParams) error
	Update(id uuid.UUID, updateMovieParams UpdateMovieParams) error
	Delete(id uuid.UUID) error
}
