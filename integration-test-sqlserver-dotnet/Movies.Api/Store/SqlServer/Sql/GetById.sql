SELECT
    Id,
    Title,
    Director,
    ReleaseDate,
    TicketPrice,
    CreatedAt,
    UpdatedAt
FROM Movies
WHERE Id = @Id