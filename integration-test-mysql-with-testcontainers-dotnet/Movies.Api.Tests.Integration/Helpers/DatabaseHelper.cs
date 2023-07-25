using System.Data;
using Dapper;
using Dapper.Contrib.Extensions;
using MySqlConnector;

namespace Movies.Api.Tests.Integration.Helpers;

public class DatabaseHelper<TId, TRecord>
    where TRecord : class
    where TId : notnull
{
    protected readonly string connectionString;
	private readonly string tableName;
	private readonly string idColumnName;
    protected readonly Func<TRecord, TId> idSelector;

    public DatabaseHelper(
        string connectionString,
        string tableName,
        Func<TRecord, TId> idSelector,
        string idColumnName = "Id")
	{
        this.connectionString = connectionString;
		this.tableName = tableName;
		this.idColumnName = idColumnName;
		this.idSelector = idSelector;
	}

    public Dictionary<TId, TRecord> AddedRecords { get; } = new Dictionary<TId, TRecord>();

    public virtual async Task<TRecord> GetRecordAsync(TId id)
    {
        await using var connection = new MySqlConnection(connectionString);
        return await connection.QueryFirstOrDefaultAsync<TRecord>(
            $"SELECT * FROM {tableName} WHERE {idColumnName} = @Id",
            new { Id = id },
            commandType: CommandType.Text);
    }

    public virtual async Task AddRecordAsync(TRecord record)
    {
        this.AddedRecords.Add(idSelector(record), record);
        await using var connection = new MySqlConnection(connectionString);
        await connection.InsertAsync<TRecord>(record);
    }

    public async Task AddRecordsAsync(IEnumerable<TRecord> records)
    {
        foreach (var record in records)
        {
            await AddRecordAsync(record);
        }
    }

    public void TrackId(TId id) => AddedRecords.Add(id, default!);

    public virtual async Task DeleteRecordAsync(TId id)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.ExecuteAsync(
            $"DELETE FROM {tableName} WHERE {idColumnName} = @Id",
            new { Id = id },
            commandType: CommandType.Text);
    }

    public async Task CleanTableAsync()
    {
        foreach (var addedRecord in AddedRecords)
        {
            await DeleteRecordAsync(addedRecord.Key);
        }
    }
}
