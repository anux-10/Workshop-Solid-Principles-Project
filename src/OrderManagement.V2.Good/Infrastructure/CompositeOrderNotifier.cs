using Microsoft.Extensions.Logging;
using OrderManagement.V2.Good.Abstractions;
using OrderManagement.V2.Good.Domain;

namespace OrderManagement.V2.Good.Infrastructure;

/// <summary>
/// A second notification channel that required ZERO changes to OrderService, the API,
/// or the e-mail notifier. In V1 this would have meant editing a switch statement
/// (and the ten-member interface, and every fake).
/// </summary>
public sealed class AuditLogNotifier : IOrderNotifier
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly string _path = Path.Combine(Directory.GetCurrentDirectory(), "notifications.log");
    private readonly ILogger<AuditLogNotifier> _logger;

    public AuditLogNotifier(ILogger<AuditLogNotifier> logger) => _logger = logger;

    public Task OrderConfirmedAsync(Order order, CancellationToken cancellationToken = default) =>
        AppendAsync($"CONFIRMED {order.Id} {order.Customer.Email} {order.Total:F2}", cancellationToken);

    public Task OrderCancelledAsync(Order order, CancellationToken cancellationToken = default) =>
        AppendAsync($"CANCELLED {order.Id} {order.Customer.Email}", cancellationToken);

    public Task RefundIssuedAsync(Order order, CancellationToken cancellationToken = default) =>
        AppendAsync($"REFUNDED {order.Id} {order.Customer.Email} {order.Total:F2}", cancellationToken);

    private async Task AppendAsync(string line, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var entry = $"{DateTimeOffset.UtcNow:O} {line}{Environment.NewLine}";
            await File.AppendAllTextAsync(_path, entry, cancellationToken);
        }
        catch (IOException exception)
        {
            // An audit failure must be visible, not swallowed like in V1.
            _logger.LogWarning(exception, "Could not append to the audit log at {Path}.", _path);
        }
        finally
        {
            Gate.Release();
        }
    }
}

/// <summary>
/// Fan-out. Both notifiers are registered as themselves, and this composite is bound to
/// <see cref="IOrderNotifier"/> in Program.cs - the service still sees one dependency.
/// </summary>
public sealed class CompositeOrderNotifier : IOrderNotifier
{
    private readonly IReadOnlyList<IOrderNotifier> _notifiers;

    public CompositeOrderNotifier(params IOrderNotifier[] notifiers) => _notifiers = notifiers;

    public Task OrderConfirmedAsync(Order order, CancellationToken cancellationToken = default) =>
        FanOutAsync(notifier => notifier.OrderConfirmedAsync(order, cancellationToken));

    public Task OrderCancelledAsync(Order order, CancellationToken cancellationToken = default) =>
        FanOutAsync(notifier => notifier.OrderCancelledAsync(order, cancellationToken));

    public Task RefundIssuedAsync(Order order, CancellationToken cancellationToken = default) =>
        FanOutAsync(notifier => notifier.RefundIssuedAsync(order, cancellationToken));

    private Task FanOutAsync(Func<IOrderNotifier, Task> action) =>
        Task.WhenAll(_notifiers.Select(action));
}
