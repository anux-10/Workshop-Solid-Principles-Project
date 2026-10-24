using OrderManagement.V2.Good.Abstractions;
using OrderManagement.V2.Good.Domain;

namespace OrderManagement.V2.Good.Discounts;

// ---------------------------------------------------------------------------
// OCP in practice: each policy is its own class, each has exactly one reason to
// change. Adding a "Gold" tier means adding GoldDiscount and one DI line; nothing
// in OrderService or the API changes.
// ---------------------------------------------------------------------------

public sealed class StandardDiscount : IDiscountStrategy
{
    public bool AppliesTo(CustomerTier tier) => tier == CustomerTier.Standard;

    public decimal CalculateDiscount(Order order) => 0m;
}

public sealed class PremiumDiscount : IDiscountStrategy
{
    private const decimal Rate = 0.10m;

    public bool AppliesTo(CustomerTier tier) => tier == CustomerTier.Premium;

    public decimal CalculateDiscount(Order order) =>
        decimal.Round(order.ItemsTotal * Rate, 2, MidpointRounding.AwayFromZero);
}

public sealed class VipDiscount : IDiscountStrategy
{
    private const decimal Rate = 0.20m;

    public bool AppliesTo(CustomerTier tier) => tier == CustomerTier.Vip;

    public decimal CalculateDiscount(Order order) =>
        decimal.Round(order.ItemsTotal * Rate, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// The only place that knows which policy serves which tier.
/// Unknown tiers are a configuration error, and they fail loudly - V1 silently charged
/// the customer full price.
/// </summary>
public sealed class DiscountStrategyProvider : IDiscountStrategyProvider
{
    private readonly IReadOnlyList<IDiscountStrategy> _strategies;

    public DiscountStrategyProvider(IEnumerable<IDiscountStrategy> strategies) =>
        _strategies = strategies.ToList();

    public IDiscountStrategy For(CustomerTier tier) =>
        _strategies.FirstOrDefault(strategy => strategy.AppliesTo(tier))
        ?? throw new InvalidOperationException($"No discount strategy is registered for tier '{tier}'.");
}
