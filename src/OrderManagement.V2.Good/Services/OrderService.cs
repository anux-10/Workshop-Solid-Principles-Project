using Microsoft.Extensions.Logging;
using OrderManagement.V2.Good.Abstractions;
using OrderManagement.V2.Good.Domain;

namespace OrderManagement.V2.Good.Services;

/// <summary>
/// The application service: orchestration only.
///
/// It does not know SQL, SMTP, file paths, or concrete customer tiers. All of that arrives
/// through the constructor as abstractions, which is why this class can be unit tested with
/// in-memory fakes in a few milliseconds (see tests/OrderManagement.V2.Good.Tests).
///
/// V1's OrderManager was ~300 lines of everything; this is the same behaviour split into
/// collaborators that each have one reason to exist.
/// </summary>
public sealed class OrderService
{
    private const decimal TaxRate = 0.08m;

    private readonly IOrderRepository _orders;
    private readonly IOrderValidator _validator;
    private readonly IDiscountStrategyProvider _discounts;
    private readonly IPaymentProcessorProvider _payments;
    private readonly IOrderNotifier _notifier;
    private readonly TimeProvider _clock;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orders,
        IOrderValidator validator,
        IDiscountStrategyProvider discounts,
        IPaymentProcessorProvider payments,
        IOrderNotifier notifier,
        TimeProvider clock,
        ILogger<OrderService> logger)
    {
        _orders = orders;
        _validator = validator;
        _discounts = discounts;
        _payments = payments;
        _notifier = notifier;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Order> CreateAsync(
        Customer customer,
        IReadOnlyList<OrderItem> items,
        CancellationToken cancellationToken = default)
    {
        // Time comes from an abstraction, so tests are deterministic and the API stores UTC.
        var order = Order.Create(customer, items, _clock.GetUtcNow());

        var validation = await _validator.ValidateAsync(order, cancellationToken);
        if (!validation.IsValid)
            throw new OrderValidationException(validation.Errors);

        var strategy = _discounts.For(order.Customer.Tier);
        var discount = strategy.CalculateDiscount(order);
        var tax = decimal.Round((order.ItemsTotal - discount) * TaxRate, 2, MidpointRounding.AwayFromZero);

        order.ApplyPricing(discount, tax);

        await _orders.AddAsync(order, cancellationToken);
        await _notifier.OrderConfirmedAsync(order, cancellationToken);

        _logger.LogInformation(
            "Order {OrderId} created for {Tier} customer with total {Total}.",
            order.Id, order.Customer.Tier, order.Total);

        return order;
    }

    public async Task<Order> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await GetRequiredAsync(id, cancellationToken);

        order.Cancel();   // the aggregate enforces the state machine
        await _orders.UpdateAsync(order, cancellationToken);
        await _notifier.OrderCancelledAsync(order, cancellationToken);

        _logger.LogInformation("Order {OrderId} cancelled.", order.Id);
        return order;
    }

    public async Task<Order> RefundAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await GetRequiredAsync(id, cancellationToken);

        order.Refund();
        await _orders.UpdateAsync(order, cancellationToken);
        await _notifier.RefundIssuedAsync(order, cancellationToken);

        _logger.LogInformation("Order {OrderId} refunded.", order.Id);
        return order;
    }

    public async Task<PaymentReceipt> PayAsync(
        string method,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // Fail fast before touching a payment provider.
        var processor = _payments.For(method);

        var order = await GetRequiredAsync(id, cancellationToken);
        order.EnsurePayable();

        var receipt = await processor.ProcessAsync(order, cancellationToken);

        _logger.LogInformation(
            "Payment {Reference} ({Settlement}) for order {OrderId}.",
            receipt.Reference, receipt.Settlement, order.Id);

        return receipt;
    }

    public Task<Order?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _orders.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken = default) =>
        _orders.GetAllAsync(cancellationToken);

    private async Task<Order> GetRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await _orders.GetByIdAsync(id, cancellationToken)
        ?? throw new OrderNotFoundException(id);
}
