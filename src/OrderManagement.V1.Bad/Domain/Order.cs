namespace OrderManagement.V1.Bad.Domain;

/// <summary>
/// VERSION 1 - INTENTIONALLY BAD DESIGN (teaching material).
///
/// Liskov Substitution Principle (LSP) violation:
/// The base type promises "every Order accepts items and calculates its own total".
/// <see cref="RefundOrder"/> changes that promise (throws / returns a negative total),
/// so any code written against <see cref="Order"/> breaks the moment it receives a refund.
/// </summary>
public class Order
{
    // Everything is a public setter: the object is an open bag of data with no invariants.
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;

    // Magic string instead of an enum: "premium", "Premium", "PREMIUM" are three different things.
    public string Tier { get; set; } = "standard";

    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<OrderItem> Items { get; set; } = new();

    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }

    public virtual void AddItem(OrderItem item) => Items.Add(item);

    public virtual decimal CalculateTotal() => Items.Sum(i => i.UnitPrice * i.Quantity);
}

/// <summary>
/// LSP violation in its most classic form: a subtype that cannot honour the base contract.
/// The compiler is happy, the business rules are not.
/// </summary>
public class RefundOrder : Order
{
    public Guid OriginalOrderId { get; set; }

    public override void AddItem(OrderItem item) =>
        throw new NotSupportedException("Refund orders cannot contain items.");

    public override decimal CalculateTotal() => -base.CalculateTotal();
}

public class OrderItem
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    // No line total on purpose: prices are recalculated in five different places instead.
}
