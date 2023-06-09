# REST API with Go, Chi and InMemory Store
## What is REST API?
An API, or application programming interface, is a set of rules that define how applications or devices can connect to and communicate with each other. A REST API is an API that conforms to the design principles of the REST, or representational state transfer architectural style. For this reason, REST APIs are sometimes referred to RESTful APIs.

Focus of this tutorial is to write a REST API with Go.

# Movie Resource
We would be managing a `Movie` resource with current project. It is not an accurate representation of how you would model a movie resource in an acutal system, just a mix of few basic types and how to handle in rest api.
| Field       | Type    |
|-------------|---------|
| ID          | UUID    |
| Title       | String  |
| Director    | String  |
| Director    | String  |
| ReleaseDate | Time    |
| TicketPrice | float64 |

# Project Setup
* Create a folder for project, I named it as `restapi-with-go-chi-and-inmemory-store` but it usually would be at the root of the GitHub repo, or a subfolder in a mono reop.
* Execute following command to initialise `go.mod` on terminal
```shell
go mod init movies-api
```
* Add a new file `main.go` with following content to start with
```go
package main

func main() {
	println("Hello, World!")
}
```

## Configuration
Add a folder named `config` and a file named `config.go`. I like to keep all application configuration in a single place and will be using excellent `envconfig` package to load the configuration, also setting some default values for options. This package allows us to load application configuration from Environment Variables, same thing can be done with standard Go packages but in my opinion this package provides nice abstraction without losing readability.
```go
package config

import (
	"time"

	"github.com/kelseyhightower/envconfig"
)

const envPrefix = ""

type Configuration struct {
	HTTPServer
}

type HTTPServer struct {
	IdleTimeout  time.Duration `envconfig:"HTTP_SERVER_IDLE_TIMEOUT" default:"60s"`
	Port         int           `envconfig:"PORT" default:"8080"`
	ReadTimeout  time.Duration `envconfig:"HTTP_SERVER_READ_TIMEOUT" default:"1s"`
	WriteTimeout time.Duration `envconfig:"HTTP_SERVER_WRITE_TIMEOUT" default:"2s"`
}

func Load() (*Configuration, error) {
	cfg := Configuration{}
	err := envconfig.Process(envPrefix, &cfg)
	if err != nil {
		return nil, err
	}

	return &cfg, nil
}
```
This would result in an error that can be resolved by executing following on terminal.
```shell
go mod tidy
```

You can improve on it by converting `Configuration` to an `interface` and then adding a configuration to each of sub-packages e.g. `api`, `store` etc.

Configuration can be updated using environment variables e.g. executing following on terminal would start the server on port 5000 after we update `main.go` to start the server.
```shell
PORT=5000 go run main.go
```

## Movie Store Interface
Add a new folder named `store` and a file named `movie_store.go`. We will add an interface for our movie store and supporting structs.
```go
package store

import (
	"context"
	"time"

	"github.com/google/uuid"
)

type Movie struct {
	ID          uuid.UUID
	Title       string
	Director    string
	ReleaseDate time.Time
	TicketPrice float64
	CreatedAt   time.Time
	UpdatedAt   time.Time
}

type CreateMovieParams struct {
	ID          uuid.UUID
	Title       string
	Director    string
	ReleaseDate time.Time
	TicketPrice float64
}

func NewCreateMovieParams(
	id uuid.UUID,
	title string,
	director string,
	releaseDate time.Time,
	ticketPrice float64,
) CreateMovieParams {
	return CreateMovieParams{
		ID:          id,
		Title:       title,
		Director:    director,
		ReleaseDate: releaseDate,
		TicketPrice: ticketPrice,
	}
}

type UpdateMovieParams struct {
	Title       string
	Director    string
	ReleaseDate time.Time
	TicketPrice float64
}

func NewUpdateMovieParams(
	title string,
	director string,
	releaseDate time.Time,
	ticketPrice float64,
) UpdateMovieParams {
	return UpdateMovieParams{
		Title:       title,
		Director:    director,
		ReleaseDate: releaseDate,
		TicketPrice: ticketPrice,
	}
}

type MoviesStore interface {
	GetAll(ctx context.Context) ([]*Movie, error)
	GetByID(ctx context.Context, id uuid.UUID) (*Movie, error)
	Create(ctx context.Context, createMovieParams CreateMovieParams) error
	Update(ctx context.Context, id uuid.UUID, updateMovieParams UpdateMovieParams) error
	Delete(ctx context.Context, id uuid.UUID) error
}
```

