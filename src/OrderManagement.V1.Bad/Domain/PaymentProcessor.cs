namespace OrderManagement.V1.Bad.Domain;

/// <summary>
/// LSP violation #2 (the realistic one).
///
/// The base contract says: "Process(amount) charges the money and returns a reference".
/// <see cref="CashOnDeliveryProcessor"/> cannot charge anything up front, so it throws.
/// Every caller that holds a <see cref="PaymentProcessor"/> is now one "cod" value away
/// from a runtime crash - the abstraction is lying.
/// </summary>
public abstract class PaymentProcessor
{
    public abstract string Process(decimal amount);
}

public class CardPaymentProcessor : PaymentProcessor
{
    public override string Process(decimal amount) =>
        "CARD-" + Guid.NewGuid().ToString("N")[..10] + " charged " + amount.ToString("F2");
}

/// <summary>
/// "Cash on delivery is a payment processor too!" - no, it is payment *terms*.
/// This type exists only because the hierarchy was designed before the requirement.
/// </summary>
public class CashOnDeliveryProcessor : PaymentProcessor
{
    public override string Process(decimal amount) =>
        throw new NotSupportedException("Cash on delivery cannot be charged up front.");
}
