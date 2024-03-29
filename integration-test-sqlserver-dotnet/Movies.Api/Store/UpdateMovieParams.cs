﻿namespace Movies.Api.Store;

public class UpdateMovieParams
{
    public string Title { get; }
    public string Director { get; }
    public decimal TicketPrice { get; }
    public DateTimeOffset ReleaseDate { get; }

    public UpdateMovieParams(
        string title,
        string director,
        decimal ticketPrice,
        DateTimeOffset releaseDate
        )
    {
        Title = title;
        Director = director;
        TicketPrice = ticketPrice;
        ReleaseDate = releaseDate;
    }
}

