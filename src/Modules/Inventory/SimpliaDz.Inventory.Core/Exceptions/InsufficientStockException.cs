namespace SimpliaDz.Inventory.Core.Exceptions;

public sealed class InsufficientStockException : DomainException
{
    public InsufficientStockException() : base("Stock cannot go below zero.") { }
    public InsufficientStockException(int available) : base($"Only {available} in stock at this shop.") { }
}