Also add a file for custom application errors named `errors.go`, these make clients of our store package agnostic of storage technology used, our storage implementation would translate any native errors to our business errors before returning to clients. 
```go
package store

import (
	"fmt"

	"github.com/google/uuid"
)

type DuplicateIDError struct {
	ID uuid.UUID
}

func (e *DuplicateIDError) Error() string {
	return fmt.Sprintf("duplicate movie id: %v", e.ID)
}

type RecordNotFoundError struct{}

func (e *RecordNotFoundError) Error() string {
	return "record not found"
}
```

## InMemoryMoviesStore
Add folder under `store` named `in_memory` and a file named `in_memory_movies_store.go`. Add a struct `InMemoryMoviesStore` with a map field to store movies in memory. Also add a `RWMutex` field to avoid concurrent read/write access to movies field.

We implement all methods defined for `MovieStore` interface to add/remove movies to map field of the `InMemoryMoviesStore` struct. For reading we lock the collection for reading, read the result and release the lock using `defer`. For writing we acquire a write lock instead of a read lock.
```go
package in_memory

import (
	"context"
	"errors"
	"movies-api/store"
	"sync"
	"time"

	"github.com/google/uuid"
)

type InMemoryMoviesStore struct {
	movies map[uuid.UUID]*store.Movie
	mu     sync.RWMutex
}

func NewInMemoryMoviesStore() *InMemoryMoviesStore {
	return &InMemoryMoviesStore{
		movies: map[uuid.UUID]*store.Movie{},
	}
}

func (s *InMemoryMoviesStore) GetAll(ctx context.Context) ([]*store.Movie, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()

	movies := make([]*store.Movie, 0)
	for _, m := range s.movies {
		movies = append(movies, m)
	}
	return movies, nil
}

func (s *InMemoryMoviesStore) GetByID(ctx context.Context, id uuid.UUID) (*store.Movie, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()

	m, ok := s.movies[id]
	if !ok {
		return nil, &store.RecordNotFoundError{}
	}

	return m, nil
}

func (s *InMemoryMoviesStore) Create(ctx context.Context, createMovieParams store.CreateMovieParams) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	if _, ok := s.movies[createMovieParams.ID]; ok {
		return &store.DuplicateIDError{ID: createMovieParams.ID}
	}

	movie := &store.Movie{
		ID:          createMovieParams.ID,
		Title:       createMovieParams.Title,
		Director:    createMovieParams.Director,
		ReleaseDate: createMovieParams.ReleaseDate,
		TicketPrice: createMovieParams.TicketPrice,
		CreatedAt:   time.Now().UTC(),
		UpdatedAt:   time.Now().UTC(),
	}

	s.movies[movie.ID] = movie
	return nil
}

func (s *InMemoryMoviesStore) Update(ctx context.Context, id uuid.UUID, updateMovieParams store.UpdateMovieParams) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	m, ok := s.movies[id]
	if !ok {
		return &store.RecordNotFoundError{}
	}

	m.Title = updateMovieParams.Title
	m.Director = updateMovieParams.Director
	m.ReleaseDate = updateMovieParams.ReleaseDate
	m.TicketPrice = updateMovieParams.TicketPrice
	m.UpdatedAt = time.Now().UTC()

	s.movies[id] = m
	return nil
}

func (s *InMemoryMoviesStore) Delete(ctx context.Context, id uuid.UUID) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	delete(s.movies, id)
	return nil
}
```

## REST Server
Add a new folder to add all REST api server related files. Let's start by adding `server.go` file and add a struct to represent REST server. This struct would have an instance of configuration required to run server, routes and all the dependencies. Also add method to start the server.
For routes we would use excellent `chi` router, that is a ligtweight, idomatic and composable router for building HTTP services.

