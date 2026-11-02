using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.UseCases.Brands.Abstractions;
using SimpliaDz.Inventory.UseCases.Brands.Commands.CreateBrand;
using SimpliaDz.Inventory.UseCases.Brands.Commands.UpdateBrand;
using SimpliaDz.Inventory.UseCases.Brands.Commands.DeactivateBrand;
using SimpliaDz.Inventory.UseCases.Brands.Queries.GetBrand;
using SimpliaDz.Inventory.UseCases.Brands.Queries.GetBrands;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

public sealed class BrandTests
{
    [Fact]
    public async Task Create_update_read_and_deactivate_preserve_identity()
    {
        var repository = new MemoryBrands();
        var created = await new CreateBrandHandler(repository)
            .Handle(new CreateBrandCommand("  Acme  "), CancellationToken.None);
        Assert.Equal("Acme", created.Name);

        var updated = await new UpdateBrandHandler(repository)
            .Handle(new UpdateBrandCommand(created.Id, "Acme Apparel"), CancellationToken.None);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Acme Apparel", (await new GetBrandHandler(repository)
            .Handle(new GetBrandQuery(created.Id), CancellationToken.None)).Name);

        await new DeactivateBrandHandler(repository)
            .Handle(new DeactivateBrandCommand(created.Id), CancellationToken.None);
        Assert.False(Assert.Single(await new GetBrandsHandler(repository)
            .Handle(new GetBrandsQuery(), CancellationToken.None)).IsActive);
        Assert.Equal(1, repository.Count);
    }

    [Fact]
    public async Task Duplicate_name_is_rejected_even_when_existing_brand_is_inactive()
    {
        var repository = new MemoryBrands();
        var brand = new Brand("Acme");
        brand.Deactivate();
        repository.Seed(brand);
        await Assert.ThrowsAsync<ConflictException>(() => new CreateBrandHandler(repository)
            .Handle(new CreateBrandCommand("Acme"), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Name_is_required_and_product_keeps_its_brand_after_deactivation()
    {
        var result = await new CreateBrandValidator().ValidateAsync(new CreateBrandCommand(" "));
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");

        var brand = new Brand("Acme");
        var product = new Product("Shirt");
        product.AssignBrand(brand.Id);
        brand.Deactivate();
        Assert.Equal(brand.Id, product.BrandId);
    }

    private sealed class MemoryBrands : IBrandRepository
    {
        private readonly List<Brand> _brands = [];
        public int Count => _brands.Count;
        public void Seed(Brand brand) => _brands.Add(brand);
        public Task<Brand?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_brands.FirstOrDefault(brand => brand.Id == id));
        public Task<IReadOnlyList<Brand>> GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Brand>>(_brands);
        public Task<bool> ExistsByNameAsync(string name, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_brands.Any(brand => brand.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                && (!excludingId.HasValue || brand.Id != excludingId.Value)));
        public Task AddAsync(Brand brand, CancellationToken ct) { _brands.Add(brand); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
