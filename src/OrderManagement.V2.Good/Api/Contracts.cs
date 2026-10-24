using OrderManagement.V2.Good.Domain;
using OrderManagement.V2.Good.Services;

namespace OrderManagement.V2.Good.Api;

// The wire contract is identical to V1, so the same UI drives both applications.
// The difference is that V2 never exposes its domain model directly.

public sealed record CustomerRequest(string? Name, string? Email, string? Tier);

public sealed record OrderItemRequest(string? Sku, string? Name, decimal UnitPrice, int Quantity);

public sealed record CreateOrderRequest(CustomerRequest? Customer, List<OrderItemRequest>? Items);

public sealed record PayRequest(string? Method);

public sealed record OrderItemResponse(
    string Sku,
    string Name,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record OrderResponse(
    Guid Id,
    string Status,
    string CustomerName,
    string CustomerEmail,
    string Tier,
    IReadOnlyList<OrderItemResponse> Items,
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal Total,
    DateTimeOffset CreatedAt)
{
    public static OrderResponse From(Order order) => new(
        order.Id,
        order.Status.ToString(),
        order.Customer.Name,
        order.Customer.Email,
        order.Customer.Tier.ToString(),
        order.Items
            .Select(item => new OrderItemResponse(item.Sku, item.Name, item.UnitPrice, item.Quantity, item.LineTotal))
            .ToArray(),
        order.Subtotal,
        order.Discount,
        order.Tax,
        order.Total,
        order.CreatedAt);
}

public sealed record PaymentResponse(string Reference, decimal Amount, string Status);

public sealed record SalesReportResponse(
    int OrderCount,
    decimal GrossRevenue,
    decimal TotalDiscount,
    decimal AverageOrderValue,
    IReadOnlyDictionary<string, int> ByStatus)
{
    public static SalesReportResponse From(SalesReport report) => new(
        report.OrderCount,
        report.GrossRevenue,
        report.TotalDiscount,
        report.AverageOrderValue,
        report.ByStatus);
}
