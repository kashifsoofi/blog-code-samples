using Microsoft.AspNetCore.Mvc;
using Movies.Api.Domain;
using Movies.Api.Requests;
using Movies.Api.Store;
using Movies.Api.Store.InMemory;

namespace Movies.Api.Controllers;

[Route("api/[controller]")]
public class MoviesController : Controller
{
    private readonly IMoviesStore moviesStore;

    public MoviesController(IMoviesStore moviesStore)
    {
        this.moviesStore = moviesStore;
    }

    // GET: api/movies
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(IEnumerable<Domain.Movie>), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var movies = moviesStore.GetAll().Select(x => new Domain.Movie(x));
        return Ok(movies);
    }

    // GET api/movies/5
    [HttpGet("{id}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Domain.Movie), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Domain.Movie), StatusCodes.Status404NotFound)]
    public IActionResult Get(Guid id)
    {
        var movie = moviesStore.GetById(id);
        if (movie == null)
        {
            return NotFound();
        }

        return Ok(new Domain.Movie(movie));
    }

    // POST api/movies
    [HttpPost]
    [Consumes(typeof(CreateMovieRequest), "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Post([FromBody] CreateMovieRequest request)
    {
        try
        {
            moviesStore.Create(new CreateMovieParams(
                request.Id,
                request.Title,
                request.Director,
                request.TicketPrice,
                request.ReleaseDate
                ));
        }
        catch (DuplicateKeyException)
        {
            return Conflict();
        }

        return Ok();
    }

    // PUT api/movies/5
    [HttpPut("{id}")]
    [Consumes(typeof(UpdateMovieRequest), "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Put(Guid id, [FromBody] UpdateMovieRequest request)
    {
        try
        {
            moviesStore.Update(id, new UpdateMovieParams(
                request.Title,
                request.Director,
                request.TicketPrice,
                request.ReleaseDate
                ));
        }
        catch (RecordNotFoundException)
        {
            return NotFound();
        }

        return Ok();
    }

    // DELETE api/values/5
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid id)
    {
        try
        {
            moviesStore.Delete(id);
        }
        catch (RecordNotFoundException)
        {
            return NotFound();
        }

        return Ok();
    }
}

