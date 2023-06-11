package main

import (
	"context"
	"log"
	"movies-api/api"
	"movies-api/config"
	"movies-api/store/mysql"
)

func main() {
	ctx := context.Background()
	cfg, err := config.Load()
	if err != nil {
		log.Fatal(err)
	}

	// store := in_memory.NewInMemoryMoviesStore()
	store := mysql.NewMySqlMoviesStore(cfg.DatabaseURL)
	server := api.NewServer(cfg.HTTPServer, store)
	server.Start(ctx)
}
