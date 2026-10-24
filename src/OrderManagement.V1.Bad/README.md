# V1 · Order Management (intentionally bad)

> ⚠️ **Educational code only.** This project violates all five SOLID principles and contains
> a real SQL injection. Never deploy it or copy from it.

Same API and UI as V2, so the two versions can be compared side by side.

```powershell
dotnet run --project src/OrderManagement.V1.Bad
# http://localhost:5081
```

## Where each principle is broken

| Principle | File | What to look at |
|---|---|---|
| **SRP** | `Services/OrderManager.cs` | one class validates, prices, writes SQL, sends e-mail, logs, builds receipts |
| **OCP** | `Services/OrderManager.cs` | tier `if/else`, notification `switch`, payment mapping in the endpoint |
| **LSP** | `Domain/Order.cs` | `RefundOrder.AddItem()` throws `NotSupportedException` |
| **LSP** | `Domain/PaymentProcessor.cs` | `CashOnDeliveryProcessor.Process()` throws |
| **ISP** | `Services/IOrderOperations.cs` | ten members, `SalesReportService` needs one |
| **DIP** | `Program.cs`, `Infrastructure/GlobalState.cs` | `new OrderManager()`, `new SqliteConnection`, `new SmtpClient`, static cache |

## Try these (they are supposed to fail)

| Action | Result |
|---|---|
| Customer name `O'Brien` | `500` — SQLite syntax error from concatenated SQL |
| Refund an order | `500` — "Refund orders cannot contain items." |
| Pay with cash on delivery | `500` — "Cash on delivery cannot be charged up front." |
| Tier `Gold` | `201` with a silent 0% discount |
| Cancel twice | `200` twice — no state machine |
| Quantity `0` (with a valid second item) | `201` — the zero line is stored |

Requests: `requests.http`. Data: `orders-v1.db` (delete it to reset).

**Go to the fixed version:** [`../OrderManagement.V2.Good`](../OrderManagement.V2.Good)
