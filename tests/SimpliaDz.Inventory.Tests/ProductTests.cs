using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.UseCases.Products.Abstractions;
using SimpliaDz.Inventory.UseCases.Products.Commands.CreateProduct;
using SimpliaDz.Inventory.UseCases.Products.Commands.DeactivateProduct;
using SimpliaDz.Inventory.UseCases.Products.Commands.UpdateProduct;
using SimpliaDz.Inventory.UseCases.Products.Queries.GetProduct;
using SimpliaDz.Inventory.UseCases.Products.Queries.GetProducts;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

public sealed class ProductTests
{
    [Fact]
    public void Product_is_a_catalog_definition_without_stock_quantity()
    {
        var propertyNames = typeof(Product).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain(propertyNames, name =>
            name.Contains("Quantity", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Stock", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Validator_rejects_a_missing_product_name()
    {
        var validator = new CreateProductValidator();
        var result = await validator.ValidateAsync(
            new CreateProductCommand("", null, null, null, null));

        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }

    [Fact]
    public async Task Create_normalizes_code_and_accepts_valid_brand_and_category()
    {
        var repository = new InMemoryProductRepository();
        var brandId = repository.AddValidBrand();
        var categoryId = repository.AddValidCategory();
        var handler = new CreateProductHandler(repository);

        var id = await handler.Handle(
            new CreateProductCommand("Classic Cotton T-Shirt", " shirt-01 ", "Cotton", brandId, categoryId),
            CancellationToken.None);

        var product = await repository.GetByIdAsync(id, CancellationToken.None);
        Assert.NotNull(product);
        Assert.Equal("SHIRT-01", product.Code);
        Assert.Equal(brandId, product.BrandId);
        Assert.Equal(categoryId, product.ProductCategoryId);
    }

    [Fact]
    public async Task Create_rejects_an_unknown_brand()
    {
        var handler = new CreateProductHandler(new InMemoryProductRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new CreateProductCommand("Shirt", null, null, Guid.NewGuid(), null),
            CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Update_changes_the_catalog_definition_without_replacing_it()
    {
        var repository = new InMemoryProductRepository();
        var product = new Product("Old name", "OLD");
        repository.Seed(product);
        var handler = new UpdateProductHandler(repository);

        var response = await handler.Handle(
            new UpdateProductCommand(product.Id, "New name", "NEW", "Updated", null, null),
            CancellationToken.None);

        Assert.Equal(product.Id, response.Id);
        Assert.Equal("New name", response.Name);
        Assert.Equal("NEW", response.Code);
        Assert.Equal(1, repository.Count);
    }

    [Fact]
    public async Task Update_can_retain_a_category_that_was_deactivated_after_assignment()
    {
        var repository = new InMemoryProductRepository();
        var categoryId = Guid.NewGuid();
        var product = new Product("Shirt", "SHIRT");
        product.AssignCategory(categoryId);
        repository.Seed(product);

        var response = await new UpdateProductHandler(repository).Handle(
            new UpdateProductCommand(product.Id, "Updated Shirt", "SHIRT", null, null, categoryId),
            CancellationToken.None);

        Assert.Equal(categoryId, response.CategoryId);
        Assert.Equal("Updated Shirt", response.Name);
    }

    [Fact]
    public async Task Products_can_be_retrieved_and_searched()
    {
        var repository = new InMemoryProductRepository();
        var shirt = new Product("Classic Cotton T-Shirt", "SHIRT");
        var jeans = new Product("Slim Jeans", "JEANS");
        jeans.Deactivate();
        repository.Seed(shirt);
        repository.Seed(jeans);

        var single = await new GetProductHandler(repository)
            .Handle(new GetProductQuery(shirt.Id), CancellationToken.None);
        var search = await new GetProductsHandler(repository)
            .Handle(new GetProductsQuery("cotton", true), CancellationToken.None);

        Assert.Equal(shirt.Id, single.Id);
        Assert.Single(search);
        Assert.Equal(shirt.Id, search[0].Id);
    }

    [Fact]
    public async Task Deactivate_keeps_the_product_for_historical_references()
    {
        var repository = new InMemoryProductRepository();
        var product = new Product("Shirt", "SHIRT");
        repository.Seed(product);

        await new DeactivateProductHandler(repository)
            .Handle(new DeactivateProductCommand(product.Id), CancellationToken.None);

        Assert.False(product.IsActive);
        Assert.Equal(1, repository.Count);
        Assert.DoesNotContain(
            typeof(IProductRepository).GetMethods(),
            method => method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class InMemoryProductRepository : IProductRepository
    {
        private readonly List<Product> _products = [];
        private readonly HashSet<Guid> _brands = [];
        private readonly HashSet<Guid> _categories = [];

        public int Count => _products.Count;

        public void Seed(Product product) => _products.Add(product);

        public Guid AddValidBrand()
        {
            var id = Guid.NewGuid();
            _brands.Add(id);
            return id;
        }

        public Guid AddValidCategory()
        {
            var id = Guid.NewGuid();
            _categories.Add(id);
            return id;
        }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_products.FirstOrDefault(product => product.Id == id));

        public Task<IReadOnlyList<Product>> SearchAsync(string? search, bool? isActive, CancellationToken ct)
        {
            IEnumerable<Product> query = _products;

            if (isActive.HasValue)
                query = query.Where(product => product.IsActive == isActive.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(product =>
                    product.Name.Value.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (product.Code?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (product.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return Task.FromResult<IReadOnlyList<Product>>(query.ToArray());
        }

        public Task AddAsync(Product product, CancellationToken ct)
        {
            _products.Add(product);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsByCodeAsync(string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_products.Any(product =>
                product.Code == code && (!excludingId.HasValue || product.Id != excludingId.Value)));

        public Task<bool> IsValidBrandAsync(Guid brandId, CancellationToken ct) =>
            Task.FromResult(_brands.Contains(brandId));

        public Task<bool> IsValidCategoryAsync(Guid categoryId, CancellationToken ct) =>
            Task.FromResult(_categories.Contains(categoryId));

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
