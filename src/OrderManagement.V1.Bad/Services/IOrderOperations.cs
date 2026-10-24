using OrderManagement.V1.Bad.Domain;

namespace OrderManagement.V1.Bad.Services;

/// <summary>
/// ISP violation: one fat interface for "everything an order system can do".
///
/// A read-only consumer (see <see cref="SalesReportService"/>) needs exactly one member
/// but is forced to depend on ten. Anyone writing a second implementation - a test double,
/// a mobile adapter, a read replica - must stub nine members they will never call.
/// </summary>
public interface IOrderOperations
{
    void SaveOrder(Order order);
    Order? GetOrder(Guid id);
    List<Order> GetAllOrders();
    void SendEmail(string to, string subject, string body);
    void SendSms(string to, string message);
    string GeneratePdfReceipt(Guid orderId);
    void ImportFromCsv(string filePath);
    void ExportToExcel(string filePath);
    void SyncInventory();
    void PrintInvoice(Guid orderId);
}
