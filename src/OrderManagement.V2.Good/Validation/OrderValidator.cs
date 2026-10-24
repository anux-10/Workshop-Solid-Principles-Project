using OrderManagement.V2.Good.Abstractions;
using OrderManagement.V2.Good.Domain;

namespace OrderManagement.V2.Good.Validation;

/// <summary>
/// Composes the rule list. It has no knowledge of individual business rules, so it never
/// needs to change when the rules do.
/// </summary>
public sealed class OrderValidator : IOrderValidator
{
    private readonly IReadOnlyList<IOrderValidationRule> _rules;

    public OrderValidator(IEnumerable<IOrderValidationRule> rules) => _rules = rules.ToList();

    public Task<OrderValidationResult> ValidateAsync(Order order, CancellationToken cancellationToken = default)
    {
        var errors = _rules.SelectMany(rule => rule.Validate(order)).ToArray();

        var result = errors.Length == 0
            ? OrderValidationResult.Success
            : new OrderValidationResult(errors);

        return Task.FromResult(result);
    }
}
