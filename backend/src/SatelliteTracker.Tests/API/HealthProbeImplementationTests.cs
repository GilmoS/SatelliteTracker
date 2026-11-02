using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SatelliteTracker.API.Configuration;
using SatelliteTracker.API.HealthChecks;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Tests.API.Infrastructure;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests.API;

// What the endpoint tests fake away: the real FCM probe (against a local token endpoint, never
// Google) and the raw SQL of the Postgres probe (run on SQLite, since the suite has no Postgres).
public class HealthProbeImplementationTests
{
    [Fact]
    public async Task FirebaseProbe_RequestsARealTokenFromTheTokenEndpoint()
    {
        using var endpoint = new LocalTokenEndpoint(HttpStatusCode.OK, """{"access_token":"t","expires_in":3600,"token_type":"Bearer"}""");
        var probe = CreateFirebaseProbe(endpoint.Url);

        await probe.RequestTokenAsync(CancellationToken.None);

        // A credential that signs its own JWT locally would never reach the endpoint.
        Assert.Equal(1, endpoint.RequestCount);
        Assert.Contains("firebase.messaging", endpoint.LastRequestBody);
    }

    [Fact]
    public async Task FirebaseProbe_KeepsTheTokenBetweenChecks()
    {
        using var endpoint = new LocalTokenEndpoint(HttpStatusCode.OK, """{"access_token":"t","expires_in":3600,"token_type":"Bearer"}""");
        var probe = CreateFirebaseProbe(endpoint.Url);

        await probe.RequestTokenAsync(CancellationToken.None);
        await probe.RequestTokenAsync(CancellationToken.None);

        Assert.Equal(1, endpoint.RequestCount);
    }

    [Fact]
    public async Task FirebaseProbe_TokenEndpointRejects_Throws()
    {
        using var endpoint = new LocalTokenEndpoint(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
        var probe = CreateFirebaseProbe(endpoint.Url);

        await Assert.ThrowsAnyAsync<Exception>(() => probe.RequestTokenAsync(CancellationToken.None));
    }

    private static FirebaseFcmTokenProbe CreateFirebaseProbe(string tokenUri)
    {
        var account = JsonSerializer.Deserialize<Dictionary<string, string>>(TestServiceAccount.CreateJson())!;
        account["token_uri"] = tokenUri;

        return new FirebaseFcmTokenProbe(Options.Create(new FirebaseOptions
        {
            ServiceAccountJson = JsonSerializer.Serialize(account)
        }));
    }

    [Fact]
    public async Task PostgresProbeSql_MatchesTheEfSchema()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new TestAppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var active = new Satellite { Id = Guid.NewGuid(), Name = "EROS C3", NoradId = 1, IsActive = true };
        var noTle = new Satellite { Id = Guid.NewGuid(), Name = "RUNNER 1", NoradId = 2, IsActive = true };
        var inactive = new Satellite { Id = Guid.NewGuid(), Name = "OLD", NoradId = 3, IsActive = false };
        var fetchedAt = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);
        var tle = new TleRecord { Id = Guid.NewGuid(), SatelliteId = active.Id, Line1 = "1", Line2 = "2", FetchedAt = fetchedAt };
        context.AddRange(active, noTle, inactive, tle,
            new TleRecord { Id = Guid.NewGuid(), SatelliteId = active.Id, Line1 = "1", Line2 = "2", FetchedAt = fetchedAt.AddHours(-2) },
            new TleRecord { Id = Guid.NewGuid(), SatelliteId = inactive.Id, Line1 = "1", Line2 = "2", FetchedAt = fetchedAt },
            new Pass { Id = Guid.NewGuid(), SatelliteId = active.Id, TleId = tle.Id, OrbitNumber = 1, CalculatedAt = fetchedAt });
        await context.SaveChangesAsync();

        Assert.Equal(1L, await ScalarAsync(connection, NpgsqlHealthDatabaseProbe.PingSql));
        Assert.NotNull(await ScalarAsync(connection, NpgsqlHealthDatabaseProbe.LatestPassCalculationSql));

        await using var command = new SqliteCommand(NpgsqlHealthDatabaseProbe.LatestTleFetchPerActiveSatelliteSql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new Dictionary<string, DateTime?>();
        while (await reader.ReadAsync())
            rows[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetDateTime(1);

        Assert.Equal(2, rows.Count); // the inactive satellite is left out
        Assert.Equal(fetchedAt, rows["EROS C3"]); // the latest of its two TLEs
        Assert.Null(rows["RUNNER 1"]);
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = new SqliteCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }

    // A one-route HTTP server on a free loopback port, standing in for oauth2.googleapis.com.
    private sealed class LocalTokenEndpoint : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly HttpStatusCode _status;
        private readonly string _responseBody;
        private int _requestCount;

        public LocalTokenEndpoint(HttpStatusCode status, string responseBody)
        {
            _status = status;
            _responseBody = responseBody;

            using var portFinder = new TcpListener(IPAddress.Loopback, 0);
            portFinder.Start();
            var port = ((IPEndPoint)portFinder.LocalEndpoint).Port;
            portFinder.Stop();

            Url = $"http://127.0.0.1:{port}/token/";
            _listener.Prefixes.Add(Url);
            _listener.Start();
            _ = ServeAsync();
        }

        public string Url { get; }
        public int RequestCount => Volatile.Read(ref _requestCount);
        public string LastRequestBody { get; private set; } = string.Empty;

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    var context = await _listener.GetContextAsync();

                    using (var reader = new StreamReader(context.Request.InputStream))
                        LastRequestBody = DecodeAssertionScope(await reader.ReadToEndAsync());
                    Interlocked.Increment(ref _requestCount);

                    context.Response.StatusCode = (int)_status;
                    context.Response.ContentType = "application/json";
                    var bytes = Encoding.UTF8.GetBytes(_responseBody);
                    await context.Response.OutputStream.WriteAsync(bytes);
                    context.Response.Close();
                }
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                // Listener stopped: the test is over.
            }
        }

        // The scope travels inside the signed JWT assertion's payload.
        private static string DecodeAssertionScope(string formBody)
        {
            var assertion = formBody.Split('&')
                .Select(pair => pair.Split('=', 2))
                .Where(pair => pair[0] == "assertion")
                .Select(pair => Uri.UnescapeDataString(pair[1]))
                .Single();

            var payload = assertion.Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        }

        public void Dispose() => _listener.Close();
    }
}
