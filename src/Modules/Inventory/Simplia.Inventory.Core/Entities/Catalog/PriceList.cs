namespace Simplia.Inventory.Core.Entities.Catalog;

public sealed class PriceList
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string CurrencyCode { get; private set; } = "DZD";
    public PriceListType Type { get; private set; }
    public bool IsActive { get; private set; } = true;
    private readonly List<PriceListItem> _items = [];
    public IReadOnlyCollection<PriceListItem> Items => _items.AsReadOnly();

    private PriceList() { }
    public PriceList(string name, string currencyCode, PriceListType type)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3 ||
            !currencyCode.Trim().All(char.IsLetter))
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currencyCode));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        Rename(name);
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        Type = type;
    }

    public void Deactivate() => IsActive = false;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
            throw new ArgumentException("Price list name must contain 1 to 150 characters.", nameof(name));
        Name = name.Trim();
    }

    public PriceListItem SetPrice(Guid variantId, decimal price)
    {
        var item = _items.FirstOrDefault(x => x.ProductVariantId == variantId);
        if (item is null)
        {
            item = new PriceListItem(Id, variantId, price);
            _items.Add(item);
        }
        else item.SetPrice(price);
        return item;
    }
}