In start method, we will construct an instance of `Server` provided by standard `net/http` package, providing `chi mux` we setup in `NewServer` method. We will then setup a method for graceful shutdown and call `ListenAndServe` to start our REST server.
```go
package api

import (
	"context"
	"fmt"
	"log"
	"movies-api/config"
	"net/http"
	"os"
	"os/signal"
	"syscall"

	"movies-api/store"

	"github.com/go-chi/chi/v5"
)

type Server struct {
	cfg    config.HTTPServer
	store  store.MoviesStore
	router *chi.Mux
}

func NewServer(cfg config.HTTPServer, store store.MoviesStore) *Server {
	srv := &Server{
		cfg:    cfg,
		store:  store,
		router: chi.NewRouter(),
	}

	srv.routes()

	return srv
}

func (s *Server) Start(ctx context.Context) {
	server := http.Server{
		Addr:         fmt.Sprintf(":%d", s.cfg.Port),
		Handler:      s.router,
		IdleTimeout:  s.cfg.IdleTimeout,
		ReadTimeout:  s.cfg.ReadTimeout,
		WriteTimeout: s.cfg.WriteTimeout,
	}

	shutdownComplete := handleShutdown(func() {
		if err := server.Shutdown(ctx); err != nil {
			log.Printf("server.Shutdown failed: %v\n", err)
		}
	})

	if err := server.ListenAndServe(); err == http.ErrServerClosed {
		<-shutdownComplete
	} else {
		log.Printf("http.ListenAndServe failed: %v\n", err)
	}

	log.Println("Shutdown gracefully")
}

func handleShutdown(onShutdownSignal func()) <-chan struct{} {
	shutdown := make(chan struct{})

	go func() {
		shutdownSignal := make(chan os.Signal, 1)
		signal.Notify(shutdownSignal, os.Interrupt, syscall.SIGTERM)

		<-shutdownSignal

		onShutdownSignal()
		close(shutdown)
	}()

	return shutdown
}
```
## Custom Errors
We would define any custom errors that are returned by our REST server in `errors.go` file under `api` folder. I have gone ahead and added all the errors I need to return from this service in the file. But practically we would start with the most common ones and then add any new when the need arise.
```go
package api

import (
	"net/http"

	"github.com/go-chi/render"
)

type ErrResponse struct {
	Err            error `json:"-"` // low-level runtime error
	HTTPStatusCode int   `json:"-"` // http response status code

	StatusText string `json:"status"`          // user-level status message
	AppCode    int64  `json:"code,omitempty"`  // application-specific error code
	ErrorText  string `json:"error,omitempty"` // application-level error message, for debugging
}

func (e *ErrResponse) Render(w http.ResponseWriter, r *http.Request) error {
	render.Status(r, e.HTTPStatusCode)
	return nil
}

var (
	ErrNotFound            = &ErrResponse{HTTPStatusCode: 404, StatusText: "Resource not found."}
	ErrBadRequest          = &ErrResponse{HTTPStatusCode: 400, StatusText: "Bad request"}
	ErrInternalServerError = &ErrResponse{HTTPStatusCode: 500, StatusText: "Internal Server Error"}
)

func ErrConflict(err error) render.Renderer {
	return &ErrResponse{
		Err:            err,
		HTTPStatusCode: 409,
		StatusText:     "Duplicate ID",
		ErrorText:      err.Error(),
	}
}
```
## Routes
I like the keep all the routes served by a service in a single place and single file named `routes.go`. Its easier to remember and eases the cognitive overload.

`routes` method hangs off our `Server` struct, defines all the endpoints on the `router` field. I have defined a `/health` endpoint that would return current health status of this service. Then added a subrouter group for movies. This can help us having middlewared applied only for `/api/movies` routes e.g. authentication, request logging.
```go
package api

import "github.com/go-chi/chi/v5"

func (s *Server) routes() {
	s.router.Use(render.SetContentType(render.ContentTypeJSON))

	s.router.Get("/health", s.handleGetHealth())

	s.router.Route("/api/movies", func(r chi.Router) {
		r.Get("/", s.handleListMovies())
		r.Post("/", s.handleCreateMovie())
		r.Route("/{id}", func(r chi.Router) {
			r.Get("/", s.handleGetMovie())
			r.Put("/", s.handleUpdateMovie())
			r.Delete("/", s.handleDeleteMovie())
		})
	})
}
```
Please note all handlers hang off the `Server` struct, this helps to access required dependencies in each of the handler. If there are multiple resources in a service, it might make sense to add separate `structs` per resource containing only dependencies required by that resource.

