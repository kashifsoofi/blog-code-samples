namespace Movies.Api.Store;

public class CreateMovieParams
{
    public Guid Id { get; }
    public string Title { get; }
    public string Director { get; }
    public decimal TicketPrice { get; }
    public DateTime ReleaseDate { get; }

    public CreateMovieParams(
        Guid id,
        string title,
        string director,
        decimal ticketPrice,
        DateTime releaseDate
        )
    {
        Id = id;
        Title = title;
        Director = director;
        TicketPrice = ticketPrice;
        ReleaseDate = releaseDate;
    }
}

