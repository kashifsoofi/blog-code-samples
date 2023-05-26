namespace Movies.Api.Store.InMemory;

public class InMemoryMoviesStore : IMoviesStore
{
    private readonly Dictionary<Guid, Movie> repository = new Dictionary<Guid, Movie>();

    public IEnumerable<Movie> GetAll()
    {
        return repository.Values.AsEnumerable();
    }

    public Movie? GetById(Guid id)
    {
        if (repository.ContainsKey(id))
        {
            return repository[id];
        }

        return null;
    }

    public void Create(CreateMovieParams createMovieParams)
    {
        if (repository.ContainsKey(createMovieParams.Id))
        {
            throw new DuplicateKeyException($"Duplicate movie id: {createMovieParams.Id}");
        }

        var movie = new Movie(
            createMovieParams.Id,
            createMovieParams.Title,
            createMovieParams.Director,
            createMovieParams.TicketPrice,
            createMovieParams.ReleaseDate,
            DateTime.UtcNow,
            DateTime.UtcNow);
        repository.Add(movie.Id, movie);
    }

    public void Update(Guid id, UpdateMovieParams updateMovieParams)
    {
        if (!repository.ContainsKey(id))
        {
            throw new RecordNotFoundException();
        }

        var movieToUpdate = repository[id];
        var movie = new Movie(
            movieToUpdate.Id,
            updateMovieParams.Title,
            updateMovieParams.Director,
            updateMovieParams.TicketPrice,
            updateMovieParams.ReleaseDate,
            movieToUpdate.CreatedAt,
            DateTime.UtcNow);

        repository[id] = movie;
    }

    public void Delete(Guid id)
    {
        if (!repository.ContainsKey(id))
        {
            throw new RecordNotFoundException();
        }

        repository.Remove(id);
    }
}

