package main

import (
	"context"
	"log"
	"movies-api/api"
	"movies-api/config"
	"movies-api/store/in_memory"
)

func main() {
	ctx := context.Background()
	cfg, err := config.Load()
	if err != nil {
		log.Fatal(err)
	}

	store := in_memory.NewInMemoryMoviesStore()
	server := api.NewServer(cfg.HTTPServer, store)
	server.Start(ctx)
}
