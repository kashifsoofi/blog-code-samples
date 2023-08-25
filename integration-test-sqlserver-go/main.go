package main

import (
	"context"
	"log"

	"github.com/kashifsoofi/blog-code-samples/integration-test-sqlserver-go/api"
	"github.com/kashifsoofi/blog-code-samples/integration-test-sqlserver-go/config"
	"github.com/kashifsoofi/blog-code-samples/integration-test-sqlserver-go/store"
)

func main() {
	ctx := context.Background()
	cfg, err := config.Load()
	if err != nil {
		log.Fatal(err)
	}

	// store := store.NewMemoryMoviesStore()
	store := store.NewSqlServerMoviesStore(cfg.DatabaseURL)
	server := api.NewServer(cfg.HTTPServer, store)
	server.Start(ctx)
}
