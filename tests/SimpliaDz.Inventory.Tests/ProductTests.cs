using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.UseCases.Products.Abstractions;
using SimpliaDz.Inventory.UseCases.Products.Commands.CreateProduct;
using SimpliaDz.Inventory.UseCases.Products.Commands.DeactivateProduct;
using SimpliaDz.Inventory.UseCases.Products.Commands.UpdateProduct;
using SimpliaDz.Inventory.UseCases.Products.Queries.GetProduct;
using SimpliaDz.Inventory.UseCases.Products.Queries.GetProducts;
using SimpliaDz.Inventory.UseCases.Variants;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

public sealed class ProductTests
{
    [Fact]
    public async Task Generate_barcode_only_for_an_active_variant_without_a_barcode()
    {
        var repository = new InMemoryProductRepository();
        var product = new Product("T-Shirt");
        var variant = product.AddVariant("TS-S", 100m);
        var factory = product.AddVariant("TS-M", 100m, barcode: "FACTORY-1");
        repository.Seed(product);
        var handler = new GenerateVariantBarcodeHandler(repository);

        var generated = await handler.Handle(new GenerateVariantBarcodeCommand(variant.Id),
            CancellationToken.None);
        Assert.Equal(variant.Barcode, generated.Barcode);
        Assert.Equal(variant.Id, (await repository.GetVariantByBarcodeAsync(generated.Barcode!,
            CancellationToken.None))!.Id);
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new GenerateVariantBarcodeCommand(factory.Id), CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new GenerateVariantBarcodeCommand(variant.Id), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Generated_barcode_retries_a_unique_index_collision()
    {
        var repository = new InMemoryProductRepository();
        var product = new Product("T-Shirt");
        var variant = product.AddVariant("TS-M", 100m);
        repository.Seed(product);
        repository.RejectNextGeneratedBarcode();

        var result = await new GenerateVariantBarcodeHandler(repository).Handle(
            new GenerateVariantBarcodeCommand(variant.Id), CancellationToken.None);

        Assert.Equal(2, repository.GeneratedBarcodeAttempts);
        Assert.True(InternalBarcode.IsValidEan13(result.Barcode));
    }

    [Fact]
    public async Task Variant_sku_and_barcode_can_be_corrected_without_changing_its_identity()
    {
        var repository = new InMemoryProductRepository();
        var product = new Product("T-Shirt");
        var variant = product.AddVariant("OLD-SKU", 100m, barcode: "OLD-BARCODE");
        var other = product.AddVariant("TAKEN-SKU", 100m, barcode: "TAKEN-BARCODE");
        repository.Seed(product);
        var handler = new UpdateVariantIdentifiersHandler(repository);

        var updated = await handler.Handle(new UpdateVariantIdentifiersCommand(variant.Id,
            "NEW-SKU", "NEW-BARCODE"), CancellationToken.None);
        Assert.Equal(variant.Id, updated.Id);
        Assert.Equal("NEW-SKU", updated.Sku);
        Assert.Equal("NEW-BARCODE", updated.Barcode);
        Assert.Equal(other.Id, (await repository.GetVariantBySkuAsync("TAKEN-SKU",
            CancellationToken.None))!.Id);
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new UpdateVariantIdentifiersCommand(variant.Id, "TAKEN-SKU", null),
            CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new UpdateVariantIdentifiersCommand(variant.Id, "NEW-SKU", "TAKEN-BARCODE"),
            CancellationToken.None).AsTask());
        variant.Deactivate();
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new UpdateVariantIdentifiersCommand(variant.Id, "OTHER", null),
            CancellationToken.None).AsTask());
    }

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
        private bool _rejectNextGeneratedBarcode;

        public int Count => _products.Count;
        public int GeneratedBarcodeAttempts { get; private set; }

        public void Seed(Product product) => _products.Add(product);
        public void RejectNextGeneratedBarcode() => _rejectNextGeneratedBarcode = true;

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

        public Task<ProductVariant?> GetVariantByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_products.SelectMany(x => x.Variants).FirstOrDefault(x => x.Id == id));
        public Task<ProductVariant?> GetVariantBySkuAsync(string sku, CancellationToken ct) =>
            Task.FromResult(_products.SelectMany(x => x.Variants).FirstOrDefault(x => x.Sku.Value == sku));
        public Task<ProductVariant?> GetVariantByBarcodeAsync(string barcode, CancellationToken ct) =>
            Task.FromResult(_products.SelectMany(x => x.Variants).FirstOrDefault(x => x.Barcode == barcode));
        public Task<bool> SkuExistsAsync(string sku, CancellationToken ct) =>
            Task.FromResult(_products.SelectMany(x => x.Variants).Any(x => x.Sku.Value == sku));
        public Task<bool> BarcodeExistsAsync(string barcode, CancellationToken ct) =>
            Task.FromResult(_products.SelectMany(x => x.Variants).Any(x => x.Barcode == barcode));
        public Task<bool> TryAssignGeneratedBarcodeAsync(Guid variantId, string barcode, CancellationToken ct)
        {
            GeneratedBarcodeAttempts++;
            if (_rejectNextGeneratedBarcode)
            {
                _rejectNextGeneratedBarcode = false;
                throw new ConflictException("Generated barcode conflicts with an existing barcode.");
            }
            return Task.FromResult(_products.SelectMany(x => x.Variants).Any(x =>
                x.Id == variantId && x.Barcode == barcode));
        }
        public Task<bool> TryUpdateVariantIdentifiersAsync(Guid variantId, string currentSku,
            string? currentBarcode, string sku, string? barcode, CancellationToken ct)
        {
            var variant = _products.SelectMany(x => x.Variants).FirstOrDefault(x =>
                x.Id == variantId && x.IsActive && x.Sku.Value == currentSku &&
                x.Barcode == currentBarcode);
            if (variant is null) return Task.FromResult(false);
            variant.SetSku(new SimpliaDz.Inventory.Core.ValueObjects.Sku(sku));
            variant.SetBarcode(barcode);
            return Task.FromResult(true);
        }
        public Task<IReadOnlyList<AttributeValue>> GetAttributeValuesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AttributeValue>>([]);

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

        public Task AddVariantAsync(ProductVariant variant, CancellationToken ct) =>
            Task.CompletedTask;

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
