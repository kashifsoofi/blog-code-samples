using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Movies.Api.Configuration;

namespace Movies.Api.Store.Mongo;

public class MongoMoviesStore : IMoviesStore
{
    private readonly IMongoCollection<Movie> moviesCollection;

    public MongoMoviesStore(IOptions<MoviesStoreConfiguration> moviesStoreConfiguration)
	{
        var mongoClient = new MongoClient(moviesStoreConfiguration.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(moviesStoreConfiguration.Value.DatabaseName);

        moviesCollection = mongoDatabase.GetCollection<Movie>(moviesStoreConfiguration.Value.MoviesCollectionName);
    }

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

    public async Task<IEnumerable<Movie>> GetAll()
    {
        return await moviesCollection.Find(_ => true).ToListAsync();
    }

    public async Task<Movie?> GetById(Guid id)
    {
        return await moviesCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

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

    public async Task Delete(Guid id)
    {
        await moviesCollection.DeleteOneAsync(x => x.Id == id);
    }
}

