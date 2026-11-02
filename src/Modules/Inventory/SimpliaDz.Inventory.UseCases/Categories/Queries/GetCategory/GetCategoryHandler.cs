using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Contracts.Categories;
using SimpliaDz.Inventory.UseCases.Categories.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Categories.Queries.GetCategory;

public sealed class GetCategoryHandler : IRequestHandler<GetCategoryQuery, CategoryResponse>
{
    private readonly ICategoryRepository _categories;

    public GetCategoryHandler(ICategoryRepository categories) => _categories = categories;

    public async ValueTask<CategoryResponse> Handle(GetCategoryQuery request, CancellationToken ct) =>
        (await _categories.GetAllAsync(ct)).FindSubtree(request.Id)
            ?? throw new NotFoundException("Category", request.Id);
}
