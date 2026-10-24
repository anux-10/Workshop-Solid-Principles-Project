using OrderManagement.V1.Bad.Domain;

namespace OrderManagement.V1.Bad.Services;


public class SalesReportService
{
    private readonly IOrderOperations _operations;

    public SalesReportService(IOrderOperations operations) => _operations = operations;

    public object Build()
    {
        var orders = _operations.GetAllOrders();
        var billable = orders.Where(o => o.Status != "Cancelled").ToList();

        return new
        {
            orderCount = orders.Count,
            grossRevenue = Math.Round(billable.Sum(o => o.Total), 2),
            totalDiscount = Math.Round(orders.Sum(o => o.Discount), 2),
            averageOrderValue = billable.Count == 0 ? 0 : Math.Round(billable.Average(o => o.Total), 2),
            byStatus = orders.GroupBy(o => o.Status).ToDictionary(g => g.Key, g => g.Count())
        };
    }
}
