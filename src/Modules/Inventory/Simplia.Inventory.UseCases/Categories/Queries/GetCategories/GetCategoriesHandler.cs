using Mediator;
using Simplia.Inventory.Contracts.Categories;
using Simplia.Inventory.UseCases.Categories.Abstractions;

namespace Simplia.Inventory.UseCases.Categories.Queries.GetCategories;

public sealed class GetCategoriesHandler : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryResponse>>
{
    private readonly ICategoryRepository _categories;

    public GetCategoriesHandler(ICategoryRepository categories) => _categories = categories;

    public async ValueTask<IReadOnlyList<CategoryResponse>> Handle(
        GetCategoriesQuery request,
        CancellationToken ct) =>
        (await _categories.GetAllAsync(ct)).ToHierarchy();
}
