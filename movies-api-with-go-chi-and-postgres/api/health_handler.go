package api

import (
	"net/http"

	"github.com/go-chi/render"
)

type healthResponse struct {
	OK bool `json:"ok"`
}

func (hr healthResponse) Render(w http.ResponseWriter, r *http.Request) error {
	return nil
}

func (s *Server) handleGetHealth(w http.ResponseWriter, r *http.Request) {
	health := healthResponse{OK: true}
	render.Render(w, r, health)
}
