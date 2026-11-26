using Mediator;
using Simplia.Inventory.Contracts.Categories;

namespace Simplia.Inventory.UseCases.Categories.Queries.GetCategories;

public sealed record GetCategoriesQuery : IRequest<IReadOnlyList<CategoryResponse>>;
