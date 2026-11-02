namespace SimpliaDz.Inventory.Core.Entities.Catalog;

public sealed class PriceListItem
{
    public Guid PriceListId { get; private set; }
    public PriceList PriceList { get; private set; } = default!;
    public Guid ProductVariantId { get; private set; }
    public ProductVariant ProductVariant { get; private set; } = default!;
    public decimal Price { get; private set; }

    private PriceListItem() { }
    public PriceListItem(Guid priceListId, Guid productVariantId, decimal price)
    {
        if (priceListId == Guid.Empty || productVariantId == Guid.Empty)
            throw new ArgumentException("Price list and variant are required.");
        PriceListId = priceListId;
        ProductVariantId = productVariantId;
        SetPrice(price);
    }

    public void SetPrice(decimal price)
    {
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price));
        Price = price;
    }
}
