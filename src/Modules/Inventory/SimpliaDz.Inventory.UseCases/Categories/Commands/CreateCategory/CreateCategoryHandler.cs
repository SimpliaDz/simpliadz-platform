using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Contracts.Categories;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.UseCases.Categories.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Categories.Commands.CreateCategory;

public sealed class CreateCategoryHandler : IRequestHandler<CreateCategoryCommand, CategoryResponse>
{
    private readonly ICategoryRepository _categories;

    public CreateCategoryHandler(ICategoryRepository categories) => _categories = categories;

    public async ValueTask<CategoryResponse> Handle(CreateCategoryCommand request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        await EnsureParentExistsAsync(request.ParentCategoryId, ct);

        if (await _categories.ExistsByNameAsync(name, request.ParentCategoryId, null, ct))
            throw new ConflictException($"Category '{name}' already exists under the selected parent.");

        var category = new ProductCategory(name, request.ParentCategoryId);
        await _categories.AddAsync(category, ct);
        await _categories.SaveChangesAsync(ct);
        return category.ToResponse();
    }

    private async Task EnsureParentExistsAsync(Guid? parentCategoryId, CancellationToken ct)
    {
        if (parentCategoryId.HasValue && !await _categories.ExistsAsync(parentCategoryId.Value, ct))
            throw new NotFoundException("Parent category", parentCategoryId.Value);
    }
}
