using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.UseCases.Categories.Abstractions;

namespace Simplia.Inventory.UseCases.Categories.Commands.DeactivateCategory;

public sealed class DeactivateCategoryHandler : IRequestHandler<DeactivateCategoryCommand>
{
    private readonly ICategoryRepository _categories;

    public DeactivateCategoryHandler(ICategoryRepository categories) => _categories = categories;

    public async ValueTask<Unit> Handle(DeactivateCategoryCommand request, CancellationToken ct)
    {
        var category = await _categories.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Category", request.Id);

        category.Deactivate();
        await _categories.SaveChangesAsync(ct);
        return default;
    }
}
