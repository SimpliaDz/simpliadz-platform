using Mediator;
namespace SimpliaDz.Inventory.UseCases.Brands.Commands.ActivateBrand;
public sealed record ActivateBrandCommand(Guid Id) : IRequest;
