using FluentValidation;
using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Contracts.Variants;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.UseCases.Products.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Variants;

public sealed record CreateVariantCommand(Guid ProductId, string Sku, string? Barcode,
    decimal RetailPrice, decimal? WholesalePrice, IReadOnlyList<Guid> AttributeValueIds) : IRequest<VariantResponse>;
public sealed record GetVariantBySkuQuery(string Sku) : IRequest<VariantResponse>;
public sealed record GetVariantByBarcodeQuery(string Barcode) : IRequest<VariantResponse>;
public sealed record GetVariantQuery(Guid Id) : IRequest<VariantResponse>;
public sealed record DeactivateVariantCommand(Guid Id) : IRequest;

public sealed class CreateVariantValidator : AbstractValidator<CreateVariantCommand>
{
    public CreateVariantValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Barcode).MaximumLength(64);
        RuleFor(x => x.RetailPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.WholesalePrice).GreaterThanOrEqualTo(0).When(x => x.WholesalePrice.HasValue);
        RuleFor(x => x.AttributeValueIds).NotEmpty();
    }
}

internal static class VariantMapping
{
    public static VariantResponse ToResponse(this ProductVariant variant) =>
        new(variant.Id, variant.ProductId, variant.Sku.Value, variant.Barcode,
            variant.IsActive, variant.RetailPrice, variant.WholesalePrice,
            variant.AttributeValues.OrderBy(link => link.AttributeValue.Attribute.Name)
                .Select(link => new VariantValueResponse(link.AttributeValueId,
                    link.AttributeValue.AttributeId, link.AttributeValue.Attribute.Name,
                    link.AttributeValue.Value, link.AttributeValue.DisplayOrder)).ToArray());
}

public sealed class CreateVariantHandler(IProductRepository products)
    : IRequestHandler<CreateVariantCommand, VariantResponse>
{
    public async ValueTask<VariantResponse> Handle(CreateVariantCommand request, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(request.ProductId, ct)
            ?? throw new NotFoundException("Product", request.ProductId);
        if (!product.IsActive) throw new ConflictException("Cannot add a variant to an inactive product.");

        var sku = request.Sku.Trim();
        var barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        if (await products.SkuExistsAsync(sku, ct)) throw new ConflictException($"SKU '{sku}' already exists.");
        if (barcode is not null && await products.BarcodeExistsAsync(barcode, ct))
            throw new ConflictException($"Barcode '{barcode}' already exists.");

        var ids = request.AttributeValueIds;
        if (ids.Distinct().Count() != ids.Count || ids.Any(id => id == Guid.Empty))
            throw new ConflictException("Attribute values must be distinct and valid.");
        var values = await products.GetAttributeValuesAsync(ids.ToArray(), ct);
        if (values.Count != ids.Count) throw new ConflictException("One or more attribute values do not exist.");
        if (values.Select(x => x.AttributeId).Distinct().Count() != values.Count)
            throw new ConflictException("Select at most one value for each attribute.");

        ProductVariant variant;
        try
        {
            variant = product.AddVariant(sku, request.RetailPrice, request.WholesalePrice,
                barcode: barcode, attributeValueIds: ids);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException(exception.Message);
        }
        await products.SaveChangesAsync(ct);
        return (await products.GetVariantByIdAsync(variant.Id, ct)
            ?? throw new NotFoundException("Variant", variant.Id)).ToResponse();
    }
}

public sealed class GetVariantBySkuHandler(IProductRepository products)
    : IRequestHandler<GetVariantBySkuQuery, VariantResponse>
{
    public async ValueTask<VariantResponse> Handle(GetVariantBySkuQuery request, CancellationToken ct) =>
        (await products.GetVariantBySkuAsync(request.Sku.Trim(), ct)
            ?? throw new NotFoundException("Variant SKU", request.Sku)).ToResponse();
}

public sealed class GetVariantByBarcodeHandler(IProductRepository products)
    : IRequestHandler<GetVariantByBarcodeQuery, VariantResponse>
{
    public async ValueTask<VariantResponse> Handle(GetVariantByBarcodeQuery request, CancellationToken ct) =>
        (await products.GetVariantByBarcodeAsync(request.Barcode.Trim(), ct)
            ?? throw new NotFoundException("Variant barcode", request.Barcode)).ToResponse();
}

public sealed class GetVariantHandler(IProductRepository products)
    : IRequestHandler<GetVariantQuery, VariantResponse>
{
    public async ValueTask<VariantResponse> Handle(GetVariantQuery request, CancellationToken ct) =>
        (await products.GetVariantByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Variant", request.Id)).ToResponse();
}

public sealed class DeactivateVariantHandler(IProductRepository products)
    : IRequestHandler<DeactivateVariantCommand>
{
    public async ValueTask<Unit> Handle(DeactivateVariantCommand request, CancellationToken ct)
    {
        var variant = await products.GetVariantByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Variant", request.Id);
        variant.Deactivate();
        await products.SaveChangesAsync(ct);
        return default;
    }
}
