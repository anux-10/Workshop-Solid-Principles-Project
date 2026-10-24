using OrderManagement.V2.Good.Abstractions;
using OrderManagement.V2.Good.Api;
using OrderManagement.V2.Good.Discounts;
using OrderManagement.V2.Good.Infrastructure;
using OrderManagement.V2.Good.Payments;
using OrderManagement.V2.Good.Services;
using OrderManagement.V2.Good.Validation;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// COMPOSITION ROOT
// Every arrow in the architecture diagram points at an abstraction above;
// the concrete classes below are wired here and nowhere else.
// ---------------------------------------------------------------------------

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Database"));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddSingleton(TimeProvider.System);

// Persistence (DIP)
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>();

// Validation (SRP + OCP: rules are composed, not hard-coded)
builder.Services.AddScoped<IOrderValidator, OrderValidator>();
builder.Services.AddScoped<IOrderValidationRule, CustomerDetailsRule>();
builder.Services.AddScoped<IOrderValidationRule, ItemsRule>();
builder.Services.AddScoped<IOrderValidationRule, MinimumOrderValueRule>();

// Pricing (OCP: one class per tier, OrderService untouched)
builder.Services.AddScoped<IDiscountStrategy, StandardDiscount>();
builder.Services.AddScoped<IDiscountStrategy, PremiumDiscount>();
builder.Services.AddScoped<IDiscountStrategy, VipDiscount>();
builder.Services.AddScoped<IDiscountStrategyProvider, DiscountStrategyProvider>();

// Payments (LSP: every processor honours the same contract)
builder.Services.AddScoped<IPaymentProcessor, CardPaymentProcessor>();
builder.Services.AddScoped<IPaymentProcessor, CashOnDeliveryProcessor>();
builder.Services.AddScoped<IPaymentProcessorProvider, PaymentProcessorProvider>();

// Notifications (DIP + OCP: add a channel without touching OrderService)
builder.Services.AddScoped<SmtpNotifier>();
builder.Services.AddScoped<AuditLogNotifier>();
builder.Services.AddScoped<IOrderNotifier>(serviceProvider => new CompositeOrderNotifier(
    serviceProvider.GetRequiredService<SmtpNotifier>(),
    serviceProvider.GetRequiredService<AuditLogNotifier>()));

// Application services
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<SalesReportService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

// Schema creation happens once, at startup - not in a constructor on every request.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

app.MapOrderEndpoints();

app.Run();
