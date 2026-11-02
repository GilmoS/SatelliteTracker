using Microsoft.Extensions.Options;
using Npgsql;
using SatelliteTracker.API.Configuration;

namespace SatelliteTracker.API.HealthChecks;

// Deliberately bypasses EF Core, and with it DatabaseRetryPolicy: a raw NpgsqlConnection from the
// same connection string, with its own command timeout. The retry strategy keeps replaying a failed
// call for about 25 s, which is right for the app and wrong here. A health check answers "right
// now", never "eventually", so this is a deliberate exception to the I2.3 rule that database access
// goes through the execution strategy (and to the repository pattern). Don't copy it elsewhere.
public sealed class NpgsqlHealthDatabaseProbe : IHealthDatabaseProbe
{
    // Public so a test can run them against the EF schema: nothing else ties these table and column
    // names to the model.
    public const string PingSql = "SELECT 1";

    public const string LatestTleFetchPerActiveSatelliteSql = """
        SELECT s."Name", MAX(t."FetchedAt")
        FROM "Satellites" s
        LEFT JOIN "TleRecords" t ON t."SatelliteId" = s."Id"
        WHERE s."IsActive"
        GROUP BY s."Id", s."Name"
        """;

    public const string LatestPassCalculationSql = """SELECT MAX("CalculatedAt") FROM "Passes" """;

    private static readonly int CommandTimeoutSeconds = (int)HealthCheckPolicy.CheckTimeout.TotalSeconds;

    private readonly string _connectionString;

    public NpgsqlHealthDatabaseProbe(IOptions<DatabaseOptions> options)
    {
        _connectionString = options.Value.DefaultConnection!;
    }

    public async Task PingAsync(CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, PingSql);
        await command.ExecuteScalarAsync(ct);
    }

    public async Task<IReadOnlyList<SatelliteTleFetch>> GetLatestTleFetchPerActiveSatelliteAsync(CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, LatestTleFetchPerActiveSatelliteSql);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var fetches = new List<SatelliteTleFetch>();
        while (await reader.ReadAsync(ct))
        {
            var latestFetchedAt = reader.IsDBNull(1) ? (DateTime?)null : reader.GetFieldValue<DateTime>(1);
            fetches.Add(new SatelliteTleFetch(reader.GetString(0), latestFetchedAt));
        }

        return fetches;
    }

    public async Task<DateTime?> GetLatestPassCalculationAsync(CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, LatestPassCalculationSql);
        return await command.ExecuteScalarAsync(ct) as DateTime?;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sql) =>
        new(sql, connection) { CommandTimeout = CommandTimeoutSeconds };
}
