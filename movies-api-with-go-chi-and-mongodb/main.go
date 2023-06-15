package main

import (
	"context"
	"log"

	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-mongodb/api"
	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-mongodb/config"
	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-mongodb/store"
)

func main() {
	ctx := context.Background()
	cfg, err := config.Load()
	if err != nil {
		log.Fatal(err)
	}

	// store := store.NewMemoryMoviesStore()
	store := store.NewMongoMoviesStore(cfg.Database)
	server := api.NewServer(cfg.HTTPServer, store)
	server.Start(ctx)
}
