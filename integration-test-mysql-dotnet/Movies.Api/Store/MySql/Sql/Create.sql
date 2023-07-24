INSERT INTO Movies(
    Id,
    Title,
    Director,
    ReleaseDate,
    TicketPrice,
    CreatedAt,
    UpdatedAt
)
VALUES (
    @Id,
    @Title,
    @Director,
    @ReleaseDate,
    @TicketPrice,
    @CreatedAt,
    @UpdatedAt
)