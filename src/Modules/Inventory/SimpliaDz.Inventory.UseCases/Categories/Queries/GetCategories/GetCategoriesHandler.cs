using Mediator;
using SimpliaDz.Inventory.Contracts.Categories;
using SimpliaDz.Inventory.UseCases.Categories.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Categories.Queries.GetCategories;

public sealed class GetCategoriesHandler : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryResponse>>
{
    private readonly ICategoryRepository _categories;

    public GetCategoriesHandler(ICategoryRepository categories) => _categories = categories;

    public async ValueTask<IReadOnlyList<CategoryResponse>> Handle(
        GetCategoriesQuery request,
        CancellationToken ct) =>
        (await _categories.GetAllAsync(ct)).ToHierarchy();
}
