UPDATE Movies
SET
    Title = @Title,
    Director = @Director,
    ReleaseDate = @ReleaseDate,
    TicketPrice = @TicketPrice,
    UpdatedAt = @UpdatedAt
WHERE id = @id