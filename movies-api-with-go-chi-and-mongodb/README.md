# REST API with Go, Chi and MongoDB
This is a continuation of an earlier post [REST API with Go, Chi and InMemory Store](https://kashifsoofi.github.io/go/rest/restapi-with-go-chi-and-inmemory-store/). In this tutorial I will extend the service to store data in a [MongoDB](https://www.mongodb.com/), I will be using [MongoDB Community Server Docker Image](https://hub.docker.com/r/mongodb/mongodb-community-server) for this sample. I will use [Docker](https://www.docker.com/) to run MongoDB.

## Setup Database Server
I will be using a docker-compose to run MongoDB in a docker container. This would allow us the add more services that our rest api is depenedent on e.g. redis server for distributed caching.

Let's start by adding a new file by right clicking on Solution name in Visual Studio and Add New File. I like to name file as `docker-compose.dev-env.yml`, feel free to name it as you like. Add following content to add a database instance for movies rest api.
```yaml
version: '3.7'

services:
  movies.db:
    image: mongodb/mongodb-community-server:6.0.5-ubuntu2204
    environment:
      - MONGODB_INITDB_ROOT_USERNAME=root
      - MONGODB_INITDB_ROOT_PASSWORD=Password123
      - MONGO_INITDB_DATABASE=Movies
    volumes:
      - moviesdbdata:/data/db
    ports:
      - "27017:27017"

volumes:
  moviesdbdata:
```

Open a terminal at the root of the solution where docker-compose file is location and execute following command to start database server.
```shell
docker-compose -f docker-compose.dev-env.yml up -d
```

## Database Migrations
We would not do any database/schema migrations for MongoDB as its a NoSQL database, [here](https://stackoverflow.com/a/49446108) is an excellent discussion on Stackoverflow on this topic. We don't need any migration for this sample however if the need arise and there is no strong use case of a schema migration script I would prefer to opt the route of supporting multiple schemas conconcurrently and update when required.

## MongoDB Movies Store
Add a new file named `mongo_movies_store.go` under `store` folder. Add a new struct `MongoMoviesStore` containing `databaseUrl`, a pointer to `mongo.Client` and a pointer to the `mongo.Collection` we will be working with. We will also add helper methods to `connect` to database and initialise `collection` field that we will use in each of the `CRUD` methods, and a helper method to `close` connection. 

```go
package store

import (
	"context"
	"time"

	"github.com/google/uuid"
	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-mongodb/config"
	"go.mongodb.org/mongo-driver/bson"
	"go.mongodb.org/mongo-driver/mongo"
	"go.mongodb.org/mongo-driver/mongo/options"
)

type MongoMoviesStore struct {
	database   config.Database
	client     *mongo.Client
	collection *mongo.Collection
}

func NewMongoMoviesStore(config config.Database) *MongoMoviesStore {
	return &MongoMoviesStore{
		database: config,
	}
}

func (s *MongoMoviesStore) connect(ctx context.Context) error {
	serverAPI := options.ServerAPI(options.ServerAPIVersion1)

	client, err := mongo.Connect(
		ctx,
		options.Client().ApplyURI(s.database.DatabaseURL).SetServerAPIOptions(serverAPI),
	)
	if err != nil {
		return err
	}

	s.client = client
	s.collection = s.client.Database(s.database.DatabaseName).Collection(s.database.MoviesCollectionName)
	return nil
}

func (s *MongoMoviesStore) close(ctx context.Context) error {
	return s.client.Disconnect(ctx)
}
```

### Add db tag
Update `Movie` struct in `movies_store.go` file to add tag to mark `ID` field as the ObjectID of Mongo. All other fields will be mapped as it is when saving and loading documents.

```go
type Movie struct {
	ID          uuid.UUID `bson:"_id"`
	...
}
```

### Context
We did not make use of the `Context` in the earlier sample `movies-api-with-go-chi-and-memory-store`, now that we are connecting to an external storage and package we are going to use to run queries support methods accepting `Context` we will update our `store.Interface` to accept `Context` and use that when running queries. `store.Interface` will be updated as follows
```go
type Interface interface {
	GetAll(ctx context.Context) ([]Movie, error)
	GetByID(ctx context.Context, id uuid.UUID) (Movie, error)
	Create(ctx context.Context, createMovieParams CreateMovieParams) error
	Update(ctx context.Context, id uuid.UUID, updateMovieParams UpdateMovieParams) error
	Delete(ctx context.Context, id uuid.UUID) error
}
```
We will also need to update `MemoryMoviesStore` methods to accept `Context` to satisfy `store.Interface` and update methods in `movies_handler` to pass request context using `r.Context()` when calling `store` methods.

### Create
We connect to database using `connect` helper method, create a new instance of `Movie` and execute `InsertOne` method with newly created instance. We are handling an `error` and return `DuplicateKeyError` if returned error is a mongo `DuplicateKeyError`. If insert is successful then we return `nil`.
Create function looks like

```go
func (s *MongoMoviesStore) Create(ctx context.Context, createMovieParams CreateMovieParams) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close(ctx)

	movie := Movie{
		ID:          createMovieParams.ID,
		Title:       createMovieParams.Title,
		Director:    createMovieParams.Director,
		ReleaseDate: createMovieParams.ReleaseDate,
		TicketPrice: createMovieParams.TicketPrice,
		CreatedAt:   time.Now().UTC(),
		UpdatedAt:   time.Now().UTC(),
	}

	if _, err := s.collection.InsertOne(ctx, movie); err != nil {
		if mongo.IsDuplicateKeyError(err) {
			return &DuplicateKeyError{ID: createMovieParams.ID}
		}
		return err
	}

	return nil
}
```

### GetAll
We connect to database using `connect` helper method, we call `Find` method on our collection to get a `cursor` with an empty filter to get all movie documents. We then use `All` method on cursor to retrieve all documents into a slice.

```go
func (s *MongoMoviesStore) GetAll(ctx context.Context) ([]Movie, error) {
	err := s.connect(ctx)
	if err != nil {
		return nil, err
	}
	defer s.close(ctx)

	cur, err := s.collection.Find(ctx, bson.D{})
	if err != nil {
		return nil, err
	}
	defer cur.Close(ctx)

	var movies []Movie
	if err := cur.All(ctx, &movies); err != nil {
		return nil, err
	}

	return movies, nil
}
```

### GetByID
We connect to database using `connect` helper method, we call `FindOne` method on our collection by passing the requested id and decode the result into a `Movie` instance. We are checking if `FindOne` retuns an `ErrNoDocuments` and return our custom `RecordNotFound` error to caller. If no error document loaded from the collection is returned.

```go
func (s *MongoMoviesStore) GetByID(ctx context.Context, id uuid.UUID) (Movie, error) {
	err := s.connect(ctx)
	if err != nil {
		return Movie{}, err
	}
	defer s.close(ctx)

	var movie Movie
	if err := s.collection.FindOne(ctx, bson.M{"_id": id}).Decode(&movie); err != nil {
		if err == mongo.ErrNoDocuments {
			return Movie{}, &RecordNotFoundError{}
		}
		return Movie{}, err
	}

	return movie, nil
}
```

### Update
We connect to database using `connect` helper method, then prepare and `update` set using the fields from `updateMovieParams` and then call `UpdateOne` method on our collection to update all the fields.
Here we are updating all the passed fields and not supporting partial updates, this means caller is responsible for correctly setting the fields to previous value if they don't want to change a specific field. This method can be enhanced to support partial updates.

```go
func (s *MongoMoviesStore) Update(ctx context.Context, id uuid.UUID, updateMovieParams UpdateMovieParams) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close(ctx)

	update := bson.M{
		"$set": bson.M{
			"Title":       updateMovieParams.Title,
			"Director":    updateMovieParams.Director,
			"ReleaseDate": updateMovieParams.ReleaseDate,
			"TicketPrice": updateMovieParams.TicketPrice,
			"UpdatedAt":   time.Now().UTC(),
		},
	}
	if _, err := s.collection.UpdateOne(ctx, bson.M{"_id": id}, update); err != nil {
		return err
	}

	return nil
}
```

### Delete
We connect to database using `connect` helper method, then we call `DeleteOne` method on our collection by passing the requested id to delete the record.

```go
func (s *MongoMoviesStore) Delete(ctx context.Context, id uuid.UUID) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close(ctx)

	if _, err := s.collection.DeleteOne(ctx, bson.M{"_id": id}); err != nil {
		return err
	}

	return nil
}
```

## Database Configuration
Add a new struct named `Database` in `config.go` and add that to `Configuration` struct as well.
```go
type Configuration struct {
	HTTPServer
	Database
}
...
type Database struct {
	DatabaseURL          string `envconfig:"DATABASE_URL" required:"true"`
	DatabaseName         string `envconfig:"DATABASE_NAME" default:"MoviesStore"`
	MoviesCollectionName string `envconfig:"MOVIES_COLLECTION_NAME" default:"MoviesCollectionName"`
}
```

## Dependency Injection
Update `main.go` as follows to create a new instance of `MongoMoviesStore`, I have opted to create instance of `MongoMoviesStore` instead of `MemoryMoviesStore`, solution can be enhanced to create either one of the dependency based on a configuration.
```go
// store := store.NewMemoryMoviesStore()
store := store.NewMongoMoviesStore(cfg.Database)
```

## Test
I am not adding any unit or integration tests for this tutorial, perhaps a following tutorial. But all the endpoints can be tested either using Postman for by following test plan from [previous article](https://kashifsoofi.github.io/go/rest/restapi-with-go-chi-and-inmemory-store/#testing).

You can start rest api with SQL Server running in docker by executing following
```shell
DATABASE_URL=mongodb://root:Password123@localhost:27017 go run main.go
```

## Source
Source code for the demo application is hosted on GitHub in [blog-code-samples](https://github.com/kashifsoofi/blog-code-samples/tree/main/movies-api-with-go-chi-and-mongodb) repository.

## References
In no particular order
* [What is a REST API?](https://www.ibm.com/topics/rest-apis)
* [envconfig](https://github.com/kelseyhightower/envconfig)
* [chi](https://github.com/go-chi/chi)
* [Docker](https://www.docker.com/)
* [MongoDB](https://www.mongodb.com/)
* [MongoDB Community Server Docker Image](https://hub.docker.com/r/mongodb/mongodb-community-server)
* [Install MongoDB Community with Docker](https://www.mongodb.com/docs/manual/tutorial/install-mongodb-community-with-docker/)
* [Schema Migration Scripts in NoSQL Databases](https://stackoverflow.com/a/49446108)
* And many more