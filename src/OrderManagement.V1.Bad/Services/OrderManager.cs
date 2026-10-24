using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using OrderManagement.V1.Bad.Domain;
using OrderManagement.V1.Bad.Infrastructure;

namespace OrderManagement.V1.Bad.Services;

/// <summary>
/// THE GOD CLASS.
///
/// SRP violation: this one type validates input, prices the order, talks to SQLite,
/// sends e-mail, writes logs, builds receipts and reports on sales. Six reasons to change.
///
/// OCP violation: customer tiers and notification channels are hard-coded if/else and
/// switch blocks. Every new business rule means editing this file.
///
/// DIP violation: it creates SqliteConnection and SmtpClient itself, and reads static
/// <see cref="GlobalState"/>. There is no abstraction to substitute, so the class cannot
/// be unit tested without a real database and a real SMTP server.
///
/// ISP violation: it implements the 10-member <see cref="IOrderOperations"/> interface
/// and throws NotImplementedException from five of them.
/// </summary>
public class OrderManager : IOrderOperations
{
    public OrderManager()
    {
        // DDL on every request, from the constructor. Also a DIP smell.
        GlobalState.EnsureDatabase();
    }

    // ---------------------------------------------------------------------
    // "Create order" - validation + pricing + persistence + e-mail in one method
    // ---------------------------------------------------------------------
    public Order CreateOrder(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerName) || order.CustomerName.Trim().Length < 2)
            throw new Exception("Customer name is required.");

        if (string.IsNullOrWhiteSpace(order.CustomerEmail) || !order.CustomerEmail.Contains('@'))
            throw new Exception("A valid e-mail is required.");

        if (order.Items.Count == 0)
            throw new Exception("An order needs at least one item.");

        // No quantity/price validation at all: Quantity = 0 or -5 is happily accepted.

        order.Status = "Confirmed";
        order.CreatedAt = DateTime.Now;   //  hard to test, ignores time zones
        order.Subtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        if (order.Subtotal < 10m)
            throw new Exception("Minimum order value is 10.00.");

        order.Discount = CalculateDiscount(order);

        // magic number 0.08 - repeated in the refund path below. Tax policy is hidden here.
        order.Tax = Math.Round((order.Subtotal - order.Discount) * 0.08m, 2);
        order.Total = order.Subtotal - order.Discount + order.Tax;

        SaveOrder(order);
        NotifyCustomer("email", order, "created");

        Console.WriteLine($"[LOG] Order {order.Id} created. Total={order.Total:F2}");
        return order;
    }

    // ---------------------------------------------------------------------
    // Pricing - OCP violation
    // ---------------------------------------------------------------------
    private decimal CalculateDiscount(Order order)
    {
        decimal percent = 0m;
        var tier = (order.Tier ?? string.Empty).Trim().ToLowerInvariant();

        //  OCP: adding a "gold" tier means editing this method, the API docs,
        //    the tests and the notification wording. Unknown tiers silently get 0%.
        if (tier == "premium")
            percent = 10m;
        else if (tier == "vip")
            percent = 20m;

        return Math.Round(order.Subtotal * percent / 100m, 2);
    }

    // ---------------------------------------------------------------------
    // Persistence - hand written SQL, string concatenation, sync I/O
    // ---------------------------------------------------------------------
    public void SaveOrder(Order order)
    {
        using var connection = new SqliteConnection(GlobalState.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        var itemsJson = JsonSerializer.Serialize(order.Items);
        var refund = order as RefundOrder;

        // SQL injection: user input is concatenated straight into the statement.
        //    A customer called O'Brien breaks it; a crafted name could drop the table.
        command.CommandText =
            "INSERT INTO Orders (Id, CustomerName, CustomerEmail, Tier, Status, CreatedAt, Subtotal, Discount, Tax, Total, Items, OriginalOrderId) VALUES (" +
            $"'{order.Id}', '{order.CustomerName}', '{order.CustomerEmail}', '{order.Tier}', '{order.Status}', " +
            $"'{order.CreatedAt:o}', {Num(order.Subtotal)}, {Num(order.Discount)}, {Num(order.Tax)}, {Num(order.Total)}, " +
            $"'{itemsJson}', {(refund is null ? "NULL" : $"'{refund.OriginalOrderId}'")})";

        command.ExecuteNonQuery();

        if (!GlobalState.Cache.Any(o => o.Id == order.Id))
            GlobalState.Cache.Add(order);
    }

    private void UpdateOrder(Order order)
    {
        using var connection = new SqliteConnection(GlobalState.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE Orders SET Status = '" + order.Status + "'," +
            " Subtotal = " + Num(order.Subtotal) + ", Discount = " + Num(order.Discount) +
            ", Tax = " + Num(order.Tax) + ", Total = " + Num(order.Total) +
            " WHERE Id = '" + order.Id + "'";

        command.ExecuteNonQuery();
    }

    public Order? GetOrder(Guid id) => ReadOrders("WHERE Id = '" + id + "'").FirstOrDefault();

    public List<Order> GetAllOrders()
    {
        //  Global mutable cache: shared by every request, never invalidated properly,
        //    and a cancelled order can still be served stale from here.
        if (!GlobalState.CacheLoaded)
        {
            GlobalState.Cache.AddRange(ReadOrders(string.Empty));
            GlobalState.CacheLoaded = true;
        }

        return GlobalState.Cache;
    }

    private static List<Order> ReadOrders(string whereClause)
    {
        var orders = new List<Order>();

        using var connection = new SqliteConnection(GlobalState.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, CustomerName, CustomerEmail, Tier, Status, CreatedAt, Subtotal, Discount, Tax, Total, Items, OriginalOrderId " +
            "FROM Orders " + whereClause + " ORDER BY CreatedAt";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var originalId = reader.IsDBNull(11) ? (Guid?)null : Guid.Parse(reader.GetString(11));

            Order order = originalId is null
                ? new Order()
                : new RefundOrder { OriginalOrderId = originalId.Value };

            order.Id = Guid.Parse(reader.GetString(0));
            order.CustomerName = reader.GetString(1);
            order.CustomerEmail = reader.GetString(2);
            order.Tier = reader.GetString(3);
            order.Status = reader.GetString(4);
            order.CreatedAt = DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            order.Subtotal = (decimal)reader.GetDouble(6);
            order.Discount = (decimal)reader.GetDouble(7);
            order.Tax = (decimal)reader.GetDouble(8);
            order.Total = (decimal)reader.GetDouble(9);
            order.Items = JsonSerializer.Deserialize<List<OrderItem>>(reader.GetString(10)) ?? new List<OrderItem>();

            orders.Add(order);
        }

        return orders;
    }

    // ---------------------------------------------------------------------
    // Business operations
    // ---------------------------------------------------------------------
    public Order? CancelOrder(Guid id)
    {
        var order = GetOrder(id);
        if (order is null)
            return null;

        // No state machine: a cancelled order can be cancelled twice, and a refused
        //    or refunded order can still be cancelled. The domain has no invariants.
        order.Status = "Cancelled";
        UpdateOrder(order);
        NotifyCustomer("email", order, "cancelled");

        var cached = GlobalState.Cache.FirstOrDefault(o => o.Id == id);
        if (cached is not null)
            cached.Status = "Cancelled";

        return order;
    }

    /// <summary>
    /// LSP demonstrated: the method is written against <see cref="Order"/> and works for
    /// every normal order. Hand it a <see cref="RefundOrder"/> and it throws at runtime.
    /// </summary>
    public Order RefundOrder(Guid id)
    {
        var original = GetOrder(id) ?? throw new Exception("Order not found.");

        var refund = new RefundOrder
        {
            OriginalOrderId = original.Id,
            CustomerName = original.CustomerName,
            CustomerEmail = original.CustomerEmail,
            Tier = original.Tier,
            Status = "Refunded",
            CreatedAt = DateTime.Now,
            Subtotal = -original.Subtotal,
            Discount = -original.Discount,
            Tax = -original.Tax,
            Total = -original.Total
        };

        foreach (var item in original.Items)
            refund.AddItem(item);   // 💥 NotSupportedException - the subtype broke the contract

        SaveOrder(refund);
        NotifyCustomer("email", refund, "refunded");
        return refund;
    }

    /// <summary>
    /// LSP demonstrated again: callers hold a <see cref="PaymentProcessor"/> and cannot know
    /// that one implementation refuses to run.
    /// </summary>
    public string ProcessPayment(Guid id, PaymentProcessor processor)
    {
        var order = GetOrder(id) ?? throw new Exception("Order not found.");

        var reference = processor.Process(order.Total);   // 💥 CashOnDeliveryProcessor throws
        Console.WriteLine($"[LOG] Payment {reference} for order {order.Id}");
        return reference;
    }

    // ---------------------------------------------------------------------
    // Notifications - OCP violation + swallowed exceptions
    // ---------------------------------------------------------------------
    public void NotifyCustomer(string channel, Order order, string reason)
    {
        var subject = reason switch
        {
            "cancelled" => "Your order was cancelled",
            "refunded" => "Your refund was issued",
            _ => "Order confirmation"
        };

        var body = $"Hi {order.CustomerName}, your order {order.Id} is now {order.Status}. Total: {order.Total:F2}.";

        //  OCP: a new channel ("push", "whatsapp") means editing this switch - and the
        //    fat interface, every fake, and every test.
        switch (channel)
        {
            case "email":
                SendEmail(order.CustomerEmail, subject, body);
                break;
            case "sms":
                SendSms(order.CustomerEmail, body);   // no phone number field exists at all
                break;
            default:
                throw new Exception("Unknown notification channel: " + channel);
        }
    }

    public void SendEmail(string to, string subject, string body)
    {
        try
        {
            using var client = new SmtpClient("localhost", 25) { EnableSsl = false, Timeout = 1500 };
            using var message = new MailMessage("orders@shop.local", to, subject, body);

            client.Send(message);
            Console.WriteLine($"[EMAIL] {subject} -> {to}");
        }
        catch (Exception)
        {
            //  Swallowed: the caller believes the customer was notified.
            //    No retry, no outbox, no way to notice in production.
            Console.WriteLine($"[EMAIL-FAILED] {subject} -> {to}");
        }
    }

    // ---------------------------------------------------------------------
    // Fat interface members that nobody should have to implement
    // ---------------------------------------------------------------------
    public string GeneratePdfReceipt(Guid orderId)
    {
        var order = GetOrder(orderId) ?? throw new Exception("Order not found.");
        return $"RECEIPT {order.Id}\n{order.CustomerName}\nTotal: {order.Total:F2}";
    }

    public void SendSms(string to, string message) => throw new NotImplementedException();
    public void ImportFromCsv(string filePath) => throw new NotImplementedException();
    public void ExportToExcel(string filePath) => throw new NotImplementedException();
    public void SyncInventory() => throw new NotImplementedException();
    public void PrintInvoice(Guid orderId) => throw new NotImplementedException();

    private static string Num(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
