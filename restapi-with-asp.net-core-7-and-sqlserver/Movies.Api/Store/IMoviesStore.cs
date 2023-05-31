namespace Movies.Api.Store;

public interface IMoviesStore
{
    Task<IEnumerable<Movie>> GetAll();
    Task<Movie?> GetById(Guid id);
    Task Create(CreateMovieParams createMovieParams);
    Task Update(Guid id, UpdateMovieParams updateMovieParams);
    Task Delete(Guid id);
}

