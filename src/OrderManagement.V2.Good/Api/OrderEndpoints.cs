using OrderManagement.V2.Good.Domain;
using OrderManagement.V2.Good.Services;

namespace OrderManagement.V2.Good.Api;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders");

        orders.MapPost("/", CreateOrder);
        orders.MapGet("/", ListOrders);
        orders.MapGet("/{id:guid}", GetOrder);
        orders.MapPost("/{id:guid}/cancel", CancelOrder);
        orders.MapPost("/{id:guid}/refund", RefundOrder);
        orders.MapPost("/{id:guid}/pay", PayOrder);

        // The report depends on SalesReportService -> IOrderRepository only.
        app.MapGet("/api/reports/sales", GetSalesReport);

        return app;
    }

    private static async Task<IResult> CreateOrder(
        CreateOrderRequest request,
        OrderService service,
        CancellationToken cancellationToken)
    {
        var customer = MapCustomer(request.Customer);
        var items = MapItems(request.Items);

        var order = await service.CreateAsync(customer, items, cancellationToken);
        return Results.Created($"/api/orders/{order.Id}", OrderResponse.From(order));
    }

    private static async Task<IResult> ListOrders(OrderService service, CancellationToken cancellationToken)
    {
        var orders = await service.ListAsync(cancellationToken);
        return Results.Ok(orders.Select(OrderResponse.From));
    }

    private static async Task<IResult> GetOrder(Guid id, OrderService service, CancellationToken cancellationToken)
    {
        var order = await service.GetAsync(id, cancellationToken);
        return order is null ? Results.NotFound() : Results.Ok(OrderResponse.From(order));
    }

    private static async Task<IResult> CancelOrder(Guid id, OrderService service, CancellationToken cancellationToken) =>
        Results.Ok(OrderResponse.From(await service.CancelAsync(id, cancellationToken)));

    private static async Task<IResult> RefundOrder(Guid id, OrderService service, CancellationToken cancellationToken) =>
        Results.Ok(OrderResponse.From(await service.RefundAsync(id, cancellationToken)));

    private static async Task<IResult> PayOrder(
        Guid id,
        PayRequest request,
        OrderService service,
        CancellationToken cancellationToken)
    {
        var receipt = await service.PayAsync(request.Method ?? "card", id, cancellationToken);
        return Results.Ok(new PaymentResponse(receipt.Reference, receipt.Amount, receipt.Settlement.ToString()));
    }

    private static async Task<IResult> GetSalesReport(
        SalesReportService service,
        CancellationToken cancellationToken) =>
        Results.Ok(SalesReportResponse.From(await service.BuildAsync(cancellationToken)));

    // ---------------------------------------------------------------------
    // Boundary mapping. Bad input becomes a 400 with a readable message here;
    // it never reaches the domain as a magic string.
    // ---------------------------------------------------------------------
    private static Customer MapCustomer(CustomerRequest? request)
    {
        if (request is null)
            throw new OrderValidationException(["Customer details are required."]);

        if (!Enum.TryParse<CustomerTier>(request.Tier, ignoreCase: true, out var tier) || !Enum.IsDefined(tier))
            throw new OrderValidationException([$"Unknown customer tier '{request.Tier}'. Use Standard, Premium or Vip."]);

        return new Customer(
            request.Name?.Trim() ?? string.Empty,
            request.Email?.Trim() ?? string.Empty,
            tier);
    }

    private static IReadOnlyList<OrderItem> MapItems(List<OrderItemRequest>? items) =>
        (items ?? [])
            .Select(item => new OrderItem(
                item.Sku?.Trim() ?? string.Empty,
                item.Name?.Trim() ?? string.Empty,
                item.UnitPrice,
                item.Quantity))
            .ToArray();
}
