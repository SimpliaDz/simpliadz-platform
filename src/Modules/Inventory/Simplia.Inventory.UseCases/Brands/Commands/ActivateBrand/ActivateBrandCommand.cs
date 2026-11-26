using Mediator;
namespace Simplia.Inventory.UseCases.Brands.Commands.ActivateBrand;
public sealed record ActivateBrandCommand(Guid Id) : IRequest;
