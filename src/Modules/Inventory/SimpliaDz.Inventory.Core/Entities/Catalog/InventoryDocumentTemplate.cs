namespace SimpliaDz.Inventory.Core.Entities.Catalog;

public enum InventoryDocumentType { StockList = 1, CountSheet = 2, CountDifferences = 3 }

public sealed class InventoryDocumentTemplate
{
    private static readonly Dictionary<InventoryDocumentType, HashSet<string>> AllowedColumns = new()
    {
        [InventoryDocumentType.StockList] = ["Product", "Sku", "Barcode", "Quantity"],
        [InventoryDocumentType.CountSheet] = ["Product", "Sku", "Barcode", "Expected", "Counted"],
        [InventoryDocumentType.CountDifferences] = ["Product", "Sku", "Barcode", "Expected", "Counted", "Difference"]
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public InventoryDocumentType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Columns { get; private set; } = string.Empty;
    public string PaperSize { get; private set; } = "A4";
    public bool Landscape { get; private set; }
    public string? FooterText { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private InventoryDocumentTemplate() { }

    public InventoryDocumentTemplate(string name, InventoryDocumentType type, string title,
        IEnumerable<string> columns, string paperSize, bool landscape, string? footerText)
    {
        Type = type;
        Update(name, title, columns, paperSize, landscape, footerText);
    }

    public void Update(string name, string title, IEnumerable<string> columns,
        string paperSize, bool landscape, string? footerText)
    {
        if (!AllowedColumns.TryGetValue(Type, out var allowed))
            throw new ArgumentException("Document type is invalid.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(title) || title.Trim().Length > 150)
            throw new ArgumentException("Template name and title are required.");
        if (paperSize is not ("A4" or "A3"))
            throw new ArgumentException("Paper size must be A4 or A3.", nameof(paperSize));
        var selected = columns?.Select(x => x.Trim()).ToArray()
            ?? throw new ArgumentException("Columns are required.", nameof(columns));
        if (selected.Length == 0 || selected.Length > allowed.Count ||
            selected.Distinct(StringComparer.Ordinal).Count() != selected.Length ||
            selected.Any(x => !allowed.Contains(x)))
            throw new ArgumentException("Document columns must be distinct and supported for this type.", nameof(columns));
        if (footerText?.Length > 500)
            throw new ArgumentException("Footer cannot exceed 500 characters.", nameof(footerText));
        Name = name.Trim();
        Title = title.Trim();
        Columns = string.Join(',', selected);
        PaperSize = paperSize;
        Landscape = landscape;
        FooterText = string.IsNullOrWhiteSpace(footerText) ? null : footerText.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public IReadOnlyList<string> GetColumns() => Columns.Split(',');
    public void Deactivate() { IsActive = false; UpdatedAt = DateTimeOffset.UtcNow; }
}
