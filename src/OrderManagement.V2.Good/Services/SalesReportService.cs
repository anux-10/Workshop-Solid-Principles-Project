using OrderManagement.V2.Good.Abstractions;
using OrderManagement.V2.Good.Domain;

namespace OrderManagement.V2.Good.Services;

/// <summary>
/// The ISP fix: this service needs to read orders and nothing else, so it depends on
/// <see cref="IOrderRepository"/> - four cohesive members - instead of V1's ten-member
/// IOrderOperations surface. Testing it needs one fake method, not ten.
/// </summary>
public sealed class SalesReportService
{
    private readonly IOrderRepository _orders;

    public SalesReportService(IOrderRepository orders) => _orders = orders;

    public async Task<SalesReport> BuildAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _orders.GetAllAsync(cancellationToken);
        var billable = orders.Where(order => order.Status == OrderStatus.Confirmed).ToList();
        var revenue = billable.Sum(order => order.Total);

        return new SalesReport(
            OrderCount: orders.Count,
            GrossRevenue: Round(revenue),
            TotalDiscount: Round(orders.Sum(order => order.Discount)),
            AverageOrderValue: billable.Count == 0 ? 0m : Round(revenue / billable.Count),
            ByStatus: orders
                .GroupBy(order => order.Status.ToString())
                .ToDictionary(group => group.Key, group => group.Count()));
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed record SalesReport(
    int OrderCount,
    decimal GrossRevenue,
    decimal TotalDiscount,
    decimal AverageOrderValue,
    IReadOnlyDictionary<string, int> ByStatus);
