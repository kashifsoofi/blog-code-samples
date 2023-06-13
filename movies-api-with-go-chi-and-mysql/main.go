package main

import (
	"context"
	"log"

	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-mysql/api"
	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-mysql/config"
	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-mysql/store"
)

func main() {
	ctx := context.Background()
	cfg, err := config.Load()
	if err != nil {
		log.Fatal(err)
	}

	// store := store.NewMemoryMoviesStore()
	store := store.NewMySqlMoviesStore(cfg.DatabaseURL)
	server := api.NewServer(cfg.HTTPServer, store)
	server.Start(ctx)
}
