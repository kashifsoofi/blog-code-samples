package store

import (
	"context"
	"time"

	"github.com/google/uuid"
)

type Movie struct {
	Id          uuid.UUID
	Title       string
	Director    string
	ReleaseDate time.Time
	TicketPrice float64
	CreatedAt   time.Time
	UpdatedAt   time.Time
}

type CreateMovieParams struct {
	Id          uuid.UUID
	Title       string
	Director    string
	ReleaseDate time.Time
	TicketPrice float64
}

func NewCreateMovieParams(
	id uuid.UUID,
	title string,
	director string,
	releaseDate time.Time,
	ticketPrice float64,
) CreateMovieParams {
	return CreateMovieParams{
		Id:          id,
		Title:       title,
		Director:    director,
		ReleaseDate: releaseDate,
		TicketPrice: ticketPrice,
	}
}

type UpdateMovieParams struct {
	Title       string
	Director    string
	ReleaseDate time.Time
	TicketPrice float64
}

func NewUpdateMovieParams(
	title string,
	director string,
	releaseDate time.Time,
	ticketPrice float64,
) UpdateMovieParams {
	return UpdateMovieParams{
		Title:       title,
		Director:    director,
		ReleaseDate: releaseDate,
		TicketPrice: ticketPrice,
	}
}

type MoviesStore interface {
	GetAll(ctx context.Context) ([]*Movie, error)
	GetById(ctx context.Context, id uuid.UUID) (*Movie, error)
	Create(ctx context.Context, createMovieParams CreateMovieParams) error
	Update(ctx context.Context, id uuid.UUID, updateMovieParams UpdateMovieParams) error
	Delete(ctx context.Context, id uuid.UUID) error
}