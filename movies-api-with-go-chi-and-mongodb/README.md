# REST API with Go, Chi and MongoDB
This is a continuation of an earlier post [REST API with Go, Chi and InMemory Store](https://kashifsoofi.github.io/go/rest/restapi-with-go-chi-and-inmemory-store/). In this tutorial I will extend the service to store data in a [MongoDB](https://www.mongodb.com/), I will be using [MongoDB Community Server Docker Image](https://hub.docker.com/r/mongodb/mongodb-community-server) for this sample. I will use [Docker](https://www.docker.com/) to run MongoDB.

## Setup Database Server
I will be using a docker-compose to run MongoDB in a docker container. This would allow us the add more services that our rest api is depenedent on e.g. redis server for distributed caching.

Let's start by adding a new file by right clicking on Solution name in Visual Studio and Add New File. I like to name file as `docker-compose.dev-env.yml`, feel free to name it as you like. Add following content to add a database instance for movies rest api.
```yaml
version: '3.7'

services:
  movies.db:
    image: mongodb/mongodb-community-server:6.0-ubi8
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
Add a new file named `mongo_movies_store.go` under `store` folder. Add a new struct `MongoMoviesStore` containing `databaseUrl` and a pointer to `sqlx.DB`, also add helper methods to `connect` to database and `close` connection as well. Also note that I have added a `noOpMapper` method and set as MapperFunc of `sqlx.DB`, reason for this is to use the same casing as the struct field name. Default behaviour for `sqlx` is to map field names to lower case column names.

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


### GetAll


### GetByID


### Update


### Delete


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
store := store.NewMongoMoviesStore(cfg.DatabaseURL)
```

## Test
I am not adding any unit or integration tests for this tutorial, perhaps a following tutorial. But all the endpoints can be tested either using Postman for by following test plan from [previous article](https://kashifsoofi.github.io/go/rest/restapi-with-go-chi-and-inmemory-store/#testing).

You can start rest api with SQL Server running in docker by executing following
```shell
DATABASE_URL=mongodb://localhost:27017 go run main.go
```

## Source
Source code for the demo application is hosted on GitHub in [blog-code-samples](https://github.com/kashifsoofi/blog-code-samples/tree/main/restapi-with-go-chi-and-mongodb) repository.

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