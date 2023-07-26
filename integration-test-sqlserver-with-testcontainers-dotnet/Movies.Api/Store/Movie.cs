namespace Movies.Api.Store;

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
        ReleaseDate = releaseDate;
        TicketPrice = ticketPrice;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }
}

