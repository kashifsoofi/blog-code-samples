package store

import (
	"context"
	"time"

	"github.com/google/uuid"
)

type Movie struct {
	ID          uuid.UUID `bson:"_id"`
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
	GetAll(ctx context.Context) ([]Movie, error)
	GetByID(ctx context.Context, id uuid.UUID) (Movie, error)
	Create(ctx context.Context, createMovieParams CreateMovieParams) error
	Update(ctx context.Context, id uuid.UUID, updateMovieParams UpdateMovieParams) error
	Delete(ctx context.Context, id uuid.UUID) error
}
