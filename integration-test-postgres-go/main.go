package main

import (
	"context"
	"log"

	"github.com/kashifsoofi/blog-code-samples/integration-test-postgres-go/api"
	"github.com/kashifsoofi/blog-code-samples/integration-test-postgres-go/config"
	"github.com/kashifsoofi/blog-code-samples/integration-test-postgres-go/store"
)

func main() {
	ctx := context.Background()
	cfg, err := config.Load()
	if err != nil {
		log.Fatal(err)
	}

	// store := store.NewMemoryMoviesStore()
	store := store.NewPostgresMoviesStore(cfg.DatabaseURL)
	server := api.NewServer(cfg.HTTPServer, store)
	server.Start(ctx)
}
