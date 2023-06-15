package config

import (
	"time"

	"github.com/kelseyhightower/envconfig"
)

const envPrefix = ""

type Configuration struct {
	HTTPServer
	Database
}

type HTTPServer struct {
	IdleTimeout  time.Duration `envconfig:"HTTP_SERVER_IDLE_TIMEOUT" default:"60s"`
	Port         int           `envconfig:"PORT" default:"8080"`
	ReadTimeout  time.Duration `envconfig:"HTTP_SERVER_READ_TIMEOUT" default:"1s"`
	WriteTimeout time.Duration `envconfig:"HTTP_SERVER_WRITE_TIMEOUT" default:"2s"`
}

type Database struct {
	DatabaseURL          string `envconfig:"DATABASE_URL" required:"true"`
	DatabaseName         string `envconfig:"DATABASE_NAME" default:"MoviesStore"`
	MoviesCollectionName string `envconfig:"MOVIES_COLLECTION_NAME" default:"MoviesCollectionName"`
}

func Load() (Configuration, error) {
	var cfg Configuration
	err := envconfig.Process(envPrefix, &cfg)
	if err != nil {
		return cfg, err
	}

	return cfg, nil
}
