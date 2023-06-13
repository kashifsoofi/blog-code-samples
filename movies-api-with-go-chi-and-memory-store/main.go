package main

import (
	"context"
	"log"

	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-memory-store/api"
	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-memory-store/config"
	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-memory-store/store"
)

func main() {
	ctx := context.Background()
	cfg, err := config.Load()
	if err != nil {
		log.Fatal(err)
	}

	store := store.NewMemoryMoviesStore()
	server := api.NewServer(cfg.HTTPServer, store)
	server.Start(ctx)
}
