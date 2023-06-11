package api

import (
	"errors"
	"net/http"
	"time"

	"movies-api/store"

	"github.com/go-chi/chi/v5"
	"github.com/go-chi/render"
	"github.com/google/uuid"
)

type movieResponse struct {
	Id          uuid.UUID `json:"id"`
	Title       string    `json:"title"`
	Director    string    `json:"director"`
	ReleaseDate time.Time `json:"release_date"`
	TicketPrice float64   `json:"ticket_price"`
	CreatedAt   time.Time `json:"created_at"`
	UpdatedAt   time.Time `json:"updated_at"`
}

func NewMovieResponse(m *store.Movie) movieResponse {
	return movieResponse{
		Id:          m.Id,
		Title:       m.Title,
		Director:    m.Director,
		ReleaseDate: m.ReleaseDate,
		TicketPrice: m.TicketPrice,
	}
}

func (hr movieResponse) Render(w http.ResponseWriter, r *http.Request) error {
	return nil
}

func NewMovieListResponse(movies []*store.Movie) []render.Renderer {
	list := []render.Renderer{}
	for _, movie := range movies {
		mr := NewMovieResponse(movie)
		list = append(list, mr)
	}
	return list
}

func (s *Server) handleListMovies() http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		movies, err := s.store.GetAll(r.Context())
		if err != nil {
			render.Render(w, r, ErrInternalServerError)
			return
		}

		render.RenderList(w, r, NewMovieListResponse(movies))
	}
}

func (s *Server) handleGetMovie() http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		idParam := chi.URLParam(r, "id")
		id, err := uuid.Parse(idParam)
		if err != nil {
			render.Render(w, r, ErrBadRequest)
			return
		}

		movie, err := s.store.GetById(r.Context(), id)
		if err != nil {
			var rnfErr *store.RecordNotFoundError
			if errors.As(err, &rnfErr) {
				render.Render(w, r, ErrNotFound)
			} else {
				render.Render(w, r, ErrInternalServerError)
			}
			return
		}

		mr := NewMovieResponse(movie)
		render.Render(w, r, mr)
	}
}

type CreateMovieRequest struct {
	Id          string    `json:"id"`
	Title       string    `json:"title"`
	Director    string    `json:"director"`
	ReleaseDate time.Time `json:"release_date"`
	TicketPrice float64   `json:"ticket_price"`
}

func (mr *CreateMovieRequest) Bind(r *http.Request) error {
	return nil
}

func (s *Server) handleCreateMovie() http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		data := &CreateMovieRequest{}
		if err := render.Bind(r, data); err != nil {
			render.Render(w, r, ErrBadRequest)
			return
		}

		createMovieParams := store.NewCreateMovieParams(
			uuid.MustParse(data.Id),
			data.Title,
			data.Director,
			data.ReleaseDate,
			data.TicketPrice,
		)
		err := s.store.Create(r.Context(), createMovieParams)
		if err != nil {
			var dupIdErr *store.DuplicateIdError
			if errors.As(err, &dupIdErr) {
				render.Render(w, r, ErrConflict(err))
			} else {
				render.Render(w, r, ErrInternalServerError)
			}
			return
		}

		w.WriteHeader(200)
		w.Write(nil)
	}
}

type updateMovieRequest struct {
	Title       string    `json:"title"`
	Director    string    `json:"director"`
	ReleaseDate time.Time `json:"release_date"`
	TicketPrice float64   `json:"ticket_price"`
}

func (mr *updateMovieRequest) Bind(r *http.Request) error {
	return nil
}

func (s *Server) handleUpdateMovie() http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		idParam := chi.URLParam(r, "id")
		id, err := uuid.Parse(idParam)
		if err != nil {
			render.Render(w, r, ErrBadRequest)
			return
		}

		data := &updateMovieRequest{}
		if err := render.Bind(r, data); err != nil {
			render.Render(w, r, ErrBadRequest)
			return
		}

		updateMovieParams := store.NewUpdateMovieParams(
			data.Title,
			data.Director,
			data.ReleaseDate,
			data.TicketPrice,
		)
		err = s.store.Update(r.Context(), id, updateMovieParams)
		if err != nil {
			var rnfErr *store.RecordNotFoundError
			if errors.As(err, &rnfErr) {
				render.Render(w, r, ErrNotFound)
			} else {
				render.Render(w, r, ErrInternalServerError)
			}
			return
		}

		w.WriteHeader(200)
		w.Write(nil)
	}
}

func (s *Server) handleDeleteMovie() http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		idParam := chi.URLParam(r, "id")
		id, err := uuid.Parse(idParam)
		if err != nil {
			render.Render(w, r, ErrBadRequest)
			return
		}

		err = s.store.Delete(r.Context(), id)
		if err != nil {
			var rnfErr *store.RecordNotFoundError
			if errors.As(err, &rnfErr) {
				render.Render(w, r, ErrNotFound)
			} else {
				render.Render(w, r, ErrInternalServerError)
			}
			return
		}

		w.WriteHeader(200)
		w.Write(nil)
	}
}
