using OrderManagement.V2.Good.Abstractions;
using OrderManagement.V2.Good.Domain;

namespace OrderManagement.V2.Good.Tests;

/// <summary>
/// Four methods. No SQLite, no global state - compare with V1 where the same test would
/// need a real file database and a real SMTP server.
/// </summary>
internal sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _orders = new();

    public IReadOnlyCollection<Order> Orders => _orders.Values;

    public Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        _orders[order.Id] = order;
        return Task.CompletedTask;
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_orders.GetValueOrDefault(id));

    public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Order>>(_orders.Values.ToList());

    public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _orders[order.Id] = order;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingNotifier : IOrderNotifier
{
    public List<string> Events { get; } = new();

    public Task OrderConfirmedAsync(Order order, CancellationToken cancellationToken = default)
    {
        Events.Add($"confirmed:{order.Id}");
        return Task.CompletedTask;
    }

    public Task OrderCancelledAsync(Order order, CancellationToken cancellationToken = default)
    {
        Events.Add($"cancelled:{order.Id}");
        return Task.CompletedTask;
    }

    public Task RefundIssuedAsync(Order order, CancellationToken cancellationToken = default)
    {
        Events.Add($"refunded:{order.Id}");
        return Task.CompletedTask;
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
