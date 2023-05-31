namespace Movies.Api.Domain;

public class Movie
{
    public Guid Id { get; }
    public string Title { get; }
    public string Director { get; }
    public DateTimeOffset ReleaseDate { get; }
    public decimal TicketPrice { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }

    public Movie(
        Guid id,
        string title,
        string director,
        DateTimeOffset releaseDate,
        decimal ticketPrice,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt
        )
    {
        Id = id;
        Title = title;
        Director = director;
        TicketPrice = ticketPrice;
        ReleaseDate = releaseDate;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Movie(Store.Movie storeMovie)
    {
        Id = storeMovie.Id;
        Title = storeMovie.Title;
        Director = storeMovie.Director;
        TicketPrice = storeMovie.TicketPrice;
        ReleaseDate = storeMovie.ReleaseDate;
        CreatedAt = storeMovie.CreatedAt;
        UpdatedAt = storeMovie.UpdatedAt;
    }
}

