using OrderManagement.V1.Bad.Contracts;
using OrderManagement.V1.Bad.Domain;
using OrderManagement.V1.Bad.Infrastructure;
using OrderManagement.V1.Bad.Services;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

GlobalState.EnsureDatabase();


app.MapPost("/api/orders", (CreateOrderRequest request) =>
{
    try
    {
        var order = new OrderManager().CreateOrder(Map(request));
        return Results.Created($"/api/orders/{order.Id}", order);
    }
    catch (Exception ex)
    {
        
        Console.WriteLine("[ERROR] " + ex);
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/api/orders", () => Results.Ok(new OrderManager().GetAllOrders()));

app.MapGet("/api/orders/{id:guid}", (Guid id) =>
{
    var order = new OrderManager().GetOrder(id);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.MapPost("/api/orders/{id:guid}/cancel", (Guid id) =>
{
    var order = new OrderManager().CancelOrder(id);
    return order is null
        ? Results.Problem("Order not found.", statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(order);
});

app.MapPost("/api/orders/{id:guid}/refund", (Guid id) =>
{
    try
    {
        return Results.Ok(new OrderManager().RefundOrder(id));
    }
    catch (Exception ex)
    {
        // 💥 LSP explosion 
        Console.WriteLine("[ERROR] " + ex);
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/orders/{id:guid}/pay", (Guid id, PayRequest request) =>
{
   
    PaymentProcessor processor = (request.Method ?? "card").Trim().ToLowerInvariant() == "cod"
        ? new CashOnDeliveryProcessor()
        : new CardPaymentProcessor();

    try
    {
        var reference = new OrderManager().ProcessPayment(id, processor);
        return Results.Ok(new { reference });
    }
    catch (Exception ex)
    {
        Console.WriteLine("[ERROR] " + ex);
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/api/reports/sales", () => Results.Ok(new SalesReportService(new OrderManager()).Build()));

app.Run();

// Mapping lives in the endpoint file too - one more responsibility in one place.
static Order Map(CreateOrderRequest request) => new()
{
    CustomerName = request.Customer?.Name ?? string.Empty,
    CustomerEmail = request.Customer?.Email ?? string.Empty,
    Tier = request.Customer?.Tier ?? "standard",
    Items = (request.Items ?? new List<ItemInput>())
        .Select(i => new OrderItem
        {
            Sku = i.Sku ?? string.Empty,
            Name = i.Name ?? string.Empty,
            UnitPrice = i.UnitPrice,
            Quantity = i.Quantity
        })
        .ToList()
};
