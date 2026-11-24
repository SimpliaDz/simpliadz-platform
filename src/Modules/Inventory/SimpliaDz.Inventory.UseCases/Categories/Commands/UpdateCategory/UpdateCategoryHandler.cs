using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Contracts.Categories;
using SimpliaDz.Inventory.UseCases.Categories.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Categories.Commands.UpdateCategory;

public sealed class UpdateCategoryHandler : IRequestHandler<UpdateCategoryCommand, CategoryResponse>
{
    private readonly ICategoryRepository _categories;

    public UpdateCategoryHandler(ICategoryRepository categories) => _categories = categories;

    public async ValueTask<CategoryResponse> Handle(UpdateCategoryCommand request, CancellationToken ct)
    {
        var category = await _categories.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Category", request.Id);
        var name = request.Name.Trim();

        if (request.ParentCategoryId.HasValue)
        {
            if (!await _categories.ExistsAsync(request.ParentCategoryId.Value, ct))
                throw new NotFoundException("Parent category", request.ParentCategoryId.Value);

            if (await _categories.WouldCreateCycleAsync(category.Id, request.ParentCategoryId.Value, ct))
                throw new ConflictException("The selected parent would create a circular category relationship.");
        }

        if (await _categories.ExistsByNameAsync(name, request.ParentCategoryId, category.Id, ct))
            throw new ConflictException($"Category '{name}' already exists under the selected parent.");

        category.Update(name, request.ParentCategoryId);
        var attributeIds = request.AttributeIds ?? [];
        if (attributeIds.Distinct().Count() != attributeIds.Count || attributeIds.Any(id => id == Guid.Empty) ||
            (await _categories.GetAttributesAsync(attributeIds, ct)).Count != attributeIds.Count)
            throw new ConflictException("One or more attributes do not exist.");
        category.AssignAttributes(attributeIds, request.RequiredAttributeIds ?? []);
        await _categories.SaveChangesAsync(ct);
        return category.ToResponse();
    }
}
