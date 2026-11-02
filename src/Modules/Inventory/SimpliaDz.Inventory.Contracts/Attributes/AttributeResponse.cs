namespace SimpliaDz.Inventory.Contracts.Attributes;

public sealed record AttributeValueResponse(Guid Id, Guid AttributeId, string Value, int DisplayOrder);
public sealed record AttributeResponse(Guid Id, string Name, IReadOnlyList<AttributeValueResponse> Values);
public sealed record CreateAttributeRequest(string Name);
public sealed record CreateAttributeValueRequest(string Value, int DisplayOrder);
public sealed record UpdateDisplayOrderRequest(int DisplayOrder);
