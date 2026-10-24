using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderManagement.V2.Good.Abstractions;
using OrderManagement.V2.Good.Domain;

namespace OrderManagement.V2.Good.Infrastructure;

/// <summary>
/// DIP: <see cref="OrderService"/> sends mail through <see cref="IOrderNotifier"/>; this class
/// owns the SMTP detail. With UseSmtp=false it writes to the dev outbox, so the app runs
/// without a mail server - and tests never touch SMTP at all.
/// </summary>
public sealed class SmtpNotifier : IOrderNotifier
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly NotificationOptions _options;
    private readonly ILogger<SmtpNotifier> _logger;

    public SmtpNotifier(IOptions<NotificationOptions> options, ILogger<SmtpNotifier> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task OrderConfirmedAsync(Order order, CancellationToken cancellationToken = default) =>
        SendAsync(
            order,
            "Order confirmed",
            $"Hi {order.Customer.Name}, your order {order.Id} was confirmed. Total: {order.Total:F2}.",
            cancellationToken);

    public Task OrderCancelledAsync(Order order, CancellationToken cancellationToken = default) =>
        SendAsync(
            order,
            "Order cancelled",
            $"Hi {order.Customer.Name}, your order {order.Id} was cancelled.",
            cancellationToken);

    public Task RefundIssuedAsync(Order order, CancellationToken cancellationToken = default) =>
        SendAsync(
            order,
            "Refund issued",
            $"Hi {order.Customer.Name}, the refund for order {order.Id} was issued ({order.Total:F2}).",
            cancellationToken);

    private async Task SendAsync(Order order, string subject, string body, CancellationToken cancellationToken)
    {
        if (!_options.UseSmtp)
        {
            _logger.LogInformation(
                "DEV OUTBOX -> {Email} | {Subject} | {Body}",
                order.Customer.Email, subject, body);
            return;
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            using var message = new MailMessage(_options.FromAddress, order.Customer.Email, subject, body);
            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort) { EnableSsl = _options.UseSsl };

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("SMTP -> {Email} | {Subject}", order.Customer.Email, subject);
        }
        finally
        {
            Gate.Release();
        }
    }
}
