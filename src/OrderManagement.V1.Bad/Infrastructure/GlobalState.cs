using Microsoft.Data.Sqlite;

namespace OrderManagement.V1.Bad.Infrastructure;

/// <summary>
/// DIP / SRP violation: static, globally reachable infrastructure detail.
///
/// - The connection string is hard-coded, so no environment can override it.
/// - The cache is a mutable static list shared by every request, with no locking.
/// - Nothing can be substituted in a unit test: there is no seam at all.
/// </summary>
public static class GlobalState
{
    public static readonly string ConnectionString =
        "Data Source=" + Path.Combine(Directory.GetCurrentDirectory(), "orders-v1.db");

    /// <summary>Global mutable cache. Thread-unsafe by design.</summary>
    public static readonly List<OrderManagement.V1.Bad.Domain.Order> Cache = new();

    public static bool CacheLoaded;

    /// <summary>
    /// Called from the OrderManager constructor, i.e. on EVERY request.
    /// DDL on the hot path - a classic "works on my machine" production surprise.
    /// </summary>
    public static void EnsureDatabase()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Orders (
                Id             TEXT PRIMARY KEY,
                CustomerName   TEXT NOT NULL,
                CustomerEmail  TEXT NOT NULL,
                Tier           TEXT NOT NULL,
                Status         TEXT NOT NULL,
                CreatedAt      TEXT NOT NULL,
                Subtotal       REAL NOT NULL,
                Discount       REAL NOT NULL,
                Tax            REAL NOT NULL,
                Total          REAL NOT NULL,
                Items          TEXT NOT NULL,
                OriginalOrderId TEXT NULL
            );
            """;
        command.ExecuteNonQuery();
    }
}
