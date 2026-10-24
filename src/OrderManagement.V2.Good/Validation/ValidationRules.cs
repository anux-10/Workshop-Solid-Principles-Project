using System.Net.Mail;
using OrderManagement.V2.Good.Domain;

namespace OrderManagement.V2.Good.Validation;

/// <summary>
/// One rule = one small object with one reason to change (SRP).
/// A new rule is a new class plus one DI line; <see cref="OrderValidator"/> stays untouched (OCP).
/// </summary>
public interface IOrderValidationRule
{
    IEnumerable<string> Validate(Order order);
}

public sealed class CustomerDetailsRule : IOrderValidationRule
{
    public IEnumerable<string> Validate(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.Customer.Name) || order.Customer.Name.Trim().Length < 2)
            yield return "Customer name must be at least 2 characters.";

        if (string.IsNullOrWhiteSpace(order.Customer.Email) || !MailAddress.TryCreate(order.Customer.Email, out _))
            yield return "Customer e-mail is not valid.";
    }
}

public sealed class ItemsRule : IOrderValidationRule
{
    public IEnumerable<string> Validate(Order order)
    {
        if (order.Items.Count == 0)
        {
            yield return "An order needs at least one item.";
            yield break;
        }

        foreach (var item in order.Items)
        {
            var label = string.IsNullOrWhiteSpace(item.Sku) ? "(no SKU)" : item.Sku;

            if (string.IsNullOrWhiteSpace(item.Sku))
                yield return "Every item needs a SKU.";

            if (string.IsNullOrWhiteSpace(item.Name))
                yield return $"Item {label} needs a name.";

            if (item.UnitPrice <= 0)
                yield return $"Item {label} must have a price greater than zero.";

            if (item.Quantity is < 1 or > 100)
                yield return $"Item {label} quantity must be between 1 and 100.";
        }
    }
}

public sealed class MinimumOrderValueRule : IOrderValidationRule
{
    private const decimal Minimum = 10m;

    public IEnumerable<string> Validate(Order order)
    {
        if (order.ItemsTotal < Minimum)
            yield return $"Minimum order value is {Minimum:F2}.";
    }
}
