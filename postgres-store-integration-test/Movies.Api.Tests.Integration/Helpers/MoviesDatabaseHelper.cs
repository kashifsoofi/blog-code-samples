using System;
using System.Data;
using Dapper;
using Movies.Api.Store;
using Npgsql;
using Xunit;

namespace Movies.Api.Tests.Integration.Helpers;

public class MoviesDatabaseHelper : DatabaseHelper<Guid, Movie>
{
	public MoviesDatabaseHelper(string connectionString)
		: base(connectionString, "movies", x => x.Id, "id")
    { }

    public async override Task AddRecordAsync(Movie record)
    {
        this.AddedRecords.Add(idSelector(record), record);

        var parameters = new
        {
            id = record.Id,
            title = record.Title,
            director = record.Director,
            release_date = record.ReleaseDate,
            ticket_price = record.TicketPrice,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
        };

        var query = @"
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
            )";

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.ExecuteAsync(query, parameters, commandType: CommandType.Text);
    }

    public async override Task<Movie> GetRecordAsync(Guid id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        return await connection.QueryFirstOrDefaultAsync<Movie>(
            $"SELECT id, title, director, ticket_price as TicketPrice, release_date as ReleaseDate, created_at as CreatedAt, updated_at as UpdatedAt FROM movies WHERE id = @id",
            new { id },
            commandType: CommandType.Text);
    }
}
