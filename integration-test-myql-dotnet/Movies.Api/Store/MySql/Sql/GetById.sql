SELECT
    Id,
    Title,
    Director,
    TicketPrice,
    ReleaseDate,
    CreatedAt,
    UpdatedAt
FROM Movies
WHERE Id = @Id