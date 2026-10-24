using OrderManagement.V2.Good.Abstractions;
using OrderManagement.V2.Good.Domain;
using OrderManagement.V2.Good.Services;

namespace OrderManagement.V2.Good.Payments;

public sealed class CardPaymentProcessor : IPaymentProcessor
{
    public string Method => "card";

    public Task<PaymentReceipt> ProcessAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var reference = "CARD-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        return Task.FromResult(new PaymentReceipt(reference, order.Total, PaymentSettlement.Captured));
    }
}

/// <summary>
/// V1 made this throw from a method whose contract promised to charge money.
/// Here the contract already allows "settled at delivery", so this class is a perfectly
/// legal substitute for any other IPaymentProcessor - LSP satisfied.
/// </summary>
public sealed class CashOnDeliveryProcessor : IPaymentProcessor
{
    public string Method => "cod";

    public Task<PaymentReceipt> ProcessAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var reference = "COD-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        return Task.FromResult(new PaymentReceipt(reference, order.Total, PaymentSettlement.DueOnDelivery));
    }
}

/// <summary>
/// Resolves a method name to its processor. New method = new implementation + one DI line.
/// </summary>
public sealed class PaymentProcessorProvider : IPaymentProcessorProvider
{
    private readonly IReadOnlyDictionary<string, IPaymentProcessor> _processors;

    public PaymentProcessorProvider(IEnumerable<IPaymentProcessor> processors) =>
        _processors = processors.ToDictionary(processor => processor.Method, StringComparer.OrdinalIgnoreCase);

    public IPaymentProcessor For(string method) =>
        _processors.TryGetValue(method, out var processor)
            ? processor
            : throw new UnknownPaymentMethodException(method);
}
