namespace Movies.Api.Store;

public class Movie
{
    public Guid Id { get; }
    public string Title { get; }
    public string Director { get; }
    public decimal TicketPrice { get; }
    public DateTime ReleaseDate { get; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; }

    public Movie(
        Guid id,
        string title,
        string director,
        decimal ticketPrice,
        DateTime releaseDate,
        DateTime createdAt,
        DateTime updatedAt
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
}

