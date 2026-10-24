using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using OrderManagement.V2.Good.Abstractions;
using OrderManagement.V2.Good.Domain;

namespace OrderManagement.V2.Good.Infrastructure;

public sealed class DatabaseOptions
{
    public string ConnectionString { get; set; } = "Data Source=orders-v2.db";
}

public sealed class NotificationOptions
{
    public bool UseSmtp { get; set; }
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 25;
    public bool UseSsl { get; set; }
    public string FromAddress { get; set; } = "orders@shop.local";
}

/// <summary>
/// Schema creation lives outside the request path and runs once at startup.
/// V1 ran the same DDL from a constructor on every request.
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(IOptions<DatabaseOptions> options) =>
        _connectionString = options.Value.ConnectionString;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Orders (
                Id           TEXT    NOT NULL PRIMARY KEY,
                CustomerName TEXT    NOT NULL,
                CustomerEmail TEXT   NOT NULL,
                Tier         TEXT    NOT NULL,
                Status       TEXT    NOT NULL,
                CreatedAt    TEXT    NOT NULL,
                Subtotal     TEXT    NOT NULL,
                Discount     TEXT    NOT NULL,
                Tax          TEXT    NOT NULL,
                Total        TEXT    NOT NULL,
                Items        TEXT    NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

/// <summary>
/// DIP: this is the only class in the application that knows the database exists.
/// Everything above it depends on <see cref="IOrderRepository"/>.
///
/// Parameterised SQL, async I/O, no global state, no cache.
/// </summary>
public sealed class SqlOrderRepository : IOrderRepository
{
    private const string SelectColumns =
        "Id, CustomerName, CustomerEmail, Tier, Status, CreatedAt, Subtotal, Discount, Tax, Total, Items";

    private readonly string _connectionString;

    public SqlOrderRepository(IOptions<DatabaseOptions> options) =>
        _connectionString = options.Value.ConnectionString;

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Orders (Id, CustomerName, CustomerEmail, Tier, Status, CreatedAt,
                                Subtotal, Discount, Tax, Total, Items)
            VALUES ($id, $name, $email, $tier, $status, $createdAt,
                    $subtotal, $discount, $tax, $total, $items);
            """;

        AddParameters(command, order);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Orders
            SET Status = $status, Subtotal = $subtotal, Discount = $discount,
                Tax = $tax, Total = $total
            WHERE Id = $id;
            """;

        AddParameters(command, order);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM Orders WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var orders = new List<Order>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM Orders ORDER BY CreatedAt DESC;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            orders.Add(Map(reader));

        return orders;
    }

    private static void AddParameters(SqliteCommand command, Order order)
    {
        // Parameters, not concatenation: a customer called O'Brien is just a customer.
        command.Parameters.AddWithValue("$id", order.Id.ToString());
        command.Parameters.AddWithValue("$name", order.Customer.Name);
        command.Parameters.AddWithValue("$email", order.Customer.Email);
        command.Parameters.AddWithValue("$tier", order.Customer.Tier.ToString());
        command.Parameters.AddWithValue("$status", order.Status.ToString());
        command.Parameters.AddWithValue("$createdAt", order.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$subtotal", order.Subtotal);
        command.Parameters.AddWithValue("$discount", order.Discount);
        command.Parameters.AddWithValue("$tax", order.Tax);
        command.Parameters.AddWithValue("$total", order.Total);
        command.Parameters.AddWithValue("$items", JsonSerializer.Serialize(order.Items));
    }

    private static Order Map(SqliteDataReader reader)
    {
        var customer = new Customer(
            reader.GetString(1),
            reader.GetString(2),
            Enum.Parse<CustomerTier>(reader.GetString(3)));

        var items = JsonSerializer.Deserialize<List<OrderItem>>(reader.GetString(10)) ?? [];
        var status = Enum.Parse<OrderStatus>(reader.GetString(4));
        var createdAt = DateTimeOffset.Parse(
            reader.GetString(5),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

        return Order.Restore(
            Guid.Parse(reader.GetString(0)),
            customer,
            items,
            status,
            createdAt,
            reader.GetDecimal(6),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader.GetDecimal(9));
    }
}
