using FluentValidation;
using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.Contracts.Attributes;
using Simplia.Inventory.Core.Entities.Catalog;
using Simplia.Inventory.UseCases.Attributes.Abstractions;

namespace Simplia.Inventory.UseCases.Attributes;

public sealed record CreateAttributeCommand(string Name) : IRequest<AttributeResponse>;
public sealed record CreateAttributeValueCommand(Guid AttributeId, string Value, int DisplayOrder) : IRequest<AttributeValueResponse>;
public sealed record SetAttributeValueOrderCommand(Guid AttributeId, Guid ValueId, int DisplayOrder) : IRequest<AttributeValueResponse>;
public sealed record GetAttributesQuery : IRequest<IReadOnlyList<AttributeResponse>>;
public sealed record GetAttributeQuery(Guid Id) : IRequest<AttributeResponse>;

public sealed class CreateAttributeValidator : AbstractValidator<CreateAttributeCommand>
{
    public CreateAttributeValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
}

public sealed class CreateAttributeValueValidator : AbstractValidator<CreateAttributeValueCommand>
{
    public CreateAttributeValueValidator()
    {
        RuleFor(x => x.Value).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class SetAttributeValueOrderValidator : AbstractValidator<SetAttributeValueOrderCommand>
{
    public SetAttributeValueOrderValidator() => RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
}

internal static class AttributeMapping
{
    public static AttributeValueResponse ToResponse(this AttributeValue value) =>
        new(value.Id, value.AttributeId, value.Value, value.DisplayOrder);

    public static AttributeResponse ToResponse(this ProductAttribute attribute) =>
        new(attribute.Id, attribute.Name,
            attribute.Values.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Value)
                .Select(x => x.ToResponse()).ToArray());
}

public sealed class CreateAttributeHandler(IAttributeRepository repository)
    : IRequestHandler<CreateAttributeCommand, AttributeResponse>
{
    public async ValueTask<AttributeResponse> Handle(CreateAttributeCommand request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await repository.NameExistsAsync(name, ct))
            throw new ConflictException($"Attribute '{name}' already exists.");
        var attribute = new ProductAttribute(name);
        await repository.AddAsync(attribute, ct);
        await repository.SaveChangesAsync(ct);
        return attribute.ToResponse();
    }
}

public sealed class CreateAttributeValueHandler(IAttributeRepository repository)
    : IRequestHandler<CreateAttributeValueCommand, AttributeValueResponse>
{
    public async ValueTask<AttributeValueResponse> Handle(CreateAttributeValueCommand request, CancellationToken ct)
    {
        var attribute = await repository.GetByIdAsync(request.AttributeId, ct)
            ?? throw new NotFoundException("Attribute", request.AttributeId);
        if (attribute.Values.Any(x => x.Value.Equals(request.Value.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"Value '{request.Value.Trim()}' already exists for this attribute.");
        var value = attribute.AddValue(request.Value, request.DisplayOrder);
        await repository.AddValueAsync(value, ct);
        await repository.SaveChangesAsync(ct);
        return value.ToResponse();
    }
}

public sealed class SetAttributeValueOrderHandler(IAttributeRepository repository)
    : IRequestHandler<SetAttributeValueOrderCommand, AttributeValueResponse>
{
    public async ValueTask<AttributeValueResponse> Handle(SetAttributeValueOrderCommand request, CancellationToken ct)
    {
        var attribute = await repository.GetByIdAsync(request.AttributeId, ct)
            ?? throw new NotFoundException("Attribute", request.AttributeId);
        var value = attribute.Values.FirstOrDefault(x => x.Id == request.ValueId)
            ?? throw new NotFoundException("Attribute value", request.ValueId);
        value.SetDisplayOrder(request.DisplayOrder);
        await repository.SaveChangesAsync(ct);
        return value.ToResponse();
    }
}

public sealed class GetAttributesHandler(IAttributeRepository repository)
    : IRequestHandler<GetAttributesQuery, IReadOnlyList<AttributeResponse>>
{
    public async ValueTask<IReadOnlyList<AttributeResponse>> Handle(GetAttributesQuery request, CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(x => x.ToResponse()).ToArray();
}

public sealed class GetAttributeHandler(IAttributeRepository repository)
    : IRequestHandler<GetAttributeQuery, AttributeResponse>
{
    public async ValueTask<AttributeResponse> Handle(GetAttributeQuery request, CancellationToken ct) =>
        (await repository.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Attribute", request.Id)).ToResponse();
}
