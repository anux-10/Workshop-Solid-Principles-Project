namespace OrderManagement.V1.Bad.Contracts;

// The API contract is fine - it is the only part of V1 worth keeping.
// V2 reuses the exact same JSON shape, which is why one UI can drive both versions.

public sealed record CustomerInput(string? Name, string? Email, string? Tier);

public sealed record ItemInput(string? Sku, string? Name, decimal UnitPrice, int Quantity);

public sealed record CreateOrderRequest(CustomerInput? Customer, List<ItemInput>? Items);

public sealed record PayRequest(string? Method);
