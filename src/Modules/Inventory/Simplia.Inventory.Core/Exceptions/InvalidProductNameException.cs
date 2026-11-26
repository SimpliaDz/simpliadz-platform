namespace Simplia.Inventory.Core.Exceptions;

public sealed class InvalidProductNameException : DomainException
{
    public InvalidProductNameException(string message) : base(message) { }
}
