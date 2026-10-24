using System.Text.Json.Serialization;

namespace OrderManagement.V2.Good.Domain;

/// <summary>
/// Rich aggregate root. State can only change through methods that enforce the rules,
/// so an invalid order cannot exist - even if a future controller forgets to validate.
/// Compare with V1 where every property had a public setter.
/// </summary>
public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    public Guid Id { get; private init; }
    public Customer Customer { get; private init; } = null!;
    public IReadOnlyList<OrderItem> Items => _items;
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }

    public decimal Subtotal { get; private set; }
    public decimal Discount { get; private set; }
    public decimal Tax { get; private set; }
    public decimal Total { get; private set; }

    public decimal ItemsTotal => decimal.Round(
        _items.Sum(item => item.LineTotal),
        2,
        MidpointRounding.AwayFromZero);

    public static Order Create(Customer customer, IEnumerable<OrderItem> items, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(items);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            Customer = customer,
            CreatedAt = createdAt,
            Status = OrderStatus.Confirmed
        };

        order._items.AddRange(items);
        return order;
    }

    /// <summary>Rehydrates an order from the database. Only the repository uses this.</summary>
    internal static Order Restore(
        Guid id,
        Customer customer,
        IEnumerable<OrderItem> items,
        OrderStatus status,
        DateTimeOffset createdAt,
        decimal subtotal,
        decimal discount,
        decimal tax,
        decimal total)
    {
        var order = new Order
        {
            Id = id,
            Customer = customer,
            Status = status,
            CreatedAt = createdAt,
            Subtotal = subtotal,
            Discount = discount,
            Tax = tax,
            Total = total
        };

        order._items.AddRange(items);
        return order;
    }

    public void ApplyPricing(decimal discount, decimal tax)
    {
        if (discount < 0)
            throw new DomainException("Discount cannot be negative.");
        if (tax < 0)
            throw new DomainException("Tax cannot be negative.");

        Subtotal = ItemsTotal;
        Discount = decimal.Round(discount, 2, MidpointRounding.AwayFromZero);
        Tax = decimal.Round(tax, 2, MidpointRounding.AwayFromZero);
        Total = decimal.Round(Subtotal - Discount + Tax, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The state machine lives in the domain, so every caller gets the same rules.
    /// V1 simply assigned <c>Status = "Cancelled"</c> from the service.
    /// </summary>
    public void Cancel()
    {
        if (Status != OrderStatus.Confirmed)
            throw new DomainException($"Only a confirmed order can be cancelled (current status: {Status}).");

        Status = OrderStatus.Cancelled;
    }

    public void Refund()
    {
        if (Status != OrderStatus.Confirmed)
            throw new DomainException($"Only a confirmed order can be refunded (current status: {Status}).");

        Status = OrderStatus.Refunded;
    }

    public void EnsurePayable()
    {
        if (Status != OrderStatus.Confirmed)
            throw new DomainException($"Only a confirmed order can be paid (current status: {Status}).");
    }
}

public sealed record OrderItem(string Sku, string Name, decimal UnitPrice, int Quantity)
{
    /// <summary>Derived, never stored twice, never recalculated in five places.</summary>
    [JsonIgnore]
    public decimal LineTotal => decimal.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);
}

public sealed record Customer(string Name, string Email, CustomerTier Tier);

public enum CustomerTier
{
    Standard,
    Premium,
    Vip
}

public enum OrderStatus
{
    Confirmed,
    Cancelled,
    Refunded
}

public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
