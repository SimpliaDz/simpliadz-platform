using Mediator;
using SimpliaDz.Inventory.Contracts.Categories;

namespace SimpliaDz.Inventory.UseCases.Categories.Queries.GetCategories;

public sealed record GetCategoriesQuery : IRequest<IReadOnlyList<CategoryResponse>>;
