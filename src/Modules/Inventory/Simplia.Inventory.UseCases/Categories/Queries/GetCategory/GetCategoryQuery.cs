using Mediator;
using Simplia.Inventory.Contracts.Categories;

namespace Simplia.Inventory.UseCases.Categories.Queries.GetCategory;

public sealed record GetCategoryQuery(Guid Id) : IRequest<CategoryResponse>;
