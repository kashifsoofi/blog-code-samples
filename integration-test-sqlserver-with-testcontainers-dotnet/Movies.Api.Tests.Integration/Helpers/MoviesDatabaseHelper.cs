using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Movies.Api.Store;

namespace Movies.Api.Tests.Integration.Helpers;

public class MoviesDatabaseHelper : DatabaseHelper<Guid, Movie>
{
	public MoviesDatabaseHelper(string connectionString)
		: base(connectionString, "Movies", x => x.Id, "Id")
    { }

    public async override Task AddRecordAsync(Movie record)
    {
        this.AddedRecords.Add(idSelector(record), record);

        var parameters = new
        {
            record.Id,
            record.Title,
            record.Director,
            record.ReleaseDate,
            record.TicketPrice,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var query = @"
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
            )";

        await using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync(query, parameters, commandType: CommandType.Text);
    }

    public async override Task<Movie> GetRecordAsync(Guid id)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.QueryFirstOrDefaultAsync<Movie>(
            $"SELECT Id, Title, Director, ReleaseDate, TicketPrice, CreatedAt, UpdatedAt FROM Movies WHERE Id = @Id",
            new { Id = id },
            commandType: CommandType.Text);
    }
}
