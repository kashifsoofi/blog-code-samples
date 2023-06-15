# REST API using ASP.NET Core 7 and MongoDB
This is a continuation of an earlier post [REST API with ASP.NET Core 7 and InMemory Store](https://kashifsoofi.github.io/aspnetcore/rest/restapi-with-asp.net-core-7-and-inmemory-store/). In this tutorial I will extend the service to store data in [MongoDB](https://www.mongodb.com/), I will be using [MongoDB Community Server Docker Image](https://hub.docker.com/r/mongodb/mongodb-community-server) for this sample. I will use [Docker](https://www.docker.com/) to run MongoDB.

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

### Setup
* Lets start by adding nuget packages
```shell
dotnet add package MongoDB.Driver --version 2.19.2
dotnet add package MongoDB.Bson --version 2.19.2
```
* Update `IMovieStore` and make all methods `async`.
* Update `Controller` to make methods `async` and `await` calls to store methods
* Update `InMemoryMoviesStore` to make methods `async`

### Configuration
Add a new folder `Configuration` and add `MoviesStoreConfiguration.cs` file.
```csharp
public class MoviesStoreConfiguration
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string MoviesCollectionName { get; set; } = null!;
}
```
Add following to the appsettings.json
```json
"MoviesStoreConfiguration": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "MoviesStore",
    "MoviesCollectionName": "Movies"
  }
```
Register the configuration in Dependency Injection container in `Program.cs`
```csharp
// Add services to the container.
builder.Services.Configure<MoviesStoreConfiguration>(
    builder.Configuration.GetSection(nameof(MoviesStoreConfiguration)));
```

### Class and Constructor
Add a new folder under `Store`, I named it as `Mongo` and add a file named `MongoMoviesStore.cs`. This class would accept an `IOptions<MoviesStoreConfiguration>` as parameter that we would use to connect to MongoDB, get database and MongoCollection.
```csharp
private readonly IMongoCollection<Movie> moviesCollection;

public MongoMoviesStore(IOptions<MoviesStoreConfiguration> moviesStoreConfiguration)
{
    var mongoClient = new MongoClient(moviesStoreConfiguration.Value.ConnectionString);
    var mongoDatabase = mongoClient.GetDatabase(moviesStoreConfiguration.Value.DatabaseName);

    moviesCollection = mongoDatabase.GetCollection<Movie>(moviesStoreConfiguration.Value.MoviesCollectionName);
}
```

I have specified this in `appsettings.json` configuration file. This is acceptable for development but NEVER put a production/stagging connection string in a configuration file. This can be put in secure vault e.g. AWS Parameter Store or Azure KeyVault and can be accessed from the application. CD pipeline can also be configured to load this value from a secure location and set as an environment variable for the container running the application.

### Create
We create a new instance of `Movie`, then we use `moviesCollection` to insert a new record, we are handling a `MongoWriteException` and throw our custom `DuplicateKeyException` if `WriteError.Code` of exception is `11000`.

Create function looks like
```csharp
public async Task Create(CreateMovieParams createMovieParams)
{
    var movie = new Movie(
        createMovieParams.Id,
        createMovieParams.Title,
        createMovieParams.Director,
        createMovieParams.TicketPrice,
        createMovieParams.ReleaseDate,
        DateTime.UtcNow,
        DateTime.UtcNow);
    try
    {
        await moviesCollection.InsertOneAsync(movie);
    }
    catch (MongoWriteException ex)
    {
        if (ex.WriteError.Category == ServerErrorCategory.DuplicateKey &&
            ex.WriteError.Code == 11000)
        {
            throw new DuplicateKeyException();
        }

        throw;
    }
}
```

### GetAll
We use moviesCollection to find all records.
```csharp
public async Task<IEnumerable<Movie>> GetAll()
{
    return await moviesCollection.Find(_ => true).ToListAsync();
}
```

### GetById
We use moviesCollection and filter on the `Id` property of the documents to get first or default instance of collection matching with passed parameter.
```csharp
public async Task<Movie?> GetById(Guid id)
{
    return await moviesCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
}
```

### Update
We use moviesCollection and use `UpdateOneAsync` method filtering record with `id` parameter and passing all updateable properties as `UpdateDefinition`.

Update function looks like
```csharp
public async Task Update(Guid id, UpdateMovieParams updateMovieParams)
{
    await moviesCollection.UpdateOneAsync(
        x => x.Id == id,
        Builders<Movie>.Update.Combine(
            Builders<Movie>.Update.Set(x => x.Title, updateMovieParams.Title),
            Builders<Movie>.Update.Set(x => x.Director, updateMovieParams.Director),
            Builders<Movie>.Update.Set(x => x.TicketPrice, updateMovieParams.TicketPrice),
            Builders<Movie>.Update.Set(x => x.ReleaseDate, updateMovieParams.ReleaseDate),
            Builders<Movie>.Update.Set(x => x.UpdatedAt, DateTime.UtcNow)
        ));
}
```

### Delete
We use moviesCollection and use `DeleteOneAsync` method of collection to delete the record using `id`.
```csharp
public async Task Delete(Guid id)
{
    await moviesCollection.DeleteOneAsync(x => x.Id == id);
}
```

Please note we don't throw `RecordNotFoundException` exception as we were doing in `InMemoryMoviesStore`, reason for that is trying to delete a record with a non existent key is not considered an error.

## Setup Dependency Injection
Final step is to setup the Dependency Injection container to wireup the new created store. Update `Program.cs` as shown below
```csharp
// builder.Services.AddSingleton<IMoviesStore, InMemoryMoviesStore>();
builder.Services.AddSingleton<IMoviesStore, MongoMoviesStore>();
```

For simplicity I have disabled `InMemoryMoviesStore`, we can add a configuration and based on that decide which service to use at runtime. That can be a good exercise however we don't do that practically. However for traffic heavy services InMemory or Distributed Cache is used to cache results to improve performance.

## Test
I am not adding any unit or integration tests for this tutorial, perhaps a following tutorial. But all the endpoints can be tested either by the Swagger UI by running the application or using Postman.

## References
In no particular order
* [What is a REST API?](https://www.ibm.com/topics/rest-apis)
* [REST API with ASP.NET Core 7 and InMemory Store](https://kashifsoofi.github.io/aspnetcore/rest/restapi-with-asp.net-core-7-and-inmemory-store/)
* [Docker](https://www.docker.com/)
* [MongoDB](https://www.mongodb.com/)
* [MongoDB Community Server Docker Image](https://hub.docker.com/r/mongodb/mongodb-community-server)
* [Install MongoDB Community with Docker](https://www.mongodb.com/docs/manual/tutorial/install-mongodb-community-with-docker/)
* [Schema Migration Scripts in NoSQL Databases](https://stackoverflow.com/a/49446108)
* And many more
