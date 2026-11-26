using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.Core.Entities.Catalog;
using Simplia.Inventory.UseCases.Prices;
using Simplia.Inventory.UseCases.Prices.Abstractions;
using Xunit;

namespace Simplia.Inventory.Tests;

public sealed class PriceListTests
{
    [Fact]
    public async Task Retail_and_wholesale_lists_resolve_distinct_decimal_prices()
    {
        var repository = new MemoryPrices();
        var retail = await new CreatePriceListHandler(repository).Handle(
            new CreatePriceListCommand("Retail", "dzd", "Retail"), CancellationToken.None);
        var wholesale = await new CreatePriceListHandler(repository).Handle(
            new CreatePriceListCommand("Wholesale", "DZD", "Wholesale"), CancellationToken.None);
        var variantId = repository.AddVariant();
        var setter = new SetVariantPriceHandler(repository);
        await setter.Handle(new SetVariantPriceCommand(retail.Id, variantId, 1500.25m), CancellationToken.None);
        await setter.Handle(new SetVariantPriceCommand(wholesale.Id, variantId, 900m), CancellationToken.None);

        var resolver = new ResolveVariantPriceHandler(repository);
        Assert.Equal(1500.25m, (await resolver.Handle(
            new ResolveVariantPriceQuery(retail.Id, variantId), CancellationToken.None)).Price);
        Assert.Equal(900m, (await resolver.Handle(
            new ResolveVariantPriceQuery(wholesale.Id, variantId), CancellationToken.None)).Price);
        Assert.Equal("DZD", retail.CurrencyCode);
    }

    [Fact]
    public async Task Setting_a_price_again_updates_the_same_entry()
    {
        var repository = new MemoryPrices();
        var list = new PriceList("Retail", "DZD", PriceListType.Retail);
        repository.Seed(list);
        var variantId = repository.AddVariant();
        var handler = new SetVariantPriceHandler(repository);
        await handler.Handle(new SetVariantPriceCommand(list.Id, variantId, 12m), CancellationToken.None);
        await handler.Handle(new SetVariantPriceCommand(list.Id, variantId, 15m), CancellationToken.None);
        Assert.Single(list.Items);
        Assert.Equal(15m, list.Items.Single().Price);
    }

    [Fact]
    public async Task Negative_prices_and_duplicate_list_names_are_rejected()
    {
        var validator = new SetVariantPriceValidator();
        Assert.False((await validator.ValidateAsync(
            new SetVariantPriceCommand(Guid.NewGuid(), Guid.NewGuid(), -1m))).IsValid);
        var repository = new MemoryPrices();
        repository.Seed(new PriceList("Retail", "DZD", PriceListType.Retail));
        await Assert.ThrowsAsync<ConflictException>(() => new CreatePriceListHandler(repository)
            .Handle(new CreatePriceListCommand("Retail", "dzd", "Retail"), CancellationToken.None).AsTask());
    }

    private sealed class MemoryPrices : IPriceListRepository
    {
        private readonly List<PriceList> _lists = [];
        private readonly HashSet<Guid> _variants = [];
        public void Seed(PriceList list) => _lists.Add(list);
        public Guid AddVariant() { var id = Guid.NewGuid(); _variants.Add(id); return id; }
        public Task<PriceList?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_lists.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<PriceList>> GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PriceList>>(_lists);
        public Task<IReadOnlyList<PriceList>> GetActiveByTypeAsync(PriceListType type,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PriceList>>(_lists
                .Where(list => list.IsActive && list.Type == type).ToArray());
        public Task<bool> ExistsByNameAsync(string name, string currencyCode, CancellationToken ct) =>
            Task.FromResult(_lists.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                x.CurrencyCode == currencyCode));
        public Task<bool> VariantExistsAsync(Guid variantId, CancellationToken ct) =>
            Task.FromResult(_variants.Contains(variantId));
        public Task AddAsync(PriceList list, CancellationToken ct)
        { _lists.Add(list); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
