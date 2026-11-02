using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.UseCases.Attributes;
using SimpliaDz.Inventory.UseCases.Attributes.Abstractions;
using Xunit;

namespace SimpliaDz.Inventory.Tests;

public sealed class AttributeTests
{
    [Fact]
    public async Task Size_and_color_use_the_same_model_and_values_are_ordered()
    {
        var repository = new MemoryAttributes();
        var create = new CreateAttributeHandler(repository);
        var size = await create.Handle(new CreateAttributeCommand("Size"), CancellationToken.None);
        var color = await create.Handle(new CreateAttributeCommand("Color"), CancellationToken.None);
        var add = new CreateAttributeValueHandler(repository);
        var large = await add.Handle(new CreateAttributeValueCommand(size.Id, "L", 2), CancellationToken.None);
        await add.Handle(new CreateAttributeValueCommand(size.Id, "S", 0), CancellationToken.None);
        await add.Handle(new CreateAttributeValueCommand(color.Id, "Black", 0), CancellationToken.None);

        var sizeResult = await new GetAttributeHandler(repository)
            .Handle(new GetAttributeQuery(size.Id), CancellationToken.None);
        Assert.Equal(["S", "L"], sizeResult.Values.Select(x => x.Value));
        Assert.All(sizeResult.Values, value => Assert.Equal(size.Id, value.AttributeId));

        await new SetAttributeValueOrderHandler(repository).Handle(
            new SetAttributeValueOrderCommand(size.Id, large.Id, 0), CancellationToken.None);
        var all = await new GetAttributesHandler(repository)
            .Handle(new GetAttributesQuery(), CancellationToken.None);
        Assert.Equal(2, all.Count);
        Assert.Equal(["L", "S"], all.Single(x => x.Id == size.Id).Values.Select(x => x.Value));
    }

    [Fact]
    public async Task Duplicate_values_are_blocked_within_one_attribute_but_allowed_across_attributes()
    {
        var repository = new MemoryAttributes();
        var first = new ProductAttribute("Color");
        var second = new ProductAttribute("Pattern");
        repository.Seed(first, second);
        var add = new CreateAttributeValueHandler(repository);
        await add.Handle(new CreateAttributeValueCommand(first.Id, "Black", 0), CancellationToken.None);
        await add.Handle(new CreateAttributeValueCommand(second.Id, "Black", 0), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => add.Handle(
            new CreateAttributeValueCommand(first.Id, " black ", 1), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Invalid_values_and_unknown_attributes_are_rejected()
    {
        var result = await new CreateAttributeValueValidator().ValidateAsync(
            new CreateAttributeValueCommand(Guid.NewGuid(), "", -1));
        Assert.Equal(2, result.Errors.Count);
        await Assert.ThrowsAsync<NotFoundException>(() => new CreateAttributeValueHandler(new MemoryAttributes())
            .Handle(new CreateAttributeValueCommand(Guid.NewGuid(), "M", 0), CancellationToken.None).AsTask());
    }

    private sealed class MemoryAttributes : IAttributeRepository
    {
        private readonly List<ProductAttribute> _attributes = [];
        public void Seed(params ProductAttribute[] attributes) => _attributes.AddRange(attributes);
        public Task<ProductAttribute?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_attributes.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<ProductAttribute>> GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProductAttribute>>(_attributes);
        public Task<bool> NameExistsAsync(string name, CancellationToken ct) =>
            Task.FromResult(_attributes.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        public Task AddAsync(ProductAttribute attribute, CancellationToken ct)
        { _attributes.Add(attribute); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
