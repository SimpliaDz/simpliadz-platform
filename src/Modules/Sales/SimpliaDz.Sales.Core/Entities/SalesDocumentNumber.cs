namespace SimpliaDz.Sales.Core.Entities;

/// <summary>Immutable, legally traceable number assigned when a sale document is first issued.</summary>
public sealed class SalesDocumentNumber
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SaleId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public int IssueYear { get; private set; }
    public int SequenceNumber { get; private set; }
    public string DisplayNumber { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; } = DateTimeOffset.UtcNow;

    private SalesDocumentNumber() { }

    public SalesDocumentNumber(Guid saleId, string documentType, int issueYear, int sequenceNumber)
    {
        if (saleId == Guid.Empty) throw new ArgumentException("Sale is required.", nameof(saleId));
        if (string.IsNullOrWhiteSpace(documentType) || documentType.Trim().Length > 40)
            throw new ArgumentException("Document type is required and must fit 40 characters.", nameof(documentType));
        if (issueYear is < 2000 or > 9999) throw new ArgumentOutOfRangeException(nameof(issueYear));
        if (sequenceNumber is < 1 or > 999_999_999) throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        SaleId = saleId;
        DocumentType = documentType.Trim();
        IssueYear = issueYear;
        SequenceNumber = sequenceNumber;
        DisplayNumber = $"F{sequenceNumber:D9}";
    }
}