## Health Endpoint Handler
I have added a separate file for `health` resource. It has a handler for a single endpoint, a struct that we would send as response and implementation of `Renderer` interface for our response struct.
```go
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

func (s *Server) handleGetHealth() http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		health := healthResponse{OK: true}
		render.Render(w, r, health)
	}
}
```

## Movies Endpoints Handlers

### Get Movie By ID
Let's start by adding a struct we would use to return a `Movie` to the caller of our REST service and also implement `Renderer` interface so that we can use `Render` method to return data.
```go
type movieResponse struct {
	ID          uuid.UUID `json:"id"`
	Title       string    `json:"title"`
	Director    string    `json:"director"`
	ReleaseDate time.Time `json:"release_date"`
	TicketPrice float64   `json:"ticket_price"`
}

func NewMovieResponse(m *store.Movie) movieResponse {
	return movieResponse{
		ID:          m.ID,
		Title:       m.Title,
		Director:    m.Director,
		ReleaseDate: m.ReleaseDate,
		TicketPrice: m.TicketPrice,
	}
}

func (hr movieResponse) Render(w http.ResponseWriter, r *http.Request) error {
	return nil
}
```
I like to keep `structs` closer to the method/package using those. It does lead to some code duplication e.g. in this case `movieResponse` is quite similar to `Movie` struct defind in `store/movies_store.go` file but this allows rest package to be not completely dependent on `store` package, we can have different tags e.g. db specific tags in `store` struct but not in `movieResponse` struct.

Now comes the handler, we receive a `ResponseWriter` and a `Request`, we extract `id` parameter from path using `URLParam` method, if parsing fails we rendera `BadRequest`.

Then we proceed to get `movie` from `store` if a record is not found in store with given `id` we render `NotFound`, if the error returned is not the one we defined in our store package then we render an `InternalServerError`, we can add more custom/known errors to store and translate to apprpriate HTTP Status Codes depending on the use case.

If everything works then we convert the `store.Movie` to `movieResponse` and render the result. Result would be returned to the caller as `json` response body.
```go
func (s *Server) handleGetMovie() http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		idParam := chi.URLParam(r, "id")
		id, err := uuid.Parse(idParam)
		if err != nil {
			render.Render(w, r, ErrBadRequest)
			return
		}

		movie, err := s.store.GetByID(r.Context(), id)
		if err != nil {
			var rnfErr *store.RecordNotFoundError
			if errors.As(err, &rnfErr) {
				render.Render(w, r, ErrNotFound)
			} else {
				render.Render(w, r, ErrInternalServerError)
			}
			return
		}

		mr := NewMovieResponse(movie)
		render.Render(w, r, mr)
	}
}
```

### Get All/List Movies
For response we would use the same `movieResponse` struct we defined for `Get By ID`, we would just add a new method to create an array/slice of `Renderer`
```go
func NewMovieListResponse(movies []*store.Movie) []render.Renderer {
	list := []render.Renderer{}
	for _, movie := range movies {
		mr := NewMovieResponse(movie)
		list = append(list, mr)
	}
	return list
}
```

And the handler method is quite simple, we call `GetAll`, if error return `InternalServerError` else return list of movies.
```go
func (s *Server) handleListMovies() http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		movies, err := s.store.GetAll(r.Context())
		if err != nil {
			render.Render(w, r, ErrInternalServerError)
			return
		}

		render.RenderList(w, r, NewMovieListResponse(movies))
	}
}
```

### Create Movie
Same as get, we would start by adding a new struct to receive parameters required to create a new movie. But instead of implmenting `Renderer` we would implement `Binder` interface, in `Bind` method custom mapping can be done if required e.g. adding meta data or setting a `CreatedBy` fields from `JWT` token.

Please note we don't have `CreatedAt` and `UpdatedAt` in this struct.
```go
type createMovieRequest struct {
	ID          string    `json:"id"`
	Title       string    `json:"title"`
	Director    string    `json:"director"`
	ReleaseDate time.Time `json:"release_date"`
	TicketPrice float64   `json:"ticket_price"`
}

func (mr *createMovieRequest) Bind(r *http.Request) error {
	return nil
}
```

