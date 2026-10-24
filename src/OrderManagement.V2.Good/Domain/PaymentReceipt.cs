namespace OrderManagement.V2.Good.Domain;

/// <summary>
/// An honest result type: the settlement is part of the contract, so cash on delivery
/// is a legitimate outcome instead of an exception. This is the LSP fix for V1.
/// </summary>
public sealed record PaymentReceipt(string Reference, decimal Amount, PaymentSettlement Settlement);

public enum PaymentSettlement
{
    /// <summary>Money was taken now (card).</summary>
    Captured,

    /// <summary>Money will be taken at delivery (cash on delivery). Not an error.</summary>
    DueOnDelivery
}
