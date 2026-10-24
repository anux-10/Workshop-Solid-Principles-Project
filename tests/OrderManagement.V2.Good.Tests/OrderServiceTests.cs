using Microsoft.Extensions.Logging.Abstractions;
using OrderManagement.V2.Good.Abstractions;
using OrderManagement.V2.Good.Discounts;
using OrderManagement.V2.Good.Domain;
using OrderManagement.V2.Good.Payments;
using OrderManagement.V2.Good.Services;
using OrderManagement.V2.Good.Validation;

namespace OrderManagement.V2.Good.Tests;

public sealed class OrderServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    // -----------------------------------------------------------------------
    // SRP/DIP payoff: the whole graph is built from in-memory fakes here, so the
    // test is deterministic and runs in microseconds.
    // -----------------------------------------------------------------------
    private static OrderService CreateService(
        InMemoryOrderRepository repository,
        RecordingNotifier notifier)
    {
        var validator = new OrderValidator(new IOrderValidationRule[]
        {
            new CustomerDetailsRule(),
            new ItemsRule(),
            new MinimumOrderValueRule()
        });

        var discounts = new DiscountStrategyProvider(new IDiscountStrategy[]
        {
            new StandardDiscount(),
            new PremiumDiscount(),
            new VipDiscount()
        });

        var payments = new PaymentProcessorProvider(new IPaymentProcessor[]
        {
            new CardPaymentProcessor(),
            new CashOnDeliveryProcessor()
        });

        return new OrderService(
            repository,
            validator,
            discounts,
            payments,
            notifier,
            new FixedTimeProvider(Now),
            NullLogger<OrderService>.Instance);
    }

    private static (OrderService Service, InMemoryOrderRepository Repository, RecordingNotifier Notifier) CreateSut()
    {
        var repository = new InMemoryOrderRepository();
        var notifier = new RecordingNotifier();
        return (CreateService(repository, notifier), repository, notifier);
    }

    private static Customer Customer(CustomerTier tier = CustomerTier.Premium) =>
        new("Ada Lovelace", "ada@example.com", tier);

    private static OrderItem[] Keyboard(int quantity = 2) =>
        [new OrderItem("KB-01", "Mechanical keyboard", 120m, quantity)];

    // -----------------------------------------------------------------------
    // Pricing (OCP: strategies are swapped without touching OrderService)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task CreateAsync_PremiumCustomer_AppliesDiscountAndTax()
    {
        var (service, repository, notifier) = CreateSut();

        var order = await service.CreateAsync(Customer(CustomerTier.Premium), Keyboard());

        Assert.Equal(240m, order.Subtotal);
        Assert.Equal(24m, order.Discount);            // 10 %
        Assert.Equal(17.28m, order.Tax);               // 8 % of 216
        Assert.Equal(233.28m, order.Total);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Single(repository.Orders);
        Assert.Contains($"confirmed:{order.Id}", notifier.Events);
    }

    [Fact]
    public async Task CreateAsync_VipCustomer_AppliesTwentyPercent()
    {
        var (service, _, _) = CreateSut();

        var order = await service.CreateAsync(
            Customer(CustomerTier.Vip),
            [new OrderItem("CH-01", "USB-C cable", 100m, 1)]);

        Assert.Equal(20m, order.Discount);
        Assert.Equal(6.40m, order.Tax);
        Assert.Equal(86.40m, order.Total);
    }

    [Fact]
    public async Task CreateAsync_StandardCustomer_PaysFullPrice()
    {
        var (service, _, _) = CreateSut();

        var order = await service.CreateAsync(
            Customer(CustomerTier.Standard),
            [new OrderItem("CH-01", "USB-C cable", 100m, 1)]);

        Assert.Equal(0m, order.Discount);
        Assert.Equal(108m, order.Total);
    }

    [Fact]
    public async Task CreateAsync_UsesInjectedClock()
    {
        var (service, _, _) = CreateSut();

        var order = await service.CreateAsync(Customer(), Keyboard());

        Assert.Equal(Now, order.CreatedAt);
    }

    // -----------------------------------------------------------------------
    // Validation (SRP: rules are separate, testable objects)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task CreateAsync_WithoutItems_ThrowsValidationException()
    {
        var (service, _, _) = CreateSut();

        var exception = await Assert.ThrowsAsync<OrderValidationException>(
            () => service.CreateAsync(Customer(), []));

        Assert.Contains("at least one item", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_WithBadEmailAndZeroQuantity_ReportsEveryBrokenRule()
    {
        var (service, _, _) = CreateSut();
        var customer = new Customer("A", "not-an-email", CustomerTier.Premium);

        var exception = await Assert.ThrowsAsync<OrderValidationException>(
            () => service.CreateAsync(customer, Keyboard(quantity: 0)));

        // name, e-mail, quantity and the minimum-order-value rule all fire.
        Assert.Equal(4, exception.Errors.Count);
        Assert.Contains(exception.Errors, error => error.Contains("name"));
        Assert.Contains(exception.Errors, error => error.Contains("e-mail"));
        Assert.Contains(exception.Errors, error => error.Contains("quantity"));
    }

    [Fact]
    public async Task CreateAsync_BelowMinimumOrderValue_IsRejected()
    {
        var (service, _, _) = CreateSut();

        var exception = await Assert.ThrowsAsync<OrderValidationException>(
            () => service.CreateAsync(
                Customer(CustomerTier.Standard),
                [new OrderItem("ST-01", "Sticker", 2m, 1)]));

        Assert.Contains("Minimum order value", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_InvalidOrder_IsNotPersistedOrAnnounced()
    {
        var (service, repository, notifier) = CreateSut();

        await Assert.ThrowsAsync<OrderValidationException>(
            () => service.CreateAsync(Customer(), []));

        Assert.Empty(repository.Orders);
        Assert.Empty(notifier.Events);
    }

    // -----------------------------------------------------------------------
    // State machine (LSP: every order obeys the same rules)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task CancelAsync_ConfirmedOrder_MarksCancelledAndNotifies()
    {
        var (service, repository, notifier) = CreateSut();
        var order = await service.CreateAsync(Customer(), Keyboard());

        var cancelled = await service.CancelAsync(order.Id);

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(OrderStatus.Cancelled, repository.Orders.Single().Status);
        Assert.Contains($"cancelled:{order.Id}", notifier.Events);
    }

    [Fact]
    public async Task CancelAsync_Twice_ThrowsInsteadOfSilentlySucceeding()
    {
        var (service, _, _) = CreateSut();
        var order = await service.CreateAsync(Customer(), Keyboard());

        await service.CancelAsync(order.Id);

        await Assert.ThrowsAsync<DomainException>(() => service.CancelAsync(order.Id));
    }

    [Fact]
    public async Task RefundAsync_ConfirmedOrder_Succeeds()
    {
        var (service, _, notifier) = CreateSut();
        var order = await service.CreateAsync(Customer(), Keyboard());

        var refunded = await service.RefundAsync(order.Id);

        Assert.Equal(OrderStatus.Refunded, refunded.Status);
        Assert.Contains($"refunded:{order.Id}", notifier.Events);
    }

    [Fact]
    public async Task RefundAsync_CancelledOrder_IsRejected()
    {
        var (service, _, _) = CreateSut();
        var order = await service.CreateAsync(Customer(), Keyboard());
        await service.CancelAsync(order.Id);

        await Assert.ThrowsAsync<DomainException>(() => service.RefundAsync(order.Id));
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNullAndCancelThrowsNotFound()
    {
        var (service, _, _) = CreateSut();
        var id = Guid.NewGuid();

        Assert.Null(await service.GetAsync(id));
        await Assert.ThrowsAsync<OrderNotFoundException>(() => service.CancelAsync(id));
    }

    // -----------------------------------------------------------------------
    // Payments (LSP: cash on delivery is a legal result, not an exception)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PayAsync_WithCard_CapturesMoney()
    {
        var (service, _, _) = CreateSut();
        var order = await service.CreateAsync(Customer(), Keyboard());

        var receipt = await service.PayAsync("card", order.Id);

        Assert.Equal(PaymentSettlement.Captured, receipt.Settlement);
        Assert.Equal(order.Total, receipt.Amount);
        Assert.StartsWith("CARD-", receipt.Reference);
    }

    [Fact]
    public async Task PayAsync_WithCashOnDelivery_DefersPaymentInsteadOfThrowing()
    {
        var (service, _, _) = CreateSut();
        var order = await service.CreateAsync(Customer(), Keyboard());

        var receipt = await service.PayAsync("cod", order.Id);

        Assert.Equal(PaymentSettlement.DueOnDelivery, receipt.Settlement);
        Assert.StartsWith("COD-", receipt.Reference);
    }

    [Fact]
    public async Task PayAsync_WithUnknownMethod_IsRejectedBeforeAnyWork()
    {
        var (service, _, _) = CreateSut();
        var order = await service.CreateAsync(Customer(), Keyboard());

        await Assert.ThrowsAsync<UnknownPaymentMethodException>(
            () => service.PayAsync("bitcoin", order.Id));
    }
}
