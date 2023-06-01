namespace Movies.Api.Store;

public interface IMoviesStore
{
    IEnumerable<Movie> GetAll();
    Movie? GetById(Guid id);
    void Create(CreateMovieParams createMovieParams);
    void Update(Guid id, UpdateMovieParams updateMovieParams);
    void Delete(Guid id);
}

