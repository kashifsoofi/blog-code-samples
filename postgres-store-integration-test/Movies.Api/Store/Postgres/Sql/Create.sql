INSERT INTO movies(
    id,
    title,
    director,
    release_date,
    ticket_price,
    created_at,
    updated_at
)
VALUES (
    @id,
    @title,
    @director,
    @release_date,
    @ticket_price,
    @created_at,
    @updated_at
)