In handler we bind request body to our struct, if `Bind` is successful then convert it to `CreateMovieParams` struct expected by `store.Create` method and call `Create` method to add movie to data store. If there is a duplicate key error we return `409 Conflict` for unknown errors we return `500 InternalServerError` and if all is successful we are returning `200 OK`.
```go
func (s *Server) handleCreateMovie() http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		data := &createMovieRequest{}
		if err := render.Bind(r, data); err != nil {
			render.Render(w, r, ErrBadRequest)
			return
		}

		createMovieParams := store.NewCreateMovieParams(
			uuid.MustParse(data.ID),
			data.Title,
			data.Director,
			data.ReleaseDate,
			data.TicketPrice,
		)
		err := s.store.Create(r.Context(), createMovieParams)
		if err != nil {
			var dupIdErr *store.DuplicateIDError
			if errors.As(err, &dupIdErr) {
				render.Render(w, r, ErrConflict(err))
			} else {
				render.Render(w, r, ErrInternalServerError)
			}
			return
		}

		w.WriteHeader(200)
		w.Write(nil)
	}
}
```

### Update Movie
Same as `Create Movie` above, we introduced a new struct `updateMovieRequest` to receive parameters required to update movie and implemeted `Binder` interface for the struct.
```go
type updateMovieRequest struct {
	Title       string    `json:"title"`
	Director    string    `json:"director"`
	ReleaseDate time.Time `json:"release_date"`
	TicketPrice float64   `json:"ticket_price"`
}

func (mr *updateMovieRequest) Bind(r *http.Request) error {
	return nil
}
```

In hander we read the `id` from path, then we bind the struct from request body. If no errors then we convert the request to `store.UpdateMovieParams` and call `Update` method of store to update movie. We return `200 OK` if upate is successful.
```go
func (s *Server) handleUpdateMovie() http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		idParam := chi.URLParam(r, "id")
		id, err := uuid.Parse(idParam)
		if err != nil {
			render.Render(w, r, ErrBadRequest)
			return
		}

		data := &updateMovieRequest{}
		if err := render.Bind(r, data); err != nil {
			render.Render(w, r, ErrBadRequest)
			return
		}

		updateMovieParams := store.NewUpdateMovieParams(
			data.Title,
			data.Director,
			data.ReleaseDate,
			data.TicketPrice,
		)
		err = s.store.Update(r.Context(), id, updateMovieParams)
		if err != nil {
			var rnfErr *store.RecordNotFoundError
			if errors.As(err, &rnfErr) {
				render.Render(w, r, ErrNotFound)
			} else {
				render.Render(w, r, ErrInternalServerError)
			}
			return
		}

		w.WriteHeader(200)
		w.Write(nil)
	}
}
```

### Delete Movie
This probably is the simplest handler as it does not need any `Renderer` or `Binder`, we simply get `id` from the path, and call `Delete` method of store to delete the resource. If delete is successful we return `200 OK`.
```go
func (s *Server) handleDeleteMovie() http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		idParam := chi.URLParam(r, "id")
		id, err := uuid.Parse(idParam)
		if err != nil {
			render.Render(w, r, ErrBadRequest)
			return
		}

		err = s.store.Delete(r.Context(), id)
		if err != nil {
			var rnfErr *store.RecordNotFoundError
			if errors.As(err, &rnfErr) {
				render.Render(w, r, ErrNotFound)
			} else {
				render.Render(w, r, ErrInternalServerError)
			}
			return
		}

		w.WriteHeader(200)
		w.Write(nil)
	}
}
```

## Start Server
Now everything is setup, its time to update `main` method. Start by laoding the configuration, then create an instance of the `InMemoryMoviesStore`, here we can also instantiate any other dependencies our server is dependent on. Next step is to create an instance of `api.Server` struct and call the `Start` method to start the server. Server would start listening on the configured port and you can invoke endpoints using `curl` or `Postman`.
```go
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
```

## Testing
I am going to list steps to manually test the api endpoints, as we don't have `Swagger UI` or any other UI to interact with this, `Postman` can be used to test the endpoints as well.
* Start Server executing following
```shell
go run main.go
```
Execute following tests in order, remember to update the port if you are running on a different port than 8080.

