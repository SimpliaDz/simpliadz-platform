using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.UseCases.Categories.Abstractions;
namespace Simplia.Inventory.UseCases.Categories.Commands.ActivateCategory;
public sealed class ActivateCategoryHandler(ICategoryRepository categories) : IRequestHandler<ActivateCategoryCommand>
{
    public async ValueTask<Unit> Handle(ActivateCategoryCommand request, CancellationToken ct)
    { var category = await categories.GetByIdAsync(request.Id, ct) ?? throw new NotFoundException("Category", request.Id); category.Activate(); await categories.SaveChangesAsync(ct); return default; }
}
