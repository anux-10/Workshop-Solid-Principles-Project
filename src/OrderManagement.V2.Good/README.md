# V2 · Order Management (SOLID)

The corrected counterpart of `OrderManagement.V1.Bad`. Same API, same UI, and the same
business behaviour — but every dependency points at an abstraction.

```powershell
dotnet run --project src/OrderManagement.V2.Good
# http://localhost:5082

dotnet test tests/OrderManagement.V2.Good.Tests
```

## Architecture

```
OrderService ──> IOrderValidator          <── OrderValidator + rules
             ──> IDiscountStrategyProvider <── Standard / Premium / Vip
             ──> IOrderRepository          <── SqlOrderRepository
             ──> IOrderNotifier            <── SmtpNotifier + AuditLogNotifier
             ──> IPaymentProcessorProvider  <── Card / CashOnDelivery
```

`Program.cs` is the composition root: concrete classes are registered there and nowhere else.

## How each principle is satisfied

- **SRP** — `OrderService` orchestrates; validation, pricing, persistence, notifications and
  payment each live in their own type. `DatabaseInitializer` owns the schema.
- **OCP** — a new tier, notifier or payment method is a new class plus one DI registration.
  `OrderService` and the endpoints do not change.
- **LSP** — `Order` is an aggregate with a state machine (no lying subclasses), and
  `IPaymentProcessor` returns a receipt whose settlement may be `DueOnDelivery`.
- **ISP** — small, cohesive interfaces (`IOrderRepository` has four members).
  `SalesReportService` depends on the repository only.
- **DIP** — constructor injection throughout, including `TimeProvider` and `ILogger<T>`.
  No `new SqliteConnection`, no `new SmtpClient`, no static state.

## Behaviour

| Situation | Result |
|---|---|
| Name with an apostrophe | `201` — parameterised SQL |
| Refund | `200`, status `Refunded` |
| Refund an already cancelled order | `409` |
| Cash on delivery | `200`, `DueOnDelivery` |
| Unknown payment method | `400` |
| Unknown tier | `400` with a readable message |
| Quantity outside 1..100 | `400` |
| Cancel twice | `200`, then `409` |

Requests: `requests.http`. Data: `orders-v2.db`; e-mail outbox: `notifications.log`.

**Compare with the broken version:** [`../OrderManagement.V1.Bad`](../OrderManagement.V1.Bad)