### Tests
#### Get All returns empty list
##### Request
```shell
curl --request GET --url "http://localhost:8080/api/movies"
```
##### Expected Response
```json
[]
```
#### Get By ID should return Not Found
##### Request
```shell
curl --request GET --url "http://localhost:8080/api/movies/1"
```
##### Expected Response
```json
[]
```
#### Get By ID should return Not Found
##### Request
```shell
curl --request GET --url "http://localhost:8080/api/movies/1"
```
##### Expected Response
```json
[]
```
#### Get By ID should return Not Found
##### Request
```shell
curl --request GET --url "http://localhost:8080/api/movies/1"
```
##### Expected Response
```json
[]
```
#### Get By ID with invalid id
##### Request
```shell
curl --request GET --url "http://localhost:8080/api/movies/1"
```
##### Expected Response
```json
{"status":"Bad request"}
```
#### Get by ID with non-existent record
##### Request
```shell
curl --request GET --url "http://localhost:8080/api/movies/98268a96-a6ac-444f-852a-c6472129aa22"
```
##### Expected Response
```json
{"status":"Resource not found."}
```
#### Create Movie
##### Request
```shell
curl --request POST --data '{ "id": "98268a96-a6ac-444f-852a-c6472129aa22", "title": "Star Wars: Episode I – The Phantom Menace", "director": "George Lucas", "release_date": "1999-05-16T01:01:01.00Z", "ticket_price": 10.70 }' --url "http://localhost:8080/api/movies"
```
##### Expected Response
```json
```
#### Create Movie with existing ID
##### Request
```shell
curl --request POST --data '{ "id": "98268a96-a6ac-444f-852a-c6472129aa22", "title": "Star Wars: Episode I – The Phantom Menace", "director": "George Lucas", "release_date": "1999-05-16T01:01:01.00Z", "ticket_price": 10.70 }' --url "http://localhost:8080/api/movies"
```
##### Expected Response
```json
{"status":"Duplicate ID","error":"duplicate movie id: 98268a96-a6ac-444f-852a-c6472129aa22"}
```
#### Get ALL Movies
##### Request
```shell
curl --request GET --url "http://localhost:8080/api/movies"
```
##### Expected Response
```json
[{"id":"98268a96-a6ac-444f-852a-c6472129aa22","title":"Star Wars: Episode I – The Phantom Menace","director":"George Lucas","release_date":"1999-05-16T01:01:01Z","ticket_price":10.7}]
```
#### Get Movie By ID
##### Request
```shell
curl --request GET --url "http://localhost:8080/api/movies/98268a96-a6ac-444f-852a-c6472129aa22"
```
##### Expected Response
```json
{"id":"98268a96-a6ac-444f-852a-c6472129aa22","title":"Star Wars: Episode I – The Phantom Menace","director":"George Lucas","release_date":"1999-05-16T01:01:01Z","ticket_price":10.7}
```
#### Update Movie
##### Request
```shell
curl --request PUT --data '{ "title": "Star Wars: Episode I – The Phantom Menace", "director": "George Lucas", "release_date": "1999-05-16T01:01:01.00Z", "ticket_price": 20.70 }' --url "http://localhost:8080/api/movies/98268a96-a6ac-444f-852a-c6472129aa22"
```
##### Expected Response
```json
```
#### Get Movie by ID - get updated record
##### Request
```shell
curl --request GET --url "http://localhost:8080/api/movies/98268a96-a6ac-444f-852a-c6472129aa22"
```
##### Expected Response
```json
{"id":"98268a96-a6ac-444f-852a-c6472129aa22","title":"Star Wars: Episode I – The Phantom Menace","director":"George Lucas","release_date":"1999-05-16T01:01:01Z","ticket_price":20.7}
```
#### Delete Movie
##### Request
```shell
curl --request DELETE --url "http://localhost:8080/api/movies/98268a96-a6ac-444f-852a-c6472129aa22"
```
##### Expected Response
```json
```
#### Get Movie By Id - deleted record
##### Request
```shell
curl --request GET --url "http://localhost:8080/api/movies/98268a96-a6ac-444f-852a-c6472129aa22"
```
##### Expected Response
```json
{"status":"Resource not found."}
```

## References
In no particular order
* [What is a REST API?](https://www.ibm.com/topics/rest-apis)
* [envconfig](https://github.com/kelseyhightower/envconfig)
* [chi](https://github.com/go-chi/chi)
* And many more