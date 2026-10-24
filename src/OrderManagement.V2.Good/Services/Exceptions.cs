namespace OrderManagement.V2.Good.Services;

public sealed class OrderValidationException : Exception
{
    public OrderValidationException(IEnumerable<string> errors)
        : base(string.Join(" ", errors))
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyList<string> Errors { get; }
}

public sealed class OrderNotFoundException : Exception
{
    public OrderNotFoundException(Guid id) : base($"Order {id} was not found.")
    {
    }
}

public sealed class UnknownPaymentMethodException : Exception
{
    public UnknownPaymentMethodException(string method)
        : base($"Unknown payment method '{method}'. Supported methods: card, cod.")
    {
    }
}
