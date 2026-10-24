using Microsoft.Extensions.Logging.Abstractions;
using OrderManagement.V2.Good.Abstractions;
using OrderManagement.V2.Good.Discounts;
using OrderManagement.V2.Good.Domain;
using OrderManagement.V2.Good.Payments;
using OrderManagement.V2.Good.Services;
using OrderManagement.V2.Good.Validation;

namespace OrderManagement.V2.Good.Tests;

public sealed class SalesReportTests
{
    [Fact]
    public async Task BuildAsync_CountsOrdersAndIgnoresCancelledRevenue()
    {
        var repository = new InMemoryOrderRepository();
        var service = new OrderService(
            repository,
            new OrderValidator(new IOrderValidationRule[]
            {
                new CustomerDetailsRule(),
                new ItemsRule(),
                new MinimumOrderValueRule()
            }),
            new DiscountStrategyProvider(new IDiscountStrategy[]
            {
                new StandardDiscount(),
                new PremiumDiscount(),
                new VipDiscount()
            }),
            new PaymentProcessorProvider(new IPaymentProcessor[]
            {
                new CardPaymentProcessor(),
                new CashOnDeliveryProcessor()
            }),
            new RecordingNotifier(),
            TimeProvider.System,
            NullLogger<OrderService>.Instance);

        var kept = await service.CreateAsync(
            new Customer("Ada Lovelace", "ada@example.com", CustomerTier.Premium),
            [new OrderItem("KB-01", "Keyboard", 120m, 2)]);

        var cancelled = await service.CreateAsync(
            new Customer("Grace Hopper", "grace@example.com", CustomerTier.Standard),
            [new OrderItem("CH-01", "Cable", 100m, 1)]);

        await service.CancelAsync(cancelled.Id);

        // SalesReportService only needs IOrderRepository - four members, not ten.
        var report = await new SalesReportService(repository).BuildAsync();

        Assert.Equal(2, report.OrderCount);
        Assert.Equal(kept.Total, report.GrossRevenue);
        Assert.Equal(kept.Discount, report.TotalDiscount);
        Assert.Equal(1, report.ByStatus["Confirmed"]);
        Assert.Equal(1, report.ByStatus["Cancelled"]);
    }
}
