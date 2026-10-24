using OrderManagement.V2.Good.Domain;

namespace OrderManagement.V2.Good.Abstractions;

/// <summary>
/// Narrow and cohesive: four operations around order persistence, nothing else.
/// The high-level <c>OrderService</c> depends on this, never on SqliteConnection.
/// </summary>
public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
}

/// <summary>
/// One job: decide whether an order is acceptable. The rules themselves are pluggable.
/// </summary>
public interface IOrderValidator
{
    Task<OrderValidationResult> ValidateAsync(Order order, CancellationToken cancellationToken = default);
}

public sealed record OrderValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static OrderValidationResult Success { get; } = new(Array.Empty<string>());
}

/// <summary>
/// OCP seam: new pricing policy = new class, registered in Program.cs.
/// OrderService never changes.
/// </summary>
public interface IDiscountStrategy
{
    bool AppliesTo(CustomerTier tier);

    decimal CalculateDiscount(Order order);
}

public interface IDiscountStrategyProvider
{
    IDiscountStrategy For(CustomerTier tier);
}

/// <summary>
/// The high-level module talks to this, not to SmtpClient.
/// Add SMS or a webhook by adding another implementation - no edits here.
/// </summary>
public interface IOrderNotifier
{
    Task OrderConfirmedAsync(Order order, CancellationToken cancellationToken = default);

    Task OrderCancelledAsync(Order order, CancellationToken cancellationToken = default);

    Task RefundIssuedAsync(Order order, CancellationToken cancellationToken = default);
}

/// <summary>
/// Honest contract: any implementation can settle now, later, or not at all.
/// </summary>
public interface IPaymentProcessor
{
    string Method { get; }

    Task<PaymentReceipt> ProcessAsync(Order order, CancellationToken cancellationToken = default);
}

public interface IPaymentProcessorProvider
{
    IPaymentProcessor For(string method);
}
