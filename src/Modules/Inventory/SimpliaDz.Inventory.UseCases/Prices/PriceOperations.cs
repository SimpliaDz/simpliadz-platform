using FluentValidation;
using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Contracts.Prices;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.UseCases.Prices.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Prices;

public sealed record CreatePriceListCommand(string Name, string CurrencyCode, string Type) : IRequest<PriceListResponse>;
public sealed record GetPriceListsQuery : IRequest<IReadOnlyList<PriceListResponse>>;
public sealed record SetVariantPriceCommand(Guid PriceListId, Guid VariantId, decimal Price) : IRequest<VariantPriceResponse>;
public sealed record ResolveVariantPriceQuery(Guid PriceListId, Guid VariantId) : IRequest<VariantPriceResponse>;

public sealed class CreatePriceListValidator : AbstractValidator<CreatePriceListCommand>
{
    public CreatePriceListValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CurrencyCode).NotEmpty().Matches("^[A-Za-z]{3}$");
        RuleFor(x => x.Type).Must(x => Enum.TryParse<PriceListType>(x, true, out var parsed) && Enum.IsDefined(parsed))
            .WithMessage("Type must be Retail or Wholesale.");
    }
}

public sealed class SetVariantPriceValidator : AbstractValidator<SetVariantPriceCommand>
{
    public SetVariantPriceValidator() => RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
}

internal static class PriceMapping
{
    public static PriceListResponse ToResponse(this PriceList list) =>
        new(list.Id, list.Name, list.CurrencyCode, list.Type.ToString(), list.IsActive);
    public static VariantPriceResponse ToResponse(this PriceList list, PriceListItem item) =>
        new(list.Id, item.ProductVariantId, list.CurrencyCode, item.Price);
}

public sealed class CreatePriceListHandler(IPriceListRepository repository)
    : IRequestHandler<CreatePriceListCommand, PriceListResponse>
{
    public async ValueTask<PriceListResponse> Handle(CreatePriceListCommand request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        if (await repository.ExistsByNameAsync(name, currency, ct))
            throw new ConflictException($"Price list '{name}' in {currency} already exists.");
        var list = new PriceList(name, currency, Enum.Parse<PriceListType>(request.Type, true));
        await repository.AddAsync(list, ct);
        await repository.SaveChangesAsync(ct);
        return list.ToResponse();
    }
}

public sealed class GetPriceListsHandler(IPriceListRepository repository)
    : IRequestHandler<GetPriceListsQuery, IReadOnlyList<PriceListResponse>>
{
    public async ValueTask<IReadOnlyList<PriceListResponse>> Handle(GetPriceListsQuery request, CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(x => x.ToResponse()).ToArray();
}

public sealed class SetVariantPriceHandler(IPriceListRepository repository)
    : IRequestHandler<SetVariantPriceCommand, VariantPriceResponse>
{
    public async ValueTask<VariantPriceResponse> Handle(SetVariantPriceCommand request, CancellationToken ct)
    {
        var list = await repository.GetByIdAsync(request.PriceListId, ct)
            ?? throw new NotFoundException("Price list", request.PriceListId);
        if (!list.IsActive) throw new ConflictException("Price list is inactive.");
        if (!await repository.VariantExistsAsync(request.VariantId, ct))
            throw new NotFoundException("Variant", request.VariantId);
        var item = list.SetPrice(request.VariantId, request.Price);
        await repository.SaveChangesAsync(ct);
        return list.ToResponse(item);
    }
}

public sealed class ResolveVariantPriceHandler(IPriceListRepository repository)
    : IRequestHandler<ResolveVariantPriceQuery, VariantPriceResponse>
{
    public async ValueTask<VariantPriceResponse> Handle(ResolveVariantPriceQuery request, CancellationToken ct)
    {
        var list = await repository.GetByIdAsync(request.PriceListId, ct)
            ?? throw new NotFoundException("Price list", request.PriceListId);
        var item = list.Items.FirstOrDefault(x => x.ProductVariantId == request.VariantId)
            ?? throw new NotFoundException("Variant price", request.VariantId);
        return list.ToResponse(item);
    }
}
