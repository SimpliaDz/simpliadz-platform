using Mediator;
using SimpliaDz.Inventory.Contracts.Categories;

namespace SimpliaDz.Inventory.UseCases.Categories.Queries.GetCategory;

public sealed record GetCategoryQuery(Guid Id) : IRequest<CategoryResponse>;
