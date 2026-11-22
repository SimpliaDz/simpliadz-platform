using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.UseCases.Categories.Abstractions;
using SimpliaDz.Inventory.UseCases.Categories.Commands.CreateCategory;
using SimpliaDz.Inventory.UseCases.Categories.Commands.DeactivateCategory;
using SimpliaDz.Inventory.UseCases.Categories.Commands.UpdateCategory;
using SimpliaDz.Inventory.UseCases.Categories.Queries.GetCategories;
using SimpliaDz.Inventory.UseCases.Categories.Queries.GetCategory;
using SimpliaDz.SharedKernel.Authorization;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

public sealed class CategoryTests
{
    [Fact]
    public async Task Validator_rejects_a_missing_category_name()
    {
        var result = await new CreateCategoryValidator().ValidateAsync(
            new CreateCategoryCommand("", null));

        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }

    [Fact]
    public async Task Category_can_be_created_as_a_root_or_under_a_parent()
    {
        var repository = new InMemoryCategoryRepository();
        var handler = new CreateCategoryHandler(repository);

        var men = await handler.Handle(new CreateCategoryCommand("Men", null), CancellationToken.None);
        var shirts = await handler.Handle(
            new CreateCategoryCommand("Shirts", men.Id),
            CancellationToken.None);

        Assert.Null(men.ParentCategoryId);
        Assert.Equal(men.Id, shirts.ParentCategoryId);
        Assert.Equal("Shirts", (await new GetCategoryHandler(repository).Handle(
            new GetCategoryQuery(shirts.Id), CancellationToken.None)).Name);
        await Assert.ThrowsAsync<NotFoundException>(() => new GetCategoryHandler(repository)
            .Handle(new GetCategoryQuery(Guid.NewGuid()), CancellationToken.None).AsTask());
        Assert.Equal(2, repository.Count);
    }

    [Fact]
    public async Task Same_name_is_allowed_under_different_parents_but_not_the_same_parent()
    {
        var repository = new InMemoryCategoryRepository();
        var men = new ProductCategory("Men");
        var women = new ProductCategory("Women");
        repository.Seed(men, women);
        var handler = new CreateCategoryHandler(repository);

        await handler.Handle(new CreateCategoryCommand("Shirts", men.Id), CancellationToken.None);
        await handler.Handle(new CreateCategoryCommand("Shirts", women.Id), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new CreateCategoryCommand("Shirts", men.Id),
            CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Category_can_be_updated_and_reparented()
    {
        var repository = new InMemoryCategoryRepository();
        var men = new ProductCategory("Men");
        var category = new ProductCategory("Tops");
        repository.Seed(men, category);

        var response = await new UpdateCategoryHandler(repository).Handle(
            new UpdateCategoryCommand(category.Id, "Shirts", men.Id),
            CancellationToken.None);

        Assert.Equal(category.Id, response.Id);
        Assert.Equal("Shirts", response.Name);
        Assert.Equal(men.Id, response.ParentCategoryId);
        Assert.Equal(2, repository.Count);
    }

    [Fact]
    public async Task Circular_parent_relationship_is_rejected()
    {
        var repository = new InMemoryCategoryRepository();
        var men = new ProductCategory("Men");
        var shirts = new ProductCategory("Shirts", men.Id);
        var casual = new ProductCategory("Casual", shirts.Id);
        repository.Seed(men, shirts, casual);

        await Assert.ThrowsAsync<ConflictException>(() => new UpdateCategoryHandler(repository).Handle(
            new UpdateCategoryCommand(men.Id, "Men", casual.Id),
            CancellationToken.None).AsTask());

        await Assert.ThrowsAsync<ConflictException>(() => new UpdateCategoryHandler(repository).Handle(
            new UpdateCategoryCommand(shirts.Id, "Shirts", shirts.Id),
            CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Categories_are_listed_as_a_hierarchy()
    {
        var repository = new InMemoryCategoryRepository();
        var men = new ProductCategory("Men");
        var shirts = new ProductCategory("Shirts", men.Id);
        var pants = new ProductCategory("Pants", men.Id);
        var women = new ProductCategory("Women");
        var dresses = new ProductCategory("Dresses", women.Id);
        repository.Seed(men, shirts, pants, women, dresses);

        var hierarchy = await new GetCategoriesHandler(repository)
            .Handle(new GetCategoriesQuery(), CancellationToken.None);

        Assert.Equal(2, hierarchy.Count);
        Assert.Equal(["Men", "Women"], hierarchy.Select(category => category.Name));
        Assert.Equal(["Pants", "Shirts"], hierarchy[0].Children.Select(category => category.Name));
        Assert.Equal("Dresses", Assert.Single(hierarchy[1].Children).Name);
    }

    [Fact]
    public async Task Deactivation_preserves_product_references_and_children()
    {
        var repository = new InMemoryCategoryRepository();
        var men = new ProductCategory("Men");
        var shirts = new ProductCategory("Shirts", men.Id);
        repository.Seed(men, shirts);
        var product = new Product("Classic Shirt");
        product.AssignCategory(shirts.Id);

        await new DeactivateCategoryHandler(repository)
            .Handle(new DeactivateCategoryCommand(shirts.Id), CancellationToken.None);

        Assert.False(shirts.IsActive);
        Assert.Equal(shirts.Id, product.ProductCategoryId);
        Assert.Equal(men.Id, shirts.ParentCategoryId);
        Assert.Equal(2, repository.Count);
        Assert.Contains(PermissionNames.Categories.View, PermissionNames.All);
        Assert.Contains(PermissionNames.Categories.Create, PermissionNames.All);
        Assert.Contains(PermissionNames.Categories.Edit, PermissionNames.All);
    }

    private sealed class InMemoryCategoryRepository : ICategoryRepository
    {
        private readonly List<ProductCategory> _categories = [];

        public int Count => _categories.Count;

        public void Seed(params ProductCategory[] categories) => _categories.AddRange(categories);

        public Task<ProductCategory?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_categories.FirstOrDefault(category => category.Id == id));

        public Task<IReadOnlyList<ProductCategory>> GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProductCategory>>(_categories);

        public Task<bool> ExistsAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_categories.Any(category => category.Id == id));

        public Task<bool> ExistsByNameAsync(
            string name,
            Guid? parentCategoryId,
            Guid? excludingId,
            CancellationToken ct) =>
            Task.FromResult(_categories.Any(category =>
                category.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                category.ParentCategoryId == parentCategoryId &&
                (!excludingId.HasValue || category.Id != excludingId.Value)));

        public Task<bool> WouldCreateCycleAsync(
            Guid categoryId,
            Guid parentCategoryId,
            CancellationToken ct)
        {
            var visited = new HashSet<Guid>();
            Guid? currentId = parentCategoryId;

            while (currentId.HasValue)
            {
                if (currentId.Value == categoryId || !visited.Add(currentId.Value))
                    return Task.FromResult(true);

                currentId = _categories
                    .FirstOrDefault(category => category.Id == currentId.Value)
                    ?.ParentCategoryId;
            }

            return Task.FromResult(false);
        }

        public Task AddAsync(ProductCategory category, CancellationToken ct)
        {
            _categories.Add(category);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
