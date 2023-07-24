namespace Movies.Api.Requests;

public class CreateMovieRequest
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Director { get; set; }
    public decimal TicketPrice { get; set; }
    public DateTime ReleaseDate { get; set; }
}

