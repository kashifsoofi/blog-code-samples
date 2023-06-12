package main

import (
	"context"
	"log"

	"github.com/kashifsoofi/blog-code-samples/restapi-with-go-chi-and-memory-store/api"
	"github.com/kashifsoofi/blog-code-samples/restapi-with-go-chi-and-memory-store/config"
	"github.com/kashifsoofi/blog-code-samples/restapi-with-go-chi-and-memory-store/store/memory"
)

func main() {
	ctx := context.Background()
	cfg, err := config.Load()
	if err != nil {
		log.Fatal(err)
	}

	store := memory.NewMemoryMoviesStore()
	server := api.NewServer(cfg.HTTPServer, store)
	server.Start(ctx)
}
